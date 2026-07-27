using EcommerceApp.Models;

namespace EcommerceApp.Services;

public interface IRecommendationService
{
    Task<IReadOnlyList<Product>> GetRecommendationsAsync(
        string? userId,
        string? sessionId,
        int take = 8,
        int? anchorProductId = null,
        IEnumerable<int>? excludedProductIds = null,
        CancellationToken cancellationToken = default);
}
