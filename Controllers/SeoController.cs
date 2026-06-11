using System.Xml.Linq;
using EcommerceApp.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
public class SeoController : Controller
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public SeoController(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    [HttpGet("/sitemap.xml")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Sitemap()
    {
        var baseUri = GetBaseUri();
        var products = await _db.Products
            .AsNoTracking()
            .OrderBy(product => product.Id)
            .Select(product => new { product.Id, product.CreatedAt })
            .ToListAsync();
        var categories = await _db.Categories
            .AsNoTracking()
            .OrderBy(category => category.Id)
            .Select(category => category.Id)
            .ToListAsync();

        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var root = new XElement(ns + "urlset");

        AddUrl(root, ns, baseUri, "/", DateTime.UtcNow, "daily", "1.0");
        AddUrl(root, ns, baseUri, "/Product", DateTime.UtcNow, "daily", "0.9");
        AddUrl(root, ns, baseUri, "/BuildPc", null, "weekly", "0.7");

        foreach (var path in new[]
        {
            "/Info/About",
            "/Info/BuyingGuide",
            "/Info/Returns",
            "/Info/Warranty",
            "/Info/Faq",
            "/Info/Privacy",
            "/Info/Terms"
        })
        {
            AddUrl(root, ns, baseUri, path, null, "monthly", "0.5");
        }

        foreach (var categoryId in categories)
        {
            AddUrl(root, ns, baseUri, $"/Product?categoryId={categoryId}", null, "weekly", "0.7");
        }

        foreach (var product in products)
        {
            AddUrl(root, ns, baseUri, $"/Product/Detail/{product.Id}", product.CreatedAt, "weekly", "0.8");
        }

        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        return Content(document.ToString(SaveOptions.DisableFormatting), "application/xml; charset=utf-8");
    }

    [HttpGet("/robots.txt")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public IActionResult Robots()
    {
        var sitemapUrl = new Uri(GetBaseUri(), "sitemap.xml");
        var content = $"""
            User-agent: *
            Allow: /
            Disallow: /Admin/
            Disallow: /Account/
            Disallow: /Cart/
            Disallow: /Order/
            Disallow: /Payment/
            Disallow: /Notification/
            Disallow: /Wishlist/
            Disallow: /ReturnWarranty/

            Sitemap: {sitemapUrl}
            """;

        return Content(content, "text/plain; charset=utf-8");
    }

    private Uri GetBaseUri()
    {
        var configuredBaseUrl = _configuration["Seo:BaseUrl"]?.Trim();
        var baseUrl = !string.IsNullOrWhiteSpace(configuredBaseUrl)
            ? configuredBaseUrl
            : $"{Request.Scheme}://{Request.Host}{Request.PathBase}";

        return new Uri($"{baseUrl.TrimEnd('/')}/", UriKind.Absolute);
    }

    private static void AddUrl(
        XElement root,
        XNamespace ns,
        Uri baseUri,
        string path,
        DateTime? lastModified,
        string changeFrequency,
        string priority)
    {
        var url = new XElement(ns + "url",
            new XElement(ns + "loc", new Uri(baseUri, path.TrimStart('/')).AbsoluteUri),
            new XElement(ns + "changefreq", changeFrequency),
            new XElement(ns + "priority", priority));

        if (lastModified.HasValue)
        {
            url.Add(new XElement(ns + "lastmod", lastModified.Value.ToUniversalTime().ToString("yyyy-MM-dd")));
        }

        root.Add(url);
    }
}
