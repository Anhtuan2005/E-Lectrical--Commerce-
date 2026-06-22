using EcommerceApp.Data;
using EcommerceApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace EcommerceApp.Services;

public class PasswordResetService : IPasswordResetService
{
    private const int TokenExpiryMinutes = 30;

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        IOptions<EmailOptions> emailOptions,
        ILogger<PasswordResetService> logger)
    {
        _db = db;
        _userManager = userManager;
        _emailSender = emailSender;
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    public async Task SendResetLinkAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            // Không lộ email có tồn tại hay không
            _logger.LogInformation("Password reset requested for non-existent email.");
            return;
        }

        var rawToken = GenerateRawToken();
        var tokenHash = HashToken(rawToken);
        var now = DateTime.UtcNow;

        var resetToken = new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiredAt = now.AddMinutes(TokenExpiryMinutes),
            CreatedAt = now
        };

        _db.PasswordResetTokens.Add(resetToken);
        await _db.SaveChangesAsync();

        var frontendUrl = _emailOptions.FrontendUrl?.TrimEnd('/');
        var resetLink = $"{frontendUrl}/Account/ResetPassword?token={rawToken}";

        var htmlBody = ResetPasswordEmailTemplate.Build(resetLink, user.FullName);
        await _emailSender.SendAsync(user.Email!, "Đặt lại mật khẩu – Techvora", htmlBody);

        _logger.LogInformation("Password reset email sent for user {UserId}.", user.Id);
    }

    public async Task<(bool Success, string? Error)> ResetPasswordAsync(string rawToken, string newPassword)
    {
        var tokenHash = HashToken(rawToken);
        var now = DateTime.UtcNow;

        var resetToken = await _db.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (resetToken is null)
            return (false, "Link đặt lại mật khẩu không hợp lệ.");

        if (resetToken.UsedAt is not null)
            return (false, "Link đặt lại mật khẩu đã được sử dụng.");

        if (resetToken.ExpiredAt < now)
            return (false, "Link đặt lại mật khẩu đã hết hạn. Vui lòng yêu cầu lại.");

        var user = await _userManager.FindByIdAsync(resetToken.UserId);
        if (user is null)
            return (false, "Không tìm thấy tài khoản.");

        // Xóa password cũ rồi set password mới
        var removeResult = await _userManager.RemovePasswordAsync(user);
        if (!removeResult.Succeeded)
            return (false, "Không thể đặt lại mật khẩu. Vui lòng thử lại.");

        var addResult = await _userManager.AddPasswordAsync(user, newPassword);
        if (!addResult.Succeeded)
        {
            var errors = string.Join(" ", addResult.Errors.Select(e => e.Description));
            return (false, errors);
        }

        // Đánh dấu token đã dùng
        resetToken.UsedAt = now;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Password reset completed for user {UserId}.", user.Id);
        return (true, null);
    }

    /// <summary>Tạo raw token 32 bytes → base64url (không có +/= gây lỗi URL).</summary>
    private static string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>SHA-256 hash token. Chỉ lưu hash, không lưu raw.</summary>
    private static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
}
