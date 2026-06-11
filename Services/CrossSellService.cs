using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Services;

public class CrossSellService : ICrossSellService
{
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
        var productIds = cartItems.Select(item => item.ProductId).ToHashSet();
        if (!productIds.Any())
        {
            return Array.Empty<CrossSellSuggestionViewModel>();
        }

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
            return Array.Empty<CrossSellSuggestionViewModel>();
        }

        var addOnProductIds = offers.Select(offer => offer.AddOnProductId).Distinct().ToHashSet();
        var salesByProduct = await GetSoldQuantitiesAsync(cancellationToken);
        var slowMovingProductIds = await GetSlowMovingProductIdsAsync(salesByProduct, cancellationToken);

        return offers
            .Where(offer => addOnProductIds.Contains(offer.AddOnProductId) && slowMovingProductIds.Contains(offer.AddOnProductId))
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
                DiscountPercent = offer.DiscountPercent,
                OriginalPrice = originalPrice,
                OfferPrice = DiscountedPrice(originalPrice, offer.DiscountPercent)
            };
        }).ToList();
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

    private static decimal DiscountedPrice(decimal price, int discountPercent)
    {
        return Math.Round(price * (100 - discountPercent) / 100m, 0);
    }
}
