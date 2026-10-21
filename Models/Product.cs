using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceApp.Models;

public class Product
{
    public int Id { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    [Required, StringLength(180)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int Stock { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public bool IsFeatured { get; set; }
    public bool IsDeleted { get; set; }

    [EnumDataType(typeof(CpuSocket))]
    public CpuSocket? Socket { get; set; }

    [EnumDataType(typeof(MemoryStandard))]
    public MemoryStandard? MemoryType { get; set; }

    // PSU rated output, or the component's documented power budget. Null means unknown.
    [Range(1, 5000)]
    public int? PowerWatts { get; set; }

    // Only administrator-entered values with a cited technical source are used for advice.
    [Range(1, 256)] public int? CpuCores { get; set; }
    [Range(1, 512)] public int? CpuThreads { get; set; }
    [Range(1, 256)] public int? GpuVramGb { get; set; }
    [Range(1, 1024)] public int? MemoryCapacityGb { get; set; }
    [Range(1, 20000)] public int? MemorySpeedMt { get; set; }
    [Range(1, 1000000)] public int? StorageCapacityGb { get; set; }
    [Range(1, 100000)] public int? StorageReadMb { get; set; }
    [Range(1, 100000)] public int? StorageWriteMb { get; set; }
    [Range(1, 32)] public int? M2Slots { get; set; }
    [StringLength(500)] public string? SpecificationSourceUrl { get; set; }

    [Range(0, 100)]
    public int DiscountPercent { get; set; }

    public decimal SalePrice => DiscountPercent > 0
        ? Math.Round(Price * (100 - DiscountPercent) / 100m, 0, MidpointRounding.AwayFromZero)
        : Price;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<ProductSearchTerm> SearchTerms { get; set; } = new List<ProductSearchTerm>();
    public ICollection<ProductInteraction> Interactions { get; set; } = new List<ProductInteraction>();

    [NotMapped]
    public string PrimaryImageUrl => Images
        .OrderBy(image => image.SortOrder)
        .ThenBy(image => image.Id)
        .Select(image => image.ImageUrl)
        .FirstOrDefault(url => !string.IsNullOrWhiteSpace(url))
        ?? "/images/placeholder.svg";
}
