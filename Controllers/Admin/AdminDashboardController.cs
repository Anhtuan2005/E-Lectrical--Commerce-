using EcommerceApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceApp.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("Admin")]
public class AdminDashboardController : Controller
{
    private readonly IOrderService _orderService;

    public AdminDashboardController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet("")]
    [HttpGet("Dashboard")]
    public async Task<IActionResult> Index()
    {
        return View("~/Views/Admin/Dashboard/Index.cshtml", await _orderService.GetDashboardAsync());
    }
}
