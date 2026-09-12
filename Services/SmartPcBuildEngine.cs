using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EcommerceApp.Services;

public static partial class SmartPcBuildEngine
{
    private const decimal MinSmartBudget = 12_000_000m;
    private const decimal MaxSmartBudget = 120_000_000m;
    private const decimal MaxSmartBudgetOverrun = 1_000_000m;
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

    public static IReadOnlyList<SmartBuildGoalViewModel> GetGoals() => GetSmartBuildGoals();

    public static SmartBuildResponse Build(SmartBuildRequest? request, IReadOnlyList<Product> inventory)
    {
        var profile = GetSmartBuildProfile(request?.Goal);
        var requestedBudget = request?.Budget > 0 ? request.Budget : profile.SuggestedBudget;
        var budget = Math.Clamp(requestedBudget, MinSmartBudget, MaxSmartBudget);
        var noteTerms = ExtractSearchTerms(request?.Note ?? string.Empty);

        var candidatePools = new Dictionary<string, List<Product>>();
        foreach (var slot in PcSlots.All)
        {
            var target = budget * profile.Allocation.GetValueOrDefault(slot, 0.1m);
            var products = SelectProductsForSlot(inventory, slot, 24);
            candidatePools[slot] = BuildCandidatePool(slot, products, profile, target, noteTerms);
        }

        var variants = BuildSmartBuildVariants(candidatePools, profile, budget, noteTerms, request?.Note);
        if (variants.Count == 0)
        {
            return new SmartBuildResponse
            {
                Success = false,
                Message = "Chưa có đủ sản phẩm linh kiện còn hàng để dựng cấu hình tự động."
            };
        }

        var primary = variants.FirstOrDefault(variant => variant.Key == "balanced") ?? variants.First();
        var response = BuildSmartBuildResponse(primary, profile, variants, requestedBudget);
        return response;
    }

    public static List<Product> SelectProductsForSlot(IReadOnlyList<Product> inventory, string slot, int take)
    {
        if (!PcSlots.All.Contains(slot)) return new List<Product>();
        take = Math.Clamp(take, 1, 100);
        var products = inventory.Where(product => product.Stock > 0 && !product.IsDeleted);
        var keywords = GetKeywordsForSlot(slot);
        var categorySlugs = GetCategorySlugsForSlot(slot);
        var categoryMatches = products
            .Where(product => product.Category is not null && categorySlugs.Contains(product.Category.Slug, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var categoryProducts = categoryMatches
            .OrderBy(product => product.SalePrice)
            .Take(Math.Max(4, take / 2))
            .Concat(categoryMatches.OrderByDescending(product => product.IsFeatured).ThenBy(product => product.SalePrice).Take(take))
            .GroupBy(product => product.Id)
            .Select(group => group.First())
            .Take(take)
            .ToList();

        if (categoryProducts.Any())
        {
            return categoryProducts;
        }

        // Fallback keyword matching supports legacy DBs where admins have not split component categories yet.
        var keywordMatches = products
            .Where(product => keywords.Any(keyword => MatchesKeyword(product, keyword)))
            .ToList();
        return keywordMatches
            .OrderBy(product => product.SalePrice)
            .Take(Math.Max(4, take / 2))
            .Concat(keywordMatches.OrderByDescending(product => product.IsFeatured).ThenBy(product => product.SalePrice).Take(take))
            .GroupBy(product => product.Id)
            .Select(group => group.First())
            .Take(take)
            .ToList();
    }

    private static bool MatchesKeyword(Product product, string keyword)
    {
        return product.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            || (product.Category?.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static string[] GetCategorySlugsForSlot(string slot) => slot switch
    {
        "CPU" => new[] { "cpu" },
        "VGA" => new[] { "vga" },
        "RAM" => new[] { "ram" },
        "SSD" => new[] { "ssd" },
        "Mainboard" => new[] { "mainboard" },
        "PSU" => new[] { "psu" },
        "Case" => new[] { "case" },
        "Cooling" => new[] { "cooling" },
        _ => Array.Empty<string>()
    };

    // These terms cover common Vietnamese and English component naming used by Techvora admins.
    private static string[] GetKeywordsForSlot(string slot) => slot switch
    {
        "CPU" => new[] { "CPU", "Processor", "Ryzen", "Core i", "Intel", "AMD" },
        "VGA" => new[] { "VGA", "GPU", "RTX", "RX ", "GeForce", "Radeon" },
        "RAM" => new[] { "RAM", "DDR4", "DDR5", "Memory" },
        "SSD" => new[] { "SSD", "NVMe", "M.2", "SATA SSD" },
        "Mainboard" => new[] { "Mainboard", "Motherboard", "Bo mạch" },
        "PSU" => new[] { "PSU", "Nguồn", "Power Supply", "550W", "650W", "750W", "850W" },
        "Case" => new[] { "Case", "Thùng máy", "Vỏ máy" },
        "Cooling" => new[] { "Tản nhiệt", "Cooler", "AIO", "Fan" },
        _ => new[] { slot }
    };

    private static List<SmartBuildVariantViewModel> BuildSmartBuildVariants(
        IReadOnlyDictionary<string, List<Product>> candidatePools,
        SmartBuildProfile profile,
        decimal budget,
        string[] noteTerms,
        string? note)
    {
        var variants = new List<SmartBuildVariantViewModel>();

        foreach (var strategy in GetSmartBuildVariantStrategies())
        {
            var selected = FindBestBuild(candidatePools, profile, budget, noteTerms, strategy);
            if (selected.Count == 0)
            {
                continue;
            }

            variants.Add(BuildSmartBuildVariant(selected, profile, budget, note, strategy));
        }

        return variants;
    }

    private static SmartBuildResponse BuildSmartBuildResponse(SmartBuildVariantViewModel primary, SmartBuildProfile profile, List<SmartBuildVariantViewModel> variants, decimal requestedBudget)
    {
        var message = requestedBudget < MinSmartBudget
            ? $"Ngân sách tối thiểu cho Smart PC Builder là {FormatMoney(MinSmartBudget)}. Hệ thống đã tự nâng lên mức này để tránh cấu hình thiếu linh kiện bắt buộc."
            : primary.Total > primary.Budget + MaxSmartBudgetOverrun
            ? $"Ngân sách hiện chưa đủ cho bộ PC bắt buộc. Hệ thống đã bỏ linh kiện tùy chọn và giữ cấu hình tối thiểu cần {FormatMoney(primary.Total)}."
            : $"Đã dựng {variants.Count} cấu hình {profile.Label.ToLowerInvariant()} để bạn so sánh.";

        return new SmartBuildResponse
        {
            Success = true,
            Message = message,
            Goal = profile.Key,
            GoalLabel = profile.Label,
            VariantKey = primary.Key,
            VariantLabel = primary.Label,
            VariantDescription = primary.Description,
            Budget = primary.Budget,
            Total = primary.Total,
            PerformanceScore = primary.PerformanceScore,
            BalanceScore = primary.BalanceScore,
            UpgradeScore = primary.UpgradeScore,
            ValueScore = primary.ValueScore,
            EstimatedWattage = primary.EstimatedWattage,
            RecommendedPsuWattage = primary.RecommendedPsuWattage,
            Slots = primary.Slots,
            Warnings = primary.Warnings,
            Insights = primary.Insights,
            CompatibilityChecks = primary.CompatibilityChecks,
            Variants = variants
        };
    }

    private static SmartBuildVariantViewModel BuildSmartBuildVariant(Dictionary<string, Product> selected, SmartBuildProfile profile, decimal budget, string? note, SmartBuildVariantStrategy strategy)
    {
        var total = selected.Values.Sum(product => product.SalePrice);
        var estimatedWattage = EstimateBuildWattage(selected);
        var recommendedPsu = RecommendPsuWattage(estimatedWattage);
        var compatibilityChecks = BuildCompatibilityChecks(selected, profile, budget, total, estimatedWattage, recommendedPsu);
        var warnings = compatibilityChecks
            .Where(check => check.Severity is "warning" or "error")
            .Select(check => check.Message)
            .ToList();
        var insights = BuildSmartInsights(selected, profile, budget, total, estimatedWattage, recommendedPsu, note);

        insights.Insert(0, strategy.Description);

        return new SmartBuildVariantViewModel
        {
            Key = strategy.Key,
            Label = strategy.Label,
            Description = strategy.Description,
            Badge = strategy.Badge,
            Budget = budget,
            Total = total,
            PerformanceScore = CalculatePerformanceScore(selected),
            BalanceScore = CalculateBalanceScore(selected, budget, total, warnings),
            UpgradeScore = CalculateUpgradeScore(selected),
            ValueScore = CalculateValueScore(selected, budget, total),
            EstimatedWattage = estimatedWattage,
            RecommendedPsuWattage = recommendedPsu,
            Warnings = warnings,
            Insights = insights,
            CompatibilityChecks = compatibilityChecks,
            Slots = PcSlots.All
                .Where(selected.ContainsKey)
                .Select(slot => ToSmartBuildSlot(slot, selected[slot], profile))
                .ToList()
        };
    }

    private static SmartBuildSlotViewModel ToSmartBuildSlot(string slot, Product product, SmartBuildProfile profile)
    {
        return new SmartBuildSlotViewModel
        {
            Slot = slot,
            Label = GetSlotLabel(slot),
            ProductId = product.Id,
            Name = product.Name,
            Category = product.Category?.Name ?? GetSlotLabel(slot),
            ImageUrl = product.PrimaryImageUrl,
            PriceRaw = product.SalePrice,
            Price = FormatMoney(product.SalePrice),
            Stock = product.Stock,
            Url = $"/Product/Detail/{product.Id}",
            Meta = BuildSlotMeta(slot, product),
            Reason = BuildSlotReason(slot, product, profile)
        };
    }

}
