using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Services;

public class CartService : ICartService
{
    private readonly AppDbContext _db;
    private readonly ICrossSellService _crossSellService;

    public CartService(AppDbContext db, ICrossSellService crossSellService)
    {
        _db = db;
        _crossSellService = crossSellService;
    }

    public async Task<CartViewModel> GetCartAsync(string? userId, string sessionId, IEnumerable<int>? productIds = null)
    {
        var cart = await GetOrCreateCartAsync(userId, sessionId);
        await _db.Entry(cart).Collection(c => c.Items).Query()
            .Include(item => item.Product)
            .ThenInclude(product => product!.Images)
            .LoadAsync();
        var items = cart.Items
            .OrderBy(item => string.IsNullOrWhiteSpace(item.GroupKey))
            .ThenBy(item => item.GroupKey)
            .ThenBy(item => item.GroupSortOrder ?? 999)
            .ThenBy(item => item.Product?.Name)
            .ToList();
        if (productIds is not null)
        {
            var selectedProductIds = productIds.Where(id => id > 0).Distinct().ToHashSet();
            items = items.Where(item => selectedProductIds.Contains(item.ProductId)).ToList();
        }

        return new CartViewModel
        {
            Items = items,
            CrossSellUnitPrices = await _crossSellService.GetEligibleUnitPricesAsync(items),
            CrossSellSuggestions = await _crossSellService.GetSuggestionsAsync(items)
        };
    }

    public async Task<Cart?> GetCartEntityAsync(string? userId, string sessionId)
    {
        return await FindCartQuery(userId, sessionId)
            .Include(cart => cart.Items)
            .ThenInclude(item => item.Product)
            .ThenInclude(product => product!.Images)
            .FirstOrDefaultAsync();
    }

    public async Task<int> GetCountAsync(string? userId, string sessionId)
    {
        return await FindCartQuery(userId, sessionId)
            .Select(cart => cart.Items.Sum(item => (int?)item.Quantity) ?? 0)
            .FirstOrDefaultAsync();
    }

    public async Task AddAsync(int productId, int quantity, string? userId, string sessionId, CartItemGroupInput? group = null)
    {
        if (quantity <= 0) throw new InvalidOperationException("Số lượng thêm vào giỏ phải lớn hơn 0.");
        var product = await _db.Products.FirstOrDefaultAsync(row => row.Id == productId);
        if (product is null || product.Stock <= 0)
        {
            throw new InvalidOperationException("Sản phẩm hiện không còn hàng.");
        }

        var cart = await GetOrCreateCartAsync(userId, sessionId);
        var item = await _db.CartItems.FirstOrDefaultAsync(row => row.CartId == cart.Id && row.ProductId == productId);
        var currentQuantity = item?.Quantity ?? 0;
        if (currentQuantity < 0 || (long)currentQuantity + quantity > product.Stock)
        {
            throw new InvalidOperationException($"Sản phẩm chỉ còn {product.Stock:N0} sản phẩm trong kho.");
        }

        if (item is null)
        {
            item = new CartItem { CartId = cart.Id, ProductId = productId, Quantity = quantity };
            ApplyGroup(item, group);
            _db.CartItems.Add(item);
        }
        else
        {
            item.Quantity += quantity;
            ApplyGroup(item, group);
        }

        Touch(cart);
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

        if (quantity <= 0 || item.Product is null || item.Product.Stock <= 0)
        {
            _db.CartItems.Remove(item);
        }
        else
        {
            item.Quantity = Math.Min(quantity, item.Product?.Stock ?? quantity);
        }

        Touch(cart);
        await _db.SaveChangesAsync();
    }

    public async Task RemoveAsync(int productId, string? userId, string sessionId)
    {
        var cart = await GetOrCreateCartAsync(userId, sessionId);
        var item = await _db.CartItems.FirstOrDefaultAsync(row => row.CartId == cart.Id && row.ProductId == productId);
        if (item is not null)
        {
            _db.CartItems.Remove(item);
            Touch(cart);
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
        Touch(cart);
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
                    foreach (var item in sessionCart.Items.ToList())
                    {
                        if (item.Quantity <= 0 || item.Product is null || item.Product.Stock <= 0)
                            _db.CartItems.Remove(item);
                        else
                            item.Quantity = Math.Min(item.Quantity, item.Product.Stock);
                    }
                    sessionCart.UserId = userId;
                    sessionCart.SessionId = null;
                    Touch(sessionCart);
                    await _db.SaveChangesAsync();
                    return sessionCart;
                }

                foreach (var sessionItem in sessionCart.Items)
                {
                    if (sessionItem.Quantity <= 0 || sessionItem.Product is null || sessionItem.Product.Stock <= 0) continue;
                    var targetItem = userCart.Items.FirstOrDefault(item => item.ProductId == sessionItem.ProductId);
                    if (targetItem is null)
                    {
                        userCart.Items.Add(new CartItem
                        {
                            ProductId = sessionItem.ProductId,
                            Quantity = Math.Min(sessionItem.Quantity, sessionItem.Product?.Stock ?? sessionItem.Quantity),
                            GroupKey = sessionItem.GroupKey,
                            GroupName = sessionItem.GroupName,
                            GroupSource = sessionItem.GroupSource,
                            GroupItemLabel = sessionItem.GroupItemLabel,
                            GroupSortOrder = sessionItem.GroupSortOrder
                        });
                    }
                    else
                    {
                        targetItem.Quantity = (int)Math.Min((long)Math.Max(0, targetItem.Quantity) + sessionItem.Quantity, sessionItem.Product.Stock);
                        if (string.IsNullOrWhiteSpace(targetItem.GroupKey) && !string.IsNullOrWhiteSpace(sessionItem.GroupKey))
                        {
                            targetItem.GroupKey = sessionItem.GroupKey;
                            targetItem.GroupName = sessionItem.GroupName;
                            targetItem.GroupSource = sessionItem.GroupSource;
                            targetItem.GroupItemLabel = sessionItem.GroupItemLabel;
                            targetItem.GroupSortOrder = sessionItem.GroupSortOrder;
                        }
                    }
                }

                _db.Carts.Remove(sessionCart);
                Touch(userCart);
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

    private static void Touch(Cart cart)
    {
        cart.UpdatedAt = DateTime.UtcNow;
    }

    private static void ApplyGroup(CartItem item, CartItemGroupInput? group)
    {
        if (group is null || string.IsNullOrWhiteSpace(group.Key))
        {
            return;
        }

        item.GroupKey = Truncate(group.Key.Trim(), 64);
        item.GroupName = Truncate(string.IsNullOrWhiteSpace(group.Name) ? "Bộ cấu hình Smart PC" : group.Name.Trim(), 120);
        item.GroupSource = Truncate(string.IsNullOrWhiteSpace(group.Source) ? "SmartPC" : group.Source.Trim(), 40);
        item.GroupItemLabel = Truncate(group.ItemLabel?.Trim(), 80);
        item.GroupSortOrder = group.SortOrder;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= maxLength ? value : value[..maxLength];
    }
}
