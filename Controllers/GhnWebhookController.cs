using EcommerceApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceApp.Controllers;

[AllowAnonymous]
[Route("shipping")]
public class GhnWebhookController : Controller
{
    private readonly IGhnShippingService _ghnShippingService;
    private readonly ILogger<GhnWebhookController> _logger;

    public GhnWebhookController(IGhnShippingService ghnShippingService, ILogger<GhnWebhookController> logger)
    {
        _ghnShippingService = ghnShippingService;
        _logger = logger;
    }

    [HttpPost("ghn-webhook")]
    public async Task<IActionResult> GhnWebhook([FromBody] GhnWebhookPayload payload)
    {
        var result = await _ghnShippingService.ApplyWebhookAsync(payload);
        if (!result.Success)
        {
            _logger.LogWarning("GHN webhook was received but not applied: {Message}", result.Message);
        }

        return Ok(new { message = result.Message });
    }
}
