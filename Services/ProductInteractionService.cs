using EcommerceApp.Data;
using EcommerceApp.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EcommerceApp.Services;

public class ProductInteractionService : IProductInteractionService
{
    private readonly AppDbContext _db;

    public ProductInteractionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task TrackAsync(int productId, string eventType, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        if (productId <= 0 || string.IsNullOrWhiteSpace(eventType) || httpContext.User.IsInRole("Admin"))
        {
            return;
        }

        eventType = eventType.Trim();
        if (eventType.Length > 40 || !IsAllowedEvent(eventType))
        {
            return;
        }

        var exists = await _db.Products.AnyAsync(product => product.Id == productId, cancellationToken);
        if (!exists)
        {
            return;
        }

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var referrer = httpContext.Request.Headers.Referer.ToString();
        _db.ProductInteractions.Add(new ProductInteraction
        {
            ProductId = productId,
            UserId = string.IsNullOrWhiteSpace(userId) ? null : userId,
            SessionId = httpContext.Session.Id,
            EventType = eventType,
            Referrer = referrer.Length > 500 ? referrer[..500] : referrer
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static bool IsAllowedEvent(string eventType)
    {
        return eventType is ProductInteractionEvents.DetailView
            or ProductInteractionEvents.ProductClick
            or ProductInteractionEvents.AddToCart
            or ProductInteractionEvents.WishlistAdd
            or ProductInteractionEvents.WishlistRemove;
    }
}
