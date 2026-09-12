using EcommerceApp.Data;
using EcommerceApp.Hubs;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Data;

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
        var order = await DatabaseTransaction.ExecuteAsync(_db, async () =>
        {
            var cart = await _cartService.GetCartEntityAsync(userId, sessionId);
            if (cart is null || !cart.Items.Any())
            {
                throw new InvalidOperationException("Giỏ hàng đang trống.");
            }

            var selectedProductIds = model.SelectedProductIds
                .Where(id => id > 0)
                .Distinct()
                .ToHashSet();
            if (!selectedProductIds.Any())
            {
                throw new InvalidOperationException("Vui lòng chọn sản phẩm cần thanh toán.");
            }

            var checkoutItems = cart.Items
                .Where(item => selectedProductIds.Contains(item.ProductId))
                .ToList();
            if (!checkoutItems.Any())
            {
                throw new InvalidOperationException("Các sản phẩm đã chọn không còn trong giỏ hàng.");
            }

            foreach (var item in checkoutItems)
            {
                if (item.Quantity <= 0)
                    throw new InvalidOperationException("Số lượng sản phẩm trong giỏ không hợp lệ. Vui lòng xoá sản phẩm và thêm lại.");
                if (item.Product is null || item.Product.Stock < item.Quantity)
                {
                    throw new InvalidOperationException($"Sản phẩm '{item.Product?.Name ?? "không xác định"}' không đủ hàng. Còn {item.Product?.Stock ?? 0} sản phẩm.");
                }
            }

            var isVnpayOrder = IsVnpayPayment(model.PaymentMethod);
            var crossSellUnitPrices = await _crossSellService.GetEligibleUnitPricesAsync(checkoutItems);
            var order = new Order
            {
                PaymentExpiresAt = IsVnpayPayment(model.PaymentMethod) ? DateTime.UtcNow.Add(OrderLifecycle.PaymentWindow) : null,
                UserId = userId,
                RecipientName = model.RecipientName,
                RecipientPhone = model.RecipientPhone,
                ShippingAddress = model.ShippingAddress,
                PaymentMethod = model.PaymentMethod,
                Status = isVnpayOrder ? OrderStatuses.AwaitingPayment : OrderStatuses.Pending,
                IsPaid = false,
                PaidAt = null,
                Items = checkoutItems.Select(item => new OrderItem
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

            foreach (var item in checkoutItems)
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

            _db.CartItems.RemoveRange(checkoutItems);
            cart.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (normalizedVoucherCode is not null && IsVoucherUsageUniqueViolation(ex))
            {
                throw new InvalidOperationException("Bạn đã sử dụng mã giảm giá này.");
            }
            return order;
        });
        var isVnpayOrder = IsVnpayPayment(order.PaymentMethod);
        await _customerSegmentService.RefreshUserAsync(userId);
        _logger.LogInformation("Order {OrderId} created for user {UserId} with payment {PaymentMethod} and total {TotalAmount}", order.Id, userId, order.PaymentMethod, order.TotalAmount);
        await _orderEmailService.SendOrderCreatedAsync(order.Id);

        if (isVnpayOrder)
        {
            await _notificationService.CreateAsync(
                userId,
                "Đơn đang chờ thanh toán",
                $"Đơn #DH{order.Id:D4} đã được tạo. Hoàn tất VNPAY để gửi đơn sang admin xác nhận.",
                NotificationTypes.Order,
                $"/Order/Detail/{order.Id}");
        }
        else
        {
            await _notificationService.CreateAsync(
                userId,
                "Đã nhận đơn hàng",
                $"Techvora đã nhận đơn #DH{order.Id:D4}. Bạn có thể theo dõi tiến trình trong lịch sử đơn hàng.",
                NotificationTypes.Order,
                $"/Order/Detail/{order.Id}");
            await NotifyAdminsAboutNewOrderAsync(order);
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

    public Task<bool> UpdateStatusAsync(int id, string status) => ChangeStatusAsync(id, status);

    private async Task<bool> ChangeStatusAsync(int id, string status, string? expectedStatus = null)
    {
        if (!OrderStatuses.All.Contains(status))
        {
            return false;
        }

        var change = await DatabaseTransaction.ExecuteAsync<(Order Order, string OldStatus, string OldRefundStatus)?>(_db, async () =>
        {
            var order = await OrderLifecycle.LoadForUpdateAsync(_db, id);
            if (order is null || (expectedStatus is not null && order.Status != expectedStatus)
                || !OrderLifecycle.CanTransition(order, status))
            {
                return null;
            }

            var oldStatus = order.Status;
            var oldRefundStatus = order.RefundStatus;
            if (status == OrderStatuses.Cancelled)
                await OrderLifecycle.CancelAsync(_db, order, "Đơn hàng bị huỷ bởi admin.", DateTime.UtcNow);
            else
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
            return (order, oldStatus, oldRefundStatus);
        }, IsolationLevel.Serializable);
        if (change is null) return false;
        var (order, oldStatus, oldRefundStatus) = change.Value;
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

        return true;
    }

    public async Task<int> ConfirmPendingOrdersAsync(IEnumerable<int> ids)
    {
        var confirmed = 0;
        foreach (var id in ids.Where(id => id > 0).Distinct().OrderBy(id => id))
            if (await ChangeStatusAsync(id, OrderStatuses.Confirmed, OrderStatuses.Pending)) confirmed++;
        return confirmed;
    }

    public async Task<bool> MarkManualRefundCompletedAsync(int id, string? note)
    {
        if (note?.Length > 500) return false;
        var order = await DatabaseTransaction.ExecuteAsync<Order?>(_db, async () =>
        {
            var current = await OrderLifecycle.LoadForUpdateAsync(_db, id);
            if (current is null || current.RefundStatus != RefundStatuses.PendingManual) return null;
            current.RefundStatus = RefundStatuses.Refunded;
            current.RefundedAt = DateTime.UtcNow;
            current.RefundNote = string.IsNullOrWhiteSpace(note) ? "Admin đã xác nhận hoàn tiền thủ công." : note.Trim();
            current.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return current;
        });
        if (order is null) return false;
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
        var change = await DatabaseTransaction.ExecuteAsync<(Order Order, string OldRefundStatus)?>(_db, async () =>
        {
            var order = await _db.Orders
                .FromSqlInterpolated($"SELECT * FROM Orders WITH (UPDLOCK, ROWLOCK) WHERE Id = {id} AND UserId = {userId}")
                .Include(row => row.Items)
                .FirstOrDefaultAsync();
            if (order is null || order.Status is not (OrderStatuses.AwaitingPayment or OrderStatuses.Pending))
            {
                return null;
            }

            var oldRefundStatus = order.RefundStatus;
            var cancellationReason = string.IsNullOrWhiteSpace(reason)
                ? "Khách hàng hủy đơn trước khi xác nhận."
                : reason.Trim();
            await OrderLifecycle.CancelAsync(_db, order, cancellationReason, DateTime.UtcNow);

            await _db.SaveChangesAsync();
            return (order, oldRefundStatus);
        }, IsolationLevel.Serializable);
        if (change is null) return false;
        var (order, oldRefundStatus) = change.Value;
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
        var startOfWeek = startOfDay.AddDays(-(((int)startOfDay.DayOfWeek + 6) % 7));
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        var startOfYear = new DateTime(now.Year, 1, 1);
        var startOfEngagementWindow = startOfDay.AddDays(-30);
        var revenueOrders = _db.Orders.WithRecognizedRevenue();

        var todayRevenue = await revenueOrders.Where(order => order.CreatedAt >= startOfDay).SumAsync(order => order.TotalAmount);
        var weekRevenue = await revenueOrders.Where(order => order.CreatedAt >= startOfWeek).SumAsync(order => order.TotalAmount);
        var monthRevenue = await revenueOrders.Where(order => order.CreatedAt >= startOfMonth).SumAsync(order => order.TotalAmount);
        var yearRevenue = await revenueOrders.Where(order => order.CreatedAt >= startOfYear).SumAsync(order => order.TotalAmount);
        var todayOrders = await _db.Orders.CountAsync(order => order.CreatedAt >= startOfDay);
        var weekOrders = await _db.Orders.CountAsync(order => order.CreatedAt >= startOfWeek);
        var monthOrders = await _db.Orders.CountAsync(order => order.CreatedAt >= startOfMonth);
        var yearOrders = await _db.Orders.CountAsync(order => order.CreatedAt >= startOfYear);

        var topProducts = await _db.OrderItems
            .IgnoreQueryFilters()
            .Include(item => item.Product)
            .WithRecognizedRevenue()
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

        var revenueByDate = await revenueOrders
            .Where(order => order.CreatedAt >= startOfDay.AddDays(-6))
            .GroupBy(order => order.CreatedAt.Date)
            .Select(group => new { Date = group.Key, Revenue = group.Sum(order => order.TotalAmount) })
            .ToDictionaryAsync(point => point.Date, point => point.Revenue);

        var revenuePoints = Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var date = startOfDay.AddDays(offset - 6);
                return new RevenuePointViewModel
                {
                    Label = date.ToString("dd/MM"),
                    Revenue = revenueByDate.GetValueOrDefault(date)
                };
            })
            .ToList();

        var salesLast30Rows = await _db.OrderItems
            .IgnoreQueryFilters()
            .WithRecognizedRevenue()
            .Where(item => item.Order!.CreatedAt >= startOfEngagementWindow)
            .GroupBy(item => item.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                Quantity = group.Sum(item => item.Quantity),
                Revenue = group.Sum(item => item.Quantity * item.UnitPrice)
            })
            .ToListAsync();
        var salesLast30 = salesLast30Rows.ToDictionary(row => row.ProductId);

        var interactionRows = await _db.ProductInteractions
            .AsNoTracking()
            .Where(interaction => interaction.CreatedAt >= startOfEngagementWindow)
            .GroupBy(interaction => new { interaction.ProductId, ProductName = interaction.Product!.Name })
            .Select(group => new
            {
                group.Key.ProductId,
                group.Key.ProductName,
                ViewCount = group.Count(interaction => interaction.EventType == ProductInteractionEvents.DetailView),
                ClickCount = group.Count(interaction => interaction.EventType == ProductInteractionEvents.ProductClick),
                AddToCartCount = group.Count(interaction => interaction.EventType == ProductInteractionEvents.AddToCart),
                WishlistCount = group.Count(interaction => interaction.EventType == ProductInteractionEvents.WishlistAdd),
                UniqueSessions = group.Select(interaction => interaction.SessionId).Distinct().Count()
            })
            .ToListAsync();
        var viewsLast30 = interactionRows.ToDictionary(row => row.ProductId, row => row.ViewCount);

        var topViewedProducts = interactionRows
            .OrderByDescending(row => row.ViewCount)
            .ThenByDescending(row => row.ClickCount)
            .ThenByDescending(row => row.AddToCartCount)
            .Take(10)
            .Select(row =>
            {
                salesLast30.TryGetValue(row.ProductId, out var sale);
                return new ProductEngagementViewModel
                {
                    ProductId = row.ProductId,
                    ProductName = row.ProductName,
                    ViewCount = row.ViewCount,
                    ClickCount = row.ClickCount,
                    AddToCartCount = row.AddToCartCount,
                    WishlistCount = row.WishlistCount,
                    UniqueSessions = row.UniqueSessions,
                    SoldQuantity = sale?.Quantity ?? 0,
                    Revenue = sale?.Revenue ?? 0
                };
            })
            .ToList();

        var productsForPriceReview = await _db.Products
            .AsNoTracking()
            .Include(product => product.Category)
            .Where(product => product.Stock > 0)
            .ToListAsync();
        var priceReviewCandidates = productsForPriceReview
            .Select(product =>
            {
                salesLast30.TryGetValue(product.Id, out var sale);
                var sold = sale?.Quantity ?? 0;
                var views = viewsLast30.GetValueOrDefault(product.Id);
                var ageDays = Math.Max(0, (int)(now - product.CreatedAt).TotalDays);
                var reason = BuildPriceReviewReason(product, ageDays, views, sold);
                return new PriceReviewProductViewModel
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    CategoryName = product.Category?.Name,
                    Price = product.Price,
                    DiscountPercent = product.DiscountPercent,
                    Stock = product.Stock,
                    AgeDays = ageDays,
                    ViewsLast30Days = views,
                    SoldLast30Days = sold,
                    InventoryValue = product.Stock * product.Price,
                    Reason = reason
                };
            })
            .Where(product => !string.IsNullOrWhiteSpace(product.Reason))
            .OrderByDescending(product => product.InventoryValue)
            .ThenByDescending(product => product.ViewsLast30Days)
            .ThenByDescending(product => product.AgeDays)
            .ToList();

        var lowStockProducts = await _db.Products
            .Include(product => product.Category)
            .Where(product => product.Stock <= 5)
            .OrderBy(product => product.Stock)
            .ThenBy(product => product.Name)
            .Take(10)
            .ToListAsync();

        return new AdminDashboardViewModel
        {
            TodayRevenue = todayRevenue,
            WeekRevenue = weekRevenue,
            MonthRevenue = monthRevenue,
            YearRevenue = yearRevenue,
            TotalOrders = await _db.Orders.CountAsync(),
            PendingOrdersCount = await _db.Orders.CountAsync(order => order.Status == OrderStatuses.Pending),
            AwaitingPaymentCount = await _db.Orders.CountAsync(order => order.Status == OrderStatuses.AwaitingPayment),
            ManualRefundCount = await _db.Orders.CountAsync(order => order.RefundStatus == RefundStatuses.PendingManual),
            LowStockCount = await _db.Products.CountAsync(product => product.Stock <= 5),
            PriceReviewCount = priceReviewCandidates.Count,
            OrdersByStatus = await _db.Orders.GroupBy(order => order.Status).ToDictionaryAsync(group => group.Key, group => group.Count()),
            PeriodMetrics = new[]
            {
                new DashboardPeriodMetricViewModel { Label = "Hôm nay", Revenue = todayRevenue, Orders = todayOrders },
                new DashboardPeriodMetricViewModel { Label = "Tuần này", Revenue = weekRevenue, Orders = weekOrders },
                new DashboardPeriodMetricViewModel { Label = "Tháng này", Revenue = monthRevenue, Orders = monthOrders },
                new DashboardPeriodMetricViewModel { Label = "Năm nay", Revenue = yearRevenue, Orders = yearOrders }
            },
            TopProducts = topProducts,
            TopViewedProducts = topViewedProducts,
            PriceReviewProducts = priceReviewCandidates.Take(8).ToList(),
            RevenuePoints = revenuePoints,
            LowStockProducts = lowStockProducts
        };

        static string BuildPriceReviewReason(Product product, int ageDays, int views, int sold)
        {
            if (ageDays >= 60 && product.Stock >= 8 && sold == 0)
            {
                return "Tồn lâu, chưa bán trong 30 ngày";
            }

            if (views >= 8 && sold == 0)
            {
                return "Nhiều người xem nhưng chưa ra đơn";
            }

            if (product.Stock >= 20 && sold <= 1)
            {
                return "Tồn kho cao, bán chậm";
            }

            if (product.DiscountPercent > 0 && views >= 8 && sold == 0)
            {
                return "Đã giảm giá nhưng chưa chuyển đổi";
            }

            return string.Empty;
        }
    }

    private async Task NotifyAdminsAboutNewOrderAsync(Order order)
    {
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
    }

    private static bool IsVnpayPayment(string? paymentMethod)
    {
        return string.Equals(paymentMethod, "VNPAY", StringComparison.OrdinalIgnoreCase);
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
