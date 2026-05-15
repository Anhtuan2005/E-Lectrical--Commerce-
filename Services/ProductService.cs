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
        var query = _db.Products.Include(product => product.Category).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(product => product.Name.Contains(search) || product.Description.Contains(search));
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

        query = sortBy switch
        {
            "price_asc" => query.OrderBy(product => product.Price),
            "price_desc" => query.OrderByDescending(product => product.Price),
            "discount_desc" => query.OrderByDescending(product => product.DiscountPercent).ThenBy(product => product.Price),
            "name_asc" => query.OrderBy(product => product.Name),
            _ => query.OrderByDescending(product => product.CreatedAt)
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
            SortBy = sortBy,
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
            .OrderByDescending(product => product.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetRelatedProductsAsync(int productId, int categoryId, int take = 8)
    {
        return await _db.Products
            .Include(product => product.Category)
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
            ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? model.ImageUrl : imageUrl,
            IsFeatured = model.IsFeatured
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        if (!string.IsNullOrWhiteSpace(product.ImageUrl))
        {
            _db.ProductImages.Add(new ProductImage { ProductId = product.Id, ImageUrl = product.ImageUrl, SortOrder = 0 });
            await _db.SaveChangesAsync();
        }
        return product;
    }

    public async Task UpdateProductAsync(ProductFormViewModel model, string? imageUrl)
    {
        var product = await _db.Products.FirstOrDefaultAsync(row => row.Id == model.Id);
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
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            product.ImageUrl = imageUrl;
        }
        else if (!string.IsNullOrWhiteSpace(model.ImageUrl))
        {
            product.ImageUrl = model.ImageUrl;
        }

        await _db.SaveChangesAsync();
        if (!string.IsNullOrWhiteSpace(product.ImageUrl)
            && !await _db.ProductImages.AnyAsync(image => image.ProductId == product.Id && image.ImageUrl == product.ImageUrl))
        {
            _db.ProductImages.Add(new ProductImage { ProductId = product.Id, ImageUrl = product.ImageUrl, SortOrder = 0 });
            await _db.SaveChangesAsync();
        }
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
}
