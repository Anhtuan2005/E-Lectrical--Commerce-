using EcommerceApp.Data;
using EcommerceApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace EcommerceApp.Services;

public static class ProductSearchIndex
{
    public static string[] QueryTerms(string? value)
    {
        return TermsFromText(value, 5);
    }

    public static async Task ReplaceTermsAsync(AppDbContext db, int productId)
    {
        var product = await db.Products
            .IgnoreQueryFilters()
            .Include(row => row.Category)
            .FirstOrDefaultAsync(row => row.Id == productId);
        if (product is null)
        {
            return;
        }

        var oldTerms = await db.ProductSearchTerms
            .Where(term => term.ProductId == productId)
            .ToListAsync();
        db.ProductSearchTerms.RemoveRange(oldTerms);

        foreach (var term in BuildTerms(product))
        {
            db.ProductSearchTerms.Add(new ProductSearchTerm
            {
                ProductId = product.Id,
                Term = term
            });
        }

        await db.SaveChangesAsync();
    }

    public static async Task RebuildAsync(AppDbContext db)
    {
        var products = await db.Products
            .IgnoreQueryFilters()
            .Include(product => product.Category)
            .ToListAsync();
        var existingProductIds = await db.ProductSearchTerms
            .Select(term => term.ProductId)
            .Distinct()
            .ToListAsync();
        var existing = existingProductIds.ToHashSet();

        foreach (var product in products.Where(product => !existing.Contains(product.Id)))
        {
            foreach (var term in BuildTerms(product))
            {
                db.ProductSearchTerms.Add(new ProductSearchTerm
                {
                    ProductId = product.Id,
                    Term = term
                });
            }
        }

        await db.SaveChangesAsync();
    }

    private static IEnumerable<string> BuildTerms(Product product)
    {
        return TermsFromText($"{product.Name} {product.Description} {product.Category?.Name}", 40);
    }

    private static string[] TermsFromText(string? value, int limit)
    {
        return Normalize(value)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => term.Length >= 2 && term.Length <= 80)
            .Distinct(StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
    }

    private static string Normalize(string? value)
    {
        var normalized = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
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
}
