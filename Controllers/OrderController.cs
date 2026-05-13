using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EcommerceApp.Controllers;

[Authorize]
public class OrderController : Controller
{
    private readonly IOrderService _orderService;
    private readonly ICartService _cartService;
    private readonly IVnpayService _vnpayService;
    private readonly UserManager<ApplicationUser> _userManager;

    public OrderController(
        IOrderService orderService,
        ICartService cartService,
        IVnpayService vnpayService,
        UserManager<ApplicationUser> userManager)
    {
        _orderService = orderService;
        _cartService = cartService;
        _vnpayService = vnpayService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Checkout()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var cart = await _cartService.GetCartAsync(userId, HttpContext.Session.Id);
        if (!cart.Items.Any())
        {
            TempData["Error"] = "Giỏ hàng đang trống.";
            return RedirectToAction("Index", "Cart");
        }

        var user = await _userManager.GetUserAsync(User);
        return View(new CheckoutViewModel
        {
            Cart = cart,
            RecipientName = user?.FullName ?? string.Empty,
            RecipientPhone = user?.PhoneNumber ?? string.Empty,
            ProfileAddress = user?.Address
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(CheckoutViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        model.Cart = await _cartService.GetCartAsync(userId, HttpContext.Session.Id);
        model.ProfileAddress = (await _userManager.GetUserAsync(User))?.Address;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var order = await _orderService.CreateOrderAsync(userId, model, HttpContext.Session.Id);
            if (model.PaymentMethod == "VNPAY")
            {
                return Redirect(_vnpayService.CreatePaymentUrl(order, HttpContext));
            }

            return RedirectToAction(nameof(Confirmation), new { id = order.Id });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return View(model);
        }
    }

    public async Task<IActionResult> Confirmation(int id)
    {
        var order = await _orderService.GetOrderAsync(id);
        return order is null || order.UserId != User.FindFirstValue(ClaimTypes.NameIdentifier)
            ? NotFound()
            : View(order);
    }

    public async Task<IActionResult> History(string? status)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return View(await _orderService.GetUserOrderHistoryAsync(userId, status));
    }

    public async Task<IActionResult> Detail(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var order = await _orderService.GetUserOrderAsync(id, userId);
        return order is null ? NotFound() : View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason, string? returnStatus)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var cancelled = await _orderService.CancelUserOrderAsync(id, userId, reason);
        if (!cancelled)
        {
            TempData["Error"] = "Chỉ có thể hủy đơn đang chờ xác nhận.";
            return RedirectToAction(nameof(History), new { status = returnStatus });
        }

        TempData["Success"] = "Đã hủy đơn hàng.";
        return RedirectToAction(nameof(History), new { status = returnStatus });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var added = await _orderService.ReorderAsync(id, userId, HttpContext.Session.Id);
        if (added == 0)
        {
            TempData["Error"] = "Các sản phẩm trong đơn hiện chưa còn hàng để mua lại.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        TempData["Success"] = "Đã thêm sản phẩm còn hàng vào giỏ.";
        return RedirectToAction("Index", "Cart");
    }
}
