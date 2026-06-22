namespace EcommerceApp.Services;

public class EmailOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = "no-reply@techvora.vn";
    public string FromName { get; set; } = "Techvora";

    /// <summary>URL frontend dùng để tạo link reset password.</summary>
    public string FrontendUrl { get; set; } = string.Empty;
}
