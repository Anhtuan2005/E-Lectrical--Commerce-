using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EcommerceApp.Controllers;

public class BuildPcController : Controller
{
    private readonly AppDbContext _db;
    private readonly ICartService _cartService;

    public BuildPcController(AppDbContext db, ICartService cartService)
    {
        _db = db;
        _cartService = cartService;
    }

    public async Task<IActionResult> Index()
    {
        var model = new BuildPcViewModel
        {
            SlotProducts = await LoadSlotProductsAsync()
        };

        return View(model);
    }

    public IActionResult Preview3d()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts(string slot)
    {
        var products = await GetProductsForSlotAsync(slot, 20);

        return Json(products.Select(product => new
        {
            id = product.Id,
            name = product.Name,
            price = product.SalePrice.ToString("N0") + " ₫",
            priceRaw = (long)product.SalePrice,
            imageUrl = product.ImageUrl,
            category = product.Category?.Name,
            stock = product.Stock
        }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart([FromForm] int[] productIds)
    {
        var cleanIds = productIds.Where(id => id > 0).Distinct().ToArray();
        if (cleanIds.Length == 0)
        {
            return Json(new { success = false, message = "Vui lòng chọn ít nhất một linh kiện." });
        }

        var validIds = await _db.Products
            .Where(product => cleanIds.Contains(product.Id) && product.Stock > 0)
            .Select(product => product.Id)
            .ToListAsync();

        foreach (var productId in validIds)
        {
            await _cartService.AddAsync(productId, 1, User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.Session.Id);
        }

        var cartCount = await _cartService.GetCountAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.Session.Id);
        return Json(new
        {
            success = validIds.Count > 0,
            message = validIds.Count > 0 ? $"Đã thêm {validIds.Count} linh kiện vào giỏ hàng." : "Các linh kiện đã chọn hiện không còn hàng.",
            itemCount = cartCount,
            redirectUrl = Url.Action("Index", "Cart")
        });
    }

    private async Task<Dictionary<string, List<Product>>> LoadSlotProductsAsync()
    {
        var result = new Dictionary<string, List<Product>>();
        foreach (var slot in PcSlots.All)
        {
            result[slot] = await GetProductsForSlotAsync(slot, 10);
        }

        return result;
    }

    private async Task<List<Product>> GetProductsForSlotAsync(string slot, int take)
    {
        var keywords = GetKeywordsForSlot(slot);
        var categorySlugs = GetCategorySlugsForSlot(slot);
        var products = await _db.Products
            .Include(product => product.Category)
            .Where(product => product.Stock > 0)
            .ToListAsync();

        var categoryProducts = products
            .Where(product => product.Category is not null && categorySlugs.Contains(product.Category.Slug, StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(product => product.IsFeatured)
            .ThenBy(product => product.SalePrice)
            .Take(take)
            .ToList();

        if (categoryProducts.Any())
        {
            return categoryProducts;
        }

        // Fallback keyword matching supports legacy DBs where admins have not split component categories yet.
        return products
            .Where(product => keywords.Any(keyword => MatchesKeyword(product, keyword)))
            .OrderByDescending(product => product.IsFeatured)
            .ThenBy(product => product.SalePrice)
            .Take(take)
            .ToList();
    }

    private static bool MatchesKeyword(Product product, string keyword)
    {
        return product.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            || (product.Category?.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static string[] GetCategorySlugsForSlot(string slot) => slot switch
    {
        "CPU" => new[] { "cpu" },
        "VGA" => new[] { "vga" },
        "RAM" => new[] { "ram" },
        "SSD" => new[] { "ssd" },
        "Mainboard" => new[] { "mainboard" },
        "PSU" => new[] { "psu" },
        "Case" => new[] { "case" },
        "Cooling" => new[] { "cooling" },
        _ => Array.Empty<string>()
    };

    // These terms cover common Vietnamese and English component naming used by Techvora admins.
    private static string[] GetKeywordsForSlot(string slot) => slot switch
    {
        "CPU" => new[] { "CPU", "Processor", "Ryzen", "Core i", "Intel", "AMD" },
        "VGA" => new[] { "VGA", "GPU", "RTX", "RX ", "GeForce", "Radeon" },
        "RAM" => new[] { "RAM", "DDR4", "DDR5", "Memory" },
        "SSD" => new[] { "SSD", "NVMe", "M.2", "SATA SSD" },
        "Mainboard" => new[] { "Mainboard", "Motherboard", "Bo mạch" },
        "PSU" => new[] { "PSU", "Nguồn", "Power Supply", "550W", "650W", "750W", "850W" },
        "Case" => new[] { "Case", "Thùng máy", "Vỏ máy" },
        "Cooling" => new[] { "Tản nhiệt", "Cooler", "AIO", "Fan" },
        _ => new[] { slot }
    };
}

public static class PcSlots
{
    public static readonly string[] All = { "CPU", "VGA", "RAM", "SSD", "Mainboard", "PSU", "Case", "Cooling" };
}
