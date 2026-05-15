using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models;

public class Order
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required, StringLength(40)]
    public string Status { get; set; } = OrderStatuses.Pending;

    [Range(0, double.MaxValue)]
    public decimal TotalAmount { get; set; }

    [StringLength(30)]
    public string? VoucherCode { get; set; }

    [Range(0, double.MaxValue)]
    public decimal DiscountAmount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Required, StringLength(500)]
    public string ShippingAddress { get; set; } = string.Empty;

    [Required, StringLength(40)]
    public string PaymentMethod { get; set; } = "COD";

    [Required, StringLength(120)]
    public string RecipientName { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string RecipientPhone { get; set; } = string.Empty;

    [StringLength(120)]
    public string? VnpayTransactionId { get; set; }

    [StringLength(20)]
    public string? VnpayResponseCode { get; set; }

    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }

    [StringLength(500)]
    public string? CancelledReason { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ShippingInfo? ShippingInfo { get; set; }
}

public static class OrderStatuses
{
    public const string Pending = "Chờ xác nhận";
    public const string Confirmed = "Đã xác nhận";
    public const string Shipping = "Đang giao";
    public const string Delivered = "Đã giao";
    public const string Cancelled = "Huỷ";

    public static readonly string[] All = { Pending, Confirmed, Shipping, Delivered, Cancelled };
}
