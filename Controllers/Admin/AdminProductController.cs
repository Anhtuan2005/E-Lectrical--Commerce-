using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace EcommerceApp.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("Admin/Product")]
public class AdminProductController : Controller
{
    private readonly IProductService _productService;
    private readonly IWebHostEnvironment _environment;
    private readonly AppDbContext _db;

    public AdminProductController(IProductService productService, IWebHostEnvironment environment, AppDbContext db)
    {
        _productService = productService;
        _environment = environment;
        _db = db;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, int? categoryId, int page = 1)
    {
        return View("~/Views/Admin/Product/Index.cshtml", await _productService.GetPagedProductsAsync(search, categoryId, null, null, "newest", page, 20));
    }

    [HttpGet("Create")]
    public async Task<IActionResult> Create()
    {
        return View("~/Views/Admin/Product/Form.cshtml", new ProductFormViewModel { Categories = await _productService.GetCategoriesAsync() });
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductFormViewModel model, IFormFile? imageFile)
    {
        model.Categories = await _productService.GetCategoriesAsync();
        if (!ModelState.IsValid)
        {
            return View("~/Views/Admin/Product/Form.cshtml", model);
        }

        await _productService.CreateProductAsync(model, await SaveImageAsync(imageFile));
        TempData["Success"] = "Đã thêm sản phẩm.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var product = await _productService.GetProductAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        return View("~/Views/Admin/Product/Form.cshtml", new ProductFormViewModel
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            Stock = product.Stock,
            DiscountPercent = product.DiscountPercent,
            CategoryId = product.CategoryId,
            ImageUrl = product.ImageUrl,
            IsFeatured = product.IsFeatured,
            Categories = await _productService.GetCategoriesAsync(),
            StockLogs = await _db.StockLogs
                .Include(log => log.ChangedByUser)
                .Where(log => log.ProductId == product.Id)
                .OrderByDescending(log => log.ChangedAt)
                .Take(20)
                .ToListAsync()
        });
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProductFormViewModel model, IFormFile? imageFile)
    {
        model.Id = id;
        model.Categories = await _productService.GetCategoriesAsync();
        if (!ModelState.IsValid)
        {
            return View("~/Views/Admin/Product/Form.cshtml", model);
        }

        var oldStock = await _db.Products
            .Where(product => product.Id == id)
            .Select(product => product.Stock)
            .FirstOrDefaultAsync();

        await _productService.UpdateProductAsync(model, await SaveImageAsync(imageFile));
        if (oldStock != model.Stock)
        {
            _db.StockLogs.Add(new StockLog
            {
                ProductId = id,
                ChangeAmount = model.Stock - oldStock,
                Reason = "Admin cập nhật tồn kho",
                ChangedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            });
            await _db.SaveChangesAsync();
        }
        TempData["Success"] = "Đã cập nhật sản phẩm.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _productService.DeleteProductAsync(id);
        TempData["Success"] = "Đã xoá sản phẩm.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("CreateCategory")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            var slug = ToSlug(name);
            if (await _db.Categories.AnyAsync(category => category.Slug == slug))
            {
                slug = $"{slug}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            }
            _db.Categories.Add(new Category { Name = name.Trim(), Slug = slug });
            await _db.SaveChangesAsync();
            TempData["Success"] = "Đã thêm danh mục.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("DeleteCategory/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _db.Categories.FindAsync(id);
        if (category is not null)
        {
            _db.Categories.Remove(category);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Đã xoá danh mục.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<string?> SaveImageAsync(IFormFile? imageFile)
    {
        if (imageFile is null || imageFile.Length == 0)
        {
            return null;
        }

        var uploadDir = Path.Combine(_environment.WebRootPath, "uploads", "products");
        Directory.CreateDirectory(uploadDir);
        var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(imageFile.FileName)}";
        var filePath = Path.Combine(uploadDir, fileName);
        await using var stream = System.IO.File.Create(filePath);
        await imageFile.CopyToAsync(stream);
        return $"/uploads/products/{fileName}";
    }

    private static string ToSlug(string value)
    {
        var slug = value.Trim().ToLowerInvariant();
        slug = Regex.Replace(slug, "[^a-z0-9\\s-]", "");
        slug = Regex.Replace(slug, "\\s+", "-");
        return string.IsNullOrWhiteSpace(slug) ? Guid.NewGuid().ToString("N")[..8] : slug;
    }
}
