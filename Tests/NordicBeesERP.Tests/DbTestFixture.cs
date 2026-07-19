using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NordicBeesERP.Data;

namespace NordicBeesERP.Tests;

/// <summary>
/// Provides a DbContextFactory pointed at the isolated nordic_bees_erp_test
/// database on the dev MariaDB/MySQL host. Mirrors Program.cs's production
/// configuration exactly (same provider, same version, same
/// QueryTrackingBehavior.NoTracking) so tests exercise the real detached-entity
/// behavior that FROZEN.md's ExecuteSqlRawAsync pattern exists to handle.
///
/// Connection string comes from the TEST_DB_CONNECTION environment variable,
/// falling back to the local dev test DB default. Never point this at
/// nordic_bees_erp (prod) or nordic_bees_erp_staging.
/// </summary>
public class DbTestFixture
{
    public IDbContextFactory<NordicBeesERPContext> Factory { get; }

    public DbTestFixture()
    {
        var connectionString = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
            ?? "Server=100.110.26.80;Port=3306;Database=nordic_bees_erp_test;Uid=erp_user;Pwd=NordicBees2024;SslMode=none;AllowPublicKeyRetrieval=True;";

        var services = new ServiceCollection();
        services.AddDbContextFactory<NordicBeesERPContext>(options =>
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)))
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        var provider = services.BuildServiceProvider();
        Factory = provider.GetRequiredService<IDbContextFactory<NordicBeesERPContext>>();
    }
}
