using EcommerceApp.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EcommerceApp.Tests;

// This fixture creates and deletes only its uniquely named local test database.
public sealed class PortSqlServerFixture : IAsyncLifetime
{
    private readonly string _databaseName = "TechvoraPortTests_" + Guid.NewGuid().ToString("N");
    public ServiceProvider Services { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var connection = new SqlConnectionStringBuilder
        {
            DataSource = ".", InitialCatalog = _databaseName, IntegratedSecurity = true,
            TrustServerCertificate = true, Encrypt = false
        }.ConnectionString;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connection,
            sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)));
        Services = services.BuildServiceProvider();
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (Services is null) return;
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Database.GetDbConnection().Database != _databaseName ||
            !_databaseName.StartsWith("TechvoraPortTests_", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to remove a database not created by this fixture.");
        await db.Database.EnsureDeletedAsync();
        await Services.DisposeAsync();
    }
}

[CollectionDefinition("PortSqlServer")]
public sealed class PortSqlServerCollection : ICollectionFixture<PortSqlServerFixture>;
