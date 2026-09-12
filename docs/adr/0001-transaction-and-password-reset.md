# ADR 0001 — Retry transaction và reset mật khẩu nguyên tử

Trạng thái: chấp nhận.

## Vấn đề

Bật `EnableRetryOnFailure` nhưng checkout/callback mở transaction trực tiếp gây `InvalidOperationException`. Reset mật khẩu gọi RemovePassword trước AddPassword, khiến mất mật khẩu cũ nếu mật khẩu mới không đạt chính sách.

## Quyết định

Helper `DatabaseTransaction.ExecuteAsync` replay toàn bộ unit of work, tải lại entity ở mỗi lần thử; side effect nằm ngoài delegate. Cập nhật kho có điều kiện và khóa order bảo vệ trạng thái.

Giữ định dạng link reset hiện tại. Token ngẫu nhiên chỉ lưu hash. Sau khi xác thực link tùy chỉnh, khóa user, dùng GeneratePasswordResetTokenAsync/ResetPasswordAsync của Identity và vô hiệu hóa các token còn lại trong cùng transaction. Identity quản lý password hash và security stamp.

## Kiểm chứng và đánh đổi

Regression test tái hiện hai lỗi trước sửa. Test sau sửa bao gồm lỗi tạm thời sau SaveChanges, callback/hủy đồng thời, hai khách tranh tồn kho/voucher, mật khẩu không hợp lệ và token dùng lặp/đồng thời.

Helper sở hữu tracking trong scope; caller không để thay đổi chưa lưu trước khi gọi. Commit không rõ kết quả và bảo đảm giao email cuối cùng cần đối soát/idempotency/outbox khi phát triển tiếp.

Tham khảo: [EF Core connection resiliency](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency#execution-strategies-and-transactions).
