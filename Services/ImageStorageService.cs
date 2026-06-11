using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace EcommerceApp.Services;

public interface IImageStorageService
{
    Task<string?> SaveAsWebpAsync(
        IFormFile? file,
        string subfolder,
        int maxWidth,
        int maxHeight,
        int quality = 82,
        long maxBytes = 8 * 1024 * 1024,
        CancellationToken cancellationToken = default);
}

public class ImageStorageService : IImageStorageService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    private readonly IWebHostEnvironment _environment;

    public ImageStorageService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string?> SaveAsWebpAsync(
        IFormFile? file,
        string subfolder,
        int maxWidth,
        int maxHeight,
        int quality = 82,
        long maxBytes = 8 * 1024 * 1024,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return null;
        }

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension) ||
            !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Chỉ hỗ trợ ảnh JPG, PNG hoặc WEBP.");
        }

        if (file.Length > maxBytes)
        {
            throw new InvalidOperationException($"Ảnh cần nhỏ hơn {Math.Ceiling(maxBytes / 1024d / 1024d):0}MB.");
        }

        if (maxWidth <= 0 || maxHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxWidth), "Kích thước ảnh tối đa phải lớn hơn 0.");
        }

        Image image;
        try
        {
            await using var input = file.OpenReadStream();
            image = await Image.LoadAsync(input, cancellationToken);
        }
        catch (UnknownImageFormatException)
        {
            throw new InvalidOperationException("Tệp tải lên không phải ảnh hợp lệ.");
        }

        using (image)
        {
            image.Mutate(context =>
            {
                context.AutoOrient();
                if (image.Width > maxWidth || image.Height > maxHeight)
                {
                    context.Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Max,
                        Size = new Size(maxWidth, maxHeight),
                        Sampler = KnownResamplers.Lanczos3
                    });
                }
            });

            image.Metadata.ExifProfile = null;
            image.Metadata.IccProfile = null;
            image.Metadata.XmpProfile = null;

            var safeFolder = string.Concat(subfolder
                .Trim()
                .ToLowerInvariant()
                .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_'));
            if (string.IsNullOrWhiteSpace(safeFolder))
            {
                throw new ArgumentException("Thư mục ảnh không hợp lệ.", nameof(subfolder));
            }

            var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var uploadRoot = Path.Combine(webRoot, "uploads", safeFolder);
            Directory.CreateDirectory(uploadRoot);
            var fileName = $"{Guid.NewGuid():N}.webp";
            var filePath = Path.Combine(uploadRoot, fileName);

            try
            {
                await image.SaveAsWebpAsync(
                    filePath,
                    new WebpEncoder { Quality = Math.Clamp(quality, 1, 100) },
                    cancellationToken);
            }
            catch
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                throw;
            }

            return $"/uploads/{safeFolder}/{fileName}";
        }
    }
}
