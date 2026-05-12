using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EcommerceApp.Controllers;

[Authorize]
public class OrderController : Controller
{
    private readonly IOrderService _orderService;
    private readonly ICartService _cartService;
    private readonly IVnpayService _vnpayService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;

    public OrderController(
        IOrderService orderService,
        ICartService cartService,
        IVnpayService vnpayService,
        UserManager<ApplicationUser> userManager,
        AppDbContext db)
    {
        _orderService = orderService;
        _cartService = cartService;
        _vnpayService = vnpayService;
        _userManager = userManager;
        _db = db;
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
        return order is null || order.UserId != User.FindFirstValue(ClaimTypes.NameIdentifier) ? NotFound() : View(order);
    }

    public async Task<IActionResult> History()
    {
        return View(await _orderService.GetUserOrdersAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string reason)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var order = await _db.Orders.FirstOrDefaultAsync(row => row.Id == id && row.UserId == userId);
        if (order is null)
        {
            return NotFound();
        }

        if (order.Status != OrderStatuses.Pending)
        {
            TempData["Error"] = "Chỉ có thể huỷ đơn đang chờ xác nhận.";
            return RedirectToAction(nameof(History));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["Error"] = "Vui lòng nhập lý do huỷ đơn.";
            return RedirectToAction(nameof(History));
        }

        order.Status = OrderStatuses.Cancelled;
        order.CancelledReason = reason.Trim();
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã huỷ đơn hàng.";
        return RedirectToAction(nameof(History));
    }
}
