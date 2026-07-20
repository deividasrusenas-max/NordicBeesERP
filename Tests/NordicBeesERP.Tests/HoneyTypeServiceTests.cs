using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// FROZEN.md behavior tests for HoneyTypeService. These are integration
/// tests against the real nordic_bees_erp_test database (global
/// QueryTrackingBehavior.NoTracking, same as production) — they exist to
/// catch the exact bug class this codebase has hit repeatedly: a write
/// method that appears to succeed (no exception) but silently persists
/// zero rows.
/// </summary>
public class HoneyTypeServiceTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public HoneyTypeServiceTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UpdateAsync_PersistsChangesToRealDatabase()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        var code = $"HT-{DateTime.UtcNow.Ticks % 10000000:D7}";
        var honeyType = new HoneyType
        {
            Code = code,
            Name = $"Test Honey Type {Guid.NewGuid():N}",
            IsActive = true,
            SortOrder = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.HoneyTypes.Add(honeyType);
        await context.SaveChangesAsync();
        var id = honeyType.Id;

        var service = new HoneyTypeService(_fixture.Factory);

        honeyType.Name = $"Updated Name {Guid.NewGuid():N}";
        await service.UpdateAsync(honeyType);

        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var reloaded = await verifyContext.HoneyTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == id);

        Assert.NotNull(reloaded);
        Assert.Equal(honeyType.Name, reloaded!.Name);

        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM honey_types WHERE id = {0}", id);
    }
}
