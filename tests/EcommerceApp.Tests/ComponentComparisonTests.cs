using System.ComponentModel.DataAnnotations;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using EcommerceApp.Services;

namespace EcommerceApp.Tests;

public class ComponentComparisonTests
{
    private static Product Part(int id, int categoryId, string slug, string name, int? capacity, string? source = "https://manufacturer.example/spec") =>
        new() { Id = id, CategoryId = categoryId, Category = new Category { Id = categoryId, Slug = slug },
            Name = name, SpecificationSourceUrl = source, GpuVramGb = capacity,
            CpuCores = capacity, MemoryCapacityGb = capacity, StorageCapacityGb = capacity,
            M2Slots = capacity, PowerWatts = capacity };

    [Theory]
    [InlineData("cpu", "cores")]
    [InlineData("vga", "vram")]
    [InlineData("ram", "ram-capacity")]
    [InlineData("ssd", "ssd-capacity")]
    [InlineData("mainboard", "m2")]
    [InlineData("psu", "watts")]
    public void Different_brands_in_same_category_use_only_verified_numeric_metric(string slug, string need)
    {
        var products = new[] { Part(1, 7, slug, "Brand A", 8), Part(2, 7, slug, "Brand B", 16) };
        var result = ComponentComparisonAdvisor.Build(products, need);
        Assert.Equal(2, result.WinnerProductId);
        Assert.DoesNotContain("camera", result.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pin", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Four_products_still_compare_and_missing_source_blocks_verdict()
    {
        var products = new[] { Part(1, 1, "vga", "A", 4), Part(2, 1, "vga", "B", 8),
            Part(3, 1, "vga", "C", 12), Part(4, 1, "vga", "D", 16) };
        Assert.Equal(4, ComponentComparisonAdvisor.Build(products, "vram").WinnerProductId);
        products[3].SpecificationSourceUrl = null;
        Assert.Null(ComponentComparisonAdvisor.Build(products, "vram").WinnerProductId);
    }

    [Fact]
    public void Different_category_and_name_only_claims_never_produce_verdict()
    {
        var cpu = Part(1, 1, "cpu", "Fast 9000", null);
        var otherCpu = Part(2, 1, "cpu", "Slow 100", null);
        Assert.Null(ComponentComparisonAdvisor.Build(new[] { cpu, otherCpu }, "cores").WinnerProductId);
        otherCpu.CategoryId = 2;
        Assert.Null(ComponentComparisonAdvisor.Build(new[] { cpu, otherCpu }, "cores").WinnerProductId);
    }

    [Fact]
    public void Ram_speed_requires_same_known_memory_standard()
    {
        var first = Part(1, 1, "ram", "A", 16);
        var second = Part(2, 1, "ram", "B", 16);
        first.MemorySpeedMt = 5600;
        second.MemorySpeedMt = 6000;
        Assert.Null(ComponentComparisonAdvisor.Build(new[] { first, second }, "ram-speed").WinnerProductId);
    }

    [Fact]
    public void Admin_specifications_require_https_source()
    {
        var model = new ProductFormViewModel { Name = "Test", Description = "Test", CategoryId = 1,
            CpuCores = 8, SpecificationSourceUrl = "http://example.test/spec" };
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), errors, true));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(model.SpecificationSourceUrl)));
        model.SpecificationSourceUrl = "https://manufacturer.example/spec";
        errors.Clear();
        Assert.True(Validator.TryValidateObject(model, new ValidationContext(model), errors, true));
    }
}
