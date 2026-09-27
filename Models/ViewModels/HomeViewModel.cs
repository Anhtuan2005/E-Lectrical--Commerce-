namespace EcommerceApp.Models.ViewModels;

public class HomeViewModel
{
    public IEnumerable<Banner> Banners { get; set; } = Enumerable.Empty<Banner>();
    public IEnumerable<PhoneBrandViewModel> Brands { get; set; } = Enumerable.Empty<PhoneBrandViewModel>();
    public IEnumerable<Product> FeaturedProducts { get; set; } = Enumerable.Empty<Product>();
    public IEnumerable<Product> PersonalizedProducts { get; set; } = Enumerable.Empty<Product>();
    public IEnumerable<Product> FlashSaleProducts { get; set; } = Enumerable.Empty<Product>();
    public IEnumerable<Product> LatestProducts { get; set; } = Enumerable.Empty<Product>();
}

public sealed record PhoneBrandViewModel(string Name, string SearchTerm, string Mark, int ProductCount);
