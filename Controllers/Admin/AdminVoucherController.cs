using EcommerceApp.Data;
using EcommerceApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("Admin/Voucher")]
public class AdminVoucherController : Controller
{
    private readonly AppDbContext _db;

    public AdminVoucherController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? filter)
    {
        var now = DateTime.UtcNow;
        var query = _db.Vouchers
            .Include(voucher => voucher.CustomerSegment)
            .Include(voucher => voucher.TargetUser)
            .AsQueryable();
        query = filter switch
        {
            "active" => query.Where(voucher => voucher.IsActive && voucher.StartDate <= now && voucher.EndDate >= now && voucher.UsedCount < voucher.UsageLimit),
            "expired" => query.Where(voucher => voucher.EndDate < now),
            "used" => query.Where(voucher => voucher.UsedCount >= voucher.UsageLimit),
            _ => query
        };
        ViewBag.Filter = filter;
        return View("~/Views/Admin/Voucher/Index.cshtml", await query.OrderByDescending(voucher => voucher.Id).ToListAsync());
    }

    [HttpGet("Create")]
    public async Task<IActionResult> Create()
    {
        await LoadSegmentOptionsAsync();
        return View("~/Views/Admin/Voucher/Form.cshtml", new Voucher { StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddMonths(1), UsageLimit = 100 });
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Code,Type,Value,MinOrderAmount,MaxDiscount,UsageLimit,StartDate,EndDate,IsActive,CustomerSegmentId")] Voucher model)
    {
        model.Code = (model.Code ?? string.Empty).Trim().ToUpperInvariant();
        await ValidateReferencesAndCodeAsync(model);
        if (!ModelState.IsValid)
        {
            await LoadSegmentOptionsAsync();
            return View("~/Views/Admin/Voucher/Form.cshtml", model);
        }

        _db.Vouchers.Add(model);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(Voucher.Code), "Mã voucher đã tồn tại hoặc dữ liệu không thể lưu.");
            await LoadSegmentOptionsAsync();
            return View("~/Views/Admin/Voucher/Form.cshtml", model);
        }
        TempData["Success"] = "Đã tạo voucher.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var voucher = await _db.Vouchers.FindAsync(id);
        if (voucher is null)
        {
            return NotFound();
        }

        await LoadSegmentOptionsAsync();
        return View("~/Views/Admin/Voucher/Form.cshtml", voucher);
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Code,Type,Value,MinOrderAmount,MaxDiscount,UsageLimit,StartDate,EndDate,IsActive,CustomerSegmentId")] Voucher model)
    {
        var voucher = await _db.Vouchers.FindAsync(id);
        if (voucher is null)
        {
            return NotFound();
        }

        model.Id = id;
        model.Code = (model.Code ?? string.Empty).Trim().ToUpperInvariant();
        await ValidateReferencesAndCodeAsync(model, id);
        if (model.UsageLimit < voucher.UsedCount)
        {
            ModelState.AddModelError(nameof(Voucher.UsageLimit), $"Giới hạn không thể thấp hơn {voucher.UsedCount} lượt đã dùng.");
        }

        if (!ModelState.IsValid)
        {
            await LoadSegmentOptionsAsync();
            return View("~/Views/Admin/Voucher/Form.cshtml", model);
        }

        voucher.Code = model.Code;
        voucher.Type = model.Type;
        voucher.Value = model.Value;
        voucher.MinOrderAmount = model.MinOrderAmount;
        voucher.MaxDiscount = model.MaxDiscount;
        voucher.UsageLimit = model.UsageLimit;
        voucher.StartDate = model.StartDate;
        voucher.EndDate = model.EndDate;
        voucher.IsActive = model.IsActive;
        voucher.CustomerSegmentId = model.CustomerSegmentId;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật voucher.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var voucher = await _db.Vouchers.FindAsync(id);
        if (voucher is not null)
        {
            if (await _db.VoucherUsages.AnyAsync(usage => usage.VoucherId == id))
            {
                TempData["Error"] = "Không thể xóa voucher đã được sử dụng. Hãy tắt voucher để giữ lịch sử đơn hàng.";
                return RedirectToAction(nameof(Index));
            }
            _db.Vouchers.Remove(voucher);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // A checkout may have used the voucher after the check above.
                _db.Entry(voucher).State = EntityState.Unchanged;
                TempData["Error"] = "Không thể xóa voucher lúc này. Hãy tải lại danh sách và thử tắt voucher.";
                return RedirectToAction(nameof(Index));
            }
            TempData["Success"] = "Đã xoá voucher.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Toggle/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var voucher = await _db.Vouchers.FindAsync(id);
        if (voucher is not null)
        {
            voucher.IsActive = !voucher.IsActive;
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task LoadSegmentOptionsAsync()
    {
        ViewBag.CustomerSegments = await _db.CustomerSegments
            .AsNoTracking()
            .OrderBy(segment => segment.Name)
            .ToListAsync();
    }

    private async Task ValidateReferencesAndCodeAsync(Voucher model, int? currentId = null)
    {
        if (!string.IsNullOrWhiteSpace(model.Code)
            && await _db.Vouchers.AnyAsync(voucher => voucher.Code == model.Code && voucher.Id != currentId))
        {
            ModelState.AddModelError(nameof(Voucher.Code), "Mã voucher đã tồn tại.");
        }

        if (model.CustomerSegmentId.HasValue
            && !await _db.CustomerSegments.AnyAsync(segment => segment.Id == model.CustomerSegmentId.Value))
        {
            ModelState.AddModelError(nameof(Voucher.CustomerSegmentId), "Nhóm khách hàng không tồn tại.");
        }
    }
}
