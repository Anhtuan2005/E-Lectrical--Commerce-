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
        period = period is "7" or "30" or "90" or "365" or "custom" ? period : "30";
        var today = DateTime.UtcNow.Date;
        var selectedEnd = (toDate ?? today).Date;
        if (selectedEnd > today)
        {
            selectedEnd = today;
            ViewData["ReportFilterError"] = "Ngày kết thúc trong tương lai đã được đưa về hôm nay.";
        }
        else if (selectedEnd < DateTime.UnixEpoch.Date)
        {
            selectedEnd = DateTime.UnixEpoch.Date;
            ViewData["ReportFilterError"] = "Ngày kết thúc quá cũ đã được đưa về 01/01/1970.";
        }

        var end = selectedEnd.AddDays(1);
        var start = period switch
        {
            "7" => end.AddDays(-7),
            "90" => end.AddMonths(-3),
            "365" => end.AddYears(-1),
            "custom" => (fromDate ?? selectedEnd.AddDays(-29)).Date,
            _ => end.AddDays(-30)
        };

        if (period == "custom" && start > selectedEnd)
        {
            start = selectedEnd.AddDays(-29);
            ViewData["ReportFilterError"] = "Từ ngày phải trước hoặc bằng đến ngày. Báo cáo đang hiển thị 30 ngày gần nhất.";
        }

        var earliestAllowed = selectedEnd.AddDays(-365);
        if (start < earliestAllowed)
        {
            start = earliestAllowed;
            ViewData["ReportFilterError"] = "Khoảng tùy chọn được giới hạn tối đa 366 ngày.";
        }

        var previousStart = start.AddDays(-(end - start).TotalDays);

        var revenueOrders = _db.Orders.WithRecognizedRevenue();
        var currentOrders = revenueOrders.Where(order => order.CreatedAt >= start && order.CreatedAt < end);
        var previousOrders = revenueOrders.Where(order => order.CreatedAt >= previousStart && order.CreatedAt < start);
        var currentRevenue = await currentOrders.SumAsync(order => order.TotalAmount);
        var previousRevenue = await previousOrders.SumAsync(order => order.TotalAmount);
        var dailyRevenue = await currentOrders
            .GroupBy(order => order.CreatedAt.Date)
            .Select(group => new { Date = group.Key, Revenue = group.Sum(order => order.TotalAmount) })
            .OrderBy(point => point.Date)
            .ToListAsync();
        var revenueByDate = dailyRevenue.ToDictionary(point => point.Date, point => point.Revenue);
        var dailyPoints = Enumerable.Range(0, Math.Max(1, (int)(end.Date - start.Date).TotalDays))
            .Select(offset =>
            {
                var date = start.Date.AddDays(offset);
                return new RevenuePointViewModel
                {
                    Label = date.ToString("dd/MM"),
                    Revenue = revenueByDate.GetValueOrDefault(date)
                };
            })
            .ToList();

        var model = new AdminReportViewModel
        {
            Period = period,
            FromDate = start.Date,
            ToDate = end.AddDays(-1).Date,
            CurrentPeriodRevenue = currentRevenue,
            PreviousPeriodRevenue = previousRevenue,
            GrowthPercent = previousRevenue == 0 ? (currentRevenue > 0 ? 100 : 0) : (currentRevenue - previousRevenue) / previousRevenue * 100,
            TotalOrders = await currentOrders.CountAsync(),
            TotalCustomers = await _db.Users.CountAsync(),
            DailyRevenue = dailyPoints,
            RevenueByCategory = await _db.OrderItems
                .IgnoreQueryFilters()
                .Include(item => item.Product).ThenInclude(product => product!.Category)
                .WithRecognizedRevenue()
                .Where(item => item.Order!.CreatedAt >= start && item.Order.CreatedAt < end)
                .GroupBy(item => item.Product!.Category!.Name)
                .ToDictionaryAsync(group => group.Key, group => group.Sum(item => item.Quantity * item.UnitPrice)),
            TopProducts = await _db.OrderItems
                .IgnoreQueryFilters()
                .Include(item => item.Product)
                .WithRecognizedRevenue()
                .Where(item => item.Order!.CreatedAt >= start && item.Order.CreatedAt < end)
                .GroupBy(item => item.Product!.Name)
                .Select(group => new TopProductViewModel { ProductName = group.Key, QuantitySold = group.Sum(item => item.Quantity), Revenue = group.Sum(item => item.Quantity * item.UnitPrice) })
                .OrderByDescending(item => item.QuantitySold)
                .Take(10)
                .ToListAsync()
        };

        return View("~/Views/Admin/Report/Index.cshtml", model);
    }
}
