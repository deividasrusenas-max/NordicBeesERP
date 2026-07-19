using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// FROZEN.md behavior tests for ProductService.DeleteProductAsync.
/// These are integration tests against the real nordic_bees_erp_test
/// database (global QueryTrackingBehavior.NoTracking, same as production).
/// They verify that the ExecuteSqlRawAsync DELETE path actually removes
/// rows — the exact bug class that was fixed in commit fef84aa
/// (FindAsync + Remove + SaveChangesAsync silently persisted 0 rows).
/// </summary>
public class ProductServiceTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ProductServiceTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DeleteProductAsync_RemovesRowFromRealDatabase()
    {
        // Arrange: insert a minimal valid product
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        var product = new Product
        {
            Code = $"TEST-{Guid.NewGuid():N}",
            Name = $"Test Product {Guid.NewGuid():N}",
            ProductType = ProductType.FinishedGood,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();
        var id = product.Id;

        var service = new ProductService(_fixture.Factory);

        // Act
        var deleted = await service.DeleteProductAsync(id);

        // Assert: service reports success
        Assert.True(deleted);

        // Assert: row is actually gone from the database (fresh context)
        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var reloaded = await verifyContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        Assert.Null(reloaded);

        // Defensive cleanup (no-op if delete succeeded)
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM products WHERE id = {0}", id);
    }

    [Fact]
    public async Task DeleteProductAsync_NonExistentId_ReturnsFalse()
    {
        var service = new ProductService(_fixture.Factory);

        var id = -999999 - new Random().Next(0, 100000);

        var deleted = await service.DeleteProductAsync(id);

        Assert.False(deleted);
    }
}
