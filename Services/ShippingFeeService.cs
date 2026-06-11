using System.Globalization;
using System.Text;

namespace EcommerceApp.Services;

public class ShippingFeeService : IShippingFeeService
{
    private const decimal FreeShippingThreshold = 30_000_000m;
    private const decimal ReducedShippingThreshold = 15_000_000m;

    private static readonly string[] SameDayProvinces = { "ho chi minh" };
    private static readonly string[] NearProvinces = { "binh duong", "dong nai", "long an", "ba ria vung tau" };
    private static readonly string[] MajorCityProvinces = { "ha noi", "da nang", "can tho", "hai phong" };
    private static readonly string[] RemoteProvinces = { "dak lak", "gia lai", "ca mau", "nghe an", "thanh hoa", "quang ninh" };

    public ShippingFeeQuote Calculate(string? province, string? district, decimal subtotal)
    {
        if (string.IsNullOrWhiteSpace(province))
        {
            return new ShippingFeeQuote
            {
                Ready = false,
                Fee = 0m,
                Zone = "Chưa chọn địa điểm",
                Eta = string.Empty,
                Message = "Chọn tỉnh/thành phố để tính phí vận chuyển."
            };
        }

        var normalizedProvince = Normalize(province);
        var normalizedDistrict = Normalize(district);
        var zone = "Toàn quốc";
        var eta = "3-5 ngày";
        var fee = 45_000m;

        if (SameDayProvinces.Any(item => normalizedProvince.Contains(item, StringComparison.Ordinal)))
        {
            zone = string.IsNullOrWhiteSpace(normalizedDistrict) ? "TP.HCM" : $"TP.HCM - {district}";
            eta = "1-2 ngày";
            fee = 20_000m;
        }
        else if (NearProvinces.Any(item => normalizedProvince.Contains(item, StringComparison.Ordinal)))
        {
            zone = "Vùng lân cận TP.HCM";
            eta = "2-3 ngày";
            fee = 30_000m;
        }
        else if (MajorCityProvinces.Any(item => normalizedProvince.Contains(item, StringComparison.Ordinal)))
        {
            zone = "Thành phố lớn";
            eta = "2-4 ngày";
            fee = 35_000m;
        }
        else if (RemoteProvinces.Any(item => normalizedProvince.Contains(item, StringComparison.Ordinal)))
        {
            zone = "Tuyến xa";
            eta = "4-6 ngày";
            fee = 60_000m;
        }

        if (subtotal >= FreeShippingThreshold)
        {
            return new ShippingFeeQuote
            {
                Ready = true,
                Fee = 0m,
                Zone = zone,
                Eta = eta,
                Message = "Miễn phí vận chuyển cho đơn từ 30.000.000 ₫."
            };
        }

        if (subtotal >= ReducedShippingThreshold)
        {
            fee = Math.Max(0m, fee - 10_000m);
        }

        return new ShippingFeeQuote
        {
            Ready = true,
            Fee = fee,
            Zone = zone,
            Eta = eta,
            Message = $"Dự kiến giao {eta}."
        };
    }

    private static string Normalize(string? value)
    {
        var normalized = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(ch switch
            {
                'đ' or 'Đ' => 'd',
                _ => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : ' '
            });
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
