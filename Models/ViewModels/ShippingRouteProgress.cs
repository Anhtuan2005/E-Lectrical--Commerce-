namespace EcommerceApp.Models.ViewModels;

public sealed record ShippingRouteProgress(
    int Percent,
    int Step,
    string Stage,
    bool IsOverdue,
    string? OverdueLabel);

public static class ShippingRouteProgressCalculator
{
    public static ShippingRouteProgress Calculate(Order order, DateTime utcNow)
    {
        var shippingStatus = order.ShippingInfo?.Status ?? string.Empty;
        if (order.Status == OrderStatuses.Cancelled || shippingStatus == ShippingStatuses.Cancelled)
        {
            return new ShippingRouteProgress(0, 0, "Đã huỷ", false, null);
        }

        if (order.Status == OrderStatuses.Delivered || shippingStatus == ShippingStatuses.Delivered)
        {
            return new ShippingRouteProgress(100, 4, "Đã giao thành công", false, null);
        }

        var start = AsUtc(order.ShippingInfo?.ShippedAt ?? order.CreatedAt);
        var end = order.ShippingInfo?.EstimatedDelivery is DateTime estimatedDelivery
            ? AsUtc(estimatedDelivery)
            : (DateTime?)null;
        var now = AsUtc(utcNow);

        if (!end.HasValue || end.Value <= start)
        {
            return FromShippingStatus(shippingStatus, order.Status);
        }

        var percent = CalculateQuarterProgress(start, end.Value, now);
        var step = percent / 25;
        var overdue = now > end.Value;
        return new ShippingRouteProgress(
            percent,
            step,
            StageFor(percent),
            overdue,
            overdue ? FormatOverdue(now - end.Value) : null);
    }

    internal static int CalculateQuarterProgress(DateTime start, DateTime end, DateTime now)
    {
        if (now <= start)
        {
            return 0;
        }

        if (now >= end)
        {
            return 100;
        }

        var ratio = (now - start).TotalMilliseconds / (end - start).TotalMilliseconds;
        return Math.Clamp((int)Math.Floor(ratio * 4), 0, 3) * 25;
    }

    private static ShippingRouteProgress FromShippingStatus(string shippingStatus, string orderStatus)
    {
        return shippingStatus switch
        {
            ShippingStatuses.WaitingPickup => new ShippingRouteProgress(0, 0, "Đang chờ lấy hàng", false, null),
            ShippingStatuses.InTransit => new ShippingRouteProgress(50, 2, "Đang trung chuyển", false, null),
            ShippingStatuses.Delayed => new ShippingRouteProgress(75, 3, "Đang giao chậm", false, null),
            ShippingStatuses.Returned => new ShippingRouteProgress(50, 2, "Đang hoàn hàng", false, null),
            _ when orderStatus == OrderStatuses.Shipping => new ShippingRouteProgress(50, 2, "Đang trung chuyển", false, null),
            _ => new ShippingRouteProgress(0, 0, "Đang chuẩn bị lộ trình", false, null)
        };
    }

    private static string StageFor(int percent)
    {
        return percent switch
        {
            25 => "Đã đi 1/4 chặng",
            50 => "Đã đi 2/4 chặng",
            75 => "Đã đi 3/4 chặng",
            100 => "Đã đến mốc dự kiến",
            _ => "Đang chuẩn bị lộ trình"
        };
    }

    private static string FormatOverdue(TimeSpan overdue)
    {
        if (overdue.TotalHours < 24)
        {
            return "Quá hạn dưới 1 ngày";
        }

        return $"Quá hạn {(int)Math.Floor(overdue.TotalDays)} ngày";
    }

    private static DateTime AsUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
