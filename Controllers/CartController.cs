using EcommerceApp.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EcommerceApp.Controllers;

public class CartController : Controller
{
    private readonly ICartService _cartService;

    public CartController(ICartService cartService)
    {
        _cartService = cartService;
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return View(await _cartService.GetCartAsync(userId, GetStableCartSessionId(userId)));
    }

    [HttpPost]
    public async Task<IActionResult> Add(int productId, int quantity = 1)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _cartService.AddAsync(productId, quantity, userId, GetStableCartSessionId(userId));
        return await CartJson("Đã thêm vào giỏ hàng.");
    }

    [HttpPost]
    public async Task<IActionResult> Update(int productId, int quantity)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _cartService.UpdateQuantityAsync(productId, quantity, userId, GetStableCartSessionId(userId));
        return await CartJson("Đã cập nhật giỏ hàng.");
    }

    [HttpPost]
    public async Task<IActionResult> Remove(int productId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _cartService.RemoveAsync(productId, userId, GetStableCartSessionId(userId));
        return await CartJson("Đã xoá sản phẩm khỏi giỏ hàng.");
    }

    private async Task<IActionResult> CartJson(string message)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var cart = await _cartService.GetCartAsync(userId, GetStableCartSessionId(userId));
        return Json(new
        {
            success = true,
            message,
            itemCount = cart.ItemCount,
            total = cart.Total.ToString("N0") + " ₫",
            items = cart.Items.Select(item => new
            {
                productId = item.ProductId,
                quantity = item.Quantity,
                lineTotal = ((item.Product?.SalePrice ?? item.Product?.Price ?? 0) * item.Quantity).ToString("N0") + " ₫"
            })
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
