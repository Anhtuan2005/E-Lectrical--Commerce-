using EcommerceApp.Models;

namespace EcommerceApp.Services;

public interface IUserNotificationService
{
    Task CreateAsync(string userId, string title, string message, string type, string? linkUrl = null);
    Task CreateForAdminsAsync(string title, string message, string type, string? linkUrl = null);
    Task<IReadOnlyList<UserNotification>> GetRecentAsync(string userId, int take = 6);
    Task<IReadOnlyList<UserNotification>> GetAllAsync(string userId, int take = 80);
    Task<UserNotification?> GetAsync(int id, string userId);
    Task<int> GetUnreadCountAsync(string userId);
    Task MarkAsReadAsync(int id, string userId);
    Task MarkAllAsReadAsync(string userId);
}
