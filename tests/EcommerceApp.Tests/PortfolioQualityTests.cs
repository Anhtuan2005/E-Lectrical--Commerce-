using System.ComponentModel.DataAnnotations;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using EcommerceApp.Services;

namespace EcommerceApp.Tests;

public class PortfolioQualityTests
{
    [Theory]
    [InlineData("Điện thoại & Phụ kiện", "dien-thoai-phu-kien")]
    [InlineData("  Máy tính -- để bàn  ", "may-tinh-de-ban")]
    [InlineData("CPU Intel® Core™ i5", "cpu-intel-core-i5")]
    public void Slug_generator_preserves_vietnamese_words(string input, string expected)
    {
        Assert.Equal(expected, SlugGenerator.Generate(input));
    }

    [Fact]
    public void Csv_formatter_escapes_quotes_uses_invariant_numbers_and_blocks_formulas()
    {
        var row = CsvFormatter.Row("=2+2", "Nguyễn \"An\"", 1_234.50m);

        Assert.Equal("\"'=2+2\",\"Nguyễn \"\"An\"\"\",\"1234.50\"", row);
    }

    [Fact]
    public void Shipping_rule_matches_the_500_thousand_copy_shown_to_customers()
    {
        var service = new ShippingFeeService();

        Assert.Equal(20_000m, service.Calculate("Hồ Chí Minh", "Quận 1", 499_999m).Fee);
        var freeQuote = service.Calculate("Hồ Chí Minh", "Quận 1", 500_000m);
        Assert.Equal(0m, freeQuote.Fee);
        Assert.Contains("500.000", freeQuote.Message);
    }

    [Theory]
    [MemberData(nameof(InvalidVouchers))]
    public void Voucher_rejects_invalid_admin_input(Voucher voucher, string property)
    {
        var results = Validate(voucher);

        Assert.Contains(results, result => result.MemberNames.Contains(property));
    }

    [Fact]
    public void Account_inputs_match_database_length_limits()
    {
        var register = new RegisterViewModel
        {
            FullName = new string('A', 121),
            Email = "user@example.com",
            PhoneNumber = "0901234567",
            Address = new string('A', 251),
            Password = "Password1",
            ConfirmPassword = "Password1"
        };

        var results = Validate(register);
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(RegisterViewModel.FullName)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(RegisterViewModel.Address)));
    }

    public static IEnumerable<object[]> InvalidVouchers()
    {
        var zeroValue = ValidVoucher();
        zeroValue.Value = 0m;
        yield return new object[] { zeroValue, nameof(Voucher.Value) };

        var excessivePercent = ValidVoucher();
        excessivePercent.Value = 101m;
        yield return new object[] { excessivePercent, nameof(Voucher.Value) };

        var zeroLimit = ValidVoucher();
        zeroLimit.UsageLimit = 0;
        yield return new object[] { zeroLimit, nameof(Voucher.UsageLimit) };

        var invalidDates = ValidVoucher();
        invalidDates.EndDate = invalidDates.StartDate;
        yield return new object[] { invalidDates, nameof(Voucher.EndDate) };
    }

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    private static Voucher ValidVoucher() => new()
    {
        Code = "WELCOME10",
        Type = VoucherType.Percent,
        Value = 10m,
        UsageLimit = 100,
        StartDate = DateTime.UtcNow.AddDays(-1),
        EndDate = DateTime.UtcNow.AddDays(1)
    };

}
