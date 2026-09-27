using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EcommerceApp.Controllers;

[Authorize]
public class WishlistController : Controller
{
    private readonly AppDbContext _db;
    private readonly IProductInteractionService _productInteractionService;
    private readonly ISprintFeatureService _sprintFeatures;
    private readonly ICatalogScope? _catalogScope;

    public WishlistController(AppDbContext db, IProductInteractionService productInteractionService, ISprintFeatureService sprintFeatures, ICatalogScope? catalogScope = null)
    {
        _db = db;
        _productInteractionService = productInteractionService;
        _sprintFeatures = sprintFeatures;
        _catalogScope = catalogScope;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var query = _db.WishlistItems
            .Include(item => item.Product)
            .ThenInclude(product => product!.Category)
            .Where(item => item.UserId == userId);
        if (_catalogScope is not null)
            query = query.Where(item => item.Product != null && item.Product.Category != null
                && (item.Product.Category.Slug == PhoneCatalogScope.RootSlug
                    || item.Product.Category.Slug.StartsWith("dien-thoai-")));
        var items = await query
            .OrderByDescending(item => item.AddedAt)
            .ToListAsync();

        return View(items);
    }

    [HttpPost("Wishlist/Toggle/{productId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int productId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var products = _db.Products.AsQueryable();
        if (_catalogScope is not null)
            products = _catalogScope.Products(products);
        if (!await products.AnyAsync(product => product.Id == productId))
        {
            return NotFound(new { message = "Sản phẩm không tồn tại hoặc đã ngừng bán." });
        }

        var item = await _db.WishlistItems.FirstOrDefaultAsync(row => row.UserId == userId && row.ProductId == productId);
        var isWishlisted = item is null;

        if (item is null)
        {
            _db.WishlistItems.Add(new WishlistItem { UserId = userId, ProductId = productId });
        }
        else
        {
            _db.WishlistItems.Remove(item);
        }

        await _db.SaveChangesAsync();
        if (_sprintFeatures.IsEnabled(3))
        {
            await _productInteractionService.TrackAsync(
                productId,
                isWishlisted ? ProductInteractionEvents.WishlistAdd : ProductInteractionEvents.WishlistRemove,
                HttpContext);
        }
        var countQuery = _db.WishlistItems.Where(row => row.UserId == userId);
        if (_catalogScope is not null)
            countQuery = countQuery.Where(item => item.Product != null && item.Product.Category != null
                && (item.Product.Category.Slug == PhoneCatalogScope.RootSlug
                    || item.Product.Category.Slug.StartsWith("dien-thoai-")));
        var count = await countQuery.CountAsync();

        return Json(new
        {
            isWishlisted,
            count,
            message = isWishlisted ? "Đã thêm vào yêu thích." : "Đã bỏ khỏi yêu thích."
        });
    }
}
