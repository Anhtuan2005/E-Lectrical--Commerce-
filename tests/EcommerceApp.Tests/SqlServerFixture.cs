using EcommerceApp.Data;
using EcommerceApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EcommerceApp.Tests;

// Every run owns a new database. Never migrate, seed, or delete the database in the supplied connection string.
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly string _databaseName = "TechvoraTests_" + Guid.NewGuid().ToString("N");
    public ServiceProvider Services { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var connection = Environment.GetEnvironmentVariable("TECHVORA_TEST_SQLSERVER")
            ?? @"Server=localhost;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";
        var builder = new SqlConnectionStringBuilder(connection) { InitialCatalog = _databaseName };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(builder.ConnectionString,
            sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)));
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequiredLength = 6;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.User.RequireUniqueEmail = true;
        }).AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
        Services = services.BuildServiceProvider();
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (Services is null) return;
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Database.GetDbConnection().Database != _databaseName || !_databaseName.StartsWith("TechvoraTests_"))
            throw new InvalidOperationException("Refusing to delete a database not owned by this test run.");
        await db.Database.EnsureDeletedAsync();
        await Services.DisposeAsync();
    }

    public static async Task<ApplicationUser> CreateUserAsync(IServiceProvider services)
    {
        var id = Guid.NewGuid().ToString("N");
        var user = new ApplicationUser { UserName = $"{id}@example.test", Email = $"{id}@example.test", FullName = "Test customer" };
        var result = await services.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, "Original123");
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));
        return user;
    }
}

[CollectionDefinition("SqlServer")]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>;
