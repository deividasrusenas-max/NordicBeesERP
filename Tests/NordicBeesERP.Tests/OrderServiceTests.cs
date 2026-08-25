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
            "DELETE FROM order_shipment_pallets WHERE shipment_id IN (SELECT id FROM order_shipments WHERE order_id = {0})",
            fixture.OrderId);
        await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM order_shipments WHERE order_id = {0}", fixture.OrderId);
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

    /// <summary>
    /// Returns the order_line_batches IDs for a line in insertion order, read via
    /// a brand-new DbContext so this proves the rows exist in the database.
    /// </summary>
    private async Task<List<int>> GetBatchIdsForLineAsync(int lineId)
    {
        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();

        var conn = verifyContext.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id FROM order_line_batches WHERE order_line_id = @lineId ORDER BY id";
        var p = cmd.CreateParameter();
        p.ParameterName = "@lineId";
        p.Value = lineId;
        cmd.Parameters.Add(p);

        var ids = new List<int>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            ids.Add(reader.GetInt32(0));

        return ids;
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

    [Fact]
    public async Task CreateShipmentAsync_PartialShipment_SetsPartiallyShippedStatus()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var fixture = await CreateOrderFixtureAsync(suffix);
        try
        {
            var service = new OrderService(_fixture.Factory);

            // Pack two batches on the single line (order auto-transitions to ready_for_pickup).
            var expiry = DateTime.Today.AddDays(365);
            await service.SaveLineBatchesAsync(fixture.LineId, new List<OrderLineBatch>
            {
                new() { LotNumber = $"LOT-PART-{suffix}", ExpiryDate = expiry, Quantity = 2m },
                new() { LotNumber = $"LOT-PART2-{suffix}", ExpiryDate = expiry, Quantity = 2m }
            }, userId: 1);

            var batchIds = await GetBatchIdsForLineAsync(fixture.LineId);
            Assert.Equal(2, batchIds.Count);

            // Ship only the FIRST batch (full quantity of 2), with notes left null (must persist as SQL NULL).
            await service.CreateShipmentAsync(fixture.OrderId, DateTime.Today, "TestCourier", null, userId: 1, new List<(int BatchId, decimal Quantity)> { (batchIds[0], 2m) });

            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();

            // Exactly one shipment row for this order, with courier persisted and notes as SQL NULL.
            var shipmentCount = await verifyContext.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM order_shipments WHERE order_id = {0}", fixture.OrderId)
                .FirstAsync();
            Assert.Equal(1, shipmentCount);

            var courierAndNotesNull = await verifyContext.Database
                .SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS Value FROM order_shipments WHERE order_id = {0} AND courier_name = {1} AND notes IS NULL",
                    fixture.OrderId, "TestCourier")
                .FirstAsync();
            Assert.Equal(1, courierAndNotesNull);

            // Exactly one pallet link, pointing at the first batch, with shipped_at NOT NULL and quantity_shipped persisted.
            var palletCount = await verifyContext.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM order_shipment_pallets WHERE order_line_batch_id = {0}", batchIds[0])
                .FirstAsync();
            Assert.Equal(1, palletCount);

            var palletQuantity = await verifyContext.Database
                .SqlQueryRaw<decimal>("SELECT quantity_shipped AS Value FROM order_shipment_pallets WHERE order_line_batch_id = {0}", batchIds[0])
                .FirstAsync();
            Assert.Equal(2m, palletQuantity);

            var secondBatchPalletCount = await verifyContext.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM order_shipment_pallets WHERE order_line_batch_id = {0}", batchIds[1])
                .FirstAsync();
            Assert.Equal(0, secondBatchPalletCount);

            var shippedAtNullCount = await verifyContext.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM order_shipment_pallets WHERE order_line_batch_id = {0} AND shipped_at IS NULL", batchIds[0])
                .FirstAsync();
            Assert.Equal(0, shippedAtNullCount);

            // Order is partially shipped — but NOT fully shipped yet.
            var orderStatus = await verifyContext.Database
                .SqlQueryRaw<string>("SELECT status AS Value FROM orders WHERE id = {0}", fixture.OrderId)
                .FirstAsync();
            Assert.Equal("partially_shipped", orderStatus);

            var orderShippedAtNullCount = await verifyContext.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM orders WHERE id = {0} AND shipped_at IS NULL", fixture.OrderId)
                .FirstAsync();
            Assert.Equal(1, orderShippedAtNullCount);
        }
        finally
        {
            await CleanupOrderFixtureAsync(fixture);
        }
    }

    [Fact]
    public async Task CreateShipmentAsync_FullShipment_SetsShippedStatusAndShippedAt()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var fixture = await CreateOrderFixtureAsync(suffix);
        try
        {
            var service = new OrderService(_fixture.Factory);

            var expiry = DateTime.Today.AddDays(365);
            await service.SaveLineBatchesAsync(fixture.LineId, new List<OrderLineBatch>
            {
                new() { LotNumber = $"LOT-FULL-{suffix}", ExpiryDate = expiry, Quantity = 2m },
                new() { LotNumber = $"LOT-FULL2-{suffix}", ExpiryDate = expiry, Quantity = 2m }
            }, userId: 1);

            var batchIds = await GetBatchIdsForLineAsync(fixture.LineId);
            Assert.Equal(2, batchIds.Count);

            // Ship the first batch (full quantity) → partially_shipped (same state as the partial test).
            await service.CreateShipmentAsync(fixture.OrderId, DateTime.Today, "TestCourier", null, userId: 1, new List<(int BatchId, decimal Quantity)> { (batchIds[0], 2m) });

            // Ship the remaining batch → all batches shipped → fully shipped.
            await service.CreateShipmentAsync(fixture.OrderId, DateTime.Today, "TestCourier", null, userId: 1, new List<(int BatchId, decimal Quantity)> { (batchIds[1], 2m) });

            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();

            var orderStatus = await verifyContext.Database
                .SqlQueryRaw<string>("SELECT status AS Value FROM orders WHERE id = {0}", fixture.OrderId)
                .FirstAsync();
            Assert.Equal("shipped", orderStatus);

            var shippedAtNullCount = await verifyContext.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM orders WHERE id = {0} AND shipped_at IS NULL", fixture.OrderId)
                .FirstAsync();
            Assert.Equal(0, shippedAtNullCount);
        }
        finally
        {
            await CleanupOrderFixtureAsync(fixture);
        }
    }

    [Fact]
    public async Task CreateShipmentAsync_PartialQuantity_SameBatchAccumulatesAcrossShipments()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var fixture = await CreateOrderFixtureAsync(suffix);
        try
        {
            var service = new OrderService(_fixture.Factory);

            // One batch of quantity 4 on the single line.
            var expiry = DateTime.Today.AddDays(365);
            await service.SaveLineBatchesAsync(fixture.LineId, new List<OrderLineBatch>
            {
                new() { LotNumber = $"LOT-ACC-{suffix}", ExpiryDate = expiry, Quantity = 4m }
            }, userId: 1);

            var batchIds = await GetBatchIdsForLineAsync(fixture.LineId);
            Assert.Single(batchIds);
            var batchId = batchIds[0];

            // Ship 2 of the 4 → partially shipped.
            await service.CreateShipmentAsync(fixture.OrderId, DateTime.Today, "TestCourier", null, userId: 1, new List<(int BatchId, decimal Quantity)> { (batchId, 2m) });

            // Ship the remaining 2 on a SECOND shipment day → same batch gets a second pallet row.
            await service.CreateShipmentAsync(fixture.OrderId, DateTime.Today.AddDays(1), "TestCourier", null, userId: 1, new List<(int BatchId, decimal Quantity)> { (batchId, 2m) });

            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();

            // Two shipments, two pallet rows for the SAME batch — accumulation across days must be allowed.
            var shipmentCount = await verifyContext.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM order_shipments WHERE order_id = {0}", fixture.OrderId)
                .FirstAsync();
            Assert.Equal(2, shipmentCount);

            var palletRowsForBatch = await verifyContext.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM order_shipment_pallets WHERE order_line_batch_id = {0}", batchId)
                .FirstAsync();
            Assert.Equal(2, palletRowsForBatch);

            var totalShippedQuantity = await verifyContext.Database
                .SqlQueryRaw<decimal>(
                    "SELECT COALESCE(SUM(quantity_shipped), 0) AS Value FROM order_shipment_pallets WHERE order_line_batch_id = {0}", batchId)
                .FirstAsync();
            Assert.Equal(4m, totalShippedQuantity);

            // Batch is now fully shipped → order must be 'shipped' with shipped_at set.
            var orderStatus = await verifyContext.Database
                .SqlQueryRaw<string>("SELECT status AS Value FROM orders WHERE id = {0}", fixture.OrderId)
                .FirstAsync();
            Assert.Equal("shipped", orderStatus);

            var shippedAtNullCount = await verifyContext.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM orders WHERE id = {0} AND shipped_at IS NULL", fixture.OrderId)
                .FirstAsync();
            Assert.Equal(0, shippedAtNullCount);
        }
        finally
        {
            await CleanupOrderFixtureAsync(fixture);
        }
    }

    [Fact]
    public async Task CreateShipmentAsync_QuantityExceedsRemaining_ThrowsAndRollsBack()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var fixture = await CreateOrderFixtureAsync(suffix);
        try
        {
            var service = new OrderService(_fixture.Factory);

            var expiry = DateTime.Today.AddDays(365);
            await service.SaveLineBatchesAsync(fixture.LineId, new List<OrderLineBatch>
            {
                new() { LotNumber = $"LOT-OVER-{suffix}", ExpiryDate = expiry, Quantity = 2m }
            }, userId: 1);

            var batchIds = await GetBatchIdsForLineAsync(fixture.LineId);
            Assert.Single(batchIds);
            var batchId = batchIds[0];

            // Requesting 5 of a remaining 2 must be refused server-side.
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateShipmentAsync(fixture.OrderId, DateTime.Today, "TestCourier", null, userId: 1, new List<(int BatchId, decimal Quantity)> { (batchId, 5m) }));

            // The failed call must have rolled back completely: no shipment row, no pallet row.
            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();

            var shipmentCount = await verifyContext.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM order_shipments WHERE order_id = {0}", fixture.OrderId)
                .FirstAsync();
            Assert.Equal(0, shipmentCount);

            var palletCount = await verifyContext.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM order_shipment_pallets WHERE order_line_batch_id = {0}", batchId)
                .FirstAsync();
            Assert.Equal(0, palletCount);
        }
        finally
        {
            await CleanupOrderFixtureAsync(fixture);
        }
    }

    [Fact]
    public async Task CreateShipmentAsync_FullyShippedBatch_Throws()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var fixture = await CreateOrderFixtureAsync(suffix);
        try
        {
            var service = new OrderService(_fixture.Factory);

            var expiry = DateTime.Today.AddDays(365);
            await service.SaveLineBatchesAsync(fixture.LineId, new List<OrderLineBatch>
            {
                new() { LotNumber = $"LOT-EXH-{suffix}", ExpiryDate = expiry, Quantity = 2m }
            }, userId: 1);

            var batchIds = await GetBatchIdsForLineAsync(fixture.LineId);
            Assert.Single(batchIds);
            var batchId = batchIds[0];

            // Ship everything first.
            await service.CreateShipmentAsync(fixture.OrderId, DateTime.Today, "TestCourier", null, userId: 1, new List<(int BatchId, decimal Quantity)> { (batchId, 2m) });

            // Shipping the same batch again (remaining = 0) must be refused with a clear message.
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateShipmentAsync(fixture.OrderId, DateTime.Today, "TestCourier", null, userId: 1, new List<(int BatchId, decimal Quantity)> { (batchId, 1m) }));
            Assert.Contains("išsiųstas", ex.Message);

            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();

            // Only the first shipment survived; the second attempt rolled back.
            var shipmentCount = await verifyContext.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM order_shipments WHERE order_id = {0}", fixture.OrderId)
                .FirstAsync();
            Assert.Equal(1, shipmentCount);

            var palletCount = await verifyContext.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM order_shipment_pallets WHERE order_line_batch_id = {0}", batchId)
                .FirstAsync();
            Assert.Equal(1, palletCount);
        }
        finally
        {
            await CleanupOrderFixtureAsync(fixture);
        }
    }

    [Fact]
    public async Task CreateShipmentAsync_UnknownBatchForOrder_Throws()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var fixture = await CreateOrderFixtureAsync(suffix);
        try
        {
            var service = new OrderService(_fixture.Factory);

            // A batch id that does not exist at all — the server-side check must refuse it.
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateShipmentAsync(fixture.OrderId, DateTime.Today, "TestCourier", null, userId: 1, new List<(int BatchId, decimal Quantity)> { (999999, 1m) }));

            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
            var shipmentCount = await verifyContext.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM order_shipments WHERE order_id = {0}", fixture.OrderId)
                .FirstAsync();
            Assert.Equal(0, shipmentCount);
        }
        finally
        {
            await CleanupOrderFixtureAsync(fixture);
        }
    }

    [Fact]
    public async Task CreateShipmentAsync_EmptyListOrNonPositiveQuantity_ThrowsArgumentException()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var fixture = await CreateOrderFixtureAsync(suffix);
        try
        {
            var service = new OrderService(_fixture.Factory);

            await Assert.ThrowsAsync<ArgumentException>(
                () => service.CreateShipmentAsync(fixture.OrderId, DateTime.Today, "TestCourier", null, userId: 1, new List<(int BatchId, decimal Quantity)>()));

            var batchIds = await GetBatchIdsForLineAsync(fixture.LineId); // none yet — but validation happens before any DB read
            Assert.Empty(batchIds);

            await Assert.ThrowsAsync<ArgumentException>(
                () => service.CreateShipmentAsync(fixture.OrderId, DateTime.Today, "TestCourier", null, userId: 1, new List<(int BatchId, decimal Quantity)> { (1, 0m) }));

            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
            var shipmentCount = await verifyContext.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM order_shipments WHERE order_id = {0}", fixture.OrderId)
                .FirstAsync();
            Assert.Equal(0, shipmentCount);
        }
        finally
        {
            await CleanupOrderFixtureAsync(fixture);
        }
    }

    [Fact]
    public async Task LinkInvoiceAsync_OnlyAllowedWhenOrderIsShipped()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var fixture = await CreateOrderFixtureAsync(suffix);
        try
        {
            var service = new OrderService(_fixture.Factory);

            // Pack the line so the order transitions to ready_for_pickup (NOT shipped).
            var expiry = DateTime.Today.AddDays(365);
            await service.SaveLineBatchesAsync(fixture.LineId, new List<OrderLineBatch>
            {
                new() { LotNumber = $"LOT-INV-{suffix}", ExpiryDate = expiry, Quantity = 4m }
            }, userId: 1);

            // While ready_for_pickup the invoice link must be refused (invoice_id stays NULL).
            await service.LinkInvoiceAsync(fixture.OrderId, invoiceId: 999, userId: 1);

            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
            var invoiceIdBefore = await verifyContext.Database
                .SqlQueryRaw<int?>("SELECT invoice_id AS Value FROM orders WHERE id = {0}", fixture.OrderId)
                .FirstAsync();
            Assert.Null(invoiceIdBefore);

            // Force the order to shipped, then linking must succeed.
            await verifyContext.Database.ExecuteSqlRawAsync(
                "UPDATE orders SET status = 'shipped' WHERE id = {0}", fixture.OrderId);

            await service.LinkInvoiceAsync(fixture.OrderId, invoiceId: 999, userId: 1);

            await using var verifyContext2 = await _fixture.Factory.CreateDbContextAsync();
            var invoiceIdAfter = await verifyContext2.Database
                .SqlQueryRaw<int?>("SELECT invoice_id AS Value FROM orders WHERE id = {0}", fixture.OrderId)
                .FirstAsync();
            Assert.Equal(999, invoiceIdAfter);
        }
        finally
        {
            await CleanupOrderFixtureAsync(fixture);
        }
    }
}
