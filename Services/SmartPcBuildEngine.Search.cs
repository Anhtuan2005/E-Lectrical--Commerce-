using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EcommerceApp.Services;

public static partial class SmartPcBuildEngine
{
    private static Dictionary<string, Product> FindBestBuild(
        IReadOnlyDictionary<string, List<Product>> candidatePools,
        SmartBuildProfile profile,
        decimal budget,
        string[] noteTerms,
        SmartBuildVariantStrategy strategy)
    {
        var requiredSlots = PcSlots.Required
            .Where(slot => candidatePools.TryGetValue(slot, out var products) && products.Count > 0)
            .ToArray();
        if (requiredSlots.Length == 0)
        {
            return new Dictionary<string, Product>();
        }

        var optionalSlots = PcSlots.Optional
            .Where(slot => candidatePools.TryGetValue(slot, out var products) && products.Count > 0)
            .ToArray();
        var allowedTotal = budget + MaxSmartBudgetOverrun;
        var slotSets = BuildSmartBuildSlotSets(requiredSlots, optionalSlots);

        var current = new Dictionary<string, Product>();
        var best = new Dictionary<string, Product>();
        var bestScore = decimal.MinValue;
        var fallback = new Dictionary<string, Product>();
        var fallbackOverrun = decimal.MaxValue;
        var fallbackScore = decimal.MinValue;

        foreach (var slots in slotSets)
        {
            var requiredOnly = !slots.Any(slot => PcSlots.Optional.Contains(slot));

            void Search(int index, decimal runningTotal)
            {
                if (index >= slots.Length)
                {
                    var total = current.Values.Sum(product => product.SalePrice);
                    var score = ScoreBuildCombination(current, profile, budget, noteTerms, strategy);

                    if (total <= allowedTotal)
                    {
                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = current.ToDictionary(item => item.Key, item => item.Value);
                        }
                    }
                    else if (requiredOnly)
                    {
                        var overrun = total - allowedTotal;
                        if (overrun < fallbackOverrun || (overrun == fallbackOverrun && score > fallbackScore))
                        {
                            fallbackOverrun = overrun;
                            fallbackScore = score;
                            fallback = current.ToDictionary(item => item.Key, item => item.Value);
                        }
                    }

                    return;
                }

                var slot = slots[index];
                foreach (var product in candidatePools[slot])
                {
                    var nextTotal = runningTotal + product.SalePrice;
                    if (!requiredOnly && nextTotal > allowedTotal)
                    {
                        continue;
                    }

                    current[slot] = product;
                    Search(index + 1, nextTotal);
                }

                current.Remove(slot);
            }

            Search(0, 0m);
        }

        return best.Count > 0 ? best : fallback;
    }

    private static List<string[]> BuildSmartBuildSlotSets(string[] requiredSlots, string[] optionalSlots)
    {
        var sets = new List<string[]>();
        var optionalSetCount = 1 << optionalSlots.Length;

        for (var mask = 0; mask < optionalSetCount; mask++)
        {
            var selectedSlots = new HashSet<string>(requiredSlots, StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < optionalSlots.Length; index++)
            {
                if ((mask & (1 << index)) != 0)
                {
                    selectedSlots.Add(optionalSlots[index]);
                }
            }

            sets.Add(PcSlots.All.Where(selectedSlots.Contains).ToArray());
        }

        return sets;
    }

    private static decimal ScoreBuildCombination(Dictionary<string, Product> build, SmartBuildProfile profile, decimal budget, string[] noteTerms, SmartBuildVariantStrategy strategy)
    {
        var total = build.Values.Sum(product => product.SalePrice);
        var score = build.Count * 24m;

        foreach (var (slot, product) in build)
        {
            var target = budget * profile.Allocation.GetValueOrDefault(slot, 0.1m);
            score += ScoreProductForGoal(slot, product, profile, target, noteTerms);
        }

        score += ScoreBudgetFit(total, budget, strategy);
        score += GetBuildPerformanceTier(build) * strategy.PerformanceWeight;
        score += GetBuildUpgradeTier(build) * strategy.UpgradeWeight;
        score += GetBuildValueTier(build, budget, total) * strategy.ValueWeight;

        score += ScoreCompatibility(build);
        score += ScorePowerHeadroom(build);
        return score;
    }

    private static decimal ScoreBudgetFit(decimal total, decimal budget, SmartBuildVariantStrategy strategy)
    {
        if (budget <= 0)
        {
            return 0m;
        }

        var targetTotal = budget * strategy.TargetBudgetRatio;
        var distance = Math.Abs(total - targetTotal) / budget;
        var score = 150m - Math.Min(220m, distance * strategy.BudgetDistancePenalty);

        if (total > budget)
        {
            score -= Math.Min(280m, ((total - budget) / budget) * strategy.OverBudgetPenalty);
        }

        if (strategy.Key == "value" && total <= budget)
        {
            score += Math.Min(36m, ((budget - total) / budget) * 80m);
        }

        if (strategy.Key == "performance" && total < budget * 0.82m)
        {
            score -= 46m;
        }

        return score;
    }

    private static decimal GetBuildPerformanceTier(Dictionary<string, Product> build)
    {
        var cpu = build.TryGetValue("CPU", out var cpuProduct) ? GetComponentTier("CPU", cpuProduct) : 0;
        var gpu = build.TryGetValue("VGA", out var gpuProduct) ? GetComponentTier("VGA", gpuProduct) : 0;
        var ram = build.TryGetValue("RAM", out var ramProduct) ? GetComponentTier("RAM", ramProduct) : 0;
        var ssd = build.TryGetValue("SSD", out var ssdProduct) ? GetComponentTier("SSD", ssdProduct) : 0;
        return cpu * 2.2m + gpu * 4.4m + ram * 1.7m + ssd * 1.2m;
    }

    private static decimal GetBuildUpgradeTier(Dictionary<string, Product> build)
    {
        var mainboard = build.TryGetValue("Mainboard", out var board) ? GetComponentTier("Mainboard", board) : 0;
        var psu = build.TryGetValue("PSU", out var power) ? GetComponentTier("PSU", power) : 0;
        var pcCase = build.TryGetValue("Case", out var caseProduct) ? GetComponentTier("Case", caseProduct) : 0;
        var cooling = build.TryGetValue("Cooling", out var cooler) ? GetComponentTier("Cooling", cooler) : 0;
        return mainboard * 1.8m + psu * 1.7m + pcCase * 1.2m + cooling;
    }

    private static decimal GetBuildValueTier(Dictionary<string, Product> build, decimal budget, decimal total)
    {
        var discount = build.Values.Sum(product => product.DiscountPercent);
        var featured = build.Values.Count(product => product.IsFeatured) * 3m;
        var budgetFit = budget > 0 && total <= budget ? 18m : 0m;
        return discount * 0.7m + featured + budgetFit;
    }

    private static List<Product> BuildCandidatePool(string slot, IReadOnlyList<Product> products, SmartBuildProfile profile, decimal targetBudget, string[] noteTerms)
    {
        if (products.Count == 0)
        {
            return new List<Product>();
        }

        var scored = products
            .Select(product => new
            {
                Product = product,
                Score = ScoreProductForGoal(slot, product, profile, targetBudget, noteTerms)
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Product.SalePrice)
            .ToList();

        var picks = new List<Product>();
        AddPick(products.OrderBy(product => product.SalePrice).FirstOrDefault());
        AddPick(scored.FirstOrDefault()?.Product);
        AddPick(products.OrderByDescending(product => GetComponentTier(slot, product)).ThenBy(product => product.SalePrice).FirstOrDefault());
        AddPick(products.OrderByDescending(product => product.IsFeatured).ThenByDescending(product => product.DiscountPercent).ThenBy(product => product.SalePrice).FirstOrDefault());
        AddPick(products.OrderByDescending(product => product.SalePrice <= targetBudget * 1.18m).ThenByDescending(product => GetComponentTier(slot, product)).FirstOrDefault());

        return picks
            .GroupBy(product => product.Id)
            .Select(group => group.First())
            .Take(4)
            .ToList();

        void AddPick(Product? product)
        {
            if (product is not null)
            {
                picks.Add(product);
            }
        }
    }

    private static decimal ScoreProductForGoal(string slot, Product product, SmartBuildProfile profile, decimal targetBudget, string[] noteTerms)
    {
        var target = Math.Max(targetBudget, 1m);
        var priceDistance = Math.Abs(product.SalePrice - target) / target;
        var priceFit = 42m - Math.Min(42m, priceDistance * 42m);
        var haystack = NormalizeSearchText($"{product.Name} {product.Category?.Name} {product.Description}");
        var keywordScore = profile.Keywords
            .Select(NormalizeSearchText)
            .Where(keyword => keyword.Length > 0 && haystack.Contains(keyword, StringComparison.Ordinal))
            .Sum(_ => 7m);
        var noteScore = noteTerms
            .Where(term => haystack.Contains(term, StringComparison.Ordinal))
            .Sum(_ => 5m);

        return priceFit
            + GetComponentTier(slot, product) * 8m
            + profile.SlotPriority.GetValueOrDefault(slot, 1) * 2m
            + keywordScore
            + noteScore
            + (product.IsFeatured ? 9m : 0m)
            + product.DiscountPercent * 0.45m;
    }

    private static decimal ScoreCompatibility(Dictionary<string, Product> build)
    {
        var score = 0m;

        if (build.TryGetValue("CPU", out var cpu) && build.TryGetValue("Mainboard", out var mainboard))
        {
            var cpuPlatform = GetCpuPlatform(cpu);
            var mainboardPlatform = GetMainboardPlatform(mainboard);
            score += string.IsNullOrEmpty(cpuPlatform) || string.IsNullOrEmpty(mainboardPlatform) || cpuPlatform == mainboardPlatform ? 74m : -220m;
        }

        if (build.TryGetValue("RAM", out var ram) && build.TryGetValue("Mainboard", out var board))
        {
            var ramType = GetMemoryType(ram);
            var boardMemory = GetMemoryType(board);
            score += string.IsNullOrEmpty(ramType) || string.IsNullOrEmpty(boardMemory) || ramType == boardMemory ? 52m : -180m;
        }

        return score;
    }

    private static decimal ScorePowerHeadroom(Dictionary<string, Product> build)
    {
        if (!build.TryGetValue("PSU", out var psu))
        {
            return -40m;
        }

        var psuWattage = (psu.PowerWatts ?? 0);
        if (psuWattage <= 0)
        {
            return 0m;
        }

        var recommended = RecommendPsuWattage(EstimateBuildWattage(build));
        return psuWattage >= recommended ? 58m : -170m;
    }

}
