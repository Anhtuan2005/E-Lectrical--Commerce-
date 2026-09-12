using EcommerceApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceApp.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("Admin/ReturnWarranty")]
public class AdminReturnWarrantyController : Controller
{
    private readonly IReturnWarrantyRequestService _requestService;

    public AdminReturnWarrantyController(IReturnWarrantyRequestService requestService)
    {
        _requestService = requestService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? status, string? type, string? query)
    {
        return View("~/Views/Admin/ReturnWarranty/Index.cshtml", await _requestService.GetAdminRequestsAsync(status, type, query));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var request = await _requestService.GetRequestAsync(id);
        return request is null ? NotFound() : View("~/Views/Admin/ReturnWarranty/Details.cshtml", request);
    }

    [HttpPost("UpdateStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status, string? adminNote)
    {
        var updated = await _requestService.UpdateStatusAsync(id, status, adminNote);
        TempData[updated ? "Success" : "Error"] = updated
            ? "Đã cập nhật trạng thái yêu cầu."
            : "Không thể chuyển yêu cầu sang trạng thái này. Hãy kiểm tra trạng thái hiện tại và ghi chú (tối đa 1.200 ký tự).";

        return RedirectToAction(nameof(Details), new { id });
    }
}
