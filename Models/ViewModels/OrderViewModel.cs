using EcommerceApp.Models;
using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models.ViewModels;

public class CheckoutViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên người nhận")]
    [StringLength(120)]
    public string RecipientName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
    [RegularExpression(@"^(03|07|08|09)\d{8}$", ErrorMessage = "Số điện thoại phải đúng định dạng di động Việt Nam")]
    public string RecipientPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn tỉnh/thành phố")]
    [StringLength(100)]
    public string Province { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập quận/huyện")]
    [StringLength(100)]
    public string District { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập phường/xã")]
    [StringLength(100)]
    public string Ward { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số nhà, tên đường")]
    [StringLength(200)]
    public string Street { get; set; } = string.Empty;

    public bool UseProfileAddress { get; set; }

    public string ShippingAddress => $"{Street}, {Ward}, {District}, {Province}";

    [Required]
    public string PaymentMethod { get; set; } = "COD";

    public string? VoucherCode { get; set; }
    public decimal DiscountAmount { get; set; }

    public CartViewModel Cart { get; set; } = new();

    public string? ProfileAddress { get; set; }
}

public class OrderListViewModel
{
    public IEnumerable<Order> Orders { get; set; } = Enumerable.Empty<Order>();
    public string? Status { get; set; }
    public string? Customer { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public IEnumerable<string> Statuses { get; set; } = OrderStatuses.All;
}

public class UserOrdersViewModel
{
    public IReadOnlyList<Order> Orders { get; set; } = Array.Empty<Order>();
    public IReadOnlyList<OrderStatusTabViewModel> Tabs { get; set; } = Array.Empty<OrderStatusTabViewModel>();
    public string ActiveStatus { get; set; } = OrderStatusFilters.All;
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }
}

public class OrderStatusTabViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public bool IsActive { get; set; }
}

public static class OrderStatusFilters
{
    public const string All = "all";
    public const string Pending = "pending";
    public const string Processing = "processing";
    public const string Shipping = "shipping";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";

    public static readonly IReadOnlyList<(string Key, string Label, string? Status)> Tabs = new[]
    {
        (All, "Tất cả", (string?)null),
        (Pending, "Chờ xác nhận", OrderStatuses.Pending),
        (Processing, "Đang xử lý", OrderStatuses.Confirmed),
        (Shipping, "Đang giao", OrderStatuses.Shipping),
        (Completed, "Hoàn thành", OrderStatuses.Delivered),
        (Cancelled, "Đã hủy", OrderStatuses.Cancelled)
    };

    public static string Normalize(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return All;
        }

        var normalized = key.Trim().ToLowerInvariant();
        return Tabs.Any(tab => tab.Key == normalized) ? normalized : All;
    }

    public static string? ToOrderStatus(string? key)
    {
        var normalized = Normalize(key);
        return Tabs.First(tab => tab.Key == normalized).Status;
    }

    public static string ToDisplayLabel(string status)
    {
        if (status == OrderStatuses.Confirmed)
        {
            return "Đang xử lý";
        }

        if (status == OrderStatuses.Delivered)
        {
            return "Hoàn thành";
        }

        if (status == OrderStatuses.Cancelled)
        {
            return "Đã hủy";
        }

        return status;
    }

    public static int StepIndex(string status)
    {
        if (status == OrderStatuses.Pending) return 0;
        if (status == OrderStatuses.Confirmed) return 1;
        if (status == OrderStatuses.Shipping) return 2;
        if (status == OrderStatuses.Delivered) return 3;
        if (status == OrderStatuses.Cancelled) return -1;
        return 0;
    }
}
