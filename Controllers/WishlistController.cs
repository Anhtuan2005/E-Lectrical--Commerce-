using EcommerceApp.Data;
using EcommerceApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EcommerceApp.Controllers;

[Authorize]
public class WishlistController : Controller
{
    private readonly AppDbContext _db;

    public WishlistController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var items = await _db.WishlistItems
            .Include(item => item.Product)
            .ThenInclude(product => product!.Category)
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.AddedAt)
            .ToListAsync();

        return View(items);
    }

    [HttpPost("Wishlist/Toggle/{productId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int productId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var item = await _db.WishlistItems.FirstOrDefaultAsync(row => row.UserId == userId && row.ProductId == productId);
        var isWishlisted = item is null;

        if (item is null)
        {
            _db.WishlistItems.Add(new WishlistItem { UserId = userId, ProductId = productId });
        }
        else
        {
            _db.WishlistItems.Remove(item);
        }

        await _db.SaveChangesAsync();
        var count = await _db.WishlistItems.CountAsync(row => row.UserId == userId);

        return Json(new
        {
            isWishlisted,
            count,
            message = isWishlisted ? "Đã thêm vào yêu thích." : "Đã bỏ khỏi yêu thích."
        });
    }
}
