using EcommerceApp.Data;
using EcommerceApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace EcommerceApp.Controllers;

[Authorize]
public class ReviewController : Controller
{
    private const int MaxReviewImages = 4;
    private const long MaxReviewImageBytes = 3 * 1024 * 1024;
    private static readonly HashSet<string> AllowedReviewImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;

    public ReviewController(AppDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("review")]
    public async Task<IActionResult> Submit(int productId, [Range(1, 5)] int rating, [Required, MinLength(10), MaxLength(1000)] string comment, List<IFormFile>? images)
    {
        if (User.IsInRole("Admin"))
        {
            return Json(new { success = false, message = "Tài khoản admin chỉ được xem và kiểm duyệt, không thể đánh giá sản phẩm." });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var canReview = await _db.Orders.AnyAsync(order => order.UserId == userId && order.Status == OrderStatuses.Delivered && order.Items.Any(item => item.ProductId == productId));
        if (!canReview)
        {
            return Json(new { success = false, message = "Bạn cần mua sản phẩm trước khi đánh giá." });
        }

        if (await _db.Reviews.AnyAsync(review => review.UserId == userId && review.ProductId == productId))
        {
            return Json(new { success = false, message = "Bạn đã đánh giá sản phẩm này." });
        }

        if (!ModelState.IsValid)
        {
            return Json(new { success = false, message = "Vui lòng chọn sao và nhập nhận xét từ 10 ký tự." });
        }

        var imageFiles = (images ?? new List<IFormFile>()).Where(file => file.Length > 0).Take(MaxReviewImages + 1).ToList();
        if (imageFiles.Count > MaxReviewImages)
        {
            return Json(new { success = false, message = $"Bạn chỉ có thể tải tối đa {MaxReviewImages} ảnh cho mỗi đánh giá." });
        }

        var imageError = ValidateReviewImages(imageFiles);
        if (!string.IsNullOrWhiteSpace(imageError))
        {
            return Json(new { success = false, message = imageError });
        }

        var savedImages = await SaveReviewImagesAsync(imageFiles);

        var review = new Review
        {
            ProductId = productId,
            UserId = userId,
            Rating = rating,
            Comment = comment.Trim()
        };

        foreach (var image in savedImages.Select((url, index) => new ReviewImage { ImageUrl = url, SortOrder = index }))
        {
            review.Images.Add(image);
        }

        _db.Reviews.Add(review);
        await _db.SaveChangesAsync();

        var user = await _db.Users.FindAsync(userId);
        return Json(new
        {
            success = true,
            message = "Cảm ơn bạn đã đánh giá.",
            review = new
            {
                user = user?.FullName ?? User.Identity?.Name ?? "Khách hàng",
                date = review.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy"),
                rating = review.Rating,
                comment = review.Comment,
                images = review.Images.OrderBy(image => image.SortOrder).Select(image => image.ImageUrl)
            }
        });
    }

    private static string? ValidateReviewImages(IEnumerable<IFormFile> images)
    {
        foreach (var image in images)
        {
            var extension = Path.GetExtension(image.FileName);
            if (!AllowedReviewImageExtensions.Contains(extension) || !image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return "Ảnh đánh giá chỉ hỗ trợ JPG, PNG hoặc WEBP.";
            }

            if (image.Length > MaxReviewImageBytes)
            {
                return "Mỗi ảnh đánh giá cần nhỏ hơn 3MB.";
            }
        }

        return null;
    }

    private async Task<List<string>> SaveReviewImagesAsync(IEnumerable<IFormFile> images)
    {
        var urls = new List<string>();
        var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadRoot = Path.Combine(webRoot, "uploads", "reviews");
        Directory.CreateDirectory(uploadRoot);

        foreach (var image in images)
        {
            var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var path = Path.Combine(uploadRoot, fileName);

            await using var stream = System.IO.File.Create(path);
            await image.CopyToAsync(stream);
            urls.Add($"/uploads/reviews/{fileName}");
        }

        return urls;
    }
}
