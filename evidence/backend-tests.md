# Backend test evidence

Kiểm tra local ngày 24/09/2026 trên Windows, .NET SDK 9.0.200, test target `net8.0` và SQL Server local. Đây là kết quả của mã nguồn tại máy ở thời điểm chạy, không thay thế kết quả CI sau khi publish.

```powershell
dotnet test tests/EcommerceApp.Tests/EcommerceApp.Tests.csproj --configuration Release --no-restore --verbosity quiet
```

```text
Passed!  - Failed:     0, Passed:    82, Skipped:     0, Total:    82, Duration: 8 s - EcommerceApp.Tests.dll (net8.0)
```

Trên bản clone mới, bỏ `--no-restore` để `dotnet test` tự khôi phục dependency. Test tạo database `TechvoraTests_*` riêng trên SQL Server được cấu hình qua `TECHVORA_TEST_SQLSERVER`, chạy migration rồi xóa database sau khi hoàn tất. Nếu không đặt biến này, test dùng SQL Server `localhost` với Windows Authentication.

| Tình huống | Mã test |
| --- | --- |
| Hai khách tranh món hàng cuối, tồn kho và voucher được ghi nguyên tử | [`OrderPaymentTests.cs`](../tests/EcommerceApp.Tests/OrderPaymentTests.cs) |
| Callback thanh toán đồng thời/lặp và callback đến sau khi hủy đơn | [`OrderPaymentTests.cs`](../tests/EcommerceApp.Tests/OrderPaymentTests.cs), [`OrderLifecycleTests.cs`](../tests/EcommerceApp.Tests/OrderLifecycleTests.cs) |
| Phân quyền, thay đổi quyền admin và đặt lại mật khẩu | [`UserAccessTests.cs`](../tests/EcommerceApp.Tests/UserAccessTests.cs), [`PasswordResetTests.cs`](../tests/EcommerceApp.Tests/PasswordResetTests.cs) |

[Workflow CI](../.github/workflows/portfolio-tests.yml) chạy lại project test với SQL Server 2022 và lưu file TRX. Mốc **82/82** ở trên là lần chạy local, không phải kết quả CI đã publish.
