using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Controllers;

public class BuildPcController : Controller
{
    private readonly AppDbContext _db;

    public BuildPcController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var model = new BuildPcViewModel
        {
            SlotProducts = await LoadSlotProductsAsync()
        };

        return View(model);
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
        var products = await _db.Products
            .Include(product => product.Category)
            .Where(product => product.Stock > 0)
            .ToListAsync();

        var matchedProducts = products
            .Where(product => keywords.Any(keyword => MatchesKeyword(product, keyword)))
            .OrderByDescending(product => product.IsFeatured)
            .ThenBy(product => product.SalePrice)
            .Take(take)
            .ToList();

        if (matchedProducts.Any())
        {
            return matchedProducts;
        }

        return products
            .OrderByDescending(product => product.IsFeatured)
            .ThenBy(product => product.SalePrice)
            .Take(take)
            .ToList();
    }

    private static bool MatchesKeyword(Product product, string keyword)
    {
        return product.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            || product.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            || (product.Category?.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false);
    }

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
