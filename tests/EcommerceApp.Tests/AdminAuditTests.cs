using EcommerceApp.Controllers.Admin;
using EcommerceApp.Controllers;
using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Globalization;

namespace EcommerceApp.Tests;

[Collection("SqlServer")]
public sealed class AdminAuditTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Repeated_quick_category_creation_produces_unique_slugs()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var controller = new AdminProductController(new ProductService(db), Mock.Of<IImageStorageService>(), db)
        { TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>()) };
        var name = "Danh mục " + Guid.NewGuid().ToString("N");
        for (var i = 0; i < 4; i++)
            Assert.IsType<RedirectToActionResult>(await controller.CreateCategory(name));
        var slugs = await db.Categories.Where(category => category.Name == name).Select(category => category.Slug).ToListAsync();
        Assert.Equal(4, slugs.Distinct().Count());
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("79228162514264337593543950335")]
    public async Task Voucher_preview_rejects_amounts_outside_database_limits(string amount)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var voucher = new Voucher { Code = Guid.NewGuid().ToString("N")[..20], Type = VoucherType.Percent,
            Value = 12.5m, UsageLimit = 10, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(1) };
        db.Vouchers.Add(voucher);
        await db.SaveChangesAsync();
        var http = new DefaultHttpContext();
        http.Features.Set(Mock.Of<ISessionFeature>(feature => feature.Session == Mock.Of<ISession>()));
        var cart = new Mock<ICartService>();
        cart.Setup(c => c.GetCartAsync(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<IEnumerable<int>?>()))
            .ReturnsAsync(new CartViewModel());
        var controller = new VoucherController(db, cart.Object, Mock.Of<ICustomerSegmentService>())
        { ControllerContext = new ControllerContext { HttpContext = http } };
        Assert.IsType<BadRequestObjectResult>(await controller.Validate(voucher.Code, decimal.Parse(amount, CultureInfo.InvariantCulture)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Deleting_voucher_preserves_used_history_and_deletes_unused_vouchers(bool used)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var voucher = new Voucher { Code = Guid.NewGuid().ToString("N")[..20], Value = 10,
            UsageLimit = 10, UsedCount = used ? 1 : 0, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(1) };
        db.Vouchers.Add(voucher);
        if (used)
        {
            var user = await SqlServerFixture.CreateUserAsync(scope.ServiceProvider);
            var order = new Order { UserId = user.Id, RecipientName = "Test", RecipientPhone = "0900000000", ShippingAddress = "Test" };
            db.VoucherUsages.Add(new VoucherUsage { Voucher = voucher, UserId = user.Id, Order = order });
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var http = new DefaultHttpContext();
        var controller = new AdminVoucherController(db) { TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>()) };

        Assert.IsType<RedirectToActionResult>(await controller.Delete(voucher.Id));
        db.ChangeTracker.Clear();
        Assert.Equal(used, await db.Vouchers.AnyAsync(v => v.Id == voucher.Id));
        if (used)
        {
            Assert.Single(await db.VoucherUsages.Where(v => v.VoucherId == voucher.Id).ToListAsync());
            Assert.True(controller.TempData.ContainsKey("Error"));
        }
    }

    [Fact]
    public async Task Invalid_banner_sort_returns_bad_request_without_changing_order()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var banner = new Banner { Title = "Test", SortOrder = 7 };
        db.Banners.Add(banner);
        await db.SaveChangesAsync();
        var controller = new AdminBannerController(db, Mock.Of<IImageStorageService>());
        foreach (var ids in new List<int>?[] { null, new(), new() { -1 }, new() { banner.Id, banner.Id }, new() { int.MaxValue } })
        {
            Assert.IsType<BadRequestObjectResult>(await controller.Sort(ids!));
        }
        db.ChangeTracker.Clear();
        Assert.Equal(7, await db.Banners.Where(b => b.Id == banner.Id).Select(b => b.SortOrder).SingleAsync());
    }
}
