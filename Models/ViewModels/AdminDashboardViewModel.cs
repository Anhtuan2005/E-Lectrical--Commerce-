namespace EcommerceApp.Models.ViewModels;

public class AdminDashboardViewModel
{
    public decimal TodayRevenue { get; set; }
    public decimal WeekRevenue { get; set; }
    public decimal MonthRevenue { get; set; }
    public decimal YearRevenue { get; set; }
    public int TotalOrders { get; set; }
    public int PendingOrdersCount { get; set; }
    public int AwaitingPaymentCount { get; set; }
    public int ManualRefundCount { get; set; }
    public int LowStockCount { get; set; }
    public int PriceReviewCount { get; set; }
    public Dictionary<string, int> OrdersByStatus { get; set; } = new();
    public IEnumerable<DashboardPeriodMetricViewModel> PeriodMetrics { get; set; } = Enumerable.Empty<DashboardPeriodMetricViewModel>();
    public IEnumerable<TopProductViewModel> TopProducts { get; set; } = Enumerable.Empty<TopProductViewModel>();
    public IEnumerable<ProductEngagementViewModel> TopViewedProducts { get; set; } = Enumerable.Empty<ProductEngagementViewModel>();
    public IEnumerable<PriceReviewProductViewModel> PriceReviewProducts { get; set; } = Enumerable.Empty<PriceReviewProductViewModel>();
    public IEnumerable<RevenuePointViewModel> RevenuePoints { get; set; } = Enumerable.Empty<RevenuePointViewModel>();
    public IEnumerable<Product> LowStockProducts { get; set; } = Enumerable.Empty<Product>();
}

public class DashboardPeriodMetricViewModel
{
    public string Label { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int Orders { get; set; }
    public decimal AverageOrderValue => Orders == 0 ? 0 : Revenue / Orders;
}

public class TopProductViewModel
{
    public string ProductName { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal Revenue { get; set; }
}

public class ProductEngagementViewModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int ViewCount { get; set; }
    public int UniqueSessions { get; set; }
    public int SoldQuantity { get; set; }
    public decimal Revenue { get; set; }
    public decimal ViewToSaleRate => ViewCount == 0 ? 0 : (decimal)SoldQuantity / ViewCount * 100;
}

public class PriceReviewProductViewModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public decimal Price { get; set; }
    public int DiscountPercent { get; set; }
    public int Stock { get; set; }
    public int AgeDays { get; set; }
    public int ViewsLast30Days { get; set; }
    public int SoldLast30Days { get; set; }
    public decimal InventoryValue { get; set; }
    public string Reason { get; set; } = string.Empty;
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
    public IEnumerable<Order> GhnTrackedOrders { get; set; } = Enumerable.Empty<Order>();
}
