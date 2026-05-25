using EcommerceApp.Models.ViewModels;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EcommerceApp.Controllers;

public class AiChatController : Controller
{
    private readonly IAiChatService _aiChatService;

    public AiChatController(IAiChatService aiChatService)
    {
        _aiChatService = aiChatService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("ai-chat")]
    public async Task<IActionResult> Ask([FromBody] AiChatRequest? request, CancellationToken cancellationToken)
    {
        var message = request?.Message?.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            return BadRequest(new
            {
                success = false,
                message = "Bạn nhập nội dung cần tư vấn trước nhé."
            });
        }

        if (message.Length > 1200)
        {
            return BadRequest(new
            {
                success = false,
                message = "Tin nhắn hơi dài. Bạn rút gọn còn khoảng 1200 ký tự giúp mình nhé."
            });
        }

        var history = request?.History?
            .Where(item => !string.IsNullOrWhiteSpace(item.Text))
            .TakeLast(8)
            .ToList() ?? new List<AiChatHistoryItem>();

        var chatResponse = await _aiChatService.AskAsync(message, history, cancellationToken);
        return Json(new
        {
            success = true,
            reply = chatResponse.Reply,
            products = chatResponse.Products
        });
    }
}
