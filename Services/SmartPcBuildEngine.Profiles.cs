using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EcommerceApp.Services;

public static partial class SmartPcBuildEngine
{
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

    internal static string GetSlotLabel(string slot) => slot switch
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
