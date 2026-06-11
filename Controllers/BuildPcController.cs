using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;

namespace EcommerceApp.Controllers;

public class BuildPcController : Controller
{
    private const decimal MinSmartBudget = 12_000_000m;
    private const decimal MaxSmartBudget = 120_000_000m;
    private const decimal MaxSmartBudgetOverrun = 1_000_000m;
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");
    private readonly AppDbContext _db;
    private readonly ICartService _cartService;

    public BuildPcController(AppDbContext db, ICartService cartService)
    {
        _db = db;
        _cartService = cartService;
    }

    public async Task<IActionResult> Index()
    {
        var model = new BuildPcViewModel
        {
            SlotProducts = await LoadSlotProductsAsync(),
            Goals = GetSmartBuildGoals()
        };

        return View(model);
    }

    public IActionResult Preview3d()
    {
        TempData["Success"] = "Smart PC Builder đã thay thế preview 3D. Bạn có thể dựng cấu hình ngay tại đây.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult MeasureModels()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("ai-chat")]
    public async Task<IActionResult> SmartBuild([FromBody] SmartBuildRequest? request)
    {
        var profile = GetSmartBuildProfile(request?.Goal);
        var requestedBudget = request?.Budget > 0 ? request.Budget : profile.SuggestedBudget;
        var budget = Math.Clamp(requestedBudget, MinSmartBudget, MaxSmartBudget);
        var noteTerms = ExtractSearchTerms(request?.Note ?? string.Empty);

        var candidatePools = new Dictionary<string, List<Product>>();
        foreach (var slot in PcSlots.All)
        {
            var target = budget * profile.Allocation.GetValueOrDefault(slot, 0.1m);
            var products = await GetProductsForSlotAsync(slot, 24);
            candidatePools[slot] = BuildCandidatePool(slot, products, profile, target, noteTerms);
        }

        var variants = BuildSmartBuildVariants(candidatePools, profile, budget, noteTerms, request?.Note);
        if (variants.Count == 0)
        {
            return Json(new SmartBuildResponse
            {
                Success = false,
                Message = "Chưa có đủ sản phẩm linh kiện còn hàng để dựng cấu hình tự động."
            });
        }

        var primary = variants.FirstOrDefault(variant => variant.Key == "balanced") ?? variants.First();
        var response = BuildSmartBuildResponse(primary, profile, variants, requestedBudget);
        return Json(response);
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts(string slot)
    {
        var products = await GetProductsForSlotAsync(slot, 20);

        return Json(products.Select(product => new
        {
            id = product.Id,
            name = product.Name,
            price = product.SalePrice.ToString("N0") + " â‚«",
            priceRaw = (long)product.SalePrice,
            imageUrl = product.PrimaryImageUrl,
            category = product.Category?.Name,
            stock = product.Stock
        }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart([FromForm] int[] productIds)
    {
        if (User.IsInRole("Admin"))
        {
            return Json(new
            {
                success = false,
                message = "Tài khoản admin chỉ được xem và kiểm tra, không thể mua cấu hình.",
                itemCount = 0
            });
        }

        var cleanIds = productIds.Where(id => id > 0).Distinct().ToArray();
        if (cleanIds.Length == 0)
        {
            return Json(new { success = false, message = "Vui lÃ²ng chá»n Ã­t nháº¥t má»™t linh kiá»‡n." });
        }

        var validIds = await _db.Products
            .Where(product => cleanIds.Contains(product.Id) && product.Stock > 0)
            .Select(product => product.Id)
            .ToListAsync();

        var addedCount = 0;
        foreach (var productId in validIds)
        {
            try
            {
                await _cartService.AddAsync(productId, 1, User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.Session.Id);
                addedCount++;
            }
            catch (InvalidOperationException)
            {
                // Stock may have changed after the slot list was rendered.
            }
        }

        var cartCount = await _cartService.GetCountAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.Session.Id);
        return Json(new
        {
            success = addedCount > 0,
            message = addedCount > 0 ? $"ÄÃ£ thÃªm {addedCount} linh kiá»‡n vÃ o giá» hÃ ng." : "CÃ¡c linh kiá»‡n Ä‘Ã£ chá»n hiá»‡n khÃ´ng cÃ²n hÃ ng.",
            itemCount = cartCount,
            redirectUrl = Url.Action("Index", "Cart")
        });
    }

    private async Task<Dictionary<string, List<Product>>> LoadSlotProductsAsync()
    {
        var result = new Dictionary<string, List<Product>>();
        foreach (var slot in PcSlots.All)
        {
            result[slot] = await GetProductsForSlotAsync(slot, 10);
        }

        return result;
    }

    private async Task<List<Product>> GetProductsForSlotAsync(string slot, int take)
    {
        var keywords = GetKeywordsForSlot(slot);
        var categorySlugs = GetCategorySlugsForSlot(slot);
        var products = await _db.Products
            .Include(product => product.Category)
            .Include(product => product.Images)
            .Where(product => product.Stock > 0)
            .ToListAsync();

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
        "Mainboard" => new[] { "Mainboard", "Motherboard", "Bo máº¡ch" },
        "PSU" => new[] { "PSU", "Nguá»“n", "Power Supply", "550W", "650W", "750W", "850W" },
        "Case" => new[] { "Case", "ThÃ¹ng mÃ¡y", "Vá» mÃ¡y" },
        "Cooling" => new[] { "Táº£n nhiá»‡t", "Cooler", "AIO", "Fan" },
        _ => new[] { slot }
    };

    private List<SmartBuildVariantViewModel> BuildSmartBuildVariants(
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

    private SmartBuildResponse BuildSmartBuildResponse(SmartBuildVariantViewModel primary, SmartBuildProfile profile, List<SmartBuildVariantViewModel> variants, decimal requestedBudget)
    {
        var message = requestedBudget < MinSmartBudget
            ? $"Ngân sách tối thiểu cho Smart PC Builder là {FormatMoney(MinSmartBudget)}. AI đã tự nâng lên mức này để tránh cấu hình thiếu linh kiện bắt buộc."
            : primary.Total > primary.Budget + MaxSmartBudgetOverrun
            ? $"Ngân sách hiện chưa đủ cho bộ PC bắt buộc. AI đã bỏ linh kiện tùy chọn và giữ cấu hình tối thiểu cần {FormatMoney(primary.Total)}."
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

    private SmartBuildVariantViewModel BuildSmartBuildVariant(Dictionary<string, Product> selected, SmartBuildProfile profile, decimal budget, string? note, SmartBuildVariantStrategy strategy)
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

    private SmartBuildSlotViewModel ToSmartBuildSlot(string slot, Product product, SmartBuildProfile profile)
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
            Url = Url.Action("Detail", "Product", new { id = product.Id }) ?? $"/Product/Detail/{product.Id}",
            Meta = BuildSlotMeta(slot, product),
            Reason = BuildSlotReason(slot, product, profile)
        };
    }

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

        var psuWattage = ExtractWattage(psu.Name);
        if (psuWattage <= 0)
        {
            return 0m;
        }

        var recommended = RecommendPsuWattage(EstimateBuildWattage(build));
        return psuWattage >= recommended ? 58m : -170m;
    }

    private static List<SmartBuildCompatibilityCheckViewModel> BuildCompatibilityChecks(
        Dictionary<string, Product> selected,
        SmartBuildProfile profile,
        decimal budget,
        decimal total,
        int estimatedWattage,
        int recommendedPsu)
    {
        var checks = new List<SmartBuildCompatibilityCheckViewModel>();

        if (total <= budget)
        {
            checks.Add(MakeCompatibilityCheck("budget", "Ngân sách", "ok", $"Nằm trong ngân sách, còn dư {FormatMoney(budget - total)}.", $"Tổng build {FormatMoney(total)} / ngân sách {FormatMoney(budget)}.", "wallet-cards"));
        }
        else if (total <= budget + MaxSmartBudgetOverrun)
        {
            checks.Add(MakeCompatibilityCheck("budget", "Ngân sách", "warning", $"Vượt ngân sách {FormatMoney(total - budget)} trong giới hạn cho phép.", $"AI chỉ cho vượt tối đa {FormatMoney(MaxSmartBudgetOverrun)} trước khi bỏ linh kiện tùy chọn.", "badge-alert"));
        }
        else
        {
            checks.Add(MakeCompatibilityCheck("budget", "Ngân sách", "error", $"Ngân sách chưa đủ cho bộ linh kiện bắt buộc, vẫn vượt {FormatMoney(total - budget)}.", $"AI đã bỏ linh kiện tùy chọn; mức tối thiểu hiện cần {FormatMoney(total)}.", "octagon-alert"));
        }

        var missingRequiredSlots = PcSlots.Required
            .Where(slot => !selected.ContainsKey(slot))
            .Select(GetSlotLabel)
            .ToList();
        if (missingRequiredSlots.Count > 0)
        {
            checks.Add(MakeCompatibilityCheck("required-slots", "Linh kiện bắt buộc", "error", "Thiếu linh kiện bắt buộc: " + string.Join(", ", missingRequiredSlots) + ".", "Cần đủ CPU, mainboard, RAM, SSD, PSU và case trước khi đặt hàng.", "octagon-alert"));
        }

        var omittedOptionalSlots = PcSlots.Optional
            .Where(slot => !selected.ContainsKey(slot))
            .Select(GetSlotLabel)
            .ToList();
        if (omittedOptionalSlots.Count > 0)
        {
            var severity = !selected.ContainsKey("VGA") && (profile.Key is "gaming" or "streaming") ? "warning" : "info";
            checks.Add(MakeCompatibilityCheck("optional-trim", "Tối ưu ngân sách", severity, "Đã tạm bỏ " + string.Join(", ", omittedOptionalSlots) + " để giữ ngân sách.", "Có thể thêm các món này sau; nếu CPU không có iGPU thì cần bổ sung VGA trước khi xuất hình.", "scissors"));
        }

        if (selected.TryGetValue("CPU", out var cpu) && selected.TryGetValue("Mainboard", out var mainboard))
        {
            var cpuPlatform = GetCpuPlatform(cpu);
            var boardPlatform = GetMainboardPlatform(mainboard);
            checks.Add(string.IsNullOrEmpty(cpuPlatform) || string.IsNullOrEmpty(boardPlatform)
                ? MakeCompatibilityCheck("socket", "CPU/Mainboard", "warning", "Chưa đọc chắc socket từ tên sản phẩm.", "Hãy kiểm tra socket CPU và chipset mainboard trên trang chi tiết trước khi mua.", "circle-help")
                : cpuPlatform == boardPlatform
                    ? MakeCompatibilityCheck("socket", "CPU/Mainboard", "ok", $"Khớp nền tảng {FormatPlatform(cpuPlatform)}.", $"{cpu.Name} đi cùng {mainboard.Name}.", "circle-check")
                    : MakeCompatibilityCheck("socket", "CPU/Mainboard", "error", "CPU và mainboard khác nền tảng socket.", $"{cpu.Name} là {FormatPlatform(cpuPlatform)}, mainboard là {FormatPlatform(boardPlatform)}.", "octagon-alert"));
        }
        else
        {
            checks.Add(MakeCompatibilityCheck("socket", "CPU/Mainboard", "warning", "Thiếu CPU hoặc mainboard để kiểm tra socket.", "Chọn đủ hai nhóm này để AI Advisor đối chiếu nền tảng.", "circle-help"));
        }

        if (selected.TryGetValue("RAM", out var ram) && selected.TryGetValue("Mainboard", out var board))
        {
            var ramType = GetMemoryType(ram);
            var boardMemory = GetMemoryType(board);
            checks.Add(string.IsNullOrEmpty(ramType) || string.IsNullOrEmpty(boardMemory)
                ? MakeCompatibilityCheck("memory", "RAM/Mainboard", "warning", "Chưa đọc chắc chuẩn RAM từ tên sản phẩm.", "Nên kiểm tra DDR4/DDR5 trên thông số sản phẩm.", "circle-help")
                : ramType == boardMemory
                    ? MakeCompatibilityCheck("memory", "RAM/Mainboard", "ok", $"Cùng chuẩn {ramType.ToUpperInvariant()}.", $"{ram.Name} phù hợp với {board.Name}.", "circle-check")
                    : MakeCompatibilityCheck("memory", "RAM/Mainboard", "error", "RAM và mainboard khác chuẩn DDR.", $"{ram.Name} là {ramType.ToUpperInvariant()}, mainboard là {boardMemory.ToUpperInvariant()}.", "octagon-alert"));
        }
        else
        {
            checks.Add(MakeCompatibilityCheck("memory", "RAM/Mainboard", "warning", "Thiếu RAM hoặc mainboard để kiểm tra chuẩn DDR.", "Chọn đủ hai nhóm này để tránh mua sai RAM.", "circle-help"));
        }

        if (selected.TryGetValue("PSU", out var psu))
        {
            var psuWattage = ExtractWattage(psu.Name);
            checks.Add(psuWattage <= 0
                ? MakeCompatibilityCheck("power", "Nguồn", "warning", "Chưa đọc được công suất PSU.", $"Công suất ước tính build khoảng {estimatedWattage}W, gợi ý nguồn từ {recommendedPsu}W.", "circle-help")
                : psuWattage >= recommendedPsu
                    ? MakeCompatibilityCheck("power", "Nguồn", "ok", $"Nguồn {psuWattage}W đủ dư tải.", $"Công suất ước tính {estimatedWattage}W, gợi ý tối thiểu {recommendedPsu}W.", "plug-zap")
                    : MakeCompatibilityCheck("power", "Nguồn", "error", $"Nguồn {psuWattage}W thấp hơn gợi ý {recommendedPsu}W.", $"Công suất ước tính {estimatedWattage}W, nên đổi PSU cao hơn.", "octagon-alert"));
        }
        else
        {
            checks.Add(MakeCompatibilityCheck("power", "Nguồn", "warning", "Thiếu PSU để kiểm tra công suất.", $"Công suất ước tính {estimatedWattage}W, gợi ý nguồn từ {recommendedPsu}W.", "circle-help"));
        }

        var cpuTier = selected.TryGetValue("CPU", out var selectedCpu) ? GetComponentTier("CPU", selectedCpu) : 0;
        var coolingTier = selected.TryGetValue("Cooling", out var cooler) ? GetComponentTier("Cooling", cooler) : 0;
        checks.Add(cpuTier >= 9 && coolingTier < 8
            ? MakeCompatibilityCheck("cooling", "Tản nhiệt", "warning", "CPU mạnh nên dùng tản nhiệt tốt hơn.", "Ưu tiên AIO 240mm hoặc tản khí cao cấp cho tải dài.", "thermometer-sun")
            : coolingTier > 0
                ? MakeCompatibilityCheck("cooling", "Tản nhiệt", "ok", "Tản nhiệt phù hợp mức CPU đã chọn.", "Vẫn nên kiểm tra chiều cao tản và socket hỗ trợ trong thông số.", "fan")
                : MakeCompatibilityCheck("cooling", "Tản nhiệt", "info", "Chưa có tản nhiệt riêng.", "Nếu CPU có cooler kèm thì vẫn dùng được, build hiệu năng cao nên thêm tản tốt.", "fan"));

        var gpuTier = selected.TryGetValue("VGA", out var gpu) ? GetComponentTier("VGA", gpu) : 0;
        var hasAirflowCase = selected.TryGetValue("Case", out var pcCase) && HasAirflowCase(pcCase);
        checks.Add(gpuTier >= 8 && !hasAirflowCase
            ? MakeCompatibilityCheck("airflow", "Case/Airflow", "warning", "VGA mạnh nên đi với case airflow tốt.", "Ưu tiên case mesh hoặc dòng airflow để giảm nhiệt khi chơi game/render.", "wind")
            : selected.ContainsKey("Case")
                ? MakeCompatibilityCheck("airflow", "Case/Airflow", "ok", "Case ổn cho luồng gió cơ bản.", "Kiểm tra thêm chiều dài VGA và số fan đi kèm khi chốt đơn.", "box")
                : MakeCompatibilityCheck("airflow", "Case/Airflow", "warning", "Thiếu case để kiểm tra airflow.", "Chọn case trước khi đặt hàng để tránh vướng kích thước linh kiện.", "box"));

        if (profile.Key is "creator" or "streaming")
        {
            var ramTier = selected.TryGetValue("RAM", out var selectedRam) ? GetComponentTier("RAM", selectedRam) : 0;
            checks.Add(ramTier >= 8
                ? MakeCompatibilityCheck("workload-memory", "RAM tác vụ nặng", "ok", "RAM đủ rộng cho mục tiêu đã chọn.", "Creator/livestream nên ưu tiên từ 32GB.", "memory-stick")
                : MakeCompatibilityCheck("workload-memory", "RAM tác vụ nặng", "warning", "Nên nâng RAM nếu làm việc nặng.", "Creator/livestream sẽ thoải mái hơn với 32GB hoặc 64GB.", "memory-stick"));
        }

        return checks;
    }

    private static SmartBuildCompatibilityCheckViewModel MakeCompatibilityCheck(string key, string label, string severity, string message, string detail, string icon)
    {
        return new SmartBuildCompatibilityCheckViewModel
        {
            Key = key,
            Label = label,
            Severity = severity,
            Message = message,
            Detail = detail,
            Icon = icon
        };
    }

    private static List<string> BuildSmartWarnings(Dictionary<string, Product> selected, decimal budget, decimal total, int recommendedPsu)
    {
        var warnings = new List<string>();

        if (total > budget)
        {
            warnings.Add($"Cấu hình đang vượt ngân sách {FormatMoney(total - budget)}. Bạn có thể giảm VGA, CPU hoặc case để sát ngân sách hơn.");
        }
        else if (budget - total > budget * 0.16m)
        {
            warnings.Add($"Còn dư {FormatMoney(budget - total)}. Nếu muốn nâng hiệu năng, ưu tiên VGA, RAM hoặc SSD.");
        }

        var missingSlots = PcSlots.All.Where(slot => !selected.ContainsKey(slot)).Select(GetSlotLabel).ToList();
        if (missingSlots.Count > 0)
        {
            warnings.Add("Thiếu nhóm linh kiện: " + string.Join(", ", missingSlots) + ".");
        }

        if (selected.TryGetValue("CPU", out var cpu) && selected.TryGetValue("Mainboard", out var mainboard))
        {
            var cpuPlatform = GetCpuPlatform(cpu);
            var boardPlatform = GetMainboardPlatform(mainboard);
            if (!string.IsNullOrEmpty(cpuPlatform) && !string.IsNullOrEmpty(boardPlatform) && cpuPlatform != boardPlatform)
            {
                warnings.Add("CPU và mainboard có dấu hiệu khác nền tảng socket. Hãy kiểm tra lại trước khi đặt hàng.");
            }
        }

        if (selected.TryGetValue("RAM", out var ram) && selected.TryGetValue("Mainboard", out var board))
        {
            var ramType = GetMemoryType(ram);
            var boardMemory = GetMemoryType(board);
            if (!string.IsNullOrEmpty(ramType) && !string.IsNullOrEmpty(boardMemory) && ramType != boardMemory)
            {
                warnings.Add("RAM và mainboard có dấu hiệu khác chuẩn DDR. Hãy đổi RAM hoặc mainboard tương thích.");
            }
        }

        if (selected.TryGetValue("PSU", out var psu))
        {
            var psuWattage = ExtractWattage(psu.Name);
            if (psuWattage > 0 && psuWattage < recommendedPsu)
            {
                warnings.Add($"Nguồn {psuWattage}W hơi thấp. Gợi ý tối thiểu {recommendedPsu}W cho cấu hình này.");
            }
        }

        return warnings;
    }

    private static List<string> BuildSmartInsights(
        Dictionary<string, Product> selected,
        SmartBuildProfile profile,
        decimal budget,
        decimal total,
        int estimatedWattage,
        int recommendedPsu,
        string? note)
    {
        var budgetInsight = total <= budget
            ? $"Tổng cấu hình {FormatMoney(total)}, còn dư {FormatMoney(budget - total)} so với ngân sách."
            : total <= budget + MaxSmartBudgetOverrun
                ? $"Tổng cấu hình {FormatMoney(total)}, vượt {FormatMoney(total - budget)} nhưng vẫn trong ngưỡng tối đa {FormatMoney(MaxSmartBudgetOverrun)}."
                : $"Bộ linh kiện bắt buộc tối thiểu hiện cần {FormatMoney(total)}, vượt {FormatMoney(total - budget)}; nên nâng ngân sách hoặc bổ sung thêm sản phẩm giá thấp.";

        var insights = new List<string>
        {
            $"AI Advisor ưu tiên {profile.Label.ToLowerInvariant()} và chọn {selected.Count}/8 nhóm linh kiện chính.",
            budgetInsight,
            $"Công suất ước tính khoảng {estimatedWattage}W, nên dùng nguồn từ {recommendedPsu}W để có dư tải."
        };

        var omittedOptionalSlots = PcSlots.Optional
            .Where(slot => !selected.ContainsKey(slot))
            .Select(GetSlotLabel)
            .ToList();
        if (omittedOptionalSlots.Count > 0)
        {
            insights.Add("Đã bỏ " + string.Join(", ", omittedOptionalSlots) + " vì ngân sách thấp; AI ưu tiên giữ CPU, mainboard, RAM, SSD, PSU và case trước.");
        }

        var missingRequiredSlots = PcSlots.Required
            .Where(slot => !selected.ContainsKey(slot))
            .Select(GetSlotLabel)
            .ToList();
        if (missingRequiredSlots.Count > 0)
        {
            insights.Add("Catalog đang thiếu linh kiện bắt buộc: " + string.Join(", ", missingRequiredSlots) + ".");
        }

        if (selected.TryGetValue("VGA", out var gpu) && selected.TryGetValue("CPU", out var cpu))
        {
            insights.Add($"Cặp CPU/GPU chính: {cpu.Name} + {gpu.Name}.");
        }

        if (!string.IsNullOrWhiteSpace(note))
        {
            insights.Add("Ghi chú của bạn đã được dùng để ưu tiên sản phẩm có mô tả phù hợp trong catalog.");
        }

        return insights;
    }

    private static int CalculatePerformanceScore(Dictionary<string, Product> selected)
    {
        var cpu = selected.TryGetValue("CPU", out var cpuProduct) ? GetComponentTier("CPU", cpuProduct) : 0;
        var gpu = selected.TryGetValue("VGA", out var gpuProduct) ? GetComponentTier("VGA", gpuProduct) : 0;
        var ram = selected.TryGetValue("RAM", out var ramProduct) ? GetComponentTier("RAM", ramProduct) : 0;
        var ssd = selected.TryGetValue("SSD", out var ssdProduct) ? GetComponentTier("SSD", ssdProduct) : 0;
        return ClampScore((int)Math.Round(cpu * 2.2m + gpu * 4.4m + ram * 1.7m + ssd * 1.2m));
    }

    private static int CalculateBalanceScore(Dictionary<string, Product> selected, decimal budget, decimal total, IReadOnlyList<string> warnings)
    {
        var score = 88 - warnings.Count * 9;
        if (budget > 0)
        {
            score -= (int)Math.Min(22m, Math.Abs(total - budget) / budget * 70m);
        }

        score += selected.Count == PcSlots.All.Length ? 8 : 0;
        return ClampScore(score);
    }

    private static int CalculateUpgradeScore(Dictionary<string, Product> selected)
    {
        var mainboard = selected.TryGetValue("Mainboard", out var board) ? GetComponentTier("Mainboard", board) : 0;
        var psu = selected.TryGetValue("PSU", out var power) ? GetComponentTier("PSU", power) : 0;
        var caseScore = selected.TryGetValue("Case", out var pcCase) ? GetComponentTier("Case", pcCase) : 0;
        return ClampScore(34 + mainboard * 3 + psu * 3 + caseScore * 2);
    }

    private static int CalculateValueScore(Dictionary<string, Product> selected, decimal budget, decimal total)
    {
        var discountScore = selected.Values.Sum(product => product.DiscountPercent);
        var budgetScore = total <= budget ? 44 : Math.Max(16, 44 - (int)((total - budget) / budget * 100));
        var featuredScore = selected.Values.Count(product => product.IsFeatured) * 4;
        return ClampScore(budgetScore + featuredScore + discountScore / 2);
    }

    private static int EstimateBuildWattage(Dictionary<string, Product> selected)
    {
        return selected.Sum(item => EstimatePartWattage(item.Key, item.Value));
    }

    private static int EstimatePartWattage(string slot, Product product)
    {
        var text = NormalizeSearchText($"{product.Name} {product.Description}");
        return slot switch
        {
            "CPU" when text.Contains("7800x3d", StringComparison.Ordinal) || text.Contains("ryzen 7", StringComparison.Ordinal) => 120,
            "CPU" when text.Contains("i7", StringComparison.Ordinal) || text.Contains("i9", StringComparison.Ordinal) => 145,
            "CPU" when text.Contains("i5", StringComparison.Ordinal) => 95,
            "CPU" => 75,
            "VGA" when text.Contains("4080", StringComparison.Ordinal) => 330,
            "VGA" when text.Contains("4070", StringComparison.Ordinal) => 230,
            "VGA" when text.Contains("7800", StringComparison.Ordinal) => 270,
            "VGA" when text.Contains("4060", StringComparison.Ordinal) => 130,
            "VGA" => 180,
            "RAM" => 12,
            "SSD" => 8,
            "Mainboard" => 55,
            "Cooling" => text.Contains("aio", StringComparison.Ordinal) ? 24 : 12,
            _ => 0
        };
    }

    private static int RecommendPsuWattage(int estimatedWattage)
    {
        var wattage = Math.Max(550, (int)Math.Ceiling((estimatedWattage * 1.35m + 80m) / 50m) * 50);
        return Math.Min(wattage, 1200);
    }

    private static int GetComponentTier(string slot, Product product)
    {
        var text = NormalizeSearchText($"{product.Name} {product.Description}");
        return slot switch
        {
            "CPU" when text.Contains("ryzen 9", StringComparison.Ordinal) || text.Contains("i9", StringComparison.Ordinal) => 10,
            "CPU" when text.Contains("7800x3d", StringComparison.Ordinal) || text.Contains("ryzen 7", StringComparison.Ordinal) || text.Contains("i7", StringComparison.Ordinal) => 9,
            "CPU" when text.Contains("i5", StringComparison.Ordinal) || text.Contains("ryzen 5", StringComparison.Ordinal) => 6,
            "VGA" when text.Contains("4080", StringComparison.Ordinal) || text.Contains("4090", StringComparison.Ordinal) => 10,
            "VGA" when text.Contains("4070", StringComparison.Ordinal) => 9,
            "VGA" when text.Contains("7800", StringComparison.Ordinal) => 8,
            "VGA" when text.Contains("4060", StringComparison.Ordinal) => 6,
            "RAM" when text.Contains("64gb", StringComparison.Ordinal) => 10,
            "RAM" when text.Contains("32gb", StringComparison.Ordinal) => 8,
            "RAM" when text.Contains("16gb", StringComparison.Ordinal) => 5,
            "SSD" when text.Contains("2tb", StringComparison.Ordinal) => 9,
            "SSD" when text.Contains("1tb", StringComparison.Ordinal) => 7,
            "Mainboard" when text.Contains("b650", StringComparison.Ordinal) || text.Contains("b760", StringComparison.Ordinal) => 8,
            "Mainboard" when text.Contains("b550", StringComparison.Ordinal) => 6,
            "PSU" => Math.Clamp(ExtractWattage(product.Name) / 100, 5, 10),
            "Case" when text.Contains("flow", StringComparison.Ordinal) || text.Contains("mesh", StringComparison.Ordinal) => 8,
            "Cooling" when text.Contains("aio", StringComparison.Ordinal) || text.Contains("nh d15", StringComparison.Ordinal) => 9,
            _ => 5
        };
    }

    private static string BuildSlotMeta(string slot, Product product)
    {
        return slot switch
        {
            "PSU" => ExtractWattage(product.Name) > 0 ? $"{ExtractWattage(product.Name)}W" : "Nguồn hệ thống",
            "RAM" => GetMemoryType(product).ToUpperInvariant(),
            "Mainboard" => GetMainboardPlatform(product).Replace("-", " ").ToUpperInvariant(),
            "CPU" => GetCpuPlatform(product).Replace("-", " ").ToUpperInvariant(),
            _ => product.Stock > 0 ? $"Còn {product.Stock} sản phẩm" : "Cần kiểm tra tồn kho"
        };
    }

    private static string BuildSlotReason(string slot, Product product, SmartBuildProfile profile)
    {
        return slot switch
        {
            "CPU" => $"Giữ nền hiệu năng ổn cho mục tiêu {profile.Label.ToLowerInvariant()}.",
            "VGA" => "Ưu tiên sức mạnh đồ họa trong phần ngân sách chính.",
            "RAM" => "Dung lượng và chuẩn RAM cân bằng với mainboard đã chọn.",
            "SSD" => "Tối ưu tốc độ tải game, dự án và hệ điều hành.",
            "Mainboard" => "Ghép nền tảng phù hợp với CPU và còn dư đường nâng cấp.",
            "PSU" => "Chọn công suất có khoảng dự phòng cho tải thực tế.",
            "Case" => "Ưu tiên airflow và không gian lắp linh kiện.",
            "Cooling" => "Giữ nhiệt độ CPU ổn định khi tải dài.",
            _ => $"Phù hợp với mục tiêu {profile.Label.ToLowerInvariant()}."
        };
    }

    private static int ExtractWattage(string value)
    {
        var match = Regex.Match(value, @"(\d{3,4})\s*w", RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, out var wattage) ? wattage : 0;
    }

    private static string GetCpuPlatform(Product product)
    {
        var text = NormalizeSearchText($"{product.Name} {product.Description}");
        if (text.Contains("intel", StringComparison.Ordinal) || text.Contains("core i", StringComparison.Ordinal))
        {
            return "intel-lga1700";
        }

        if (text.Contains("7800", StringComparison.Ordinal) || text.Contains("am5", StringComparison.Ordinal) || text.Contains("ryzen 7000", StringComparison.Ordinal))
        {
            return "amd-am5";
        }

        return text.Contains("ryzen", StringComparison.Ordinal) || text.Contains("am4", StringComparison.Ordinal) ? "amd-am4" : string.Empty;
    }

    private static string GetMainboardPlatform(Product product)
    {
        var text = NormalizeSearchText($"{product.Name} {product.Description}");
        if (text.Contains("b760", StringComparison.Ordinal) || text.Contains("intel", StringComparison.Ordinal))
        {
            return "intel-lga1700";
        }

        if (text.Contains("b650", StringComparison.Ordinal) || text.Contains("am5", StringComparison.Ordinal))
        {
            return "amd-am5";
        }

        return text.Contains("b550", StringComparison.Ordinal) || text.Contains("am4", StringComparison.Ordinal) ? "amd-am4" : string.Empty;
    }

    private static string GetMemoryType(Product product)
    {
        var text = NormalizeSearchText($"{product.Name} {product.Description}");
        if (text.Contains("ddr5", StringComparison.Ordinal))
        {
            return "ddr5";
        }

        return text.Contains("ddr4", StringComparison.Ordinal) ? "ddr4" : string.Empty;
    }

    private static bool HasAirflowCase(Product product)
    {
        var text = NormalizeSearchText($"{product.Name} {product.Description}");
        return text.Contains("flow", StringComparison.Ordinal)
            || text.Contains("mesh", StringComparison.Ordinal)
            || text.Contains("airflow", StringComparison.Ordinal)
            || text.Contains("lancool", StringComparison.Ordinal);
    }

    private static string FormatPlatform(string platform) => platform switch
    {
        "intel-lga1700" => "Intel LGA1700",
        "amd-am4" => "AMD AM4",
        "amd-am5" => "AMD AM5",
        _ => platform
    };

    private static IReadOnlyList<SmartBuildGoalViewModel> GetSmartBuildGoals()
    {
        return GetSmartBuildProfiles()
            .Select(profile => new SmartBuildGoalViewModel
            {
                Key = profile.Key,
                Label = profile.Label,
                Description = profile.Description,
                Icon = profile.Icon,
                SuggestedBudget = (int)profile.SuggestedBudget
            })
            .ToList();
    }

    private static SmartBuildProfile GetSmartBuildProfile(string? key)
    {
        return GetSmartBuildProfiles().FirstOrDefault(profile => profile.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            ?? GetSmartBuildProfiles().First();
    }

    private static IReadOnlyList<SmartBuildVariantStrategy> GetSmartBuildVariantStrategies()
    {
        return new List<SmartBuildVariantStrategy>
        {
            new()
            {
                Key = "value",
                Label = "Tiết kiệm",
                Badge = "Giữ ngân sách",
                Description = "Cấu hình tiết kiệm ưu tiên linh kiện đáng tiền, hạn chế vượt ngân sách.",
                TargetBudgetRatio = 0.88m,
                BudgetDistancePenalty = 260m,
                OverBudgetPenalty = 520m,
                PerformanceWeight = 1.6m,
                UpgradeWeight = 1.3m,
                ValueWeight = 4.6m
            },
            new()
            {
                Key = "balanced",
                Label = "Cân bằng",
                Badge = "Khuyên dùng",
                Description = "Cấu hình cân bằng giữ hiệu năng, độ ổn định và khả năng nâng cấp ở mức hài hòa.",
                TargetBudgetRatio = 0.98m,
                BudgetDistancePenalty = 210m,
                OverBudgetPenalty = 360m,
                PerformanceWeight = 2.6m,
                UpgradeWeight = 2.1m,
                ValueWeight = 2.5m
            },
            new()
            {
                Key = "performance",
                Label = "Hiệu năng",
                Badge = "Mạnh nhất",
                Description = "Cấu hình hiệu năng dùng nhiều ngân sách hơn cho CPU/GPU để tối đa FPS hoặc tốc độ render.",
                TargetBudgetRatio = 1.02m,
                BudgetDistancePenalty = 170m,
                OverBudgetPenalty = 430m,
                PerformanceWeight = 5.2m,
                UpgradeWeight = 2.2m,
                ValueWeight = 1.2m
            }
        };
    }

    private static IReadOnlyList<SmartBuildProfile> GetSmartBuildProfiles()
    {
        return new List<SmartBuildProfile>
        {
            new()
            {
                Key = "gaming",
                Label = "Gaming 2K",
                Description = "Ưu tiên FPS, VGA mạnh và CPU đủ kéo game mới.",
                Icon = "gamepad-2",
                SuggestedBudget = 28_000_000m,
                Keywords = new[] { "gaming", "game", "fps", "rtx", "geforce", "radeon", "x3d" },
                Allocation = new Dictionary<string, decimal>
                {
                    ["CPU"] = 0.18m, ["VGA"] = 0.38m, ["RAM"] = 0.08m, ["SSD"] = 0.09m,
                    ["Mainboard"] = 0.10m, ["PSU"] = 0.08m, ["Case"] = 0.06m, ["Cooling"] = 0.03m
                },
                SlotPriority = new Dictionary<string, int>
                {
                    ["VGA"] = 10, ["CPU"] = 8, ["RAM"] = 6, ["SSD"] = 6, ["PSU"] = 6,
                    ["Mainboard"] = 5, ["Case"] = 3, ["Cooling"] = 3
                }
            },
            new()
            {
                Key = "creator",
                Label = "Đồ họa và render",
                Description = "Cân CPU, GPU, RAM và SSD cho dự án nặng.",
                Icon = "wand-sparkles",
                SuggestedBudget = 42_000_000m,
                Keywords = new[] { "render", "creator", "workstation", "video", "64gb", "2tb", "rtx" },
                Allocation = new Dictionary<string, decimal>
                {
                    ["CPU"] = 0.22m, ["VGA"] = 0.29m, ["RAM"] = 0.14m, ["SSD"] = 0.11m,
                    ["Mainboard"] = 0.10m, ["PSU"] = 0.07m, ["Case"] = 0.04m, ["Cooling"] = 0.03m
                },
                SlotPriority = new Dictionary<string, int>
                {
                    ["CPU"] = 10, ["VGA"] = 9, ["RAM"] = 9, ["SSD"] = 8, ["PSU"] = 7,
                    ["Mainboard"] = 6, ["Cooling"] = 5, ["Case"] = 3
                }
            },
            new()
            {
                Key = "office",
                Label = "Học tập và văn phòng",
                Description = "Êm, tiết kiệm, đủ nhanh cho làm việc lâu dài.",
                Icon = "briefcase-business",
                SuggestedBudget = 22_000_000m,
                Keywords = new[] { "office", "hoc tap", "van phong", "em", "tiet kiem", "ben" },
                Allocation = new Dictionary<string, decimal>
                {
                    ["CPU"] = 0.18m, ["VGA"] = 0.24m, ["RAM"] = 0.09m, ["SSD"] = 0.10m,
                    ["Mainboard"] = 0.13m, ["PSU"] = 0.08m, ["Case"] = 0.10m, ["Cooling"] = 0.08m
                },
                SlotPriority = new Dictionary<string, int>
                {
                    ["CPU"] = 8, ["SSD"] = 8, ["RAM"] = 7, ["Mainboard"] = 6, ["PSU"] = 6,
                    ["Case"] = 5, ["Cooling"] = 5, ["VGA"] = 3
                }
            },
            new()
            {
                Key = "streaming",
                Label = "Livestream",
                Description = "Mượt khi vừa chơi vừa stream, ưu tiên GPU và RAM.",
                Icon = "radio-tower",
                SuggestedBudget = 36_000_000m,
                Keywords = new[] { "stream", "livestream", "rtx", "nvenc", "32gb", "gaming" },
                Allocation = new Dictionary<string, decimal>
                {
                    ["CPU"] = 0.20m, ["VGA"] = 0.34m, ["RAM"] = 0.10m, ["SSD"] = 0.09m,
                    ["Mainboard"] = 0.10m, ["PSU"] = 0.08m, ["Case"] = 0.05m, ["Cooling"] = 0.04m
                },
                SlotPriority = new Dictionary<string, int>
                {
                    ["VGA"] = 10, ["CPU"] = 9, ["RAM"] = 8, ["PSU"] = 7, ["SSD"] = 6,
                    ["Mainboard"] = 5, ["Cooling"] = 4, ["Case"] = 3
                }
            }
        };
    }

    private static string GetSlotLabel(string slot) => slot switch
    {
        "CPU" => "CPU",
        "VGA" => "Card đồ họa",
        "RAM" => "RAM",
        "SSD" => "Ổ cứng SSD",
        "Mainboard" => "Mainboard",
        "PSU" => "Nguồn",
        "Case" => "Vỏ case",
        "Cooling" => "Tản nhiệt",
        _ => slot
    };

    private static string[] ExtractSearchTerms(string value)
    {
        return NormalizeSearchText(value)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => term.Length >= 2)
            .Distinct()
            .Take(12)
            .ToArray();
    }

    private static string NormalizeSearchText(string value)
    {
        var normalized = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(ch switch
            {
                'đ' or 'Đ' => 'd',
                _ => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : ' '
            });
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string FormatMoney(decimal value)
    {
        return value.ToString("N0", VietnameseCulture) + " ₫";
    }

    private static int ClampScore(int score)
    {
        return Math.Clamp(score, 0, 100);
    }

    private sealed class SmartBuildProfile
    {
        public string Key { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Icon { get; init; } = "sparkles";
        public decimal SuggestedBudget { get; init; }
        public string[] Keywords { get; init; } = Array.Empty<string>();
        public IReadOnlyDictionary<string, decimal> Allocation { get; init; } = new Dictionary<string, decimal>();
        public IReadOnlyDictionary<string, int> SlotPriority { get; init; } = new Dictionary<string, int>();
    }

    private sealed class SmartBuildVariantStrategy
    {
        public string Key { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public string Badge { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public decimal TargetBudgetRatio { get; init; } = 1m;
        public decimal BudgetDistancePenalty { get; init; } = 200m;
        public decimal OverBudgetPenalty { get; init; } = 360m;
        public decimal PerformanceWeight { get; init; } = 2.5m;
        public decimal UpgradeWeight { get; init; } = 2m;
        public decimal ValueWeight { get; init; } = 2m;
    }
}

public static class PcSlots
{
    public static readonly string[] All = { "CPU", "VGA", "RAM", "SSD", "Mainboard", "PSU", "Case", "Cooling" };
    public static readonly string[] Required = { "CPU", "Mainboard", "RAM", "SSD", "PSU", "Case" };
    public static readonly string[] Optional = { "VGA", "Cooling" };
}
