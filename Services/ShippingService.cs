using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Services;

public class ShippingService : IShippingService
{
    private readonly AppDbContext _db;
    private readonly IOrderEmailService _orderEmailService;
    private readonly IUserNotificationService _notificationService;

    public ShippingService(AppDbContext db, IOrderEmailService orderEmailService, IUserNotificationService notificationService)
    {
        _db = db;
        _orderEmailService = orderEmailService;
        _notificationService = notificationService;
    }

    public async Task AssignAsync(int orderId, string carrier, string trackingCode, DateTime? estimatedDelivery)
    {
        var order = await _db.Orders.Include(row => row.ShippingInfo).FirstOrDefaultAsync(row => row.Id == orderId);
        if (order is null)
        {
            return;
        }

        order.ShippingInfo ??= new ShippingInfo { OrderId = orderId };
        order.ShippingInfo.Carrier = carrier;
        order.ShippingInfo.TrackingCode = trackingCode;
        order.ShippingInfo.EstimatedDelivery = estimatedDelivery;
        order.ShippingInfo.ShippedAt ??= DateTime.UtcNow;
        order.ShippingInfo.Status = string.IsNullOrWhiteSpace(trackingCode) ? ShippingStatuses.WaitingPickup : ShippingStatuses.InTransit;
        order.Status = OrderStatuses.Shipping;
        order.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _orderEmailService.SendOrderStatusChangedAsync(order.Id, OrderStatuses.Shipping);
        await _notificationService.CreateAsync(
            order.UserId,
            "Đơn hàng đang giao",
            string.IsNullOrWhiteSpace(trackingCode)
                ? $"Đơn #DH{order.Id:D4} đã được bàn giao cho vận chuyển."
                : $"Đơn #DH{order.Id:D4} đang giao với mã vận đơn {trackingCode}.",
            NotificationTypes.Order,
            $"/Order/Detail/{order.Id}");
    }

    public async Task<bool> UpdateTrackingAsync(int orderId, string? carrier, string trackingCode)
    {
        if (string.IsNullOrWhiteSpace(trackingCode))
        {
            return false;
        }

        var order = await _db.Orders.Include(row => row.ShippingInfo).FirstOrDefaultAsync(row => row.Id == orderId);
        if (order is null)
        {
            return false;
        }

        order.ShippingInfo ??= new ShippingInfo { OrderId = orderId };
        order.ShippingInfo.Carrier = string.IsNullOrWhiteSpace(carrier)
            ? string.IsNullOrWhiteSpace(order.ShippingInfo.Carrier) ? "Techvora Express" : order.ShippingInfo.Carrier
            : carrier.Trim();
        order.ShippingInfo.TrackingCode = trackingCode.Trim();
        order.ShippingInfo.ShippedAt ??= DateTime.UtcNow;
        order.ShippingInfo.Status = ShippingStatuses.InTransit;
        order.Status = OrderStatuses.Shipping;
        order.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _orderEmailService.SendOrderStatusChangedAsync(order.Id, OrderStatuses.Shipping);
        await _notificationService.CreateAsync(
            order.UserId,
            "Đã cập nhật mã vận đơn",
            $"Đơn #DH{order.Id:D4} đang giao với mã vận đơn {order.ShippingInfo.TrackingCode}.",
            NotificationTypes.Order,
            $"/Order/Detail/{order.Id}");
        return true;
    }

    public async Task UpdateStatusAsync(int orderId, string status)
    {
        if (!ShippingStatuses.All.Contains(status))
        {
            return;
        }

        var info = await _db.ShippingInfos.Include(row => row.Order).FirstOrDefaultAsync(row => row.OrderId == orderId);
        if (info is null)
        {
            return;
        }

        info.Status = status;
        if (status == ShippingStatuses.Delivered && info.Order is not null)
        {
            info.Order.Status = OrderStatuses.Delivered;
            info.Order.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        if (status == ShippingStatuses.Delivered && info.Order is not null)
        {
            await _orderEmailService.SendOrderStatusChangedAsync(info.Order.Id, OrderStatuses.Delivered);
            await _notificationService.CreateAsync(
                info.Order.UserId,
                "Đơn hàng đã giao",
                $"Đơn #DH{info.Order.Id:D4} đã giao thành công. Bạn có thể đánh giá sản phẩm hoặc gửi yêu cầu bảo hành nếu cần.",
                NotificationTypes.Order,
                $"/Order/Detail/{info.Order.Id}");
        }
    }

    public async Task<ShippingDashboardViewModel> GetDashboardAsync()
    {
        var actionNeeded = await GetActionNeededOrdersAsync();

        return new ShippingDashboardViewModel
        {
            OrdersByShippingStatus = await _db.ShippingInfos.GroupBy(info => info.Status).ToDictionaryAsync(group => group.Key, group => group.Count()),
            MissingTrackingCount = actionNeeded.Count(order => order.ShippingInfo is null || string.IsNullOrWhiteSpace(order.ShippingInfo.TrackingCode)),
            DelayedCount = actionNeeded.Count(IsDelayed),
            ActionNeededOrders = actionNeeded
        };
    }

    public async Task<IEnumerable<Order>> GetActionNeededOrdersAsync()
    {
        var now = DateTime.UtcNow;
        return await _db.Orders
            .Include(order => order.User)
            .Include(order => order.ShippingInfo)
            .Where(order =>
                order.Status != OrderStatuses.Delivered &&
                order.Status != OrderStatuses.Cancelled &&
                (order.ShippingInfo == null ||
                 order.ShippingInfo.TrackingCode == "" ||
                 (order.ShippingInfo.EstimatedDelivery.HasValue && order.ShippingInfo.EstimatedDelivery.Value < now && order.ShippingInfo.Status != ShippingStatuses.Delivered)))
            .OrderBy(order => order.CreatedAt)
            .ToListAsync();
    }

    private static bool IsDelayed(Order order)
    {
        return order.ShippingInfo?.EstimatedDelivery is DateTime estimated &&
               estimated < DateTime.UtcNow &&
               order.ShippingInfo.Status != ShippingStatuses.Delivered;
    }
}
