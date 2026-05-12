using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;

namespace EcommerceApp.Services;

public interface IProductService
{
    Task<ProductViewModel> GetPagedProductsAsync(string? search, int? categoryId, decimal? minPrice, decimal? maxPrice, string? sortBy, int page, int pageSize);
    Task<IEnumerable<Product>> GetFeaturedProductsAsync(int count);
    Task<IEnumerable<Product>> GetLatestProductsAsync(int count);
    Task<IEnumerable<Product>> GetRelatedProductsAsync(int productId, int categoryId, int take = 8);
    Task<IEnumerable<Category>> GetCategoriesAsync();
    Task<Product?> GetProductAsync(int id);
    Task<Product> CreateProductAsync(ProductFormViewModel model, string? imageUrl);
    Task UpdateProductAsync(ProductFormViewModel model, string? imageUrl);
    Task DeleteProductAsync(int id);
}
