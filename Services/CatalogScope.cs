using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using System.Text.RegularExpressions;

namespace EcommerceApp.Services;

public interface ICatalogScope
{
    IQueryable<Product> Products(IQueryable<Product> query);
    IQueryable<Category> Categories(IQueryable<Category> query);
    bool IsPhoneCategory(string? slug);
    bool IsPhoneCategoryName(string? name);
    bool IsPhoneBanner(Banner banner);
}

/// <summary>
/// Giới hạn bản triển khai này vào đúng đề tài cửa hàng điện thoại. Dữ liệu cũ
/// vẫn được giữ trong database để có thể phục hồi, nhưng không được đưa ra UI/API.
/// </summary>
public sealed class PhoneCatalogScope : ICatalogScope
{
    public const string RootSlug = "dien-thoai";
    private const string SlugPrefix = "dien-thoai-";

    private static readonly string[] OutOfScopeTerms =
    {
        "build pc", "buildpc", "smart pc", "laptop", "macbook", "phụ kiện", "phu kien",
        "linh kiện", "linh kien", "màn hình", "man hinh", "đồng hồ", "dong ho",
        "tai nghe", "bàn phím", "ban phim", "chuột", "chuot"
    };

    public IQueryable<Product> Products(IQueryable<Product> query) =>
        query.Where(product => product.Category != null
            && (product.Category.Slug == RootSlug || product.Category.Slug.StartsWith(SlugPrefix)));

    public IQueryable<Category> Categories(IQueryable<Category> query) =>
        query.Where(category => category.Slug == RootSlug || category.Slug.StartsWith(SlugPrefix));

    public bool IsPhoneCategory(string? slug) =>
        string.Equals(slug, RootSlug, StringComparison.OrdinalIgnoreCase)
        || (slug?.StartsWith(SlugPrefix, StringComparison.OrdinalIgnoreCase) ?? false);

    public bool IsPhoneCategoryName(string? name) =>
        IsPhoneCategory(SlugGenerator.Generate(name ?? string.Empty));

    public bool IsPhoneBanner(Banner banner)
    {
        var content = $"{banner.Title} {banner.Subtitle} {banner.ButtonText} {banner.LinkUrl}";
        return !OutOfScopeTerms.Any(term => content.Contains(term, StringComparison.OrdinalIgnoreCase))
            && !Regex.IsMatch(content, @"\b(cpu|vga|ram|ssd|mainboard|psu|cooling)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}

/// <summary>
/// Một nguồn duy nhất cho các hãng điện thoại được trình bày trên storefront.
/// Hãng được suy ra từ tên sản phẩm nên hoạt động cả với database cũ chỉ có
/// một danh mục gốc "Điện thoại".
/// </summary>
public static class PhoneBrandCatalog
{
    private sealed record Definition(string Name, string SearchTerm, string Mark, params string[] MatchTerms);

    private static readonly Definition[] Definitions =
    {
        new("iPhone", "iPhone", "iP", "iphone", "apple"),
        new("Samsung", "Samsung", "S", "samsung", "galaxy"),
        new("Xiaomi", "Xiaomi", "Mi", "xiaomi", "redmi", "poco"),
        new("OPPO", "OPPO", "O", "oppo"),
        new("vivo", "Vivo", "v", "vivo"),
        new("realme", "Realme", "R", "realme"),
        new("Google Pixel", "Pixel", "G", "google pixel", "pixel"),
        new("Sony Xperia", "Xperia", "X", "sony xperia", "xperia")
    };

    public static IReadOnlyList<PhoneBrandViewModel> Summarize(IEnumerable<string> productNames)
    {
        var names = productNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        return Definitions
            .Select(brand => new PhoneBrandViewModel(
                brand.Name,
                brand.SearchTerm,
                brand.Mark,
                names.Count(name => brand.MatchTerms.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase)))))
            .Where(brand => brand.ProductCount > 0)
            .ToList();
    }

    public static PhoneBrandViewModel? Resolve(string? productName)
    {
        if (string.IsNullOrWhiteSpace(productName)) return null;
        var brand = Definitions.FirstOrDefault(definition =>
            definition.MatchTerms.Any(term => productName.Contains(term, StringComparison.OrdinalIgnoreCase)));
        return brand is null ? null : new PhoneBrandViewModel(brand.Name, brand.SearchTerm, brand.Mark, 0);
    }
}
