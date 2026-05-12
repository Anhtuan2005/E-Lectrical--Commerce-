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
    private readonly AppDbContext _db;

    public ProductController(IProductService productService, ICartService cartService, AppDbContext db)
    {
        _productService = productService;
        _cartService = cartService;
        _db = db;
    }

    public async Task<IActionResult> Index(string? search, int? categoryId, decimal? minPrice, decimal? maxPrice, string? sortBy, int page = 1)
    {
        return View(await _productService.GetPagedProductsAsync(search, categoryId, minPrice, maxPrice, sortBy, page, 12));
    }

    [HttpGet]
    public async Task<IActionResult> Search(string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Json(Array.Empty<object>());
        }

        var products = await _db.Products
            .Where(product => product.Name.Contains(q))
            .OrderByDescending(product => product.IsFeatured)
            .ThenBy(product => product.Name)
            .Take(5)
            .ToListAsync();

        return Json(products.Select(product => new
            {
                id = product.Id,
                name = product.Name,
                price = product.SalePrice.ToString("N0") + " ₫",
                imageUrl = product.ImageUrl
            }));
    }

    public async Task<IActionResult> Detail(int id)
    {
        var product = await _productService.GetProductAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        var reviews = await _db.Reviews
            .Include(review => review.User)
            .Where(review => review.ProductId == id && review.IsApproved)
            .OrderByDescending(review => review.CreatedAt)
            .ToListAsync();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAuthenticated = !string.IsNullOrWhiteSpace(userId);
        var hasPurchased = isAuthenticated && await _db.Orders
            .AnyAsync(order => order.UserId == userId && order.Status == OrderStatuses.Delivered && order.Items.Any(item => item.ProductId == id));
        var hasReviewed = isAuthenticated && await _db.Reviews.AnyAsync(review => review.UserId == userId && review.ProductId == id);

        var model = new ProductDetailViewModel
        {
            Product = product,
            RelatedProducts = await _productService.GetRelatedProductsAsync(product.Id, product.CategoryId),
            Reviews = reviews,
            ReviewCount = reviews.Count,
            AverageRating = reviews.Any() ? reviews.Average(review => review.Rating) : 0,
            RatingDistribution = Enumerable.Range(1, 5).ToDictionary(star => star, star => reviews.Count(review => review.Rating == star)),
            IsWishlisted = isAuthenticated && await _db.WishlistItems.AnyAsync(item => item.UserId == userId && item.ProductId == id),
            CanReview = hasPurchased && !hasReviewed,
            HasPurchased = hasPurchased,
            HasReviewed = hasReviewed,
            IsAuthenticated = isAuthenticated
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
    {
        await _cartService.AddAsync(productId, quantity, User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.Session.Id);
        TempData["Success"] = "Đã thêm sản phẩm vào giỏ hàng.";
        return RedirectToAction("Detail", new { id = productId });
    }
}
