using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models;

public class Voucher
{
    public int Id { get; set; }
    [Required, StringLength(30)] public string Code { get; set; } = string.Empty;
    public VoucherType Type { get; set; }
    public decimal Value { get; set; }
    public decimal MinOrderAmount { get; set; }
    public decimal MaxDiscount { get; set; }
    public int UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<VoucherUsage> Usages { get; set; } = new List<VoucherUsage>();
}

public enum VoucherType
{
    Percent,
    FixedAmount
}
