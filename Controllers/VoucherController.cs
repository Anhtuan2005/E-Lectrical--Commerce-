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
    private readonly ICustomerSegmentService _customerSegmentService;

    public VoucherController(AppDbContext db, ICartService cartService, ICustomerSegmentService customerSegmentService)
    {
        _db = db;
        _cartService = cartService;
        _customerSegmentService = customerSegmentService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("voucher")]
    public Task<IActionResult> Apply(string code)
    {
        return Validate(code);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("voucher")]
    public async Task<IActionResult> Validate(string code, decimal? subtotalOverride = null)
    {
        const decimal maxDatabaseAmount = 9_999_999_999_999_999.99m;
        if (!ModelState.IsValid || subtotalOverride is < 0 or > maxDatabaseAmount)
        {
            return BadRequest(new { valid = false, message = "Số tiền tạm tính không hợp lệ." });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var cart = await _cartService.GetCartAsync(userId, HttpContext.Session.Id);
        var subtotal = subtotalOverride ?? cart.Total;
        if (subtotal is < 0 or > maxDatabaseAmount)
        {
            return BadRequest(new { valid = false, message = "Số tiền tạm tính không hợp lệ." });
        }
        var normalizedCode = (code ?? string.Empty).Trim().ToUpperInvariant();
        var voucher = await _db.Vouchers.AsNoTracking().FirstOrDefaultAsync(row => row.Code == normalizedCode);

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

        if (!string.IsNullOrWhiteSpace(userId) && await _db.VoucherUsages.AnyAsync(usage => usage.VoucherId == voucher.Id && usage.UserId == userId))
        {
            return Json(new { valid = false, discountAmount = 0, discountLabel = "", newTotal = subtotal, message = "Bạn đã sử dụng mã giảm giá này." });
        }

        if (!string.IsNullOrWhiteSpace(voucher.TargetUserId) && voucher.TargetUserId != userId)
        {
            return Json(new { valid = false, discountAmount = 0, discountLabel = "", newTotal = subtotal, message = "Mã giảm giá này chỉ áp dụng cho tài khoản được tặng." });
        }

        if (voucher.CustomerSegmentId.HasValue &&
            (string.IsNullOrWhiteSpace(userId) || !await _customerSegmentService.UserBelongsToSegmentAsync(userId, voucher.CustomerSegmentId.Value)))
        {
            return Json(new { valid = false, discountAmount = 0, discountLabel = "", newTotal = subtotal, message = "Mã giảm giá này chỉ áp dụng cho nhóm khách hàng phù hợp." });
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
