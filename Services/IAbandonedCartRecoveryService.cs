namespace EcommerceApp.Services;

public interface IAbandonedCartRecoveryService
{
    Task ProcessDueCartsAsync(CancellationToken cancellationToken = default);
    Task<AbandonedCartReminderMessage?> GetLoginReminderAsync(string userId, CancellationToken cancellationToken = default);
}

public sealed record AbandonedCartReminderMessage(string RecoveryCode, DateTime ExpiresAt, int ItemCount);
