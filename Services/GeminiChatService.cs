using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Services;

public class GeminiChatService : IAiChatService
{
    private const int MaxHistoryItems = 8;
    private const int MaxProductsInPrompt = 18;
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiChatService> _logger;

    public GeminiChatService(
        HttpClient httpClient,
        AppDbContext db,
        IConfiguration configuration,
        ILogger<GeminiChatService> logger)
    {
        _httpClient = httpClient;
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AiChatResponse> AskAsync(string message, IReadOnlyList<AiChatHistoryItem> history, CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["Gemini:ApiKey"] ?? _configuration["GEMINI_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return TextOnly("Chatbot AI chưa được cấu hình API key. Vui lòng thêm Gemini:ApiKey hoặc biến môi trường GEMINI_API_KEY.");
        }

        var model = _configuration["Gemini:Model"] ?? "gemini-3.5-flash";
        var baseUrl = (_configuration["Gemini:BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1beta").TrimEnd('/');
        var endpoint = $"{baseUrl}/{NormalizeModelPath(model)}:generateContent";
        var catalogContext = await BuildCatalogContextAsync(message, cancellationToken);

        var request = new GeminiGenerateRequest
        {
            SystemInstruction = new GeminiContent
            {
                Parts = new List<GeminiPart>
                {
                    new() { Text = BuildSystemInstruction(catalogContext) }
                }
            },
            Contents = BuildConversation(message, history),
            GenerationConfig = new GeminiGenerationConfig
            {
                Temperature = 0.55,
                TopP = 0.9,
                MaxOutputTokens = 2200,
                ThinkingConfig = BuildThinkingConfig(model)
            }
        };

        try
        {
            using var response = await SendGeminiRequestWithRetryAsync(endpoint, apiKey, request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Gemini API returned {StatusCode}: {Body}", response.StatusCode, error);
                return TextOnly("Mình chưa kết nối được Gemini lúc này. Bạn thử lại sau ít phút nhé.");
            }

            var result = await response.Content.ReadFromJsonAsync<GeminiGenerateResponse>(JsonOptions, cancellationToken);
            var answer = ExtractText(result);
            if (string.IsNullOrWhiteSpace(answer))
            {
                return TextOnly("Mình chưa có đủ dữ liệu để tư vấn chắc chắn. Bạn cho mình thêm nhu cầu, ngân sách và mục đích sử dụng nhé.");
            }

            return new AiChatResponse
            {
                Reply = answer.Trim(),
                Products = await BuildProductLinksAsync(answer, cancellationToken)
            };
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return TextOnly("Gemini phản hồi hơi lâu. Bạn thử gửi lại câu hỏi ngắn hơn giúp mình nhé.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Gemini API request failed.");
            return TextOnly("Mình đang gặp lỗi kết nối AI. Bạn thử lại sau một chút nhé.");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Gemini API response could not be parsed.");
            return TextOnly("Mình nhận được phản hồi AI nhưng chưa đọc được nội dung. Bạn thử lại giúp mình nhé.");
        }
    }

    private async Task<List<AiChatProductLink>> BuildProductLinksAsync(string answer, CancellationToken cancellationToken)
    {
        var ids = ExtractMentionedProductIds(answer).Take(6).ToList();
        var query = _db.Products
            .AsNoTracking()
            .Include(product => product.Category)
            .Where(product => product.Stock > 0);

        var products = ids.Count > 0
            ? await query.Where(product => ids.Contains(product.Id)).ToListAsync(cancellationToken)
            : await query
                .OrderByDescending(product => product.IsFeatured)
                .ThenByDescending(product => product.DiscountPercent)
                .Take(30)
                .ToListAsync(cancellationToken);

        if (ids.Count == 0)
        {
            products = products
                .Where(product => answer.Contains(product.Name, StringComparison.OrdinalIgnoreCase))
                .Take(4)
                .ToList();
        }

        return products
            .OrderBy(product => ids.IndexOf(product.Id) < 0 ? int.MaxValue : ids.IndexOf(product.Id))
            .Take(4)
            .Select(product => new AiChatProductLink
            {
                Id = product.Id,
                Name = product.Name,
                Category = product.Category?.Name ?? "Sản phẩm",
                Price = FormatMoney(product.SalePrice),
                Url = $"/Product/Detail/{product.Id}"
            })
            .ToList();
    }

    private static AiChatResponse TextOnly(string reply)
    {
        return new AiChatResponse { Reply = reply };
    }

    private async Task<HttpResponseMessage> SendGeminiRequestWithRetryAsync(string endpoint, string apiKey, GeminiGenerateRequest request, CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(request, options: JsonOptions)
            };
            message.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);

            var response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!ShouldRetry(response.StatusCode) || attempt == maxAttempts)
            {
                return response;
            }

            _logger.LogWarning("Gemini API returned {StatusCode}. Retrying attempt {NextAttempt}/{MaxAttempts}.", response.StatusCode, attempt + 1, maxAttempts);
            response.Dispose();
            await Task.Delay(TimeSpan.FromMilliseconds(300 * attempt * attempt), cancellationToken);
        }

        throw new InvalidOperationException("Gemini retry loop ended unexpectedly.");
    }

    private static bool ShouldRetry(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;
    }

    private static List<int> ExtractMentionedProductIds(string answer)
    {
        return Regex.Matches(answer, @"(?:/Product/Detail/|#)(\d+)", RegexOptions.IgnoreCase)
            .Select(match => int.TryParse(match.Groups[1].Value, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
    }

    private static GeminiThinkingConfig? BuildThinkingConfig(string model)
    {
        var normalized = model.Trim().ToLowerInvariant();

        if (normalized.StartsWith("gemini-3", StringComparison.Ordinal))
        {
            return new GeminiThinkingConfig { ThinkingLevel = "minimal" };
        }

        if (normalized.StartsWith("gemini-2.5", StringComparison.Ordinal))
        {
            return new GeminiThinkingConfig { ThinkingBudget = 0 };
        }

        return null;
    }

    private async Task<string> BuildCatalogContextAsync(string message, CancellationToken cancellationToken)
    {
        var products = await _db.Products
            .AsNoTracking()
            .Include(product => product.Category)
            .Where(product => product.Stock > 0)
            .OrderByDescending(product => product.IsFeatured)
            .ThenByDescending(product => product.DiscountPercent)
            .ThenByDescending(product => product.CreatedAt)
            .Take(80)
            .ToListAsync(cancellationToken);

        if (products.Count == 0)
        {
            return "Hiện chưa có sản phẩm còn hàng trong catalog.";
        }

        var terms = ExtractSearchTerms(message);
        var rankedProducts = products
            .Select(product => new
            {
                Product = product,
                Score = ScoreProduct(product, terms)
            })
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Product.IsFeatured)
            .ThenByDescending(item => item.Product.DiscountPercent)
            .ThenBy(item => item.Product.SalePrice)
            .Take(MaxProductsInPrompt)
            .Select(item => item.Product)
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine("Catalog sản phẩm còn hàng của Techvora:");
        foreach (var product in rankedProducts)
        {
            var discount = product.DiscountPercent > 0 ? $", giảm {product.DiscountPercent}%" : string.Empty;
            builder
                .Append("- #")
                .Append(product.Id)
                .Append(": ")
                .Append(product.Name)
                .Append(" | Danh mục: ")
                .Append(product.Category?.Name ?? "Chưa phân loại")
                .Append(" | Giá bán: ")
                .Append(FormatMoney(product.SalePrice))
                .Append(discount)
                .Append(" | Tồn: ")
                .Append(product.Stock)
                .Append(" | Link: /Product/Detail/")
                .Append(product.Id);

            var description = CompactText(product.Description, 180);
            if (!string.IsNullOrWhiteSpace(description))
            {
                builder.Append(" | Mô tả: ").Append(description);
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string BuildSystemInstruction(string catalogContext)
    {
        return $"""
Bạn là trợ lý tư vấn mua sắm của Techvora, trả lời bằng tiếng Việt tự nhiên, ngắn gọn và hữu ích.
Nhiệm vụ: hỏi thêm khi thiếu nhu cầu, tư vấn điện thoại, laptop, linh kiện, phụ kiện và cấu hình PC theo ngân sách.
Chỉ dùng dữ liệu sản phẩm được cung cấp bên dưới khi nói về tên sản phẩm, giá, tồn kho hoặc link. Không bịa sản phẩm, giá hay khuyến mãi.
Khi gợi ý sản phẩm, chọn tối đa 4 lựa chọn. Mỗi lựa chọn phải ghi đủ: tên sản phẩm, mã #id, giá bán, lý do ngắn và link /Product/Detail/id.
Ưu tiên trả lời trong 4 đến 7 dòng. Hoàn thành câu trả lời, không dừng giữa câu.
Nếu câu hỏi không liên quan mua sắm công nghệ, lịch sự kéo cuộc trò chuyện về tư vấn sản phẩm Techvora.
Không cam kết giá tuyệt đối; nhắc khách kiểm tra trang chi tiết trước khi mua nếu cần.

{catalogContext}
""";
    }

    private static List<GeminiContent> BuildConversation(string message, IReadOnlyList<AiChatHistoryItem> history)
    {
        var contents = new List<GeminiContent>();
        foreach (var item in history.TakeLast(MaxHistoryItems))
        {
            var text = CompactText(item.Text, 1200);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            contents.Add(new GeminiContent
            {
                Role = item.Role.Equals("model", StringComparison.OrdinalIgnoreCase) ? "model" : "user",
                Parts = new List<GeminiPart> { new() { Text = text } }
            });
        }

        contents.Add(new GeminiContent
        {
            Role = "user",
            Parts = new List<GeminiPart> { new() { Text = CompactText(message, 1600) } }
        });

        return contents;
    }

    private static string ExtractText(GeminiGenerateResponse? response)
    {
        if (response?.Candidates is null)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            response.Candidates
                .SelectMany(candidate => candidate.Content?.Parts ?? new List<GeminiPart>())
                .Select(part => part.Text)
                .Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private static int ScoreProduct(Product product, string[] terms)
    {
        var haystack = NormalizeSearchText($"{product.Name} {product.Category?.Name} {product.Description}");
        var score = product.IsFeatured ? 6 : 0;
        score += product.DiscountPercent > 0 ? 4 : 0;

        foreach (var term in terms)
        {
            if (haystack.Contains(term, StringComparison.Ordinal))
            {
                score += 10;
            }
        }

        return score;
    }

    private static string[] ExtractSearchTerms(string message)
    {
        return NormalizeSearchText(message)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => term.Length >= 2)
            .Distinct()
            .Take(12)
            .ToArray();
    }

    private static string NormalizeSearchText(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : ' ');
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string CompactText(string value, int maxLength)
    {
        var compact = string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= maxLength ? compact : compact[..maxLength].TrimEnd() + "...";
    }

    private static string FormatMoney(decimal value)
    {
        return value.ToString("N0", VietnameseCulture) + " đ";
    }

    private static string NormalizeModelPath(string model)
    {
        var trimmed = model.Trim().Trim('/');
        return trimmed.StartsWith("models/", StringComparison.OrdinalIgnoreCase) ? trimmed : $"models/{trimmed}";
    }

    private sealed class GeminiGenerateRequest
    {
        [JsonPropertyName("contents")]
        public List<GeminiContent> Contents { get; set; } = new();

        [JsonPropertyName("systemInstruction")]
        public GeminiContent? SystemInstruction { get; set; }

        [JsonPropertyName("generationConfig")]
        public GeminiGenerationConfig? GenerationConfig { get; set; }
    }

    private sealed class GeminiContent
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("parts")]
        public List<GeminiPart> Parts { get; set; } = new();
    }

    private sealed class GeminiPart
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private sealed class GeminiGenerationConfig
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("topP")]
        public double TopP { get; set; }

        [JsonPropertyName("maxOutputTokens")]
        public int MaxOutputTokens { get; set; }

        [JsonPropertyName("thinkingConfig")]
        public GeminiThinkingConfig? ThinkingConfig { get; set; }
    }

    private sealed class GeminiThinkingConfig
    {
        [JsonPropertyName("thinkingBudget")]
        public int? ThinkingBudget { get; set; }

        [JsonPropertyName("thinkingLevel")]
        public string? ThinkingLevel { get; set; }
    }

    private sealed class GeminiGenerateResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate>? Candidates { get; set; }
    }

    private sealed class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; set; }
    }
}
