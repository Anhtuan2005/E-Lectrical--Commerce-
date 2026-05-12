using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models;

public class Product
{
    public int Id { get; set; }

    [Required, StringLength(180)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int Stock { get; set; }

    [StringLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public bool IsFeatured { get; set; }

    [Range(0, 100)]
    public int DiscountPercent { get; set; }

    public decimal SalePrice => DiscountPercent > 0
        ? Math.Round(Price * (1 - DiscountPercent / 100m), 0)
        : Price;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
}
