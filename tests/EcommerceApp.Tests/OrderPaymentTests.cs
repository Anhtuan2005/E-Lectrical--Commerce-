using EcommerceApp.Controllers;
using EcommerceApp.Data;
using EcommerceApp.Hubs;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace EcommerceApp.Tests;

[Collection("SqlServer")]
public sealed class OrderPaymentTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Checkout_with_retry_enabled_commits_order_stock_and_cart_together()
    {
        var (productId, users) = await SeedCartsAsync(2, 1);
        await using var scope = fixture.Services.CreateAsyncScope();
        var order = await CreateService(scope.ServiceProvider).CreateOrderAsync(users[0], Checkout(productId), "test");
        await using var verification = fixture.Services.CreateAsyncScope();
        var db = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(OrderStatuses.Pending, order.Status);
        Assert.Equal(1, await db.Products.Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync());
        Assert.False(await db.CartItems.AnyAsync(i => i.Cart!.UserId == users[0]));
        Assert.Single(await db.Orders.Where(o => o.UserId == users[0]).ToListAsync());
    }

    [Fact]
    public async Task Two_customers_competing_for_last_item_create_exactly_one_order()
    {
        var (productId, users) = await SeedCartsAsync(1, 2);
        async Task<bool> BuyAsync(string userId)
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            try
            {
                await CreateService(scope.ServiceProvider).CreateOrderAsync(userId, Checkout(productId), "test");
                return true;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("hàng")) { return false; }
        }
        var results = await Task.WhenAll(users.Select(BuyAsync));
        Assert.Single(results, success => success);
        await using var verification = fixture.Services.CreateAsyncScope();
        var db = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await db.Products.Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync());
        Assert.Single(await db.Orders.Where(o => users.Contains(o.UserId)).ToListAsync());
    }

    [Fact]
    public async Task Concurrent_cancellation_restores_inventory_once()
    {
        var (productId, users) = await SeedCartsAsync(2, 1);
        int orderId;
        await using (var setup = fixture.Services.CreateAsyncScope())
            orderId = (await CreateService(setup.ServiceProvider).CreateOrderAsync(users[0], Checkout(productId), "test")).Id;
        async Task<bool> CancelAsync()
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            return await CreateService(scope.ServiceProvider).CancelUserOrderAsync(orderId, users[0], "Test");
        }
        Assert.Single(await Task.WhenAll(CancelAsync(), CancelAsync()), success => success);
        await using var verification = fixture.Services.CreateAsyncScope();
        var db = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await db.Products.Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync());
    }

    [Fact]
    public async Task Last_voucher_use_is_atomic_across_customers()
    {
        var (productId, users) = await SeedCartsAsync(5, 2);
        var code = Guid.NewGuid().ToString("N")[..20];
        await using (var setup = fixture.Services.CreateAsyncScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Vouchers.Add(new Voucher { Code = code, Value = 100, Type = VoucherType.FixedAmount,
                UsageLimit = 1, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(1) });
            await db.SaveChangesAsync();
        }
        async Task<bool> BuyAsync(string userId)
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var model = Checkout(productId);
            model.VoucherCode = code;
            try { await CreateService(scope.ServiceProvider).CreateOrderAsync(userId, model, "test"); return true; }
            catch (InvalidOperationException ex) when (ex.Message.Contains("giảm giá")) { return false; }
        }
        Assert.Single(await Task.WhenAll(users.Select(BuyAsync)), success => success);
        await using var verification = fixture.Services.CreateAsyncScope();
        var dbVerify = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, (await dbVerify.Vouchers.SingleAsync(v => v.Code == code)).UsedCount);
        Assert.Single(await dbVerify.VoucherUsages.Where(v => v.Voucher!.Code == code).ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_payment_callbacks_apply_once_including_payment_after_cancellation(bool cancelled)
    {
        var (productId, users) = await SeedCartsAsync(2, 1);
        Order order;
        await using (var setup = fixture.Services.CreateAsyncScope())
        {
            var model = Checkout(productId);
            model.PaymentMethod = "VNPAY";
            order = await CreateService(setup.ServiceProvider).CreateOrderAsync(users[0], model, "test");
            if (cancelled) await CreateService(setup.ServiceProvider).CancelUserOrderAsync(order.Id, users[0], "Test");
        }
        var email = new Mock<IOrderEmailService>();
        async Task CallbackAsync()
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var controller = CreatePaymentController(scope.ServiceProvider, order, email);
            var result = Assert.IsType<JsonResult>(await controller.VnpayIpn());
            Assert.Equal("00", result.Value!.GetType().GetProperty("RspCode")!.GetValue(result.Value));
        }
        await Task.WhenAll(CallbackAsync(), CallbackAsync());
        await using var verification = fixture.Services.CreateAsyncScope();
        var db = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await db.Orders.SingleAsync(o => o.Id == order.Id);
        Assert.True(saved.IsPaid);
        Assert.Equal(cancelled ? OrderStatuses.Cancelled : OrderStatuses.Pending, saved.Status);
        Assert.Equal(cancelled ? RefundStatuses.PendingManual : RefundStatuses.NotRequired, saved.RefundStatus);
        email.Verify(e => e.SendOrderStatusChangedAsync(order.Id, OrderStatuses.Pending), cancelled ? Times.Never() : Times.Once());
        email.Verify(e => e.SendRefundRequiredAsync(order.Id), cancelled ? Times.Once() : Times.Never());
    }

    [Fact]
    public async Task Admin_status_update_works_with_retry_enabled()
    {
        var (productId, users) = await SeedCartsAsync(2, 1);
        await using var scope = fixture.Services.CreateAsyncScope();
        var service = CreateService(scope.ServiceProvider);
        var order = await service.CreateOrderAsync(users[0], Checkout(productId), "test");
        Assert.True(await service.UpdateStatusAsync(order.Id, OrderStatuses.Confirmed));
        Assert.Equal(OrderStatuses.Confirmed, (await service.GetOrderAsync(order.Id))!.Status);
    }

    private async Task<(int ProductId, string[] Users)> SeedCartsAsync(int stock, int userCount)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = new Product { Name = "Test product", Description = "Fixture", Price = 1_000_000, Stock = stock,
            Category = new Category { Name = "Tests", Slug = Guid.NewGuid().ToString("N") } };
        db.Products.Add(product);
        var users = new List<string>();
        for (var i = 0; i < userCount; i++)
        {
            var user = await SqlServerFixture.CreateUserAsync(scope.ServiceProvider);
            users.Add(user.Id);
            db.Carts.Add(new Cart { UserId = user.Id, Items = new List<CartItem> { new() { Product = product, Quantity = 1 } } });
        }
        await db.SaveChangesAsync();
        return (product.Id, users.ToArray());
    }

    private static CheckoutViewModel Checkout(int productId) => new()
    {
        SelectedProductIds = new List<int> { productId }, PaymentMethod = "COD",
        RecipientName = "Test customer", RecipientPhone = "0900000000", Province = "Hồ Chí Minh", District = "Quận 1"
    };

    private static OrderService CreateService(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var crossSell = new CrossSellService(db);
        return new OrderService(db, new CartService(db, crossSell), new ShippingFeeService(),
            Mock.Of<IOrderEmailService>(), Mock.Of<ICustomerSegmentService>(), crossSell,
            Mock.Of<IUserNotificationService>(), Hub(), NullLogger<OrderService>.Instance);
    }

    private static PaymentController CreatePaymentController(IServiceProvider services, Order order, Mock<IOrderEmailService> email)
    {
        var vnpay = new Mock<IVnpayService>();
        vnpay.Setup(v => v.ProcessCallback(It.IsAny<IQueryCollection>())).Returns(new VnpayResponse
        {
            OrderId = order.Id.ToString(), Amount = order.TotalAmount, IsSuccess = true, IsSignatureValid = true,
            ResponseCode = "00", TransactionStatus = "00", TransactionId = "test-transaction"
        });
        return new PaymentController(services.GetRequiredService<AppDbContext>(), vnpay.Object, email.Object,
            Mock.Of<IUserNotificationService>(), Mock.Of<ICustomerSegmentService>(), Hub(), NullLogger<PaymentController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
    }

    private static IHubContext<AdminNotificationHub> Hub()
    {
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(Mock.Of<IClientProxy>());
        var hub = new Mock<IHubContext<AdminNotificationHub>>();
        hub.SetupGet(h => h.Clients).Returns(clients.Object);
        return hub.Object;
    }
}
