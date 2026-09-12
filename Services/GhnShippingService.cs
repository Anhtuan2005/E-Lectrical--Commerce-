using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EcommerceApp.Data;
using EcommerceApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EcommerceApp.Services;

public interface IGhnShippingService
{
    bool IsConfigured { get; }
    Task<GhnOperationResult> CreateOrderAsync(int orderId, GhnCreateOrderInput input);
    Task<GhnOperationResult> CalculateFeeAsync(int orderId, GhnCreateOrderInput input);
    Task<GhnAddressResult> GetProvincesAsync();
    Task<GhnAddressResult> GetDistrictsAsync(int provinceId);
    Task<GhnAddressResult> GetWardsAsync(int districtId);
    Task<GhnShipmentDetailResult> GetOrderDetailAsync(int orderId);
    Task<GhnShipmentDetailResult> GetOrderDetailByTrackingCodeAsync(string trackingCode);
    Task<GhnOperationResult> SyncOrderAsync(int orderId);
    Task<GhnOperationResult> CancelOrderAsync(int orderId);
    Task<GhnWebhookApplyResult> ApplyWebhookAsync(GhnWebhookPayload payload);
}

public sealed class GhnShippingService : IGhnShippingService
{
    private const string CarrierName = "Giao Hàng Nhanh";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly GhnOptions _options;
    private readonly AppDbContext _db;
    private readonly IOrderEmailService _orderEmailService;
    private readonly IUserNotificationService _notificationService;
    private readonly ILogger<GhnShippingService> _logger;

    public GhnShippingService(
        HttpClient httpClient,
        IOptions<GhnOptions> options,
        AppDbContext db,
        IOrderEmailService orderEmailService,
        IUserNotificationService notificationService,
        ILogger<GhnShippingService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _db = db;
        _orderEmailService = orderEmailService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.Token) &&
        !string.IsNullOrWhiteSpace(_options.ShopId) &&
        !string.IsNullOrWhiteSpace(_options.BaseUrl);

    public async Task<GhnOperationResult> CreateOrderAsync(int orderId, GhnCreateOrderInput input)
    {
        if (!IsConfigured)
        {
            return GhnOperationResult.Fail("Chưa cấu hình GHN Token/ShopId/BaseUrl.");
        }

        if (input.ToDistrictId <= 0 || string.IsNullOrWhiteSpace(input.ToWardCode))
        {
            return GhnOperationResult.Fail("Cần nhập District ID và Ward Code của GHN.");
        }

        var order = await LoadOrderAsync(orderId);
        if (order is null)
        {
            return GhnOperationResult.Fail("Không tìm thấy đơn hàng.");
        }

        if (!OrderLifecycle.CanShip(order))
        {
            return GhnOperationResult.Fail("Chỉ tạo vận đơn cho đơn đã sẵn sàng xử lý.");
        }

        if (!string.IsNullOrWhiteSpace(order.ShippingInfo?.TrackingCode) &&
            string.Equals(order.ShippingInfo.Carrier, CarrierName, StringComparison.OrdinalIgnoreCase))
        {
            return GhnOperationResult.Fail("Đơn này đã có vận đơn GHN.");
        }

        var request = BuildCreateOrderRequest(order, input);
        using var response = await PostAsync("v2/shipping-order/create", request);
        var payload = await ReadGhnResponseAsync(response);
        if (!response.IsSuccessStatusCode || !payload.Success)
        {
            return GhnOperationResult.Fail(payload.Message);
        }

        var orderCode = payload.Data.TryGetString("order_code");
        if (string.IsNullOrWhiteSpace(orderCode))
        {
            return GhnOperationResult.Fail("GHN không trả về mã vận đơn.");
        }

        var totalFee = payload.Data.TryGetDecimal("total_fee") ?? payload.Data.TryGetDecimal("fee");
        var expectedDelivery = payload.Data.TryGetDateTime("expected_delivery_time");
        if (!await ApplyShipmentAsync(order, orderCode, "ready_to_pick", totalFee, expectedDelivery))
        {
            _logger.LogWarning("GHN created {TrackingCode}, but order {OrderId} changed before the shipment could be saved", orderCode, order.Id);
            return GhnOperationResult.Fail($"GHN đã tạo {orderCode} nhưng trạng thái đơn đã thay đổi. Kiểm tra và huỷ vận đơn trên GHN nếu cần.");
        }
        return GhnOperationResult.Ok($"Đã tạo vận đơn GHN {orderCode}.", orderCode);
    }

    public async Task<GhnOperationResult> CalculateFeeAsync(int orderId, GhnCreateOrderInput input)
    {
        if (!IsConfigured)
        {
            return GhnOperationResult.Fail("Chưa cấu hình GHN Token/ShopId/BaseUrl.");
        }

        if (input.ToDistrictId <= 0 || string.IsNullOrWhiteSpace(input.ToWardCode))
        {
            return GhnOperationResult.Fail("Cần nhập District ID và Ward Code của GHN để tính phí.");
        }

        var order = await LoadOrderAsync(orderId);
        if (order is null)
        {
            return GhnOperationResult.Fail("Không tìm thấy đơn hàng.");
        }

        using var response = await PostAsync("v2/shipping-order/fee", BuildFeeRequest(order, input));
        var payload = await ReadGhnResponseAsync(response);
        if (!response.IsSuccessStatusCode || !payload.Success)
        {
            return GhnOperationResult.Fail(payload.Message);
        }

        var quote = BuildFeeQuote(order.Id, payload.Data);
        return quote.Total > 0
            ? GhnOperationResult.Ok($"Phí GHN dự kiến: {quote.Total:N0}đ.", feeQuote: quote)
            : GhnOperationResult.Ok("Đã tính phí GHN nhưng phản hồi không có tổng phí.");
    }

    public async Task<GhnAddressResult> GetProvincesAsync()
    {
        return await FetchAddressOptionsAsync(
            "master-data/province",
            new { },
            new[] { "ProvinceID", "province_id", "provinceId", "code" },
            new[] { "ProvinceName", "province_name", "provinceName", "name" });
    }

    public async Task<GhnAddressResult> GetDistrictsAsync(int provinceId)
    {
        if (provinceId <= 0)
        {
            return GhnAddressResult.Fail("Thiếu Province ID.");
        }

        return await FetchAddressOptionsAsync(
            "master-data/district",
            new { province_id = provinceId },
            new[] { "DistrictID", "district_id", "districtId", "code" },
            new[] { "DistrictName", "district_name", "districtName", "name" });
    }

    public async Task<GhnAddressResult> GetWardsAsync(int districtId)
    {
        if (districtId <= 0)
        {
            return GhnAddressResult.Fail("Thiếu District ID.");
        }

        return await FetchAddressOptionsAsync(
            "master-data/ward",
            new { district_id = districtId },
            new[] { "WardCode", "ward_code", "wardCode", "code" },
            new[] { "WardName", "ward_name", "wardName", "name" });
    }

    public async Task<GhnOperationResult> SyncOrderAsync(int orderId)
    {
        if (!IsConfigured)
        {
            return GhnOperationResult.Fail("Chưa cấu hình GHN Token/ShopId/BaseUrl.");
        }

        var order = await LoadOrderAsync(orderId);
        if (order?.ShippingInfo is null ||
            !string.Equals(order.ShippingInfo.Carrier, CarrierName, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(order.ShippingInfo.TrackingCode))
        {
            return GhnOperationResult.Fail("Đơn chưa có mã vận đơn GHN.");
        }

        using var response = await PostAsync("v2/shipping-order/detail", new
        {
            order_code = order.ShippingInfo.TrackingCode
        });
        var payload = await ReadGhnResponseAsync(response);
        if (!response.IsSuccessStatusCode || !payload.Success)
        {
            return GhnOperationResult.Fail(payload.Message);
        }

        var ghnStatus = payload.Data.TryGetString("status");
        var totalFee = payload.Data.TryGetDecimal("total_fee");
        var expectedDelivery = payload.Data.TryGetDateTime("leadtime")
            ?? payload.Data.TryGetDateTime("expected_delivery_time");
        if (!await ApplyShipmentAsync(order, order.ShippingInfo.TrackingCode, ghnStatus, totalFee, expectedDelivery))
            return GhnOperationResult.Fail("Không áp dụng trạng thái GHN cũ hoặc không phù hợp với trạng thái hiện tại của đơn.");
        return GhnOperationResult.Ok("Đã đồng bộ trạng thái GHN.", order.ShippingInfo.TrackingCode);
    }

    public async Task<GhnShipmentDetailResult> GetOrderDetailAsync(int orderId)
    {
        if (!IsConfigured)
        {
            return GhnShipmentDetailResult.Fail("Chưa cấu hình GHN Token/ShopId/BaseUrl.");
        }

        var order = await LoadOrderAsync(orderId);
        if (order?.ShippingInfo is null ||
            !string.Equals(order.ShippingInfo.Carrier, CarrierName, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(order.ShippingInfo.TrackingCode))
        {
            return GhnShipmentDetailResult.Fail("Đơn chưa có mã vận đơn GHN.");
        }

        return await FetchOrderDetailAsync(order.Id, order.ShippingInfo.TrackingCode);
    }

    public async Task<GhnShipmentDetailResult> GetOrderDetailByTrackingCodeAsync(string trackingCode)
    {
        if (!IsConfigured)
        {
            return GhnShipmentDetailResult.Fail("Chưa cấu hình GHN Token/ShopId/BaseUrl.");
        }

        var orderCode = trackingCode.Trim();
        if (string.IsNullOrWhiteSpace(orderCode))
        {
            return GhnShipmentDetailResult.Fail("Vui lòng nhập mã vận đơn GHN.");
        }

        var order = await _db.Orders
            .Include(row => row.ShippingInfo)
            .FirstOrDefaultAsync(row => row.ShippingInfo != null && row.ShippingInfo.TrackingCode == orderCode);

        return await FetchOrderDetailAsync(order?.Id, orderCode);
    }

    public async Task<GhnOperationResult> CancelOrderAsync(int orderId)
    {
        if (!IsConfigured)
        {
            return GhnOperationResult.Fail("Chưa cấu hình GHN Token/ShopId/BaseUrl.");
        }

        var order = await LoadOrderAsync(orderId);
        if (order?.ShippingInfo is null ||
            !string.Equals(order.ShippingInfo.Carrier, CarrierName, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(order.ShippingInfo.TrackingCode))
        {
            return GhnOperationResult.Fail("Đơn chưa có mã vận đơn GHN.");
        }

        var orderCode = order.ShippingInfo.TrackingCode;
        using var response = await PostAsync("v2/switch-status/cancel", new
        {
            order_codes = new[] { orderCode }
        });
        var payload = await ReadGhnResponseAsync(response);
        if (!response.IsSuccessStatusCode || !payload.Success)
        {
            return GhnOperationResult.Fail(payload.Message);
        }

        if (!await ApplyShipmentAsync(order, orderCode, "cancel", null, null))
            return GhnOperationResult.Fail($"GHN đã nhận yêu cầu huỷ {orderCode}; trạng thái nội bộ không thay đổi. Vui lòng kiểm tra vận đơn.");
        return GhnOperationResult.Ok($"Đã huỷ vận đơn GHN {orderCode}.", orderCode);
    }

    public async Task<GhnWebhookApplyResult> ApplyWebhookAsync(GhnWebhookPayload payload)
    {
        var orderCode = payload.OrderCode?.Trim();
        var clientOrderCode = payload.ClientOrderCode?.Trim();
        if (string.IsNullOrWhiteSpace(orderCode) && string.IsNullOrWhiteSpace(clientOrderCode))
        {
            return new GhnWebhookApplyResult(false, "Webhook thiếu OrderCode/ClientOrderCode.");
        }

        Order? order = null;
        if (!string.IsNullOrWhiteSpace(orderCode))
        {
            order = await _db.Orders
                .Include(row => row.ShippingInfo)
                .FirstOrDefaultAsync(row => row.ShippingInfo != null && row.ShippingInfo.TrackingCode == orderCode);
        }

        if (order is null && TryParseClientOrderCode(clientOrderCode, out var orderId))
        {
            order = await _db.Orders
                .Include(row => row.ShippingInfo)
                .FirstOrDefaultAsync(row => row.Id == orderId);
        }

        if (order is null)
        {
            return new GhnWebhookApplyResult(false, "Không tìm thấy đơn tương ứng webhook GHN.");
        }

        var applied = await ApplyShipmentAsync(order, orderCode ?? order.ShippingInfo?.TrackingCode ?? "", payload.Status, payload.TotalFee, payload.Time);
        return new GhnWebhookApplyResult(true, applied ? "Đã nhận webhook GHN." : "Đã nhận; bỏ qua cập nhật không còn phù hợp với trạng thái hiện tại.");
    }

    private async Task<Order?> LoadOrderAsync(int orderId)
    {
        return await _db.Orders
            .Include(row => row.Items)
            .ThenInclude(item => item.Product)
            .Include(row => row.ShippingInfo)
            .FirstOrDefaultAsync(row => row.Id == orderId);
    }

    private object BuildCreateOrderRequest(Order order, GhnCreateOrderInput input)
    {
        var weight = Math.Max(1, input.Weight <= 0 ? _options.DefaultWeight : input.Weight);
        var length = Math.Max(1, input.Length <= 0 ? _options.DefaultLength : input.Length);
        var width = Math.Max(1, input.Width <= 0 ? _options.DefaultWidth : input.Width);
        var height = Math.Max(1, input.Height <= 0 ? _options.DefaultHeight : input.Height);
        var codAmount = string.Equals(order.PaymentMethod, "COD", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToInt32(Math.Round(order.TotalAmount, 0))
            : 0;
        var insuranceValue = Math.Min(5_000_000, Convert.ToInt32(Math.Round(order.Items.Sum(item => item.UnitPrice * item.Quantity), 0)));

        return new
        {
            payment_type_id = input.PaymentTypeId <= 0 ? _options.PaymentTypeId : input.PaymentTypeId,
            note = string.IsNullOrWhiteSpace(input.Note) ? $"Techvora DH{order.Id:D6}" : input.Note.Trim(),
            required_note = string.IsNullOrWhiteSpace(input.RequiredNote) ? _options.RequiredNote : input.RequiredNote.Trim(),
            client_order_code = "DH" + order.Id.ToString("D6"),
            to_name = order.RecipientName,
            to_phone = order.RecipientPhone,
            to_address = order.ShippingAddress,
            to_ward_code = input.ToWardCode.Trim(),
            to_district_id = input.ToDistrictId,
            cod_amount = codAmount,
            content = $"Don hang Techvora DH{order.Id:D6}",
            weight,
            length,
            width,
            height,
            service_type_id = input.ServiceTypeId <= 0 ? _options.ServiceTypeId : input.ServiceTypeId,
            insurance_value = insuranceValue,
            items = order.Items.Select(item => new
            {
                name = item.Product?.Name ?? ("San pham #" + item.ProductId),
                code = item.ProductId.ToString(),
                quantity = item.Quantity,
                price = Convert.ToInt32(Math.Round(item.UnitPrice, 0)),
                weight = Math.Max(1, weight / Math.Max(1, order.Items.Sum(row => row.Quantity)))
            }).ToList()
        };
    }

    private object BuildFeeRequest(Order order, GhnCreateOrderInput input)
    {
        var weight = Math.Max(1, input.Weight <= 0 ? _options.DefaultWeight : input.Weight);
        var length = Math.Max(1, input.Length <= 0 ? _options.DefaultLength : input.Length);
        var width = Math.Max(1, input.Width <= 0 ? _options.DefaultWidth : input.Width);
        var height = Math.Max(1, input.Height <= 0 ? _options.DefaultHeight : input.Height);
        var insuranceValue = Math.Min(5_000_000, Convert.ToInt32(Math.Round(order.Items.Sum(item => item.UnitPrice * item.Quantity), 0)));

        return new
        {
            service_type_id = input.ServiceTypeId <= 0 ? _options.ServiceTypeId : input.ServiceTypeId,
            insurance_value = insuranceValue,
            to_ward_code = input.ToWardCode.Trim(),
            to_district_id = input.ToDistrictId,
            weight,
            length,
            width,
            height
        };
    }

    private static GhnFeeQuote BuildFeeQuote(int orderId, JsonElement data)
    {
        var total = data.TryGetDecimal("total")
            ?? data.TryGetDecimal("total_fee")
            ?? data.TryGetDecimal("service_fee")
            ?? 0;
        var lines = new List<GhnFeeLine>();

        AddFeeLine(lines, "Phí dịch vụ", data.TryGetDecimal("service_fee"));
        AddFeeLine(lines, "Phí khai giá", data.TryGetDecimal("insurance_fee"));
        AddFeeLine(lines, "Phí COD", data.TryGetDecimal("cod_fee"));
        AddFeeLine(lines, "Phí lấy hàng vùng xa", data.TryGetDecimal("pick_remote_areas_fee"));
        AddFeeLine(lines, "Phí giao vùng xa", data.TryGetDecimal("deliver_remote_areas_fee"));
        AddFeeLine(lines, "Phí gửi tại bưu cục", data.TryGetDecimal("pick_station_fee"));
        AddFeeLine(lines, "Phí giao lại", data.TryGetDecimal("r2s_fee"));
        AddFeeLine(lines, "Phí trả chứng từ", data.TryGetDecimal("document_return"));
        AddFeeLine(lines, "Phí đồng kiểm", data.TryGetDecimal("double_check"));
        AddFeeLine(lines, "Phí COD thất bại", data.TryGetDecimal("cod_failed_fee"));

        var coupon = data.TryGetDecimal("coupon_value");
        if (coupon.HasValue && coupon.Value != 0)
        {
            lines.Add(new GhnFeeLine("Khuyến mãi", -Math.Abs(coupon.Value)));
        }

        if (lines.Count == 0 && total > 0)
        {
            lines.Add(new GhnFeeLine("Phí GHN", total));
        }

        return new GhnFeeQuote(orderId, total, lines);
    }

    private static void AddFeeLine(List<GhnFeeLine> lines, string label, decimal? amount)
    {
        if (amount.HasValue && amount.Value != 0)
        {
            lines.Add(new GhnFeeLine(label, amount.Value));
        }
    }

    private async Task<GhnShipmentDetailResult> FetchOrderDetailAsync(int? orderId, string orderCode)
    {
        using var response = await PostAsync("v2/shipping-order/detail", new
        {
            order_code = orderCode
        });
        var payload = await ReadGhnResponseAsync(response);
        if (!response.IsSuccessStatusCode || !payload.Success)
        {
            return GhnShipmentDetailResult.Fail($"Không tìm thấy vận đơn GHN {orderCode}.");
        }

        var detail = BuildShipmentDetail(orderId, orderCode, payload.Data);
        return GhnShipmentDetailResult.Ok($"Đã tải chi tiết vận đơn GHN {detail.OrderCode}.", detail);
    }

    private static GhnShipmentDetail BuildShipmentDetail(int? orderId, string fallbackOrderCode, JsonElement data)
    {
        var orderCode = data.TryGetString("order_code") ?? fallbackOrderCode;
        var ghnStatus = data.TryGetString("status");
        var shippingStatus = string.IsNullOrWhiteSpace(ghnStatus) ? null : MapGhnStatus(ghnStatus);
        var totalFee = data.TryGetDecimal("total_fee") ?? data.TryGetDecimal("fee");
        if (!totalFee.HasValue &&
            data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty("fee", out var feeElement))
        {
            totalFee = feeElement.TryGetDecimal("total")
                ?? feeElement.TryGetDecimal("main_service")
                ?? feeElement.TryGetDecimal("service_fee");
        }

        var expectedDelivery = data.TryGetDateTime("leadtime")
            ?? data.TryGetDateTime("expected_delivery_time");
        var createdAt = data.TryGetDateTime("created_date")
            ?? data.TryGetDateTime("created_at");
        var updatedAt = data.TryGetDateTime("updated_date")
            ?? data.TryGetDateTime("modified_date")
            ?? data.TryGetDateTime("updated_at");

        var lines = new List<GhnShipmentDetailLine>();
        AddDetailLine(lines, "Trạng thái GHN", ghnStatus);
        AddDetailLine(lines, "Trạng thái giao hàng", shippingStatus);
        AddDetailLine(lines, "Người nhận", data.TryGetString("to_name"));
        AddDetailLine(lines, "SĐT nhận", data.TryGetString("to_phone"));
        AddDetailLine(lines, "Địa chỉ nhận", data.TryGetString("to_address"));
        AddDetailLine(lines, "Người gửi", data.TryGetString("from_name"));
        AddDetailLine(lines, "SĐT gửi", data.TryGetString("from_phone"));
        AddDetailLine(lines, "Địa chỉ gửi", data.TryGetString("from_address"));
        AddDetailLine(lines, "COD", FormatMoney(data.TryGetDecimal("cod_amount")));
        AddDetailLine(lines, "Khối lượng", FormatWeight(data.TryGetInt("weight")));
        AddDetailLine(lines, "Khối lượng quy đổi", FormatWeight(data.TryGetInt("converted_weight")));
        AddDetailLine(lines, "Kích thước", FormatSize(data.TryGetInt("length"), data.TryGetInt("width"), data.TryGetInt("height")));
        AddDetailLine(lines, "Ghi chú", data.TryGetString("note"));
        AddDetailLine(lines, "Yêu cầu giao", data.TryGetString("required_note"));
        AddDetailLine(lines, "Nội dung hàng", data.TryGetString("content"));
        AddDetailLine(lines, "Mã đơn nội bộ", data.TryGetString("client_order_code"));
        AddDetailLine(lines, "Lý do", data.TryGetString("reason"));
        AddDetailLine(lines, "Ngày tạo", FormatDate(createdAt));
        AddDetailLine(lines, "Dự kiến giao", FormatDate(expectedDelivery));
        AddDetailLine(lines, "Cập nhật cuối", FormatDate(updatedAt));

        return new GhnShipmentDetail(
            orderId,
            orderCode,
            ghnStatus,
            shippingStatus,
            totalFee,
            expectedDelivery,
            createdAt,
            updatedAt,
            lines);
    }

    private static void AddDetailLine(List<GhnShipmentDetailLine> lines, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            lines.Add(new GhnShipmentDetailLine(label, NormalizeGhnText(value)));
        }
    }

    private static string NormalizeGhnText(string value)
    {
        var text = value.Trim();
        for (var i = 0; i < 2 && LooksMojibake(text); i++)
        {
            var decoded = DecodeUtf8FromLatin1(text);
            if (string.Equals(decoded, text, StringComparison.Ordinal))
            {
                break;
            }

            text = decoded;
        }

        return text;
    }

    private static bool LooksMojibake(string text)
    {
        return text.Contains('Ã') ||
               text.Contains('Â') ||
               text.Contains('Ä') ||
               text.Contains('Æ') ||
               text.Contains("áº", StringComparison.Ordinal) ||
               text.Contains("á»", StringComparison.Ordinal) ||
               text.Contains("â€", StringComparison.Ordinal) ||
               text.Contains("â‚", StringComparison.Ordinal);
    }

    private static string DecodeUtf8FromLatin1(string text)
    {
        try
        {
            var bytes = Encoding.Latin1.GetBytes(text);
            var decoded = Encoding.UTF8.GetString(bytes);
            return decoded.Contains('\uFFFD') ? text : decoded;
        }
        catch (DecoderFallbackException)
        {
            return text;
        }
    }

    private static string? FormatMoney(decimal? amount)
    {
        return amount.HasValue ? $"{amount.Value:N0}đ" : null;
    }

    private static string? FormatWeight(int? grams)
    {
        return grams.HasValue && grams.Value > 0 ? $"{grams.Value:N0}g" : null;
    }

    private static string? FormatSize(int? length, int? width, int? height)
    {
        return length.HasValue && width.HasValue && height.HasValue
            ? $"{length.Value} x {width.Value} x {height.Value} cm"
            : null;
    }

    private static string? FormatDate(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        var date = value.Value.Kind == DateTimeKind.Unspecified
            ? value.Value
            : value.Value.ToLocalTime();
        return date.ToString("dd/MM/yyyy HH:mm");
    }

    private async Task<GhnAddressResult> FetchAddressOptionsAsync(string path, object body, string[] codeKeys, string[] nameKeys)
    {
        if (!IsConfigured)
        {
            return GhnAddressResult.Fail("Chưa cấu hình GHN Token/ShopId/BaseUrl.");
        }

        using var response = await PostAsync(path, body);
        var payload = await ReadGhnResponseAsync(response);
        if (!response.IsSuccessStatusCode || !payload.Success)
        {
            return GhnAddressResult.Fail(payload.Message);
        }

        if (payload.Data.ValueKind != JsonValueKind.Array)
        {
            return GhnAddressResult.Ok(Array.Empty<GhnAddressOption>());
        }

        var items = payload.Data.EnumerateArray()
            .Select(item =>
            {
                var code = item.TryGetStringAny(codeKeys);
                var name = item.TryGetStringAny(nameKeys);
                return string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name)
                    ? null
                    : new GhnAddressOption(code, name);
            })
            .Where(item => item is not null)
            .Cast<GhnAddressOption>()
            .OrderBy(item => item.Name)
            .ToList();

        return GhnAddressResult.Ok(items);
    }

    private async Task<bool> ApplyShipmentAsync(Order order, string orderCode, string? ghnStatus, decimal? totalFee, DateTime? expectedDelivery)
    {
        var mappedStatus = MapGhnStatus(ghnStatus);
        var orderId = order.Id;
        var saved = await DatabaseTransaction.ExecuteAsync<Order?>(_db, async () =>
        {
            var current = await OrderLifecycle.LoadForUpdateAsync(_db, orderId);
            if (current is null || !OrderLifecycle.CanApplyShipment(current, mappedStatus)) return null;
            // A late callback for an old shipment must not replace the current tracking number.
            if (!string.IsNullOrWhiteSpace(current.ShippingInfo?.TrackingCode) &&
                (!string.Equals(current.ShippingInfo.TrackingCode, orderCode, StringComparison.OrdinalIgnoreCase) ||
                 !string.Equals(current.ShippingInfo.Carrier, CarrierName, StringComparison.OrdinalIgnoreCase))) return null;
            current.ShippingInfo ??= new ShippingInfo { OrderId = orderId };
            current.ShippingInfo.Carrier = CarrierName;
            current.ShippingInfo.TrackingCode = orderCode;
            if (expectedDelivery.HasValue)
                current.ShippingInfo.EstimatedDelivery = expectedDelivery.Value.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(expectedDelivery.Value, DateTimeKind.Utc)
                    : expectedDelivery.Value.ToUniversalTime();
            // Carrier cost is not a revision of the shipping price agreed at checkout.
            current.ShippingInfo.Status = mappedStatus;
            current.ShippingInfo.ShippedAt ??= DateTime.UtcNow;
            current.Status = mappedStatus == ShippingStatuses.Delivered ? OrderStatuses.Delivered : OrderStatuses.Shipping;
            current.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return current;
        });
        if (saved is null) return false;
        if (mappedStatus == ShippingStatuses.Delivered)
        {
            await _orderEmailService.SendOrderStatusChangedAsync(saved.Id, OrderStatuses.Delivered);
            await _notificationService.CreateAsync(saved.UserId, "Đơn hàng đã giao",
                $"Đơn #DH{saved.Id:D4} đã giao thành công qua GHN.",
                NotificationTypes.Order, $"/Order/Detail/{saved.Id}");
        }
        return true;
    }

    private async Task<HttpResponseMessage> PostAsync(string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, NormalizePath(path))
        {
            Content = JsonContent.Create(body, mediaType: null, options: SerializerOptions)
        };
        request.Headers.TryAddWithoutValidation("Token", _options.Token);
        request.Headers.TryAddWithoutValidation("ShopId", _options.ShopId);
        return await _httpClient.SendAsync(request);
    }

    private async Task<GhnApiResponse> ReadGhnResponseAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(text))
        {
            return new GhnApiResponse(false, response.ReasonPhrase ?? "GHN không trả dữ liệu.", default);
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement.Clone();
            var code = root.TryGetInt("code");
            var message = root.TryGetString("message") ?? root.TryGetString("code_message") ?? response.ReasonPhrase ?? "GHN lỗi.";
            var success = response.IsSuccessStatusCode && (code is null or 200);
            var data = root.TryGetProperty("data", out var dataElement) ? dataElement.Clone() : default;
            return new GhnApiResponse(success, message, data);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not parse GHN response: {Response}", text);
            return new GhnApiResponse(false, "Không đọc được phản hồi GHN.", default);
        }
    }

    private string NormalizePath(string path)
    {
        var baseUrl = (_options.BaseUrl ?? string.Empty).TrimEnd('/');
        return baseUrl + "/" + path.TrimStart('/');
    }

    private static string MapGhnStatus(string? status)
    {
        var normalized = (status ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized is "ready_to_pick" or "picking" or "money_collect_picking")
        {
            return ShippingStatuses.WaitingPickup;
        }

        if (normalized is "delivered")
        {
            return ShippingStatuses.Delivered;
        }

        if (normalized.Contains("return"))
        {
            return ShippingStatuses.Returned;
        }

        if (normalized is "cancel" or "cancelled" or "canceled")
        {
            return ShippingStatuses.Cancelled;
        }

        if (normalized.Contains("fail") || normalized.Contains("delay"))
        {
            return ShippingStatuses.Delayed;
        }

        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return ShippingStatuses.InTransit;
        }

        return ShippingStatuses.WaitingPickup;
    }

    private static bool TryParseClientOrderCode(string? clientOrderCode, out int orderId)
    {
        orderId = 0;
        if (string.IsNullOrWhiteSpace(clientOrderCode))
        {
            return false;
        }

        var text = clientOrderCode.Trim();
        if (text.StartsWith("DH", StringComparison.OrdinalIgnoreCase))
        {
            text = text[2..];
        }

        return int.TryParse(text, out orderId) && orderId > 0;
    }

    private sealed record GhnApiResponse(bool Success, string Message, JsonElement Data);
}

public sealed class GhnOptions
{
    public string BaseUrl { get; set; } = "https://dev-online-gateway.ghn.vn/shiip/public-api";
    public string Token { get; set; } = string.Empty;
    public string ShopId { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public int DefaultWeight { get; set; } = 2000;
    public int DefaultLength { get; set; } = 30;
    public int DefaultWidth { get; set; } = 25;
    public int DefaultHeight { get; set; } = 15;
    public int ServiceTypeId { get; set; } = 2;
    public int PaymentTypeId { get; set; } = 1;
    public string RequiredNote { get; set; } = "KHONGCHOXEMHANG";
}

public sealed class GhnCreateOrderInput
{
    public int ToDistrictId { get; set; }
    public string ToWardCode { get; set; } = string.Empty;
    public int Weight { get; set; }
    public int Length { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int ServiceTypeId { get; set; }
    public int PaymentTypeId { get; set; }
    public string? RequiredNote { get; set; }
    public string? Note { get; set; }
}

public sealed record GhnOperationResult(bool Success, string Message, string? OrderCode, GhnFeeQuote? FeeQuote = null)
{
    public static GhnOperationResult Ok(string message, string? orderCode = null, GhnFeeQuote? feeQuote = null) => new(true, message, orderCode, feeQuote);
    public static GhnOperationResult Fail(string message) => new(false, message, null);
}

public sealed record GhnFeeQuote(int OrderId, decimal Total, IReadOnlyList<GhnFeeLine> Lines);

public sealed record GhnFeeLine(string Label, decimal Amount);

public sealed record GhnShipmentDetail(
    int? OrderId,
    string OrderCode,
    string? GhnStatus,
    string? ShippingStatus,
    decimal? TotalFee,
    DateTime? ExpectedDelivery,
    DateTime? CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<GhnShipmentDetailLine> Lines);

public sealed record GhnShipmentDetailLine(string Label, string Value);

public sealed record GhnShipmentDetailResult(bool Success, string Message, GhnShipmentDetail? Detail)
{
    public static GhnShipmentDetailResult Ok(string message, GhnShipmentDetail detail) => new(true, message, detail);
    public static GhnShipmentDetailResult Fail(string message) => new(false, message, null);
}

public sealed record GhnAddressOption(string Code, string Name);

public sealed record GhnAddressResult(bool Success, string Message, IReadOnlyList<GhnAddressOption> Items)
{
    public static GhnAddressResult Ok(IReadOnlyList<GhnAddressOption> items) => new(true, string.Empty, items);
    public static GhnAddressResult Fail(string message) => new(false, message, Array.Empty<GhnAddressOption>());
}

public sealed record GhnWebhookApplyResult(bool Success, string Message);

public sealed class GhnWebhookPayload
{
    public decimal? CODAmount { get; set; }
    public DateTime? CODTransferDate { get; set; }
    public string? ClientOrderCode { get; set; }
    public int? ConvertedWeight { get; set; }
    public string? Description { get; set; }
    public GhnWebhookFee? Fee { get; set; }
    public int? Height { get; set; }
    public bool? IsPartialReturn { get; set; }
    public int? Length { get; set; }
    public string? OrderCode { get; set; }
    public string? PartialReturnCode { get; set; }
    public int? PaymentType { get; set; }
    public string? Reason { get; set; }
    public string? ReasonCode { get; set; }
    public int? ShopID { get; set; }
    public string? Status { get; set; }
    public DateTime? Time { get; set; }
    public decimal? TotalFee { get; set; }
    public string? Type { get; set; }
    public string? Warehouse { get; set; }
    public int? Weight { get; set; }
    public int? Width { get; set; }
}

public sealed class GhnWebhookFee
{
    public decimal? CODFailedFee { get; set; }
    public decimal? CODFee { get; set; }
    public decimal? Coupon { get; set; }
    public decimal? DeliverRemoteAreasFee { get; set; }
    public decimal? DocumentReturn { get; set; }
    public decimal? DoubleCheck { get; set; }
    public decimal? Insurance { get; set; }
    public decimal? MainService { get; set; }
    public decimal? PickRemoteAreasFee { get; set; }
    public decimal? R2S { get; set; }
    public decimal? Return { get; set; }
    public decimal? StationDO { get; set; }
    public decimal? StationPU { get; set; }
    public decimal? Total { get; set; }
}

internal static class GhnJsonExtensions
{
    public static string? TryGetString(this JsonElement element, string property)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(property, out var value) &&
            value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
        {
            return value.ToString();
        }

        return null;
    }

    public static int? TryGetInt(this JsonElement element, string property)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(property, out var value) &&
            value.TryGetInt32(out var result))
        {
            return result;
        }

        return null;
    }

    public static decimal? TryGetDecimal(this JsonElement element, string property)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(property, out var value) &&
            value.TryGetDecimal(out var result))
        {
            return result;
        }

        return null;
    }

    public static DateTime? TryGetDateTime(this JsonElement element, string property)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(property, out var value))
        {
            if (value.ValueKind == JsonValueKind.String && value.TryGetDateTime(out var result))
            {
                return result;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var unixSeconds))
            {
                return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
            }
        }

        return null;
    }

    public static string? TryGetStringAny(this JsonElement element, IEnumerable<string> properties)
    {
        foreach (var property in properties)
        {
            var value = element.TryGetString(property);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
