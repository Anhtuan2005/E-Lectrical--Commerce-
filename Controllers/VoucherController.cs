using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EcommerceApp.Controllers;

[Authorize]
public class VoucherController : Controller
{
    private readonly AppDbContext _db;
    private readonly ICartService _cartService;

    public VoucherController(AppDbContext db, ICartService cartService)
    {
        _db = db;
        _cartService = cartService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Apply(string code)
    {
        return Validate(code);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("voucher")]
    public async Task<IActionResult> Validate(string code)
    {
        var cart = await _cartService.GetCartAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.Session.Id);
        var subtotal = cart.Total;
        var voucher = await _db.Vouchers.FirstOrDefaultAsync(row => row.Code == (code ?? string.Empty).Trim().ToUpper());

        if (voucher is null || !voucher.IsActive)
        {
            return Json(new { valid = false, discountAmount = 0, discountLabel = "", newTotal = subtotal, message = "Mã giảm giá không hợp lệ." });
        }

        if (voucher.StartDate > DateTime.UtcNow || voucher.EndDate < DateTime.UtcNow)
        {
            return Json(new { valid = false, discountAmount = 0, discountLabel = "", newTotal = subtotal, message = "Mã giảm giá đã hết hạn hoặc chưa bắt đầu." });
        }

        if (voucher.UsedCount >= voucher.UsageLimit)
        {
            return Json(new { valid = false, discountAmount = 0, discountLabel = "", newTotal = subtotal, message = "Mã giảm giá đã hết lượt sử dụng." });
        }

        if (subtotal < voucher.MinOrderAmount)
        {
            return Json(new { valid = false, discountAmount = 0, discountLabel = "", newTotal = subtotal, message = $"Đơn hàng cần tối thiểu {voucher.MinOrderAmount:N0} ₫." });
        }

        var discount = voucher.Type == VoucherType.FixedAmount
            ? Math.Min(voucher.Value, subtotal)
            : Math.Min(subtotal * voucher.Value / 100m, voucher.MaxDiscount > 0 ? voucher.MaxDiscount : subtotal);
        var newTotal = Math.Max(0, subtotal - discount);

        return Json(new
        {
            valid = true,
            discountAmount = discount,
            discountLabel = "Giảm " + discount.ToString("N0") + " ₫",
            formattedDiscount = discount.ToString("N0") + " ₫",
            newTotal,
            total = newTotal.ToString("N0") + " ₫",
            code = voucher.Code,
            message = "Đã áp dụng mã giảm giá."
        });
    }
}
