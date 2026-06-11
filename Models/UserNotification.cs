using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models;

public class UserNotification
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required, StringLength(120)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string Message { get; set; } = string.Empty;

    [Required, StringLength(40)]
    public string Type { get; set; } = NotificationTypes.System;

    [StringLength(500)]
    public string? LinkUrl { get; set; }

    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}

public static class NotificationTypes
{
    public const string Order = "order";
    public const string Support = "support";
    public const string Voucher = "voucher";
    public const string Wishlist = "wishlist";
    public const string System = "system";
}
