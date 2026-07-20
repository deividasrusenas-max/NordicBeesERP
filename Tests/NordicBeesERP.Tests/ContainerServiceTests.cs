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
            Code = $"WH-{DateTime.UtcNow.Ticks % 10000000:D7}",
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
}
