namespace EcommerceApp.Models.ViewModels;

public class AiChatRequest
{
    public string Message { get; set; } = string.Empty;
    public List<AiChatHistoryItem> History { get; set; } = new();
}

public class AiChatHistoryItem
{
    public string Role { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public class AiChatResponse
{
    public string Reply { get; set; } = string.Empty;
    public List<AiChatProductLink> Products { get; set; } = new();
}

public class AiChatProductLink
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
