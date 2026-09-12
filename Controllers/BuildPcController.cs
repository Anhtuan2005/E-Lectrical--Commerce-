using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace EcommerceApp.Controllers;

public class BuildPcController : Controller
{
    private readonly AppDbContext _db;
    private readonly ICartService _cartService;
    private readonly ISmartPcBuildService _builder;

    public BuildPcController(AppDbContext db, ICartService cartService, ISmartPcBuildService builder)
    {
        _db = db;
        _cartService = cartService;
        _builder = builder;
    }

    public async Task<IActionResult> Index()
    {
        var model = new BuildPcViewModel
        {
            SlotProducts = await _builder.GetSlotProductsAsync(),
            Goals = _builder.GetGoals()
        };

        return View(model);
    }

    public IActionResult Preview3d()
    {
        TempData["Success"] = "Smart PC Builder đã thay thế preview 3D. Bạn có thể dựng cấu hình ngay tại đây.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult MeasureModels()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("ai-chat")]
    public async Task<IActionResult> SmartBuild([FromBody] SmartBuildRequest? request)
    {
        return Json(await _builder.BuildAsync(request));
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts(string slot)
    {
        var products = await _builder.GetProductsForSlotAsync(slot, 20);

        return Json(products.Select(product => new
        {
            id = product.Id,
            name = product.Name,
            price = product.SalePrice.ToString("N0") + " ₫",
            priceRaw = (long)product.SalePrice,
            imageUrl = product.PrimaryImageUrl,
            category = product.Category?.Name,
            stock = product.Stock
        }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart([FromForm] int[] productIds, [FromForm] string[] slots, [FromForm] string? groupName)
    {
        if (User.IsInRole("Admin"))
        {
            return Json(new
            {
                success = false,
                message = "Tài khoản admin chỉ được xem và kiểm tra, không thể mua cấu hình.",
                itemCount = 0
            });
        }

        var selectedItems = productIds
            .Select((id, index) => new
            {
                ProductId = id,
                Slot = index < slots.Length ? slots[index] : string.Empty
            })
            .Where(item => item.ProductId > 0)
            .GroupBy(item => item.ProductId)
            .Select(group => group.First())
            .ToList();

        if (selectedItems.Count == 0)
        {
            return Json(new { success = false, message = "Vui lòng chọn ít nhất một linh kiện." });
        }

        var cleanIds = selectedItems.Select(item => item.ProductId).ToArray();
        var validIds = await _db.Products
            .Where(product => cleanIds.Contains(product.Id) && product.Stock > 0)
            .Select(product => product.Id)
            .ToListAsync();
        var validIdSet = validIds.ToHashSet();
        var groupKey = $"smartpc-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..31];
        var safeGroupName = string.IsNullOrWhiteSpace(groupName) ? "Bộ cấu hình Smart PC" : groupName.Trim();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var sessionId = GetStableCartSessionId(userId);

        var addedCount = 0;
        foreach (var item in selectedItems.Where(item => validIdSet.Contains(item.ProductId)))
        {
            try
            {
                var slotIndex = Array.FindIndex(PcSlots.All, slot => slot.Equals(item.Slot, StringComparison.OrdinalIgnoreCase));
                await _cartService.AddAsync(
                    item.ProductId,
                    1,
                    userId,
                    sessionId,
                    new CartItemGroupInput
                    {
                        Key = groupKey,
                        Name = safeGroupName,
                        Source = "SmartPC",
                        ItemLabel = slotIndex >= 0 ? SmartPcBuildEngine.GetSlotLabel(PcSlots.All[slotIndex]) : item.Slot,
                        SortOrder = slotIndex >= 0 ? slotIndex : 999
                    });
                addedCount++;
            }
            catch (InvalidOperationException)
            {
                // Stock may have changed after the slot list was rendered.
            }
        }

        var cartCount = await _cartService.GetCountAsync(userId, sessionId);
        return Json(new
        {
            success = addedCount > 0,
            message = addedCount > 0 ? $"Đã thêm {addedCount} linh kiện vào giỏ hàng." : "Các linh kiện đã chọn hiện không còn hàng.",
            itemCount = cartCount,
            redirectUrl = Url.Action("Index", "Cart")
        });
    }

    private string GetStableCartSessionId(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            HttpContext.Session.SetString("CartSession", "active");
        }

        return HttpContext.Session.Id;
    }

}
