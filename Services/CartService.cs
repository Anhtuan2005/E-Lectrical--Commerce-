using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Services;

public class CartService : ICartService
{
    private readonly AppDbContext _db;

    public CartService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<CartViewModel> GetCartAsync(string? userId, string sessionId)
    {
        var cart = await GetOrCreateCartAsync(userId, sessionId);
        await _db.Entry(cart).Collection(c => c.Items).Query().Include(item => item.Product).LoadAsync();
        return new CartViewModel { Items = cart.Items.OrderBy(item => item.Product?.Name).ToList() };
    }

    public async Task<Cart?> GetCartEntityAsync(string? userId, string sessionId)
    {
        return await FindCartQuery(userId, sessionId)
            .Include(cart => cart.Items)
            .ThenInclude(item => item.Product)
            .FirstOrDefaultAsync();
    }

    public async Task<int> GetCountAsync(string? userId, string sessionId)
    {
        var cart = await GetCartEntityAsync(userId, sessionId);
        return cart?.Items.Sum(item => item.Quantity) ?? 0;
    }

    public async Task AddAsync(int productId, int quantity, string? userId, string sessionId)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product is null || product.Stock <= 0)
        {
            return;
        }

        quantity = Math.Max(1, quantity);
        var cart = await GetOrCreateCartAsync(userId, sessionId);
        var item = await _db.CartItems.FirstOrDefaultAsync(row => row.CartId == cart.Id && row.ProductId == productId);

        if (item is null)
        {
            _db.CartItems.Add(new CartItem { CartId = cart.Id, ProductId = productId, Quantity = Math.Min(quantity, product.Stock) });
        }
        else
        {
            item.Quantity = Math.Min(item.Quantity + quantity, product.Stock);
        }

        await _db.SaveChangesAsync();
    }

    public async Task UpdateQuantityAsync(int productId, int quantity, string? userId, string sessionId)
    {
        var cart = await GetOrCreateCartAsync(userId, sessionId);
        var item = await _db.CartItems.Include(row => row.Product).FirstOrDefaultAsync(row => row.CartId == cart.Id && row.ProductId == productId);
        if (item is null)
        {
            return;
        }

        if (quantity <= 0)
        {
            _db.CartItems.Remove(item);
        }
        else
        {
            item.Quantity = Math.Min(quantity, item.Product?.Stock ?? quantity);
        }

        await _db.SaveChangesAsync();
    }

    public async Task RemoveAsync(int productId, string? userId, string sessionId)
    {
        var cart = await GetOrCreateCartAsync(userId, sessionId);
        var item = await _db.CartItems.FirstOrDefaultAsync(row => row.CartId == cart.Id && row.ProductId == productId);
        if (item is not null)
        {
            _db.CartItems.Remove(item);
            await _db.SaveChangesAsync();
        }
    }

    public async Task ClearAsync(string? userId, string sessionId)
    {
        var cart = await GetCartEntityAsync(userId, sessionId);
        if (cart is null)
        {
            return;
        }

        _db.CartItems.RemoveRange(cart.Items);
        await _db.SaveChangesAsync();
    }

    public async Task MergeGuestCartAsync(string userId, string sessionId)
    {
        await GetOrCreateCartAsync(userId, sessionId);
    }

    private async Task<Cart> GetOrCreateCartAsync(string? userId, string sessionId)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var userCart = await _db.Carts.Include(cart => cart.Items).ThenInclude(item => item.Product).FirstOrDefaultAsync(cart => cart.UserId == userId);
            var sessionCart = await _db.Carts.Include(cart => cart.Items).ThenInclude(item => item.Product).FirstOrDefaultAsync(cart => cart.SessionId == sessionId);

            if (sessionCart is not null)
            {
                if (userCart is null)
                {
                    sessionCart.UserId = userId;
                    sessionCart.SessionId = null;
                    await _db.SaveChangesAsync();
                    return sessionCart;
                }

                foreach (var sessionItem in sessionCart.Items)
                {
                    var targetItem = userCart.Items.FirstOrDefault(item => item.ProductId == sessionItem.ProductId);
                    if (targetItem is null)
                    {
                        userCart.Items.Add(new CartItem { ProductId = sessionItem.ProductId, Quantity = Math.Min(sessionItem.Quantity, sessionItem.Product?.Stock ?? sessionItem.Quantity) });
                    }
                    else
                    {
                        targetItem.Quantity = Math.Min(targetItem.Quantity + sessionItem.Quantity, targetItem.Product?.Stock ?? targetItem.Quantity + sessionItem.Quantity);
                    }
                }

                _db.Carts.Remove(sessionCart);
                await _db.SaveChangesAsync();
                return userCart;
            }
        }

        var cart = await FindCartQuery(userId, sessionId).FirstOrDefaultAsync();
        if (cart is not null)
        {
            return cart;
        }

        cart = new Cart { UserId = userId, SessionId = string.IsNullOrWhiteSpace(userId) ? sessionId : null };
        _db.Carts.Add(cart);
        await _db.SaveChangesAsync();
        return cart;
    }

    private IQueryable<Cart> FindCartQuery(string? userId, string sessionId)
    {
        return string.IsNullOrWhiteSpace(userId)
            ? _db.Carts.Where(cart => cart.SessionId == sessionId)
            : _db.Carts.Where(cart => cart.UserId == userId);
    }
}
