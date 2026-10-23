using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Models.WarehouseModule;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Integration tests for ContainerService.UpdateHoneyTypeAsync against the real
/// nordic_bees_erp_test database. Verifies that the ExecuteSqlRawAsync write
/// path actually persists changes (no silent NoTracking drop).
/// </summary>
[Collection("RealDatabase")]
public class ContainerServiceTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ContainerServiceTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UpdateHoneyTypeAsync_PersistsChangeToRealDatabase()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        // 1. Create first HoneyType (original)
        var firstHoneyType = new HoneyType
        {
            Code = $"HT-{DateTime.UtcNow.Ticks % 10000000:D7}",
            Name = $"Test Honey Type 1 {DateTime.UtcNow.Ticks}",
            IsActive = true,
        };
        context.HoneyTypes.Add(firstHoneyType);
        await context.SaveChangesAsync();
        var firstHoneyTypeId = firstHoneyType.Id;

        // 2. Create second HoneyType (the "new" one we'll assign)
        var secondHoneyType = new HoneyType
        {
            Code = $"HT-{DateTime.UtcNow.Ticks % 10000000:D7}",
            Name = $"Test Honey Type 2 {DateTime.UtcNow.Ticks}",
            IsActive = true,
        };
        context.HoneyTypes.Add(secondHoneyType);
        await context.SaveChangesAsync();
        var secondHoneyTypeId = secondHoneyType.Id;

        // 3. Create Warehouse
        var warehouse = new Warehouse
        {
            // unique per creation (Guid) — avoids same-second fixture-code collisions
            Code = $"WH-{Guid.NewGuid():N}"[..17],
            Name = $"Test Warehouse {DateTime.UtcNow.Ticks}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Warehouses.Add(warehouse);
        await context.SaveChangesAsync();
        var warehouseId = warehouse.Id;

        // 4. Create BusinessPartner (supplier)
        var supplier = new BusinessPartner
        {
            PartnerType = PartnerType.Supplier,
            Name = $"Test Supplier {DateTime.UtcNow.Ticks}",
            Country = "Lithuania",
            CountryCode = "LT",
            DefaultLanguage = "LT",
            PaymentTermDays = 14,
            DefaultVatRate = 0m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.BusinessPartners.Add(supplier);
        await context.SaveChangesAsync();
        var supplierId = supplier.Id;

        // 5. Create Delivery
        var delivery = new Delivery
        {
            DeliveryDate = DateTime.UtcNow.Date,
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            Status = "RECEIVED",
            TotalAmount = 0m,
            BarrelsOwed = 0,
            NeedReturnBarrels = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Deliveries.Add(delivery);
        await context.SaveChangesAsync();
        var deliveryId = delivery.Id;

        // 6. Create DeliveryLine
        var line = new DeliveryLine
        {
            DeliveryId = deliveryId,
            ContainerType = "BARREL",
            ContainerCount = 2,
            TotalNetWeight = 200m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.DeliveryLines.Add(line);
        await context.SaveChangesAsync();
        var lineId = line.Id;

        // 7. Create 2 Container records, both with the first honey type
        var containerCode1 = $"CC-{DateTime.UtcNow.Ticks % 10000000:D7}";
        var containerCode2 = $"CC-{DateTime.UtcNow.Ticks % 10000000:D7}";

        var container1 = new Container
        {
            ContainerCode = containerCode1,
            ContainerType = "BARREL",
            DeliveryLineId = lineId,
            HoneyTypeId = firstHoneyTypeId,
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            GrossWeight = 110m,
            TareWeight = 10m,
            NetWeight = 100m,
            Quantity = 1,
            RemainingQuantity = 1,
            Status = "IN_STOCK",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Containers.Add(container1);
        await context.SaveChangesAsync();
        var containerId1 = container1.Id;

        var container2 = new Container
        {
            ContainerCode = containerCode2,
            ContainerType = "BARREL",
            DeliveryLineId = lineId,
            HoneyTypeId = firstHoneyTypeId,
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            GrossWeight = 110m,
            TareWeight = 10m,
            NetWeight = 100m,
            Quantity = 1,
            RemainingQuantity = 1,
            Status = "IN_STOCK",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Containers.Add(container2);
        await context.SaveChangesAsync();
        var containerId2 = container2.Id;

        // Act
        var service = new ContainerService(_fixture.Factory);
        var containerIds = new List<int> { containerId1, containerId2 };
        await service.UpdateHoneyTypeAsync(containerIds, secondHoneyTypeId);

        // Assert with fresh context (cleanup in finally ensures rows are removed even if assertions fail)
        try
        {
            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();

            var reloadedContainer1 = await verifyContext.Containers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == containerId1);
            Assert.NotNull(reloadedContainer1);
            Assert.Equal(secondHoneyTypeId, reloadedContainer1!.HoneyTypeId);

            var reloadedContainer2 = await verifyContext.Containers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == containerId2);
            Assert.NotNull(reloadedContainer2);
            Assert.Equal(secondHoneyTypeId, reloadedContainer2!.HoneyTypeId);
        }
        finally
        {
            await using var cleanupContext = await _fixture.Factory.CreateDbContextAsync();
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM containers WHERE id = {0}", containerId1);
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM containers WHERE id = {0}", containerId2);
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM delivery_lines WHERE id = {0}", lineId);
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM deliveries WHERE id = {0}", deliveryId);
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM warehouses WHERE id = {0}", warehouseId);
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM business_partners WHERE id = {0}", supplierId);
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM honey_types WHERE id = {0}", firstHoneyTypeId);
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM honey_types WHERE id = {0}", secondHoneyTypeId);
        }
    }

    [Fact]
    public async Task WriteOffAsync_PersistsReasonToContainersNotes()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        // 1. Create Warehouse
        var warehouse = new Warehouse
        {
            // unique per creation (Guid) — avoids same-second fixture-code collisions
            Code = $"WH-{Guid.NewGuid():N}"[..17],
            Name = $"Test Warehouse {DateTime.UtcNow.Ticks}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Warehouses.Add(warehouse);
        await context.SaveChangesAsync();
        var warehouseId = warehouse.Id;

        // 2. Create BusinessPartner (supplier)
        var supplier = new BusinessPartner
        {
            PartnerType = PartnerType.Supplier,
            Name = $"Test Supplier {DateTime.UtcNow.Ticks}",
            Country = "Lithuania",
            CountryCode = "LT",
            DefaultLanguage = "LT",
            PaymentTermDays = 14,
            DefaultVatRate = 0m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.BusinessPartners.Add(supplier);
        await context.SaveChangesAsync();
        var supplierId = supplier.Id;

        // 3. Create Delivery
        var delivery = new Delivery
        {
            DeliveryDate = DateTime.UtcNow.Date,
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            Status = "RECEIVED",
            TotalAmount = 0m,
            BarrelsOwed = 0,
            NeedReturnBarrels = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Deliveries.Add(delivery);
        await context.SaveChangesAsync();
        var deliveryId = delivery.Id;

        // 4. Create DeliveryLine
        var line = new DeliveryLine
        {
            DeliveryId = deliveryId,
            ContainerType = "BARREL",
            ContainerCount = 1,
            TotalNetWeight = 100m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.DeliveryLines.Add(line);
        await context.SaveChangesAsync();
        var lineId = line.Id;

        // 5. Create one Container with a pre-existing note (to prove the service appends to it)
        var container = new Container
        {
            ContainerCode = $"CC-{DateTime.UtcNow.Ticks % 10000000:D7}",
            ContainerType = "BARREL",
            DeliveryLineId = lineId,
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            GrossWeight = 110m,
            TareWeight = 10m,
            NetWeight = 100m,
            Quantity = 1,
            RemainingQuantity = 1,
            Status = "IN_STOCK",
            Notes = "original-note",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Containers.Add(container);
        await context.SaveChangesAsync();
        var containerId = container.Id;

        // Act
        const string reason = "Pažeista: plastikinis įtrūkęs";
        var service = new ContainerService(_fixture.Factory);
        await service.WriteOffAsync(new List<int> { containerId }, reason, null);

        // Assert with fresh context (cleanup in finally ensures rows are removed even if assertions fail)
        try
        {
            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();

            var reloadedContainer = await verifyContext.Containers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == containerId);
            Assert.NotNull(reloadedContainer);
            Assert.Equal("WRITTEN_OFF", reloadedContainer!.Status);
            Assert.Equal($"original-note | {reason}", reloadedContainer.Notes); // reason is appended, earlier notes kept

            var movement = await verifyContext.StockMovements
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ContainerId == containerId);
            Assert.NotNull(movement);
            Assert.Equal("OUT", movement!.MovementType);
            Assert.Equal(reason, movement.Notes);
        }
        finally
        {
            await using var cleanupContext = await _fixture.Factory.CreateDbContextAsync();
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM stock_movements WHERE container_id = {0}", containerId);
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM containers WHERE id = {0}", containerId);
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM delivery_lines WHERE id = {0}", lineId);
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM deliveries WHERE id = {0}", deliveryId);
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM warehouses WHERE id = {0}", warehouseId);
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM business_partners WHERE id = {0}", supplierId);
        }
    }

    // ---- honey type change on deliveries: split / merge / guards ----

    /// <summary>Own warehouse, supplier, delivery and honey types for one test; <see cref="CleanupAsync"/> removes everything.</summary>
    private sealed class HoneyTypeScenario
    {
        private readonly DbTestFixture _fixture;
        public int TypeA, TypeB, InactiveType, WarehouseId, SupplierId, DeliveryId;
        public readonly List<int> LineIds = new();
        public readonly List<int> ContainerIds = new();

        public HoneyTypeScenario(DbTestFixture fixture) => _fixture = fixture;

        public static async Task<HoneyTypeScenario> CreateAsync(DbTestFixture fixture, bool signed = false)
        {
            var sc = new HoneyTypeScenario(fixture);
            await using var ctx = await fixture.Factory.CreateDbContextAsync();
            var tag = Guid.NewGuid().ToString("N");

            async Task<int> AddType(string label, bool active)
            {
                var ht = new HoneyType { Code = $"H{Guid.NewGuid():N}"[..12], Name = $"Test {label} {tag}", IsActive = active };
                ctx.HoneyTypes.Add(ht);
                await ctx.SaveChangesAsync();
                return ht.Id;
            }
            sc.TypeA = await AddType("A", true);
            sc.TypeB = await AddType("B", true);
            sc.InactiveType = await AddType("Inactive", false);

            var wh = new Warehouse { Code = $"WH-{tag}"[..17], Name = $"Test Warehouse {tag}", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            ctx.Warehouses.Add(wh);
            await ctx.SaveChangesAsync();
            sc.WarehouseId = wh.Id;

            var supplier = new BusinessPartner
            {
                PartnerType = PartnerType.Supplier, Name = $"Test Supplier {tag}", Country = "Lithuania", CountryCode = "LT",
                DefaultLanguage = "LT", PaymentTermDays = 14, DefaultVatRate = 0m, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            ctx.BusinessPartners.Add(supplier);
            await ctx.SaveChangesAsync();
            sc.SupplierId = supplier.Id;

            var delivery = new Delivery
            {
                DeliveryDate = DateTime.UtcNow.Date, SupplierId = sc.SupplierId, WarehouseId = sc.WarehouseId, Status = "RECEIVED",
                TotalAmount = 0m, BarrelsOwed = 0, NeedReturnBarrels = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            ctx.Deliveries.Add(delivery);
            await ctx.SaveChangesAsync();
            sc.DeliveryId = delivery.Id;

            if (signed)
                await ctx.Database.ExecuteSqlRawAsync(
                    "UPDATE deliveries SET supplier_signed_at = NOW(), supplier_signer_name = {0}, supplier_signature_svg = {1} WHERE id = {2}",
                    "Test Signer", "<svg/>", sc.DeliveryId);
            return sc;
        }

        /// <summary>One line with one container per entry in <paramref name="netWeights"/> (tare 10); totals recomputed afterwards.</summary>
        public async Task<(int LineId, List<int> ContainerIds)> AddLineAsync(int? honeyTypeId, decimal? unitPrice, decimal[] netWeights, string status = "IN_STOCK", bool withContainers = true)
        {
            await using var ctx = await _fixture.Factory.CreateDbContextAsync();
            var line = new DeliveryLine
            {
                DeliveryId = DeliveryId, HoneyTypeId = honeyTypeId, ContainerType = "BARREL", ContainerCount = netWeights.Length,
                TotalNetWeight = netWeights.Sum(), UnitPrice = unitPrice, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            ctx.DeliveryLines.Add(line);
            await ctx.SaveChangesAsync();
            LineIds.Add(line.Id);

            var ids = new List<int>();
            if (withContainers)
            {
                foreach (var net in netWeights)
                {
                    var c = new Container
                    {
                        ContainerCode = $"CC-{Guid.NewGuid():N}"[..10], ContainerType = "BARREL", DeliveryLineId = line.Id, HoneyTypeId = honeyTypeId,
                        SupplierId = SupplierId, WarehouseId = WarehouseId, GrossWeight = net + 10m, TareWeight = 10m, NetWeight = net,
                        Quantity = 1, RemainingQuantity = 1, Status = status, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                    };
                    ctx.Containers.Add(c);
                    await ctx.SaveChangesAsync();
                    ids.Add(c.Id);
                    ContainerIds.Add(c.Id);
                }
            }
            return (line.Id, ids);
        }

        /// <summary>Consistent totals plus a cached receipt path, as on a real delivery.</summary>
        public async Task FinalizeAsync()
        {
            await using var ctx = await _fixture.Factory.CreateDbContextAsync();
            await DeliveryTotals.RecalculateAsync(ctx, DeliveryId);
            await ctx.Database.ExecuteSqlRawAsync("UPDATE deliveries SET receipt_pdf_path = {0} WHERE id = {1}", "cached-receipt.pdf", DeliveryId);
        }

        public async Task<DeliverySnapshot> SnapshotAsync()
        {
            await using var ctx = await _fixture.Factory.CreateDbContextAsync();
            var delivery = await ctx.Deliveries.AsNoTracking().FirstAsync(d => d.Id == DeliveryId);
            var lines = await ctx.DeliveryLines.AsNoTracking().Where(l => l.DeliveryId == DeliveryId).OrderBy(l => l.Id).ToListAsync();
            var lineIds = lines.Select(l => l.Id).ToList();
            var containers = await ctx.Containers.AsNoTracking().Where(c => c.DeliveryLineId.HasValue && lineIds.Contains(c.DeliveryLineId.Value)).ToListAsync();
            return new DeliverySnapshot(delivery, lines, containers);
        }

        public async Task CleanupAsync()
        {
            await using var ctx = await _fixture.Factory.CreateDbContextAsync();
            await ctx.Database.ExecuteSqlRawAsync(
                "DELETE FROM containers WHERE delivery_line_id IN (SELECT id FROM (SELECT id FROM delivery_lines WHERE delivery_id = {0}) x)", DeliveryId);
            foreach (var id in ContainerIds)
                await ctx.Database.ExecuteSqlRawAsync("DELETE FROM containers WHERE id = {0}", id);
            await ctx.Database.ExecuteSqlRawAsync("DELETE FROM delivery_lines WHERE delivery_id = {0}", DeliveryId);
            await ctx.Database.ExecuteSqlRawAsync("DELETE FROM deliveries WHERE id = {0}", DeliveryId);
            await ctx.Database.ExecuteSqlRawAsync("DELETE FROM warehouses WHERE id = {0}", WarehouseId);
            await ctx.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", SupplierId);
            foreach (var id in new[] { TypeA, TypeB, InactiveType })
                await ctx.Database.ExecuteSqlRawAsync("DELETE FROM honey_types WHERE id = {0}", id);
        }
    }

    private sealed record DeliverySnapshot(Delivery Delivery, List<DeliveryLine> Lines, List<Container> Containers);

    [Fact]
    public async Task UpdateHoneyTypeAsync_Subset_SplitsLine_TotalsUnchanged_ReceiptCleared()
    {
        var sc = await HoneyTypeScenario.CreateAsync(_fixture);
        try
        {
            var (_, ids) = await sc.AddLineAsync(sc.TypeA, 2m, new[] { 100m, 100m, 50m });
            await sc.FinalizeAsync();
            var before = await sc.SnapshotAsync();
            Assert.Equal(250m, before.Delivery.TotalNetWeight);
            Assert.Equal(500m, before.Delivery.TotalAmount);

            await new ContainerService(_fixture.Factory).UpdateHoneyTypeAsync(new List<int> { ids[0], ids[1] }, sc.TypeB);

            var after = await sc.SnapshotAsync();
            Assert.Equal(2, after.Lines.Count);
            var lineA = Assert.Single(after.Lines, l => l.HoneyTypeId == sc.TypeA);
            var lineB = Assert.Single(after.Lines, l => l.HoneyTypeId == sc.TypeB);
            Assert.Equal(50m, lineA.TotalNetWeight);
            Assert.Equal(200m, lineB.TotalNetWeight);
            Assert.Equal(1, lineA.ContainerCount);
            Assert.Equal(2, lineB.ContainerCount);
            Assert.Equal(2m, lineB.UnitPrice); // split-off line keeps the price
            Assert.Equal(250m, after.Delivery.TotalNetWeight);
            Assert.Equal(500m, after.Delivery.TotalAmount);
            Assert.Null(after.Delivery.ReceiptPdfPath);
        }
        finally { await sc.CleanupAsync(); }
    }

    [Fact]
    public async Task UpdateHoneyTypeAsync_BackToOriginalType_MergesLinesAgain()
    {
        var sc = await HoneyTypeScenario.CreateAsync(_fixture);
        try
        {
            var (lineId, ids) = await sc.AddLineAsync(sc.TypeA, 2m, new[] { 100m, 100m, 50m });
            await sc.FinalizeAsync();
            var service = new ContainerService(_fixture.Factory);
            await service.UpdateHoneyTypeAsync(new List<int> { ids[0], ids[1] }, sc.TypeB);
            Assert.Equal(2, (await sc.SnapshotAsync()).Lines.Count);

            await service.UpdateHoneyTypeAsync(new List<int> { ids[0], ids[1] }, sc.TypeA);

            var after = await sc.SnapshotAsync();
            var line = Assert.Single(after.Lines);
            Assert.Equal(lineId, line.Id); // lowest id survives
            Assert.Equal(sc.TypeA, line.HoneyTypeId);
            Assert.Equal(250m, line.TotalNetWeight);
            Assert.Equal(3, line.ContainerCount);
            Assert.All(after.Containers, c => Assert.Equal(lineId, c.DeliveryLineId));
            Assert.Equal(250m, after.Delivery.TotalNetWeight);
            Assert.Equal(500m, after.Delivery.TotalAmount);
        }
        finally { await sc.CleanupAsync(); }
    }

    [Fact]
    public async Task UpdateHoneyTypeAsync_SameTypeDifferentUnitPrice_NotMerged()
    {
        var sc = await HoneyTypeScenario.CreateAsync(_fixture);
        try
        {
            await sc.AddLineAsync(sc.TypeA, 2m, new[] { 100m });
            var (_, idsB) = await sc.AddLineAsync(sc.TypeB, 3m, new[] { 100m });
            await sc.FinalizeAsync();

            await new ContainerService(_fixture.Factory).UpdateHoneyTypeAsync(idsB, sc.TypeA);

            var after = await sc.SnapshotAsync();
            Assert.Equal(2, after.Lines.Count);
            Assert.All(after.Lines, l => Assert.Equal(sc.TypeA, l.HoneyTypeId));
            Assert.Equal(new[] { 2m, 3m }, after.Lines.Select(l => l.UnitPrice!.Value).OrderBy(p => p).ToArray());
            Assert.Equal(500m, after.Delivery.TotalAmount); // 100 x 2 + 100 x 3
        }
        finally { await sc.CleanupAsync(); }
    }

    [Theory]
    [InlineData("RESERVED")]
    [InlineData("IN_PRODUCTION")]
    public async Task UpdateHoneyTypeAsync_ContainerNotChangeable_ThrowsAndWritesNothing(string status)
    {
        var sc = await HoneyTypeScenario.CreateAsync(_fixture);
        try
        {
            var (_, inStock) = await sc.AddLineAsync(sc.TypeA, 2m, new[] { 100m });
            var (_, blocked) = await sc.AddLineAsync(sc.TypeA, 2m, new[] { 100m }, status);
            await sc.FinalizeAsync();
            var before = await sc.SnapshotAsync();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new ContainerService(_fixture.Factory).UpdateHoneyTypeAsync(new List<int> { inStock[0], blocked[0] }, sc.TypeB));
            Assert.Contains(status, ex.Message);

            var after = await sc.SnapshotAsync();
            Assert.Equal(before.Lines.Select(l => (l.Id, l.HoneyTypeId, l.TotalNetWeight)), after.Lines.Select(l => (l.Id, l.HoneyTypeId, l.TotalNetWeight)));
            Assert.All(after.Containers, c => Assert.Equal(sc.TypeA, c.HoneyTypeId));
            Assert.Equal(before.Delivery.TotalNetWeight, after.Delivery.TotalNetWeight);
            Assert.Equal(before.Delivery.TotalAmount, after.Delivery.TotalAmount);
            Assert.Equal("cached-receipt.pdf", after.Delivery.ReceiptPdfPath);
        }
        finally { await sc.CleanupAsync(); }
    }

    [Theory]
    [InlineData(false)] // inactive type
    [InlineData(true)]  // type that does not exist
    public async Task UpdateHoneyTypeAsync_InactiveOrMissingType_ThrowsAndWritesNothing(bool missing)
    {
        var sc = await HoneyTypeScenario.CreateAsync(_fixture);
        try
        {
            var (_, ids) = await sc.AddLineAsync(sc.TypeA, 2m, new[] { 100m, 100m });
            await sc.FinalizeAsync();
            var before = await sc.SnapshotAsync();
            var target = missing ? int.MaxValue : sc.InactiveType;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new ContainerService(_fixture.Factory).UpdateHoneyTypeAsync(new List<int> { ids[0] }, target));
            Assert.Equal("Medaus rūšis nerasta arba neaktyvi.", ex.Message);

            var after = await sc.SnapshotAsync();
            Assert.Single(after.Lines);
            Assert.Equal(sc.TypeA, after.Lines[0].HoneyTypeId);
            Assert.All(after.Containers, c => Assert.Equal(sc.TypeA, c.HoneyTypeId));
            Assert.Equal(before.Delivery.TotalNetWeight, after.Delivery.TotalNetWeight);
            Assert.Equal("cached-receipt.pdf", after.Delivery.ReceiptPdfPath);
        }
        finally { await sc.CleanupAsync(); }
    }

    [Fact]
    public async Task UpdateHoneyTypeAsync_SignedDelivery_Allowed_SignatureKept_ReceiptCleared()
    {
        var sc = await HoneyTypeScenario.CreateAsync(_fixture, signed: true);
        try
        {
            var (_, ids) = await sc.AddLineAsync(sc.TypeA, 2m, new[] { 100m, 100m });
            await sc.FinalizeAsync();
            var before = await sc.SnapshotAsync();
            Assert.NotNull(before.Delivery.SupplierSignedAt);

            await new ContainerService(_fixture.Factory).UpdateHoneyTypeAsync(new List<int> { ids[0] }, sc.TypeB);

            var after = await sc.SnapshotAsync();
            Assert.Equal(2, after.Lines.Count);
            Assert.NotNull(after.Delivery.SupplierSignedAt);
            Assert.Equal(before.Delivery.SupplierSignedAt, after.Delivery.SupplierSignedAt);
            Assert.Equal("Test Signer", after.Delivery.SupplierSignerName);
            Assert.Equal("<svg/>", after.Delivery.SupplierSignatureSvg);
            Assert.Null(after.Delivery.ReceiptPdfPath);
            Assert.Equal(before.Delivery.TotalNetWeight, after.Delivery.TotalNetWeight);
        }
        finally { await sc.CleanupAsync(); }
    }

    [Fact]
    public async Task UpdateDeliveryLineHoneyTypeAsync_NullTypeLine_SetsLineAndAllContainers()
    {
        var sc = await HoneyTypeScenario.CreateAsync(_fixture);
        try
        {
            var (lineId, ids) = await sc.AddLineAsync(null, 2m, new[] { 100m, 50m });
            await sc.FinalizeAsync();

            await new ContainerService(_fixture.Factory).UpdateDeliveryLineHoneyTypeAsync(lineId, sc.TypeA);

            var after = await sc.SnapshotAsync();
            var line = Assert.Single(after.Lines);
            Assert.Equal(sc.TypeA, line.HoneyTypeId);
            Assert.Equal(150m, line.TotalNetWeight);
            Assert.Equal(2, after.Containers.Count);
            Assert.All(after.Containers, c => Assert.Equal(sc.TypeA, c.HoneyTypeId));
            Assert.Null(after.Delivery.ReceiptPdfPath);
        }
        finally { await sc.CleanupAsync(); }
    }

    [Fact]
    public async Task UpdateDeliveryLineHoneyTypeAsync_LegacyLineWithoutContainers_SetsLineType()
    {
        var sc = await HoneyTypeScenario.CreateAsync(_fixture);
        try
        {
            var (lineId, _) = await sc.AddLineAsync(null, 2m, new[] { 80m }, withContainers: false);
            await sc.FinalizeAsync();

            await new ContainerService(_fixture.Factory).UpdateDeliveryLineHoneyTypeAsync(lineId, sc.TypeB);

            var after = await sc.SnapshotAsync();
            var line = Assert.Single(after.Lines);
            Assert.Equal(sc.TypeB, line.HoneyTypeId);
            Assert.Equal(80m, line.TotalNetWeight); // legacy weight untouched
            Assert.Null(after.Delivery.ReceiptPdfPath);
        }
        finally { await sc.CleanupAsync(); }
    }
}
