using EcommerceApp.Data;
using EcommerceApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace EcommerceApp.Tests;

[Collection("SqlServer")]
public sealed class BootstrapTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Production_startup_does_not_create_demo_admin_or_catalog()
    {
        await using var provider = Provider(demoEnabled: false);
        await using var before = provider.CreateAsyncScope();
        var count = await before.ServiceProvider.GetRequiredService<AppDbContext>().Products.CountAsync();
        await SeedData.InitializeAsync(provider);
        await using var scope = provider.CreateAsyncScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("admin@shop.vn"));
        Assert.Equal(count, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Products.CountAsync());
    }

    [Fact]
    public async Task Production_rejects_accidentally_enabled_demo_mode()
    {
        await using var provider = Provider(demoEnabled: true);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SeedData.InitializeAsync(provider));
        Assert.Contains("Production", error.Message);
    }

    private ServiceProvider Provider(bool demoEnabled)
    {
        using var scope = fixture.Services.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetConnectionString();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connection));
        services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Demo:Enabled"] = demoEnabled.ToString() }).Build());
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);
        services.AddSingleton(environment.Object);
        return services.BuildServiceProvider();
    }
}
