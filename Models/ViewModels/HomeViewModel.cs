namespace EcommerceApp.Models.ViewModels;

public class HomeViewModel
{
    public IEnumerable<Banner> Banners { get; set; } = Enumerable.Empty<Banner>();
    public IEnumerable<Category> Categories { get; set; } = Enumerable.Empty<Category>();
    public IEnumerable<Product> FeaturedProducts { get; set; } = Enumerable.Empty<Product>();
    public IEnumerable<Product> LatestProducts { get; set; } = Enumerable.Empty<Product>();
}
