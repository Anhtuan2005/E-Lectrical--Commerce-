using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models;

public class Review
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    [Range(1, 5)] public int Rating { get; set; }
    [Required, MinLength(10), MaxLength(1000)] public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsApproved { get; set; } = true;
}
