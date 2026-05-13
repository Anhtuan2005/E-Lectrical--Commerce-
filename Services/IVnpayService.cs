using EcommerceApp.Models;

namespace EcommerceApp.Services;

public interface IVnpayService
{
    string CreatePaymentUrl(Order order, HttpContext context);
    VnpayResponse ProcessCallback(IQueryCollection query);
}

public class VnpayResponse
{
    public bool IsSuccess { get; set; }
    public bool IsSignatureValid { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string ResponseCode { get; set; } = string.Empty;
    public string TransactionStatus { get; set; } = string.Empty;
}
