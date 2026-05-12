using EcommerceApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await MarkInitialMigrationForLegacyDatabaseAsync(db);
        await db.Database.MigrateAsync();

        foreach (var role in new[] { "Admin", "User" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        await EnsureUserAsync(userManager, "admin@shop.vn", "Quản trị viên", "Hồ Chí Minh", "0900000000", "Admin@123", "Admin");
        var sampleUsers = new List<ApplicationUser>
        {
            await EnsureUserAsync(userManager, "khachhang1@shop.vn", "Minh Anh", "Hà Nội", "0911111111", "User@123", "User"),
            await EnsureUserAsync(userManager, "khachhang2@shop.vn", "Quốc Huy", "Đà Nẵng", "0911111112", "User@123", "User"),
            await EnsureUserAsync(userManager, "khachhang3@shop.vn", "Thanh Mai", "Hồ Chí Minh", "0911111113", "User@123", "User"),
            await EnsureUserAsync(userManager, "khachhang4@shop.vn", "Hoàng Nam", "Cần Thơ", "0911111114", "User@123", "User"),
            await EnsureUserAsync(userManager, "khachhang5@shop.vn", "Linh Chi", "Hải Phòng", "0911111115", "User@123", "User")
        };

        var categories = await EnsureCategoriesAsync(db);
        await EnsureProductsAsync(db, categories);
        await EnsureBannersAsync(db);
        await EnsureVouchersAsync(db);
        await EnsureReviewsAsync(db, sampleUsers);
    }

    private static async Task MarkInitialMigrationForLegacyDatabaseAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[AspNetRoles]') IS NOT NULL AND OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260511155424_InitialCreate', N'8.0.8');
END

IF OBJECT_ID(N'[AspNetRoles]') IS NOT NULL
   AND OBJECT_ID(N'[__EFMigrationsHistory]') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260511155424_InitialCreate')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260511155424_InitialCreate', N'8.0.8');
END");
    }

    private static async Task<ApplicationUser> EnsureUserAsync(UserManager<ApplicationUser> userManager, string email, string fullName, string address, string phone, string password, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                Address = address,
                PhoneNumber = phone
            };
            await userManager.CreateAsync(user, password);
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }

        return user;
    }

    private static async Task<List<Category>> EnsureCategoriesAsync(AppDbContext db)
    {
        var seeds = new[]
        {
            new Category { Name = "Điện thoại", Slug = "dien-thoai" },
            new Category { Name = "Laptop", Slug = "laptop" },
            new Category { Name = "Phụ kiện", Slug = "phu-kien" },
            new Category { Name = "Màn hình", Slug = "man-hinh" },
            new Category { Name = "Đồng hồ thông minh", Slug = "dong-ho-thong-minh" }
        };

        foreach (var seed in seeds)
        {
            if (!await db.Categories.AnyAsync(category => category.Slug == seed.Slug))
            {
                db.Categories.Add(seed);
            }
        }

        await db.SaveChangesAsync();
        return await db.Categories.OrderBy(category => category.Id).ToListAsync();
    }

    private static async Task EnsureProductsAsync(AppDbContext db, List<Category> categories)
    {
        if (await db.Products.CountAsync() >= 30)
        {
            await EnsureDiscountsAsync(db);
            return;
        }

        var phone = categories.First(category => category.Slug == "dien-thoai");
        var laptop = categories.First(category => category.Slug == "laptop");
        var accessory = categories.First(category => category.Slug == "phu-kien");
        var monitor = categories.First(category => category.Slug == "man-hinh");
        var watch = categories.First(category => category.Slug == "dong-ho-thong-minh");

        var products = new[]
        {
            P("iPhone 15 Pro", "Camera 48MP, chip A17 Pro, khung titan nhẹ và bền.", 28990000, 18, "https://images.unsplash.com/photo-1695048133142-1a20484d2569?auto=format&fit=crop&w=900&q=80", phone.Id, true, 30),
            P("Samsung Galaxy S24", "Màn hình rực rỡ, AI tiện dụng, pin bền cho cả ngày.", 21990000, 25, "https://images.unsplash.com/photo-1610945265064-0e34e5519bbf?auto=format&fit=crop&w=900&q=80", phone.Id, true, 29),
            P("Xiaomi 14", "Cấu hình mạnh, sạc nhanh, camera Leica gọn trong tay.", 14990000, 32, "https://images.unsplash.com/photo-1598327105666-5b89351aff97?auto=format&fit=crop&w=900&q=80", phone.Id, false, 28),
            P("OPPO Reno 11", "Thiết kế mỏng, chụp chân dung đẹp, sạc nhanh tiện lợi.", 10990000, 22, "https://images.unsplash.com/photo-1605236453806-6ff36851218e?auto=format&fit=crop&w=900&q=80", phone.Id, false, 27),
            P("Vivo V30", "Màn hình cong, camera selfie sắc nét, pin lớn.", 11990000, 20, "https://images.unsplash.com/photo-1592750475338-74b7b21085ab?auto=format&fit=crop&w=900&q=80", phone.Id, false, 26),
            P("Realme 12 Pro", "Hiệu năng tốt trong tầm giá, thiết kế nổi bật.", 8990000, 28, "https://images.unsplash.com/photo-1601784551446-20c9e07cdbdb?auto=format&fit=crop&w=900&q=80", phone.Id, false, 25),
            P("Google Pixel 8", "Android thuần, camera thông minh, cập nhật lâu dài.", 16990000, 14, "https://images.unsplash.com/photo-1616348436168-de43ad0db179?auto=format&fit=crop&w=900&q=80", phone.Id, true, 24),
            P("Sony Xperia 1 V", "Màn hình 4K, quay video chuyên sâu, âm thanh chất lượng.", 24990000, 8, "https://images.unsplash.com/photo-1606041011872-596597976b25?auto=format&fit=crop&w=900&q=80", phone.Id, false, 23),

            P("MacBook Air M3", "Laptop mỏng nhẹ, pin lâu, hiệu năng ổn định.", 27990000, 12, "https://images.unsplash.com/photo-1517336714731-489689fd1ca8?auto=format&fit=crop&w=900&q=80", laptop.Id, true, 22),
            P("Dell XPS 13", "Vỏ nhôm chắc chắn, màn hình đẹp, trải nghiệm cao cấp.", 32990000, 9, "https://images.unsplash.com/photo-1496181133206-80ce9b88a853?auto=format&fit=crop&w=900&q=80", laptop.Id, false, 21),
            P("ASUS Vivobook 15", "OLED sắc nét, thiết kế gọn, phù hợp học tập và văn phòng.", 15990000, 21, "https://images.unsplash.com/photo-1588872657578-7efd1f1555ed?auto=format&fit=crop&w=900&q=80", laptop.Id, true, 20),
            P("Lenovo ThinkPad X1", "Bàn phím tốt, độ bền cao, tối ưu cho công việc.", 34990000, 7, "https://images.unsplash.com/photo-1541807084-5c52b6b3adef?auto=format&fit=crop&w=900&q=80", laptop.Id, false, 19),
            P("HP Spectre x360", "Gập xoay linh hoạt, màn hình cảm ứng, hoàn thiện cao.", 29990000, 10, "https://images.unsplash.com/photo-1484788984921-03950022c9ef?auto=format&fit=crop&w=900&q=80", laptop.Id, false, 18),
            P("Acer Swift Go", "Nhẹ, nhanh, giá dễ tiếp cận cho học tập.", 17990000, 19, "https://images.unsplash.com/photo-1525547719571-a2d4ac8945e2?auto=format&fit=crop&w=900&q=80", laptop.Id, false, 17),
            P("MSI Modern 15", "Hiệu năng ổn, màn lớn, phù hợp văn phòng.", 15990000, 17, "https://images.unsplash.com/photo-1531297484001-80022131f5a1?auto=format&fit=crop&w=900&q=80", laptop.Id, false, 16),

            P("Tai nghe Sony WH-1000XM5", "Chống ồn chủ động, âm thanh chi tiết, đeo êm.", 7990000, 30, "https://images.unsplash.com/photo-1618366712010-f4ae9c647dcb?auto=format&fit=crop&w=900&q=80", accessory.Id, true, 15),
            P("AirPods Pro 2", "Tai nghe không dây nhỏ gọn, chống ồn và xuyên âm.", 5890000, 34, "https://images.unsplash.com/photo-1600294037681-c80b4cb5b434?auto=format&fit=crop&w=900&q=80", accessory.Id, true, 14),
            P("Bàn phím cơ Keychron K2", "Bàn phím cơ không dây layout gọn, gõ êm và chắc.", 2490000, 16, "https://images.unsplash.com/photo-1618384887929-16ec33fab9ef?auto=format&fit=crop&w=900&q=80", accessory.Id, false, 13),
            P("Chuột Logitech MX Master 3S", "Chuột công thái học cho người làm việc nhiều giờ.", 2290000, 27, "https://images.unsplash.com/photo-1527814050087-3793815479db?auto=format&fit=crop&w=900&q=80", accessory.Id, false, 12),
            P("Hub USB-C Anker 7-in-1", "Mở rộng HDMI, USB, thẻ nhớ và sạc nhanh.", 1590000, 26, "https://images.unsplash.com/photo-1625842268584-8f3296236761?auto=format&fit=crop&w=900&q=80", accessory.Id, false, 11),
            P("Cáp USB-C Belkin", "Cáp bền, truyền dữ liệu ổn định, sạc nhanh.", 390000, 60, "https://images.unsplash.com/photo-1603539444875-76e7684265f6?auto=format&fit=crop&w=900&q=80", accessory.Id, false, 10),
            P("Sạc nhanh Ugreen 65W", "Sạc GaN gọn nhẹ, dùng cho điện thoại và laptop.", 890000, 48, "https://images.unsplash.com/photo-1615526675159-e248c3021d3f?auto=format&fit=crop&w=900&q=80", accessory.Id, false, 9),

            P("LG UltraFine 27 inch", "Màn hình 4K màu đẹp cho thiết kế và văn phòng.", 9990000, 9, "https://images.unsplash.com/photo-1527443224154-c4a3942d3acf?auto=format&fit=crop&w=900&q=80", monitor.Id, true, 8),
            P("Samsung Odyssey G5", "Tần số quét cao, cong nhẹ, tối ưu chơi game.", 7490000, 13, "https://images.unsplash.com/photo-1616588589676-62b3bd4ff6d2?auto=format&fit=crop&w=900&q=80", monitor.Id, false, 7),
            P("Dell UltraSharp U2723QE", "USB-C, màu chuẩn, chân đế linh hoạt.", 12990000, 8, "https://images.unsplash.com/photo-1547082299-de196ea013d6?auto=format&fit=crop&w=900&q=80", monitor.Id, false, 6),
            P("ASUS ProArt PA278QV", "Màn hình sáng tạo nội dung với màu cân chỉnh sẵn.", 6990000, 11, "https://images.unsplash.com/photo-1585792180666-f7347c490ee2?auto=format&fit=crop&w=900&q=80", monitor.Id, false, 5),

            P("Apple Watch Series 9", "Theo dõi sức khỏe, thông báo nhanh, hoàn thiện đẹp.", 10990000, 18, "https://images.unsplash.com/photo-1434493789847-2f02dc6ca35d?auto=format&fit=crop&w=900&q=80", watch.Id, true, 4),
            P("Samsung Galaxy Watch 6", "Màn hình đẹp, đo luyện tập, pin ổn định.", 6990000, 20, "https://images.unsplash.com/photo-1508685096489-7aacd43bd3b1?auto=format&fit=crop&w=900&q=80", watch.Id, false, 3),
            P("Garmin Venu 3", "Theo dõi thể thao chuyên sâu, pin dài ngày.", 9990000, 12, "https://images.unsplash.com/photo-1557438159-51eec7a6c9e8?auto=format&fit=crop&w=900&q=80", watch.Id, false, 2),
            P("Xiaomi Band 8", "Vòng đeo thông minh gọn nhẹ, giá tốt.", 990000, 45, "https://images.unsplash.com/photo-1575311373937-040b8e1fd5b6?auto=format&fit=crop&w=900&q=80", watch.Id, false, 1)
        };

        foreach (var product in products)
        {
            if (!await db.Products.AnyAsync(row => row.Name == product.Name))
            {
                db.Products.Add(product);
            }
        }

        await db.SaveChangesAsync();
        await EnsureDiscountsAsync(db);
    }

    private static async Task EnsureDiscountsAsync(AppDbContext db)
    {
        var discountMap = new Dictionary<string, int>
        {
            ["iPhone 15 Pro"] = 8,
            ["Samsung Galaxy S24"] = 10,
            ["MacBook Air M3"] = 7,
            ["AirPods Pro 2"] = 12,
            ["LG UltraFine 27 inch"] = 15
        };

        foreach (var item in discountMap)
        {
            var product = await db.Products.FirstOrDefaultAsync(row => row.Name == item.Key);
            if (product is not null && product.DiscountPercent == 0)
            {
                product.DiscountPercent = item.Value;
            }
        }

        await db.SaveChangesAsync();
    }

    private static Product P(string name, string description, decimal price, int stock, string imageUrl, int categoryId, bool featured, int daysAgo)
    {
        return new Product
        {
            Name = name,
            Description = description,
            Price = price,
            Stock = stock,
            ImageUrl = imageUrl,
            CategoryId = categoryId,
            IsFeatured = featured,
            CreatedAt = DateTime.UtcNow.AddDays(-daysAgo)
        };
    }

    private static async Task EnsureBannersAsync(AppDbContext db)
    {
        if (await db.Banners.AnyAsync())
        {
            return;
        }

        db.Banners.AddRange(
            new Banner { Title = "iPhone 15 Pro.\nTitan. Đỉnh cao.", Subtitle = "Camera 48MP, chip A17 Pro, thiết kế titan lần đầu tiên.", ImageUrl = "https://images.unsplash.com/photo-1695048133142-1a20484d2569?auto=format&fit=crop&w=1100&q=80", LinkUrl = "/Product", ButtonText = "Khám phá ngay", SortOrder = 1 },
            new Banner { Title = "MacBook Air M3.\nMỏng nhẹ, bền bỉ.", Subtitle = "Hiệu năng mượt mà cho học tập, công việc và sáng tạo mỗi ngày.", ImageUrl = "https://images.unsplash.com/photo-1517336714731-489689fd1ca8?auto=format&fit=crop&w=1100&q=80", LinkUrl = "/Product", ButtonText = "Xem laptop", SortOrder = 2 },
            new Banner { Title = "Phụ kiện chuẩn gu.\nGiá tốt hôm nay.", Subtitle = "Tai nghe, bàn phím, chuột và sạc nhanh cho góc làm việc gọn gàng.", ImageUrl = "https://images.unsplash.com/photo-1618384887929-16ec33fab9ef?auto=format&fit=crop&w=1100&q=80", LinkUrl = "/Product", ButtonText = "Mua phụ kiện", SortOrder = 3 }
        );
        await db.SaveChangesAsync();
    }

    private static async Task EnsureVouchersAsync(AppDbContext db)
    {
        var now = DateTime.UtcNow;
        var vouchers = new[]
        {
            new Voucher { Code = "WELCOME10", Type = VoucherType.Percent, Value = 10, MinOrderAmount = 500000, MaxDiscount = 150000, UsageLimit = 200, StartDate = now.AddDays(-7), EndDate = now.AddMonths(3), IsActive = true },
            new Voucher { Code = "FREESHIP", Type = VoucherType.FixedAmount, Value = 30000, MinOrderAmount = 300000, MaxDiscount = 30000, UsageLimit = 300, StartDate = now.AddDays(-7), EndDate = now.AddMonths(2), IsActive = true },
            new Voucher { Code = "SALE20", Type = VoucherType.Percent, Value = 20, MinOrderAmount = 1000000, MaxDiscount = 200000, UsageLimit = 150, StartDate = now.AddDays(-7), EndDate = now.AddMonths(1), IsActive = true }
        };

        foreach (var voucher in vouchers)
        {
            if (!await db.Vouchers.AnyAsync(row => row.Code == voucher.Code))
            {
                db.Vouchers.Add(voucher);
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureReviewsAsync(AppDbContext db, List<ApplicationUser> users)
    {
        var firstProduct = await db.Products.OrderBy(product => product.Id).FirstOrDefaultAsync();
        if (firstProduct is null || await db.Reviews.AnyAsync(review => review.ProductId == firstProduct.Id))
        {
            return;
        }

        var comments = new[]
        {
            "Máy đẹp, cầm chắc tay và hiệu năng rất mượt trong mọi tác vụ.",
            "Camera chụp thiếu sáng tốt hơn kỳ vọng, giao hàng cũng nhanh.",
            "Pin đủ dùng cả ngày, màn hình sáng và màu rất dễ chịu.",
            "Đóng gói cẩn thận, sản phẩm đúng mô tả, đáng tiền.",
            "Tư vấn rõ ràng, kích hoạt bảo hành nhanh, mình rất hài lòng."
        };

        for (var i = 0; i < comments.Length; i++)
        {
            db.Reviews.Add(new Review
            {
                ProductId = firstProduct.Id,
                UserId = users[i].Id,
                Rating = i == 1 ? 4 : 5,
                Comment = comments[i],
                CreatedAt = DateTime.UtcNow.AddDays(-(i + 1)),
                IsApproved = true
            });
        }

        await db.SaveChangesAsync();
    }
}
