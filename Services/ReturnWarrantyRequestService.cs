using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Services;

public class ReturnWarrantyRequestService : IReturnWarrantyRequestService
{
    private const int MaxEvidenceImages = 5;
    private const long MaxEvidenceImageBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly IUserNotificationService _notificationService;
    private readonly ILogger<ReturnWarrantyRequestService> _logger;

    public ReturnWarrantyRequestService(
        AppDbContext db,
        IWebHostEnvironment environment,
        IUserNotificationService notificationService,
        ILogger<ReturnWarrantyRequestService> logger)
    {
        _db = db;
        _environment = environment;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<ReturnWarrantyRequestCreateViewModel?> BuildCreateModelAsync(int orderId, string userId)
    {
        var order = await LoadUserDeliveredOrder(orderId, userId).FirstOrDefaultAsync();
        if (order is null)
        {
            return null;
        }

        return new ReturnWarrantyRequestCreateViewModel
        {
            OrderId = order.Id,
            ContactName = order.RecipientName,
            ContactPhone = order.RecipientPhone,
            Order = order,
            Items = order.Items.Select(item => new ReturnWarrantyRequestItemInput
            {
                OrderItemId = item.Id,
                Selected = true,
                Quantity = item.Quantity
            }).ToList()
        };
    }

    public async Task<ReturnWarrantyRequest> CreateAsync(string userId, ReturnWarrantyRequestCreateViewModel model)
    {
        if (!ReturnWarrantyRequestTypes.All.Contains(model.Type))
        {
            throw new InvalidOperationException("Loại yêu cầu không hợp lệ.");
        }

        var order = await LoadUserDeliveredOrder(model.OrderId, userId).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Chỉ có thể gửi yêu cầu cho đơn hàng đã giao.");

        var selectedInputs = model.Items
            .Where(item => item.Selected)
            .ToDictionary(item => item.OrderItemId, item => Math.Max(1, item.Quantity));

        if (selectedInputs.Count == 0)
        {
            throw new InvalidOperationException("Vui lòng chọn ít nhất một sản phẩm cần hỗ trợ.");
        }

        var imageFiles = (model.Images ?? new List<IFormFile>()).Where(file => file.Length > 0).Take(MaxEvidenceImages + 1).ToList();
        var imageError = ValidateImages(imageFiles);
        if (!string.IsNullOrWhiteSpace(imageError))
        {
            throw new InvalidOperationException(imageError);
        }

        var request = new ReturnWarrantyRequest
        {
            UserId = userId,
            OrderId = order.Id,
            Type = model.Type,
            ContactName = model.ContactName.Trim(),
            ContactPhone = model.ContactPhone.Trim(),
            Reason = model.Reason.Trim(),
            Description = model.Description.Trim(),
            PreferredResolution = string.IsNullOrWhiteSpace(model.PreferredResolution)
                ? "Liên hệ tư vấn phương án phù hợp"
                : model.PreferredResolution.Trim()
        };

        foreach (var orderItem in order.Items)
        {
            if (!selectedInputs.TryGetValue(orderItem.Id, out var requestedQuantity))
            {
                continue;
            }

            request.Items.Add(new ReturnWarrantyRequestItem
            {
                OrderItemId = orderItem.Id,
                ProductId = orderItem.ProductId,
                ProductNameSnapshot = orderItem.Product?.Name ?? $"Sản phẩm #{orderItem.ProductId}",
                Quantity = Math.Clamp(requestedQuantity, 1, orderItem.Quantity)
            });
        }

        if (!request.Items.Any())
        {
            throw new InvalidOperationException("Không tìm thấy sản phẩm hợp lệ trong đơn hàng.");
        }

        var savedImages = await SaveImagesAsync(imageFiles);
        foreach (var image in savedImages.Select((url, index) => new ReturnWarrantyRequestImage { ImageUrl = url, SortOrder = index }))
        {
            request.Images.Add(image);
        }

        _db.ReturnWarrantyRequests.Add(request);
        await _db.SaveChangesAsync();

        var userLink = $"/ReturnWarranty/Details/{request.Id}";
        var adminLink = $"/Admin/ReturnWarranty/{request.Id}";
        await _notificationService.CreateAsync(
            userId,
            $"Đã nhận yêu cầu {request.Type.ToLowerInvariant()}",
            $"Techvora đã nhận yêu cầu cho đơn #DH{order.Id:D4} và sẽ phản hồi sau khi kiểm tra.",
            NotificationTypes.Support,
            userLink);
        await _notificationService.CreateForAdminsAsync(
            $"Yêu cầu {request.Type.ToLowerInvariant()} mới",
            $"Đơn #DH{order.Id:D4} vừa có yêu cầu {request.Type.ToLowerInvariant()} từ {order.User?.FullName ?? order.User?.Email ?? "khách hàng"}.",
            NotificationTypes.Support,
            adminLink);

        _logger.LogInformation("User {UserId} created return/warranty request {RequestId} for order {OrderId}", userId, request.Id, order.Id);
        return request;
    }

    public async Task<IReadOnlyList<ReturnWarrantyRequest>> GetUserRequestsAsync(string userId)
    {
        return await RequestQuery()
            .Where(request => request.UserId == userId)
            .OrderByDescending(request => request.CreatedAt)
            .ToListAsync();
    }

    public Task<ReturnWarrantyRequest?> GetUserRequestAsync(int id, string userId)
    {
        return RequestQuery().FirstOrDefaultAsync(request => request.Id == id && request.UserId == userId);
    }

    public Task<ReturnWarrantyRequest?> GetRequestAsync(int id)
    {
        return RequestQuery().FirstOrDefaultAsync(request => request.Id == id);
    }

    public async Task<AdminReturnWarrantyRequestsViewModel> GetAdminRequestsAsync(string? status, string? type, string? query)
    {
        var requests = RequestQuery().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            requests = requests.Where(request => request.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            requests = requests.Where(request => request.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var keyword = query.Trim();
            var hasNumericKeyword = int.TryParse(keyword.TrimStart('#'), out var numericKeyword);
            requests = requests.Where(request =>
                request.ContactName.Contains(keyword) ||
                request.ContactPhone.Contains(keyword) ||
                (request.User != null && (request.User.FullName.Contains(keyword) || request.User.Email!.Contains(keyword))) ||
                (hasNumericKeyword && (request.Id == numericKeyword || request.OrderId == numericKeyword)));
        }

        return new AdminReturnWarrantyRequestsViewModel
        {
            Status = status,
            Type = type,
            Query = query,
            Requests = await requests.OrderByDescending(request => request.CreatedAt).ToListAsync()
        };
    }

    public async Task<bool> UpdateStatusAsync(int id, string status, string? adminNote)
    {
        if (!ReturnWarrantyRequestStatuses.All.Contains(status))
        {
            return false;
        }

        var request = await _db.ReturnWarrantyRequests.Include(row => row.Order).FirstOrDefaultAsync(row => row.Id == id);
        if (request is null)
        {
            return false;
        }

        var oldStatus = request.Status;
        request.Status = status;
        request.AdminNote = string.IsNullOrWhiteSpace(adminNote) ? null : adminNote.Trim();
        request.UpdatedAt = DateTime.UtcNow;
        if (status != ReturnWarrantyRequestStatuses.Submitted)
        {
            request.ReviewedAt ??= DateTime.UtcNow;
        }

        request.CompletedAt = status == ReturnWarrantyRequestStatuses.Completed ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync();

        if (oldStatus != status)
        {
            await _notificationService.CreateAsync(
                request.UserId,
                $"Yêu cầu {request.Type.ToLowerInvariant()} đã cập nhật",
                $"Yêu cầu #{request.Id} cho đơn #DH{request.OrderId:D4} hiện ở trạng thái: {request.Status}.",
                NotificationTypes.Support,
                $"/ReturnWarranty/Details/{request.Id}");
        }

        _logger.LogInformation("Admin updated return/warranty request {RequestId} to {Status}", id, status);
        return true;
    }

    private IQueryable<Order> LoadUserDeliveredOrder(int orderId, string userId)
    {
        return _db.Orders
            .IgnoreQueryFilters()
            .Include(order => order.User)
            .Include(order => order.Items)
            .ThenInclude(item => item.Product)
            .ThenInclude(product => product!.Images)
            .Where(order => order.Id == orderId && order.UserId == userId && order.Status == OrderStatuses.Delivered);
    }

    private IQueryable<ReturnWarrantyRequest> RequestQuery()
    {
        return _db.ReturnWarrantyRequests
            .AsSplitQuery()
            .Include(request => request.User)
            .Include(request => request.Order)
            .Include(request => request.Items)
            .ThenInclude(item => item.OrderItem)
            .ThenInclude(item => item!.Product)
            .ThenInclude(product => product!.Images)
            .Include(request => request.Images);
    }

    private static string? ValidateImages(IReadOnlyList<IFormFile> images)
    {
        if (images.Count > MaxEvidenceImages)
        {
            return $"Bạn chỉ có thể tải tối đa {MaxEvidenceImages} ảnh minh chứng.";
        }

        foreach (var image in images)
        {
            var extension = Path.GetExtension(image.FileName);
            if (!AllowedImageExtensions.Contains(extension) || !image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return "Ảnh minh chứng chỉ hỗ trợ JPG, PNG hoặc WEBP.";
            }

            if (image.Length > MaxEvidenceImageBytes)
            {
                return "Mỗi ảnh minh chứng cần nhỏ hơn 5MB.";
            }
        }

        return null;
    }

    private async Task<List<string>> SaveImagesAsync(IEnumerable<IFormFile> images)
    {
        var urls = new List<string>();
        var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadRoot = Path.Combine(webRoot, "uploads", "return-warranty");
        Directory.CreateDirectory(uploadRoot);

        foreach (var image in images)
        {
            var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var path = Path.Combine(uploadRoot, fileName);

            await using var stream = File.Create(path);
            await image.CopyToAsync(stream);
            urls.Add($"/uploads/return-warranty/{fileName}");
        }

        return urls;
    }
}
