using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;

namespace EcommerceApp.Services;

public interface IShippingService
{
    Task AssignAsync(int orderId, string carrier, string trackingCode, DateTime? estimatedDelivery);
    Task UpdateStatusAsync(int orderId, string status);
    Task<ShippingDashboardViewModel> GetDashboardAsync();
    Task<IEnumerable<Order>> GetActionNeededOrdersAsync();
}
