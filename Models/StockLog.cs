using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models;

public class StockLog
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int ChangeAmount { get; set; }

    [StringLength(40)]
    public string MovementType { get; set; } = StockMovementTypes.Manual;
    public int? QuantityBefore { get; set; }
    public int? QuantityAfter { get; set; }
    public int? OrderId { get; set; }
    public Order? Order { get; set; }

    [Required, StringLength(300)]
    public string Reason { get; set; } = string.Empty;

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public string? ChangedByUserId { get; set; }
    public ApplicationUser? ChangedByUser { get; set; }
}

public static class StockMovementTypes
{
    public const string Opening = "Tồn kho ban đầu";
    public const string Manual = "Điều chỉnh thủ công";
    public const string Sale = "Xuất cho đơn hàng";
    public const string Cancellation = "Hoàn kho do hủy đơn";
}
