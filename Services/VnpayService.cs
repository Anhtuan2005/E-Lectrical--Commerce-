using EcommerceApp.Models;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace EcommerceApp.Services;

public class VnpayService : IVnpayService
{
    private readonly IConfiguration _configuration;

    public VnpayService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string CreatePaymentUrl(Order order, HttpContext context)
    {
        var section = _configuration.GetSection("Vnpay");
        var baseUrl = section["BaseUrl"] ?? throw new InvalidOperationException("Thiếu VNPAY BaseUrl.");
        var tmnCode = section["TmnCode"] ?? throw new InvalidOperationException("Thiếu VNPAY TmnCode.");
        var hashSecret = section["HashSecret"] ?? throw new InvalidOperationException("Thiếu VNPAY HashSecret.");
        var returnUrl = section["ReturnUrl"] ?? throw new InvalidOperationException("Thiếu VNPAY ReturnUrl.");

        var now = DateTime.Now;
        var txnRef = $"{order.Id}_{now:yyyyMMddHHmmss}";
        var ipAddr = context.Connection.RemoteIpAddress?.ToString();
        if (string.IsNullOrWhiteSpace(ipAddr) || ipAddr == "::1")
        {
            ipAddr = "127.0.0.1";
        }

        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = tmnCode,
            ["vnp_Amount"] = ((long)(order.TotalAmount * 100m)).ToString(CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = "VND",
            ["vnp_TxnRef"] = txnRef,
            ["vnp_OrderInfo"] = $"Thanh toan don hang Voltix so {order.Id}",
            ["vnp_OrderType"] = "other",
            ["vnp_Locale"] = "vn",
            ["vnp_ReturnUrl"] = returnUrl,
            ["vnp_IpAddr"] = ipAddr,
            ["vnp_CreateDate"] = now.ToString("yyyyMMddHHmmss"),
            ["vnp_ExpireDate"] = now.AddMinutes(15).ToString("yyyyMMddHHmmss")
        };

        var queryToSign = BuildRawQuery(parameters);
        var secureHash = HmacSha512(hashSecret, queryToSign);

        return $"{baseUrl}?{queryToSign}&vnp_SecureHash={secureHash}";
    }

    public VnpayResponse ProcessCallback(IQueryCollection query)
    {
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in query)
        {
            if (!kv.Key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (kv.Key.Equals("vnp_SecureHash", StringComparison.OrdinalIgnoreCase)
                || kv.Key.Equals("vnp_SecureHashType", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            fields[kv.Key] = kv.Value.ToString();
        }

        var rawData = BuildRawQuery(fields);
        var expectedHash = HmacSha512(_configuration["Vnpay:HashSecret"] ?? string.Empty, rawData);
        var actualHash = query["vnp_SecureHash"].ToString();
        var sigValid = expectedHash.Equals(actualHash, StringComparison.OrdinalIgnoreCase);

        var txnRef = query["vnp_TxnRef"].ToString();
        var orderId = txnRef.Split('_')[0];
        var responseCode = query["vnp_ResponseCode"].ToString();
        var amount = decimal.TryParse(query["vnp_Amount"], out var raw) ? raw / 100m : 0m;

        return new VnpayResponse
        {
            IsSignatureValid = sigValid,
            IsSuccess = sigValid && responseCode == "00",
            TransactionId = query["vnp_TransactionNo"].ToString(),
            OrderId = orderId,
            Amount = amount,
            ResponseCode = responseCode
        };
    }

    private static string BuildRawQuery(SortedDictionary<string, string> data)
    {
        return string.Join("&", data
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => $"{item.Key}={Uri.EscapeDataString(item.Value)}"));
    }

    private static string HmacSha512(string key, string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        using var hmac = new HMACSHA512(keyBytes);
        return Convert.ToHexString(hmac.ComputeHash(dataBytes)).ToLowerInvariant();
    }
}
