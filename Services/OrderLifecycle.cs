using EcommerceApp.Data;
using EcommerceApp.Models;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Services;

public static class OrderLifecycle
{
    public static readonly TimeSpan PaymentWindow = TimeSpan.FromMinutes(15);

    public static bool IsUnpaidVnpay(Order order) => !order.IsPaid &&
        string.Equals(order.PaymentMethod, "VNPAY", StringComparison.OrdinalIgnoreCase);

    public static DateTime PaymentDeadline(Order order) => order.PaymentExpiresAt ?? order.CreatedAt.Add(PaymentWindow);

    public static bool IsPaymentExpired(Order order, DateTime now) => IsUnpaidVnpay(order) &&
        order.Status is OrderStatuses.AwaitingPayment or OrderStatuses.Pending && PaymentDeadline(order) <= now;

    public static bool CanTransition(Order order, string status)
    {
        if (!OrderStatuses.All.Contains(status)) return false;
        if (status == order.Status) return true;
        if (IsUnpaidVnpay(order) && status != OrderStatuses.Cancelled) return false;
        return (order.Status, status) switch
        {
            (OrderStatuses.AwaitingPayment, OrderStatuses.Pending or OrderStatuses.Cancelled) => true,
            (OrderStatuses.Pending, OrderStatuses.Confirmed or OrderStatuses.Cancelled) => true,
            (OrderStatuses.Confirmed, OrderStatuses.Shipping or OrderStatuses.Cancelled) => true,
            (OrderStatuses.Shipping, OrderStatuses.Delivered) =>
                order.ShippingInfo?.Status is not (ShippingStatuses.Returned or ShippingStatuses.Cancelled),
            _ => false
        };
    }

    public static bool CanShip(Order order) => !IsUnpaidVnpay(order) &&
        order.Status is OrderStatuses.Confirmed or OrderStatuses.Shipping &&
        order.ShippingInfo?.Status is not (ShippingStatuses.Delivered or ShippingStatuses.Returned or ShippingStatuses.Cancelled);

    public static bool CanApplyShipment(Order order, string status)
    {
        if (!CanShip(order) || !ShippingStatuses.All.Contains(status) || status == ShippingStatuses.NotAssigned)
            return false;
        var previous = order.ShippingInfo?.Status;
        if (previous is ShippingStatuses.Delivered or ShippingStatuses.Returned or ShippingStatuses.Cancelled)
            return false;
        // Delayed/in-transit can alternate, but a late pickup event must not move a parcel backwards.
        return status != ShippingStatuses.WaitingPickup ||
            previous is null or "" or ShippingStatuses.NotAssigned or ShippingStatuses.WaitingPickup;
    }

    // Call only inside DatabaseTransaction, before reading or mutating an order.
    public static Task<Order?> LoadForUpdateAsync(AppDbContext db, int id) => db.Orders
        .FromSqlInterpolated($"SELECT * FROM Orders WITH (UPDLOCK, ROWLOCK) WHERE Id = {id}")
        .Include(order => order.Items)
        .Include(order => order.ShippingInfo)
        .FirstOrDefaultAsync();

    // The caller holds the order lock. Stock and cancellation commit together, exactly once.
    public static async Task CancelAsync(AppDbContext db, Order order, string reason, DateTime now)
    {
        if (order.Status == OrderStatuses.Cancelled) return;
        order.Status = OrderStatuses.Cancelled;
        order.CancelledReason = reason;
        order.UpdatedAt = now;
        if (order.IsPaid && string.Equals(order.PaymentMethod, "VNPAY", StringComparison.OrdinalIgnoreCase)
            && order.RefundStatus == RefundStatuses.NotRequired)
        {
            order.RefundStatus = RefundStatuses.PendingManual;
            order.RefundRequestedAt = now;
            order.RefundNote = reason;
        }
        foreach (var item in order.Items.OrderBy(item => item.ProductId))
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Products SET Stock = Stock + {item.Quantity} WHERE Id = {item.ProductId}");
    }
}
