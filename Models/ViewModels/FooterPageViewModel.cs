namespace EcommerceApp.Models.ViewModels;

public class FooterPageViewModel
{
    public string Eyebrow { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Intro { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = "Xem sản phẩm";
    public string ActionUrl { get; set; } = "/Product";
    public IReadOnlyList<FooterPageSectionViewModel> Sections { get; set; } = Array.Empty<FooterPageSectionViewModel>();
}

public class FooterPageSectionViewModel
{
    public string Title { get; set; } = string.Empty;
    public IReadOnlyList<string> Items { get; set; } = Array.Empty<string>();
}
