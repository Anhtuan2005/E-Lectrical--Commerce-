using System.Globalization;

namespace EcommerceApp.Services;

public static class CsvFormatter
{
    private static readonly char[] FormulaPrefixes = { '=', '+', '-', '@', '\t', '\r', '\n' };

    public static string Row(params object?[] values) =>
        string.Join(',', values.Select(value => Cell(Format(value))));

    public static string Cell(string? value)
    {
        var safe = value ?? string.Empty;
        var trimmed = safe.TrimStart();
        if (trimmed.Length > 0 && FormulaPrefixes.Contains(trimmed[0]))
        {
            safe = "'" + safe;
        }

        return $"\"{safe.Replace("\"", "\"\"")}\"";
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTimeOffset date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => value.ToString() ?? string.Empty
    };
}
