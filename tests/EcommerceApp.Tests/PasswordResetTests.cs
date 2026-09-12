using System.Security.Cryptography;
using System.Text;
using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace EcommerceApp.Tests;

[Collection("SqlServer")]
public sealed class PasswordResetTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Rejected_password_preserves_existing_password_and_unused_token()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var user = await SqlServerFixture.CreateUserAsync(scope.ServiceProvider);
        var rawToken = await AddTokenAsync(scope.ServiceProvider, user.Id);
        var result = await CreateService(scope.ServiceProvider).ResetPasswordAsync(rawToken, "abcdef");
        Assert.False(result.Success);

        await using var verification = fixture.Services.CreateAsyncScope();
        var manager = verification.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True(await manager.CheckPasswordAsync((await manager.FindByIdAsync(user.Id))!, "Original123"));
        var db = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Null((await db.PasswordResetTokens.SingleAsync(t => t.UserId == user.Id)).UsedAt);
    }

    [Fact]
    public async Task Successful_reset_invalidates_token_and_other_outstanding_links()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var user = await SqlServerFixture.CreateUserAsync(scope.ServiceProvider);
        var rawToken = await AddTokenAsync(scope.ServiceProvider, user.Id);
        var otherToken = await AddTokenAsync(scope.ServiceProvider, user.Id);
        var service = CreateService(scope.ServiceProvider);
        Assert.True((await service.ResetPasswordAsync(rawToken, "Replacement123")).Success);
        Assert.False((await service.ResetPasswordAsync(rawToken, "Replay123")).Success);
        Assert.False((await service.ResetPasswordAsync(otherToken, "Sibling123")).Success);
        await using var verification = fixture.Services.CreateAsyncScope();
        var manager = verification.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True(await manager.CheckPasswordAsync((await manager.FindByIdAsync(user.Id))!, "Replacement123"));
    }

    [Fact]
    public async Task Concurrent_use_of_one_token_has_exactly_one_winner()
    {
        string rawToken;
        await using (var setup = fixture.Services.CreateAsyncScope())
        {
            var user = await SqlServerFixture.CreateUserAsync(setup.ServiceProvider);
            rawToken = await AddTokenAsync(setup.ServiceProvider, user.Id);
        }
        async Task<bool> ResetAsync(string password)
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            return (await CreateService(scope.ServiceProvider).ResetPasswordAsync(rawToken, password)).Success;
        }
        var results = await Task.WhenAll(ResetAsync("First123"), ResetAsync("Second123"));
        Assert.Single(results, success => success);
    }

    [Fact]
    public async Task Expired_link_cannot_change_password()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var user = await SqlServerFixture.CreateUserAsync(scope.ServiceProvider);
        var rawToken = await AddTokenAsync(scope.ServiceProvider, user.Id, expired: true);
        Assert.False((await CreateService(scope.ServiceProvider).ResetPasswordAsync(rawToken, "Replacement123")).Success);
        Assert.True(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CheckPasswordAsync(user, "Original123"));
    }

    private static PasswordResetService CreateService(IServiceProvider services) => new(
        services.GetRequiredService<AppDbContext>(), services.GetRequiredService<UserManager<ApplicationUser>>(),
        Mock.Of<IEmailSender>(), Options.Create(new EmailOptions()), NullLogger<PasswordResetService>.Instance);

    private static async Task<string> AddTokenAsync(IServiceProvider services, string userId, bool expired = false)
    {
        var raw = Guid.NewGuid().ToString("N");
        var db = services.GetRequiredService<AppDbContext>();
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = userId, TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant(),
            CreatedAt = DateTime.UtcNow, ExpiredAt = DateTime.UtcNow.AddMinutes(expired ? -1 : 30)
        });
        await db.SaveChangesAsync();
        return raw;
    }
}
