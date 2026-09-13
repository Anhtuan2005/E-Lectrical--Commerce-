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
            query = query.Where(product => (product.DiscountPercent > 0
                ? Math.Round(product.Price * (100 - product.DiscountPercent) / 100m, 0)
                : product.Price) >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(product => (product.DiscountPercent > 0
                ? Math.Round(product.Price * (100 - product.DiscountPercent) / 100m, 0)
                : product.Price) <= maxPrice.Value);
        }

        var normalizedSortBy = sortBy switch
        {
            "best_selling" or "discount_desc" or "newest" or "price_asc" or "price_desc" => sortBy,
            _ => "featured"
        };

        var completedOrderItems = _db.OrderItems.WithRecognizedRevenue();

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
                    ? Math.Round(product.Price * (100 - product.DiscountPercent) / 100m, 0)
                    : product.Price)
                .ThenByDescending(product => product.CreatedAt),
            "price_desc" => query
                .OrderByDescending(product => product.DiscountPercent > 0
                    ? Math.Round(product.Price * (100 - product.DiscountPercent) / 100m, 0)
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
        page = Math.Min(page, totalPages);
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

    public async Task<Product> CreateProductAsync(ProductFormViewModel model, IEnumerable<string?> imageUrls)
    {
        var product = new Product
        {
            Name = model.Name,
            Description = model.Description,
            Price = model.Price,
            Stock = model.Stock,
            DiscountPercent = model.DiscountPercent,
            CategoryId = model.CategoryId,
            IsFeatured = model.IsFeatured,
            Socket = model.Socket,
            MemoryType = model.MemoryType,
            PowerWatts = model.PowerWatts
        };

        ReplaceProductImages(product, BuildImageUrls(model, imageUrls), removeExisting: false);

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        await ProductSearchIndex.ReplaceTermsAsync(_db, product.Id);
        return product;
    }

    public async Task UpdateProductAsync(ProductFormViewModel model, IEnumerable<string?> imageUrls)
    {
        byte[] version;
        try { version = Convert.FromBase64String(model.RowVersion ?? ""); }
        catch (FormatException) { throw new DbUpdateConcurrencyException("Phiên bản sản phẩm không hợp lệ."); }
        if (version.Length != 8) throw new DbUpdateConcurrencyException("Thiếu phiên bản sản phẩm.");
        var normalizedImageUrls = BuildImageUrls(model, imageUrls);
        await DatabaseTransaction.ExecuteAsync(_db, async () =>
        {
            var product = await _db.Products
                .Include(row => row.Images)
                .FirstOrDefaultAsync(row => row.Id == model.Id);
            if (product is null || !product.RowVersion.SequenceEqual(version))
                throw new DbUpdateConcurrencyException("Sản phẩm đã được thay đổi hoặc xoá.");
            _db.Entry(product).Property(row => row.RowVersion).OriginalValue = version;
            // Even image-only edits must check the root product version when saving.
            _db.Entry(product).Property(row => row.Name).IsModified = true;

            product.Name = model.Name;
            product.Description = model.Description;
            product.Price = model.Price;
            product.DiscountPercent = model.DiscountPercent;
            product.CategoryId = model.CategoryId;
            product.IsFeatured = model.IsFeatured;
            product.Socket = model.Socket;
            product.MemoryType = model.MemoryType;
            product.PowerWatts = model.PowerWatts;

            if (normalizedImageUrls.Count > 0)
            {
                ReplaceProductImages(product, normalizedImageUrls, removeExisting: true);
            }

            await _db.SaveChangesAsync();
            await ProductSearchIndex.ReplaceTermsAsync(_db, product.Id);
            return true;
        });
    }

    public Task<bool> AdjustStockAsync(int id, int change, string reason, string? actorId)
    {
        if (change == 0 || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 300)
            return Task.FromResult(false);
        return DatabaseTransaction.ExecuteAsync(_db, async () =>
        {
            var updated = await _db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE Products SET Stock = Stock + {change}
                WHERE Id = {id} AND IsDeleted = 0
                    AND CAST(Stock AS bigint) + {change} BETWEEN 0 AND 2147483647
                """);
            if (updated != 1) return false;
            _db.StockLogs.Add(new StockLog { ProductId = id, ChangeAmount = change,
                Reason = reason.Trim(), ChangedByUserId = actorId });
            await _db.SaveChangesAsync();
            return true;
        });
    }

    public async Task DeleteProductAsync(int id)
    {
        await _db.Products.IgnoreQueryFilters().Where(product => product.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(product => product.IsDeleted, true));
    }

    private static List<string> BuildImageUrls(ProductFormViewModel model, IEnumerable<string?> uploadedImageUrls)
    {
        var urls = new List<string>();
        AddImageUrls(urls, SplitImageUrls(model.ImageUrls));
        AddImageUrl(urls, model.ImageUrl);
        AddImageUrls(urls, uploadedImageUrls);
        return urls;
    }

    private static IEnumerable<string?> SplitImageUrls(string? imageUrls)
    {
        return string.IsNullOrWhiteSpace(imageUrls)
            ? Enumerable.Empty<string?>()
            : imageUrls.Split(new[] { "\r\n", "\n", "\r", "," }, StringSplitOptions.RemoveEmptyEntries);
    }

    private void ReplaceProductImages(Product product, IReadOnlyList<string> imageUrls, bool removeExisting)
    {
        if (removeExisting)
        {
            _db.ProductImages.RemoveRange(product.Images);
        }

        product.Images.Clear();
        for (var index = 0; index < imageUrls.Count; index++)
        {
            product.Images.Add(new ProductImage
            {
                ImageUrl = imageUrls[index],
                SortOrder = index
            });
        }
    }

    private static void AddImageUrls(List<string> urls, IEnumerable<string?> imageUrls)
    {
        foreach (var imageUrl in imageUrls)
        {
            AddImageUrl(urls, imageUrl);
        }
    }

    private static void AddImageUrl(List<string> urls, string? imageUrl)
    {
        var normalized = NormalizeImageUrl(imageUrl);
        if (normalized is not null && !urls.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            urls.Add(normalized);
        }
    }

    private static string? NormalizeImageUrl(string? imageUrl)
    {
        return string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
    }
}
