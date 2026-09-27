using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using EcommerceApp.Services;

namespace EcommerceApp.Tests;

public sealed class SmartPcBuildTests
{
    [Fact]
    public void Missing_socket_data_is_unknown_even_when_name_contains_Intel()
    {
        var inventory = Inventory();
        inventory[0].Name = "Intel CPU";
        inventory[0].Socket = null;
        var response = Build(inventory);
        Assert.True(response.Success);
        Assert.Equal("warning", response.CompatibilityChecks.Single(c => c.Key == "socket").Severity);
    }

    [Fact]
    public void Structured_socket_data_overrides_misleading_product_names()
    {
        var inventory = Inventory();
        inventory[0].Name = "Intel CPU";
        inventory[0].Socket = CpuSocket.Am5;
        inventory[1].Socket = CpuSocket.Am5;
        Assert.Equal("ok", Build(inventory).CompatibilityChecks.Single(c => c.Key == "socket").Severity);
    }

    [Theory]
    [InlineData(CpuSocket.Lga1200, CpuSocket.Lga1700)]
    [InlineData(CpuSocket.Am4, CpuSocket.Am5)]
    public void Different_sockets_are_reported_as_incompatible(CpuSocket cpu, CpuSocket board)
    {
        var inventory = Inventory();
        inventory[0].Socket = cpu;
        inventory[1].Socket = board;
        Assert.Equal("error", Build(inventory).CompatibilityChecks.Single(c => c.Key == "socket").Severity);
    }

    [Fact]
    public void Different_memory_standards_are_reported_as_incompatible()
    {
        var inventory = Inventory();
        inventory[2].MemoryType = MemoryStandard.Ddr4;
        Assert.Equal("error", Build(inventory).CompatibilityChecks.Single(c => c.Key == "memory").Severity);
    }

    [Fact]
    public void Psu_capacity_comes_from_specification_instead_of_its_name()
    {
        var inventory = Inventory();
        inventory[4].Name = "PSU 2000W";
        inventory[4].PowerWatts = 100;
        Assert.Equal("error", Build(inventory).CompatibilityChecks.Single(c => c.Key == "power").Severity);
    }

    [Fact]
    public void Deleted_and_sold_out_components_are_excluded()
    {
        var inventory = Inventory();
        inventory[0].Stock = 0;
        Assert.Empty(SmartPcBuildEngine.SelectProductsForSlot(inventory, "CPU", 10));
        inventory[0].Stock = 1;
        inventory[0].IsDeleted = true;
        Assert.Empty(SmartPcBuildEngine.SelectProductsForSlot(inventory, "CPU", 10));
    }

    [Fact]
    public void Build_preserves_required_components_and_total_matches_selected_prices()
    {
        var response = Build(Inventory());
        Assert.True(response.Success);
        Assert.All(PcSlots.Required, slot => Assert.Contains(response.Slots, item => item.Slot == slot));
        Assert.Equal(response.Slots.Sum(slot => slot.PriceRaw), response.Total);
    }

    private static SmartBuildResponse Build(List<Product> inventory) =>
        SmartPcBuildEngine.Build(new SmartBuildRequest { Budget = 20_000_000, Goal = "gaming" }, inventory);

    private static List<Product> Inventory()
    {
        var slots = new[] { "CPU", "Mainboard", "RAM", "SSD", "PSU", "Case" };
        var products = slots.Select((slot, index) => new Product
        {
            Id = index + 1, Name = slot, Description = "Fixture", Price = 1_000_000, Stock = 10,
            Category = new Category { Id = index + 1, Name = slot, Slug = slot.ToLowerInvariant() }
        }).ToList();
        products[0].Socket = CpuSocket.Lga1700;
        products[1].Socket = CpuSocket.Lga1700;
        products[1].MemoryType = MemoryStandard.Ddr5;
        products[2].MemoryType = MemoryStandard.Ddr5;
        products[4].PowerWatts = 650;
        return products;
    }
}
