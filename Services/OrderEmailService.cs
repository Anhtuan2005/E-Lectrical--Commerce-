using EcommerceApp.Data;
using EcommerceApp.Models;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Services;

public class OrderEmailService : IOrderEmailService
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<OrderEmailService> _logger;

    public OrderEmailService(AppDbContext db, IEmailSender emailSender, ILogger<OrderEmailService> logger)
    {
        _db = db;
        _emailSender = emailSender;
        _logger = logger;
    }

    public Task SendOrderCreatedAsync(int orderId)
    {
        return SendAsync(orderId, OrderEmailTemplate.OrderCreated);
    }

    public Task SendOrderStatusChangedAsync(int orderId, string status)
    {
        return SendAsync(orderId, order => OrderEmailTemplate.StatusChanged(order, status));
    }

    public Task SendRefundRequiredAsync(int orderId)
    {
        return SendAsync(orderId, OrderEmailTemplate.RefundRequired);
    }

    public Task SendRefundCompletedAsync(int orderId)
    {
        return SendAsync(orderId, OrderEmailTemplate.RefundCompleted);
    }

    private async Task SendAsync(int orderId, Func<Order, (string Subject, string HtmlBody)> buildMessage)
    {
        try
        {
            var order = await LoadOrderAsync(orderId);
            var email = order?.User?.Email;
            if (order is null || string.IsNullOrWhiteSpace(email))
            {
                _logger.LogWarning("Skipped order email for order {OrderId} because the order or customer email was not found.", orderId);
                return;
            }

            var message = buildMessage(order);
            await _emailSender.SendAsync(email, message.Subject, message.HtmlBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send order email for order {OrderId}", orderId);
        }
    }

    private Task<Order?> LoadOrderAsync(int orderId)
    {
        return _db.Orders
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(order => order.User)
            .Include(order => order.ShippingInfo)
            .Include(order => order.Items)
            .ThenInclude(item => item.Product)
            .FirstOrDefaultAsync(order => order.Id == orderId);
    }
}
