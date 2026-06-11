using EcommerceApp.Models.ViewModels;

namespace EcommerceApp.Services;

public interface ICustomerSegmentService
{
    Task RefreshAsync(CancellationToken cancellationToken = default);
    Task RefreshUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> UserBelongsToSegmentAsync(string userId, int segmentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerSegmentSummaryViewModel>> GetSummariesAsync(CancellationToken cancellationToken = default);
    Task<CustomerSegmentDetailViewModel?> GetDetailAsync(int segmentId, CancellationToken cancellationToken = default);
}
