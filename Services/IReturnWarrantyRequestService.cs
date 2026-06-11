using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;

namespace EcommerceApp.Services;

public interface IReturnWarrantyRequestService
{
    Task<ReturnWarrantyRequestCreateViewModel?> BuildCreateModelAsync(int orderId, string userId);
    Task<ReturnWarrantyRequest> CreateAsync(string userId, ReturnWarrantyRequestCreateViewModel model);
    Task<IReadOnlyList<ReturnWarrantyRequest>> GetUserRequestsAsync(string userId);
    Task<ReturnWarrantyRequest?> GetUserRequestAsync(int id, string userId);
    Task<ReturnWarrantyRequest?> GetRequestAsync(int id);
    Task<AdminReturnWarrantyRequestsViewModel> GetAdminRequestsAsync(string? status, string? type, string? query);
    Task<bool> UpdateStatusAsync(int id, string status, string? adminNote);
}
