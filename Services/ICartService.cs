using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;

namespace EcommerceApp.Services;

public interface ICartService
{
    Task<CartViewModel> GetCartAsync(string? userId, string sessionId, IEnumerable<int>? productIds = null);
    Task<int> GetCountAsync(string? userId, string sessionId);
    Task AddAsync(int productId, int quantity, string? userId, string sessionId, CartItemGroupInput? group = null);
    Task UpdateQuantityAsync(int productId, int quantity, string? userId, string sessionId);
    Task RemoveAsync(int productId, string? userId, string sessionId);
    Task ClearAsync(string? userId, string sessionId);
    Task MergeGuestCartAsync(string userId, string sessionId);
    Task<Cart?> GetCartEntityAsync(string? userId, string sessionId);
}

public sealed class CartItemGroupInput
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string? ItemLabel { get; init; }
    public int? SortOrder { get; init; }
}
