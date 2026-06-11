using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models;

public class CustomerSegmentMember
{
    public int Id { get; set; }

    public int CustomerSegmentId { get; set; }
    public CustomerSegment? CustomerSegment { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public int TotalOrders { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTime? LastOrderAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
