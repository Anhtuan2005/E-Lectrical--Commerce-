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

        var recipientEmail = user.Email!;
        var htmlBody = ResetPasswordEmailTemplate.Build(resetLink, user.FullName);
        await _emailSender.SendAsync(recipientEmail, "Đặt lại mật khẩu – Techvora", htmlBody);

        _logger.LogInformation(
            "Password reset email sent to {MaskedEmail} for user {UserId}.",
            MaskEmail(recipientEmail),
            user.Id);
    }

    public async Task<(bool Success, string? Error)> ResetPasswordAsync(string rawToken, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return (false, "Link đặt lại mật khẩu không hợp lệ.");

        var tokenHash = HashToken(rawToken);
        return await DatabaseTransaction.ExecuteAsync<(bool Success, string? Error)>(_db, async () =>
        {
            var userId = await _db.PasswordResetTokens.AsNoTracking()
                .Where(token => token.TokenHash == tokenHash)
                .Select(token => token.UserId).FirstOrDefaultAsync();
            if (userId is null)
                return (false, "Link đặt lại mật khẩu không hợp lệ.");

            // Serialize all reset links for this user, then recheck the token under the lock.
            var user = await _db.Users
                .FromSqlInterpolated($"SELECT * FROM AspNetUsers WITH (UPDLOCK, ROWLOCK) WHERE Id = {userId}")
                .FirstOrDefaultAsync();
            if (user is null)
                return (false, "Không tìm thấy tài khoản.");

            var resetToken = await _db.PasswordResetTokens.AsNoTracking()
                .FirstOrDefaultAsync(token => token.TokenHash == tokenHash);
            if (resetToken is null || resetToken.UsedAt is not null)
                return (false, "Link đặt lại mật khẩu đã được sử dụng.");

            var now = DateTime.UtcNow;
            if (resetToken.ExpiredAt <= now)
                return (false, "Link đặt lại mật khẩu đã hết hạn. Vui lòng yêu cầu lại.");

            // Identity validates the new password before changing its hash and security stamp.
            var identityToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, identityToken, newPassword);
            if (!result.Succeeded)
                return (false, string.Join(" ", result.Errors.Select(error => error.Description)));

            await _db.PasswordResetTokens.Where(token => token.UserId == user.Id && token.UsedAt == null)
                .ExecuteUpdateAsync(update => update.SetProperty(token => token.UsedAt, now));
            return (true, null);
        });
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

    private static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "***";
        }

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0 || atIndex == email.Length - 1)
        {
            return "***";
        }

        var local = email[..atIndex];
        var domain = email[(atIndex + 1)..];
        var visible = local.Length <= 2 ? local[..1] : local[..2];
        return $"{visible}***@{domain}";
    }
}
