using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models;

public class CrossSellOffer
{
    public int Id { get; set; }

    public int AnchorProductId { get; set; }
    public Product? AnchorProduct { get; set; }

    public int AddOnProductId { get; set; }
    public Product? AddOnProduct { get; set; }

    [Range(1, 90)]
    public int DiscountPercent { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
