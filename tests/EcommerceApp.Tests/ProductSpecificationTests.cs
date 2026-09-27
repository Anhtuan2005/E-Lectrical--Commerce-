using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using EcommerceApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EcommerceApp.Tests;

[Collection("SqlServer")]
public sealed class ProductSpecificationTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Product_create_and_update_persist_component_metadata()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var category = new Category { Name = "Mainboard", Slug = Guid.NewGuid().ToString("N") };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        var service = new ProductService(db);
        var model = new ProductFormViewModel { Name = "Test mainboard", Description = "Fixture", CategoryId = category.Id,
            Price = 1_000_000, Stock = 1, Socket = CpuSocket.Lga1700, MemoryType = MemoryStandard.Ddr5, PowerWatts = 30 };
        var product = await service.CreateProductAsync(model, Array.Empty<string>());
        await using (var verifyCreate = fixture.Services.CreateAsyncScope())
        {
            var saved = await verifyCreate.ServiceProvider.GetRequiredService<AppDbContext>().Products.SingleAsync(p => p.Id == product.Id);
            Assert.Equal(CpuSocket.Lga1700, saved.Socket);
            Assert.Equal(MemoryStandard.Ddr5, saved.MemoryType);
            Assert.Equal(30, saved.PowerWatts);
        }
        model.Id = product.Id;
        model.RowVersion = Convert.ToBase64String(product.RowVersion);
        model.Socket = CpuSocket.Am4;
        model.MemoryType = MemoryStandard.Ddr4;
        model.PowerWatts = null;
        await service.UpdateProductAsync(model, Array.Empty<string>());
        await using var verifyUpdate = fixture.Services.CreateAsyncScope();
        var updated = await verifyUpdate.ServiceProvider.GetRequiredService<AppDbContext>().Products.SingleAsync(p => p.Id == product.Id);
        Assert.Equal(CpuSocket.Am4, updated.Socket);
        Assert.Equal(MemoryStandard.Ddr4, updated.MemoryType);
        Assert.Null(updated.PowerWatts);
    }
}
