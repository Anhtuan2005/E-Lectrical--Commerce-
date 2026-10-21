using EcommerceApp.Models;

namespace EcommerceApp.Services;

public record ComponentComparisonNeed(string Id, string Label, string Purpose, string MetricLabel, string Unit);
public record ComponentComparisonResult(int? WinnerProductId, string Headline, string Detail,
    ComponentComparisonNeed? Need, IReadOnlyList<string> Evidence);

/// <summary>Only compares administrator-entered numbers with a cited HTTPS source.</summary>
public static class ComponentComparisonAdvisor
{
    private static readonly IReadOnlyDictionary<string, ComponentComparisonNeed[]> Needs =
        new Dictionary<string, ComponentComparisonNeed[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["cpu"] = [
                new("cores", "Tác vụ nhiều nhân", "Số nhân giúp chọn CPU cho khối lượng công việc song song; không thay thế benchmark.", "Số nhân", "nhân"),
                new("threads", "Tác vụ nhiều luồng", "So số luồng được công bố; không suy ra tốc độ xử lý.", "Số luồng", "luồng")],
            ["vga"] = [new("vram", "Dung lượng VRAM", "Ưu tiên bộ nhớ đồ họa cho dữ liệu lớn; không suy ra FPS.", "VRAM", "GB")],
            ["ram"] = [
                new("ram-capacity", "Đa nhiệm / dung lượng", "Ưu tiên dung lượng RAM đã công bố.", "Dung lượng RAM", "GB"),
                new("ram-speed", "Tốc độ danh định", "Chỉ so MT/s khi cùng chuẩn RAM; không suy ra tốc độ ứng dụng.", "Tốc độ RAM", "MT/s")],
            ["ssd"] = [
                new("ssd-capacity", "Lưu trữ nhiều", "Ưu tiên dung lượng SSD đã công bố.", "Dung lượng SSD", "GB"),
                new("ssd-read", "Đọc tuần tự", "So tốc độ đọc tuần tự công bố; không suy ra độ trễ hay tốc độ dùng thực tế.", "Đọc tuần tự", "MB/s"),
                new("ssd-write", "Ghi tuần tự", "So tốc độ ghi tuần tự công bố; không suy ra tốc độ dùng thực tế.", "Ghi tuần tự", "MB/s")],
            ["mainboard"] = [new("m2", "Mở rộng SSD", "Ưu tiên số khe M.2; cần kiểm tra tương thích CPU và chuẩn khe trước khi mua.", "Khe M.2", "khe")],
            ["psu"] = [new("watts", "Dự phòng công suất", "Ưu tiên công suất danh định; cần kiểm tra tổng tải và đầu cấp nguồn của cấu hình.", "Công suất", "W")]
        };

    public static IReadOnlyList<ComponentComparisonNeed> GetNeeds(string? categorySlug) =>
        categorySlug is not null && Needs.TryGetValue(categorySlug, out var needs)
            ? needs : Array.Empty<ComponentComparisonNeed>();

    public static IReadOnlyDictionary<string, string> BuildVerifiedSpecs(Product product)
    {
        var specs = new Dictionary<string, string>();
        if (!HasSource(product)) return specs;
        void Add(string label, int? value, string unit)
        {
            if (value.HasValue) specs[label] = $"{value.Value:N0} {unit}";
        }
        switch (product.Category?.Slug?.ToLowerInvariant())
        {
            case "cpu": Add("Số nhân (đã dẫn nguồn)", product.CpuCores, "nhân");
                Add("Số luồng (đã dẫn nguồn)", product.CpuThreads, "luồng"); break;
            case "vga": Add("VRAM (đã dẫn nguồn)", product.GpuVramGb, "GB"); break;
            case "ram": Add("Dung lượng (đã dẫn nguồn)", product.MemoryCapacityGb, "GB");
                Add("Tốc độ danh định (đã dẫn nguồn)", product.MemorySpeedMt, "MT/s"); break;
            case "ssd": Add("Dung lượng (đã dẫn nguồn)", product.StorageCapacityGb, "GB");
                Add("Đọc tuần tự (đã dẫn nguồn)", product.StorageReadMb, "MB/s");
                Add("Ghi tuần tự (đã dẫn nguồn)", product.StorageWriteMb, "MB/s"); break;
            case "mainboard": Add("Khe M.2 (đã dẫn nguồn)", product.M2Slots, "khe"); break;
            case "psu": Add("Công suất (đã dẫn nguồn)", product.PowerWatts, "W"); break;
        }
        return specs;
    }

    public static ComponentComparisonResult Build(IReadOnlyList<Product> products, string? needId)
    {
        if (products.Count is < 2 or > 4 || products.Any(p => p.CategoryId != products[0].CategoryId))
            return Unknown("Chỉ so sánh 2–4 linh kiện cùng danh mục.");

        var needs = GetNeeds(products[0].Category?.Slug);
        if (needs.Count == 0)
            return Unknown("Danh mục này chưa có tiêu chí so sánh linh kiện đáng tin cậy.");

        var need = needs.FirstOrDefault(n => string.Equals(n.Id, needId, StringComparison.OrdinalIgnoreCase)) ?? needs[0];
        if (need.Id == "ram-speed" && (products.Any(p => p.MemoryType is null) ||
            products.Select(p => p.MemoryType).Distinct().Count() != 1))
            return Unknown("Chưa đủ thông tin: chỉ so tốc độ danh định khi tất cả mẫu cùng chuẩn RAM đã xác minh.", need);

        if (products.Any(p => !HasSource(p)))
            return Unknown("Chưa đủ thông tin: cần URL HTTPS của tài liệu thông số cho mọi mẫu.", need);

        var values = products.Select(p => Read(p, need.Id)).ToArray();
        if (values.Any(v => v is null))
            return Unknown($"Chưa đủ thông tin: cần {need.MetricLabel.ToLowerInvariant()} đã xác minh cho mọi mẫu.", need);

        var evidence = products.Select((p, i) => $"{p.Name}: {values[i]} {need.Unit}").ToArray();
        var highest = values.Max()!.Value;
        var leaders = products.Where((_, i) => values[i] == highest).ToArray();
        if (leaders.Length != 1)
            return new(null, $"Chưa có mẫu nổi trội về {need.MetricLabel.ToLowerInvariant()}",
                $"Có {leaders.Length} mẫu cùng mức cao nhất. {need.Purpose}", need, evidence);

        return new(leaders[0].Id, $"{leaders[0].Name} phù hợp hơn nếu ưu tiên {need.Label.ToLowerInvariant()}",
            $"Chỉ dựa trên {need.MetricLabel.ToLowerInvariant()} được Admin nhập kèm nguồn. {need.Purpose}", need, evidence);
    }

    private static ComponentComparisonResult Unknown(string detail, ComponentComparisonNeed? need = null) =>
        new(null, "Chưa đủ thông tin để kết luận", detail, need, Array.Empty<string>());

    private static bool HasSource(Product product) => Uri.TryCreate(product.SpecificationSourceUrl,
        UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    private static int? Read(Product product, string id) => id switch
    {
        "cores" => product.CpuCores,
        "threads" => product.CpuThreads,
        "vram" => product.GpuVramGb,
        "ram-capacity" => product.MemoryCapacityGb,
        "ram-speed" => product.MemorySpeedMt,
        "ssd-capacity" => product.StorageCapacityGb,
        "ssd-read" => product.StorageReadMb,
        "ssd-write" => product.StorageWriteMb,
        "m2" => product.M2Slots,
        "watts" => product.PowerWatts,
        _ => null
    };
}
