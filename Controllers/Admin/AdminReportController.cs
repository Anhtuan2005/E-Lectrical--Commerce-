using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("Admin/Report")]
public class AdminReportController : Controller
{
    private readonly AppDbContext _db;

    public AdminReportController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string period = "30", DateTime? fromDate = null, DateTime? toDate = null)
    {
        var end = (toDate ?? DateTime.UtcNow).Date.AddDays(1);
        var start = period switch
        {
            "7" => end.AddDays(-7),
            "90" => end.AddMonths(-3),
            "custom" => (fromDate ?? end.AddDays(-30)).Date,
            _ => end.AddDays(-30)
        };
        var previousStart = start.AddDays(-(end - start).TotalDays);

        var revenueOrders = _db.Orders.Where(order => order.IsPaid || order.Status == OrderStatuses.Delivered);
        var currentOrders = revenueOrders.Where(order => order.CreatedAt >= start && order.CreatedAt < end);
        var previousOrders = revenueOrders.Where(order => order.CreatedAt >= previousStart && order.CreatedAt < start);
        var currentRevenue = await currentOrders.SumAsync(order => order.TotalAmount);
        var previousRevenue = await previousOrders.SumAsync(order => order.TotalAmount);

        var model = new AdminReportViewModel
        {
            Period = period,
            FromDate = fromDate,
            ToDate = toDate,
            CurrentPeriodRevenue = currentRevenue,
            PreviousPeriodRevenue = previousRevenue,
            GrowthPercent = previousRevenue == 0 ? (currentRevenue > 0 ? 100 : 0) : (currentRevenue - previousRevenue) / previousRevenue * 100,
            TotalOrders = await currentOrders.CountAsync(),
            TotalCustomers = await _db.Users.CountAsync(),
            DailyRevenue = await currentOrders
                .GroupBy(order => order.CreatedAt.Date)
                .Select(group => new RevenuePointViewModel { Label = group.Key.ToString("dd/MM"), Revenue = group.Sum(order => order.TotalAmount) })
                .OrderBy(point => point.Label)
                .ToListAsync(),
            RevenueByCategory = await _db.OrderItems
                .IgnoreQueryFilters()
                .Include(item => item.Product).ThenInclude(product => product!.Category)
                .Where(item => item.Order != null && (item.Order.IsPaid || item.Order.Status == OrderStatuses.Delivered) && item.Order.CreatedAt >= start && item.Order.CreatedAt < end)
                .GroupBy(item => item.Product!.Category!.Name)
                .ToDictionaryAsync(group => group.Key, group => group.Sum(item => item.Quantity * item.UnitPrice)),
            TopProducts = await _db.OrderItems
                .IgnoreQueryFilters()
                .Include(item => item.Product)
                .Where(item => item.Order != null && (item.Order.IsPaid || item.Order.Status == OrderStatuses.Delivered) && item.Order.CreatedAt >= start && item.Order.CreatedAt < end)
                .GroupBy(item => item.Product!.Name)
                .Select(group => new TopProductViewModel { ProductName = group.Key, QuantitySold = group.Sum(item => item.Quantity), Revenue = group.Sum(item => item.Quantity * item.UnitPrice) })
                .OrderByDescending(item => item.QuantitySold)
                .Take(10)
                .ToListAsync()
        };

        return View("~/Views/Admin/Report/Index.cshtml", model);
    }
}
