using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using EcommerceApp.Services;

namespace EcommerceApp.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IProductService _productService;
    private readonly AppDbContext _db;

    public HomeController(ILogger<HomeController> logger, IProductService productService, AppDbContext db)
    {
        _logger = logger;
        _productService = productService;
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var model = new HomeViewModel
        {
            Banners = await _db.Banners.Where(banner => banner.IsActive).OrderBy(banner => banner.SortOrder).Take(3).ToListAsync(),
            Categories = await _productService.GetCategoriesAsync(),
            FeaturedProducts = await _productService.GetFeaturedProductsAsync(8),
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
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
