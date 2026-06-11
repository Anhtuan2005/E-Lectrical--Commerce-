using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models;

public class ProductSearchTerm
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Required, StringLength(80)]
    public string Term { get; set; } = string.Empty;
}
