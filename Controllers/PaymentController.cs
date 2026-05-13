using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EcommerceApp.Controllers;

[Route("payment")]
public class PaymentController : Controller
{
    private readonly AppDbContext _db;
    private readonly IVnpayService _vnpayService;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(AppDbContext db, IVnpayService vnpayService, ILogger<PaymentController> logger)
    {
        _db = db;
        _vnpayService = vnpayService;
        _logger = logger;
    }

    [Authorize]
    [HttpPost("vnpay-create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VnpayCreate(int orderId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var order = await _db.Orders.FirstOrDefaultAsync(row => row.Id == orderId && row.UserId == userId);
        if (order is null)
        {
            return NotFound();
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
        TempData["PaymentResult"] = response.IsSuccess
            ? "Thanh toán VNPAY thành công."
            : "Thanh toán VNPAY chưa hoàn tất hoặc chữ ký không hợp lệ.";

        return order is null
            ? RedirectToAction("Index", "Home")
            : RedirectToAction("Confirmation", "Order", new { id = order.Id });
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

        if (order.IsPaid || order.Status != OrderStatuses.Pending)
        {
            return Json(new { RspCode = "02", Message = "Order already confirmed" });
        }

        await UpdateOrderPaymentAsync(response, order);
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

        var order = await _db.Orders.FirstOrDefaultAsync(row => row.Id == id);
        if (order is null)
        {
            return null;
        }

        if (order.TotalAmount != response.Amount)
        {
            return null;
        }

        await UpdateOrderPaymentAsync(response, order);
        return order;
    }

    private async Task UpdateOrderPaymentAsync(VnpayResponse response, Order order)
    {
        order.VnpayTransactionId = response.TransactionId;
        order.VnpayResponseCode = response.ResponseCode;
        if (response.IsSuccess)
        {
            order.IsPaid = true;
            order.PaidAt = DateTime.UtcNow;
            if (order.Status == OrderStatuses.Pending)
            {
                order.Status = OrderStatuses.Confirmed;
            }
        }

        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }
}
