using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Integration tests for OrderService.SaveLineBatchesAsync against the real
/// nordic_bees_erp_test database (global QueryTrackingBehavior.NoTracking,
/// same as production). Proves the ExecuteSqlRawAsync write path actually
/// reaches the database — not just mutates in-memory state — and that a
/// second call REPLACES unshipped batches instead of appending to them.
/// </summary>
public class OrderServiceTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public OrderServiceTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    private static BusinessPartner NewTestCustomer(string name) => new()
    {
        PartnerType = PartnerType.Customer,
        Name = name,
        Country = "Lithuania",
        CountryCode = "LT",
        DefaultLanguage = "LT",
        PaymentTermDays = 14,
        DefaultVatRate = 21m,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Product NewTestProduct(string code) => new()
    {
        Code = code,
        Name = $"Test Product {code}",
        ProductType = ProductType.FinishedGood,
        Unit = "kg",
        CostPrice = 1m,
        SalePrice = 2m,
        PurchasePrice = 1m,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private sealed record TestOrderFixture(int CustomerId, int ProductId, int OrderId, int LineId);

    private Task<TestOrderFixture> CreateOrderFixtureAsync(string suffix) => CreateOrderFixtureRawAsync(suffix);

    private async Task CleanupOrderFixtureAsync(TestOrderFixture fixture)
    {
        // Child-first so FK constraints never block the deletes.
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM order_line_batches WHERE order_line_id = {0}", fixture.LineId);
        await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM order_lines WHERE id = {0}", fixture.LineId);
        await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM orders WHERE id = {0}", fixture.OrderId);
        await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM products WHERE id = {0}", fixture.ProductId);
        await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM business_partners WHERE id = {0}", fixture.CustomerId);
    }

    /// <summary>
    /// Inserts the FK chain for one confirmed order with one line, using raw SQL
    /// INSERTs (positional params) instead of EF DbSet tracking — NordicBeesERPContext
    /// has no Orders/OrderLines DbSets. Returns the auto-increment IDs.
    /// </summary>
    private async Task<TestOrderFixture> CreateOrderFixtureRawAsync(string suffix)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        // Customer (FK target for orders.customer_id) — genuine INSERT via Add() is fine.
        var customer = NewTestCustomer($"Test Customer {suffix}");
        context.BusinessPartners.Add(customer);
        await context.SaveChangesAsync();

        // Product (FK target for order_lines.product_id) — genuine INSERT via Add() is fine.
        var product = NewTestProduct($"UZSTEST{suffix}");
        context.Products.Add(product);
        await context.SaveChangesAsync();

        // Order + line via raw SQL (no DbSet available).
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO orders (order_number, order_date, customer_id, status, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, 'confirmed', NOW(), NOW())",
            $"UZSTEST{suffix}", DateTime.Today, customer.Id);

        var orderId = await context.Database.SqlQueryRaw<int>(
            "SELECT id AS Value FROM orders WHERE order_number = {0}", $"UZSTEST{suffix}").FirstAsync();

        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO order_lines (order_id, line_number, product_id, quantity) VALUES ({0}, 1, {1}, 4)",
            orderId, product.Id);

        var lineId = await context.Database.SqlQueryRaw<int>(
            "SELECT id AS Value FROM order_lines WHERE order_id = {0} AND line_number = 1", orderId).FirstAsync();

        return new TestOrderFixture(customer.Id, product.Id, orderId, lineId);
    }

    private async Task<List<(string LotNumber, decimal Quantity, bool PackedAtNotNull)>> GetBatchesForLineAsync(int lineId)
    {
        // Verify with a BRAND NEW DbContext and raw SQL (same pattern the service uses),
        // so this proves rows exist in the database, not just in memory.
        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();

        var conn = verifyContext.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT lot_number, quantity, packed_at FROM order_line_batches 
                            WHERE order_line_id = @lineId ORDER BY id";
        var p = cmd.CreateParameter();
        p.ParameterName = "@lineId";
        p.Value = lineId;
        cmd.Parameters.Add(p);

        var result = new List<(string, decimal, bool)>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add((
                reader.GetString(reader.GetOrdinal("lot_number")),
                reader.GetDecimal(reader.GetOrdinal("quantity")),
                !reader.IsDBNull(reader.GetOrdinal("packed_at"))));
        }

        return result;
    }

    [Fact]
    public async Task SaveLineBatchesAsync_InsertsAllBatchesWithPackedAt()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var fixture = await CreateOrderFixtureAsync(suffix);
        try
        {
            var service = new OrderService(_fixture.Factory);

            var expiry = DateTime.Today.AddDays(365);
            await service.SaveLineBatchesAsync(fixture.LineId, new List<OrderLineBatch>
            {
                new() { LotNumber = "LOT-A", ExpiryDate = expiry, Quantity = 2m },
                new() { LotNumber = "LOT-B", ExpiryDate = expiry, Quantity = 2m }
            }, userId: 1);

            var batches = await GetBatchesForLineAsync(fixture.LineId);

            Assert.Equal(2, batches.Count);
            Assert.All(batches, b => Assert.True(b.PackedAtNotNull));
            Assert.Contains(batches, b => b.LotNumber == "LOT-A" && b.Quantity == 2m);
            Assert.Contains(batches, b => b.LotNumber == "LOT-B" && b.Quantity == 2m);

            // Parent line must be stamped as packed (packed_at NOT NULL) — and the
            // order should have auto-transitioned to ready_for_pickup.
            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
            var linePackedAtNullCount = await verifyContext.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM order_lines WHERE id = {0} AND packed_at IS NULL", fixture.LineId)
                .FirstAsync();
            Assert.Equal(0, linePackedAtNullCount);

            var orderStatus = await verifyContext.Database
                .SqlQueryRaw<string>("SELECT status AS Value FROM orders WHERE id = {0}", fixture.OrderId)
                .FirstAsync();
            Assert.Equal("ready_for_pickup", orderStatus);
        }
        finally
        {
            await CleanupOrderFixtureAsync(fixture);
        }
    }

    [Fact]
    public async Task SaveLineBatchesAsync_SecondCall_ReplacesUnshippedBatches()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var fixture = await CreateOrderFixtureAsync(suffix);
        try
        {
            var service = new OrderService(_fixture.Factory);

            var expiry = DateTime.Today.AddDays(365);
            await service.SaveLineBatchesAsync(fixture.LineId, new List<OrderLineBatch>
            {
                new() { LotNumber = "LOT-A", ExpiryDate = expiry, Quantity = 2m },
                new() { LotNumber = "LOT-B", ExpiryDate = expiry, Quantity = 2m }
            }, userId: 1);

            // Second call with different lots — the old (unshipped) batches must be gone.
            await service.SaveLineBatchesAsync(fixture.LineId, new List<OrderLineBatch>
            {
                new() { LotNumber = "LOT-C", ExpiryDate = expiry, Quantity = 1m },
                new() { LotNumber = "LOT-D", ExpiryDate = expiry, Quantity = 3m }
            }, userId: 1);

            var batches = await GetBatchesForLineAsync(fixture.LineId);

            Assert.Equal(2, batches.Count);
            Assert.Contains(batches, b => b.LotNumber == "LOT-C" && b.Quantity == 1m);
            Assert.Contains(batches, b => b.LotNumber == "LOT-D" && b.Quantity == 3m);
            Assert.DoesNotContain(batches, b => b.LotNumber == "LOT-A");
            Assert.DoesNotContain(batches, b => b.LotNumber == "LOT-B");
        }
        finally
        {
            await CleanupOrderFixtureAsync(fixture);
        }
    }

    [Fact]
    public async Task SaveLineBatchesAsync_EmptyList_ThrowsArgumentException()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var fixture = await CreateOrderFixtureAsync(suffix);
        try
        {
            var service = new OrderService(_fixture.Factory);

            await Assert.ThrowsAsync<ArgumentException>(
                () => service.SaveLineBatchesAsync(fixture.LineId, new List<OrderLineBatch>(), userId: 1));
        }
        finally
        {
            await CleanupOrderFixtureAsync(fixture);
        }
    }
}
