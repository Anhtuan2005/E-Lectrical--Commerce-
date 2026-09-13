using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("Admin/Banner")]
public class AdminBannerController : Controller
{
    private readonly AppDbContext _db;
    private readonly IImageStorageService _imageStorage;

    public AdminBannerController(AppDbContext db, IImageStorageService imageStorage)
    {
        _db = db;
        _imageStorage = imageStorage;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        return View("~/Views/Admin/Banner/Index.cshtml", await _db.Banners.OrderBy(banner => banner.SortOrder).ToListAsync());
    }

    [HttpGet("Create")]
    public IActionResult Create()
    {
        return View("~/Views/Admin/Banner/Form.cshtml", new BannerFormViewModel());
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BannerFormViewModel model, IFormFile? imageFile)
    {
        model.Id = 0;
        ValidateBanner(model, imageFile, requireImage: true);
        if (!ModelState.IsValid)
        {
            return View("~/Views/Admin/Banner/Form.cshtml", model);
        }

        try
        {
            model.ImageUrl = await _imageStorage.SaveAsWebpAsync(imageFile, "banners", 1920, 1080, 84) ?? model.ImageUrl;
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("imageFile", ex.Message);
            return View("~/Views/Admin/Banner/Form.cshtml", model);
        }
        var banner = new Banner();
        ApplyForm(banner, model);
        _db.Banners.Add(banner);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã thêm banner.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var banner = await _db.Banners.FindAsync(id);
        return banner is null ? NotFound() : View("~/Views/Admin/Banner/Form.cshtml", new BannerFormViewModel
        {
            Id = banner.Id, Title = banner.Title, Subtitle = banner.Subtitle, ImageUrl = banner.ImageUrl,
            LinkUrl = banner.LinkUrl, ButtonText = banner.ButtonText, SortOrder = banner.SortOrder, IsActive = banner.IsActive
        });
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, BannerFormViewModel model, IFormFile? imageFile)
    {
        model.Id = id;
        ValidateBanner(model, imageFile, requireImage: true);
        if (!ModelState.IsValid)
        {
            return View("~/Views/Admin/Banner/Form.cshtml", model);
        }

        var banner = await _db.Banners.FindAsync(id);
        if (banner is null)
        {
            return NotFound();
        }

        try
        {
            model.ImageUrl = await _imageStorage.SaveAsWebpAsync(imageFile, "banners", 1920, 1080, 84)
                ?? model.ImageUrl
                ?? banner.ImageUrl;
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("imageFile", ex.Message);
            return View("~/Views/Admin/Banner/Form.cshtml", model);
        }
        ApplyForm(banner, model);
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
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sort([FromBody] List<int>? ids)
    {
        if (!ModelState.IsValid || ids is null || ids.Count == 0 || ids.Any(id => id <= 0) || ids.Distinct().Count() != ids.Count)
        {
            return BadRequest(new { message = "Danh sách banner không hợp lệ." });
        }
        var banners = await _db.Banners.Where(banner => ids.Contains(banner.Id)).ToListAsync();
        if (banners.Count != ids.Count)
        {
            return BadRequest(new { message = "Danh sách chứa banner không tồn tại." });
        }
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

    private static void ApplyForm(Banner banner, BannerFormViewModel model)
    {
        banner.Title = model.Title;
        banner.Subtitle = model.Subtitle ?? string.Empty;
        banner.ImageUrl = model.ImageUrl ?? string.Empty;
        banner.LinkUrl = model.LinkUrl ?? string.Empty;
        banner.ButtonText = model.ButtonText;
        banner.SortOrder = model.SortOrder;
        banner.IsActive = model.IsActive;
    }

    private void ValidateBanner(BannerFormViewModel model, IFormFile? imageFile, bool requireImage)
    {
        model.Title = (model.Title ?? string.Empty).Trim();
        model.Subtitle = (model.Subtitle ?? string.Empty).Trim();
        model.ImageUrl = (model.ImageUrl ?? string.Empty).Trim();
        model.LinkUrl = (model.LinkUrl ?? string.Empty).Trim();
        model.ButtonText = (model.ButtonText ?? string.Empty).Trim();

        if (model.Title.Length == 0)
        {
            ModelState.AddModelError(nameof(Banner.Title), "Vui lòng nhập tiêu đề.");
        }

        if (requireImage && imageFile is not { Length: > 0 } && model.ImageUrl.Length == 0)
        {
            ModelState.AddModelError(nameof(Banner.ImageUrl), "Vui lòng nhập URL hoặc tải ảnh banner.");
        }

        if (model.ImageUrl.Length > 0 && !IsSafeLink(model.ImageUrl))
        {
            ModelState.AddModelError(nameof(Banner.ImageUrl), "URL ảnh phải là đường dẫn nội bộ hoặc địa chỉ HTTP/HTTPS.");
        }

        if (model.LinkUrl.Length > 0 && !IsSafeLink(model.LinkUrl))
        {
            ModelState.AddModelError(nameof(Banner.LinkUrl), "Link đích phải là đường dẫn nội bộ hoặc địa chỉ HTTP/HTTPS.");
        }
    }

    private bool IsSafeLink(string value)
    {
        return Url.IsLocalUrl(value)
            || (Uri.TryCreate(value, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));
    }

}
