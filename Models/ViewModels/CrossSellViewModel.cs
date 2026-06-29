namespace EcommerceApp.Models.ViewModels;

public class CrossSellSuggestionViewModel
{
    public int OfferId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string AnchorProductName { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = "/images/placeholder.svg";
    public string Source { get; set; } = "manual";
    public decimal ConfidencePercent { get; set; }
    public int SupportCount { get; set; }
    public int DiscountPercent { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal OfferPrice { get; set; }
    public decimal Savings => Math.Max(0, OriginalPrice - OfferPrice);
    public bool HasDiscount => DiscountPercent > 0 && OfferPrice < OriginalPrice;
    public string BadgeText => HasDiscount ? $"-{DiscountPercent}%" : "Gợi ý";
    public string ContextText => Source == "apriori"
        ? $"Hay mua cùng {AnchorProductName}"
        : $"Kèm {AnchorProductName}";
}
