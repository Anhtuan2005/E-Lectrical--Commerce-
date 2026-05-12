namespace EcommerceApp.Models.ViewModels;

public class CartViewModel
{
    public IEnumerable<CartItem> Items { get; set; } = Enumerable.Empty<CartItem>();
    public decimal Total => Items.Sum(item => (item.Product?.SalePrice ?? item.Product?.Price ?? 0) * item.Quantity);
    public int ItemCount => Items.Sum(item => item.Quantity);
}
