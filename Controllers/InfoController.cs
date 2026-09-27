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
                Section("Techvora làm gì", "Chọn lọc điện thoại chính hãng theo từng thương hiệu.", "Tối ưu trang sản phẩm để khách xem giá, thông số và tồn kho trong một luồng liền mạch.", "Duy trì quy trình đặt hàng và thanh toán COD rõ ràng."),
                Section("Cam kết vận hành", "Giá bán hiển thị rõ trước khi đặt hàng.", "Điện thoại có mô tả, hình ảnh và trạng thái tồn kho cụ thể.", "Thông tin giao nhận được xác nhận tại bước checkout.")
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
                Section("Thông tin được sử dụng", "Thông tin tài khoản như tên, email, số điện thoại và địa chỉ giao hàng.", "Thông tin cần thiết để tạo và giao đơn hàng.", "Dữ liệu tìm kiếm cơ bản giúp hiển thị đúng điện thoại khách cần."),
                Section("Cách bảo vệ dữ liệu", "Mật khẩu được lưu bằng cơ chế bảo mật của hệ thống đăng nhập.", "Đơn Sprint 1 chỉ sử dụng thanh toán khi nhận hàng COD.", "Khách hàng có thể cập nhật thông tin cá nhân trong hồ sơ tài khoản.")
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
                Section("Giá và tồn kho", "Giá và số lượng tồn có thể thay đổi theo thời điểm.", "Tổng tiền được hiển thị rõ trước khi xác nhận đặt hàng.", "Trường hợp sai sót hiển thị, Techvora sẽ thông báo trước khi tiếp tục xử lý đơn.")
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
                Section("Vị trí thường tuyển", "Tư vấn bán điện thoại.", "Vận hành đơn hàng và chăm sóc khách hàng.", "Quản trị nội dung, hình ảnh và thông số sản phẩm."),
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
            "Quy trình mua hàng trên Techvora được thiết kế để khách chọn điện thoại và đặt hàng COD nhanh.",
            new[]
            {
                Section("Các bước đặt hàng", "Chọn điện thoại và kiểm tra giá, thông số, tồn kho.", "Nhấn Thêm vào giỏ hoặc Mua ngay trên trang chi tiết.", "Kiểm tra giỏ hàng, nhập thông tin giao nhận và xác nhận thanh toán COD."),
                Section("Mẹo mua nhanh", "Đăng nhập trước khi checkout để dùng địa chỉ đã lưu.", "Dùng bộ lọc thương hiệu và mức giá để thu hẹp kết quả.", "So sánh thông số các mẫu điện thoại trước khi chọn.")
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
            "Liên hệ hỗ trợ",
            "mailto:hello@techvora.vn");
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
                Section("Tôi có thể thanh toán bằng gì?", "Phiên bản Sprint 1 hỗ trợ thanh toán khi nhận hàng COD.", "Tổng tiền cuối cùng hiển thị rõ trước khi xác nhận.", "Không yêu cầu nhập thông tin thẻ trên website."),
                Section("Làm sao chọn đúng điện thoại?", "Dùng danh mục theo hãng, tìm kiếm và bộ lọc giá.", "Mở trang chi tiết để xem hình ảnh, thông số và tồn kho.", "Dùng chức năng so sánh khi cần đối chiếu nhiều mẫu.")
            },
            "Xem sản phẩm",
            "/Product");
    }

    private IActionResult FooterPage(string eyebrow, string title, string intro, IReadOnlyList<FooterPageSectionViewModel> sections, string actionLabel = "Xem sản phẩm", string actionUrl = "/Product")
    {
        ViewData["MetaDescription"] = intro;
        ViewData["CanonicalPath"] = Request.Path.Value;
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
