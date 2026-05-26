using EcommerceApp.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceApp.Controllers;

public class InfoController : Controller
{
    public IActionResult About()
    {
        return FooterPage(
            "Thông tin",
            "Về Techvora",
            "Techvora là cửa hàng công nghệ tập trung vào trải nghiệm mua sắm nhanh, rõ thông tin và dễ ra quyết định.",
            new[]
            {
                Section("Techvora làm gì", "Chọn lọc điện thoại, laptop, linh kiện PC và phụ kiện có nhu cầu thực tế cao.", "Tối ưu trang sản phẩm để khách xem giá, tồn kho, đánh giá và ưu đãi trong một luồng liền mạch.", "Duy trì chính sách thanh toán, giao hàng và bảo hành minh bạch."),
                Section("Cam kết vận hành", "Giá bán hiển thị rõ trước khi đặt hàng.", "Sản phẩm có mô tả, hình ảnh và trạng thái tồn kho cụ thể.", "Đơn hàng được cập nhật trạng thái để khách dễ theo dõi.")
            });
    }

    public IActionResult Privacy()
    {
        return FooterPage(
            "Thông tin",
            "Chính sách bảo mật",
            "Techvora chỉ thu thập thông tin cần thiết để xử lý đơn hàng, hỗ trợ khách hàng và cải thiện trải nghiệm mua sắm.",
            new[]
            {
                Section("Thông tin được sử dụng", "Thông tin tài khoản như tên, email, số điện thoại và địa chỉ giao hàng.", "Thông tin đơn hàng, thanh toán và lịch sử hỗ trợ.", "Dữ liệu duyệt web cơ bản giúp cải thiện tìm kiếm và gợi ý sản phẩm."),
                Section("Cách bảo vệ dữ liệu", "Mật khẩu được lưu bằng cơ chế bảo mật của hệ thống đăng nhập.", "Thông tin thanh toán trực tuyến được xử lý qua cổng thanh toán được cấu hình.", "Khách hàng có thể cập nhật thông tin cá nhân trong hồ sơ tài khoản.")
            },
            "Mở hồ sơ",
            "/Account/Profile");
    }

    public IActionResult Terms()
    {
        return FooterPage(
            "Thông tin",
            "Điều khoản sử dụng",
            "Khi sử dụng Techvora, khách hàng đồng ý tuân thủ các điều khoản mua hàng, thanh toán và sử dụng tài khoản dưới đây.",
            new[]
            {
                Section("Tài khoản và đặt hàng", "Khách hàng chịu trách nhiệm bảo mật thông tin đăng nhập.", "Đơn hàng chỉ được xác nhận khi thông tin giao nhận hợp lệ.", "Techvora có thể liên hệ để xác minh đơn hàng có giá trị lớn hoặc thông tin chưa rõ."),
                Section("Giá và khuyến mãi", "Giá, voucher và tồn kho có thể thay đổi theo thời điểm.", "Mỗi mã giảm giá áp dụng theo điều kiện riêng về thời gian, số lượt dùng và giá trị đơn hàng.", "Trường hợp sai sót hiển thị, Techvora sẽ thông báo trước khi tiếp tục xử lý đơn.")
            });
    }

    public IActionResult Careers()
    {
        return FooterPage(
            "Thông tin",
            "Tuyển dụng",
            "Techvora tìm kiếm những người thích sản phẩm công nghệ, vận hành gọn gàng và phục vụ khách hàng tử tế.",
            new[]
            {
                Section("Vị trí thường tuyển", "Tư vấn bán hàng công nghệ.", "Vận hành đơn hàng và chăm sóc khách hàng.", "Quản trị nội dung sản phẩm, hình ảnh và khuyến mãi."),
                Section("Cách ứng tuyển", "Gửi CV và vị trí mong muốn về hello@techvora.vn.", "Tiêu đề email theo mẫu: Ứng tuyển - Vị trí - Họ tên.", "Techvora sẽ phản hồi khi hồ sơ phù hợp với nhu cầu hiện tại.")
            },
            "Gửi email",
            "mailto:hello@techvora.vn");
    }

    public IActionResult BuyingGuide()
    {
        return FooterPage(
            "Hỗ trợ",
            "Hướng dẫn mua hàng",
            "Quy trình mua hàng trên Techvora được thiết kế để khách chọn sản phẩm, áp mã và thanh toán nhanh.",
            new[]
            {
                Section("Các bước đặt hàng", "Chọn sản phẩm và kiểm tra giá, tồn kho, đánh giá.", "Nhấn Thêm vào giỏ hoặc Mua ngay trên trang chi tiết.", "Kiểm tra giỏ hàng, nhập thông tin giao nhận và chọn phương thức thanh toán."),
                Section("Mẹo mua nhanh", "Đăng nhập trước khi checkout để lưu địa chỉ và dùng mã giảm giá.", "Kiểm tra tab Flash Sale để tìm sản phẩm đang giảm.", "Đọc đánh giá có ảnh trước khi chọn linh kiện hoặc phụ kiện.")
            });
    }

    public IActionResult Returns()
    {
        return FooterPage(
            "Hỗ trợ",
            "Chính sách đổi trả",
            "Techvora hỗ trợ đổi trả trong trường hợp sản phẩm lỗi, giao sai mẫu hoặc không đúng mô tả tại thời điểm nhận hàng.",
            new[]
            {
                Section("Điều kiện đổi trả", "Sản phẩm còn đầy đủ hộp, phụ kiện và hóa đơn hoặc thông tin đơn hàng.", "Thời gian yêu cầu trong vòng 7 ngày kể từ khi nhận hàng.", "Lỗi phát sinh không do va đập, vào nước, cháy nổ hoặc can thiệp phần cứng."),
                Section("Quy trình xử lý", "Liên hệ Techvora kèm mã đơn hàng và hình ảnh tình trạng sản phẩm.", "Bộ phận hỗ trợ xác nhận điều kiện đổi trả.", "Khách gửi sản phẩm về điểm tiếp nhận hoặc theo hướng dẫn của nhân viên hỗ trợ.")
            },
            "Xem đơn hàng",
            "/Order/History");
    }

    public IActionResult Warranty()
    {
        return FooterPage(
            "Hỗ trợ",
            "Bảo hành",
            "Sản phẩm tại Techvora được hỗ trợ bảo hành theo chính sách của hãng hoặc chính sách ghi trên từng trang sản phẩm.",
            new[]
            {
                Section("Thông tin cần có", "Mã đơn hàng hoặc tài khoản đã mua sản phẩm.", "Số serial, tình trạng lỗi và hình ảnh hoặc video mô tả lỗi.", "Phụ kiện đi kèm nếu lỗi liên quan đến bộ sản phẩm."),
                Section("Thời gian xử lý", "Techvora tiếp nhận và kiểm tra thông tin ban đầu.", "Sản phẩm được chuyển đến trung tâm bảo hành phù hợp.", "Thời gian phản hồi phụ thuộc vào hãng và loại lỗi thực tế.")
            },
            "Liên hệ hỗ trợ",
            "mailto:hello@techvora.vn");
    }

    public IActionResult Faq()
    {
        return FooterPage(
            "Hỗ trợ",
            "FAQ",
            "Những câu hỏi thường gặp khi mua hàng, thanh toán và theo dõi đơn tại Techvora.",
            new[]
            {
                Section("Tôi có thể thanh toán bằng gì?", "Techvora hỗ trợ COD và thanh toán trực tuyến VNPAY khi checkout.", "Mã giảm giá hợp lệ sẽ được kiểm tra trước khi đặt hàng.", "Tổng tiền cuối cùng hiển thị rõ trước khi xác nhận."),
                Section("Làm sao theo dõi đơn?", "Đăng nhập và mở mục Đơn hàng của tôi.", "Trạng thái đơn sẽ cập nhật theo quá trình xác nhận, giao hàng và hoàn tất.", "Nếu cần đổi địa chỉ, hãy liên hệ trước khi đơn được bàn giao cho vận chuyển."),
                Section("Khi nào tôi được đánh giá sản phẩm?", "Bạn có thể đánh giá sau khi có đơn hàng đã giao chứa sản phẩm đó.", "Mỗi tài khoản được đánh giá một lần cho mỗi sản phẩm.", "Đánh giá có thể kèm ảnh thực tế để hỗ trợ khách khác.")
            },
            "Xem sản phẩm",
            "/Product");
    }

    private IActionResult FooterPage(string eyebrow, string title, string intro, IReadOnlyList<FooterPageSectionViewModel> sections, string actionLabel = "Xem sản phẩm", string actionUrl = "/Product")
    {
        return View("Page", new FooterPageViewModel
        {
            Eyebrow = eyebrow,
            Title = title,
            Intro = intro,
            Sections = sections,
            ActionLabel = actionLabel,
            ActionUrl = actionUrl
        });
    }

    private static FooterPageSectionViewModel Section(string title, params string[] items)
    {
        return new FooterPageSectionViewModel
        {
            Title = title,
            Items = items
        };
    }
}
