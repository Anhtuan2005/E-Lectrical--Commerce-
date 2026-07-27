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
    private readonly IGhnShippingService _ghnShippingService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<OrderController> _logger;

    public OrderController(
        IOrderService orderService,
        ICartService cartService,
        IShippingFeeService shippingFeeService,
        IVnpayService vnpayService,
        IGhnShippingService ghnShippingService,
        UserManager<ApplicationUser> userManager,
        ILogger<OrderController> logger)
    {
        _orderService = orderService;
        _cartService = cartService;
        _shippingFeeService = shippingFeeService;
        _vnpayService = vnpayService;
        _ghnShippingService = ghnShippingService;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<IActionResult> Checkout([FromQuery] int[] selectedProductIds)
    {
        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "Tài khoản admin chỉ được xem và kiểm tra, không thể đặt hàng.";
            return RedirectToAction("Index", "Product");
        }

        var selectedIds = NormalizeSelectedProductIds(selectedProductIds);
        if (!selectedIds.Any())
        {
            TempData["Error"] = "Vui lòng chọn sản phẩm cần thanh toán.";
            return RedirectToAction("Index", "Cart");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var cart = await _cartService.GetCartAsync(userId, GetStableCartSessionId(userId), selectedIds);
        if (!cart.Items.Any())
        {
            TempData["Error"] = "Giỏ hàng đang trống.";
            return RedirectToAction("Index", "Cart");
        }

        var user = await _userManager.GetUserAsync(User);
        return View(new CheckoutViewModel
        {
            Cart = cart,
            SelectedProductIds = selectedIds,
            RecipientName = user?.FullName ?? string.Empty,
            RecipientPhone = user?.PhoneNumber ?? string.Empty,
            ShippingFee = _shippingFeeService.Calculate(null, null, cart.Total).Fee,
            ProfileAddress = user?.Address
        });
    }

    [HttpGet]
    public async Task<IActionResult> ShippingFee(string? province, string? district, [FromQuery] int[] selectedProductIds)
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
        var cart = await _cartService.GetCartAsync(userId, GetStableCartSessionId(userId), NormalizeSelectedProductIds(selectedProductIds));
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
        model.SelectedProductIds = NormalizeSelectedProductIds(model.SelectedProductIds);
        if (!model.SelectedProductIds.Any())
        {
            TempData["Error"] = "Vui lòng chọn sản phẩm cần thanh toán.";
            return RedirectToAction("Index", "Cart");
        }

        model.Cart = await _cartService.GetCartAsync(userId, GetStableCartSessionId(userId), model.SelectedProductIds);
        if (!model.Cart.Items.Any())
        {
            TempData["Error"] = "Các sản phẩm đã chọn không còn trong giỏ hàng.";
            return RedirectToAction("Index", "Cart");
        }

        model.ProfileAddress = (await _userManager.GetUserAsync(User))?.Address;
        model.ShippingFee = _shippingFeeService.Calculate(model.Province, model.District, model.Cart.Total).Fee;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.PaymentMethod == "VNPAY" && !_vnpayService.IsConfigured)
        {
            ModelState.AddModelError(nameof(model.PaymentMethod), "VNPAY đang tạm ngưng, vui lòng chọn thanh toán khi nhận hàng.");
            return View(model);
        }

        try
        {
            var order = await _orderService.CreateOrderAsync(userId, model, GetStableCartSessionId(userId));
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
        if (order is null)
        {
            return NotFound();
        }

        if (ShouldRefreshGhnStatus(order))
        {
            try
            {
                var result = await _ghnShippingService.SyncOrderAsync(order.Id);
                if (result.Success)
                {
                    order = await _orderService.GetUserOrderAsync(id, userId) ?? order;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Could not refresh GHN status for order {OrderId}.", order.Id);
            }
        }

        return View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason, string? returnStatus)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var cancelled = await _orderService.CancelUserOrderAsync(id, userId, reason);
        if (!cancelled)
        {
            TempData["Error"] = "Chỉ có thể huỷ đơn đang chờ thanh toán hoặc chờ xác nhận.";
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

    private bool ShouldRefreshGhnStatus(Order order)
    {
        return _ghnShippingService.IsConfigured &&
               order.Status != OrderStatuses.Delivered &&
               order.Status != OrderStatuses.Cancelled &&
               order.ShippingInfo is not null &&
               string.Equals(order.ShippingInfo.Carrier, "Giao Hàng Nhanh", StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(order.ShippingInfo.TrackingCode) &&
               order.UpdatedAt < DateTime.UtcNow.AddMinutes(-2);
    }

    private static List<int> NormalizeSelectedProductIds(IEnumerable<int>? productIds)
    {
        return (productIds ?? Array.Empty<int>())
            .Where(id => id > 0)
            .Distinct()
            .ToList();
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
