using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Services;

public class ProductService : IProductService
{
    private readonly AppDbContext _db;

    public ProductService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ProductViewModel> GetPagedProductsAsync(string? search, int? categoryId, decimal? minPrice, decimal? maxPrice, string? sortBy, int page, int pageSize)
    {
        page = Math.Max(page, 1);
        var query = _db.Products
            .Include(product => product.Category)
            .Include(product => product.Images)
            .AsQueryable();

        var searchTerms = ProductSearchIndex.QueryTerms(search);
        foreach (var term in searchTerms)
        {
            var token = term;
            query = query.Where(product => product.SearchTerms.Any(searchTerm => searchTerm.Term.StartsWith(token)));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(product => product.CategoryId == categoryId.Value);
        }

        if (minPrice.HasValue)
        {
            query = query.Where(product => product.Price >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(product => product.Price <= maxPrice.Value);
        }

        var normalizedSortBy = sortBy switch
        {
            "best_selling" or "discount_desc" or "newest" or "price_asc" or "price_desc" => sortBy,
            _ => "featured"
        };

        var completedOrderItems = _db.OrderItems
            .Where(item => item.Order != null
                && (item.Order.Status == OrderStatuses.Delivered
                    || (item.Order.IsPaid && item.Order.Status != OrderStatuses.Cancelled)));

        query = normalizedSortBy switch
        {
            "best_selling" => query
                .OrderByDescending(product => completedOrderItems
                    .Where(item => item.ProductId == product.Id)
                    .Sum(item => (int?)item.Quantity) ?? 0)
                .ThenByDescending(product => product.IsFeatured)
                .ThenByDescending(product => product.CreatedAt),
            "discount_desc" => query
                .OrderByDescending(product => product.DiscountPercent)
                .ThenByDescending(product => product.CreatedAt),
            "newest" => query.OrderByDescending(product => product.CreatedAt),
            "price_asc" => query
                .OrderBy(product => product.DiscountPercent > 0
                    ? product.Price * (100 - product.DiscountPercent) / 100
                    : product.Price)
                .ThenByDescending(product => product.CreatedAt),
            "price_desc" => query
                .OrderByDescending(product => product.DiscountPercent > 0
                    ? product.Price * (100 - product.DiscountPercent) / 100
                    : product.Price)
                .ThenByDescending(product => product.CreatedAt),
            _ => query
                .OrderByDescending(product => product.IsFeatured)
                .ThenByDescending(product => completedOrderItems
                    .Where(item => item.ProductId == product.Id)
                    .Sum(item => (int?)item.Quantity) ?? 0)
                .ThenByDescending(product => product.CreatedAt)
        };

        var totalItems = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        var products = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ProductViewModel
        {
            Products = products,
            Categories = await GetCategoriesAsync(),
            Search = search,
            CategoryId = categoryId,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            SortBy = normalizedSortBy,
            CurrentPage = page,
            TotalPages = totalPages,
            TotalItems = totalItems,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<Product>> GetFeaturedProductsAsync(int count)
    {
        return await _db.Products
            .Include(product => product.Category)
            .Include(product => product.Images)
            .Where(product => product.IsFeatured)
            .OrderByDescending(product => product.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetLatestProductsAsync(int count)
    {
        return await _db.Products
            .Include(product => product.Category)
            .Include(product => product.Images)
            .OrderByDescending(product => product.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetRelatedProductsAsync(int productId, int categoryId, int take = 8)
    {
        return await _db.Products
            .Include(product => product.Category)
            .Include(product => product.Images.OrderBy(image => image.SortOrder))
            .Where(product => product.CategoryId == categoryId && product.Id != productId)
            .OrderByDescending(product => product.IsFeatured)
            .ThenByDescending(product => product.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<IEnumerable<Category>> GetCategoriesAsync()
    {
        return await _db.Categories.Include(category => category.Products).OrderBy(category => category.Name).ToListAsync();
    }

    public async Task<Product?> GetProductAsync(int id)
    {
        return await _db.Products
            .Include(product => product.Category)
            .Include(product => product.Images.OrderBy(image => image.SortOrder))
            .FirstOrDefaultAsync(product => product.Id == id);
    }

    public async Task<Product> CreateProductAsync(ProductFormViewModel model, string? imageUrl)
    {
        var product = new Product
        {
            Name = model.Name,
            Description = model.Description,
            Price = model.Price,
            Stock = model.Stock,
            DiscountPercent = model.DiscountPercent,
            CategoryId = model.CategoryId,
            IsFeatured = model.IsFeatured
        };
        var primaryImageUrl = NormalizeImageUrl(imageUrl) ?? NormalizeImageUrl(model.ImageUrl);
        if (primaryImageUrl is not null)
        {
            product.Images.Add(new ProductImage { ImageUrl = primaryImageUrl, SortOrder = 0 });
        }

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        await ProductSearchIndex.ReplaceTermsAsync(_db, product.Id);
        return product;
    }

    public async Task UpdateProductAsync(ProductFormViewModel model, string? imageUrl)
    {
        var product = await _db.Products
            .Include(row => row.Images)
            .FirstOrDefaultAsync(row => row.Id == model.Id);
        if (product is null)
        {
            return;
        }

        product.Name = model.Name;
        product.Description = model.Description;
        product.Price = model.Price;
        product.Stock = model.Stock;
        product.DiscountPercent = model.DiscountPercent;
        product.CategoryId = model.CategoryId;
        product.IsFeatured = model.IsFeatured;
        var primaryImageUrl = NormalizeImageUrl(imageUrl) ?? NormalizeImageUrl(model.ImageUrl);
        if (primaryImageUrl is not null)
        {
            var primaryImage = product.Images
                .OrderBy(row => row.SortOrder)
                .ThenBy(row => row.Id)
                .FirstOrDefault();
            if (primaryImage is null)
            {
                product.Images.Add(new ProductImage { ImageUrl = primaryImageUrl, SortOrder = 0 });
            }
            else
            {
                primaryImage.ImageUrl = primaryImageUrl;
                primaryImage.SortOrder = 0;
            }
        }

        await _db.SaveChangesAsync();
        await ProductSearchIndex.ReplaceTermsAsync(_db, product.Id);
    }

    public async Task DeleteProductAsync(int id)
    {
        var product = await _db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(row => row.Id == id);
        if (product is null)
        {
            return;
        }

        product.IsDeleted = true;
        await _db.SaveChangesAsync();
    }

    private static string? NormalizeImageUrl(string? imageUrl)
    {
        return string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
    }
}
