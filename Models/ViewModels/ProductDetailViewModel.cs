namespace EcommerceApp.Models.ViewModels;

public class ProductDetailViewModel
{
    public Product Product { get; set; } = new();
    public IEnumerable<Product> RelatedProducts { get; set; } = Enumerable.Empty<Product>();
    public IEnumerable<Review> Reviews { get; set; } = Enumerable.Empty<Review>();
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public Dictionary<int, int> RatingDistribution { get; set; } = new();
    public bool IsWishlisted { get; set; }
    public bool CanReview { get; set; }
    public bool HasPurchased { get; set; }
    public bool HasReviewed { get; set; }
    public bool IsAuthenticated { get; set; }
}
