using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using EcommerceApp.Services;
using System.Security.Claims;

namespace EcommerceApp.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IProductService _productService;
    private readonly IRecommendationService _recommendationService;
    private readonly ISprintFeatureService _sprintFeatures;
    private readonly AppDbContext _db;
    private readonly ICatalogScope? _catalogScope;

    public HomeController(ILogger<HomeController> logger, IProductService productService, IRecommendationService recommendationService, ISprintFeatureService sprintFeatures, AppDbContext db, ICatalogScope? catalogScope = null)
    {
        _logger = logger;
        _productService = productService;
        _recommendationService = recommendationService;
        _sprintFeatures = sprintFeatures;
        _db = db;
        _catalogScope = catalogScope;
    }

    public async Task<IActionResult> Index()
    {
        var banners = await _db.Banners
            .Where(banner => banner.IsActive)
            .OrderBy(banner => banner.SortOrder)
            .ToListAsync();
        if (_catalogScope is not null)
            banners = banners.Where(_catalogScope.IsPhoneBanner).ToList();

        var productNameQuery = _db.Products.AsNoTracking().AsQueryable();
        if (_catalogScope is not null)
            productNameQuery = _catalogScope.Products(productNameQuery);
        var productNames = await productNameQuery.Select(product => product.Name).ToListAsync();

        var flashSaleQuery = _db.Products
            .Include(product => product.Category)
            .Include(product => product.Images)
            .AsQueryable();
        if (_catalogScope is not null)
            flashSaleQuery = _catalogScope.Products(flashSaleQuery);

        var model = new HomeViewModel
        {
            Banners = banners,
            Brands = PhoneBrandCatalog.Summarize(productNames),
            FeaturedProducts = await _productService.GetFeaturedProductsAsync(8),
            PersonalizedProducts = _sprintFeatures.IsEnabled(3)
                ? await _recommendationService.GetRecommendationsAsync(
                    User.FindFirstValue(ClaimTypes.NameIdentifier),
                    HttpContext.Session.Id,
                    take: 8)
                : Array.Empty<Product>(),
            FlashSaleProducts = _sprintFeatures.IsEnabled(3)
                ? await flashSaleQuery
                    .Where(product => product.DiscountPercent > 0 && product.Stock > 0)
                    .OrderByDescending(product => product.DiscountPercent)
                    .ThenBy(product => product.Price)
                    .Take(6)
                    .ToListAsync()
                : Array.Empty<Product>(),
            LatestProducts = await _productService.GetLatestProductsAsync(10)
        };

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Status(int code)
    {
        if (code is < 400 or > 599)
        {
            code = StatusCodes.Status500InternalServerError;
        }

        Response.StatusCode = code;
        ViewData["StatusCode"] = code;
        if (code >= 500)
        {
            ViewData["StatusTitle"] = "Oops! Đã xảy ra lỗi.";
            ViewData["StatusMessage"] = "Hệ thống đang gặp sự cố. Vui lòng thử lại sau.";
        }
        else
        {
            ViewData["StatusTitle"] = code == 404 ? "Không tìm thấy trang" : "Có lỗi xảy ra";
            ViewData["StatusMessage"] = code == 404
                ? "Trang bạn đang mở có thể đã được di chuyển hoặc không còn tồn tại."
                : "Techvora chưa thể xử lý yêu cầu này. Bạn quay lại trang chủ hoặc thử lại sau một chút.";
        }
        return View("Status");
    }
}
