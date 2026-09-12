using EcommerceApp.Models;

namespace EcommerceApp.Data;

public static class RevenueQueries
{
    public static IQueryable<Order> WithRecognizedRevenue(this IQueryable<Order> orders) => orders.Where(order =>
        order.Status != OrderStatuses.Cancelled && order.RefundStatus == RefundStatuses.NotRequired &&
        (order.IsPaid || order.Status == OrderStatuses.Delivered));

    public static IQueryable<OrderItem> WithRecognizedRevenue(this IQueryable<OrderItem> items) => items.Where(item =>
        item.Order != null && item.Order.Status != OrderStatuses.Cancelled &&
        item.Order.RefundStatus == RefundStatuses.NotRequired &&
        (item.Order.IsPaid || item.Order.Status == OrderStatuses.Delivered));
}
