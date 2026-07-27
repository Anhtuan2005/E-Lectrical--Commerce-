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
    private readonly AppDbContext _db;

    public HomeController(ILogger<HomeController> logger, IProductService productService, IRecommendationService recommendationService, AppDbContext db)
    {
        _logger = logger;
        _productService = productService;
        _recommendationService = recommendationService;
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var model = new HomeViewModel
        {
            Banners = await _db.Banners.Where(banner => banner.IsActive).OrderBy(banner => banner.SortOrder).Take(3).ToListAsync(),
            Categories = await _productService.GetCategoriesAsync(),
            FeaturedProducts = await _productService.GetFeaturedProductsAsync(8),
            PersonalizedProducts = await _recommendationService.GetRecommendationsAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                HttpContext.Session.Id,
                take: 8),
            FlashSaleProducts = await _db.Products
                .Include(product => product.Category)
                .Include(product => product.Images)
                .Where(product => product.DiscountPercent > 0 && product.Stock > 0)
                .OrderByDescending(product => product.DiscountPercent)
                .ThenBy(product => product.Price)
                .Take(6)
                .ToListAsync(),
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
