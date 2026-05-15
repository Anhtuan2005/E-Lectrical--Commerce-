using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;

namespace EcommerceApp.Services;

public interface IOrderService
{
    Task<Order> CreateOrderAsync(string userId, CheckoutViewModel model, string sessionId);
    Task<IEnumerable<Order>> GetUserOrdersAsync(string userId);
    Task<UserOrdersViewModel> GetUserOrderHistoryAsync(string userId, string? status, int page, int pageSize);
    Task<Order?> GetUserOrderAsync(int id, string userId);
    Task<OrderListViewModel> GetOrdersAsync(string? status, string? customer, DateTime? fromDate, DateTime? toDate);
    Task<Order?> GetOrderAsync(int id);
    Task UpdateStatusAsync(int id, string status);
    Task<bool> CancelUserOrderAsync(int id, string userId, string? reason);
    Task<int> ReorderAsync(int id, string userId, string sessionId);
    Task<AdminDashboardViewModel> GetDashboardAsync();
}
