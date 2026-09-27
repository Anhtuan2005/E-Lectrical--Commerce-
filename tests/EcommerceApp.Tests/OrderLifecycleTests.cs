using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace EcommerceApp.Tests;

public sealed partial class OrderPaymentTests
{
    [Fact]
    public async Task Admin_cannot_skip_or_reverse_fulfilment_or_cancel_delivered_stock()
    {
        var (productId, users) = await SeedCartsAsync(2, 1);
        await using var scope = fixture.Services.CreateAsyncScope();
        var service = CreateService(scope.ServiceProvider);
        var order = await service.CreateOrderAsync(users[0], Checkout(productId), "test");
        Assert.False(await service.UpdateStatusAsync(order.Id, OrderStatuses.Delivered));
        Assert.True(await service.UpdateStatusAsync(order.Id, OrderStatuses.Confirmed));
        Assert.False(await service.UpdateStatusAsync(order.Id, OrderStatuses.Pending));
        Assert.True(await service.UpdateStatusAsync(order.Id, OrderStatuses.Shipping));
        Assert.False(await service.UpdateStatusAsync(order.Id, OrderStatuses.Cancelled));
        Assert.True(await service.UpdateStatusAsync(order.Id, OrderStatuses.Delivered));
        Assert.False(await service.UpdateStatusAsync(order.Id, OrderStatuses.Pending));
        Assert.False(await service.UpdateStatusAsync(order.Id, OrderStatuses.Cancelled));
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Products
            .Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Manual_and_GHN_shipping_cannot_resurrect_cancelled_or_ship_unpaid_orders(bool cancelled)
    {
        var (productId, users) = await SeedCartsAsync(2, 1);
        await using var scope = fixture.Services.CreateAsyncScope();
        var model = Checkout(productId);
        model.PaymentMethod = "VNPAY";
        var service = CreateService(scope.ServiceProvider);
        var order = await service.CreateOrderAsync(users[0], model, "test");
        if (cancelled) Assert.True(await service.CancelUserOrderAsync(order.Id, users[0], "Test"));
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var shipping = new ShippingService(db, Mock.Of<IOrderEmailService>(), Mock.Of<IUserNotificationService>());
        Assert.False(await shipping.AssignAsync(order.Id, "GHN", "test", null));
        Assert.False(await shipping.UpdateTrackingAsync(order.Id, "GHN", "test"));
        await Ghn(db).ApplyWebhookAsync(new GhnWebhookPayload { ClientOrderCode = $"DH{order.Id}", OrderCode = "test", Status = "delivered" });
        db.ChangeTracker.Clear();
        var saved = await db.Orders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal(cancelled ? OrderStatuses.Cancelled : OrderStatuses.AwaitingPayment, saved.Status);
        Assert.False(await db.ShippingInfos.AnyAsync(s => s.OrderId == order.Id));
    }

    [Fact]
    public async Task Concurrent_bulk_confirmation_and_cancellation_preserve_stock_and_only_one_winner()
    {
        var (productId, users) = await SeedCartsAsync(2, 1);
        int orderId;
        await using (var setup = fixture.Services.CreateAsyncScope())
            orderId = (await CreateService(setup.ServiceProvider).CreateOrderAsync(users[0], Checkout(productId), "test")).Id;
        await using var confirming = fixture.Services.CreateAsyncScope();
        await using var cancelling = fixture.Services.CreateAsyncScope();
        var confirm = CreateService(confirming.ServiceProvider).ConfirmPendingOrdersAsync(new[] { orderId, orderId });
        var cancel = CreateService(cancelling.ServiceProvider).CancelUserOrderAsync(orderId, users[0], "Test");
        await Task.WhenAll(confirm, cancel);
        var cancelled = await cancel;
        Assert.Equal(1, await confirm + (cancelled ? 1 : 0));
        await using var verify = fixture.Services.CreateAsyncScope();
        var db = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(cancelled ? OrderStatuses.Cancelled : OrderStatuses.Confirmed,
            await db.Orders.Where(o => o.Id == orderId).Select(o => o.Status).SingleAsync());
        Assert.Equal(cancelled ? 2 : 1, await db.Products.Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync());
        Assert.Equal(0, await CreateService(verify.ServiceProvider).ConfirmPendingOrdersAsync(new[] { orderId }));
    }

    [Fact]
    public async Task Expiry_cancellation_and_late_payment_race_release_stock_once_and_require_refund()
    {
        var (productId, users) = await SeedCartsAsync(2, 1);
        Order order;
        await using (var setup = fixture.Services.CreateAsyncScope())
        {
            var model = Checkout(productId); model.PaymentMethod = "VNPAY";
            order = await CreateService(setup.ServiceProvider).CreateOrderAsync(users[0], model, "test");
            var db = setup.ServiceProvider.GetRequiredService<AppDbContext>();
            order.PaymentExpiresAt = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }
        await using var expiring = fixture.Services.CreateAsyncScope();
        await using var cancelling = fixture.Services.CreateAsyncScope();
        await using var paying = fixture.Services.CreateAsyncScope();
        await Task.WhenAll(
            new OrderPaymentExpiryService(expiring.ServiceProvider.GetRequiredService<AppDbContext>()).ExpirePendingAsync(DateTime.UtcNow),
            CreateService(cancelling.ServiceProvider).CancelUserOrderAsync(order.Id, users[0], "Test"),
            CreatePaymentController(paying.ServiceProvider, order, new Mock<IOrderEmailService>()).VnpayIpn());
        await using var verify = fixture.Services.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await verifyDb.Orders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal(OrderStatuses.Cancelled, saved.Status);
        Assert.True(saved.IsPaid);
        Assert.Equal(RefundStatuses.PendingManual, saved.RefundStatus);
        Assert.Equal(2, await verifyDb.Products.Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync());
        await new OrderPaymentExpiryService(verifyDb).ExpirePendingAsync(DateTime.UtcNow);
        Assert.Equal(2, await verifyDb.Products.Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Callback_checks_deadline_even_before_expiry_worker_runs(bool expired)
    {
        var (productId, users) = await SeedCartsAsync(2, 1);
        await using var scope = fixture.Services.CreateAsyncScope();
        var model = Checkout(productId); model.PaymentMethod = "VNPAY";
        var order = await CreateService(scope.ServiceProvider).CreateOrderAsync(users[0], model, "test");
        Assert.NotNull(order.PaymentExpiresAt);
        Assert.InRange((order.PaymentExpiresAt!.Value - DateTime.UtcNow).TotalMinutes, 14, 15);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (expired) { order.PaymentExpiresAt = DateTime.UtcNow.AddSeconds(-1); await db.SaveChangesAsync(); }
        await CreatePaymentController(scope.ServiceProvider, order, new Mock<IOrderEmailService>()).VnpayIpn();
        await new OrderPaymentExpiryService(db).ExpirePendingAsync(DateTime.UtcNow.AddHours(1));
        db.ChangeTracker.Clear();
        var saved = await db.Orders.SingleAsync(o => o.Id == order.Id);
        Assert.True(saved.IsPaid);
        Assert.Equal(expired ? OrderStatuses.Cancelled : OrderStatuses.Pending, saved.Status);
        Assert.Equal(expired ? RefundStatuses.PendingManual : RefundStatuses.NotRequired, saved.RefundStatus);
        Assert.Equal(expired ? 2 : 1, await db.Products.Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync());
    }

    [Fact]
    public async Task Expiry_worker_preserves_unexpired_orders_and_expires_unpaid_orders_once()
    {
        var (productId, users) = await SeedCartsAsync(2, 1);
        await using var scope = fixture.Services.CreateAsyncScope();
        var model = Checkout(productId); model.PaymentMethod = "VNPAY";
        var order = await CreateService(scope.ServiceProvider).CreateOrderAsync(users[0], model, "test");
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var expiry = new OrderPaymentExpiryService(db);
        await expiry.ExpirePendingAsync(DateTime.UtcNow);
        Assert.Equal(OrderStatuses.AwaitingPayment, await db.Orders.Where(o => o.Id == order.Id).Select(o => o.Status).SingleAsync());
        await expiry.ExpirePendingAsync(order.PaymentExpiresAt!.Value);
        await expiry.ExpirePendingAsync(order.PaymentExpiresAt.Value);
        Assert.Equal(OrderStatuses.Cancelled, await db.Orders.Where(o => o.Id == order.Id).Select(o => o.Status).SingleAsync());
        Assert.Equal(2, await db.Products.Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync());
    }

    [Fact]
    public async Task GHN_duplicate_and_out_of_order_callbacks_do_not_regress_delivery_or_repeat_email()
    {
        var (productId, users) = await SeedCartsAsync(2, 1);
        await using var scope = fixture.Services.CreateAsyncScope();
        var service = CreateService(scope.ServiceProvider);
        var order = await service.CreateOrderAsync(users[0], Checkout(productId), "test");
        Assert.True(await service.UpdateStatusAsync(order.Id, OrderStatuses.Confirmed));
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var email = new Mock<IOrderEmailService>();
        var ghn = Ghn(db, email.Object);
        var code = $"test-{order.Id}";
        foreach (var status in new[] { "transporting", "ready_to_pick", "delivered", "delivered", "transporting", "cancel" })
            await ghn.ApplyWebhookAsync(new GhnWebhookPayload { ClientOrderCode = $"DH{order.Id}", OrderCode = code, Status = status });
        db.ChangeTracker.Clear();
        var saved = await db.Orders.Include(o => o.ShippingInfo).SingleAsync(o => o.Id == order.Id);
        Assert.Equal(OrderStatuses.Delivered, saved.Status);
        Assert.Equal(ShippingStatuses.Delivered, saved.ShippingInfo!.Status);
        email.Verify(e => e.SendOrderStatusChangedAsync(order.Id, OrderStatuses.Delivered), Times.Once());
        Assert.False(await new ShippingService(db, email.Object, Mock.Of<IUserNotificationService>())
            .UpdateStatusAsync(order.Id, ShippingStatuses.InTransit));
    }

    [Fact]
    public async Task Concurrent_manual_refund_confirmation_records_one_completion()
    {
        var (productId, users) = await SeedCartsAsync(2, 1);
        Order order;
        await using (var setup = fixture.Services.CreateAsyncScope())
        {
            var model = Checkout(productId); model.PaymentMethod = "VNPAY";
            var service = CreateService(setup.ServiceProvider);
            order = await service.CreateOrderAsync(users[0], model, "test");
            await service.CancelUserOrderAsync(order.Id, users[0], "Test");
            await CreatePaymentController(setup.ServiceProvider, order, new Mock<IOrderEmailService>()).VnpayIpn();
        }
        async Task<bool> CompleteAsync()
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            return await CreateService(scope.ServiceProvider).MarkManualRefundCompletedAsync(order.Id, "Refund test");
        }
        Assert.Single(await Task.WhenAll(CompleteAsync(), CompleteAsync()), completed => completed);
        await using var verify = fixture.Services.CreateAsyncScope();
        var saved = await verify.ServiceProvider.GetRequiredService<AppDbContext>().Orders.SingleAsync(o => o.Id == order.Id);
        Assert.True(saved.IsPaid);
        Assert.Equal(RefundStatuses.Refunded, saved.RefundStatus);
        Assert.NotNull(saved.RefundedAt);
    }

    [Fact]
    public async Task Shipping_assignment_and_admin_cancellation_cannot_both_win()
    {
        var (productId, users) = await SeedCartsAsync(2, 1);
        int orderId;
        await using (var setup = fixture.Services.CreateAsyncScope())
        {
            var service = CreateService(setup.ServiceProvider);
            orderId = (await service.CreateOrderAsync(users[0], Checkout(productId), "test")).Id;
            await service.UpdateStatusAsync(orderId, OrderStatuses.Confirmed);
        }
        await using var assigning = fixture.Services.CreateAsyncScope();
        await using var cancelling = fixture.Services.CreateAsyncScope();
        var shipping = new ShippingService(assigning.ServiceProvider.GetRequiredService<AppDbContext>(),
            Mock.Of<IOrderEmailService>(), Mock.Of<IUserNotificationService>());
        var assign = shipping.AssignAsync(orderId, "Carrier", "tracking", null);
        var cancel = CreateService(cancelling.ServiceProvider).UpdateStatusAsync(orderId, OrderStatuses.Cancelled);
        Assert.Single(await Task.WhenAll(assign, cancel), succeeded => succeeded);
        var cancelled = await cancel;
        await using var verify = fixture.Services.CreateAsyncScope();
        var db = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(cancelled ? OrderStatuses.Cancelled : OrderStatuses.Shipping,
            await db.Orders.Where(o => o.Id == orderId).Select(o => o.Status).SingleAsync());
        Assert.Equal(cancelled ? 2 : 1, await db.Products.Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync());
    }

    private static GhnShippingService Ghn(AppDbContext db, IOrderEmailService? email = null) => new(
        new HttpClient(), Options.Create(new GhnOptions()), db, email ?? Mock.Of<IOrderEmailService>(),
        Mock.Of<IUserNotificationService>(), NullLogger<GhnShippingService>.Instance);
}
