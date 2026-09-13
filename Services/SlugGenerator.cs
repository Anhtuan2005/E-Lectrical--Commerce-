using System.Globalization;
using System.Text;

namespace EcommerceApp.Services;

public static class SlugGenerator
{
    public static string Generate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(normalized.Length);
        var pendingSeparator = false;

        foreach (var sourceChar in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(sourceChar) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var ch = sourceChar switch
            {
                'đ' or 'Đ' => 'd',
                _ => char.ToLowerInvariant(sourceChar)
            };

            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pendingSeparator && result.Length > 0)
                {
                    result.Append('-');
                }

                result.Append(ch);
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = result.Length > 0;
            }
        }

        return result.ToString();
    }
}
