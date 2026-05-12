namespace EcommerceApp.Models.ViewModels;

public class AdminDashboardViewModel
{
    public decimal TodayRevenue { get; set; }
    public decimal WeekRevenue { get; set; }
    public decimal MonthRevenue { get; set; }
    public int TotalOrders { get; set; }
    public Dictionary<string, int> OrdersByStatus { get; set; } = new();
    public IEnumerable<TopProductViewModel> TopProducts { get; set; } = Enumerable.Empty<TopProductViewModel>();
    public IEnumerable<RevenuePointViewModel> RevenuePoints { get; set; } = Enumerable.Empty<RevenuePointViewModel>();
    public IEnumerable<Product> LowStockProducts { get; set; } = Enumerable.Empty<Product>();
}

public class TopProductViewModel
{
    public string ProductName { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal Revenue { get; set; }
}

public class RevenuePointViewModel
{
    public string Label { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
}

public class ShippingDashboardViewModel
{
    public Dictionary<string, int> OrdersByShippingStatus { get; set; } = new();
    public int MissingTrackingCount { get; set; }
    public int DelayedCount { get; set; }
    public IEnumerable<Order> ActionNeededOrders { get; set; } = Enumerable.Empty<Order>();
}
