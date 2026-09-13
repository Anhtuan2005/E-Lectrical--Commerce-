using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models;

public class Voucher : IValidatableObject
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập mã voucher")]
    [StringLength(30, ErrorMessage = "Mã voucher không được vượt quá 30 ký tự")]
    public string Code { get; set; } = string.Empty;

    [EnumDataType(typeof(VoucherType), ErrorMessage = "Loại voucher không hợp lệ")]
    public VoucherType Type { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Giá trị giảm phải lớn hơn 0")]
    public decimal Value { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Đơn tối thiểu không được âm")]
    public decimal MinOrderAmount { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Mức giảm tối đa không được âm")]
    public decimal MaxDiscount { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Giới hạn lượt dùng phải từ 1 trở lên")]
    public int UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; } = true;

    public int? CustomerSegmentId { get; set; }
    public CustomerSegment? CustomerSegment { get; set; }

    public string? TargetUserId { get; set; }
    public ApplicationUser? TargetUser { get; set; }

    public ICollection<VoucherUsage> Usages { get; set; } = new List<VoucherUsage>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        const decimal maxDatabaseAmount = 9_999_999_999_999_999.99m;

        if (Value > maxDatabaseAmount)
        {
            yield return new ValidationResult("Giá trị giảm vượt quá giới hạn cho phép", new[] { nameof(Value) });
        }

        if (MinOrderAmount > maxDatabaseAmount)
        {
            yield return new ValidationResult("Đơn tối thiểu vượt quá giới hạn cho phép", new[] { nameof(MinOrderAmount) });
        }

        if (MaxDiscount > maxDatabaseAmount)
        {
            yield return new ValidationResult("Mức giảm tối đa vượt quá giới hạn cho phép", new[] { nameof(MaxDiscount) });
        }

        if (Type == VoucherType.Percent && Value > 100m)
        {
            yield return new ValidationResult("Phần trăm giảm không được vượt quá 100%", new[] { nameof(Value) });
        }

        if (EndDate <= StartDate)
        {
            yield return new ValidationResult("Ngày kết thúc phải sau ngày bắt đầu", new[] { nameof(EndDate) });
        }
    }
}

public enum VoucherType
{
    Percent,
    FixedAmount
}
