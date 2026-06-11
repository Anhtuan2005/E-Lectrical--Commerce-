using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models;

public class ReturnWarrantyRequest
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    [Required, StringLength(30)]
    public string Type { get; set; } = ReturnWarrantyRequestTypes.Return;

    [Required, StringLength(40)]
    public string Status { get; set; } = ReturnWarrantyRequestStatuses.Submitted;

    [Required, StringLength(120)]
    public string ContactName { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string ContactPhone { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string Reason { get; set; } = string.Empty;

    [Required, StringLength(1200)]
    public string Description { get; set; } = string.Empty;

    [StringLength(120)]
    public string PreferredResolution { get; set; } = string.Empty;

    [StringLength(1200)]
    public string? AdminNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ICollection<ReturnWarrantyRequestItem> Items { get; set; } = new List<ReturnWarrantyRequestItem>();
    public ICollection<ReturnWarrantyRequestImage> Images { get; set; } = new List<ReturnWarrantyRequestImage>();
}

public class ReturnWarrantyRequestItem
{
    public int Id { get; set; }
    public int ReturnWarrantyRequestId { get; set; }
    public ReturnWarrantyRequest? ReturnWarrantyRequest { get; set; }

    public int OrderItemId { get; set; }
    public OrderItem? OrderItem { get; set; }

    public int ProductId { get; set; }

    [Required, StringLength(240)]
    public string ProductNameSnapshot { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;
}

public class ReturnWarrantyRequestImage
{
    public int Id { get; set; }
    public int ReturnWarrantyRequestId { get; set; }
    public ReturnWarrantyRequest? ReturnWarrantyRequest { get; set; }

    [Required, StringLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}

public static class ReturnWarrantyRequestTypes
{
    public const string Return = "Đổi trả";
    public const string Warranty = "Bảo hành";

    public static readonly string[] All = { Return, Warranty };
}

public static class ReturnWarrantyRequestStatuses
{
    public const string Submitted = "Mới gửi";
    public const string Reviewing = "Đang xử lý";
    public const string WaitingForCustomer = "Chờ khách bổ sung";
    public const string Approved = "Đã duyệt";
    public const string Rejected = "Từ chối";
    public const string Completed = "Hoàn tất";

    public static readonly string[] All =
    {
        Submitted,
        Reviewing,
        WaitingForCustomer,
        Approved,
        Rejected,
        Completed
    };
}
