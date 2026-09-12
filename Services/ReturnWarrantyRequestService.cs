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
    private readonly IImageStorageService _imageStorage;
    private readonly IUserNotificationService _notificationService;
    private readonly ILogger<ReturnWarrantyRequestService> _logger;

    public ReturnWarrantyRequestService(
        AppDbContext db,
        IImageStorageService imageStorage,
        IUserNotificationService notificationService,
        ILogger<ReturnWarrantyRequestService> logger)
    {
        _db = db;
        _imageStorage = imageStorage;
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
        var reserved = await ReservedQuantitiesAsync(order.Id);

        return new ReturnWarrantyRequestCreateViewModel
        {
            OrderId = order.Id,
            ContactName = order.RecipientName,
            ContactPhone = order.RecipientPhone,
            Order = order,
            Items = order.Items.Select(item => new ReturnWarrantyRequestItemInput
            {
                OrderItemId = item.Id,
                Selected = item.Quantity > reserved.GetValueOrDefault(item.Id),
                AvailableQuantity = (int)Math.Max(0, item.Quantity - reserved.GetValueOrDefault(item.Id)),
                Quantity = (int)Math.Max(1, item.Quantity - reserved.GetValueOrDefault(item.Id))
            }).ToList()
        };
    }

    public async Task<ReturnWarrantyRequest> CreateAsync(string userId, ReturnWarrantyRequestCreateViewModel model)
    {
        if (!ReturnWarrantyRequestTypes.All.Contains(model.Type))
            throw new InvalidOperationException("Loại yêu cầu không hợp lệ.");
        var selectedInputs = model.Items.Where(item => item.Selected).ToList();
        if (selectedInputs.Count == 0)
            throw new InvalidOperationException("Vui lòng chọn ít nhất một sản phẩm cần hỗ trợ.");
        if (selectedInputs.Any(item => item.Quantity <= 0 || item.OrderItemId <= 0) ||
            selectedInputs.Select(item => item.OrderItemId).Distinct().Count() != selectedInputs.Count)
            throw new InvalidOperationException("Mỗi sản phẩm chỉ được chọn một lần và số lượng phải lớn hơn 0.");

        var imageFiles = (model.Images ?? new List<IFormFile>()).Where(file => file.Length > 0).Take(MaxEvidenceImages + 1).ToList();
        var imageError = ValidateImages(imageFiles);
        if (!string.IsNullOrWhiteSpace(imageError)) throw new InvalidOperationException(imageError);
        // Validate ownership before writing files, then recheck quantities while holding the order lock.
        if (!await LoadUserDeliveredOrder(model.OrderId, userId).AnyAsync())
            throw new InvalidOperationException("Chỉ có thể gửi yêu cầu cho đơn hàng đã giao.");
        var savedImages = await SaveImagesAsync(imageFiles);
        var request = await DatabaseTransaction.ExecuteAsync(_db, async () =>
        {
            var order = await OrderLifecycle.LoadForUpdateAsync(_db, model.OrderId);
            if (order is null || order.UserId != userId || order.Status != OrderStatuses.Delivered)
                throw new InvalidOperationException("Chỉ có thể gửi yêu cầu cho đơn hàng đã giao.");
            var orderItems = await _db.OrderItems.IgnoreQueryFilters().Include(item => item.Product)
                .Where(item => item.OrderId == order.Id).ToDictionaryAsync(item => item.Id);
            var reserved = await ReservedQuantitiesAsync(order.Id);
            var current = new ReturnWarrantyRequest
            {
                UserId = userId, OrderId = order.Id, Type = model.Type,
                ContactName = model.ContactName.Trim(), ContactPhone = model.ContactPhone.Trim(),
                Reason = model.Reason.Trim(), Description = model.Description.Trim(),
                PreferredResolution = string.IsNullOrWhiteSpace(model.PreferredResolution)
                    ? "Liên hệ tư vấn phương án phù hợp" : model.PreferredResolution.Trim()
            };
            foreach (var input in selectedInputs)
            {
                if (!orderItems.TryGetValue(input.OrderItemId, out var item))
                    throw new InvalidOperationException("Sản phẩm được chọn không thuộc đơn hàng này.");
                var available = item.Quantity - reserved.GetValueOrDefault(item.Id);
                if (input.Quantity > available)
                    throw new InvalidOperationException($"Sản phẩm '{item.Product?.Name}' còn {Math.Max(0, available)} sản phẩm có thể gửi yêu cầu. Số lượng còn lại đã có yêu cầu đang xử lý hoặc đã đổi trả.");
                current.Items.Add(new ReturnWarrantyRequestItem
                {
                    OrderItemId = item.Id, ProductId = item.ProductId, Quantity = input.Quantity,
                    ProductNameSnapshot = item.Product?.Name ?? $"Sản phẩm #{item.ProductId}"
                });
            }
            foreach (var image in savedImages.Select((url, index) => new ReturnWarrantyRequestImage { ImageUrl = url, SortOrder = index }))
                current.Images.Add(image);
            _db.ReturnWarrantyRequests.Add(current);
            await _db.SaveChangesAsync();
            return current;
        });

        await _notificationService.CreateAsync(userId, $"Đã nhận yêu cầu {request.Type.ToLowerInvariant()}",
            $"Techvora đã nhận yêu cầu cho đơn #DH{request.OrderId:D4} và sẽ phản hồi sau khi kiểm tra.",
            NotificationTypes.Support, $"/ReturnWarranty/Details/{request.Id}");
        await _notificationService.CreateForAdminsAsync($"Yêu cầu {request.Type.ToLowerInvariant()} mới",
            $"Đơn #DH{request.OrderId:D4} vừa có yêu cầu {request.Type.ToLowerInvariant()} từ {request.ContactName}.",
            NotificationTypes.Support, $"/Admin/ReturnWarranty/{request.Id}");
        _logger.LogInformation("User {UserId} created return/warranty request {RequestId} for order {OrderId}", userId, request.Id, request.OrderId);
        return request;
    }

    private Task<Dictionary<int, long>> ReservedQuantitiesAsync(int orderId) => _db.ReturnWarrantyRequestItems
        .IgnoreQueryFilters()
        .Where(item => item.ReturnWarrantyRequest!.OrderId == orderId &&
            item.ReturnWarrantyRequest.Status != ReturnWarrantyRequestStatuses.Rejected &&
            (item.ReturnWarrantyRequest.Type == ReturnWarrantyRequestTypes.Return ||
             item.ReturnWarrantyRequest.Status != ReturnWarrantyRequestStatuses.Completed))
        .GroupBy(item => item.OrderItemId)
        .ToDictionaryAsync(group => group.Key, group => group.Sum(item => (long)item.Quantity));

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
        if (!ReturnWarrantyRequestStatuses.All.Contains(status) || adminNote?.Length > 1200) return false;
        var change = await DatabaseTransaction.ExecuteAsync<(ReturnWarrantyRequest Request, bool Changed)?>(_db, async () =>
        {
            var orderId = await _db.ReturnWarrantyRequests.Where(request => request.Id == id)
                .Select(request => (int?)request.OrderId).SingleOrDefaultAsync();
            if (orderId is null || await OrderLifecycle.LoadForUpdateAsync(_db, orderId.Value) is null) return null;
            var request = await _db.ReturnWarrantyRequests.SingleOrDefaultAsync(row => row.Id == id);
            if (request is null || !ReturnRequestLifecycle.CanTransition(request.Status, status)) return null;
            if (request.Status == status)
            {
                if (status is not (ReturnWarrantyRequestStatuses.Completed or ReturnWarrantyRequestStatuses.Rejected))
                {
                    request.AdminNote = string.IsNullOrWhiteSpace(adminNote) ? null : adminNote.Trim();
                    request.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                }
                return (request, false);
            }
            request.Status = status;
            request.AdminNote = string.IsNullOrWhiteSpace(adminNote) ? null : adminNote.Trim();
            request.UpdatedAt = DateTime.UtcNow;
            request.ReviewedAt ??= DateTime.UtcNow;
            if (status == ReturnWarrantyRequestStatuses.Completed) request.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return (request, true);
        });
        if (change is null) return false;
        var (request, changed) = change.Value;
        if (changed)
        {
            await _notificationService.CreateAsync(request.UserId, $"Yêu cầu {request.Type.ToLowerInvariant()} đã cập nhật",
                $"Yêu cầu #{request.Id} cho đơn #DH{request.OrderId:D4} hiện ở trạng thái: {request.Status}.",
                NotificationTypes.Support, $"/ReturnWarranty/Details/{request.Id}");
            _logger.LogInformation("Admin updated return/warranty request {RequestId} to {Status}", id, status);
        }
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
        foreach (var image in images)
        {
            var url = await _imageStorage.SaveAsWebpAsync(
                image,
                "return-warranty",
                1800,
                1800,
                80,
                MaxEvidenceImageBytes);
            if (url is not null)
            {
                urls.Add(url);
            }
        }

        return urls;
    }
}
