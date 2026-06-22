namespace EcommerceApp.Services;

public interface IPasswordResetService
{
    /// <summary>
    /// Tạo token reset password, hash rồi lưu DB, gửi email chứa link reset.
    /// Luôn thành công bất kể email có tồn tại hay không (chống user enumeration).
    /// </summary>
    Task SendResetLinkAsync(string email);

    /// <summary>
    /// Verify token và đổi password. Trả về (success, errorMessage).
    /// </summary>
    Task<(bool Success, string? Error)> ResetPasswordAsync(string rawToken, string newPassword);
}
