using EcommerceApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("Admin/User")]
public class AdminUserController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AdminUserController> _logger;

    public AdminUserController(UserManager<ApplicationUser> userManager, ILogger<AdminUserController> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var users = await _userManager.Users.OrderBy(user => user.FullName).ToListAsync();
        var roles = new Dictionary<string, IList<string>>();
        foreach (var user in users)
        {
            roles[user.Id] = await _userManager.GetRolesAsync(user);
        }

        ViewBag.Roles = roles;
        return View("~/Views/Admin/User/Index.cshtml", users);
    }

    [HttpPost("ToggleLock/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLock(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is not null)
        {
            var isLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;
            await _userManager.SetLockoutEndDateAsync(user, isLocked ? null : DateTimeOffset.UtcNow.AddYears(10));
            TempData["Success"] = isLocked ? "Đã mở khoá tài khoản." : "Đã khoá tài khoản.";
            _logger.LogInformation("Admin {Admin} {Action} user {UserId}", User.Identity?.Name, isLocked ? "unlocked" : "locked", user.Id);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("MakeAdmin/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MakeAdmin(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is not null && !await _userManager.IsInRoleAsync(user, "Admin"))
        {
            await _userManager.AddToRoleAsync(user, "Admin");
            TempData["Success"] = "Đã cấp quyền Admin.";
            _logger.LogInformation("Admin {Admin} granted Admin role to user {UserId}", User.Identity?.Name, user.Id);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("RemoveAdmin/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAdmin(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is not null && await _userManager.IsInRoleAsync(user, "Admin"))
        {
            await _userManager.RemoveFromRoleAsync(user, "Admin");
            TempData["Success"] = "Đã gỡ quyền Admin.";
            _logger.LogInformation("Admin {Admin} removed Admin role from user {UserId}", User.Identity?.Name, user.Id);
        }

        return RedirectToAction(nameof(Index));
    }
}
