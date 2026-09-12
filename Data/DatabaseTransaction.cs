using System.Data;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Data;

/// <summary>
/// Runs a complete database unit through SQL Server's retry strategy. Callers must reload
/// their entities inside the delegate and send external notifications only after it returns.
/// </summary>
public static class DatabaseTransaction
{
    public static Task<T> ExecuteAsync<T>(AppDbContext db, Func<Task<T>> operation,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
    {
        return db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // A rolled-back attempt may have accepted tracked changes or generated identities.
            // Reload on every attempt, including any entities read by the calling controller.
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(isolationLevel);
            var result = await operation();
            await transaction.CommitAsync();
            return result;
        });
    }
}
