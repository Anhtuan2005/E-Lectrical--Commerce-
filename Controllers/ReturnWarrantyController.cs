using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EcommerceApp.Controllers;

[Authorize]
public class ReturnWarrantyController : Controller
{
    private readonly IReturnWarrantyRequestService _requestService;

    public ReturnWarrantyController(IReturnWarrantyRequestService requestService)
    {
        _requestService = requestService;
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return View(new UserReturnWarrantyRequestsViewModel
        {
            Requests = await _requestService.GetUserRequestsAsync(userId)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(int orderId)
    {
        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "Tài khoản admin chỉ được xem và kiểm tra, không thể tạo yêu cầu hỗ trợ.";
            return RedirectToAction("History", "Order");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var model = await _requestService.BuildCreateModelAsync(orderId, userId);
        if (model is null)
        {
            TempData["Error"] = "Chỉ có thể tạo yêu cầu đổi trả hoặc bảo hành cho đơn hàng đã giao.";
            return RedirectToAction("Detail", "Order", new { id = orderId });
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ReturnWarrantyRequestCreateViewModel model)
    {
        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "Tài khoản admin chỉ được xem và kiểm tra, không thể tạo yêu cầu hỗ trợ.";
            return RedirectToAction("History", "Order");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var baseModel = await _requestService.BuildCreateModelAsync(model.OrderId, userId);
        if (baseModel is null)
        {
            TempData["Error"] = "Chỉ có thể tạo yêu cầu đổi trả hoặc bảo hành cho đơn hàng đã giao.";
            return RedirectToAction("Detail", "Order", new { id = model.OrderId });
        }

        model.Order = baseModel.Order;
        if (model.Items.Count == 0)
        {
            model.Items = baseModel.Items;
        }
        var availableItems = baseModel.Items.ToDictionary(item => item.OrderItemId);
        if (model.Items.Any(item => !availableItems.ContainsKey(item.OrderItemId)) ||
            model.Items.Select(item => item.OrderItemId).Distinct().Count() != model.Items.Count)
        {
            model.Items = baseModel.Items;
            foreach (var key in ModelState.Keys.Where(key => key.StartsWith("Items", StringComparison.OrdinalIgnoreCase)).ToList())
                ModelState.Remove(key);
            ModelState.AddModelError(string.Empty, "Danh sách sản phẩm không hợp lệ. Vui lòng chọn lại sản phẩm thuộc đơn hàng.");
        }
        foreach (var item in model.Items) item.AvailableQuantity = availableItems[item.OrderItemId].AvailableQuantity;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var request = await _requestService.CreateAsync(userId, model);
            TempData["Success"] = $"Đã gửi yêu cầu {request.Type.ToLowerInvariant()} #{request.Id}.";
            return RedirectToAction(nameof(Details), new { id = request.Id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    public async Task<IActionResult> Details(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var request = await _requestService.GetUserRequestAsync(id, userId);
        return request is null ? NotFound() : View(request);
    }
}
