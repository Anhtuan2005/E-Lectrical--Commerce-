using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models;

public class Banner
{
    public int Id { get; set; }
    [Required] public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string LinkUrl { get; set; } = string.Empty;
    public string ButtonText { get; set; } = "Khám phá ngay";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
