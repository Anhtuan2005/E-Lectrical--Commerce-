namespace EcommerceApp.Models.ViewModels;

public class AdminReportViewModel
{
    public List<RevenuePointViewModel> DailyRevenue { get; set; } = new();
    public Dictionary<string, decimal> RevenueByCategory { get; set; } = new();
    public List<TopProductViewModel> TopProducts { get; set; } = new();
    public decimal CurrentPeriodRevenue { get; set; }
    public decimal PreviousPeriodRevenue { get; set; }
    public decimal GrowthPercent { get; set; }
    public int TotalOrders { get; set; }
    public int TotalCustomers { get; set; }
    public string Period { get; set; } = "30";
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
