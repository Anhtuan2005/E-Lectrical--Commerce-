using EcommerceApp.Models;

namespace EcommerceApp.Models.ViewModels;

public class BuildPcViewModel
{
    public Dictionary<string, List<Product>> SlotProducts { get; set; } = new();
}
