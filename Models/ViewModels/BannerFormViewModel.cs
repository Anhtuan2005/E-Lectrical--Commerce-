using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models.ViewModels;

public class BannerFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề.")]
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? ImageUrl { get; set; }
    public string? LinkUrl { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập nội dung nút.")]
    public string ButtonText { get; set; } = "Khám phá ngay";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
