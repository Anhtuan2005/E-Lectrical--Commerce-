using EcommerceApp.Models.ViewModels;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EcommerceApp.Controllers;

[Authorize]
public class NotificationController : Controller
{
    private readonly IUserNotificationService _notificationService;

    public NotificationController(IUserNotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return View(new NotificationIndexViewModel
        {
            Notifications = await _notificationService.GetAllAsync(userId),
            UnreadCount = await _notificationService.GetUnreadCountAsync(userId)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Go(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var notification = await _notificationService.GetAsync(id, userId);
        if (notification is null)
        {
            return RedirectToAction(nameof(Index));
        }

        await _notificationService.MarkAsReadAsync(id, userId);
        if (!string.IsNullOrWhiteSpace(notification.LinkUrl) && Url.IsLocalUrl(notification.LinkUrl))
        {
            return LocalRedirect(notification.LinkUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _notificationService.MarkAllAsReadAsync(userId);
        return RedirectToAction(nameof(Index));
    }
}
