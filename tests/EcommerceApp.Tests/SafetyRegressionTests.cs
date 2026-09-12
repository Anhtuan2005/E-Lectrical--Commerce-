using EcommerceApp.Controllers.Admin;
using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace EcommerceApp.Tests;

public sealed partial class OrderPaymentTests
{
    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Cart_rejects_overflow_and_nonpositive_additions_without_changing_existing_quantity(int quantity)
    {
        var (productId, users) = await SeedCartsAsync(10, 1);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cart = new CartService(db, new CrossSellService(db));
        await Assert.ThrowsAsync<InvalidOperationException>(() => cart.AddAsync(productId, quantity, users[0], "test"));
        Assert.Equal(1, await db.CartItems.Where(i => i.Cart!.UserId == users[0]).Select(i => i.Quantity).SingleAsync());
        Assert.Equal(10, await db.Products.Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Checkout_rejects_invalid_quantity_from_cart_provider_before_writing_anything(int quantity)
    {
        var (productId, users) = await SeedCartsAsync(10, 1);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var snapshot = await db.Carts.AsNoTracking().Include(c => c.Items).ThenInclude(i => i.Product).SingleAsync(c => c.UserId == users[0]);
        snapshot.Items.Single().Quantity = quantity;
        var cart = new Mock<ICartService>();
        cart.Setup(c => c.GetCartEntityAsync(users[0], "test")).ReturnsAsync(snapshot);
        var crossSell = new CrossSellService(db);
        var service = new OrderService(db, cart.Object, new ShippingFeeService(), Mock.Of<IOrderEmailService>(),
            Mock.Of<ICustomerSegmentService>(), crossSell, Mock.Of<IUserNotificationService>(), Hub(), NullLogger<OrderService>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateOrderAsync(users[0], Checkout(productId), "test"));
        Assert.False(await db.Orders.AnyAsync(o => o.UserId == users[0]));
        Assert.Equal(10, await db.Products.Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync());
        Assert.Equal(1, await db.CartItems.Where(i => i.Cart!.UserId == users[0]).Select(i => i.Quantity).SingleAsync());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Both_category_routes_preserve_products_and_order_history_including_soft_deleted_products(bool alternate, bool deleted)
    {
        var (productId, users) = await SeedCartsAsync(10, 1);
        await using var scope = fixture.Services.CreateAsyncScope();
        var order = await CreateService(scope.ServiceProvider).CreateOrderAsync(users[0], Checkout(productId), "test");
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var categoryId = await db.Products.Where(p => p.Id == productId).Select(p => p.CategoryId).SingleAsync();
        if (deleted) await new ProductService(db).DeleteProductAsync(productId);
        db.ChangeTracker.Clear();
        if (alternate)
            await WithTempData(new AdminProductController(new ProductService(db), Mock.Of<IImageStorageService>(), db)).DeleteCategory(categoryId);
        else
            await WithTempData(new AdminCategoryController(db)).Delete(categoryId);
        db.ChangeTracker.Clear();
        Assert.True(await db.Categories.AnyAsync(c => c.Id == categoryId));
        Assert.True(await db.Products.IgnoreQueryFilters().AnyAsync(p => p.Id == productId));
        Assert.Single(await db.OrderItems.Where(i => i.OrderId == order.Id).ToListAsync());
    }

    [Fact]
    public async Task Refunded_or_cancelled_orders_do_not_grant_VIP_but_valid_sales_still_do()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await SqlServerFixture.CreateUserAsync(scope.ServiceProvider);
        var order = new Order { UserId = user.Id, Status = OrderStatuses.Cancelled, IsPaid = true,
            RefundStatus = RefundStatuses.Refunded, TotalAmount = 25_000_000, RecipientName = "Test", RecipientPhone = "0900000000", ShippingAddress = "Test" };
        db.Orders.Add(order); await db.SaveChangesAsync();
        var segments = new CustomerSegmentService(db);
        await segments.RefreshUserAsync(user.Id);
        Assert.False(await db.CustomerSegmentMembers.AnyAsync(m => m.UserId == user.Id && m.CustomerSegment!.Code == CustomerSegmentCodes.Vip));
        order.Status = OrderStatuses.Delivered; order.RefundStatus = RefundStatuses.NotRequired;
        await db.SaveChangesAsync(); await segments.RefreshUserAsync(user.Id);
        Assert.True(await db.CustomerSegmentMembers.AnyAsync(m => m.UserId == user.Id && m.CustomerSegment!.Code == CustomerSegmentCodes.Vip));
    }

    [Fact]
    public async Task Concurrent_return_requests_cannot_exceed_the_purchased_quantity()
    {
        var (orderId, itemId, userId) = await SeedDeliveredForSupportAsync();
        async Task<bool> SubmitAsync()
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            try { await Support(scope.ServiceProvider).CreateAsync(userId, SupportInput(orderId, itemId)); return true; }
            catch (InvalidOperationException) { return false; }
        }
        Assert.Single(await Task.WhenAll(SubmitAsync(), SubmitAsync()), accepted => accepted);
    }

    [Fact]
    public async Task Completed_return_cannot_reopen_or_return_the_same_unit_again()
    {
        var (orderId, itemId, userId) = await SeedDeliveredForSupportAsync();
        await using var scope = fixture.Services.CreateAsyncScope();
        var service = Support(scope.ServiceProvider);
        var request = await service.CreateAsync(userId, SupportInput(orderId, itemId));
        Assert.True(await service.UpdateStatusAsync(request.Id, ReturnWarrantyRequestStatuses.Reviewing, "Check"));
        Assert.True(await service.UpdateStatusAsync(request.Id, ReturnWarrantyRequestStatuses.Approved, "Approve"));
        Assert.True(await service.UpdateStatusAsync(request.Id, ReturnWarrantyRequestStatuses.Completed, "Done"));
        var completedAt = (await service.GetRequestAsync(request.Id))!.CompletedAt;
        Assert.False(await service.UpdateStatusAsync(request.Id, ReturnWarrantyRequestStatuses.Submitted, "Reopen"));
        Assert.True(await service.UpdateStatusAsync(request.Id, ReturnWarrantyRequestStatuses.Completed, "Repeat"));
        Assert.Equal(completedAt, (await service.GetRequestAsync(request.Id))!.CompletedAt);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(userId, SupportInput(orderId, itemId)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Database_rejects_nonpositive_cart_and_order_quantities(int quantity)
    {
        var (productId, users) = await SeedCartsAsync(10, 1);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE CartItems SET Quantity = {quantity} WHERE ProductId = {productId}"));
        var order = await CreateService(scope.ServiceProvider).CreateOrderAsync(users[0], Checkout(productId), "test");
        await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE OrderItems SET Quantity = {quantity} WHERE OrderId = {order.Id}"));
        Assert.Equal(1, await db.OrderItems.Where(i => i.OrderId == order.Id).Select(i => i.Quantity).SingleAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Database_blocks_cascading_deletion_even_when_controller_guards_are_bypassed(bool deleteCategory)
    {
        var (productId, users) = await SeedCartsAsync(10, 1);
        await using var scope = fixture.Services.CreateAsyncScope();
        var order = await CreateService(scope.ServiceProvider).CreateOrderAsync(users[0], Checkout(productId), "test");
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var categoryId = await db.Products.Where(p => p.Id == productId).Select(p => p.CategoryId).SingleAsync();
        if (deleteCategory)
            await Assert.ThrowsAsync<SqlException>(() => db.Categories.Where(c => c.Id == categoryId).ExecuteDeleteAsync());
        else
            await Assert.ThrowsAsync<SqlException>(() => db.Products.Where(p => p.Id == productId).ExecuteDeleteAsync());
        Assert.Single(await db.OrderItems.Where(i => i.OrderId == order.Id).ToListAsync());
        Assert.True(await db.Products.AnyAsync(p => p.Id == productId));
    }

    [Fact]
    public async Task Rejected_requests_release_capacity_and_completed_warranty_allows_future_support()
    {
        var (orderId, itemId, userId) = await SeedDeliveredForSupportAsync();
        await using var scope = fixture.Services.CreateAsyncScope();
        var service = Support(scope.ServiceProvider);
        var rejected = await service.CreateAsync(userId, SupportInput(orderId, itemId));
        Assert.Equal(0, (await service.BuildCreateModelAsync(orderId, userId))!.Items.Single().AvailableQuantity);
        Assert.True(await service.UpdateStatusAsync(rejected.Id, ReturnWarrantyRequestStatuses.Rejected, "Rejected"));
        Assert.Equal(1, (await service.BuildCreateModelAsync(orderId, userId))!.Items.Single().AvailableQuantity);
        var input = SupportInput(orderId, itemId); input.Type = ReturnWarrantyRequestTypes.Warranty;
        var warranty = await service.CreateAsync(userId, input);
        Assert.False(await service.UpdateStatusAsync(warranty.Id, ReturnWarrantyRequestStatuses.Completed, "Skip review"));
        await service.UpdateStatusAsync(warranty.Id, ReturnWarrantyRequestStatuses.Reviewing, "Review");
        await service.UpdateStatusAsync(warranty.Id, ReturnWarrantyRequestStatuses.Approved, "Approve");
        await service.UpdateStatusAsync(warranty.Id, ReturnWarrantyRequestStatuses.Completed, "Repair complete");
        Assert.Equal(1, (await service.BuildCreateModelAsync(orderId, userId))!.Items.Single().AvailableQuantity);
        Assert.NotNull(await service.CreateAsync(userId, input));
    }

    [Fact]
    public async Task Out_of_stock_quantity_update_removes_line_instead_of_saving_zero()
    {
        var (productId, users) = await SeedCartsAsync(1, 1);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Products.Where(p => p.Id == productId).ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Stock, 0));
        await new CartService(db, new CrossSellService(db)).UpdateQuantityAsync(productId, 1, users[0], "test");
        Assert.False(await db.CartItems.AnyAsync(i => i.ProductId == productId));
    }

    private async Task<(int OrderId, int ItemId, string UserId)> SeedDeliveredForSupportAsync()
    {
        var (productId, users) = await SeedCartsAsync(10, 1);
        await using var scope = fixture.Services.CreateAsyncScope();
        var service = CreateService(scope.ServiceProvider);
        var order = await service.CreateOrderAsync(users[0], Checkout(productId), "test");
        await service.UpdateStatusAsync(order.Id, OrderStatuses.Confirmed);
        await service.UpdateStatusAsync(order.Id, OrderStatuses.Shipping);
        await service.UpdateStatusAsync(order.Id, OrderStatuses.Delivered);
        return (order.Id, order.Items.Single().Id, users[0]);
    }

    private static ReturnWarrantyRequestCreateViewModel SupportInput(int orderId, int itemId) => new()
    {
        OrderId = orderId, ContactName = "Test", ContactPhone = "0900000000", Reason = "Test",
        Description = "Test support request description", Items = new() { new() { OrderItemId = itemId, Selected = true, Quantity = 1 } }
    };
    private static ReturnWarrantyRequestService Support(IServiceProvider services) => new(services.GetRequiredService<AppDbContext>(),
        Mock.Of<IImageStorageService>(), Mock.Of<IUserNotificationService>(), NullLogger<ReturnWarrantyRequestService>.Instance);
    private static T WithTempData<T>(T controller) where T : Controller
    {
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.TempData = new TempDataDictionary(controller.HttpContext, Mock.Of<ITempDataProvider>());
        return controller;
    }
}
