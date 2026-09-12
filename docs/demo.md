# Kịch bản demo trong khoảng 3–5 phút

Chạy trên database demo riêng theo README; không bật SMTP hay giao dịch thật khi quay demo.

1. Mở trang chủ, tìm/lọc sản phẩm. Mở chi tiết, thử wishlist và so sánh.
2. Vào Smart PC, chọn mục tiêu/ngân sách, dựng cấu hình. Giải thích socket/RAM, nguồn dự trù và các phương án. Điểm số là heuristic.
3. Đăng nhập khách demo, thêm giỏ, checkout COD. Mở lịch sử/chi tiết rồi hủy đơn đang chờ.
4. Đăng nhập admin, xem dashboard và đơn vừa tạo. Mở sản phẩm PC để xem/sửa metadata. Dùng hai cửa sổ nếu muốn trình diễn SignalR.
5. Mở kết quả test: mua món cuối, callback lặp, reset mật khẩu lỗi. Dùng ERD/ADR giải thích quyết định.

Chỉ demo VNPAY/GHN khi cấu hình tài khoản sandbox. Hình trong `screenshots` là dữ liệu demo; repo chưa có video/URL triển khai công khai.

Metadata seed chỉ gán cho tên mẫu xác định. Socket i5-13400F đối chiếu [Intel](https://www.intel.com/content/www/us/en/products/compare.html?productIds=134590%2C230501); Ryzen 7800X3D đối chiếu [AMD](https://www.amd.com/en/products/processors/desktops/ryzen/7000-series/amd-ryzen-7-7800x3d.html). Đây không phải cơ chế đoán socket từ mọi tên CPU.
