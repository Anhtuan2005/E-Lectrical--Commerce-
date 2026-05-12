using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;

namespace EcommerceApp.Services;

public interface IOrderService
{
    Task<Order> CreateOrderAsync(string userId, CheckoutViewModel model, string sessionId);
    Task<IEnumerable<Order>> GetUserOrdersAsync(string userId);
    Task<OrderListViewModel> GetOrdersAsync(string? status, string? customer, DateTime? fromDate, DateTime? toDate);
    Task<Order?> GetOrderAsync(int id);
    Task UpdateStatusAsync(int id, string status);
    Task<AdminDashboardViewModel> GetDashboardAsync();
}
