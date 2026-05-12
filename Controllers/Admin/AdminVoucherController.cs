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
        var query = _db.Vouchers.AsQueryable();
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
    public IActionResult Create()
    {
        return View("~/Views/Admin/Voucher/Form.cshtml", new Voucher { StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddMonths(1), UsageLimit = 100 });
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Voucher model)
    {
        model.Code = model.Code.Trim().ToUpperInvariant();
        if (!ModelState.IsValid)
        {
            return View("~/Views/Admin/Voucher/Form.cshtml", model);
        }

        _db.Vouchers.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã tạo voucher.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var voucher = await _db.Vouchers.FindAsync(id);
        return voucher is null ? NotFound() : View("~/Views/Admin/Voucher/Form.cshtml", voucher);
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Voucher model)
    {
        var voucher = await _db.Vouchers.FindAsync(id);
        if (voucher is null)
        {
            return NotFound();
        }

        voucher.Code = model.Code.Trim().ToUpperInvariant();
        voucher.Type = model.Type;
        voucher.Value = model.Value;
        voucher.MinOrderAmount = model.MinOrderAmount;
        voucher.MaxDiscount = model.MaxDiscount;
        voucher.UsageLimit = model.UsageLimit;
        voucher.StartDate = model.StartDate;
        voucher.EndDate = model.EndDate;
        voucher.IsActive = model.IsActive;
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
            _db.Vouchers.Remove(voucher);
            await _db.SaveChangesAsync();
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
}
