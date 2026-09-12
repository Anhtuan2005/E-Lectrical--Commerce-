using EcommerceApp.Models;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;

namespace EcommerceApp.Tests;

public sealed class PaymentDeadlineTests
{
    [Fact]
    public void Retrying_payment_keeps_original_deadline_and_uses_Vietnam_time()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Vnpay:BaseUrl"] = "https://payments.example.test/pay", ["Vnpay:TmnCode"] = "test",
            ["Vnpay:HashSecret"] = "test-secret", ["Vnpay:ReturnUrl"] = "https://store.example.test/payment/vnpay-return"
        }).Build();
        var service = new VnpayService(configuration);
        var order = new Order { Id = 1, CreatedAt = DateTime.UtcNow.AddMinutes(-5), PaymentExpiresAt = DateTime.UtcNow.AddMinutes(10) };
        var query = QueryHelpers.ParseQuery(new Uri(service.CreatePaymentUrl(order, new DefaultHttpContext())).Query);
        Assert.Equal(order.PaymentExpiresAt.Value.AddHours(7).ToString("yyyyMMddHHmmss"), query["vnp_ExpireDate"].ToString());
        order.PaymentExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        Assert.Throws<InvalidOperationException>(() => service.CreatePaymentUrl(order, new DefaultHttpContext()));
    }
}
