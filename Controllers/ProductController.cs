using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EcommerceApp.Controllers;

public class ProductController : Controller
{
    private readonly IProductService _productService;
    private readonly ICartService _cartService;
    private readonly IProductSpecService _productSpecService;
    private readonly IProductInteractionService _productInteractionService;
    private readonly IRecommendationService _recommendationService;
    private readonly AppDbContext _db;

    public ProductController(IProductService productService, ICartService cartService, IProductSpecService productSpecService, IProductInteractionService productInteractionService, IRecommendationService recommendationService, AppDbContext db)
    {
        _productService = productService;
        _cartService = cartService;
        _productSpecService = productSpecService;
        _productInteractionService = productInteractionService;
        _recommendationService = recommendationService;
        _db = db;
    }

    public async Task<IActionResult> Index(string? search, int? categoryId, decimal? minPrice, decimal? maxPrice, string? sortBy, int page = 1)
    {
        return View(await _productService.GetPagedProductsAsync(search, categoryId, minPrice, maxPrice, sortBy, page, 12));
    }

    [HttpGet]
    public async Task<IActionResult> Compare([FromQuery] int[]? ids)
    {
        var requestedIds = (ids ?? Array.Empty<int>())
            .Where(id => id > 0)
            .Distinct()
            .Take(4)
            .ToList();

        var products = await _db.Products
            .AsNoTracking()
            .Include(product => product.Category)
            .Include(product => product.Images)
            .Where(product => requestedIds.Contains(product.Id))
            .ToListAsync();
        var productsById = products.ToDictionary(product => product.Id);
        var orderedProducts = requestedIds
            .Where(productsById.ContainsKey)
            .Select(id => productsById[id])
            .ToList();
        var categoryId = orderedProducts.FirstOrDefault()?.CategoryId;
        var comparableProducts = categoryId.HasValue
            ? orderedProducts.Where(product => product.CategoryId == categoryId.Value).ToList()
            : new List<Product>();
        var items = comparableProducts
            .Select(product =>
            {
                return new ProductCompareItemViewModel
                {
                    Product = product,
                    Specs = _productSpecService.Build(product)
                        .GroupBy(spec => spec.Label, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.OrdinalIgnoreCase)
                };
            })
            .ToList();
        var labels = items
            .SelectMany(item => item.Specs.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return View(new ProductCompareViewModel
        {
            Products = items,
            SpecLabels = labels,
            CategoryName = comparableProducts.FirstOrDefault()?.Category?.Name,
            HasRejectedProducts = comparableProducts.Count != orderedProducts.Count
        });
    }

    [HttpGet]
    public async Task<IActionResult> Search(string? q)
    {
        var terms = ProductSearchIndex.QueryTerms(q);
        if (terms.Length == 0)
        {
            return Json(Array.Empty<object>());
        }

        var query = _db.Products
            .AsNoTracking()
            .Include(product => product.Category)
            .Include(product => product.Images)
            .Where(product => product.Stock > 0);

        foreach (var term in terms)
        {
            var token = term;
            query = query.Where(product => product.SearchTerms.Any(searchTerm => searchTerm.Term.StartsWith(token)));
        }

        var products = await query
            .OrderByDescending(product => product.IsFeatured)
            .ThenBy(product => product.Name)
            .Take(8)
            .ToListAsync();

        return Json(products.Select(product => new
            {
                id = product.Id,
                name = product.Name,
                price = product.SalePrice.ToString("N0") + " ₫",
                imageUrl = product.PrimaryImageUrl,
                category = product.Category?.Name,
                url = Url.Action(nameof(Detail), "Product", new { id = product.Id })
            }));
    }

    [HttpGet("/Product/Detail/{id:int}")]
    [HttpGet("/Detail/{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        var product = await _productService.GetProductAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        var reviews = await _db.Reviews
            .Include(review => review.User)
            .Include(review => review.Images.OrderBy(image => image.SortOrder))
            .Where(review => review.ProductId == id && review.IsApproved)
            .OrderByDescending(review => review.CreatedAt)
            .ToListAsync();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAuthenticated = !string.IsNullOrWhiteSpace(userId);
        var isAdmin = User.IsInRole("Admin");
        await _productInteractionService.TrackAsync(id, ProductInteractionEvents.DetailView, HttpContext);

        var hasPurchased = isAuthenticated && !isAdmin && await _db.Orders
            .AnyAsync(order => order.UserId == userId && order.Status == OrderStatuses.Delivered && order.Items.Any(item => item.ProductId == id));
        var hasReviewed = isAuthenticated && await _db.Reviews.AnyAsync(review => review.UserId == userId && review.ProductId == id);

        var model = new ProductDetailViewModel
        {
            Product = product,
            RecommendedProducts = await _recommendationService.GetRecommendationsAsync(
                userId,
                HttpContext.Session.Id,
                take: 8,
                anchorProductId: product.Id),
            RelatedProducts = await _productService.GetRelatedProductsAsync(product.Id, product.CategoryId),
            Reviews = reviews,
            TechnicalSpecs = _productSpecService.Build(product),
            ReviewCount = reviews.Count,
            AverageRating = reviews.Any() ? reviews.Average(review => review.Rating) : 0,
            RatingDistribution = Enumerable.Range(1, 5).ToDictionary(star => star, star => reviews.Count(review => review.Rating == star)),
            IsWishlisted = isAuthenticated && await _db.WishlistItems.AnyAsync(item => item.UserId == userId && item.ProductId == id),
            CanReview = !isAdmin && hasPurchased && !hasReviewed,
            HasPurchased = hasPurchased,
            HasReviewed = hasReviewed,
            IsAuthenticated = isAuthenticated
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Track(int productId, string eventType)
    {
        await _productInteractionService.TrackAsync(productId, eventType, HttpContext);
        return NoContent();
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
    {
        if (User.IsInRole("Admin"))
        {
            TempData["Error"] = "Tài khoản admin chỉ được xem và kiểm tra, không thể mua hàng.";
            return RedirectToAction("Detail", new { id = productId });
        }

        try
        {
            await _cartService.AddAsync(productId, quantity, User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.Session.Id);
            await _productInteractionService.TrackAsync(productId, ProductInteractionEvents.AddToCart, HttpContext);
            TempData["Success"] = "Đã thêm sản phẩm vào giỏ hàng.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Detail", new { id = productId });
    }
}
