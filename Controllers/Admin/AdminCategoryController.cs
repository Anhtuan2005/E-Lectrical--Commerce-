using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("Admin/Category")]
public class AdminCategoryController : Controller
{
    private readonly AppDbContext _db;

    public AdminCategoryController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var categories = await _db.Categories
            .Include(category => category.Products)
            .OrderBy(category => category.Name)
            .ToListAsync();
        return View("~/Views/Admin/Category/Index.cshtml", categories);
    }

    [HttpGet("Create")]
    public IActionResult Create()
    {
        return View("~/Views/Admin/Category/Form.cshtml", new Category());
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Description")] Category model)
    {
        ModelState.Remove(nameof(Category.Slug));
        model.Name = (model.Name ?? string.Empty).Trim();
        if (model.Name.Length == 0)
        {
            ModelState.AddModelError(nameof(Category.Name), "Vui lòng nhập tên danh mục");
        }
        if (!ModelState.IsValid)
        {
            return View("~/Views/Admin/Category/Form.cshtml", model);
        }

        model.Name = model.Name.Trim();
        model.Slug = await UniqueSlugAsync(model.Name);
        _db.Categories.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã tạo danh mục.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var category = await _db.Categories.FindAsync(id);
        return category is null ? NotFound() : View("~/Views/Admin/Category/Form.cshtml", category);
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Name,Description")] Category model)
    {
        model.Id = id;
        ModelState.Remove(nameof(Category.Slug));
        model.Name = (model.Name ?? string.Empty).Trim();
        if (model.Name.Length == 0)
        {
            ModelState.AddModelError(nameof(Category.Name), "Vui lòng nhập tên danh mục");
        }
        if (!ModelState.IsValid)
        {
            return View("~/Views/Admin/Category/Form.cshtml", model);
        }

        var category = await _db.Categories.FindAsync(id);
        if (category is null)
        {
            return NotFound();
        }

        category.Name = model.Name.Trim();
        category.Description = model.Description;
        category.Slug = await UniqueSlugAsync(model.Name, id);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật danh mục.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await CategoryDeletion.DeleteAsync(_db, id);
        if (result == CategoryDeleteResult.NotFound)
        {
            return NotFound();
        }

        if (result == CategoryDeleteResult.InUse)
        {
            TempData["Error"] = "Không thể xoá danh mục còn sản phẩm, kể cả sản phẩm đã ẩn. Hãy chuyển sản phẩm sang danh mục khác trước.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "Đã xoá danh mục.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<string> UniqueSlugAsync(string name, int? currentId = null)
    {
        var slug = SlugGenerator.Generate(name);
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = Guid.NewGuid().ToString("N")[..8];
        }

        var candidate = slug;
        var index = 2;
        while (await _db.Categories.AnyAsync(row => row.Slug == candidate && row.Id != currentId))
        {
            candidate = $"{slug}-{index++}";
        }

        return candidate;
    }

}
