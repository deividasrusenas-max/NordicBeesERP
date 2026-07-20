using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// FROZEN.md behavior test for WarehouseService. Integration test
/// against the real nordic_bees_erp_test database (global
/// QueryTrackingBehavior.NoTracking, same as production) — verifies
/// that WarehouseService.UpdateAsync correctly persists changes via
/// ExecuteSqlRawAsync instead of silently persisting 0 rows through
/// a detached-entity SaveChangesAsync.
/// </summary>
public class WarehouseServiceTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public WarehouseServiceTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UpdateAsync_PersistsChangesToRealDatabase()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        var code = $"WH-{DateTime.UtcNow.Ticks % 10000000:D7}";
        var warehouse = new Warehouse
        {
            Code = code,
            Name = "Test Warehouse",
            WarehouseTypeId = null,
            Address = null,
            City = null,
            Country = "LT",
            Description = null,
            IsActive = true,
            Email = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Warehouses.Add(warehouse);
        await context.SaveChangesAsync();
        var id = warehouse.Id;

        var service = new WarehouseService(_fixture.Factory);

        warehouse.Name = "Updated Warehouse Name";
        await service.UpdateAsync(warehouse);

        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var reloaded = await verifyContext.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id);

        Assert.NotNull(reloaded);
        Assert.Equal("Updated Warehouse Name", reloaded!.Name);

        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM warehouses WHERE id = {0}", id);
    }
}
