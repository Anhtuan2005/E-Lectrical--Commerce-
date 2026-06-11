using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using System.Globalization;
using System.Net;

namespace EcommerceApp.Services;

public static class OrderEmailTemplate
{
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    public static (string Subject, string HtmlBody) OrderCreated(Order order)
    {
        var subject = $"Techvora đã nhận đơn hàng #DH{order.Id:D4}";
        return (subject, Layout(
            order,
            "Đã nhận đơn hàng",
            "Techvora đã nhận đơn của bạn. Chúng tôi sẽ kiểm tra và cập nhật trạng thái tiếp theo qua email.",
            "Đơn hàng đã được tạo",
            "Chờ xác nhận"));
    }

    public static (string Subject, string HtmlBody) StatusChanged(Order order, string status)
    {
        var displayStatus = OrderStatusFilters.ToDisplayLabel(status);
        var subject = $"Cập nhật đơn hàng #DH{order.Id:D4}: {displayStatus}";
        var lead = status switch
        {
            _ when status == OrderStatuses.Confirmed => "Đơn hàng của bạn đã được xác nhận và đang được chuẩn bị.",
            _ when status == OrderStatuses.Shipping => "Đơn hàng đã được bàn giao cho đơn vị vận chuyển.",
            _ when status == OrderStatuses.Delivered => "Đơn hàng đã được giao thành công. Cảm ơn bạn đã mua sắm tại Techvora.",
            _ when status == OrderStatuses.Cancelled => "Đơn hàng đã được huỷ theo trạng thái mới nhất.",
            _ => "Đơn hàng của bạn vừa có cập nhật trạng thái."
        };

        return (subject, Layout(order, $"Đơn hàng {displayStatus}", lead, "Trạng thái hiện tại", displayStatus));
    }

    public static (string Subject, string HtmlBody) RefundRequired(Order order)
    {
        var subject = $"Đơn #DH{order.Id:D4} cần hoàn tiền VNPAY";
        return (subject, Layout(
            order,
            "Đơn VNPAY cần hoàn tiền",
            "Đơn hàng đã thanh toán qua VNPAY và vừa bị huỷ. Techvora đã ghi nhận yêu cầu hoàn tiền để đội ngũ xử lý thủ công.",
            "Trạng thái hoàn tiền",
            order.RefundStatus));
    }

    public static (string Subject, string HtmlBody) RefundCompleted(Order order)
    {
        var subject = $"Techvora đã ghi nhận hoàn tiền đơn #DH{order.Id:D4}";
        return (subject, Layout(
            order,
            "Đã ghi nhận hoàn tiền",
            "Techvora đã đánh dấu đơn hàng là đã hoàn tiền. Nếu bạn cần đối soát thêm, vui lòng liên hệ bộ phận hỗ trợ.",
            "Trạng thái hoàn tiền",
            order.RefundStatus));
    }

    private static string Layout(Order order, string title, string lead, string badgeLabel, string badgeValue)
    {
        var itemRows = string.Join("", order.Items.Select(item =>
        {
            var productName = Encode(item.Product?.Name ?? "Sản phẩm");
            var lineTotal = FormatMoney(item.Quantity * item.UnitPrice);
            return $"""
                <tr>
                    <td style="padding:12px 0;border-bottom:1px solid #e8e8ed;color:#1d1d1f;">{productName}<br><span style="color:#6e6e73;font-size:13px;">SL {item.Quantity} x {FormatMoney(item.UnitPrice)}</span></td>
                    <td style="padding:12px 0;border-bottom:1px solid #e8e8ed;text-align:right;color:#1d1d1f;font-weight:700;">{lineTotal}</td>
                </tr>
                """;
        }));

        if (string.IsNullOrWhiteSpace(itemRows))
        {
            itemRows = """<tr><td colspan="2" style="padding:12px 0;color:#6e6e73;">Không có sản phẩm.</td></tr>""";
        }

        var carrier = string.IsNullOrWhiteSpace(order.ShippingInfo?.Carrier) ? "Chưa gán" : order.ShippingInfo!.Carrier;
        var trackingCode = string.IsNullOrWhiteSpace(order.ShippingInfo?.TrackingCode) ? "Chưa có" : order.ShippingInfo!.TrackingCode;

        return $"""
            <!doctype html>
            <html lang="vi">
            <body style="margin:0;background:#f5f5f7;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Arial,sans-serif;color:#1d1d1f;">
                <div style="max-width:680px;margin:0 auto;padding:28px 16px;">
                    <div style="background:#ffffff;border:1px solid #e8e8ed;border-radius:18px;overflow:hidden;box-shadow:0 18px 50px rgba(0,113,227,.08);">
                        <div style="padding:24px;background:linear-gradient(135deg,#e8f4fd,#ffffff);border-bottom:1px solid #e8e8ed;">
                            <div style="color:#0071e3;font-size:12px;font-weight:900;letter-spacing:.08em;text-transform:uppercase;">Techvora</div>
                            <h1 style="margin:8px 0 8px;font-size:26px;line-height:1.2;color:#1d1d1f;">{Encode(title)}</h1>
                            <p style="margin:0;color:#6e6e73;font-size:15px;line-height:1.55;">{Encode(lead)}</p>
                        </div>
                        <div style="padding:22px 24px;">
                            <div style="display:inline-block;padding:8px 12px;border-radius:999px;background:#e8f4fd;color:#0051a8;font-weight:800;font-size:13px;">{Encode(badgeLabel)}: {Encode(badgeValue)}</div>
                            <h2 style="margin:22px 0 8px;font-size:18px;">#DH{order.Id:D4}</h2>
                            <p style="margin:0 0 16px;color:#6e6e73;line-height:1.55;">Người nhận: <strong style="color:#1d1d1f;">{Encode(order.RecipientName)}</strong><br>Địa chỉ: {Encode(order.ShippingAddress)}</p>
                            <table role="presentation" style="width:100%;border-collapse:collapse;margin-top:8px;">
                                {itemRows}
                                <tr>
                                    <td style="padding:16px 0 0;color:#6e6e73;">Tổng thanh toán</td>
                                    <td style="padding:16px 0 0;text-align:right;color:#0051a8;font-weight:900;font-size:18px;">{FormatMoney(order.TotalAmount)}</td>
                                </tr>
                            </table>
                            <div style="margin-top:22px;padding:14px;border-radius:14px;background:#f5f5f7;color:#424245;font-size:14px;line-height:1.55;">
                                Thanh toán: <strong>{Encode(order.PaymentMethod)}</strong><br>
                                Vận chuyển: <strong>{Encode(carrier)}</strong> · Mã vận đơn: <strong>{Encode(trackingCode)}</strong>
                            </div>
                        </div>
                    </div>
                    <p style="margin:18px 4px 0;color:#6e6e73;font-size:12px;line-height:1.5;">Email này được gửi tự động từ Techvora. Vui lòng không chia sẻ mã giao dịch hoặc thông tin đơn hàng cho người lạ.</p>
                </div>
            </body>
            </html>
            """;
    }

    private static string FormatMoney(decimal value) => value.ToString("N0", ViCulture) + " đ";

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
