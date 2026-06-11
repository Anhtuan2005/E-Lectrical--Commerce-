using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models;

public class CustomerSegment
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [StringLength(500)]
    public string RuleDescription { get; set; } = string.Empty;

    [StringLength(500)]
    public string RecommendedAction { get; set; } = string.Empty;

    public bool IsSystem { get; set; } = true;

    public ICollection<CustomerSegmentMember> Members { get; set; } = new List<CustomerSegmentMember>();
    public ICollection<Voucher> Vouchers { get; set; } = new List<Voucher>();
}

public static class CustomerSegmentCodes
{
    public const string NewCustomer = "NEW_CUSTOMER";
    public const string Vip = "VIP";
    public const string Inactive = "INACTIVE";
}
