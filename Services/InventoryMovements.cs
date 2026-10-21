using EcommerceApp.Data;
using EcommerceApp.Models;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Services;

public static class InventoryMovements
{
    // The caller owns the database transaction and saves the log before committing.
    // UPDATE retains the row lock until commit, so the balances belong to this movement.
    public static async Task<bool> ChangeAsync(AppDbContext db, int productId, int change,
        string type, string reason, string? actorId = null, Order? order = null, bool requireActive = true)
    {
        var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE Products SET Stock = Stock + {change}
            WHERE Id = {productId} AND ({requireActive} = 0 OR IsDeleted = 0)
                AND CAST(Stock AS bigint) + {change} BETWEEN 0 AND 2147483647
            """);
        if (affected != 1) return false;
        var after = await db.Products.IgnoreQueryFilters().Where(product => product.Id == productId)
            .Select(product => product.Stock).SingleAsync();
        db.StockLogs.Add(new StockLog
        {
            ProductId = productId, ChangeAmount = change, QuantityBefore = after - change,
            QuantityAfter = after, MovementType = type, Reason = reason,
            ChangedByUserId = actorId, Order = order
        });
        return true;
    }
}
