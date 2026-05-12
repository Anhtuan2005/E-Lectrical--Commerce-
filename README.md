# Voltix - ASP.NET Core MVC E-Commerce

Voltix là website thương mại điện tử tiếng Việt dùng ASP.NET Core 8 MVC, Razor `.cshtml`, CSS thuần, JavaScript thuần, Entity Framework Core Code First và ASP.NET Core Identity.

## Tính năng chính

- Trang chủ, danh mục, danh sách sản phẩm, tìm kiếm, lọc giá, sắp xếp và phân trang.
- Chi tiết sản phẩm với breadcrumb, gallery ảnh, tồn kho, stepper số lượng, AJAX thêm giỏ, wishlist, chia sẻ link, review và sản phẩm liên quan.
- Checkout chỉ dành cho người đã đăng nhập, có địa chỉ tách theo tỉnh/thành phố, quận/huyện, phường/xã, số nhà/tên đường.
- Thanh toán COD hoặc VNPAY sandbox, callback cập nhật trạng thái đã thanh toán.
- Voucher AJAX, giỏ hàng AJAX, định dạng tiền tệ `1.250.000 ₫`.
- Đăng ký, đăng nhập split-screen Voltix, ghi nhớ đăng nhập, quên mật khẩu, đổi mật khẩu và hồ sơ cá nhân.
- Lịch sử đơn hàng, huỷ đơn đang chờ xác nhận kèm lý do.
- Admin dashboard có doanh thu, trạng thái đơn, top sản phẩm, hàng sắp hết.
- Admin quản lý sản phẩm, danh mục, banner, voucher, review, vận chuyển, người dùng và báo cáo.
- StockLog ghi nhận thay đổi tồn kho khi admin sửa sản phẩm.
- Rate limiting cho đăng nhập, gửi review và áp dụng voucher.
- Serilog ghi console và rolling file tại `logs/app-.log`.

## VNPAY sandbox

Cấu hình nằm trong `appsettings.json`:

```json
"Vnpay": {
  "TmnCode": "KJJMVRRU",
  "HashSecret": "UEA3CO3UWSC4S0A1I39S62SRZBM4G1SC",
  "BaseUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
  "ReturnUrl": "https://unperiodic-overt-mindy.ngrok-free.dev/payment/vnpay-return",
  "IpnUrl": "https://unperiodic-overt-mindy.ngrok-free.dev/payment/vnpay-ipn"
}
```

Lưu ý: `ReturnUrl` và `IpnUrl` phải khớp chính xác với URL đăng ký trên portal sandbox VNPAY. Nếu ngrok đổi domain, cập nhật lại cả portal và `appsettings.json`.

Thẻ test NCB:

- Số thẻ: `9704198526191432198`
- Tên: `NGUYEN VAN A`
- Ngày: `07/15`
- OTP: `123456`

## Tài khoản mặc định

- Email: `admin@shop.vn`
- Mật khẩu: `Admin@123`

## Yêu cầu môi trường

- .NET SDK 8 trở lên.
- SQL Server local.
- Connection string nằm trong `appsettings.json`.

## Cài đặt và chạy

```powershell
cd E:\Ecommerce_PF\EcommerceApp
dotnet restore
dotnet ef database update
dotnet run --urls http://localhost:5009
```

## Migration mới

Migration V3 mới nhất:

- `Data/Migrations/20260512130028_VoltixV3_ProfileInventoryProductImages.cs`

Migration này thêm:

- `ProductImages`
- `StockLogs`
- `Categories.Description`
- mở rộng `Orders.ShippingAddress` lên 500 ký tự

## Ghi chú kỹ thuật

- Không dùng TypeScript, React, Vue, Angular, Tailwind hoặc Bootstrap cho UI tuỳ biến.
- UI tiếng Việt, Razor `.cshtml`, CSS thuần, JavaScript thuần.
- VNPAY signature dùng `Uri.EscapeDataString` theo RFC 3986 và loại bỏ ký tự đặc biệt trong `vnp_OrderInfo`.
