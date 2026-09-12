using EcommerceApp.Data;
using EcommerceApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Services;

public sealed record UserAccessResult(bool Succeeded, string Message);

public sealed class AdminUserAccessService(AppDbContext db, UserManager<ApplicationUser> users)
{
    public Task<UserAccessResult> SetLockAsync(string actorId, string userId, bool locked) =>
        ChangeAsync(actorId, userId, locked, null);

    public Task<UserAccessResult> SetAdminAsync(string actorId, string userId, bool admin) =>
        ChangeAsync(actorId, userId, null, admin);

    private async Task<UserAccessResult> ChangeAsync(string actorId, string userId, bool? locked, bool? admin)
    {
        try
        {
            return await DatabaseTransaction.ExecuteAsync(db, async () =>
            {
                // All privilege changes share this lock; two admins cannot remove each other concurrently.
                var role = await db.Roles.FromSqlRaw("SELECT * FROM AspNetRoles WITH (UPDLOCK, HOLDLOCK) WHERE NormalizedName = N'ADMIN'")
                    .SingleOrDefaultAsync();
                var now = DateTimeOffset.UtcNow;
                if (role is null || !await db.UserRoles.AnyAsync(r => r.RoleId == role.Id && r.UserId == actorId) ||
                    !await db.Users.AnyAsync(u => u.Id == actorId && (!u.LockoutEnabled || u.LockoutEnd == null || u.LockoutEnd <= now)))
                    return new UserAccessResult(false, "Bạn không còn quyền quản trị đang hoạt động. Vui lòng đăng nhập lại.");
                if (actorId == userId && (locked == true || admin == false))
                    return new UserAccessResult(false, "Không thể tự khoá tài khoản hoặc tự gỡ quyền Admin.");

                var user = await db.Users.FromSqlInterpolated($"SELECT * FROM AspNetUsers WITH (UPDLOCK, ROWLOCK) WHERE Id = {userId}")
                    .SingleOrDefaultAsync();
                if (user is null) return new UserAccessResult(false, "Không tìm thấy tài khoản.");
                var isAdmin = await users.IsInRoleAsync(user, "Admin");
                if (isAdmin && (locked == true || admin == false) &&
                    !await db.UserRoles.Where(r => r.RoleId == role.Id && r.UserId != userId)
                        .Join(db.Users, r => r.UserId, u => u.Id, (r, u) => u)
                        .AnyAsync(u => !u.LockoutEnabled || u.LockoutEnd == null || u.LockoutEnd <= now))
                    return new UserAccessResult(false, "Cần giữ ít nhất một Admin đang hoạt động.");

                if (locked.HasValue)
                {
                    EnsureSucceeded(await users.SetLockoutEnabledAsync(user, true));
                    EnsureSucceeded(await users.SetLockoutEndDateAsync(user, locked.Value ? now.AddYears(10) : null));
                    if (!locked.Value) EnsureSucceeded(await users.ResetAccessFailedCountAsync(user));
                }
                else if (admin.HasValue && admin.Value != isAdmin)
                {
                    EnsureSucceeded(admin.Value ? await users.AddToRoleAsync(user, "Admin") : await users.RemoveFromRoleAsync(user, "Admin"));
                }
                // Revoke all previously issued cookies, including those from before an unlock or promotion.
                EnsureSucceeded(await users.UpdateSecurityStampAsync(user));
                return new UserAccessResult(true, locked.HasValue
                    ? locked.Value ? "Đã khoá tài khoản và thu hồi phiên đăng nhập cũ." : "Đã mở khoá. Người dùng cần đăng nhập lại."
                    : admin == true ? "Đã cấp quyền Admin. Người dùng cần đăng nhập lại." : "Đã gỡ quyền Admin và thu hồi phiên đăng nhập cũ.");
            });
        }
        catch (IdentityAccessUpdateException)
        {
            return new UserAccessResult(false, "Không thể cập nhật tài khoản. Dữ liệu có thể vừa thay đổi; hãy tải lại trang và thử lại.");
        }
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded) throw new IdentityAccessUpdateException();
    }
    private sealed class IdentityAccessUpdateException : Exception;
}
