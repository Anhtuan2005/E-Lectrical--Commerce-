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
        return View(await _cartService.GetCartAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.Session.Id));
    }

    [HttpPost]
    public async Task<IActionResult> Add(int productId, int quantity = 1)
    {
        await _cartService.AddAsync(productId, quantity, User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.Session.Id);
        return await CartJson("Đã thêm vào giỏ hàng.");
    }

    [HttpPost]
    public async Task<IActionResult> Update(int productId, int quantity)
    {
        await _cartService.UpdateQuantityAsync(productId, quantity, User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.Session.Id);
        return await CartJson("Đã cập nhật giỏ hàng.");
    }

    [HttpPost]
    public async Task<IActionResult> Remove(int productId)
    {
        await _cartService.RemoveAsync(productId, User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.Session.Id);
        return await CartJson("Đã xoá sản phẩm khỏi giỏ hàng.");
    }

    private async Task<IActionResult> CartJson(string message)
    {
        var cart = await _cartService.GetCartAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.Session.Id);
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
}
