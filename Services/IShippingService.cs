using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;

namespace EcommerceApp.Services;

public interface IShippingService
{
    Task<bool> AssignAsync(int orderId, string carrier, string trackingCode, DateTime? estimatedDelivery);
    Task<bool> UpdateTrackingAsync(int orderId, string? carrier, string trackingCode);
    Task<bool> UpdateStatusAsync(int orderId, string status);
    Task<ShippingDashboardViewModel> GetDashboardAsync();
    Task<IEnumerable<Order>> GetActionNeededOrdersAsync();
}
