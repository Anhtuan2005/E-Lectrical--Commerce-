using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models;

public class ProductInteraction
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [StringLength(120)]
    public string SessionId { get; set; } = string.Empty;

    [Required, StringLength(40)]
    public string EventType { get; set; } = ProductInteractionEvents.DetailView;

    [StringLength(500)]
    public string? Referrer { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class ProductInteractionEvents
{
    public const string DetailView = "detail_view";
}
