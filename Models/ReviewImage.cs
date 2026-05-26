using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models;

public class ReviewImage
{
    public int Id { get; set; }
    public int ReviewId { get; set; }
    public Review? Review { get; set; }

    [Required, StringLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}
