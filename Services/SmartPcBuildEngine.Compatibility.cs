using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EcommerceApp.Services;

public static partial class SmartPcBuildEngine
{
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
            checks.Add(MakeCompatibilityCheck("budget", "Ngân sách", "warning", $"Vượt ngân sách {FormatMoney(total - budget)} trong giới hạn cho phép.", $"Hệ thống chỉ cho vượt tối đa {FormatMoney(MaxSmartBudgetOverrun)} trước khi bỏ linh kiện tùy chọn.", "badge-alert"));
        }
        else
        {
            checks.Add(MakeCompatibilityCheck("budget", "Ngân sách", "error", $"Ngân sách chưa đủ cho bộ linh kiện bắt buộc, vẫn vượt {FormatMoney(total - budget)}.", $"Hệ thống đã bỏ linh kiện tùy chọn; mức tối thiểu hiện cần {FormatMoney(total)}.", "octagon-alert"));
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
                ? MakeCompatibilityCheck("socket", "CPU/Mainboard", "warning", "Chưa có đủ thông số socket để xác nhận tương thích.", "Hãy kiểm tra socket CPU và chipset mainboard trên trang chi tiết trước khi mua.", "circle-help")
                : cpuPlatform == boardPlatform
                    ? MakeCompatibilityCheck("socket", "CPU/Mainboard", "ok", $"Khớp nền tảng {FormatPlatform(cpuPlatform)}.", $"{cpu.Name} đi cùng {mainboard.Name}.", "circle-check")
                    : MakeCompatibilityCheck("socket", "CPU/Mainboard", "error", "CPU và mainboard khác nền tảng socket.", $"{cpu.Name} là {FormatPlatform(cpuPlatform)}, mainboard là {FormatPlatform(boardPlatform)}.", "octagon-alert"));
        }
        else
        {
            checks.Add(MakeCompatibilityCheck("socket", "CPU/Mainboard", "warning", "Thiếu CPU hoặc mainboard để kiểm tra socket.", "Chọn đủ hai nhóm này để Trợ lý cấu hình đối chiếu nền tảng.", "circle-help"));
        }

        if (selected.TryGetValue("RAM", out var ram) && selected.TryGetValue("Mainboard", out var board))
        {
            var ramType = GetMemoryType(ram);
            var boardMemory = GetMemoryType(board);
            checks.Add(string.IsNullOrEmpty(ramType) || string.IsNullOrEmpty(boardMemory)
                ? MakeCompatibilityCheck("memory", "RAM/Mainboard", "warning", "Chưa có đủ thông số chuẩn RAM để xác nhận tương thích.", "Nên kiểm tra DDR4/DDR5 trên thông số sản phẩm.", "circle-help")
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
            var psuWattage = (psu.PowerWatts ?? 0);
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
            var psuWattage = (psu.PowerWatts ?? 0);
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
            $"Trợ lý cấu hình ưu tiên {profile.Label.ToLowerInvariant()} và chọn {selected.Count}/8 nhóm linh kiện chính.",
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
        if (slot != "PSU" && product.PowerWatts.HasValue) return product.PowerWatts.Value;
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
            "PSU" => Math.Clamp((product.PowerWatts ?? 0) / 100, 5, 10),
            "Case" when text.Contains("flow", StringComparison.Ordinal) || text.Contains("mesh", StringComparison.Ordinal) => 8,
            "Cooling" when text.Contains("aio", StringComparison.Ordinal) || text.Contains("nh d15", StringComparison.Ordinal) => 9,
            _ => 5
        };
    }

    private static string BuildSlotMeta(string slot, Product product)
    {
        return slot switch
        {
            "PSU" => (product.PowerWatts ?? 0) > 0 ? $"{(product.PowerWatts ?? 0)}W" : "Nguồn hệ thống",
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

    private static string GetCpuPlatform(Product product) => SocketKey(product.Socket);

    private static string GetMainboardPlatform(Product product) => SocketKey(product.Socket);

    private static string SocketKey(CpuSocket? socket) => socket switch
    {
        CpuSocket.Lga1200 => "intel-lga1200",
        CpuSocket.Lga1700 => "intel-lga1700",
        CpuSocket.Lga1851 => "intel-lga1851",
        CpuSocket.Am4 => "amd-am4",
        CpuSocket.Am5 => "amd-am5",
        _ => string.Empty
    };

    private static string GetMemoryType(Product product) => product.MemoryType switch
    {
        MemoryStandard.Ddr4 => "ddr4",
        MemoryStandard.Ddr5 => "ddr5",
        _ => string.Empty
    };

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
        "intel-lga1200" => "Intel LGA1200",
        "intel-lga1851" => "Intel LGA1851",
        "amd-am4" => "AMD AM4",
        "amd-am5" => "AMD AM5",
        _ => platform
    };

}
