using System.ComponentModel.DataAnnotations;
using EcommerceApp.Data;
using EcommerceApp.Controllers.Admin;
using EcommerceApp.Hubs;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using EcommerceApp.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace EcommerceApp.Tests;

[Collection("PortSqlServer")]
public class OperationalPortTests(PortSqlServerFixture fixture)
{
    [Fact]
    public async Task Checkout_and_confirmed_cancellation_log_balances_and_order_once()
    {
        var (productId, userId) = await SeedCartAsync(3);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = CreateService(db);
        var order = await service.CreateOrderAsync(userId, Checkout(productId), "test-session");
        Assert.Equal(2, await db.Products.Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync());
        Assert.True(await service.UpdateStatusAsync(order.Id, OrderStatuses.Confirmed));
        Assert.True(await service.CancelUserOrderAsync(order.Id, userId, "Đổi ý trước khi giao"));
        Assert.False(await service.CancelUserOrderAsync(order.Id, userId, "Thử hủy lại"));

        db.ChangeTracker.Clear();
        Assert.Equal(3, await db.Products.Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync());
        var movements = await db.StockLogs.Where(log => log.ProductId == productId && log.OrderId == order.Id)
            .OrderBy(log => log.Id).ToListAsync();
        Assert.Collection(movements,
            sale => { Assert.Equal(StockMovementTypes.Sale, sale.MovementType); Assert.Equal(3, sale.QuantityBefore); Assert.Equal(2, sale.QuantityAfter); },
            reversal => { Assert.Equal(StockMovementTypes.Cancellation, reversal.MovementType); Assert.Equal(2, reversal.QuantityBefore); Assert.Equal(3, reversal.QuantityAfter); });
    }

    [Fact]
    public async Task Delivered_cod_refund_requires_receipt_and_records_audit_once()
    {
        var (productId, userId) = await SeedCartAsync(2);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = CreateService(db);
        var order = await service.CreateOrderAsync(userId, Checkout(productId), "test-session");
        Assert.True(await service.UpdateStatusAsync(order.Id, OrderStatuses.Confirmed));
        Assert.True(await service.UpdateStatusAsync(order.Id, OrderStatuses.Shipping));
        Assert.True(await service.UpdateStatusAsync(order.Id, OrderStatuses.Delivered));
        Assert.False(await service.MarkManualRefundCompletedAsync(order.Id, ManualRefundMethods.Cash, "", "Lý do", userId));
        Assert.True(await service.MarkManualRefundCompletedAsync(order.Id, ManualRefundMethods.Cash, "receipt-123", "Hoàn theo thỏa thuận", userId));
        Assert.False(await service.MarkManualRefundCompletedAsync(order.Id, ManualRefundMethods.Cash, "receipt-123", "Lặp", userId));
        db.ChangeTracker.Clear();
        var saved = await db.Orders.SingleAsync(row => row.Id == order.Id);
        Assert.True(saved.IsPaid);
        Assert.Equal(RefundStatuses.Refunded, saved.RefundStatus);
        Assert.Equal("receipt-123", saved.RefundReference);
        Assert.Equal(order.TotalAmount, saved.RefundAmount);
        Assert.Equal(userId, saved.RefundConfirmedBy);
    }

    [Fact]
    public async Task Paid_vnpay_cancellation_can_be_reconciled_once_with_vnpay_reference()
    {
        var (productId, userId) = await SeedCartAsync(2);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = CreateService(db);
        var checkout = Checkout(productId);
        checkout.PaymentMethod = "VNPAY";
        var created = await service.CreateOrderAsync(userId, checkout, "test-session");
        var order = await db.Orders.SingleAsync(row => row.Id == created.Id);
        order.IsPaid = true;
        order.PaidAt = DateTime.UtcNow;
        order.Status = OrderStatuses.Pending;
        await db.SaveChangesAsync();
        Assert.True(await service.CancelUserOrderAsync(order.Id, userId, "Đổi ý"));
        db.ChangeTracker.Clear();
        Assert.Equal(RefundStatuses.PendingManual,
            await db.Orders.Where(row => row.Id == order.Id).Select(row => row.RefundStatus).SingleAsync());
        Assert.False(await service.MarkManualRefundCompletedAsync(order.Id, ManualRefundMethods.Cash, "", "Lý do", userId));
        Assert.True(await service.MarkManualRefundCompletedAsync(order.Id, ManualRefundMethods.Vnpay,
            "VNPAY-REFUND-123", "Đã hoàn qua VNPAY", userId));
        db.ChangeTracker.Clear();
        var saved = await db.Orders.SingleAsync(row => row.Id == order.Id);
        Assert.Equal(RefundStatuses.Refunded, saved.RefundStatus);
        Assert.Equal(ManualRefundMethods.Vnpay, saved.RefundMethod);
        Assert.Equal("VNPAY-REFUND-123", saved.RefundReference);
        Assert.Equal(2, await db.Products.Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync());
    }

    [Fact]
    public async Task Manual_stock_adjustment_records_before_after_without_order()
    {
        var (productId, _) = await SeedCartAsync(5);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await new ProductService(db).AdjustStockAsync(productId, -2, "Kiểm kê", null));
        db.ChangeTracker.Clear();
        var log = await db.StockLogs.Where(row => row.ProductId == productId).SingleAsync();
        Assert.Equal(StockMovementTypes.Manual, log.MovementType);
        Assert.Equal(5, log.QuantityBefore);
        Assert.Equal(3, log.QuantityAfter);
        Assert.Null(log.OrderId);
    }

    [Fact]
    public async Task Store_reply_is_public_only_for_approved_review()
    {
        var (productId, userId) = await SeedCartAsync(1);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var review = new Review { ProductId = productId, UserId = userId, Rating = 5,
            Comment = "Sản phẩm sử dụng tốt.", IsApproved = true };
        db.Reviews.Add(review);
        await db.SaveChangesAsync();
        var controller = new AdminReviewController(db, Mock.Of<IUserNotificationService>())
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };
        await controller.Reply(review.Id, "  Cảm ơn bạn đã đánh giá.  ");
        db.ChangeTracker.Clear();
        Assert.Equal("Cảm ơn bạn đã đánh giá.", (await db.Reviews.SingleAsync(r => r.Id == review.Id)).StoreReply);
        review = await db.Reviews.SingleAsync(r => r.Id == review.Id);
        review.IsApproved = false;
        await db.SaveChangesAsync();
        await controller.Reply(review.Id, "Không hiển thị");
        db.ChangeTracker.Clear();
        Assert.Equal("Cảm ơn bạn đã đánh giá.", (await db.Reviews.SingleAsync(r => r.Id == review.Id)).StoreReply);
    }

    [Fact]
    public void Confirmed_order_can_cancel_only_before_shipping_handoff()
    {
        var order = new Order { Status = OrderStatuses.Confirmed };
        Assert.True(OrderLifecycle.CanCustomerCancel(order));
        order.ShippingInfo = new ShippingInfo { Status = ShippingStatuses.NotAssigned };
        Assert.True(OrderLifecycle.CanCustomerCancel(order));
        order.ShippingInfo.TrackingCode = "GHN-123";
        Assert.False(OrderLifecycle.CanCustomerCancel(order));
        order.Status = OrderStatuses.Shipping;
        Assert.False(OrderLifecycle.CanCustomerCancel(order));
    }

    [Fact]
    public void Warranty_form_keeps_server_validation_for_short_description_and_phone()
    {
        var request = new ReturnWarrantyRequestCreateViewModel
        {
            OrderId = 1, ContactName = "Khách", ContactPhone = "123", Reason = "Bị lỗi",
            Description = "   mô tả ngắn   "
        };
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), errors, true));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(request.Description)) && error.ErrorMessage!.Contains("hiện có"));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(request.ContactPhone)));
        request.ContactPhone = "0901234567";
        request.Description = "Linh kiện bị lỗi khi bật máy từ sáng hôm qua.";
        errors.Clear();
        Assert.True(Validator.TryValidateObject(request, new ValidationContext(request), errors, true));
    }

    private async Task<(int ProductId, string UserId)> SeedCartAsync(int stock)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new ApplicationUser { Id = Guid.NewGuid().ToString("N"), UserName = Guid.NewGuid().ToString("N") + "@test.local",
            Email = Guid.NewGuid().ToString("N") + "@test.local", FullName = "Test buyer" };
        var product = new Product { Name = "Test component", Description = "Fixture", Price = 1_000_000,
            Stock = stock, Category = new Category { Name = "Test", Slug = Guid.NewGuid().ToString("N") } };
        db.Users.Add(user);
        db.Products.Add(product);
        db.Carts.Add(new Cart { User = user, Items = new List<CartItem> { new() { Product = product, Quantity = 1 } } });
        await db.SaveChangesAsync();
        return (product.Id, user.Id);
    }

    private static CheckoutViewModel Checkout(int productId) => new()
    {
        SelectedProductIds = new List<int> { productId }, PaymentMethod = "COD",
        RecipientName = "Test", RecipientPhone = "0901234567", Province = "Hồ Chí Minh", District = "Quận 1"
    };

    private static OrderService CreateService(AppDbContext db)
    {
        var crossSell = new CrossSellService(db);
        return new OrderService(db, new CartService(db, crossSell), new ShippingFeeService(),
            Mock.Of<IOrderEmailService>(), Mock.Of<ICustomerSegmentService>(), crossSell,
            Mock.Of<IUserNotificationService>(), Mock.Of<IHubContext<AdminNotificationHub>>(),
            NullLogger<OrderService>.Instance);
    }
}
