using EcommerceApp.Data;
using EcommerceApp.Models;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Services;

public class RecommendationService : IRecommendationService
{
    private const int InteractionLookbackDays = 90;
    private const int PurchaseLookbackDays = 365;

    private readonly AppDbContext _db;

    public RecommendationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Product>> GetRecommendationsAsync(
        string? userId,
        string? sessionId,
        int take = 8,
        int? anchorProductId = null,
        IEnumerable<int>? excludedProductIds = null,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 24);
        var now = DateTime.UtcNow;
        var seedScores = new Dictionary<int, decimal>();
        var excludedIds = new HashSet<int>(excludedProductIds ?? Enumerable.Empty<int>());

        if (anchorProductId.HasValue && anchorProductId.Value > 0)
        {
            AddScore(seedScores, anchorProductId.Value, 10m);
            excludedIds.Add(anchorProductId.Value);
        }

        var hasUserId = !string.IsNullOrWhiteSpace(userId);
        var hasSessionId = !string.IsNullOrWhiteSpace(sessionId);

        if (hasUserId || hasSessionId)
        {
            var cartProductIds = await _db.CartItems
                .AsNoTracking()
                .Where(item => item.Cart != null
                    && ((hasUserId && item.Cart.UserId == userId)
                        || (hasSessionId && item.Cart.SessionId == sessionId)))
                .Select(item => item.ProductId)
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var productId in cartProductIds)
            {
                AddScore(seedScores, productId, 8m);
                excludedIds.Add(productId);
            }
        }

        if (hasUserId)
        {
            var wishlistProductIds = await _db.WishlistItems
                .AsNoTracking()
                .Where(item => item.UserId == userId)
                .Select(item => item.ProductId)
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var productId in wishlistProductIds)
            {
                AddScore(seedScores, productId, 5m);
                excludedIds.Add(productId);
            }

            var userOrderRows = await _db.OrderItems
                .AsNoTracking()
                .Where(item => item.Order != null
                    && item.Order.UserId == userId
                    && item.Order.CreatedAt >= now.AddDays(-PurchaseLookbackDays)
                    && item.Order.Status != OrderStatuses.Cancelled
                    && (item.Order.IsPaid || item.Order.Status == OrderStatuses.Delivered))
                .Select(item => new { item.ProductId, item.Quantity })
                .ToListAsync(cancellationToken);

            foreach (var row in userOrderRows)
            {
                AddScore(seedScores, row.ProductId, Math.Min(12m, row.Quantity * 6m));
                excludedIds.Add(row.ProductId);
            }
        }

        if (hasUserId || hasSessionId)
        {
            var recentInteractions = await _db.ProductInteractions
                .AsNoTracking()
                .Where(interaction => interaction.CreatedAt >= now.AddDays(-InteractionLookbackDays)
                    && ((hasUserId && interaction.UserId == userId)
                        || (hasSessionId && interaction.SessionId == sessionId)))
                .Select(interaction => new
                {
                    interaction.ProductId,
                    interaction.EventType,
                    interaction.CreatedAt
                })
                .ToListAsync(cancellationToken);

            foreach (var interaction in recentInteractions)
            {
                AddScore(
                    seedScores,
                    interaction.ProductId,
                    InteractionWeight(interaction.EventType) * RecencyBoost(now, interaction.CreatedAt));
                excludedIds.Add(interaction.ProductId);
            }
        }

        var candidateScores = new Dictionary<int, decimal>();
        var purchaseSince = now.AddDays(-PurchaseLookbackDays);
        var orderRows = await _db.OrderItems
            .AsNoTracking()
            .Where(item => item.Order != null
                && item.Order.CreatedAt >= purchaseSince
                && item.Order.Status != OrderStatuses.Cancelled
                && (item.Order.IsPaid || item.Order.Status == OrderStatuses.Delivered))
            .Select(item => new { item.OrderId, item.ProductId, item.Quantity })
            .ToListAsync(cancellationToken);

        foreach (var group in orderRows.GroupBy(row => row.ProductId))
        {
            AddScore(candidateScores, group.Key, Math.Min(18m, group.Sum(row => row.Quantity) * 0.35m));
        }

        var baskets = orderRows
            .GroupBy(row => row.OrderId)
            .Select(group => group.Select(row => row.ProductId).Distinct().ToHashSet())
            .Where(basket => basket.Count >= 2)
            .ToList();

        foreach (var seed in seedScores)
        {
            var anchorSupport = baskets.Count(basket => basket.Contains(seed.Key));
            if (anchorSupport == 0)
            {
                continue;
            }

            foreach (var group in baskets
                .Where(basket => basket.Contains(seed.Key))
                .SelectMany(basket => basket)
                .Where(productId => productId != seed.Key && !excludedIds.Contains(productId))
                .GroupBy(productId => productId))
            {
                var confidence = group.Count() / (decimal)anchorSupport;
                AddScore(candidateScores, group.Key, seed.Value * confidence * 5m + group.Count() * 0.8m);
            }
        }

        var trendInteractions = await _db.ProductInteractions
            .AsNoTracking()
            .Where(interaction => interaction.CreatedAt >= now.AddDays(-30))
            .Select(interaction => new
            {
                interaction.ProductId,
                interaction.EventType,
                interaction.CreatedAt
            })
            .ToListAsync(cancellationToken);

        foreach (var interaction in trendInteractions)
        {
            AddScore(
                candidateScores,
                interaction.ProductId,
                InteractionWeight(interaction.EventType) * RecencyBoost(now, interaction.CreatedAt) * 0.18m);
        }

        if (seedScores.Any())
        {
            var seedProductIds = seedScores.Keys.ToHashSet();
            var seedProducts = await _db.Products
                .AsNoTracking()
                .Where(product => seedProductIds.Contains(product.Id))
                .Select(product => new { product.Id, product.CategoryId })
                .ToListAsync(cancellationToken);
            var categoryWeights = seedProducts
                .GroupBy(product => product.CategoryId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(product => seedScores[product.Id]));

            var categoryCandidateIds = await _db.Products
                .AsNoTracking()
                .Where(product => product.Stock > 0
                    && !excludedIds.Contains(product.Id)
                    && categoryWeights.Keys.Contains(product.CategoryId))
                .Select(product => new { product.Id, product.CategoryId })
                .ToListAsync(cancellationToken);

            foreach (var product in categoryCandidateIds)
            {
                AddScore(candidateScores, product.Id, categoryWeights[product.CategoryId] * 0.65m);
            }
        }

        var candidates = await _db.Products
            .AsNoTracking()
            .Where(product => product.Stock > 0 && !excludedIds.Contains(product.Id))
            .Select(product => new
            {
                product.Id,
                product.IsFeatured,
                product.DiscountPercent,
                product.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var rankedIds = candidates
            .Select(product => new
            {
                product.Id,
                Score = candidateScores.GetValueOrDefault(product.Id)
                    + (product.IsFeatured ? 1.5m : 0m)
                    + Math.Min(product.DiscountPercent, 30) * 0.04m
                    + (product.CreatedAt >= now.AddDays(-30) ? 0.8m : 0m),
                product.IsFeatured,
                product.CreatedAt
            })
            .Where(product => product.Score > 0)
            .OrderByDescending(product => product.Score)
            .ThenByDescending(product => product.IsFeatured)
            .ThenByDescending(product => product.CreatedAt)
            .Take(take)
            .Select(product => product.Id)
            .ToList();

        if (!rankedIds.Any())
        {
            return Array.Empty<Product>();
        }

        var products = await _db.Products
            .AsNoTracking()
            .Include(product => product.Category)
            .Include(product => product.Images.OrderBy(image => image.SortOrder))
            .Where(product => rankedIds.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, cancellationToken);

        return rankedIds
            .Where(products.ContainsKey)
            .Select(productId => products[productId])
            .ToList();
    }

    private static void AddScore(Dictionary<int, decimal> scores, int productId, decimal score)
    {
        if (productId <= 0 || score <= 0)
        {
            return;
        }

        scores[productId] = scores.GetValueOrDefault(productId) + score;
    }

    private static decimal InteractionWeight(string eventType)
    {
        return eventType switch
        {
            ProductInteractionEvents.AddToCart => 7m,
            ProductInteractionEvents.WishlistAdd => 6m,
            ProductInteractionEvents.ProductClick => 3m,
            ProductInteractionEvents.DetailView => 2m,
            _ => 1m
        };
    }

    private static decimal RecencyBoost(DateTime now, DateTime createdAt)
    {
        var ageDays = (now - createdAt).TotalDays;
        return ageDays switch
        {
            <= 7 => 1.4m,
            <= 30 => 1m,
            <= 90 => 0.55m,
            _ => 0.25m
        };
    }
}
