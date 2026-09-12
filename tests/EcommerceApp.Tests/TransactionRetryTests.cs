using EcommerceApp.Data;
using EcommerceApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EcommerceApp.Tests;

[Collection("SqlServer")]
public sealed class TransactionRetryTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Transient_failure_after_save_retries_with_fresh_entities_and_no_duplicate_rows()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var slug = Guid.NewGuid().ToString("N");
        var attempts = 0;
        var categoryId = await DatabaseTransaction.ExecuteAsync(db, async () =>
        {
            attempts++;
            var category = new Category { Name = "Retry fixture", Slug = slug };
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            if (attempts == 1) throw new TimeoutException("Simulated transient failure before commit.");
            return category.Id;
        });
        Assert.Equal(2, attempts);
        await using var verify = fixture.Services.CreateAsyncScope();
        var rows = await verify.ServiceProvider.GetRequiredService<AppDbContext>().Categories.Where(c => c.Slug == slug).ToListAsync();
        Assert.Equal(categoryId, Assert.Single(rows).Id);
    }
}
