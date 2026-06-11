using EcommerceApp.Data;
using EcommerceApp.Hubs;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Services;

public class OrderService : IOrderService
{
    private readonly AppDbContext _db;
    private readonly ICartService _cartService;
    private readonly IShippingFeeService _shippingFeeService;
    private readonly IOrderEmailService _orderEmailService;
    private readonly ICustomerSegmentService _customerSegmentService;
    private readonly ICrossSellService _crossSellService;
    private readonly IUserNotificationService _notificationService;
    private readonly IHubContext<AdminNotificationHub> _adminNotificationHub;
    private readonly ILogger<OrderService> _logger;

    public OrderService(AppDbContext db, ICartService cartService, IShippingFeeService shippingFeeService, IOrderEmailService orderEmailService, ICustomerSegmentService customerSegmentService, ICrossSellService crossSellService, IUserNotificationService notificationService, IHubContext<AdminNotificationHub> adminNotificationHub, ILogger<OrderService> logger)
    {
        _db = db;
        _cartService = cartService;
        _shippingFeeService = shippingFeeService;
        _orderEmailService = orderEmailService;
        _customerSegmentService = customerSegmentService;
        _crossSellService = crossSellService;
        _notificationService = notificationService;
        _adminNotificationHub = adminNotificationHub;
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

        var crossSellUnitPrices = await _crossSellService.GetEligibleUnitPricesAsync(cart.Items);
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
                UnitPrice = crossSellUnitPrices.TryGetValue(item.ProductId, out var unitPrice)
                    ? unitPrice
                    : (item.Product?.SalePrice ?? item.Product?.Price ?? 0)
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

            if (!string.IsNullOrWhiteSpace(voucher.TargetUserId) && voucher.TargetUserId != userId)
            {
                throw new InvalidOperationException("Mã giảm giá này chỉ áp dụng cho tài khoản được tặng.");
            }

            if (voucher.CustomerSegmentId.HasValue &&
                !await _customerSegmentService.UserBelongsToSegmentAsync(userId, voucher.CustomerSegmentId.Value))
            {
                throw new InvalidOperationException("Mã giảm giá này chỉ áp dụng cho nhóm khách hàng phù hợp.");
            }

            var affected = await _db.Database.ExecuteSqlRawAsync(
                "UPDATE Vouchers SET UsedCount = UsedCount + 1 WHERE Id = {0} AND UsedCount < UsageLimit",
                voucher.Id);
            if (affected == 0)
            {
                throw new InvalidOperationException("Mã giảm giá vừa hết lượt sử dụng. Vui lòng chọn mã khác.");
            }

            order.DiscountAmount = CalculateDiscount(voucher, subtotal);
            appliedVoucherId = voucher.Id;
        }

        var shippingQuote = _shippingFeeService.Calculate(model.Province, model.District, subtotal);
        order.ShippingFee = shippingQuote.Fee;
        order.TotalAmount = Math.Max(0, subtotal - order.DiscountAmount) + order.ShippingFee;

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
        await _customerSegmentService.RefreshUserAsync(userId);
        _logger.LogInformation("Order {OrderId} created for user {UserId} with payment {PaymentMethod} and total {TotalAmount}", order.Id, userId, order.PaymentMethod, order.TotalAmount);
        await _orderEmailService.SendOrderCreatedAsync(order.Id);
        await _notificationService.CreateAsync(
            userId,
            "Đã nhận đơn hàng",
            $"Techvora đã nhận đơn #DH{order.Id:D4}. Bạn có thể theo dõi tiến trình trong lịch sử đơn hàng.",
            NotificationTypes.Order,
            $"/Order/Detail/{order.Id}");
        await _notificationService.CreateForAdminsAsync(
            "Đơn hàng mới",
            $"Đơn #DH{order.Id:D4} vừa được tạo với tổng tiền {order.TotalAmount:N0} đ.",
            NotificationTypes.Order,
            $"/Admin/Order/{order.Id}");
        try
        {
            await _adminNotificationHub.Clients
                .Group(AdminNotificationHub.AdminGroup)
                .SendAsync("OrderCreated", new AdminOrderCreatedMessage(
                    order.Id,
                    $"DH{order.Id:D4}",
                    order.RecipientName,
                    order.TotalAmount,
                    order.CreatedAt,
                    $"/Admin/Order/{order.Id}"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not publish realtime notification for order {OrderId}", order.Id);
        }
        return order;
    }

    public async Task<IEnumerable<Order>> GetUserOrdersAsync(string userId)
    {
        return await _db.Orders
            .IgnoreQueryFilters()
            .Include(order => order.Items).ThenInclude(item => item.Product)
            .ThenInclude(product => product!.Images)
            .Include(order => order.ShippingInfo)
            .Include(order => order.VoucherUsage).ThenInclude(usage => usage!.Voucher)
            .Include(order => order.ReturnWarrantyRequests)
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
                .ThenInclude(product => product!.Images)
                .Include(order => order.ShippingInfo)
                .Include(order => order.VoucherUsage).ThenInclude(usage => usage!.Voucher)
                .Include(order => order.ReturnWarrantyRequests)
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
            .ThenInclude(product => product!.Images)
            .Include(order => order.ShippingInfo)
            .Include(order => order.VoucherUsage).ThenInclude(usage => usage!.Voucher)
            .Include(order => order.ReturnWarrantyRequests)
            .FirstOrDefaultAsync(order => order.Id == id && order.UserId == userId);
    }

    public async Task<OrderListViewModel> GetOrdersAsync(string? status, string? customer, DateTime? fromDate, DateTime? toDate)
    {
        var query = _db.Orders
            .AsNoTracking()
            .AsSplitQuery()
            .Include(order => order.User)
            .Include(order => order.ShippingInfo)
            .Include(order => order.VoucherUsage).ThenInclude(usage => usage!.Voucher)
            .Include(order => order.ReturnWarrantyRequests)
            .Include(order => order.Items)
            .ThenInclude(item => item.Product)
            .ThenInclude(product => product!.Images)
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
            .ThenInclude(product => product!.Images)
            .Include(order => order.ShippingInfo)
            .Include(order => order.VoucherUsage).ThenInclude(usage => usage!.Voucher)
            .Include(order => order.ReturnWarrantyRequests)
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

        var oldStatus = order.Status;
        var oldRefundStatus = order.RefundStatus;
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

        if (status == OrderStatuses.Cancelled)
        {
            MarkManualRefundRequiredIfNeeded(order, "Đơn VNPAY đã thanh toán bị huỷ bởi admin.");
        }

        await _db.SaveChangesAsync();
        await _customerSegmentService.RefreshUserAsync(order.UserId);
        _logger.LogInformation("Admin updated order {OrderId} status to {Status}", id, status);

        if (oldStatus != status)
        {
            await _orderEmailService.SendOrderStatusChangedAsync(order.Id, status);
            await _notificationService.CreateAsync(
                order.UserId,
                "Cập nhật đơn hàng",
                $"Đơn #DH{order.Id:D4} hiện ở trạng thái: {OrderStatusFilters.ToDisplayLabel(status)}.",
                NotificationTypes.Order,
                $"/Order/Detail/{order.Id}");
        }

        if (oldRefundStatus != order.RefundStatus && order.RefundStatus == RefundStatuses.PendingManual)
        {
            await _orderEmailService.SendRefundRequiredAsync(order.Id);
        }
    }

    public async Task<int> ConfirmPendingOrdersAsync(IEnumerable<int> ids)
    {
        var orderIds = ids.Distinct().ToList();
        if (!orderIds.Any())
        {
            return 0;
        }

        var orders = await _db.Orders
            .Where(order => orderIds.Contains(order.Id) && order.Status == OrderStatuses.Pending)
            .ToListAsync();

        foreach (var order in orders)
        {
            order.Status = OrderStatuses.Confirmed;
            order.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Admin bulk-confirmed {OrderCount} pending orders", orders.Count);
        foreach (var order in orders)
        {
            await _orderEmailService.SendOrderStatusChangedAsync(order.Id, OrderStatuses.Confirmed);
            await _notificationService.CreateAsync(
                order.UserId,
                "Đơn hàng đã được xác nhận",
                $"Đơn #DH{order.Id:D4} đang được chuẩn bị.",
                NotificationTypes.Order,
                $"/Order/Detail/{order.Id}");
        }

        return orders.Count;
    }

    public async Task<bool> MarkManualRefundCompletedAsync(int id, string? note)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(row => row.Id == id);
        if (order is null || order.RefundStatus != RefundStatuses.PendingManual)
        {
            return false;
        }

        order.RefundStatus = RefundStatuses.Refunded;
        order.RefundedAt = DateTime.UtcNow;
        order.RefundNote = string.IsNullOrWhiteSpace(note)
            ? "Admin đã xác nhận hoàn tiền thủ công."
            : note.Trim();
        order.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        _logger.LogInformation("Admin marked manual refund completed for order {OrderId}", id);
        await _orderEmailService.SendRefundCompletedAsync(order.Id);
        await _notificationService.CreateAsync(
            order.UserId,
            "Đã ghi nhận hoàn tiền",
            $"Đơn #DH{order.Id:D4} đã được đánh dấu hoàn tiền.",
            NotificationTypes.Order,
            $"/Order/Detail/{order.Id}");
        return true;
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
        var oldRefundStatus = order.RefundStatus;
        order.Status = OrderStatuses.Cancelled;
        order.CancelledReason = string.IsNullOrWhiteSpace(reason)
            ? "Khách hàng hủy đơn trước khi xác nhận."
            : reason.Trim();
        order.UpdatedAt = DateTime.UtcNow;
        MarkManualRefundRequiredIfNeeded(order, "Đơn VNPAY đã thanh toán bị khách hàng huỷ.");

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
        await _orderEmailService.SendOrderStatusChangedAsync(order.Id, OrderStatuses.Cancelled);
        await _notificationService.CreateAsync(
            userId,
            "Đã huỷ đơn hàng",
            $"Đơn #DH{order.Id:D4} đã được huỷ và tồn kho đã được hoàn lại.",
            NotificationTypes.Order,
            $"/Order/Detail/{order.Id}");
        await _notificationService.CreateForAdminsAsync(
            "Khách huỷ đơn hàng",
            $"Đơn #DH{order.Id:D4} vừa được khách hàng huỷ.",
            NotificationTypes.Order,
            $"/Admin/Order/{order.Id}");
        if (oldRefundStatus != order.RefundStatus && order.RefundStatus == RefundStatuses.PendingManual)
        {
            await _orderEmailService.SendRefundRequiredAsync(order.Id);
        }

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
            try
            {
                await _cartService.AddAsync(item.ProductId, quantity, userId, sessionId);
                added++;
            }
            catch (InvalidOperationException)
            {
                // The cart may already contain all currently available stock.
            }
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

    private static void MarkManualRefundRequiredIfNeeded(Order order, string note)
    {
        if (!order.IsPaid ||
            !string.Equals(order.PaymentMethod, "VNPAY", StringComparison.OrdinalIgnoreCase) ||
            order.RefundStatus is RefundStatuses.PendingManual or RefundStatuses.Refunded)
        {
            return;
        }

        order.RefundStatus = RefundStatuses.PendingManual;
        order.RefundRequestedAt ??= DateTime.UtcNow;
        order.RefundNote = note;
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
