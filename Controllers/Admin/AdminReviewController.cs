using EcommerceApp.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("Admin/Review")]
public class AdminReviewController : Controller
{
    private readonly AppDbContext _db;

    public AdminReviewController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var reviews = await _db.Reviews
            .Include(review => review.Product)
            .Include(review => review.User)
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
