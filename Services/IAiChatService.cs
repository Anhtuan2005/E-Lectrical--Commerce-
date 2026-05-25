using EcommerceApp.Models.ViewModels;

namespace EcommerceApp.Services;

public interface IAiChatService
{
    Task<AiChatResponse> AskAsync(string message, IReadOnlyList<AiChatHistoryItem> history, CancellationToken cancellationToken = default);
}
