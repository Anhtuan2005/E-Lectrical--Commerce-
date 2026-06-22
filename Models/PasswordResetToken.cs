using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models;

public class PasswordResetToken
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    /// <summary>SHA-256 hash of the raw token. Raw token is never stored.</summary>
    [Required, MaxLength(128)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiredAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public ApplicationUser User { get; set; } = null!;
}
