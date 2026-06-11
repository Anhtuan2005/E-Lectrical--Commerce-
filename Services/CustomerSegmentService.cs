using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Services;

public class CustomerSegmentService : ICustomerSegmentService
{
    private const int VipOrderThreshold = 3;
    private const decimal VipSpentThreshold = 20_000_000m;
    private const int InactiveDays = 60;

    private readonly AppDbContext _db;

    public CustomerSegmentService(AppDbContext db)
    {
        _db = db;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSegmentsAsync(cancellationToken);

        var userIds = await GetCustomerUserIdsAsync(cancellationToken);
        var staleMembers = await _db.CustomerSegmentMembers
            .Where(member => !userIds.Contains(member.UserId))
            .ToListAsync(cancellationToken);
        _db.CustomerSegmentMembers.RemoveRange(staleMembers);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var userId in userIds)
        {
            await RefreshUserAsync(userId, cancellationToken);
        }
    }

    public async Task RefreshUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        await EnsureSegmentsAsync(cancellationToken);
        if (!await IsCustomerUserAsync(userId, cancellationToken))
        {
            var existingMemberships = await _db.CustomerSegmentMembers
                .Where(member => member.UserId == userId)
                .ToListAsync(cancellationToken);
            _db.CustomerSegmentMembers.RemoveRange(existingMemberships);
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        var stats = await GetCustomerStatsAsync(userId, cancellationToken);
        var targetCodes = ResolveSegmentCodes(stats).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var segments = await _db.CustomerSegments.ToListAsync(cancellationToken);
        var memberships = await _db.CustomerSegmentMembers
            .Where(member => member.UserId == userId)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;

        foreach (var segment in segments)
        {
            var membership = memberships.FirstOrDefault(member => member.CustomerSegmentId == segment.Id);
            if (targetCodes.Contains(segment.Code))
            {
                if (membership is null)
                {
                    _db.CustomerSegmentMembers.Add(new CustomerSegmentMember
                    {
                        CustomerSegmentId = segment.Id,
                        UserId = userId,
                        TotalOrders = stats.TotalOrders,
                        TotalSpent = stats.TotalSpent,
                        LastOrderAt = stats.LastOrderAt,
                        UpdatedAt = now
                    });
                }
                else
                {
                    membership.TotalOrders = stats.TotalOrders;
                    membership.TotalSpent = stats.TotalSpent;
                    membership.LastOrderAt = stats.LastOrderAt;
                    membership.UpdatedAt = now;
                }
            }
            else if (membership is not null)
            {
                _db.CustomerSegmentMembers.Remove(membership);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> UserBelongsToSegmentAsync(string userId, int segmentId, CancellationToken cancellationToken = default)
    {
        await RefreshUserAsync(userId, cancellationToken);

        return await _db.CustomerSegmentMembers
            .AnyAsync(member => member.UserId == userId && member.CustomerSegmentId == segmentId, cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerSegmentSummaryViewModel>> GetSummariesAsync(CancellationToken cancellationToken = default)
    {
        return await _db.CustomerSegments
            .AsNoTracking()
            .OrderBy(segment => segment.Id)
            .Select(segment => new CustomerSegmentSummaryViewModel
            {
                Id = segment.Id,
                Code = segment.Code,
                Name = segment.Name,
                Description = segment.Description,
                RuleDescription = segment.RuleDescription,
                RecommendedAction = segment.RecommendedAction,
                MemberCount = segment.Members.Count,
                TotalSpent = segment.Members.Sum(member => (decimal?)member.TotalSpent) ?? 0,
                LastOrderAt = segment.Members.Max(member => member.LastOrderAt)
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomerSegmentDetailViewModel?> GetDetailAsync(int segmentId, CancellationToken cancellationToken = default)
    {
        var segment = await _db.CustomerSegments
            .AsNoTracking()
            .Where(row => row.Id == segmentId)
            .Select(row => new CustomerSegmentSummaryViewModel
            {
                Id = row.Id,
                Code = row.Code,
                Name = row.Name,
                Description = row.Description,
                RuleDescription = row.RuleDescription,
                RecommendedAction = row.RecommendedAction,
                MemberCount = row.Members.Count,
                TotalSpent = row.Members.Sum(member => (decimal?)member.TotalSpent) ?? 0,
                LastOrderAt = row.Members.Max(member => member.LastOrderAt)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (segment is null)
        {
            return null;
        }

        var members = await _db.CustomerSegmentMembers
            .AsNoTracking()
            .Include(member => member.User)
            .Where(member => member.CustomerSegmentId == segmentId)
            .OrderByDescending(member => member.TotalSpent)
            .ThenByDescending(member => member.LastOrderAt)
            .Select(member => new CustomerSegmentMemberViewModel
            {
                UserId = member.UserId,
                FullName = member.User != null ? member.User.FullName : string.Empty,
                Email = member.User != null ? member.User.Email ?? string.Empty : string.Empty,
                TotalOrders = member.TotalOrders,
                TotalSpent = member.TotalSpent,
                LastOrderAt = member.LastOrderAt,
                UpdatedAt = member.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return new CustomerSegmentDetailViewModel
        {
            Segment = segment,
            Members = members
        };
    }

    private async Task EnsureSegmentsAsync(CancellationToken cancellationToken)
    {
        foreach (var segment in DefaultSegments())
        {
            var existing = await _db.CustomerSegments.FirstOrDefaultAsync(row => row.Code == segment.Code, cancellationToken);
            if (existing is null)
            {
                _db.CustomerSegments.Add(segment);
                continue;
            }

            existing.Name = segment.Name;
            existing.Description = segment.Description;
            existing.RuleDescription = segment.RuleDescription;
            existing.RecommendedAction = segment.RecommendedAction;
            existing.IsSystem = true;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<CustomerStats> GetCustomerStatsAsync(string userId, CancellationToken cancellationToken)
    {
        var orders = _db.Orders
            .Where(order => order.UserId == userId && (order.IsPaid || order.Status == OrderStatuses.Delivered));

        var totalOrders = await orders.CountAsync(cancellationToken);
        var totalSpent = totalOrders == 0 ? 0 : await orders.SumAsync(order => order.TotalAmount, cancellationToken);
        var lastOrderAt = totalOrders == 0
            ? null
            : await orders.MaxAsync(order => (DateTime?)order.CreatedAt, cancellationToken);

        return new CustomerStats(totalOrders, totalSpent, lastOrderAt);
    }

    private async Task<List<string>> GetCustomerUserIdsAsync(CancellationToken cancellationToken)
    {
        var adminRoleIds = await _db.Roles
            .Where(role => role.Name == "Admin")
            .Select(role => role.Id)
            .ToListAsync(cancellationToken);

        var adminUserIds = adminRoleIds.Count == 0
            ? new List<string>()
            : await _db.UserRoles
                .Where(userRole => adminRoleIds.Contains(userRole.RoleId))
                .Select(userRole => userRole.UserId)
                .ToListAsync(cancellationToken);

        return await _db.Users
            .Where(user => !adminUserIds.Contains(user.Id))
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task<bool> IsCustomerUserAsync(string userId, CancellationToken cancellationToken)
    {
        return !await _db.UserRoles
            .Join(_db.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { userRole.UserId, role.Name })
            .AnyAsync(row => row.UserId == userId && row.Name == "Admin", cancellationToken);
    }

    private static IEnumerable<string> ResolveSegmentCodes(CustomerStats stats)
    {
        if (stats.TotalOrders == 0)
        {
            yield return CustomerSegmentCodes.NewCustomer;
        }

        if (stats.TotalOrders >= VipOrderThreshold || stats.TotalSpent >= VipSpentThreshold)
        {
            yield return CustomerSegmentCodes.Vip;
        }

        if (stats.TotalOrders > 0 && stats.LastOrderAt <= DateTime.UtcNow.AddDays(-InactiveDays))
        {
            yield return CustomerSegmentCodes.Inactive;
        }
    }

    private static IReadOnlyList<CustomerSegment> DefaultSegments() => new[]
    {
        new CustomerSegment
        {
            Code = CustomerSegmentCodes.NewCustomer,
            Name = "Khách mới",
            Description = "Tài khoản chưa phát sinh đơn mua thành công.",
            RuleDescription = "Tổng đơn mua = 0.",
            RecommendedAction = "Gửi ưu đãi lần đầu để kích hoạt đơn hàng đầu tiên."
        },
        new CustomerSegment
        {
            Code = CustomerSegmentCodes.Vip,
            Name = "Khách VIP",
            Description = "Khách đã mua nhiều lần hoặc có tổng chi tiêu cao.",
            RuleDescription = $"Từ {VipOrderThreshold} đơn mua hoặc tổng chi tiêu từ {VipSpentThreshold:N0} ₫.",
            RecommendedAction = "Chăm sóc riêng, tặng voucher giá trị cao hoặc ưu đãi độc quyền."
        },
        new CustomerSegment
        {
            Code = CustomerSegmentCodes.Inactive,
            Name = "Lâu không mua",
            Description = "Khách từng mua nhưng đã lâu chưa quay lại.",
            RuleDescription = $"Có đơn mua và lần mua gần nhất cách đây từ {InactiveDays} ngày.",
            RecommendedAction = "Gửi khuyến mãi kéo lại hoặc gợi ý sản phẩm liên quan đơn cũ."
        }
    };

    private sealed record CustomerStats(int TotalOrders, decimal TotalSpent, DateTime? LastOrderAt);
}
