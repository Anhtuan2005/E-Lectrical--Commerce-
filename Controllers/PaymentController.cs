using EcommerceApp.Data;
using EcommerceApp.Hubs;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;

namespace EcommerceApp.Controllers;

[Route("payment")]
public class PaymentController : Controller
{
    private readonly AppDbContext _db;
    private readonly IVnpayService _vnpayService;
    private readonly IOrderEmailService _orderEmailService;
    private readonly IUserNotificationService _notificationService;
    private readonly ICustomerSegmentService _customerSegmentService;
    private readonly IHubContext<AdminNotificationHub> _adminNotificationHub;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(AppDbContext db, IVnpayService vnpayService, IOrderEmailService orderEmailService, IUserNotificationService notificationService, ICustomerSegmentService customerSegmentService, IHubContext<AdminNotificationHub> adminNotificationHub, ILogger<PaymentController> logger)
    {
        _db = db;
        _vnpayService = vnpayService;
        _orderEmailService = orderEmailService;
        _notificationService = notificationService;
        _customerSegmentService = customerSegmentService;
        _adminNotificationHub = adminNotificationHub;
        _logger = logger;
    }

    [Authorize]
    [HttpPost("vnpay-create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VnpayCreate(int orderId)
    {
        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "Tài khoản admin chỉ được xem và kiểm tra, không thể thanh toán đơn hàng.";
            return RedirectToAction("Detail", "Order", new { id = orderId });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var order = await _db.Orders.FirstOrDefaultAsync(row => row.Id == orderId && row.UserId == userId);
        if (order is null)
        {
            return NotFound();
        }

        if (!string.Equals(order.PaymentMethod, "VNPAY", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Đơn hàng này không dùng phương thức VNPAY.";
            return RedirectToAction("Detail", "Order", new { id = order.Id });
        }

        if (order.IsPaid)
        {
            TempData["Success"] = "Đơn hàng này đã thanh toán.";
            return RedirectToAction("Confirmation", "Order", new { id = order.Id });
        }

        if (!IsUnpaidVnpayOrderAwaitingPayment(order))
        {
            TempData["Error"] = "Chỉ có thể thanh toán lại đơn VNPAY đang chờ thanh toán.";
            return RedirectToAction("Detail", "Order", new { id = order.Id });
        }

        return Redirect(_vnpayService.CreatePaymentUrl(order, HttpContext));
    }

    [AllowAnonymous]
    [HttpGet("vnpay-return")]
    public async Task<IActionResult> VnpayReturn()
    {
        var response = _vnpayService.ProcessCallback(Request.Query);
        _logger.LogInformation("VNPAY return received for order {OrderId}. Success={Success}, SignatureValid={SignatureValid}, ResponseCode={ResponseCode}, TransactionStatus={TransactionStatus}", response.OrderId, response.IsSuccess, response.IsSignatureValid, response.ResponseCode, response.TransactionStatus);
        var order = await UpdateOrderPaymentAsync(response);
        if (order is null)
        {
            TempData["Error"] = "Không thể xác thực kết quả thanh toán VNPAY.";
            return RedirectToAction("Index", "Home");
        }

        if (response.IsSuccess)
        {
            TempData["Success"] = "Thanh toán VNPAY thành công.";
            return RedirectToAction("Confirmation", "Order", new { id = order.Id });
        }

        TempData["Error"] = "Thanh toán VNPAY chưa hoàn tất. Đơn hàng vẫn đang chờ thanh toán.";
        return RedirectToAction("Detail", "Order", new { id = order.Id });
    }

    [AllowAnonymous]
    [HttpGet("vnpay-ipn")]
    [HttpPost("vnpay-ipn")]
    public async Task<IActionResult> VnpayIpn()
    {
        var response = _vnpayService.ProcessCallback(Request.Query);
        _logger.LogInformation("VNPAY IPN received for order {OrderId}. Success={Success}, SignatureValid={SignatureValid}, ResponseCode={ResponseCode}, TransactionStatus={TransactionStatus}", response.OrderId, response.IsSuccess, response.IsSignatureValid, response.ResponseCode, response.TransactionStatus);

        if (!response.IsSignatureValid)
        {
            return Json(new { RspCode = "97", Message = "Invalid signature" });
        }

        if (!int.TryParse(response.OrderId, out var id))
        {
            return Json(new { RspCode = "01", Message = "Order not found" });
        }

        var order = await _db.Orders.FirstOrDefaultAsync(row => row.Id == id);
        if (order is null)
        {
            return Json(new { RspCode = "01", Message = "Order not found" });
        }

        if (order.TotalAmount != response.Amount)
        {
            return Json(new { RspCode = "04", Message = "Invalid amount" });
        }

        if (order.IsPaid && response.IsSuccess)
        {
            return Json(new { RspCode = "00", Message = "Order already confirmed" });
        }

        if (order.IsPaid || !IsUnpaidVnpayOrderAwaitingPayment(order))
        {
            return Json(new { RspCode = "02", Message = "Order already confirmed" });
        }

        await UpdateOrderPaymentAsync(response);
        return Json(new { RspCode = "00", Message = "Confirm Success" });
    }

    private async Task<Order?> UpdateOrderPaymentAsync(VnpayResponse response)
    {
        if (!response.IsSignatureValid)
        {
            return null;
        }

        if (!int.TryParse(response.OrderId, out var id))
        {
            return null;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var order = await _db.Orders
            .FromSqlInterpolated($"SELECT * FROM Orders WITH (UPDLOCK, ROWLOCK) WHERE Id = {id}")
            .FirstOrDefaultAsync();
        if (order is null)
        {
            return null;
        }

        if (order.TotalAmount != response.Amount)
        {
            return null;
        }

        var shouldSendStatusEmail = ApplyPaymentResponse(response, order);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        if (response.IsSuccess)
        {
            await _customerSegmentService.RefreshUserAsync(order.UserId);
        }

        if (shouldSendStatusEmail)
        {
            await _orderEmailService.SendOrderStatusChangedAsync(order.Id, order.Status);
            await _notificationService.CreateAsync(
                order.UserId,
                "Thanh toán VNPAY thành công",
                $"Đơn #DH{order.Id:D4} đã thanh toán và chuyển sang trạng thái {OrderStatusFilters.ToDisplayLabel(order.Status)}.",
                NotificationTypes.Order,
                $"/Order/Detail/{order.Id}");
            await NotifyAdminsAboutPaidVnpayOrderAsync(order);
        }

        return order;
    }

    private static bool ApplyPaymentResponse(VnpayResponse response, Order order)
    {
        var wasPaid = order.IsPaid;

        if (order.IsPaid)
        {
            return false;
        }

        order.VnpayTransactionId = response.TransactionId;
        order.VnpayResponseCode = response.ResponseCode;
        if (response.IsSuccess)
        {
            order.IsPaid = true;
            order.PaidAt = DateTime.UtcNow;
            if (order.Status == OrderStatuses.AwaitingPayment)
            {
                order.Status = OrderStatuses.Pending;
            }
        }

        order.UpdatedAt = DateTime.UtcNow;
        return !wasPaid && response.IsSuccess && order.Status == OrderStatuses.Pending;
    }

    private async Task NotifyAdminsAboutPaidVnpayOrderAsync(Order order)
    {
        await _notificationService.CreateForAdminsAsync(
            "Đơn VNPAY đã thanh toán",
            $"Đơn #DH{order.Id:D4} đã thanh toán VNPAY và đang chờ xác nhận.",
            NotificationTypes.Order,
            $"/Admin/Order/{order.Id}");
        try
        {
            await _adminNotificationHub.Clients
                .Group(AdminNotificationHub.AdminGroup)
                .SendAsync("OrderCreated", new AdminOrderCreatedMessage(
                    order.Id,
                    $"DH{order.Id:D4}",
                    order.RecipientName,
                    order.TotalAmount,
                    order.PaidAt ?? order.UpdatedAt,
                    $"/Admin/Order/{order.Id}"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not publish realtime notification for paid VNPAY order {OrderId}", order.Id);
        }
    }

    private static bool IsUnpaidVnpayOrderAwaitingPayment(Order order)
    {
        return !order.IsPaid
            && string.Equals(order.PaymentMethod, "VNPAY", StringComparison.OrdinalIgnoreCase)
            && order.Status is OrderStatuses.AwaitingPayment or OrderStatuses.Pending;
    }
}
