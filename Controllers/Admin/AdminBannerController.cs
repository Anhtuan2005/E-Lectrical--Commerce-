using EcommerceApp.Data;
using EcommerceApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("Admin/Banner")]
public class AdminBannerController : Controller
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;

    public AdminBannerController(AppDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        return View("~/Views/Admin/Banner/Index.cshtml", await _db.Banners.OrderBy(banner => banner.SortOrder).ToListAsync());
    }

    [HttpGet("Create")]
    public IActionResult Create()
    {
        return View("~/Views/Admin/Banner/Form.cshtml", new Banner());
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Banner model, IFormFile? imageFile)
    {
        if (!ModelState.IsValid)
        {
            return View("~/Views/Admin/Banner/Form.cshtml", model);
        }

        model.ImageUrl = await SaveImageAsync(imageFile) ?? model.ImageUrl;
        _db.Banners.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã thêm banner.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var banner = await _db.Banners.FindAsync(id);
        return banner is null ? NotFound() : View("~/Views/Admin/Banner/Form.cshtml", banner);
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Banner model, IFormFile? imageFile)
    {
        if (!ModelState.IsValid)
        {
            return View("~/Views/Admin/Banner/Form.cshtml", model);
        }

        var banner = await _db.Banners.FindAsync(id);
        if (banner is null)
        {
            return NotFound();
        }

        banner.Title = model.Title;
        banner.Subtitle = model.Subtitle;
        banner.LinkUrl = model.LinkUrl;
        banner.ButtonText = model.ButtonText;
        banner.SortOrder = model.SortOrder;
        banner.IsActive = model.IsActive;
        banner.ImageUrl = await SaveImageAsync(imageFile) ?? model.ImageUrl ?? banner.ImageUrl;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật banner.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var banner = await _db.Banners.FindAsync(id);
        if (banner is not null)
        {
            _db.Banners.Remove(banner);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Đã xoá banner.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("ToggleActive/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var banner = await _db.Banners.FindAsync(id);
        if (banner is not null)
        {
            banner.IsActive = !banner.IsActive;
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Sort")]
    public async Task<IActionResult> Sort([FromBody] List<int> ids)
    {
        var banners = await _db.Banners.Where(banner => ids.Contains(banner.Id)).ToListAsync();
        for (var i = 0; i < ids.Count; i++)
        {
            var banner = banners.FirstOrDefault(row => row.Id == ids[i]);
            if (banner is not null)
            {
                banner.SortOrder = i + 1;
            }
        }

        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    private async Task<string?> SaveImageAsync(IFormFile? imageFile)
    {
        if (imageFile is null || imageFile.Length == 0)
        {
            return null;
        }

        var uploadDir = Path.Combine(_environment.WebRootPath, "uploads", "banners");
        Directory.CreateDirectory(uploadDir);
        var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(imageFile.FileName)}";
        var filePath = Path.Combine(uploadDir, fileName);
        await using var stream = System.IO.File.Create(filePath);
        await imageFile.CopyToAsync(stream);
        return $"/uploads/banners/{fileName}";
    }
}
