using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;

namespace EcommerceApp.Tests;

[Collection("SqlServer")]
public sealed class UserAccessTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Lock_and_unlock_revoke_old_cookies_and_only_new_login_is_accepted()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var admin = await CreateAdminAsync(services);
        var user = await SqlServerFixture.CreateUserAsync(services);
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var factory = new MinimalUserClaimsPrincipalFactory(users, Options.Create(new IdentityOptions()));
        var cookie = await factory.CreateAsync(user);
        var access = Access(services);
        Assert.True((await access.SetLockAsync(admin.Id, user.Id, true)).Succeeded);
        Assert.False(await CookieAcceptedAsync(services, cookie));
        Assert.True((await access.SetLockAsync(admin.Id, user.Id, false)).Succeeded);
        Assert.False(await CookieAcceptedAsync(services, cookie));
        var currentUser = (await users.FindByIdAsync(user.Id))!;
        Assert.True(await CookieAcceptedAsync(services, await factory.CreateAsync(currentUser)));
    }

    [Fact]
    public async Task Lockout_invalidates_current_session_even_when_the_stamp_has_not_changed()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var user = await SqlServerFixture.CreateUserAsync(services);
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        await users.SetLockoutEnabledAsync(user, true);
        var cookie = await new MinimalUserClaimsPrincipalFactory(users, Options.Create(new IdentityOptions())).CreateAsync(user);
        await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(15));
        Assert.False(await CookieAcceptedAsync(services, cookie));
    }

    [Fact]
    public async Task Admin_cannot_lock_or_demote_self_and_cannot_act_after_losing_role()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var first = await CreateAdminAsync(services);
        var second = await CreateAdminAsync(services);
        var access = Access(services);
        Assert.False((await access.SetLockAsync(first.Id, first.Id, true)).Succeeded);
        Assert.False((await access.SetAdminAsync(first.Id, first.Id, false)).Succeeded);
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var oldCookie = await new MinimalUserClaimsPrincipalFactory(users, Options.Create(new IdentityOptions())).CreateAsync(first);
        Assert.True((await access.SetAdminAsync(second.Id, first.Id, false)).Succeeded);
        Assert.False(await CookieAcceptedAsync(services, oldCookie));
        Assert.False((await access.SetAdminAsync(first.Id, second.Id, false)).Succeeded);
        Assert.True(await users.IsInRoleAsync((await users.FindByIdAsync(second.Id))!, "Admin"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Two_admins_cannot_revoke_each_other_concurrently(bool lockAccounts)
    {
        string firstId, secondId;
        await using (var setup = fixture.Services.CreateAsyncScope())
        {
            firstId = (await CreateAdminAsync(setup.ServiceProvider)).Id;
            secondId = (await CreateAdminAsync(setup.ServiceProvider)).Id;
        }
        async Task<bool> RevokeAsync(string actor, string target)
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var access = Access(scope.ServiceProvider);
            return (lockAccounts ? await access.SetLockAsync(actor, target, true) : await access.SetAdminAsync(actor, target, false)).Succeeded;
        }
        Assert.Single(await Task.WhenAll(RevokeAsync(firstId, secondId), RevokeAsync(secondId, firstId)), succeeded => succeeded);
    }

    private static AdminUserAccessService Access(IServiceProvider services) => new(
        services.GetRequiredService<AppDbContext>(), services.GetRequiredService<UserManager<ApplicationUser>>());

    private static async Task<ApplicationUser> CreateAdminAsync(IServiceProvider services)
    {
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync("Admin")) Assert.True((await roles.CreateAsync(new IdentityRole("Admin"))).Succeeded);
        var user = await SqlServerFixture.CreateUserAsync(services);
        Assert.True((await services.GetRequiredService<UserManager<ApplicationUser>>().AddToRoleAsync(user, "Admin")).Succeeded);
        return user;
    }

    private static async Task<bool> CookieAcceptedAsync(IServiceProvider services, ClaimsPrincipal principal)
    {
        services.GetRequiredService<AppDbContext>().ChangeTracker.Clear();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var options = Options.Create(new IdentityOptions());
        var signIn = new SignInManager<ApplicationUser>(users, new HttpContextAccessor(), new MinimalUserClaimsPrincipalFactory(users, options),
            options, NullLogger<SignInManager<ApplicationUser>>.Instance,
            new AuthenticationSchemeProvider(Options.Create(new AuthenticationOptions())), new DefaultUserConfirmation<ApplicationUser>());
        var authentication = new Mock<IAuthenticationService>();
        var stampValidator = new Mock<ISecurityStampValidator>();
        using var requestServices = new ServiceCollection().AddSingleton(authentication.Object).AddSingleton(stampValidator.Object).BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = requestServices };
        var scheme = new AuthenticationScheme(IdentityConstants.ApplicationScheme, null, typeof(CookieAuthenticationHandler));
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties { IssuedUtc = DateTimeOffset.UtcNow }, scheme.Name);
        var context = new CookieValidatePrincipalContext(httpContext, scheme, new CookieAuthenticationOptions(), ticket);
        await new ApplicationCookieEvents(signIn, users).ValidatePrincipal(context);
        if (context.Principal is null)
            authentication.Verify(a => a.SignOutAsync(httpContext, IdentityConstants.ApplicationScheme, null), Times.Once());
        else
            stampValidator.Verify(v => v.ValidateAsync(context), Times.Once());
        return context.Principal is not null;
    }
}
