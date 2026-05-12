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
    private readonly AppDbContext _db;

    public ReviewController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("review")]
    public async Task<IActionResult> Submit(int productId, [Range(1, 5)] int rating, [Required, MinLength(10), MaxLength(1000)] string comment)
    {
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

        var review = new Review
        {
            ProductId = productId,
            UserId = userId,
            Rating = rating,
            Comment = comment.Trim()
        };
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
                comment = review.Comment
            }
        });
    }
}
