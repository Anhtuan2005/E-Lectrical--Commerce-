using Microsoft.AspNetCore.Http;

namespace EcommerceApp.Services;

public interface IProductInteractionService
{
    Task TrackAsync(int productId, string eventType, HttpContext httpContext, CancellationToken cancellationToken = default);
}
