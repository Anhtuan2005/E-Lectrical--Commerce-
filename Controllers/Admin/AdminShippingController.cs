using EcommerceApp.Models;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace EcommerceApp.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("Admin/Shipping")]
public class AdminShippingController : Controller
{
    private readonly IShippingService _shippingService;
    private readonly IGhnShippingService _ghnShippingService;

    public AdminShippingController(IShippingService shippingService, IGhnShippingService ghnShippingService)
    {
        _shippingService = shippingService;
        _ghnShippingService = ghnShippingService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewBag.Statuses = ShippingStatuses.All;
        ViewBag.GhnConfigured = _ghnShippingService.IsConfigured;
        return View("~/Views/Admin/Shipping/Index.cshtml", await _shippingService.GetDashboardAsync());
    }

    [HttpPost("UpdateStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int orderId, string status)
    {
        var updated = await _shippingService.UpdateStatusAsync(orderId, status);
        TempData[updated ? "Success" : "Error"] = updated
            ? "Đã cập nhật trạng thái vận chuyển."
            : "Không thể chuyển vận đơn sang trạng thái này. Hãy kiểm tra trạng thái hiện tại của đơn.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("UpdateTracking")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTracking(int orderId, string? carrier, string trackingCode)
    {
        var updated = await _shippingService.UpdateTrackingAsync(orderId, carrier, trackingCode);
        TempData[updated ? "Success" : "Error"] = updated
            ? $"Đã cập nhật mã vận đơn cho đơn #{orderId}."
            : "Đơn phải được xác nhận và đủ điều kiện giao hàng. Mã vận đơn và tên đơn vị vận chuyển tối đa 80 ký tự.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("GhnCreate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GhnCreate(int orderId, GhnCreateOrderInput input, string? returnUrl)
    {
        var result = await _ghnShippingService.CreateOrderAsync(orderId, input);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectBack(returnUrl);
    }

    [HttpPost("GhnFee")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GhnFee(int orderId, GhnCreateOrderInput input, string? returnUrl)
    {
        var result = await _ghnShippingService.CalculateFeeAsync(orderId, input);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        if (result.Success && result.FeeQuote is not null)
        {
            TempData["GhnFeeQuote"] = JsonSerializer.Serialize(result.FeeQuote);
        }

        return RedirectBack(returnUrl);
    }

    [HttpGet("GhnProvinces")]
    public async Task<IActionResult> GhnProvinces()
    {
        return Json(await _ghnShippingService.GetProvincesAsync());
    }

    [HttpGet("GhnDistricts")]
    public async Task<IActionResult> GhnDistricts(int provinceId)
    {
        return Json(await _ghnShippingService.GetDistrictsAsync(provinceId));
    }

    [HttpGet("GhnWards")]
    public async Task<IActionResult> GhnWards(int districtId)
    {
        return Json(await _ghnShippingService.GetWardsAsync(districtId));
    }

    [HttpGet("GhnDetail")]
    public async Task<IActionResult> GhnDetail(string? trackingCode, string? returnUrl)
    {
        var result = await _ghnShippingService.GetOrderDetailByTrackingCodeAsync(trackingCode ?? string.Empty);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        if (result.Success && result.Detail is not null)
        {
            TempData["GhnShipmentDetail"] = JsonSerializer.Serialize(result.Detail);
        }

        return RedirectBack(returnUrl);
    }

    [HttpPost("GhnDetail")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GhnDetail(int orderId, string? returnUrl)
    {
        var result = await _ghnShippingService.GetOrderDetailAsync(orderId);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        if (result.Success && result.Detail is not null)
        {
            TempData["GhnShipmentDetail"] = JsonSerializer.Serialize(result.Detail);
        }

        return RedirectBack(returnUrl);
    }

    [HttpPost("GhnSync")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GhnSync(int orderId, string? returnUrl)
    {
        var result = await _ghnShippingService.SyncOrderAsync(orderId);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectBack(returnUrl);
    }

    [HttpPost("GhnCancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GhnCancel(int orderId, string? returnUrl)
    {
        var result = await _ghnShippingService.CancelOrderAsync(orderId);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectBack(returnUrl);
    }

    private IActionResult RedirectBack(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }
}
