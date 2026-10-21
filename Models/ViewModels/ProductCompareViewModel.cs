using EcommerceApp.Services;

namespace EcommerceApp.Models.ViewModels;

public class ProductCompareViewModel
{
    public IReadOnlyList<ProductCompareItemViewModel> Products { get; set; } = Array.Empty<ProductCompareItemViewModel>();
    public IReadOnlyList<string> SpecLabels { get; set; } = Array.Empty<string>();
    public string? CategoryName { get; set; }
    public bool HasRejectedProducts { get; set; }
    public IReadOnlyList<ComponentComparisonNeed> Needs { get; set; } = Array.Empty<ComponentComparisonNeed>();
    public ComponentComparisonResult? Advice { get; set; }
}

public class ProductCompareItemViewModel
{
    public Product Product { get; set; } = new();
    public IReadOnlyDictionary<string, string> Specs { get; set; } = new Dictionary<string, string>();
}
