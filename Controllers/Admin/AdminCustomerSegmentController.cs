using EcommerceApp.Models.ViewModels;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceApp.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("Admin/CustomerSegment")]
public class AdminCustomerSegmentController : Controller
{
    private readonly ICustomerSegmentService _customerSegmentService;

    public AdminCustomerSegmentController(ICustomerSegmentService customerSegmentService)
    {
        _customerSegmentService = customerSegmentService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        await _customerSegmentService.RefreshAsync();
        var model = new CustomerSegmentIndexViewModel
        {
            Segments = await _customerSegmentService.GetSummariesAsync(),
            RefreshedAt = DateTime.UtcNow
        };

        return View("~/Views/Admin/CustomerSegment/Index.cshtml", model);
    }

    [HttpPost("Refresh")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Refresh()
    {
        await _customerSegmentService.RefreshAsync();
        TempData["Success"] = "Đã cập nhật phân nhóm khách hàng.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Details/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var model = await _customerSegmentService.GetDetailAsync(id);
        return model is null
            ? NotFound()
            : View("~/Views/Admin/CustomerSegment/Details.cshtml", model);
    }
}
