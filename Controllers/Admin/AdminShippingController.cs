using EcommerceApp.Models;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceApp.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("Admin/Shipping")]
public class AdminShippingController : Controller
{
    private readonly IShippingService _shippingService;

    public AdminShippingController(IShippingService shippingService)
    {
        _shippingService = shippingService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewBag.Statuses = ShippingStatuses.All;
        return View("~/Views/Admin/Shipping/Index.cshtml", await _shippingService.GetDashboardAsync());
    }

    [HttpPost("UpdateStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int orderId, string status)
    {
        await _shippingService.UpdateStatusAsync(orderId, status);
        TempData["Success"] = "Đã cập nhật trạng thái vận chuyển.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("UpdateTracking")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTracking(int orderId, string? carrier, string trackingCode)
    {
        var updated = await _shippingService.UpdateTrackingAsync(orderId, carrier, trackingCode);
        TempData[updated ? "Success" : "Error"] = updated
            ? $"Đã cập nhật mã vận đơn cho đơn #{orderId}."
            : "Vui lòng nhập mã vận đơn hợp lệ.";

        return RedirectToAction(nameof(Index));
    }
}
