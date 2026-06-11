namespace EcommerceApp.Services;

public interface IShippingFeeService
{
    ShippingFeeQuote Calculate(string? province, string? district, decimal subtotal);
}

public sealed class ShippingFeeQuote
{
    public decimal Fee { get; init; }
    public string Zone { get; init; } = string.Empty;
    public string Eta { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public bool Ready { get; init; }
}
