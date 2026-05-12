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
}
