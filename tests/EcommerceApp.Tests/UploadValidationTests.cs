using EcommerceApp.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Moq;

namespace EcommerceApp.Tests;

public class UploadValidationTests
{
    [Fact]
    public async Task Corrupted_png_returns_a_validation_error_instead_of_an_image_decoder_exception()
    {
        // Valid PNG signature with a truncated IHDR chunk.
        using var data = new MemoryStream(Convert.FromHexString("89504E470D0A1A0A0000000D4948445200000001"));
        var file = new FormFile(data, 0, data.Length, "imageFile", "broken.png")
        { Headers = new HeaderDictionary(), ContentType = "image/png" };
        var service = new ImageStorageService(Mock.Of<IWebHostEnvironment>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsWebpAsync(file, "banners", 100, 100));
    }
}
