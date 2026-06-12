namespace EcommerceApp.Models;

public class CartItem
{
    public int Id { get; set; }
    public int CartId { get; set; }
    public Cart? Cart { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
    public string? GroupKey { get; set; }
    public string? GroupName { get; set; }
    public string? GroupSource { get; set; }
    public string? GroupItemLabel { get; set; }
    public int? GroupSortOrder { get; set; }
}
