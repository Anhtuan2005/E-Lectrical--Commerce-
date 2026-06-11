namespace EcommerceApp.Models.ViewModels;

public class CartViewModel
{
    public IEnumerable<CartItem> Items { get; set; } = Enumerable.Empty<CartItem>();
    public IReadOnlyDictionary<int, decimal> CrossSellUnitPrices { get; set; } = new Dictionary<int, decimal>();
    public IReadOnlyList<CrossSellSuggestionViewModel> CrossSellSuggestions { get; set; } = Array.Empty<CrossSellSuggestionViewModel>();
    public decimal GrossTotal => Items.Sum(item => GetRegularUnitPrice(item) * item.Quantity);
    public decimal CrossSellDiscountAmount => Items.Sum(item => Math.Max(0, GetRegularUnitPrice(item) - GetUnitPrice(item)) * item.Quantity);
    public decimal Total => Items.Sum(item => GetUnitPrice(item) * item.Quantity);
    public int ItemCount => Items.Sum(item => item.Quantity);

    public decimal GetUnitPrice(CartItem item)
    {
        return CrossSellUnitPrices.TryGetValue(item.ProductId, out var unitPrice)
            ? unitPrice
            : GetRegularUnitPrice(item);
    }

    public decimal GetRegularUnitPrice(CartItem item)
    {
        return item.Product?.SalePrice ?? item.Product?.Price ?? 0;
    }
}
