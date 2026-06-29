using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Services;

public class CrossSellService : ICrossSellService
{
    private const int AprioriLookbackDays = 180;
    private const decimal MinimumConfidence = 0.2m;

    private readonly AppDbContext _db;

    public CrossSellService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyDictionary<int, decimal>> GetEligibleUnitPricesAsync(IEnumerable<CartItem> cartItems, CancellationToken cancellationToken = default)
    {
        var items = cartItems.Where(item => item.Product is not null).ToList();
        if (!items.Any())
        {
            return new Dictionary<int, decimal>();
        }

        var productIds = items.Select(item => item.ProductId).ToHashSet();
        var offers = await ActiveOffers()
            .Where(offer => productIds.Contains(offer.AnchorProductId) && productIds.Contains(offer.AddOnProductId))
            .ToListAsync(cancellationToken);

        return offers
            .GroupBy(offer => offer.AddOnProductId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var item = items.First(row => row.ProductId == group.Key);
                    var regularPrice = item.Product?.SalePrice ?? item.Product?.Price ?? 0;
                    var bestDiscount = group.Max(offer => offer.DiscountPercent);
                    return DiscountedPrice(regularPrice, bestDiscount);
                });
    }

    public async Task<IReadOnlyList<CrossSellSuggestionViewModel>> GetSuggestionsAsync(IEnumerable<CartItem> cartItems, int take = 4, CancellationToken cancellationToken = default)
    {
        var items = cartItems.Where(item => item.Product is not null).ToList();
        var productIds = items.Select(item => item.ProductId).ToHashSet();
        if (!productIds.Any())
        {
            return Array.Empty<CrossSellSuggestionViewModel>();
        }

        var suggestions = await GetManualOfferSuggestionsAsync(productIds, take, cancellationToken);
        if (suggestions.Count >= take)
        {
            return suggestions;
        }

        var excludedIds = productIds
            .Concat(suggestions.Select(suggestion => suggestion.ProductId))
            .ToHashSet();
        var aprioriSuggestions = await GetAprioriSuggestionsAsync(items, excludedIds, take - suggestions.Count, cancellationToken);

        return suggestions
            .Concat(aprioriSuggestions)
            .Take(take)
            .ToList();
    }

    private async Task<List<CrossSellSuggestionViewModel>> GetManualOfferSuggestionsAsync(IReadOnlySet<int> productIds, int take, CancellationToken cancellationToken)
    {
        var offers = await ActiveOffers()
            .AsNoTracking()
            .Include(offer => offer.AnchorProduct)
            .Include(offer => offer.AddOnProduct)
            .ThenInclude(product => product!.Images)
            .Where(offer => productIds.Contains(offer.AnchorProductId)
                && !productIds.Contains(offer.AddOnProductId)
                && offer.AddOnProduct != null
                && offer.AddOnProduct.Stock > 0
                && !offer.AddOnProduct.IsDeleted)
            .ToListAsync(cancellationToken);

        if (!offers.Any())
        {
            return new List<CrossSellSuggestionViewModel>();
        }

        var salesByProduct = await GetSoldQuantitiesAsync(cancellationToken);
        var slowMovingProductIds = await GetSlowMovingProductIdsAsync(salesByProduct, cancellationToken);

        return offers
            .Where(offer => slowMovingProductIds.Contains(offer.AddOnProductId))
            .GroupBy(offer => offer.AddOnProductId)
            .Select(group => group
                .OrderByDescending(offer => offer.DiscountPercent)
                .ThenBy(offer => salesByProduct.GetValueOrDefault(offer.AddOnProductId, 0))
                .First())
            .OrderBy(offer => salesByProduct.GetValueOrDefault(offer.AddOnProductId, 0))
            .ThenByDescending(offer => offer.DiscountPercent)
            .ThenBy(offer => offer.AddOnProduct!.Price)
            .Take(take)
            .Select(offer =>
            {
                var product = offer.AddOnProduct!;
                var originalPrice = product.SalePrice;
                return new CrossSellSuggestionViewModel
                {
                    OfferId = offer.Id,
                    ProductId = product.Id,
                    ProductName = product.Name,
                    AnchorProductName = offer.AnchorProduct?.Name ?? string.Empty,
                    ImageUrl = product.PrimaryImageUrl,
                    Source = "manual",
                    DiscountPercent = offer.DiscountPercent,
                    OriginalPrice = originalPrice,
                    OfferPrice = DiscountedPrice(originalPrice, offer.DiscountPercent)
                };
            })
            .ToList();
    }

    private async Task<IReadOnlyList<CrossSellSuggestionViewModel>> GetAprioriSuggestionsAsync(
        IReadOnlyCollection<CartItem> cartItems,
        HashSet<int> excludedIds,
        int take,
        CancellationToken cancellationToken)
    {
        if (take <= 0)
        {
            return Array.Empty<CrossSellSuggestionViewModel>();
        }

        var cartProductIds = cartItems.Select(item => item.ProductId).Distinct().ToHashSet();
        var anchorNames = cartItems
            .Where(item => item.Product is not null)
            .GroupBy(item => item.ProductId)
            .ToDictionary(group => group.Key, group => group.First().Product!.Name);
        var since = DateTime.UtcNow.AddDays(-AprioriLookbackDays);
        var orderRows = await _db.OrderItems
            .AsNoTracking()
            .Where(item => item.Order != null
                && item.Order.CreatedAt >= since
                && item.Order.Status != OrderStatuses.Cancelled
                && (item.Order.IsPaid || item.Order.Status == OrderStatuses.Delivered))
            .Select(item => new { item.OrderId, item.ProductId })
            .Distinct()
            .ToListAsync(cancellationToken);

        var baskets = orderRows
            .GroupBy(row => row.OrderId)
            .Select(group => group.Select(row => row.ProductId).Distinct().ToHashSet())
            .Where(basket => basket.Count >= 2)
            .ToList();
        if (!baskets.Any())
        {
            return Array.Empty<CrossSellSuggestionViewModel>();
        }

        var minimumSupportCount = baskets.Count < 10
            ? 1
            : Math.Max(2, (int)Math.Ceiling(baskets.Count * 0.03m));
        var candidates = new Dictionary<int, AprioriCandidate>();

        foreach (var anchorProductId in cartProductIds)
        {
            var anchorSupport = baskets.Count(basket => basket.Contains(anchorProductId));
            if (anchorSupport == 0)
            {
                continue;
            }

            var anchorName = anchorNames.GetValueOrDefault(anchorProductId, "sản phẩm trong giỏ");
            foreach (var group in baskets
                .Where(basket => basket.Contains(anchorProductId))
                .SelectMany(basket => basket)
                .Where(productId => productId != anchorProductId && !excludedIds.Contains(productId))
                .GroupBy(productId => productId))
            {
                AddCandidate(candidates, group.Key, group.Count(), anchorSupport, anchorName, minimumSupportCount);
            }
        }

        if (cartProductIds.Count > 1)
        {
            var cartSupport = baskets.Count(basket => cartProductIds.All(basket.Contains));
            if (cartSupport > 0)
            {
                foreach (var group in baskets
                    .Where(basket => cartProductIds.All(basket.Contains))
                    .SelectMany(basket => basket)
                    .Where(productId => !cartProductIds.Contains(productId) && !excludedIds.Contains(productId))
                    .GroupBy(productId => productId))
                {
                    AddCandidate(candidates, group.Key, group.Count(), cartSupport, "giỏ hiện tại", minimumSupportCount);
                }
            }
        }

        var rankedCandidates = candidates.Values
            .Where(candidate => candidate.Confidence >= MinimumConfidence)
            .OrderByDescending(candidate => candidate.Confidence)
            .ThenByDescending(candidate => candidate.SupportCount)
            .Take(take * 3)
            .ToList();
        if (!rankedCandidates.Any())
        {
            return Array.Empty<CrossSellSuggestionViewModel>();
        }

        var candidateProductIds = rankedCandidates.Select(candidate => candidate.ProductId).ToHashSet();
        var products = await _db.Products
            .AsNoTracking()
            .Include(product => product.Images)
            .Where(product => candidateProductIds.Contains(product.Id) && product.Stock > 0 && !product.IsDeleted)
            .ToDictionaryAsync(product => product.Id, cancellationToken);

        return rankedCandidates
            .Where(candidate => products.ContainsKey(candidate.ProductId))
            .Take(take)
            .Select(candidate =>
            {
                var product = products[candidate.ProductId];
                var originalPrice = product.SalePrice;
                return new CrossSellSuggestionViewModel
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    AnchorProductName = candidate.AnchorProductName,
                    ImageUrl = product.PrimaryImageUrl,
                    Source = "apriori",
                    ConfidencePercent = Math.Round(candidate.Confidence * 100m, 1),
                    SupportCount = candidate.SupportCount,
                    DiscountPercent = 0,
                    OriginalPrice = originalPrice,
                    OfferPrice = originalPrice
                };
            })
            .ToList();
    }

    private async Task<Dictionary<int, int>> GetSoldQuantitiesAsync(CancellationToken cancellationToken)
    {
        return await _db.OrderItems
            .AsNoTracking()
            .Where(item => item.Order != null && item.Order.Status != OrderStatuses.Cancelled)
            .GroupBy(item => item.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                Quantity = group.Sum(item => item.Quantity)
            })
            .ToDictionaryAsync(row => row.ProductId, row => row.Quantity, cancellationToken);
    }

    private async Task<HashSet<int>> GetSlowMovingProductIdsAsync(IReadOnlyDictionary<int, int> salesByProduct, CancellationToken cancellationToken)
    {
        var products = await _db.Products
            .AsNoTracking()
            .Where(product => product.Stock > 0 && !product.IsDeleted)
            .Select(product => product.Id)
            .ToListAsync(cancellationToken);

        var rankedProducts = products
            .Select(productId => new
            {
                ProductId = productId,
                SoldQuantity = salesByProduct.GetValueOrDefault(productId, 0)
            })
            .OrderBy(product => product.SoldQuantity)
            .ThenBy(product => product.ProductId)
            .ToList();

        if (!rankedProducts.Any())
        {
            return new HashSet<int>();
        }

        if (rankedProducts.All(product => product.SoldQuantity == 0))
        {
            return rankedProducts.Select(product => product.ProductId).ToHashSet();
        }

        var slowCount = Math.Max(1, (int)Math.Ceiling(rankedProducts.Count * 0.5m));
        var cutoff = rankedProducts[slowCount - 1].SoldQuantity;
        return rankedProducts
            .Where(product => product.SoldQuantity <= cutoff)
            .Select(product => product.ProductId)
            .ToHashSet();
    }

    private IQueryable<CrossSellOffer> ActiveOffers()
    {
        var now = DateTime.UtcNow;
        return _db.CrossSellOffers.Where(offer =>
            offer.IsActive
            && (offer.StartDate == null || offer.StartDate <= now)
            && (offer.EndDate == null || offer.EndDate >= now));
    }

    private static void AddCandidate(
        Dictionary<int, AprioriCandidate> candidates,
        int productId,
        int supportCount,
        int antecedentSupport,
        string anchorProductName,
        int minimumSupportCount)
    {
        if (supportCount < minimumSupportCount || antecedentSupport <= 0)
        {
            return;
        }

        var confidence = supportCount / (decimal)antecedentSupport;
        if (candidates.TryGetValue(productId, out var existing)
            && (existing.Confidence > confidence
                || (existing.Confidence == confidence && existing.SupportCount >= supportCount)))
        {
            return;
        }

        candidates[productId] = new AprioriCandidate(productId, supportCount, confidence, anchorProductName);
    }

    private static decimal DiscountedPrice(decimal price, int discountPercent)
    {
        return Math.Round(price * (100 - discountPercent) / 100m, 0);
    }

    private sealed record AprioriCandidate(int ProductId, int SupportCount, decimal Confidence, string AnchorProductName);
}
