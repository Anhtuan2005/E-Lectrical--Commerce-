using EcommerceApp.Models;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EcommerceApp.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("Admin/User")]
public class AdminUserController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AdminUserController> _logger;
    private readonly AdminUserAccessService _access;

    public AdminUserController(UserManager<ApplicationUser> userManager, ILogger<AdminUserController> logger, AdminUserAccessService access)
    {
        _userManager = userManager;
        _logger = logger;
        _access = access;
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
    public async Task<IActionResult> ToggleLock(string id, bool? locked)
    {
        if (!locked.HasValue) return BadRequest();
        Record(await _access.SetLockAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", id, locked.Value), id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("MakeAdmin/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MakeAdmin(string id)
    {
        Record(await _access.SetAdminAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", id, true), id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("RemoveAdmin/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAdmin(string id)
    {
        Record(await _access.SetAdminAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", id, false), id);
        return RedirectToAction(nameof(Index));
    }

    private void Record(UserAccessResult result, string userId)
    {
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
        if (result.Succeeded) _logger.LogInformation("Admin {Admin} updated access for user {UserId}: {Result}", User.Identity?.Name, userId, result.Message);
    }
}
