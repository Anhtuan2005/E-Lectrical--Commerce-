using EcommerceApp.Data;
using EcommerceApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Services;

public class UserNotificationService : IUserNotificationService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public UserNotificationService(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task CreateAsync(string userId, string title, string message, string type, string? linkUrl = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        _db.UserNotifications.Add(new UserNotification
        {
            UserId = userId,
            Title = title.Trim(),
            Message = message.Trim(),
            Type = string.IsNullOrWhiteSpace(type) ? NotificationTypes.System : type.Trim(),
            LinkUrl = string.IsNullOrWhiteSpace(linkUrl) ? null : linkUrl.Trim()
        });

        await _db.SaveChangesAsync();
    }

    public async Task CreateForAdminsAsync(string title, string message, string type, string? linkUrl = null)
    {
        var admins = await _userManager.GetUsersInRoleAsync("Admin");
        foreach (var admin in admins)
        {
            _db.UserNotifications.Add(new UserNotification
            {
                UserId = admin.Id,
                Title = title.Trim(),
                Message = message.Trim(),
                Type = string.IsNullOrWhiteSpace(type) ? NotificationTypes.System : type.Trim(),
                LinkUrl = string.IsNullOrWhiteSpace(linkUrl) ? null : linkUrl.Trim()
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<UserNotification>> GetRecentAsync(string userId, int take = 6)
    {
        return await BaseQuery(userId)
            .Take(Math.Clamp(take, 1, 20))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<UserNotification>> GetAllAsync(string userId, int take = 80)
    {
        return await BaseQuery(userId)
            .Take(Math.Clamp(take, 10, 200))
            .ToListAsync();
    }

    public Task<UserNotification?> GetAsync(int id, string userId)
    {
        return _db.UserNotifications.FirstOrDefaultAsync(notification => notification.Id == id && notification.UserId == userId);
    }

    public Task<int> GetUnreadCountAsync(string userId)
    {
        return _db.UserNotifications.CountAsync(notification => notification.UserId == userId && !notification.IsRead);
    }

    public async Task MarkAsReadAsync(int id, string userId)
    {
        var notification = await _db.UserNotifications.FirstOrDefaultAsync(row => row.Id == id && row.UserId == userId);
        if (notification is null || notification.IsRead)
        {
            return;
        }

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        var now = DateTime.UtcNow;
        await _db.UserNotifications
            .Where(notification => notification.UserId == userId && !notification.IsRead)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(notification => notification.IsRead, true)
                .SetProperty(notification => notification.ReadAt, now));
    }

    private IQueryable<UserNotification> BaseQuery(string userId)
    {
        return _db.UserNotifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId)
            .OrderBy(notification => notification.IsRead)
            .ThenByDescending(notification => notification.CreatedAt);
    }
}
