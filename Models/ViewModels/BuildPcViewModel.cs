using EcommerceApp.Models;

namespace EcommerceApp.Models.ViewModels;

public class BuildPcViewModel
{
    public Dictionary<string, List<Product>> SlotProducts { get; set; } = new();
    public IReadOnlyList<SmartBuildGoalViewModel> Goals { get; set; } = Array.Empty<SmartBuildGoalViewModel>();
}

public class SmartBuildGoalViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "sparkles";
    public int SuggestedBudget { get; set; }
}

public class SmartBuildRequest
{
    public string Goal { get; set; } = "gaming";
    public decimal Budget { get; set; } = 20_000_000m;
    public string? Note { get; set; }
}

public class SmartBuildResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;
    public string GoalLabel { get; set; } = string.Empty;
    public string VariantKey { get; set; } = "balanced";
    public string VariantLabel { get; set; } = "Cân bằng";
    public string VariantDescription { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public decimal Total { get; set; }
    public decimal Remaining => Budget - Total;
    public int PerformanceScore { get; set; }
    public int BalanceScore { get; set; }
    public int UpgradeScore { get; set; }
    public int ValueScore { get; set; }
    public int EstimatedWattage { get; set; }
    public int RecommendedPsuWattage { get; set; }
    public List<SmartBuildSlotViewModel> Slots { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Insights { get; set; } = new();
    public List<SmartBuildCompatibilityCheckViewModel> CompatibilityChecks { get; set; } = new();
    public List<SmartBuildVariantViewModel> Variants { get; set; } = new();
}

public class SmartBuildVariantViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Badge { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public decimal Total { get; set; }
    public decimal Remaining => Budget - Total;
    public int PerformanceScore { get; set; }
    public int BalanceScore { get; set; }
    public int UpgradeScore { get; set; }
    public int ValueScore { get; set; }
    public int EstimatedWattage { get; set; }
    public int RecommendedPsuWattage { get; set; }
    public List<SmartBuildSlotViewModel> Slots { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Insights { get; set; } = new();
    public List<SmartBuildCompatibilityCheckViewModel> CompatibilityChecks { get; set; } = new();
}

public class SmartBuildCompatibilityCheckViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Severity { get; set; } = "ok";
    public string Message { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Icon { get; set; } = "circle-check";
}

public class SmartBuildSlotViewModel
{
    public string Slot { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal PriceRaw { get; set; }
    public string Price { get; set; } = string.Empty;
    public int Stock { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Meta { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
