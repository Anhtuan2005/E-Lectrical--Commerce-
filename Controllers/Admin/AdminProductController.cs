using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EcommerceApp.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("Admin/Product")]
public class AdminProductController : Controller
{
    private readonly IProductService _productService;
    private readonly IImageStorageService _imageStorage;
    private readonly AppDbContext _db;

    public AdminProductController(IProductService productService, IImageStorageService imageStorage, AppDbContext db)
    {
        _productService = productService;
        _imageStorage = imageStorage;
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
    public async Task<IActionResult> Create(ProductFormViewModel model, IFormFile? imageFile, List<IFormFile>? imageFiles)
    {
        model.Categories = await _productService.GetCategoriesAsync();
        if (!model.Categories.Any(category => category.Id == model.CategoryId))
        {
            ModelState.AddModelError(nameof(model.CategoryId), "Danh mục đã chọn không tồn tại.");
        }

        if (!ModelState.IsValid)
        {
            return View("~/Views/Admin/Product/Form.cshtml", model);
        }

        List<string> imageUrls;
        try
        {
            imageUrls = await SaveProductImagesAsync(imageFile, imageFiles);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("imageFiles", ex.Message);
            return View("~/Views/Admin/Product/Form.cshtml", model);
        }

        await _productService.CreateProductAsync(model, imageUrls);
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

        var imageUrls = product.Images
            .OrderBy(image => image.SortOrder)
            .ThenBy(image => image.Id)
            .Select(image => image.ImageUrl)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return View("~/Views/Admin/Product/Form.cshtml", new ProductFormViewModel
        {
            Id = product.Id,
            RowVersion = Convert.ToBase64String(product.RowVersion),
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            Stock = product.Stock,
            DiscountPercent = product.DiscountPercent,
            CategoryId = product.CategoryId,
            ImageUrl = imageUrls.FirstOrDefault() ?? product.PrimaryImageUrl,
            ImageUrls = string.Join(Environment.NewLine, imageUrls),
            IsFeatured = product.IsFeatured,
            Socket = product.Socket,
            MemoryType = product.MemoryType,
            PowerWatts = product.PowerWatts,
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
    public async Task<IActionResult> Edit(int id, ProductFormViewModel model, IFormFile? imageFile, List<IFormFile>? imageFiles)
    {
        model.Id = id;
        model.Categories = await _productService.GetCategoriesAsync();
        if (!model.Categories.Any(category => category.Id == model.CategoryId))
        {
            ModelState.AddModelError(nameof(model.CategoryId), "Danh mục đã chọn không tồn tại.");
        }

        if (!ModelState.IsValid)
        {
            return View("~/Views/Admin/Product/Form.cshtml", model);
        }

        List<string> imageUrls;
        try
        {
            imageUrls = await SaveProductImagesAsync(imageFile, imageFiles);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("imageFiles", ex.Message);
            return View("~/Views/Admin/Product/Form.cshtml", model);
        }

        try
        {
            await _productService.UpdateProductAsync(model, imageUrls);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError("", "Sản phẩm hoặc tồn kho đã thay đổi từ khi bạn mở trang. Hãy tải lại trang và kiểm tra trước khi lưu lại.");
            Response.StatusCode = StatusCodes.Status409Conflict;
            return View("~/Views/Admin/Product/Form.cshtml", model);
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

    [HttpPost("AdjustStock/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdjustStock(int id, int change, string reason)
    {
        var updated = ModelState.IsValid && await _productService.AdjustStockAsync(
            id, change, reason, User.FindFirstValue(ClaimTypes.NameIdentifier));
        TempData[updated ? "Success" : "Error"] = updated
            ? "Đã điều chỉnh tồn kho và lưu lịch sử."
            : "Nhập số lượng tăng/giảm khác 0 và lý do (tối đa 300 ký tự). Không thể giảm quá số hàng còn trong kho.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("CreateCategory")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(string name)
    {
        var normalizedName = (name ?? string.Empty).Trim();
        if (normalizedName.Length is > 0 and <= 100)
        {
            var slug = SlugGenerator.Generate(normalizedName);
            if (string.IsNullOrWhiteSpace(slug))
            {
                slug = Guid.NewGuid().ToString("N")[..8];
            }
            var baseSlug = slug;
            var suffix = 2;
            while (await _db.Categories.AnyAsync(category => category.Slug == slug))
            {
                slug = $"{baseSlug}-{suffix++}";
            }
            _db.Categories.Add(new Category { Name = normalizedName, Slug = slug });
            await _db.SaveChangesAsync();
            TempData["Success"] = "Đã thêm danh mục.";
        }
        else
        {
            TempData["Error"] = "Tên danh mục phải có từ 1 đến 100 ký tự.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("DeleteCategory/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var result = await CategoryDeletion.DeleteAsync(_db, id);
        if (result == CategoryDeleteResult.NotFound) return NotFound();
        TempData[result == CategoryDeleteResult.Deleted ? "Success" : "Error"] = result == CategoryDeleteResult.Deleted
            ? "Đã xoá danh mục."
            : "Không thể xoá danh mục còn sản phẩm, kể cả sản phẩm đã ẩn. Hãy chuyển sản phẩm sang danh mục khác trước.";

        return RedirectToAction(nameof(Index));
    }

    private async Task<List<string>> SaveProductImagesAsync(IFormFile? imageFile, IReadOnlyCollection<IFormFile>? imageFiles)
    {
        var files = new List<IFormFile>();
        if (imageFile is { Length: > 0 })
        {
            files.Add(imageFile);
        }

        if (imageFiles is not null)
        {
            files.AddRange(imageFiles.Where(file => file.Length > 0));
        }

        if (files.Count > 8)
        {
            throw new InvalidOperationException("Tải tối đa 8 ảnh cho một sản phẩm.");
        }

        var urls = new List<string>();
        foreach (var file in files)
        {
            var url = await _imageStorage.SaveAsWebpAsync(file, "products", 1600, 1600, 82);
            if (!string.IsNullOrWhiteSpace(url))
            {
                urls.Add(url);
            }
        }

        return urls;
    }
}
