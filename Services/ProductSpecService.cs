using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;

namespace EcommerceApp.Services;

public interface IProductSpecService
{
    IReadOnlyList<ProductSpecViewModel> Build(Product product);
}

public class ProductSpecService : IProductSpecService
{
    public IReadOnlyList<ProductSpecViewModel> Build(Product product)
    {
        var name = product.Name;
        var normalizedName = NormalizeSpecText(name);
        var slug = product.Category?.Slug?.ToLowerInvariant() ?? string.Empty;
        var brand = DetectBrand(name);
        var knownSpecs = BuildKnownProductSpecs(normalizedName, brand);

        if (knownSpecs is not null)
        {
            return knownSpecs;
        }

        return slug switch
        {
            "dien-thoai" => new[]
            {
                Spec("Hãng sản xuất", brand),
                Spec("Bảo hành", "12 tháng"),
                Spec("Màn hình", "OLED/AMOLED 120Hz"),
                Spec("Chip xử lý", "Chip di động hiệu năng cao"),
                Spec("RAM / Bộ nhớ", "8GB-12GB RAM, 128GB-256GB lưu trữ"),
                Spec("Camera sau", "Camera AI nhiều ống kính"),
                Spec("Pin và sạc", "Pin dùng cả ngày, hỗ trợ sạc nhanh"),
                Spec("Hệ điều hành", normalizedName.Contains("iphone") ? "iOS" : "Android")
            },
            "laptop" => new[]
            {
                Spec("Hãng sản xuất", brand),
                Spec("Bảo hành", "12 tháng"),
                Spec("CPU", normalizedName.Contains("macbook") ? "Apple M3" : "Intel Core / AMD Ryzen thế hệ mới"),
                Spec("RAM", normalizedName.Contains("macbook") ? "8GB / 16GB unified memory" : "16GB DDR4/DDR5"),
                Spec("Ổ cứng", "SSD NVMe 512GB / 1TB"),
                Spec("Màn hình", normalizedName.Contains("vivobook") ? "15.6 inch OLED" : "13-15.6 inch, viền mỏng"),
                Spec("Kết nối", "Wi-Fi, Bluetooth, USB-C, USB-A, HDMI tùy dòng"),
                Spec("Pin", "Tối ưu cho học tập và làm việc di động")
            },
            "man-hinh" => new[]
            {
                Spec("Hãng sản xuất", brand),
                Spec("Bảo hành", "36 tháng"),
                Spec("Kích thước màn hình", normalizedName.Contains("27") ? "27 inch" : "24-32 inch"),
                Spec("Tấm nền", normalizedName.Contains("odyssey") ? "VA cong" : "IPS"),
                Spec("Độ phân giải", normalizedName.Contains("4k") || normalizedName.Contains("ultrafine") || normalizedName.Contains("u2723") ? "4K UHD (3840 x 2160)" : "2K/QHD (2560 x 1440)"),
                Spec("Tần số quét", normalizedName.Contains("odyssey") ? "144Hz" : "60Hz / 75Hz"),
                Spec("Thời gian phản hồi", normalizedName.Contains("odyssey") ? "1 ms" : "5 ms"),
                Spec("Không gian màu", "99-118% sRGB"),
                Spec("Độ sáng", "300-350 cd/m²"),
                Spec("Khử nhấp nháy", "Có"),
                Spec("Cổng kết nối", "HDMI, DisplayPort, USB-C tùy phiên bản"),
                Spec("Tương thích VESA", "100 x 100 mm")
            },
            "dong-ho-thong-minh" => new[]
            {
                Spec("Hãng sản xuất", brand),
                Spec("Bảo hành", "12 tháng"),
                Spec("Màn hình", normalizedName.Contains("band") ? "AMOLED dạng vòng đeo" : "AMOLED cảm ứng"),
                Spec("Kết nối", "Bluetooth, đồng bộ điện thoại"),
                Spec("Theo dõi sức khỏe", "Nhịp tim, giấc ngủ, vận động"),
                Spec("Chống nước", "5 ATM / IP tùy dòng"),
                Spec("Pin", normalizedName.Contains("garmin") || normalizedName.Contains("band") ? "Nhiều ngày sử dụng" : "1-2 ngày sử dụng")
            },
            "cpu" => new[]
            {
                Spec("Hãng sản xuất", brand),
                Spec("Bảo hành", "36 tháng"),
                Spec("Socket", normalizedName.Contains("7800") ? "AM5" : normalizedName.Contains("intel") || normalizedName.Contains("core i") ? "LGA1700" : "AM4"),
                Spec("Số nhân / luồng", normalizedName.Contains("13400") ? "10 nhân / 16 luồng" : normalizedName.Contains("7800") ? "8 nhân / 16 luồng" : "6 nhân / 12 luồng"),
                Spec("Xung nhịp", normalizedName.Contains("7800") ? "Up to 5.0GHz" : normalizedName.Contains("13400") ? "Up to 4.6GHz" : "Up to 4.4GHz"),
                Spec("Cache", normalizedName.Contains("x3d") ? "96MB L3 3D V-Cache" : normalizedName.Contains("13400") ? "20MB Intel Smart Cache" : "32MB L3"),
                Spec("TDP", normalizedName.Contains("7800") ? "120W" : "65W"),
                Spec("Đồ họa tích hợp", normalizedName.Contains("13400f") ? "Không" : "Tùy phiên bản")
            },
            "vga" => new[]
            {
                Spec("Hãng sản xuất", brand),
                Spec("Bảo hành", "36 tháng"),
                Spec("GPU", normalizedName.Contains("7800") ? "Radeon RX 7800 XT" : normalizedName.Contains("4070") ? "GeForce RTX 4070 SUPER" : "GeForce RTX 4060"),
                Spec("VRAM", normalizedName.Contains("16gb") ? "16GB GDDR6" : normalizedName.Contains("12gb") ? "12GB GDDR6X" : "8GB GDDR6"),
                Spec("Độ phân giải khuyến nghị", normalizedName.Contains("4060") ? "Gaming 1080p" : "Gaming 2K/QHD"),
                Spec("Cổng xuất hình", "HDMI, DisplayPort"),
                Spec("Nguồn đề xuất", normalizedName.Contains("4070") || normalizedName.Contains("7800") ? "650W trở lên" : "550W trở lên")
            },
            "ram" => new[]
            {
                Spec("Hãng sản xuất", brand),
                Spec("Bảo hành", "36 tháng"),
                Spec("Dung lượng", normalizedName.Contains("64gb") ? "64GB" : normalizedName.Contains("32gb") ? "32GB" : "16GB"),
                Spec("Chuẩn RAM", normalizedName.Contains("ddr5") ? "DDR5" : "DDR4"),
                Spec("Bus", normalizedName.Contains("6000") ? "6000MHz" : normalizedName.Contains("5600") ? "5600MHz" : "3200MHz"),
                Spec("Tản nhiệt", "Có heatspreader"),
                Spec("Tương thích", "Desktop PC")
            },
            "ssd" => new[]
            {
                Spec("Hãng sản xuất", brand),
                Spec("Bảo hành", "36 tháng"),
                Spec("Dung lượng", normalizedName.Contains("2tb") ? "2TB" : "1TB"),
                Spec("Chuẩn giao tiếp", "M.2 NVMe PCIe"),
                Spec("Tốc độ", normalizedName.Contains("980 pro") ? "PCIe 4.0 hiệu năng cao" : "NVMe tốc độ cao"),
                Spec("Phù hợp", "Hệ điều hành, game, project dung lượng lớn")
            },
            "mainboard" => new[]
            {
                Spec("Hãng sản xuất", brand),
                Spec("Bảo hành", "36 tháng"),
                Spec("Chipset", normalizedName.Contains("b650") ? "AMD B650" : normalizedName.Contains("b760") ? "Intel B760" : "AMD B550"),
                Spec("Socket", normalizedName.Contains("b760") ? "LGA1700" : normalizedName.Contains("b650") ? "AM5" : "AM4"),
                Spec("RAM hỗ trợ", normalizedName.Contains("ddr5") || normalizedName.Contains("b650") ? "DDR5" : "DDR4"),
                Spec("Kết nối", normalizedName.Contains("wifi") || normalizedName.Contains("ax") ? "Wi-Fi, LAN, USB, M.2" : "LAN, USB, M.2, PCIe")
            },
            "psu" => new[]
            {
                Spec("Hãng sản xuất", brand),
                Spec("Bảo hành", "36 tháng"),
                Spec("Công suất", normalizedName.Contains("850") ? "850W" : normalizedName.Contains("750") ? "750W" : "550W"),
                Spec("Chuẩn hiệu suất", normalizedName.Contains("bronze") ? "80 Plus Bronze" : "80 Plus Gold"),
                Spec("Kiểu dây", normalizedName.Contains("seasonic") ? "Full modular" : "Dây liền / bán modular tùy dòng"),
                Spec("Bảo vệ điện", "OVP, OPP, SCP")
            },
            "case" => new[]
            {
                Spec("Hãng sản xuất", brand),
                Spec("Bảo hành", "12 tháng"),
                Spec("Kích thước", "Mid Tower"),
                Spec("Hỗ trợ mainboard", "ATX / Micro-ATX / Mini-ITX"),
                Spec("Tản nhiệt", "Hỗ trợ fan và radiator AIO"),
                Spec("Không gian VGA", "Phù hợp VGA dài cho build gaming")
            },
            "cooling" => new[]
            {
                Spec("Hãng sản xuất", brand),
                Spec("Bảo hành", "24-36 tháng"),
                Spec("Loại tản nhiệt", normalizedName.Contains("aio") || normalizedName.Contains("frostflow") ? "Tản nhiệt nước AIO 240mm" : "Tản nhiệt khí CPU"),
                Spec("Socket hỗ trợ", "Intel / AMD phổ biến"),
                Spec("Độ ồn", "Tối ưu vận hành êm"),
                Spec("Phù hợp", "CPU gaming và làm việc")
            },
            _ => new[]
            {
                Spec("Hãng sản xuất", brand),
                Spec("Bảo hành", "12 tháng"),
                Spec("Danh mục", product.Category?.Name ?? "Sản phẩm"),
                Spec("Tình trạng", "Hàng mới"),
                Spec("Kho hiện tại", product.Stock > 0 ? $"Còn {product.Stock} sản phẩm" : "Hết hàng"),
                Spec("Thanh toán", "COD, chuyển khoản, VNPAY")
            }
        };
    }

    private static IReadOnlyList<ProductSpecViewModel>? BuildKnownProductSpecs(string normalizedName, string brand)
    {
        if (normalizedName.Contains("iphone 15 pro"))
        {
            return new[]
            {
                Spec("Hãng sản xuất", "Apple"),
                Spec("Bảo hành", "12 tháng"),
                Spec("Màn hình", "6.1 inch Super Retina XDR OLED, 120Hz"),
                Spec("Chip xử lý", "Apple A17 Pro"),
                Spec("RAM / Bộ nhớ", "8GB RAM, 128GB-1TB tùy phiên bản"),
                Spec("Camera sau", "48MP chính, 12MP ultra-wide, 12MP telephoto"),
                Spec("Pin và sạc", "USB-C, sạc nhanh, MagSafe"),
                Spec("Chất liệu", "Khung titan, mặt kính Ceramic Shield"),
                Spec("Hệ điều hành", "iOS")
            };
        }

        if (normalizedName.Contains("samsung galaxy s24"))
        {
            return new[]
            {
                Spec("Hãng sản xuất", "Samsung"),
                Spec("Bảo hành", "12 tháng"),
                Spec("Màn hình", "6.2 inch Dynamic AMOLED 2X, 120Hz"),
                Spec("Chip xử lý", "Snapdragon 8 Gen 3 / Exynos 2400 tùy thị trường"),
                Spec("RAM / Bộ nhớ", "8GB RAM, 128GB-256GB lưu trữ"),
                Spec("Camera sau", "50MP chính, 12MP ultra-wide, 10MP telephoto"),
                Spec("Pin và sạc", "4000mAh, sạc nhanh 25W"),
                Spec("Hệ điều hành", "Android, One UI")
            };
        }

        if (normalizedName.Contains("xiaomi 14"))
        {
            return PhoneSpecs("Xiaomi", "6.36 inch LTPO OLED, 120Hz", "Snapdragon 8 Gen 3", "12GB RAM, 256GB lưu trữ", "Cụm 3 camera Leica 50MP", "4610mAh, sạc nhanh 90W");
        }

        if (normalizedName.Contains("oppo reno 11"))
        {
            return PhoneSpecs("OPPO", "6.7 inch AMOLED, 120Hz", "Dimensity 7050 / Snapdragon tùy phiên bản", "8GB RAM, 256GB lưu trữ", "50MP OIS, camera chân dung", "5000mAh, sạc nhanh 67W");
        }

        if (normalizedName.Contains("vivo v30"))
        {
            return PhoneSpecs("Vivo", "6.78 inch AMOLED cong, 120Hz", "Snapdragon 7 Gen 3", "12GB RAM, 256GB lưu trữ", "Camera 50MP, selfie 50MP", "5000mAh, sạc nhanh 80W");
        }

        if (normalizedName.Contains("realme 12 pro"))
        {
            return PhoneSpecs("Realme", "6.7 inch AMOLED cong, 120Hz", "Snapdragon 6 Gen 1", "8GB RAM, 256GB lưu trữ", "50MP OIS, telephoto chân dung", "5000mAh, sạc nhanh 67W");
        }

        if (normalizedName.Contains("google pixel 8"))
        {
            return PhoneSpecs("Google", "6.2 inch OLED, 120Hz", "Google Tensor G3", "8GB RAM, 128GB-256GB lưu trữ", "50MP chính, 12MP ultra-wide", "4575mAh, sạc nhanh và sạc không dây");
        }

        if (normalizedName.Contains("sony xperia 1 v"))
        {
            return PhoneSpecs("Sony", "6.5 inch OLED 4K, 120Hz", "Snapdragon 8 Gen 2", "12GB RAM, 256GB lưu trữ", "48MP Exmor T, camera chuyên quay video", "5000mAh, sạc nhanh");
        }

        if (normalizedName.Contains("macbook air m3"))
        {
            return new[]
            {
                Spec("Hãng sản xuất", "Apple"),
                Spec("Bảo hành", "12 tháng"),
                Spec("CPU", "Apple M3, 8 nhân CPU"),
                Spec("GPU", "GPU 8-10 nhân tùy cấu hình"),
                Spec("RAM", "8GB / 16GB unified memory"),
                Spec("Ổ cứng", "SSD 256GB / 512GB tùy phiên bản"),
                Spec("Màn hình", "Liquid Retina 13.6 inch"),
                Spec("Pin", "Tối đa khoảng 18 giờ sử dụng"),
                Spec("Cổng kết nối", "MagSafe 3, 2 x Thunderbolt / USB 4")
            };
        }

        if (normalizedName.Contains("dell xps 13"))
        {
            return LaptopSpecs("Dell", "Intel Core i5/i7 hoặc Core Ultra tùy phiên bản", "16GB LPDDR5", "SSD NVMe 512GB", "13.4 inch FHD+ / OLED tùy cấu hình", "2 x Thunderbolt / USB-C, Wi-Fi 6E");
        }

        if (normalizedName.Contains("asus vivobook 15"))
        {
            return LaptopSpecs("ASUS", "Intel Core / AMD Ryzen tiết kiệm điện", "16GB DDR4/DDR5", "SSD NVMe 512GB", "15.6 inch OLED", "USB-C, USB-A, HDMI, Wi-Fi");
        }

        if (normalizedName.Contains("lenovo thinkpad x1"))
        {
            return LaptopSpecs("Lenovo", "Intel Core i7 / Core Ultra tùy phiên bản", "16GB LPDDR5", "SSD NVMe 512GB", "14 inch chống chói, viền mỏng", "Thunderbolt, HDMI, Wi-Fi 6E");
        }

        if (normalizedName.Contains("hp spectre x360"))
        {
            return LaptopSpecs("HP", "Intel Core i7 / Core Ultra", "16GB RAM", "SSD NVMe 1TB", "Màn hình cảm ứng OLED, gập xoay 360 độ", "Thunderbolt, USB-A, Wi-Fi 6E");
        }

        if (normalizedName.Contains("acer swift go"))
        {
            return LaptopSpecs("Acer", "Intel Core i5/i7 hoặc Core Ultra", "16GB RAM", "SSD NVMe 512GB", "14 inch OLED / IPS tùy phiên bản", "USB-C, HDMI, Wi-Fi 6");
        }

        if (normalizedName.Contains("msi modern 15"))
        {
            return LaptopSpecs("MSI", "Intel Core i5 / AMD Ryzen 5", "16GB RAM", "SSD NVMe 512GB", "15.6 inch Full HD IPS", "USB-C, USB-A, HDMI, Wi-Fi");
        }

        if (normalizedName.Contains("lg ultrafine 27"))
        {
            return MonitorSpecs("LG", "27 inch", "IPS", "4K UHD (3840 x 2160)", "60Hz", "5 ms", "99% sRGB / DCI-P3 tùy phiên bản", "USB-C, DisplayPort, HDMI", "100 x 100 mm");
        }

        if (normalizedName.Contains("samsung odyssey g5"))
        {
            return MonitorSpecs("Samsung", "27 inch", "VA cong", "QHD (2560 x 1440)", "144Hz", "1 ms", "HDR10, tối ưu gaming", "HDMI, DisplayPort", "75 x 75 mm");
        }

        if (normalizedName.Contains("dell ultrasharp u2723qe"))
        {
            return MonitorSpecs("Dell", "27 inch", "IPS Black", "4K UHD (3840 x 2160)", "60Hz", "5 ms", "100% sRGB, 98% DCI-P3", "USB-C 90W, HDMI, DisplayPort, USB Hub", "100 x 100 mm");
        }

        if (normalizedName.Contains("asus proart pa278qv"))
        {
            return MonitorSpecs("ASUS", "27 inch", "IPS", "QHD (2560 x 1440)", "75Hz", "5 ms", "100% sRGB / Rec.709, Calman Verified", "HDMI, DisplayPort, Mini DisplayPort", "100 x 100 mm");
        }

        if (normalizedName.Contains("apple watch series 9"))
        {
            return WatchSpecs("Apple", "Retina LTPO OLED always-on", "S9 SiP", "Nhịp tim, ECG, SpO2, giấc ngủ", "50m", "Khoảng 18 giờ");
        }

        if (normalizedName.Contains("samsung galaxy watch 6"))
        {
            return WatchSpecs("Samsung", "Super AMOLED always-on", "Exynos W930", "Nhịp tim, BIA, giấc ngủ, luyện tập", "5 ATM / IP68", "Khoảng 1-2 ngày");
        }

        if (normalizedName.Contains("garmin venu 3"))
        {
            return WatchSpecs("Garmin", "AMOLED", "Nền tảng Garmin", "GPS, nhịp tim, giấc ngủ, thể thao chuyên sâu", "5 ATM", "Tối đa khoảng 14 ngày");
        }

        if (normalizedName.Contains("xiaomi band 8"))
        {
            return WatchSpecs("Xiaomi", "1.62 inch AMOLED", "Vòng đeo thông minh", "Nhịp tim, SpO2, giấc ngủ, hơn 150 chế độ tập", "5 ATM", "Tối đa khoảng 16 ngày");
        }

        if (normalizedName.Contains("ryzen 5 5600"))
        {
            return new[]
            {
                Spec("Hãng sản xuất", "AMD"),
                Spec("Bảo hành", "36 tháng"),
                Spec("Socket", "AM4"),
                Spec("Số nhân / luồng", "6 nhân / 12 luồng"),
                Spec("Xung nhịp", "3.5GHz, boost tối đa 4.4GHz"),
                Spec("Cache", "35MB tổng cache"),
                Spec("TDP", "65W"),
                Spec("Đồ họa tích hợp", "Không"),
                Spec("RAM hỗ trợ", "DDR4")
            };
        }

        if (normalizedName.Contains("i5-13400f"))
        {
            return new[]
            {
                Spec("Hãng sản xuất", "Intel"),
                Spec("Bảo hành", "36 tháng"),
                Spec("Socket", "LGA1700"),
                Spec("Số nhân / luồng", "10 nhân / 16 luồng"),
                Spec("Xung nhịp", "Up to 4.6GHz"),
                Spec("Cache", "20MB Intel Smart Cache"),
                Spec("TDP", "65W, turbo tối đa khoảng 148W"),
                Spec("Đồ họa tích hợp", "Không"),
                Spec("RAM hỗ trợ", "DDR4 / DDR5 tùy mainboard")
            };
        }

        if (normalizedName.Contains("7800x3d"))
        {
            return new[]
            {
                Spec("Hãng sản xuất", "AMD"),
                Spec("Bảo hành", "36 tháng"),
                Spec("Socket", "AM5"),
                Spec("Số nhân / luồng", "8 nhân / 16 luồng"),
                Spec("Xung nhịp", "4.2GHz, boost tối đa 5.0GHz"),
                Spec("Cache", "104MB tổng cache, 3D V-Cache"),
                Spec("TDP", "120W"),
                Spec("Đồ họa tích hợp", "Radeon Graphics cơ bản"),
                Spec("RAM hỗ trợ", "DDR5")
            };
        }

        if (normalizedName.Contains("rtx 4060"))
        {
            return VgaSpecs("ASUS", "GeForce RTX 4060 OC", "8GB GDDR6", "128-bit", "Gaming 1080p, DLSS 3", "HDMI 2.1, DisplayPort 1.4a", "550W trở lên");
        }

        if (normalizedName.Contains("rtx 4070 super"))
        {
            return VgaSpecs("MSI", "GeForce RTX 4070 SUPER", "12GB GDDR6X", "192-bit", "Gaming 2K/QHD, render và livestream", "HDMI 2.1, DisplayPort 1.4a", "650W trở lên");
        }

        if (normalizedName.Contains("rx 7800 xt"))
        {
            return VgaSpecs("Sapphire", "Radeon RX 7800 XT", "16GB GDDR6", "256-bit", "Gaming 2K/QHD, VRAM rộng", "HDMI 2.1, DisplayPort 2.1", "700W trở lên");
        }

        if (normalizedName.Contains("kingston fury beast"))
        {
            return RamSpecs("Kingston", "16GB", "DDR4", "3200MHz", "CL16 tham khảo", "1.35V", "Desktop PC");
        }

        if (normalizedName.Contains("corsair vengeance"))
        {
            return RamSpecs("Corsair", "32GB (2 x 16GB)", "DDR5", "5600MHz", "CL36/CL40 tùy lô hàng", "1.25V tham khảo", "Desktop PC");
        }

        if (normalizedName.Contains("trident z5"))
        {
            return RamSpecs("G.Skill", "64GB (2 x 32GB)", "DDR5", "6000MHz", "CL30/CL36 tùy lô hàng", "1.35V tham khảo", "Desktop PC, XMP/EXPO tùy phiên bản");
        }

        if (normalizedName.Contains("980 pro"))
        {
            return SsdSpecs("Samsung", "1TB", "M.2 2280", "PCIe 4.0 x4 NVMe", "Đọc đến khoảng 7000MB/s", "Ghi đến khoảng 5000MB/s", "600TBW tham khảo");
        }

        if (normalizedName.Contains("sn580"))
        {
            return SsdSpecs("WD", "1TB", "M.2 2280", "PCIe 4.0 x4 NVMe", "Đọc đến khoảng 4150MB/s", "Ghi đến khoảng 4150MB/s", "600TBW tham khảo");
        }

        if (normalizedName.Contains("p3 plus"))
        {
            return SsdSpecs("Crucial", "2TB", "M.2 2280", "PCIe 4.0 x4 NVMe", "Đọc đến khoảng 5000MB/s", "Ghi đến khoảng 4200MB/s", "440TBW tham khảo");
        }

        if (normalizedName.Contains("b550m-plus"))
        {
            return MainboardSpecs("ASUS", "AMD B550", "AM4", "Micro-ATX", "DDR4", "PCIe 4.0, M.2 NVMe, LAN 2.5G", "Ryzen 3000/5000");
        }

        if (normalizedName.Contains("b760m-a"))
        {
            return MainboardSpecs("MSI", "Intel B760", "LGA1700", "Micro-ATX", "DDR5", "Wi-Fi, LAN 2.5G, M.2 NVMe, USB-C", "Intel Core thế hệ 12/13/14");
        }

        if (normalizedName.Contains("b650 aorus"))
        {
            return MainboardSpecs("Gigabyte", "AMD B650", "AM5", "ATX", "DDR5", "Wi-Fi 6E, LAN 2.5G, M.2 NVMe", "Ryzen 7000/8000/9000 tùy BIOS");
        }

        if (normalizedName.Contains("cx550"))
        {
            return PsuSpecs("Corsair", "550W", "80 Plus Bronze", "ATX", "Dây liền", "OVP, OPP, SCP", "Build phổ thông, VGA tầm trung");
        }

        if (normalizedName.Contains("mwe gold 750"))
        {
            return PsuSpecs("Cooler Master", "750W", "80 Plus Gold", "ATX", "Dây liền / semi-modular tùy phiên bản", "OVP, OPP, SCP, OTP", "RTX 4070 và CPU hiệu năng cao");
        }

        if (normalizedName.Contains("focus gx 850"))
        {
            return PsuSpecs("Seasonic", "850W", "80 Plus Gold", "ATX", "Full modular", "OVP, OPP, SCP, OTP", "Build gaming cao cấp / workstation");
        }

        if (normalizedName.Contains("nzxt h5 flow"))
        {
            return CaseSpecs("NZXT", "Mid Tower", "ATX / Micro-ATX / Mini-ITX", "Airflow mặt trước và đường gió riêng cho GPU", "VGA dài khoảng 365mm", "Radiator 240/280mm tùy vị trí");
        }

        if (normalizedName.Contains("lancool 216"))
        {
            return CaseSpecs("Lian Li", "Mid Tower", "E-ATX / ATX / Micro-ATX / Mini-ITX", "Mặt trước mesh, fan lớn tối ưu gió", "VGA dài khoảng 392mm", "Radiator 360mm tùy vị trí");
        }

        if (normalizedName.Contains("td500"))
        {
            return CaseSpecs("Cooler Master", "Mid Tower", "ATX / Micro-ATX / Mini-ITX", "Mặt trước mesh, quạt ARGB", "VGA dài khoảng 410mm", "Radiator 360mm tùy vị trí");
        }

        if (normalizedName.Contains("deepcool ak400"))
        {
            return CoolerSpecs("DeepCool", "Tản nhiệt khí CPU", "1 quạt 120mm", "4 heatpipe", "Intel LGA1700/1200, AMD AM4/AM5", "CPU tầm trung, vận hành êm");
        }

        if (normalizedName.Contains("frostflow x 240"))
        {
            return CoolerSpecs("ID-Cooling", "Tản nhiệt nước AIO 240mm", "2 quạt 120mm", "Radiator 240mm", "Intel / AMD phổ biến", "CPU gaming hiệu năng cao");
        }

        if (normalizedName.Contains("nh-d15"))
        {
            return CoolerSpecs("Noctua", "Tản nhiệt khí dual tower", "2 quạt 140mm", "6 heatpipe", "Intel / AMD phổ biến", "CPU cao cấp cần độ ồn thấp");
        }

        if (normalizedName.Contains("wh-1000xm5"))
        {
            return AccessorySpecs("Sony", "Tai nghe chụp tai chống ồn", "Bluetooth, chống ồn chủ động ANC", "Khoảng 30 giờ", "USB-C, jack 3.5mm", "Đàm thoại, làm việc, nghe nhạc");
        }

        if (normalizedName.Contains("airpods pro 2"))
        {
            return AccessorySpecs("Apple", "Tai nghe true wireless", "ANC, xuyên âm, Adaptive Audio", "Khoảng 6 giờ, thêm pin với hộp sạc", "USB-C / MagSafe tùy phiên bản", "iPhone, iPad, Mac");
        }

        if (normalizedName.Contains("keychron k2"))
        {
            return AccessorySpecs("Keychron", "Bàn phím cơ không dây", "Layout 75%, Bluetooth / USB-C", "Pin sạc tích hợp", "Switch cơ tùy phiên bản", "Windows, macOS");
        }

        if (normalizedName.Contains("mx master 3s"))
        {
            return AccessorySpecs("Logitech", "Chuột công thái học", "Bluetooth / Logi Bolt, cảm biến 8000 DPI", "Pin sạc USB-C", "Cuộn MagSpeed, nút lập trình", "Làm việc văn phòng, thiết kế");
        }

        if (normalizedName.Contains("anker 7-in-1"))
        {
            return AccessorySpecs("Anker", "Hub USB-C 7-in-1", "HDMI, USB-A, USB-C, thẻ nhớ tùy phiên bản", "Hỗ trợ sạc pass-through", "Vỏ nhôm gọn nhẹ", "Laptop USB-C");
        }

        if (normalizedName.Contains("belkin"))
        {
            return AccessorySpecs("Belkin", "Cáp USB-C", "Sạc và truyền dữ liệu", "Công suất tùy củ sạc", "Dây bền, đầu cắm chắc", "Điện thoại, laptop, phụ kiện USB-C");
        }

        if (normalizedName.Contains("ugreen 65w"))
        {
            return AccessorySpecs("Ugreen", "Củ sạc GaN 65W", "USB-C / USB-A tùy phiên bản", "Power Delivery, sạc nhanh", "Thiết kế gọn", "Điện thoại, tablet, laptop mỏng nhẹ");
        }

        return null;
    }

    private static IReadOnlyList<ProductSpecViewModel> PhoneSpecs(string brand, string display, string chip, string memory, string camera, string battery) => new[]
    {
        Spec("Hãng sản xuất", brand),
        Spec("Bảo hành", "12 tháng"),
        Spec("Màn hình", display),
        Spec("Chip xử lý", chip),
        Spec("RAM / Bộ nhớ", memory),
        Spec("Camera sau", camera),
        Spec("Pin và sạc", battery),
        Spec("Hệ điều hành", brand == "Apple" ? "iOS" : "Android")
    };

    private static IReadOnlyList<ProductSpecViewModel> LaptopSpecs(string brand, string cpu, string ram, string storage, string display, string ports) => new[]
    {
        Spec("Hãng sản xuất", brand),
        Spec("Bảo hành", "12 tháng"),
        Spec("CPU", cpu),
        Spec("RAM", ram),
        Spec("Ổ cứng", storage),
        Spec("Màn hình", display),
        Spec("Cổng kết nối", ports),
        Spec("Phù hợp", "Học tập, văn phòng, làm việc di động")
    };

    private static IReadOnlyList<ProductSpecViewModel> MonitorSpecs(string brand, string size, string panel, string resolution, string refreshRate, string response, string color, string ports, string vesa) => new[]
    {
        Spec("Hãng sản xuất", brand),
        Spec("Bảo hành", "36 tháng"),
        Spec("Kích thước màn hình", size),
        Spec("Tấm nền", panel),
        Spec("Độ phân giải", resolution),
        Spec("Tần số quét", refreshRate),
        Spec("Thời gian phản hồi", response),
        Spec("Không gian màu", color),
        Spec("Cổng kết nối", ports),
        Spec("Tương thích VESA", vesa)
    };

    private static IReadOnlyList<ProductSpecViewModel> WatchSpecs(string brand, string display, string chip, string health, string waterResistance, string battery) => new[]
    {
        Spec("Hãng sản xuất", brand),
        Spec("Bảo hành", "12 tháng"),
        Spec("Màn hình", display),
        Spec("Nền tảng", chip),
        Spec("Theo dõi sức khỏe", health),
        Spec("Chống nước", waterResistance),
        Spec("Pin", battery),
        Spec("Kết nối", "Bluetooth, đồng bộ điện thoại")
    };

    private static IReadOnlyList<ProductSpecViewModel> VgaSpecs(string brand, string gpu, string vram, string bus, string target, string ports, string psu) => new[]
    {
        Spec("Hãng sản xuất", brand),
        Spec("Bảo hành", "36 tháng"),
        Spec("GPU", gpu),
        Spec("VRAM", vram),
        Spec("Bus bộ nhớ", bus),
        Spec("Độ phân giải khuyến nghị", target),
        Spec("Cổng xuất hình", ports),
        Spec("Nguồn đề xuất", psu)
    };

    private static IReadOnlyList<ProductSpecViewModel> RamSpecs(string brand, string capacity, string type, string bus, string latency, string voltage, string compatible) => new[]
    {
        Spec("Hãng sản xuất", brand),
        Spec("Bảo hành", "36 tháng"),
        Spec("Dung lượng", capacity),
        Spec("Chuẩn RAM", type),
        Spec("Bus", bus),
        Spec("Độ trễ", latency),
        Spec("Điện áp", voltage),
        Spec("Tương thích", compatible)
    };

    private static IReadOnlyList<ProductSpecViewModel> SsdSpecs(string brand, string capacity, string form, string interfaceType, string read, string write, string endurance) => new[]
    {
        Spec("Hãng sản xuất", brand),
        Spec("Bảo hành", "36 tháng"),
        Spec("Dung lượng", capacity),
        Spec("Kích thước", form),
        Spec("Chuẩn giao tiếp", interfaceType),
        Spec("Tốc độ đọc", read),
        Spec("Tốc độ ghi", write),
        Spec("Độ bền ghi", endurance)
    };

    private static IReadOnlyList<ProductSpecViewModel> MainboardSpecs(string brand, string chipset, string socket, string form, string ram, string connections, string cpuSupport) => new[]
    {
        Spec("Hãng sản xuất", brand),
        Spec("Bảo hành", "36 tháng"),
        Spec("Chipset", chipset),
        Spec("Socket", socket),
        Spec("Kích thước", form),
        Spec("RAM hỗ trợ", ram),
        Spec("Kết nối", connections),
        Spec("CPU hỗ trợ", cpuSupport)
    };

    private static IReadOnlyList<ProductSpecViewModel> PsuSpecs(string brand, string wattage, string efficiency, string standard, string cable, string protection, string suitable) => new[]
    {
        Spec("Hãng sản xuất", brand),
        Spec("Bảo hành", "36 tháng"),
        Spec("Công suất", wattage),
        Spec("Chuẩn hiệu suất", efficiency),
        Spec("Chuẩn nguồn", standard),
        Spec("Kiểu dây", cable),
        Spec("Bảo vệ điện", protection),
        Spec("Phù hợp", suitable)
    };

    private static IReadOnlyList<ProductSpecViewModel> CaseSpecs(string brand, string size, string mainboard, string airflow, string gpu, string radiator) => new[]
    {
        Spec("Hãng sản xuất", brand),
        Spec("Bảo hành", "12 tháng"),
        Spec("Kích thước", size),
        Spec("Hỗ trợ mainboard", mainboard),
        Spec("Airflow", airflow),
        Spec("Không gian VGA", gpu),
        Spec("Tản nhiệt", radiator)
    };

    private static IReadOnlyList<ProductSpecViewModel> CoolerSpecs(string brand, string type, string fan, string heatsink, string socket, string suitable) => new[]
    {
        Spec("Hãng sản xuất", brand),
        Spec("Bảo hành", "24-36 tháng"),
        Spec("Loại tản nhiệt", type),
        Spec("Quạt", fan),
        Spec("Cấu trúc", heatsink),
        Spec("Socket hỗ trợ", socket),
        Spec("Phù hợp", suitable)
    };

    private static IReadOnlyList<ProductSpecViewModel> AccessorySpecs(string brand, string type, string feature, string power, string materialOrPort, string suitable) => new[]
    {
        Spec("Hãng sản xuất", brand),
        Spec("Bảo hành", "12 tháng"),
        Spec("Loại sản phẩm", type),
        Spec("Tính năng chính", feature),
        Spec("Pin / công suất", power),
        Spec("Kết nối / chất liệu", materialOrPort),
        Spec("Phù hợp", suitable)
    };

    private static ProductSpecViewModel Spec(string label, string value) => new()
    {
        Label = label,
        Value = value
    };

    private static string NormalizeSpecText(string value) => value.Trim().ToLowerInvariant();

    private static string DetectBrand(string name)
    {
        if (name.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("MacBook", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("AirPods", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Apple Watch", StringComparison.OrdinalIgnoreCase))
        {
            return "Apple";
        }

        if (name.Contains("Galaxy", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Odyssey", StringComparison.OrdinalIgnoreCase))
        {
            return "Samsung";
        }

        if (name.Contains("Ryzen", StringComparison.OrdinalIgnoreCase))
        {
            return "AMD";
        }

        if (name.Contains("Core i", StringComparison.OrdinalIgnoreCase))
        {
            return "Intel";
        }

        if (name.Contains("Pixel", StringComparison.OrdinalIgnoreCase))
        {
            return "Google";
        }

        if (name.Contains("UltraFine", StringComparison.OrdinalIgnoreCase))
        {
            return "LG";
        }

        var brands = new[]
        {
            "Apple", "Samsung", "Xiaomi", "OPPO", "Vivo", "Realme", "Google", "Sony",
            "Dell", "ASUS", "Lenovo", "HP", "Acer", "MSI", "LG", "Garmin", "AMD",
            "Intel", "Kingston", "Corsair", "G.Skill", "WD", "Crucial", "Gigabyte",
            "Cooler Master", "Seasonic", "NZXT", "Lian Li", "DeepCool", "Noctua",
            "Sapphire", "ID-Cooling", "Keychron", "Logitech", "Anker", "Belkin", "Ugreen"
        };

        return brands.FirstOrDefault(brand => name.Contains(brand, StringComparison.OrdinalIgnoreCase)) ?? "Techvora Select";
    }
}
