using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models.ViewModels;

public class ProductViewModel
{
    public IEnumerable<Product> Products { get; set; } = Enumerable.Empty<Product>();
    public IEnumerable<Category> Categories { get; set; } = Enumerable.Empty<Category>();
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? SortBy { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalItems { get; set; }
    public int PageSize { get; set; } = 12;
    public int FromItem => TotalItems == 0 ? 0 : ((CurrentPage - 1) * PageSize) + 1;
    public int ToItem => Math.Min(CurrentPage * PageSize, TotalItems);
}

public class ProductFormViewModel : IValidatableObject
{
    public int Id { get; set; }
    public string? RowVersion { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm")]
    [StringLength(180, ErrorMessage = "Tên sản phẩm không được vượt quá 180 ký tự")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mô tả")]
    [StringLength(2000, ErrorMessage = "Mô tả không được vượt quá 2.000 ký tự")]
    public string Description { get; set; } = string.Empty;

    [Range(0, double.MaxValue, ErrorMessage = "Giá không hợp lệ")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Tồn kho không hợp lệ")]
    public int Stock { get; set; }

    [Range(0, 100, ErrorMessage = "Giảm giá phải từ 0 đến 100")]
    public int DiscountPercent { get; set; }

    [StringLength(500, ErrorMessage = "URL ảnh không được vượt quá 500 ký tự")]
    public string? ImageUrl { get; set; }
    public string? ImageUrls { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn danh mục")]
    public int CategoryId { get; set; }
    public bool IsFeatured { get; set; }
    [EnumDataType(typeof(CpuSocket))]
    public CpuSocket? Socket { get; set; }
    [EnumDataType(typeof(MemoryStandard))]
    public MemoryStandard? MemoryType { get; set; }
    [Range(1, 5000, ErrorMessage = "Công suất phải từ 1 đến 5.000 W")]
    public int? PowerWatts { get; set; }
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
    public IEnumerable<Category> Categories { get; set; } = Enumerable.Empty<Category>();
    public IEnumerable<StockLog> StockLogs { get; set; } = Enumerable.Empty<StockLog>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasAdvisorySpec = CpuCores.HasValue || CpuThreads.HasValue || GpuVramGb.HasValue ||
            MemoryCapacityGb.HasValue || MemorySpeedMt.HasValue || StorageCapacityGb.HasValue ||
            StorageReadMb.HasValue || StorageWriteMb.HasValue || M2Slots.HasValue;
        if ((hasAdvisorySpec || !string.IsNullOrWhiteSpace(SpecificationSourceUrl)) &&
            (!Uri.TryCreate(SpecificationSourceUrl, UriKind.Absolute, out var source) ||
             source.Scheme != Uri.UriSchemeHttps))
        {
            yield return new ValidationResult("Nhập URL HTTPS của tài liệu thông số trước khi dùng gợi ý so sánh.",
                new[] { nameof(SpecificationSourceUrl) });
        }
        const decimal maxDatabaseAmount = 9_999_999_999_999_999.99m;
        if (Price > maxDatabaseAmount)
        {
            yield return new ValidationResult("Giá vượt quá giới hạn cho phép", new[] { nameof(Price) });
        }

        var urls = (ImageUrls ?? string.Empty)
            .Split(new[] { "\r\n", "\n", "\r", "," }, StringSplitOptions.RemoveEmptyEntries)
            .Select(url => url.Trim())
            .Where(url => url.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (urls.Count > 8)
        {
            yield return new ValidationResult("Mỗi sản phẩm có tối đa 8 URL ảnh", new[] { nameof(ImageUrls) });
        }

        if (urls.Any(url => url.Length > 500))
        {
            yield return new ValidationResult("Mỗi URL ảnh không được vượt quá 500 ký tự", new[] { nameof(ImageUrls) });
        }
    }
}
