using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Services;

public class ShippingService : IShippingService
{
    private const string GhnCarrierName = "Giao Hàng Nhanh";

    private readonly AppDbContext _db;
    private readonly IOrderEmailService _orderEmailService;
    private readonly IUserNotificationService _notificationService;

    public ShippingService(AppDbContext db, IOrderEmailService orderEmailService, IUserNotificationService notificationService)
    {
        _db = db;
        _orderEmailService = orderEmailService;
        _notificationService = notificationService;
    }

    public Task<bool> AssignAsync(int orderId, string carrier, string trackingCode, DateTime? estimatedDelivery) =>
        SaveTrackingAsync(orderId, carrier, trackingCode, estimatedDelivery);

    public Task<bool> UpdateTrackingAsync(int orderId, string? carrier, string trackingCode) =>
        string.IsNullOrWhiteSpace(trackingCode) ? Task.FromResult(false) :
            SaveTrackingAsync(orderId, carrier, trackingCode, null);

    private async Task<bool> SaveTrackingAsync(int orderId, string? carrier, string? trackingCode, DateTime? estimatedDelivery)
    {
        if (carrier?.Length > 80 || trackingCode?.Length > 80) return false;
        var order = await DatabaseTransaction.ExecuteAsync<Order?>(_db, async () =>
        {
            var current = await OrderLifecycle.LoadForUpdateAsync(_db, orderId);
            var status = string.IsNullOrWhiteSpace(trackingCode) ? ShippingStatuses.WaitingPickup : ShippingStatuses.InTransit;
            if (current is null || !OrderLifecycle.CanApplyShipment(current, status)) return null;
            current.ShippingInfo ??= new ShippingInfo { OrderId = orderId };
            current.ShippingInfo.Carrier = string.IsNullOrWhiteSpace(carrier)
                ? (string.IsNullOrWhiteSpace(current.ShippingInfo.Carrier) ? "Techvora Express" : current.ShippingInfo.Carrier)
                : carrier.Trim();
            current.ShippingInfo.TrackingCode = trackingCode?.Trim() ?? "";
            if (estimatedDelivery.HasValue) current.ShippingInfo.EstimatedDelivery = estimatedDelivery;
            current.ShippingInfo.ShippedAt ??= DateTime.UtcNow;
            current.ShippingInfo.Status = status;
            current.Status = OrderStatuses.Shipping;
            current.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return current;
        });
        if (order is null) return false;
        await _orderEmailService.SendOrderStatusChangedAsync(order.Id, OrderStatuses.Shipping);
        await _notificationService.CreateAsync(order.UserId, "Cập nhật vận chuyển",
            $"Đơn #DH{order.Id:D4} đang giao. Mã vận đơn: {order.ShippingInfo!.TrackingCode}.",
            NotificationTypes.Order, $"/Order/Detail/{order.Id}");
        return true;
    }

    public async Task<bool> UpdateStatusAsync(int orderId, string status)
    {
        var order = await DatabaseTransaction.ExecuteAsync<Order?>(_db, async () =>
        {
            var current = await OrderLifecycle.LoadForUpdateAsync(_db, orderId);
            if (current?.ShippingInfo is null || !OrderLifecycle.CanApplyShipment(current, status)) return null;
            if (current.ShippingInfo.Status == status) return null;
            current.ShippingInfo.Status = status;
            if (status == ShippingStatuses.Delivered) current.Status = OrderStatuses.Delivered;
            current.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return current;
        });
        if (order is null) return false;
        if (status == ShippingStatuses.Delivered)
        {
            await _orderEmailService.SendOrderStatusChangedAsync(order.Id, OrderStatuses.Delivered);
            await _notificationService.CreateAsync(order.UserId, "Đơn hàng đã giao",
                $"Đơn #DH{order.Id:D4} đã giao thành công. Bạn có thể đánh giá sản phẩm hoặc gửi yêu cầu bảo hành nếu cần.",
                NotificationTypes.Order, $"/Order/Detail/{order.Id}");
        }
        return true;
    }

    public async Task<ShippingDashboardViewModel> GetDashboardAsync()
    {
        var actionNeeded = (await GetActionNeededOrdersAsync()).ToList();

        return new ShippingDashboardViewModel
        {
            OrdersByShippingStatus = await _db.ShippingInfos.GroupBy(info => info.Status).ToDictionaryAsync(group => group.Key, group => group.Count()),
            MissingTrackingCount = actionNeeded.Count(order => order.ShippingInfo is null || string.IsNullOrWhiteSpace(order.ShippingInfo.TrackingCode)),
            DelayedCount = actionNeeded.Count(IsDelayed),
            ActionNeededOrders = actionNeeded,
            GhnTrackedOrders = await GetGhnTrackedOrdersAsync()
        };
    }

    public async Task<IEnumerable<Order>> GetActionNeededOrdersAsync()
    {
        var now = DateTime.UtcNow;
        return await _db.Orders
            .Include(order => order.User)
            .Include(order => order.ShippingInfo)
            .Where(order =>
                (order.Status == OrderStatuses.Confirmed || order.Status == OrderStatuses.Shipping) &&
                (order.PaymentMethod != "VNPAY" || order.IsPaid) &&
                (order.ShippingInfo == null ||
                 order.ShippingInfo.TrackingCode == "" ||
                 (order.ShippingInfo.EstimatedDelivery.HasValue && order.ShippingInfo.EstimatedDelivery.Value < now && order.ShippingInfo.Status != ShippingStatuses.Delivered)))
            .OrderBy(order => order.CreatedAt)
            .ToListAsync();
    }

    private async Task<IEnumerable<Order>> GetGhnTrackedOrdersAsync()
    {
        return await _db.Orders
            .Include(order => order.User)
            .Include(order => order.ShippingInfo)
            .Where(order =>
                order.ShippingInfo != null &&
                order.ShippingInfo.Carrier == GhnCarrierName &&
                order.ShippingInfo.TrackingCode != "" &&
                order.Status != OrderStatuses.Delivered &&
                order.Status != OrderStatuses.Cancelled &&
                order.ShippingInfo.Status != ShippingStatuses.Delivered &&
                order.ShippingInfo.Status != ShippingStatuses.Cancelled)
            .OrderBy(order => order.ShippingInfo!.EstimatedDelivery ?? DateTime.MaxValue)
            .ThenByDescending(order => order.UpdatedAt)
            .Take(20)
            .ToListAsync();
    }

    private static bool IsDelayed(Order order)
    {
        return order.ShippingInfo?.EstimatedDelivery is DateTime estimated &&
               estimated < DateTime.UtcNow &&
               order.ShippingInfo.Status != ShippingStatuses.Delivered;
    }
}
