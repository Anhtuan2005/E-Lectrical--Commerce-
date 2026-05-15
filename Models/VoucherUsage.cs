using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models;

public class VoucherUsage
{
    public int Id { get; set; }

    public int VoucherId { get; set; }
    public Voucher? Voucher { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public DateTime UsedAt { get; set; } = DateTime.UtcNow;
}
