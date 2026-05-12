using EcommerceApp.Data;
using EcommerceApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

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
    public async Task<IActionResult> Create(Category model)
    {
        ModelState.Remove(nameof(Category.Slug));
        if (!ModelState.IsValid)
        {
            return View("~/Views/Admin/Category/Form.cshtml", model);
        }

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
    public async Task<IActionResult> Edit(int id, Category model)
    {
        ModelState.Remove(nameof(Category.Slug));
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
        var category = await _db.Categories.Include(row => row.Products).FirstOrDefaultAsync(row => row.Id == id);
        if (category is null)
        {
            return NotFound();
        }

        if (category.Products.Any())
        {
            TempData["Error"] = "Không thể xoá danh mục đang có sản phẩm.";
            return RedirectToAction(nameof(Index));
        }

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã xoá danh mục.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<string> UniqueSlugAsync(string name, int? currentId = null)
    {
        var slug = ToSlug(name);
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

    private static string ToSlug(string value)
    {
        var slug = value.Trim().ToLowerInvariant();
        slug = Regex.Replace(slug, "[^a-z0-9\\s-]", "");
        slug = Regex.Replace(slug, "\\s+", "-");
        return slug.Trim('-');
    }
}
