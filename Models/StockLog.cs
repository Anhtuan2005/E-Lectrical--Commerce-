using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models;

public class StockLog
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int ChangeAmount { get; set; }

    [Required, StringLength(300)]
    public string Reason { get; set; } = string.Empty;

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public string? ChangedByUserId { get; set; }
    public ApplicationUser? ChangedByUser { get; set; }
}
