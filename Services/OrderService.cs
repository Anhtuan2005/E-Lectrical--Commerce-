using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using Microsoft.Data.SqlClient;
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
        var normalizedVoucherCode = NormalizeVoucherCode(model.VoucherCode);
        int? appliedVoucherId = null;
        if (normalizedVoucherCode is not null)
        {
            var now = DateTime.UtcNow;
            var voucher = await _db.Vouchers.AsNoTracking().FirstOrDefaultAsync(row => row.Code == normalizedVoucherCode);
            if (voucher is null || !voucher.IsActive)
            {
                throw new InvalidOperationException("Mã giảm giá không hợp lệ.");
            }

            if (voucher.StartDate > now || voucher.EndDate < now)
            {
                throw new InvalidOperationException("Mã giảm giá đã hết hạn hoặc chưa bắt đầu.");
            }

            if (voucher.UsedCount >= voucher.UsageLimit)
            {
                throw new InvalidOperationException("Mã giảm giá đã hết lượt sử dụng.");
            }

            if (subtotal < voucher.MinOrderAmount)
            {
                throw new InvalidOperationException($"Đơn hàng cần tối thiểu {voucher.MinOrderAmount:N0} ₫ để dùng mã này.");
            }

            if (await _db.VoucherUsages.AnyAsync(usage => usage.VoucherId == voucher.Id && usage.UserId == userId))
            {
                throw new InvalidOperationException("Bạn đã sử dụng mã giảm giá này.");
            }

            var affected = await _db.Database.ExecuteSqlRawAsync(
                "UPDATE Vouchers SET UsedCount = UsedCount + 1 WHERE Id = {0} AND UsedCount < UsageLimit",
                voucher.Id);
            if (affected == 0)
            {
                throw new InvalidOperationException("Mã giảm giá vừa hết lượt sử dụng. Vui lòng chọn mã khác.");
            }

            order.VoucherCode = voucher.Code;
            order.DiscountAmount = CalculateDiscount(voucher, subtotal);
            appliedVoucherId = voucher.Id;
        }
        order.TotalAmount = Math.Max(0, subtotal - order.DiscountAmount);

        _db.Orders.Add(order);
        if (appliedVoucherId.HasValue)
        {
            _db.VoucherUsages.Add(new VoucherUsage
            {
                VoucherId = appliedVoucherId.Value,
                UserId = userId,
                Order = order
            });
        }

        foreach (var item in cart.Items)
        {
            var affected = await _db.Database.ExecuteSqlRawAsync(
                "UPDATE Products SET Stock = Stock - {0} WHERE Id = {1} AND Stock >= {0} AND IsDeleted = 0",
                item.Quantity,
                item.ProductId);
            if (affected == 0)
            {
                throw new InvalidOperationException($"Sản phẩm '{item.Product?.Name ?? "không xác định"}' vừa hết hàng. Vui lòng cập nhật giỏ hàng.");
            }
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (normalizedVoucherCode is not null && IsVoucherUsageUniqueViolation(ex))
        {
            throw new InvalidOperationException("Bạn đã sử dụng mã giảm giá này.");
        }

        await _cartService.ClearAsync(userId, sessionId);
        await transaction.CommitAsync();
        _logger.LogInformation("Order {OrderId} created for user {UserId} with payment {PaymentMethod} and total {TotalAmount}", order.Id, userId, order.PaymentMethod, order.TotalAmount);
        return order;
    }

    public async Task<IEnumerable<Order>> GetUserOrdersAsync(string userId)
    {
        return await _db.Orders
            .IgnoreQueryFilters()
            .Include(order => order.Items).ThenInclude(item => item.Product)
            .Include(order => order.ShippingInfo)
            .Where(order => order.UserId == userId)
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync();
    }

    public async Task<UserOrdersViewModel> GetUserOrderHistoryAsync(string userId, string? status, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Max(1, pageSize);
        var activeStatus = OrderStatusFilters.Normalize(status);
        var targetStatus = OrderStatusFilters.ToOrderStatus(activeStatus);
        var query = _db.Orders
            .IgnoreQueryFilters()
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

        var totalItems = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Min(page, totalPages);

        return new UserOrdersViewModel
        {
            ActiveStatus = activeStatus,
            Orders = await query
                .OrderByDescending(order => order.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(order => order.Items).ThenInclude(item => item.Product)
                .Include(order => order.ShippingInfo)
                .ToListAsync(),
            Tabs = OrderStatusFilters.Tabs.Select(tab => new OrderStatusTabViewModel
            {
                Key = tab.Key,
                Label = tab.Label,
                Count = tab.Status is null ? total : counts.GetValueOrDefault(tab.Status),
                IsActive = tab.Key == activeStatus
            }).ToList(),
            CurrentPage = page,
            TotalPages = totalPages,
            PageSize = pageSize,
            TotalItems = totalItems
        };
    }

    public async Task<Order?> GetUserOrderAsync(int id, string userId)
    {
        return await _db.Orders
            .IgnoreQueryFilters()
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
            .IgnoreQueryFilters()
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
        var revenueOrders = _db.Orders.Where(order => order.IsPaid || order.Status == OrderStatuses.Delivered);

        var topProducts = await _db.OrderItems
            .IgnoreQueryFilters()
            .Include(item => item.Product)
            .Where(item => item.Order != null && (item.Order.IsPaid || item.Order.Status == OrderStatuses.Delivered))
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

        var revenuePoints = await revenueOrders
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
            TodayRevenue = await revenueOrders.Where(order => order.CreatedAt >= startOfDay).SumAsync(order => order.TotalAmount),
            WeekRevenue = await revenueOrders.Where(order => order.CreatedAt >= startOfWeek).SumAsync(order => order.TotalAmount),
            MonthRevenue = await revenueOrders.Where(order => order.CreatedAt >= startOfMonth).SumAsync(order => order.TotalAmount),
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

    private static string? NormalizeVoucherCode(string? code)
    {
        return string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
    }

    private static bool IsVoucherUsageUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException
            && sqlException.Errors.Cast<SqlError>().Any(error => error.Number is 2601 or 2627
                && error.Message.Contains("IX_VoucherUsages_VoucherId_UserId", StringComparison.OrdinalIgnoreCase));
    }
}
