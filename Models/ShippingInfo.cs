using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models;

public class ShippingInfo
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }

    [StringLength(80)]
    public string Carrier { get; set; } = string.Empty;

    [StringLength(80)]
    public string TrackingCode { get; set; } = string.Empty;

    public DateTime? ShippedAt { get; set; }
    public DateTime? EstimatedDelivery { get; set; }

    [StringLength(60)]
    public string Status { get; set; } = ShippingStatuses.NotAssigned;
}

public static class ShippingStatuses
{
    public const string NotAssigned = "Chưa gán vận chuyển";
    public const string WaitingPickup = "Chờ lấy hàng";
    public const string InTransit = "Đang vận chuyển";
    public const string Delivered = "Đã giao";
    public const string Delayed = "Chậm trễ";

    public static readonly string[] All = { NotAssigned, WaitingPickup, InTransit, Delivered, Delayed };
}
