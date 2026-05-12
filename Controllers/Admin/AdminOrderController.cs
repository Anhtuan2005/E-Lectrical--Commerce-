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
    private readonly AppDbContext _db;
    private readonly ILogger<AdminOrderController> _logger;

    public AdminOrderController(IOrderService orderService, IShippingService shippingService, AppDbContext db, ILogger<AdminOrderController> logger)
    {
        _orderService = orderService;
        _shippingService = shippingService;
        _db = db;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? status, string? customer, DateTime? fromDate, DateTime? toDate)
    {
        return View("~/Views/Admin/Order/Index.cshtml", await _orderService.GetOrdersAsync(status, customer, fromDate, toDate));
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
        await _orderService.UpdateStatusAsync(id, status);
        TempData["Success"] = "Đã cập nhật trạng thái đơn hàng.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("AssignShipping")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignShipping(int id, string carrier, string trackingCode, DateTime? estimatedDelivery)
    {
        await _shippingService.AssignAsync(id, carrier, trackingCode, estimatedDelivery);
        TempData["Success"] = "Đã gán thông tin vận chuyển.";
        return RedirectToAction(nameof(Details), new { id });
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
        var model = await _orderService.GetOrdersAsync(status, customer, fromDate, toDate);
        var csv = new StringBuilder();
        csv.AppendLine("MaDon,KhachHang,Email,TrangThai,TongTien,NgayDat,VanChuyen,MaVanDon");
        foreach (var order in model.Orders)
        {
            csv.AppendLine($"{order.Id},\"{order.User?.FullName}\",{order.User?.Email},\"{order.Status}\",{order.TotalAmount},{order.CreatedAt:yyyy-MM-dd},\"{order.ShippingInfo?.Carrier}\",\"{order.ShippingInfo?.TrackingCode}\"");
        }

        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv", $"don-hang-{DateTime.Now:yyyyMMddHHmm}.csv");
    }

    [HttpGet("NewCount")]
    public async Task<IActionResult> NewCount(long since)
    {
        var sinceDate = DateTimeOffset.FromUnixTimeMilliseconds(since).UtcDateTime;
        var count = await _db.Orders.CountAsync(order => order.CreatedAt > sinceDate);
        return Json(new { count });
    }
}
