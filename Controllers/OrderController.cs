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
    private readonly IShippingFeeService _shippingFeeService;
    private readonly IVnpayService _vnpayService;
    private readonly UserManager<ApplicationUser> _userManager;

    public OrderController(
        IOrderService orderService,
        ICartService cartService,
        IShippingFeeService shippingFeeService,
        IVnpayService vnpayService,
        UserManager<ApplicationUser> userManager)
    {
        _orderService = orderService;
        _cartService = cartService;
        _shippingFeeService = shippingFeeService;
        _vnpayService = vnpayService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Checkout()
    {
        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "Tài khoản admin chỉ được xem và kiểm tra, không thể đặt hàng.";
            return RedirectToAction("Index", "Product");
        }

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
            ShippingFee = _shippingFeeService.Calculate(null, null, cart.Total).Fee,
            ProfileAddress = user?.Address
        });
    }

    [HttpGet]
    public async Task<IActionResult> ShippingFee(string? province, string? district)
    {
        if (User.IsInRole("Admin"))
        {
            return Json(new
            {
                ready = false,
                fee = 0,
                formattedFee = "0 đ",
                subtotal = 0,
                total = 0,
                formattedTotal = "0 đ",
                zone = "",
                eta = "",
                message = "Tài khoản admin không thể đặt hàng."
            });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var cart = await _cartService.GetCartAsync(userId, HttpContext.Session.Id);
        var quote = _shippingFeeService.Calculate(province, district, cart.Total);
        var total = cart.Total + quote.Fee;

        return Json(new
        {
            ready = quote.Ready,
            fee = quote.Fee,
            formattedFee = quote.Fee.ToString("N0") + " đ",
            subtotal = cart.Total,
            total,
            formattedTotal = total.ToString("N0") + " đ",
            zone = quote.Zone,
            eta = quote.Eta,
            message = quote.Message
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(CheckoutViewModel model)
    {
        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "Tài khoản admin chỉ được xem và kiểm tra, không thể đặt hàng.";
            return RedirectToAction("Index", "Product");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        model.Cart = await _cartService.GetCartAsync(userId, HttpContext.Session.Id);
        model.ProfileAddress = (await _userManager.GetUserAsync(User))?.Address;
        model.ShippingFee = _shippingFeeService.Calculate(model.Province, model.District, model.Cart.Total).Fee;
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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var order = await _orderService.GetOrderAsync(id);
        if (order is null || order.UserId != userId)
        {
            return NotFound();
        }

        if (IsUnpaidVnpayOrder(order))
        {
            TempData["Error"] = "Đơn VNPAY chưa thanh toán thành công nên chưa được xác nhận.";
            return RedirectToAction(nameof(Detail), new { id = order.Id });
        }

        return View(order);
    }

    public async Task<IActionResult> History(string? status, int page = 1)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return View(await _orderService.GetUserOrderHistoryAsync(userId, status, page, 10));
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
            TempData["Error"] = "Chỉ có thể huỷ đơn đang chờ xác nhận.";
            return RedirectToAction(nameof(History), new { status = returnStatus });
        }

        TempData["Success"] = "Đã huỷ đơn hàng.";
        return RedirectToAction(nameof(History), new { status = returnStatus });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder(int id)
    {
        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "Tài khoản admin chỉ được xem và kiểm tra, không thể mua lại đơn hàng.";
            return RedirectToAction(nameof(Detail), new { id });
        }

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

    private static bool IsUnpaidVnpayOrder(Order order)
    {
        return string.Equals(order.PaymentMethod, "VNPAY", StringComparison.OrdinalIgnoreCase)
            && !order.IsPaid;
    }
}
