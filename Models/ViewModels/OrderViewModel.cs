using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models.ViewModels;

public class CheckoutViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên người nhận")]
    [StringLength(120)]
    public string RecipientName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
    [RegularExpression(@"^(03|07|08|09)\d{8}$", ErrorMessage = "Số điện thoại phải đúng định dạng di động Việt Nam")]
    public string RecipientPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn tỉnh/thành phố")]
    [StringLength(100)]
    public string Province { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập quận/huyện")]
    [StringLength(100)]
    public string District { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập phường/xã")]
    [StringLength(100)]
    public string Ward { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số nhà, tên đường")]
    [StringLength(200)]
    public string Street { get; set; } = string.Empty;

    public bool UseProfileAddress { get; set; }

    public string ShippingAddress => $"{Street}, {Ward}, {District}, {Province}";

    [Required]
    public string PaymentMethod { get; set; } = "COD";

    public string? VoucherCode { get; set; }
    public decimal DiscountAmount { get; set; }

    public CartViewModel Cart { get; set; } = new();

    public string? ProfileAddress { get; set; }
}

public class OrderListViewModel
{
    public IEnumerable<Order> Orders { get; set; } = Enumerable.Empty<Order>();
    public string? Status { get; set; }
    public string? Customer { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public IEnumerable<string> Statuses { get; set; } = OrderStatuses.All;
}
