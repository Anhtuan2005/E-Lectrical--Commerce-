using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models;

public class AbandonedCartReminder
{
    public int Id { get; set; }

    public int CartId { get; set; }
    public Cart? Cart { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public int? VoucherId { get; set; }
    public Voucher? Voucher { get; set; }

    [Required, StringLength(30)]
    public string RecoveryCode { get; set; } = string.Empty;

    public DateTime CartUpdatedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? EmailSentAt { get; set; }
    public DateTime? LastShownAt { get; set; }
}
