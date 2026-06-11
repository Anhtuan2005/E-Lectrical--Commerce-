namespace EcommerceApp.Models.ViewModels;

public class CrossSellSuggestionViewModel
{
    public int OfferId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string AnchorProductName { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = "/images/placeholder.svg";
    public int DiscountPercent { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal OfferPrice { get; set; }
    public decimal Savings => Math.Max(0, OriginalPrice - OfferPrice);
}
