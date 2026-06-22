using System.Net;

namespace EcommerceApp.Services;

public static class ResetPasswordEmailTemplate
{
    public static string Build(string resetLink, string? userName)
    {
        var greeting = string.IsNullOrWhiteSpace(userName)
            ? "Xin chào"
            : $"Xin chào {Encode(userName)}";

        return $$"""
            <!doctype html>
            <html lang="vi">
            <body style="margin:0;background:#f5f5f7;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Arial,sans-serif;color:#1d1d1f;">
                <div style="max-width:680px;margin:0 auto;padding:28px 16px;">
                    <div style="background:#ffffff;border:1px solid #e8e8ed;border-radius:18px;overflow:hidden;box-shadow:0 18px 50px rgba(0,113,227,.08);">
                        <div style="padding:24px;background:linear-gradient(135deg,#e8f4fd,#ffffff);border-bottom:1px solid #e8e8ed;">
                            <div style="color:#0071e3;font-size:12px;font-weight:900;letter-spacing:.08em;text-transform:uppercase;">Techvora</div>
                            <h1 style="margin:8px 0 8px;font-size:26px;line-height:1.2;color:#1d1d1f;">Đặt lại mật khẩu</h1>
                            <p style="margin:0;color:#6e6e73;font-size:15px;line-height:1.55;">{{greeting}}, chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản Techvora của bạn.</p>
                        </div>
                        <div style="padding:22px 24px;">
                            <p style="margin:0 0 16px;color:#424245;font-size:15px;line-height:1.6;">
                                Nhấn nút bên dưới để đặt mật khẩu mới. Link có hiệu lực trong <strong>30 phút</strong>.
                            </p>
                            <div style="text-align:center;margin:24px 0;">
                                <a href="{{resetLink}}" style="display:inline-block;padding:14px 36px;background:#0071e3;color:#ffffff;font-size:16px;font-weight:700;border-radius:12px;text-decoration:none;letter-spacing:.02em;">
                                    Đặt lại mật khẩu
                                </a>
                            </div>
                            <p style="margin:0 0 12px;color:#6e6e73;font-size:13px;line-height:1.55;">
                                Nếu nút không hoạt động, sao chép đường link sau vào trình duyệt:
                            </p>
                            <p style="margin:0 0 16px;word-break:break-all;color:#0051a8;font-size:13px;">
                                {{Encode(resetLink)}}
                            </p>
                            <div style="padding:14px;border-radius:14px;background:#f5f5f7;color:#424245;font-size:14px;line-height:1.55;">
                                ⚠️ Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này. Tài khoản của bạn vẫn an toàn.
                            </div>
                        </div>
                    </div>
                    <p style="margin:18px 4px 0;color:#6e6e73;font-size:12px;line-height:1.5;">
                        Email này được gửi tự động từ Techvora. Vui lòng không chia sẻ link đặt lại mật khẩu cho người khác.
                    </p>
                </div>
            </body>
            </html>
            """;
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
