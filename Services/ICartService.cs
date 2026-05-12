using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;

namespace EcommerceApp.Services;

public interface ICartService
{
    Task<CartViewModel> GetCartAsync(string? userId, string sessionId);
    Task<int> GetCountAsync(string? userId, string sessionId);
    Task AddAsync(int productId, int quantity, string? userId, string sessionId);
    Task UpdateQuantityAsync(int productId, int quantity, string? userId, string sessionId);
    Task RemoveAsync(int productId, string? userId, string sessionId);
    Task ClearAsync(string? userId, string sessionId);
    Task MergeGuestCartAsync(string userId, string sessionId);
    Task<Cart?> GetCartEntityAsync(string? userId, string sessionId);
}
