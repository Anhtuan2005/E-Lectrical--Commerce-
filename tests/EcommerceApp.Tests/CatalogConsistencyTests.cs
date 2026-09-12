using EcommerceApp.Controllers.Admin;
using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EcommerceApp.Tests;

public sealed partial class OrderPaymentTests
{
    [Fact]
    public async Task Stale_product_form_cannot_restore_sold_stock_or_overwrite_another_edit()
    {
        var (productId, users) = await SeedCartsAsync(10, 1);
        await using var admin = fixture.Services.CreateAsyncScope();
        var db = admin.ServiceProvider.GetRequiredService<AppDbContext>();
        var products = new ProductService(db);
        var product = (await products.GetProductAsync(productId))!;
        var model = new ProductFormViewModel { Id = productId, RowVersion = Convert.ToBase64String(product.RowVersion),
            Name = product.Name, Description = "Edited description", Price = product.Price, Stock = 10, CategoryId = product.CategoryId };
        await using (var buyer = fixture.Services.CreateAsyncScope())
            await CreateService(buyer.ServiceProvider).CreateOrderAsync(users[0], Checkout(productId), "test");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => products.UpdateProductAsync(model, Array.Empty<string>()));
        db.ChangeTracker.Clear();
        var current = (await products.GetProductAsync(productId))!;
        Assert.Equal(9, current.Stock);
        Assert.NotEqual(model.Description, current.Description);
        model.RowVersion = Convert.ToBase64String(current.RowVersion);
        // Even a crafted form with an up-to-date version cannot set an absolute stock count.
        await products.UpdateProductAsync(model, Array.Empty<string>());
        model.Description = "A second admin's stale edit";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => products.UpdateProductAsync(model, Array.Empty<string>()));
        db.ChangeTracker.Clear();
        current = (await products.GetProductAsync(productId))!;
        Assert.Equal(9, current.Stock);
        Assert.Equal("Edited description", current.Description);
    }

    [Fact]
    public async Task Stock_adjustment_and_checkout_both_apply_and_log_failure_rolls_back_adjustment()
    {
        var (productId, users) = await SeedCartsAsync(2, 1);
        await using var admin = fixture.Services.CreateAsyncScope();
        await using var buyer = fixture.Services.CreateAsyncScope();
        var db = admin.ServiceProvider.GetRequiredService<AppDbContext>();
        var products = new ProductService(db);
        var adjust = products.AdjustStockAsync(productId, 4, "Nhập hàng", users[0]);
        var checkout = CreateService(buyer.ServiceProvider).CreateOrderAsync(users[0], Checkout(productId), "test");
        await Task.WhenAll(adjust, checkout);
        Assert.True(await adjust);
        Assert.Equal(5, await db.Products.Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync());
        Assert.False(await products.AdjustStockAsync(productId, -6, "Xuất quá tồn", users[0]));
        Assert.False(await products.AdjustStockAsync(productId, int.MaxValue, "Overflow", users[0]));
        await Assert.ThrowsAsync<DbUpdateException>(() => products.AdjustStockAsync(productId, 2, "Rollback", "missing-user"));
        Assert.Equal(5, await db.Products.Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync());
        var log = Assert.Single(await db.StockLogs.AsNoTracking().Where(l => l.ProductId == productId).ToListAsync());
        Assert.Equal(4, log.ChangeAmount);
        Assert.Equal(users[0], log.ChangedByUserId);
    }

    [Theory]
    [InlineData(2000000, 50, 1000000)]
    [InlineData(201, 50, 101)]
    [InlineData(123, 0, 123)]
    public async Task Price_filters_use_displayed_sale_price_including_rounding(decimal price, int discount, decimal expected)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = new Product { Name = "Discount boundary", Description = "Test", Price = price, DiscountPercent = discount,
            Category = new Category { Name = "Sale", Slug = Guid.NewGuid().ToString("N") }, Stock = 1 };
        db.Products.Add(product); await db.SaveChangesAsync();
        var service = new ProductService(db);
        Assert.Equal(expected, product.SalePrice);
        var result = await service.GetPagedProductsAsync(null, product.CategoryId, expected, expected, "price_asc", 1, 12);
        Assert.Equal(product.Id, Assert.Single(result.Products).Id);
        Assert.Empty((await service.GetPagedProductsAsync(null, product.CategoryId, null, expected - 1, null, 1, 12)).Products);
        Assert.Empty((await service.GetPagedProductsAsync(null, product.CategoryId, expected + 1, null, null, 1, 12)).Products);
    }

    [Fact]
    public async Task Dashboard_and_report_exclude_cancelled_pending_refund_and_refunded_orders()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = CreateService(scope.ServiceProvider);
        var before = await service.GetDashboardAsync();
        var reportBefore = Assert.IsType<AdminReportViewModel>(Assert.IsType<ViewResult>(await new AdminReportController(db).Index()).Model);
        var user = await SqlServerFixture.CreateUserAsync(scope.ServiceProvider);
        var name = Guid.NewGuid().ToString("N");
        var product = new Product { Name = name, Description = "Revenue test", Price = 1_000_000,
            Category = new Category { Name = name, Slug = name } };
        foreach (var (status, paid, refund) in new[]
        {
            (OrderStatuses.Delivered, false, RefundStatuses.NotRequired),
            (OrderStatuses.Confirmed, true, RefundStatuses.NotRequired),
            (OrderStatuses.Cancelled, true, RefundStatuses.NotRequired),
            (OrderStatuses.Cancelled, true, RefundStatuses.Refunded),
            (OrderStatuses.Shipping, true, RefundStatuses.PendingManual),
            (OrderStatuses.Delivered, true, RefundStatuses.Refunded)
        })
            db.Orders.Add(new Order { UserId = user.Id, Status = status, IsPaid = paid, RefundStatus = refund,
                RecipientName = "Test", RecipientPhone = "0900000000", ShippingAddress = "Test", TotalAmount = 1_000_000,
                Items = new List<OrderItem> { new() { Product = product, Quantity = 1, UnitPrice = 1_000_000 } } });
        await db.SaveChangesAsync();
        var dashboard = await service.GetDashboardAsync();
        var report = Assert.IsType<AdminReportViewModel>(Assert.IsType<ViewResult>(await new AdminReportController(db).Index()).Model);
        Assert.Equal(2_000_000, dashboard.TodayRevenue - before.TodayRevenue);
        Assert.Equal(2_000_000, report.CurrentPeriodRevenue - reportBefore.CurrentPeriodRevenue);
        Assert.Equal(2_000_000, report.RevenueByCategory[name]);
        Assert.Equal(2, Assert.Single(report.TopProducts, p => p.ProductName == name).QuantitySold);
        Assert.Equal(2, Assert.Single(dashboard.TopProducts, p => p.ProductName == name).QuantitySold);
    }
}
