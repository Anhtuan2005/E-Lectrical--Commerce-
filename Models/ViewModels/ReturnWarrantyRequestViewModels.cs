using EcommerceApp.Models;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models.ViewModels;

public class ReturnWarrantyRequestCreateViewModel
{
    public int OrderId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn loại yêu cầu")]
    public string Type { get; set; } = ReturnWarrantyRequestTypes.Return;

    [Required(ErrorMessage = "Vui lòng nhập người liên hệ")]
    [StringLength(120)]
    public string ContactName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
    [RegularExpression(@"^(03|07|08|09)\d{8}$", ErrorMessage = "Số điện thoại phải đúng định dạng di động Việt Nam")]
    public string ContactPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn lý do")]
    [StringLength(120)]
    public string Reason { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng mô tả tình trạng sản phẩm")]
    [MinLength(20, ErrorMessage = "Mô tả cần ít nhất 20 ký tự")]
    [StringLength(1200)]
    public string Description { get; set; } = string.Empty;

    [StringLength(120)]
    public string PreferredResolution { get; set; } = "Liên hệ tư vấn phương án phù hợp";

    public List<ReturnWarrantyRequestItemInput> Items { get; set; } = new();
    public List<IFormFile>? Images { get; set; }
    public Order? Order { get; set; }
}

public class ReturnWarrantyRequestItemInput
{
    public int OrderItemId { get; set; }
    public bool Selected { get; set; }
    public int Quantity { get; set; } = 1;
}

public class UserReturnWarrantyRequestsViewModel
{
    public IReadOnlyList<ReturnWarrantyRequest> Requests { get; set; } = Array.Empty<ReturnWarrantyRequest>();
}

public class AdminReturnWarrantyRequestsViewModel
{
    public IReadOnlyList<ReturnWarrantyRequest> Requests { get; set; } = Array.Empty<ReturnWarrantyRequest>();
    public string? Status { get; set; }
    public string? Type { get; set; }
    public string? Query { get; set; }
    public IEnumerable<string> Statuses { get; set; } = ReturnWarrantyRequestStatuses.All;
    public IEnumerable<string> Types { get; set; } = ReturnWarrantyRequestTypes.All;
}

public class NotificationIndexViewModel
{
    public IReadOnlyList<UserNotification> Notifications { get; set; } = Array.Empty<UserNotification>();
    public int UnreadCount { get; set; }
}
