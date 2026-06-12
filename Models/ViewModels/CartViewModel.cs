namespace EcommerceApp.Models.ViewModels;

public class CartViewModel
{
    public IEnumerable<CartItem> Items { get; set; } = Enumerable.Empty<CartItem>();
    public IReadOnlyDictionary<int, decimal> CrossSellUnitPrices { get; set; } = new Dictionary<int, decimal>();
    public IReadOnlyList<CrossSellSuggestionViewModel> CrossSellSuggestions { get; set; } = Array.Empty<CrossSellSuggestionViewModel>();
    public IReadOnlyList<CartItemGroupViewModel> ItemGroups => Items
        .Where(item => !string.IsNullOrWhiteSpace(item.GroupKey))
        .GroupBy(item => item.GroupKey!)
        .Select(group =>
        {
            var first = group.First();
            return new CartItemGroupViewModel
            {
                Key = group.Key,
                Name = string.IsNullOrWhiteSpace(first.GroupName) ? "Bộ cấu hình Smart PC" : first.GroupName!,
                Source = string.IsNullOrWhiteSpace(first.GroupSource) ? "SmartPC" : first.GroupSource!,
                Items = group
                    .OrderBy(item => item.GroupSortOrder ?? 999)
                    .ThenBy(item => item.Product?.Name)
                    .ToList()
            };
        })
        .ToList();
    public IEnumerable<CartItem> StandaloneItems => Items.Where(item => string.IsNullOrWhiteSpace(item.GroupKey));
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

    public decimal GetGroupTotal(CartItemGroupViewModel group)
    {
        return group.Items.Sum(item => GetUnitPrice(item) * item.Quantity);
    }
}

public class CartItemGroupViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public IReadOnlyList<CartItem> Items { get; set; } = Array.Empty<CartItem>();
    public int ComponentCount => Items.Count;
    public int QuantityCount => Items.Sum(item => item.Quantity);
}
