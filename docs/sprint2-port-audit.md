# Đối chiếu trước khi chuyển phần dùng chung của Sprint 2

Nguồn: `Anhtuan2005/cnpm@25fbbf9` (Sprint 2). Đích: `Anhtuan2005/E-Lectrical--Commerce-@9882635` (`origin/master` ngày 21/10/2026). Nhánh làm việc: `codex/port-sprint2`.

| Nhóm | Đích đã có | Cần bổ sung | Không chuyển |
| --- | --- | --- | --- |
| So sánh | Tối đa bốn sản phẩm cùng danh mục; bảng giá và thông số | Gợi ý theo thông số linh kiện do Admin nhập kèm nguồn; cho phép khác hãng trong cùng danh mục | Pin, camera, bảng xếp hạng chip điện thoại, suy đoán từ tên sản phẩm, so khác loại linh kiện |
| Đổi trả / bảo hành | Quy trình tạo và quản trị; server kiểm tra mô tả tối thiểu 20 ký tự, số điện thoại, sản phẩm, số lượng và ảnh | Lỗi tức thời theo trường, đếm ký tự, trạng thái lỗi dễ nhận biết, hỗ trợ giảm chuyển động | Thay luồng hậu mãi hiện có |
| Kho | Điều chỉnh thủ công có log; bán/hủy cập nhật tồn | Trước/sau, loại giao dịch và mã đơn cho nhập, bán, hủy | Dữ liệu seed điện thoại của nguồn |
| Hủy đơn | Hủy đơn đang chờ và hoàn kho một lần | Hủy đơn đã xác nhận trước bàn giao vận chuyển | Hủy đơn đã bàn giao |
| Hoàn tiền | Trạng thái và ghi chú hoàn tiền thủ công | Kênh, mã đối soát, số tiền, người xác nhận cho COD/VNPAY | Tự đánh dấu đã hoàn khi chưa hoàn thực tế |
| Đánh giá | Đăng, duyệt/ẩn/xóa, hiển thị đánh giá | Phản hồi công khai từ cửa hàng | Xóa cơ chế duyệt hiện tại |
| Khác | Voucher, thanh toán, giao vận, Smart PC Builder, catalog linh kiện | Chỉ giữ phần cải thiện còn thiếu | Giới hạn Sprint 2, bộ lọc chỉ điện thoại |

Bước đôi bên xác nhận đã nhận hàng trả và đã nhận tiền chỉ là đề xuất, không có trong commit nguồn và không thuộc phạm vi chuyển.

## Kiểm thử và giới hạn dữ liệu

- `dotnet test tests/EcommerceApp.Tests/EcommerceApp.Tests.csproj` tự tạo database SQL Server tên `TechvoraPortTests_<GUID>`, chạy migration, rồi xóa database đó. Không dùng connection string ứng dụng đang chạy.
- `tests/browser/port-smoke.cjs` cần Playwright với Edge, một ứng dụng Development chạy trên `http://127.0.0.1:5089` cùng database kiểm thử mới, `Demo__Enabled=true`, và hai CPU demo (ID 31/32) có `CpuCores`, `CpuThreads`, `SpecificationSourceUrl` HTTPS trong **database kiểm thử**. Chạy một lần trên database mới vì bài kiểm tra tạo yêu cầu hỗ trợ và hoàn tiền cho đơn demo 1/2. Script chặn API địa chỉ bên ngoài và trả dữ liệu địa chỉ mẫu để kiểm thử checkout ổn định.
- Migration để `QuantityBefore`/`QuantityAfter` của log cũ là `NULL`: số dư lịch sử không thể dựng chính xác từ log cũ. Giao dịch mới có đủ số lượng trước/sau và mã đơn.
- URL nguồn và số đo do Admin nhập phải được đối chiếu với tài liệu chính thức trước khi công bố. Hệ thống kiểm tra URL HTTPS và dữ liệu số, không tự xác minh nội dung trang nguồn. Khi chưa nhập đủ dữ liệu, giao diện không chọn mẫu thắng.
- Migration coi các đơn COD **đã giao** trước đây là đã thu tiền và cập nhật `IsPaid/PaidAt`; cần đối chiếu ngoại lệ COD đã giao nhưng chưa thu tiền trước khi áp dụng lên dữ liệu thực.
