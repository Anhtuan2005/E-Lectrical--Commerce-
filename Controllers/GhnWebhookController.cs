using EcommerceApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace EcommerceApp.Controllers;

[AllowAnonymous]
[Route("shipping")]
public class GhnWebhookController : Controller
{
    private readonly IGhnShippingService _ghnShippingService;
    private readonly GhnOptions _options;
    private readonly ILogger<GhnWebhookController> _logger;

    public GhnWebhookController(
        IGhnShippingService ghnShippingService,
        IOptions<GhnOptions> options,
        ILogger<GhnWebhookController> logger)
    {
        _ghnShippingService = ghnShippingService;
        _options = options.Value;
        _logger = logger;
    }

    [HttpPost("ghn-webhook")]
    public async Task<IActionResult> GhnWebhook([FromBody] GhnWebhookPayload payload, [FromQuery] string? secret)
    {
        var secretValid = HasValidSecret(secret);
        var shopIdValid = int.TryParse(_options.ShopId, out var shopId) && payload.ShopID == shopId;
        if (!secretValid || !shopIdValid)
        {
            _logger.LogWarning(
                "Rejected an unauthenticated GHN webhook. SecretValid={SecretValid}, ShopIdValid={ShopIdValid}, ConfiguredShopId={ConfiguredShopId}, PayloadShopId={PayloadShopId}",
                secretValid,
                shopIdValid,
                _options.ShopId,
                payload.ShopID);
            return Unauthorized();
        }

        var result = await _ghnShippingService.ApplyWebhookAsync(payload);
        if (!result.Success)
        {
            _logger.LogWarning("GHN webhook was received but not applied: {Message}", result.Message);
        }

        return Ok(new { message = result.Message });
    }

    private bool HasValidSecret(string? providedSecret)
    {
        var expected = Encoding.UTF8.GetBytes(_options.WebhookSecret ?? string.Empty);
        var provided = Encoding.UTF8.GetBytes(providedSecret ?? string.Empty);
        return expected.Length > 0 &&
            expected.Length == provided.Length &&
            CryptographicOperations.FixedTimeEquals(expected, provided);
    }
}
