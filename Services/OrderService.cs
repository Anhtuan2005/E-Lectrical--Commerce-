using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Services;

public class OrderService : IOrderService
{
    private readonly AppDbContext _db;
    private readonly ICartService _cartService;
    private readonly ILogger<OrderService> _logger;

    public OrderService(AppDbContext db, ICartService cartService, ILogger<OrderService> logger)
    {
        _db = db;
        _cartService = cartService;
        _logger = logger;
    }

    public async Task<Order> CreateOrderAsync(string userId, CheckoutViewModel model, string sessionId)
    {
        var cart = await _cartService.GetCartEntityAsync(userId, sessionId);
        if (cart is null || !cart.Items.Any())
        {
            throw new InvalidOperationException("Giỏ hàng đang trống.");
        }

        foreach (var item in cart.Items)
        {
            if (item.Product is null || item.Product.Stock < item.Quantity)
            {
                throw new InvalidOperationException($"Sản phẩm '{item.Product?.Name ?? "không xác định"}' không đủ hàng. Còn {item.Product?.Stock ?? 0} sản phẩm.");
            }
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var order = new Order
        {
            UserId = userId,
            RecipientName = model.RecipientName,
            RecipientPhone = model.RecipientPhone,
            ShippingAddress = model.ShippingAddress,
            PaymentMethod = model.PaymentMethod,
            Status = OrderStatuses.Pending,
            IsPaid = false,
            PaidAt = null,
            Items = cart.Items.Select(item => new OrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.Product?.SalePrice ?? item.Product?.Price ?? 0
            }).ToList()
        };

        var subtotal = order.Items.Sum(item => item.Quantity * item.UnitPrice);
        if (!string.IsNullOrWhiteSpace(model.VoucherCode))
        {
            var code = model.VoucherCode.Trim().ToUpperInvariant();
            var voucher = await _db.Vouchers.FirstOrDefaultAsync(row => row.Code == code);
            if (voucher is not null && voucher.IsActive && voucher.StartDate <= DateTime.UtcNow && voucher.EndDate >= DateTime.UtcNow && voucher.UsedCount < voucher.UsageLimit && subtotal >= voucher.MinOrderAmount)
            {
                model.DiscountAmount = CalculateDiscount(voucher, subtotal);
                voucher.UsedCount += 1;
            }
        }
        order.TotalAmount = Math.Max(0, subtotal - model.DiscountAmount);

        _db.Orders.Add(order);

        foreach (var item in cart.Items)
        {
            var affected = await _db.Database.ExecuteSqlRawAsync(
                "UPDATE Products SET Stock = Stock - {0} WHERE Id = {1} AND Stock >= {0}",
                item.Quantity,
                item.ProductId);
            if (affected == 0)
            {
                throw new InvalidOperationException($"Sản phẩm '{item.Product?.Name ?? "không xác định"}' vừa hết hàng. Vui lòng cập nhật giỏ hàng.");
            }
        }

        await _db.SaveChangesAsync();
        await _cartService.ClearAsync(userId, sessionId);
        await transaction.CommitAsync();
        _logger.LogInformation("Order {OrderId} created for user {UserId} with payment {PaymentMethod} and total {TotalAmount}", order.Id, userId, order.PaymentMethod, order.TotalAmount);
        return order;
    }

    public async Task<IEnumerable<Order>> GetUserOrdersAsync(string userId)
    {
        return await _db.Orders
            .Include(order => order.Items).ThenInclude(item => item.Product)
            .Include(order => order.ShippingInfo)
            .Where(order => order.UserId == userId)
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync();
    }

    public async Task<UserOrdersViewModel> GetUserOrderHistoryAsync(string userId, string? status)
    {
        var activeStatus = OrderStatusFilters.Normalize(status);
        var targetStatus = OrderStatusFilters.ToOrderStatus(activeStatus);
        var query = _db.Orders
            .Include(order => order.Items).ThenInclude(item => item.Product)
            .Include(order => order.ShippingInfo)
            .Where(order => order.UserId == userId);

        var counts = await _db.Orders
            .Where(order => order.UserId == userId)
            .GroupBy(order => order.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Status, row => row.Count);
        var total = counts.Values.Sum();

        if (!string.IsNullOrWhiteSpace(targetStatus))
        {
            query = query.Where(order => order.Status == targetStatus);
        }

        return new UserOrdersViewModel
        {
            ActiveStatus = activeStatus,
            Orders = await query.OrderByDescending(order => order.CreatedAt).ToListAsync(),
            Tabs = OrderStatusFilters.Tabs.Select(tab => new OrderStatusTabViewModel
            {
                Key = tab.Key,
                Label = tab.Label,
                Count = tab.Status is null ? total : counts.GetValueOrDefault(tab.Status),
                IsActive = tab.Key == activeStatus
            }).ToList()
        };
    }

    public async Task<Order?> GetUserOrderAsync(int id, string userId)
    {
        return await _db.Orders
            .Include(order => order.User)
            .Include(order => order.Items).ThenInclude(item => item.Product)
            .Include(order => order.ShippingInfo)
            .FirstOrDefaultAsync(order => order.Id == id && order.UserId == userId);
    }

    public async Task<OrderListViewModel> GetOrdersAsync(string? status, string? customer, DateTime? fromDate, DateTime? toDate)
    {
        var query = _db.Orders
            .Include(order => order.User)
            .Include(order => order.ShippingInfo)
            .Include(order => order.Items)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(order => order.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(customer))
        {
            query = query.Where(order => order.User != null && (order.User.FullName.Contains(customer) || order.User.Email!.Contains(customer)));
        }

        if (fromDate.HasValue)
        {
            query = query.Where(order => order.CreatedAt.Date >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            query = query.Where(order => order.CreatedAt.Date <= toDate.Value.Date);
        }

        return new OrderListViewModel
        {
            Orders = await query.OrderByDescending(order => order.CreatedAt).ToListAsync(),
            Status = status,
            Customer = customer,
            FromDate = fromDate,
            ToDate = toDate
        };
    }

    public async Task<Order?> GetOrderAsync(int id)
    {
        return await _db.Orders
            .Include(order => order.User)
            .Include(order => order.Items).ThenInclude(item => item.Product)
            .Include(order => order.ShippingInfo)
            .FirstOrDefaultAsync(order => order.Id == id);
    }

    public async Task UpdateStatusAsync(int id, string status)
    {
        if (!OrderStatuses.All.Contains(status))
        {
            return;
        }

        var order = await _db.Orders.Include(row => row.ShippingInfo).FirstOrDefaultAsync(row => row.Id == id);
        if (order is null)
        {
            return;
        }

        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;

        if (status == OrderStatuses.Shipping && order.ShippingInfo is not null && string.IsNullOrWhiteSpace(order.ShippingInfo.Status))
        {
            order.ShippingInfo.Status = ShippingStatuses.InTransit;
        }
        else if (status == OrderStatuses.Delivered && order.ShippingInfo is not null)
        {
            order.ShippingInfo.Status = ShippingStatuses.Delivered;
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Admin updated order {OrderId} status to {Status}", id, status);
    }

    public async Task<bool> CancelUserOrderAsync(int id, string userId, string? reason)
    {
        var order = await _db.Orders
            .Include(row => row.Items)
            .FirstOrDefaultAsync(row => row.Id == id && row.UserId == userId);
        if (order is null || order.Status != OrderStatuses.Pending)
        {
            return false;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();
        order.Status = OrderStatuses.Cancelled;
        order.CancelledReason = string.IsNullOrWhiteSpace(reason)
            ? "Khách hàng hủy đơn trước khi xác nhận."
            : reason.Trim();
        order.UpdatedAt = DateTime.UtcNow;

        foreach (var item in order.Items)
        {
            await _db.Database.ExecuteSqlRawAsync(
                "UPDATE Products SET Stock = Stock + {0} WHERE Id = {1}",
                item.Quantity,
                item.ProductId);
        }

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        _logger.LogInformation("User {UserId} cancelled order {OrderId}", userId, id);
        return true;
    }

    public async Task<int> ReorderAsync(int id, string userId, string sessionId)
    {
        var order = await _db.Orders
            .Include(row => row.Items)
            .ThenInclude(item => item.Product)
            .FirstOrDefaultAsync(row => row.Id == id && row.UserId == userId);
        if (order is null)
        {
            return 0;
        }

        var added = 0;
        foreach (var item in order.Items)
        {
            if (item.Product is null || item.Product.Stock <= 0)
            {
                continue;
            }

            var quantity = Math.Min(item.Quantity, item.Product.Stock);
            await _cartService.AddAsync(item.ProductId, quantity, userId, sessionId);
            added++;
        }

        _logger.LogInformation("User {UserId} reordered {AddedCount} items from order {OrderId}", userId, added, id);
        return added;
    }

    public async Task<AdminDashboardViewModel> GetDashboardAsync()
    {
        var now = DateTime.UtcNow;
        var startOfDay = now.Date;
        var startOfWeek = startOfDay.AddDays(-(int)startOfDay.DayOfWeek);
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        var paidOrders = _db.Orders.Where(order => order.IsPaid || order.Status == OrderStatuses.Delivered);

        var topProducts = await _db.OrderItems
            .Include(item => item.Product)
            .GroupBy(item => item.Product!.Name)
            .Select(group => new TopProductViewModel
            {
                ProductName = group.Key,
                QuantitySold = group.Sum(item => item.Quantity),
                Revenue = group.Sum(item => item.Quantity * item.UnitPrice)
            })
            .OrderByDescending(item => item.QuantitySold)
            .Take(5)
            .ToListAsync();

        var revenuePoints = await paidOrders
            .Where(order => order.CreatedAt >= startOfDay.AddDays(-6))
            .GroupBy(order => order.CreatedAt.Date)
            .Select(group => new RevenuePointViewModel
            {
                Label = group.Key.ToString("dd/MM"),
                Revenue = group.Sum(order => order.TotalAmount)
            })
            .ToListAsync();

        return new AdminDashboardViewModel
        {
            TodayRevenue = await paidOrders.Where(order => order.CreatedAt >= startOfDay).SumAsync(order => order.TotalAmount),
            WeekRevenue = await paidOrders.Where(order => order.CreatedAt >= startOfWeek).SumAsync(order => order.TotalAmount),
            MonthRevenue = await paidOrders.Where(order => order.CreatedAt >= startOfMonth).SumAsync(order => order.TotalAmount),
            TotalOrders = await _db.Orders.CountAsync(),
            OrdersByStatus = await _db.Orders.GroupBy(order => order.Status).ToDictionaryAsync(group => group.Key, group => group.Count()),
            TopProducts = topProducts,
            RevenuePoints = revenuePoints.OrderBy(point => point.Label).ToList(),
            LowStockProducts = await _db.Products
                .Include(product => product.Category)
                .Where(product => product.Stock <= 5)
                .OrderBy(product => product.Stock)
                .ThenBy(product => product.Name)
                .Take(10)
                .ToListAsync()
        };
    }

    private static decimal CalculateDiscount(Voucher voucher, decimal subtotal)
    {
        if (voucher.Type == VoucherType.FixedAmount)
        {
            return Math.Min(voucher.Value, subtotal);
        }

        var discount = subtotal * voucher.Value / 100m;
        return voucher.MaxDiscount > 0 ? Math.Min(discount, voucher.MaxDiscount) : discount;
    }
}
