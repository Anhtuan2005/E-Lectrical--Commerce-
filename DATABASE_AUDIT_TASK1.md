# Nhiệm vụ 1: Kiểm tra CSDL hiện tại

Nguồn kiểm tra: `Data/AppDbContext.cs`, `Models/*.cs`, `Data/Migrations/AppDbContextModelSnapshot.cs`.

## ERD hiện tại

```mermaid
erDiagram
    AspNetUsers {
        string Id PK
        string Email
        string PasswordHash
        string FullName
        string Address
    }

    AspNetRoles {
        string Id PK
        string Name
    }

    AspNetUserRoles {
        string UserId FK
        string RoleId FK
    }

    Categories {
        int Id PK
        string Name
        string Slug UK
        string Description
    }

    Products {
        int Id PK
        string Name
        string Description
        decimal Price
        int Stock
        string ImageUrl
        int CategoryId FK
        int DiscountPercent
        bool IsFeatured
        bool IsDeleted
        datetime CreatedAt
    }

    ProductImages {
        int Id PK
        int ProductId FK
        string ImageUrl
        int SortOrder
    }

    Carts {
        int Id PK
        string SessionId
        string UserId FK
        datetime CreatedAt
    }

    CartItems {
        int Id PK
        int CartId FK
        int ProductId FK
        int Quantity
    }

    Orders {
        int Id PK
        string UserId FK
        string Status
        decimal TotalAmount
        string VoucherCode
        decimal DiscountAmount
        string ShippingAddress
        string PaymentMethod
        string RecipientName
        string RecipientPhone
        string VnpayTransactionId
        string VnpayResponseCode
        bool IsPaid
        datetime PaidAt
        string CancelledReason
        datetime CreatedAt
        datetime UpdatedAt
    }

    OrderItems {
        int Id PK
        int OrderId FK
        int ProductId FK
        int Quantity
        decimal UnitPrice
    }

    ShippingInfos {
        int Id PK
        int OrderId FK
        string Carrier
        string TrackingCode
        datetime ShippedAt
        datetime EstimatedDelivery
        string Status
    }

    WishlistItems {
        int Id PK
        string UserId FK
        int ProductId FK
        datetime AddedAt
    }

    Reviews {
        int Id PK
        int ProductId FK
        string UserId FK
        int Rating
        string Comment
        datetime CreatedAt
        bool IsApproved
    }

    Vouchers {
        int Id PK
        string Code UK
        int Type
        decimal Value
        decimal MinOrderAmount
        decimal MaxDiscount
        int UsageLimit
        int UsedCount
        datetime StartDate
        datetime EndDate
        bool IsActive
    }

    VoucherUsages {
        int Id PK
        int VoucherId FK
        string UserId FK
        int OrderId FK
        datetime UsedAt
    }

    StockLogs {
        int Id PK
        int ProductId FK
        int ChangeAmount
        string Reason
        datetime ChangedAt
        string ChangedByUserId FK
    }

    Banners {
        int Id PK
        string Title
        string Subtitle
        string ImageUrl
        string LinkUrl
        string ButtonText
        int SortOrder
        bool IsActive
    }

    AspNetUsers ||--o{ AspNetUserRoles : has
    AspNetRoles ||--o{ AspNetUserRoles : has

    AspNetUsers ||--o{ Carts : owns
    AspNetUsers ||--o{ Orders : places
    AspNetUsers ||--o{ WishlistItems : saves
    AspNetUsers ||--o{ Reviews : writes
    AspNetUsers ||--o{ VoucherUsages : uses
    AspNetUsers ||--o{ StockLogs : changes

    Categories ||--o{ Products : contains
    Products ||--o{ ProductImages : has
    Products ||--o{ CartItems : appears_in
    Products ||--o{ OrderItems : appears_in
    Products ||--o{ WishlistItems : appears_in
    Products ||--o{ Reviews : receives
    Products ||--o{ StockLogs : tracked_by

    Carts ||--o{ CartItems : contains
    Orders ||--o{ OrderItems : contains
    Orders ||--|| ShippingInfos : has
    Orders ||--o{ VoucherUsages : records
    Vouchers ||--o{ VoucherUsages : used_by
```

## Điểm bất hợp lý

1. `Orders.Status`, `Orders.PaymentMethod`, `ShippingInfos.Status` đang lưu chuỗi tự do, không có bảng danh mục hoặc ràng buộc CHECK. Dữ liệu dễ bị lệch chính tả/trạng thái ngoài luồng.

2. Trạng thái thanh toán chưa đủ rõ: `Orders` chỉ có `IsPaid`, `PaidAt`, `VnpayResponseCode`, `VnpayTransactionId`. Thiếu bảng/field `PaymentStatus` để phân biệt `Pending`, `Paid`, `Failed`, `Refunded`, `Cancelled`.

3. Voucher bị lưu trùng logic: `Orders.VoucherCode` và `VoucherUsages` cùng ghi nhận mã đã dùng. Dễ lệch dữ liệu nếu một bên cập nhật, một bên không.

4. `VoucherUsages.OrderId` không có unique constraint, nên về mặt CSDL một đơn hàng có thể gắn nhiều dòng voucher usage dù nghiệp vụ hiện tại chỉ áp dụng một mã.

5. `Products.ImageUrl` và bảng `ProductImages` cùng lưu ảnh sản phẩm. Không có quy tắc rõ ảnh nào là ảnh chính, dễ không đồng bộ.

6. `CartItems` thiếu unique constraint `(CartId, ProductId)`. CSDL vẫn cho phép một giỏ có nhiều dòng trùng sản phẩm nếu lỗi xảy ra ngoài service.

7. `OrderItems` chỉ lưu `ProductId`, `Quantity`, `UnitPrice`; thiếu snapshot tên sản phẩm/SKU tại thời điểm mua. Nếu sản phẩm đổi tên hoặc bị xóa vật lý, lịch sử đơn hàng mất ngữ cảnh.

8. Địa chỉ bị lưu dạng chuỗi ở `AspNetUsers.Address` và `Orders.ShippingAddress`, trong khi checkout lại có tỉnh/quận/phường ở UI. CSDL chưa chuẩn hóa địa chỉ, khó thống kê/vận chuyển/lọc theo khu vực.

## Ghi chú kiểm tra nhanh

- Không thiếu bảng chi tiết đơn hàng: đã có `OrderItems`.
- Không lưu mật khẩu plain-text: ASP.NET Identity dùng `AspNetUsers.PasswordHash`.
