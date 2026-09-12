using EcommerceApp.Data;
using EcommerceApp.Models;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Services;

public sealed class OrderPaymentExpiryService(AppDbContext db)
{
    public async Task<int> ExpirePendingAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var ids = await db.Orders.AsNoTracking()
            .Where(order => order.PaymentMethod == "VNPAY" && !order.IsPaid &&
                (order.Status == OrderStatuses.AwaitingPayment || order.Status == OrderStatuses.Pending) &&
                (order.PaymentExpiresAt ?? order.CreatedAt.AddMinutes(15)) <= now)
            .OrderBy(order => order.Id).Select(order => order.Id).Take(100).ToListAsync(cancellationToken);
        var expired = 0;
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await DatabaseTransaction.ExecuteAsync(db, async () =>
            {
                var order = await OrderLifecycle.LoadForUpdateAsync(db, id);
                if (order is null || !OrderLifecycle.IsPaymentExpired(order, now)) return false;
                await OrderLifecycle.CancelAsync(db, order, "Hết thời hạn thanh toán VNPAY (15 phút).", now);
                await db.SaveChangesAsync(cancellationToken);
                return true;
            })) expired++;
        }
        return expired;
    }
}

public sealed class OrderPaymentExpiryHostedService(
    IServiceScopeFactory scopes, ILogger<OrderPaymentExpiryHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var count = await scope.ServiceProvider.GetRequiredService<OrderPaymentExpiryService>()
                    .ExpirePendingAsync(DateTime.UtcNow, stoppingToken);
                if (count > 0) logger.LogInformation("Expired {OrderCount} unpaid VNPAY orders and released stock", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Could not expire unpaid VNPAY orders; will retry next cycle"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
