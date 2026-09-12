# Gợi ý trình bày trên CV

Điều chỉnh theo phần bạn trực tiếp thực hiện và giải thích được. Không thêm lượng người dùng, số đơn thật, mức giảm latency hoặc độ bao phủ test nếu chưa đo.

**Techvora — E-commerce web application**  
ASP.NET Core MVC, EF Core, SQL Server, ASP.NET Core Identity, JavaScript, SignalR.

- Implemented checkout, inventory and voucher workflows with SQL Server transactions, VNPAY sandbox payment callbacks and order management.
- Built a budget-based PC configuration engine using structured socket, memory and power specifications, separating the algorithm from HTTP/database access.
- Added SQL Server integration tests for inventory contention, duplicate callbacks, cancellation and password reset; configured GitHub Actions build and browser smoke checks.

Đặt link GitHub cạnh tên project. Thêm demo khi đã triển khai/kiểm chứng. Có thể thay bullet Build PC bằng GHN/SignalR nếu đó là phần bạn hiểu sâu hơn.

## Câu hỏi phỏng vấn nên chuẩn bị

- Hai người cùng mua món cuối: câu SQL nào bảo vệ tồn kho, rollback gồm dữ liệu nào?
- Retry query khác gì retry transaction? Vì sao tải lại entity sau rollback?
- IPN gửi hai lần hoặc thanh toán đến sau khi hủy thì sao?
- Vì sao không xóa mật khẩu cũ trước khi xác thực mật khẩu mới?
- Thiếu socket thì kết luận gì? Điểm hiệu năng được tính hay được đo?
- Vì sao test SQL Server thật thay vì EF InMemory?
- Giới hạn còn lại: outbox, đối soát commit/GHN, kiểm thử tải? Có thể giải thích vì sao callback thanh toán đến sau hạn phải vào luồng hoàn tiền thay vì mở lại đơn.
