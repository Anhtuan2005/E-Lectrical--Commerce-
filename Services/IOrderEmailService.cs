namespace EcommerceApp.Services;

public interface IOrderEmailService
{
    Task SendOrderCreatedAsync(int orderId);
    Task SendOrderStatusChangedAsync(int orderId, string status);
    Task SendRefundRequiredAsync(int orderId);
    Task SendRefundCompletedAsync(int orderId);
}
