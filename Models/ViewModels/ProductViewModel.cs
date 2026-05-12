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

public class ProductFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mô tả")]
    public string Description { get; set; } = string.Empty;

    [Range(0, double.MaxValue, ErrorMessage = "Giá phải lớn hơn hoặc bằng 0")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Tồn kho không hợp lệ")]
    public int Stock { get; set; }

    [Range(0, 100, ErrorMessage = "Giảm giá phải từ 0 đến 100")]
    public int DiscountPercent { get; set; }

    public string ImageUrl { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public bool IsFeatured { get; set; }
    public IEnumerable<Category> Categories { get; set; } = Enumerable.Empty<Category>();
    public IEnumerable<StockLog> StockLogs { get; set; } = Enumerable.Empty<StockLog>();
}
