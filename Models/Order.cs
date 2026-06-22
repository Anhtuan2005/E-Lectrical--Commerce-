using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

    [Range(0, double.MaxValue)]
    public decimal ShippingFee { get; set; }

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

    [Required, StringLength(40)]
    public string RefundStatus { get; set; } = RefundStatuses.NotRequired;

    public DateTime? RefundRequestedAt { get; set; }
    public DateTime? RefundedAt { get; set; }

    [StringLength(500)]
    public string? RefundNote { get; set; }

    [StringLength(500)]
    public string? CancelledReason { get; set; }

    [Required, StringLength(40)]
    public string InvoiceStatus { get; set; } = InvoiceStatuses.NotIssued;

    [StringLength(40)]
    public string? InvoiceProvider { get; set; }

    [StringLength(80)]
    public string? InvoiceFkey { get; set; }

    [StringLength(60)]
    public string? InvoicePattern { get; set; }

    [StringLength(60)]
    public string? InvoiceSerial { get; set; }

    [StringLength(80)]
    public string? InvoiceNumber { get; set; }

    [StringLength(160)]
    public string? InvoiceLookupCode { get; set; }

    [StringLength(500)]
    public string? InvoiceViewUrl { get; set; }

    public DateTime? InvoiceIssuedAt { get; set; }
    public DateTime? InvoiceSyncedAt { get; set; }

    [StringLength(1000)]
    public string? InvoiceRawResponse { get; set; }

    [StringLength(1000)]
    public string? InvoiceErrorMessage { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<ReturnWarrantyRequest> ReturnWarrantyRequests { get; set; } = new List<ReturnWarrantyRequest>();
    public ShippingInfo? ShippingInfo { get; set; }
    public VoucherUsage? VoucherUsage { get; set; }

    [NotMapped]
    public string? VoucherCode => VoucherUsage?.Voucher?.Code;
}

public static class OrderStatuses
{
    public const string AwaitingPayment = "Chờ thanh toán";
    public const string Pending = "Chờ xác nhận";
    public const string Confirmed = "Đã xác nhận";
    public const string Shipping = "Đang giao";
    public const string Delivered = "Đã giao";
    public const string Cancelled = "Huỷ";

    public static readonly string[] All = { AwaitingPayment, Pending, Confirmed, Shipping, Delivered, Cancelled };
}

public static class RefundStatuses
{
    public const string NotRequired = "Không cần hoàn tiền";
    public const string PendingManual = "Cần hoàn tiền thủ công";
    public const string Refunded = "Đã hoàn tiền";

    public static readonly string[] All = { NotRequired, PendingManual, Refunded };
}

public static class InvoiceStatuses
{
    public const string NotIssued = "Chưa xuất";
    public const string Issued = "Đã xuất";
    public const string Synced = "Đã đồng bộ";
    public const string PaymentConfirmed = "Đã xác nhận thanh toán";
    public const string Cancelled = "Đã huỷ";
    public const string Error = "Lỗi";
}
