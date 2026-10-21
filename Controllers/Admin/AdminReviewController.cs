using EcommerceApp.Data;
using EcommerceApp.Services;
using EcommerceApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("Admin/Review")]
public class AdminReviewController : Controller
{
    private readonly AppDbContext _db;
    private readonly IUserNotificationService _notifications;

    public AdminReviewController(AppDbContext db, IUserNotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    [HttpPost("Reply/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(int id, string? reply)
    {
        var review = await _db.Reviews.FindAsync(id);
        if (review is null) return NotFound();
        if (!review.IsApproved)
        {
            TempData["Error"] = "Chỉ phản hồi đánh giá đang hiển thị.";
            return RedirectToAction(nameof(Index));
        }
        var content = reply?.Trim();
        if (string.IsNullOrWhiteSpace(content) || content.Length > 500)
        {
            TempData["Error"] = "Phản hồi cần có nội dung và không vượt quá 500 ký tự.";
            return RedirectToAction(nameof(Index));
        }
        var firstReply = string.IsNullOrWhiteSpace(review.StoreReply);
        review.StoreReply = content;
        review.StoreRepliedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        if (firstReply)
            await _notifications.CreateAsync(review.UserId, "Cửa hàng đã phản hồi đánh giá",
                "Techvora đã phản hồi đánh giá sản phẩm của bạn.", NotificationTypes.Review,
                $"/Product/Detail/{review.ProductId}");
        TempData["Success"] = "Đã đăng phản hồi công khai.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var reviews = await _db.Reviews
            .Include(review => review.Product)
            .Include(review => review.User)
            .Include(review => review.Images.OrderBy(image => image.SortOrder))
            .OrderByDescending(review => review.CreatedAt)
            .ToListAsync();
        return View("~/Views/Admin/Review/Index.cshtml", reviews);
    }

    [HttpPost("Approve/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var review = await _db.Reviews.FindAsync(id);
        if (review is not null)
        {
            review.IsApproved = true;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Reject/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id)
    {
        var review = await _db.Reviews.FindAsync(id);
        if (review is not null)
        {
            review.IsApproved = false;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var review = await _db.Reviews.FindAsync(id);
        if (review is not null)
        {
            _db.Reviews.Remove(review);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
