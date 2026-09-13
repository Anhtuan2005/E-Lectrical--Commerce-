using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace EcommerceApp.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("Admin/Order")]
public class AdminOrderController : Controller
{
    private readonly IOrderService _orderService;
    private readonly IShippingService _shippingService;
    private readonly IInvoiceService _invoiceService;
    private readonly AppDbContext _db;
    private readonly ILogger<AdminOrderController> _logger;

    public AdminOrderController(
        IOrderService orderService,
        IShippingService shippingService,
        IInvoiceService invoiceService,
        AppDbContext db,
        ILogger<AdminOrderController> logger)
    {
        _orderService = orderService;
        _shippingService = shippingService;
        _invoiceService = invoiceService;
        _db = db;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? status, string? customer, DateTime? fromDate, DateTime? toDate, int page = 1)
    {
        return View("~/Views/Admin/Order/Index.cshtml", await _orderService.GetOrdersAsync(status, customer, fromDate, toDate, page));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var order = await _orderService.GetOrderAsync(id);
        return order is null ? NotFound() : View("~/Views/Admin/Order/Details.cshtml", order);
    }

    [HttpPost("UpdateStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        var updated = await _orderService.UpdateStatusAsync(id, status);
        TempData[updated ? "Success" : "Error"] = updated
            ? "Đã cập nhật trạng thái đơn hàng."
            : "Không thể chuyển đơn sang trạng thái đã chọn.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("BulkConfirm")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkConfirm(int[] selectedOrderIds, string? returnUrl)
    {
        var confirmed = await _orderService.ConfirmPendingOrdersAsync(selectedOrderIds);
        TempData[confirmed > 0 ? "Success" : "Error"] = confirmed > 0
            ? $"Đã xác nhận {confirmed} đơn hàng."
            : "Chọn ít nhất một đơn đang chờ xác nhận.";

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("AssignShipping")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignShipping(int id, string carrier, string trackingCode, DateTime? estimatedDelivery)
    {
        var updated = await _shippingService.AssignAsync(id, carrier, trackingCode, estimatedDelivery);
        TempData[updated ? "Success" : "Error"] = updated
            ? "Đã gán thông tin vận chuyển."
            : "Không thể gán vận chuyển. Đơn phải được xác nhận, VNPAY phải đã thanh toán; tên đơn vị và mã vận đơn tối đa 80 ký tự.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("MarkRefunded")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRefunded(int id, string? note)
    {
        var updated = await _orderService.MarkManualRefundCompletedAsync(id, note);
        TempData[updated ? "Success" : "Error"] = updated
            ? "Đã ghi nhận hoàn tiền thủ công cho đơn VNPAY."
            : "Đơn hàng không ở trạng thái cần hoàn tiền thủ công.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("IssueInvoice")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IssueInvoice(int id)
    {
        var result = await _invoiceService.IssueInvoiceAsync(id);
        if (!result.Success)
        {
            TempData["Error"] = result.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        var file = await _invoiceService.DownloadPdfAsync(id);
        if (!file.Success || file.Content is null)
        {
            TempData["Success"] = result.Message;
            TempData["Error"] = file.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        return File(file.Content, "application/pdf", file.FileName ?? $"hoa-don-{id}.pdf");
    }

    [HttpPost("CancelInvoice")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelInvoice(int id)
    {
        var result = await _invoiceService.CancelInvoiceAsync(id);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("DownloadInvoicePdf")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DownloadInvoicePdf(int id)
    {
        var result = await _invoiceService.DownloadPdfAsync(id);
        if (!result.Success || result.Content is null)
        {
            TempData["Error"] = result.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        return File(result.Content, "application/pdf", result.FileName ?? $"hoa-don-{id}.pdf");
    }

    [HttpGet("Print/{id:int}")]
    public async Task<IActionResult> Print(int id)
    {
        var order = await _orderService.GetOrderAsync(id);
        return order is null ? NotFound() : View("~/Views/Admin/Order/Print.cshtml", order);
    }

    [HttpGet("ExportCsv")]
    public async Task<IActionResult> ExportCsv(string? status, string? customer, DateTime? fromDate, DateTime? toDate)
    {
        var model = await _orderService.GetOrdersAsync(status, customer, fromDate, toDate, paginate: false);
        var csv = new StringBuilder();
        csv.AppendLine(CsvFormatter.Row("MaDon", "KhachHang", "Email", "TrangThai", "HoanTien", "TongTien", "NgayDat", "VanChuyen", "MaVanDon"));
        foreach (var order in model.Orders)
        {
            csv.AppendLine(CsvFormatter.Row(
                order.Id,
                order.User?.FullName,
                order.User?.Email,
                order.Status,
                order.RefundStatus,
                order.TotalAmount,
                order.CreatedAt,
                order.ShippingInfo?.Carrier,
                order.ShippingInfo?.TrackingCode));
        }

        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv", $"don-hang-{DateTime.Now:yyyyMMddHHmm}.csv");
    }

    [HttpGet("NewCount")]
    public async Task<IActionResult> NewCount(long since)
    {
        DateTime sinceDate;
        try
        {
            sinceDate = DateTimeOffset.FromUnixTimeMilliseconds(since).UtcDateTime;
        }
        catch (ArgumentOutOfRangeException)
        {
            return BadRequest(new { error = "Mốc thời gian không hợp lệ." });
        }
        var count = await _db.Orders.CountAsync(order =>
            (order.CreatedAt > sinceDate && order.Status != OrderStatuses.AwaitingPayment) ||
            (order.PaymentMethod == "VNPAY" && order.IsPaid && order.Status == OrderStatuses.Pending && order.PaidAt > sinceDate));
        return Json(new { count });
    }
}
