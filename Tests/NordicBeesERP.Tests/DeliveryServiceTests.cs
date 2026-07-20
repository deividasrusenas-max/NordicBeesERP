using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Models.WarehouseModule;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Integration tests for DeliveryService.UpdatePricesAsync against the real
/// nordic_bees_erp_test database. Verifies that the ExecuteSqlRawAsync write
/// path actually persists changes (no silent NoTracking drop).
/// </summary>
public class DeliveryServiceTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public DeliveryServiceTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UpdatePricesAsync_PersistsLineAndDeliveryTotals()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        // 1. Create Warehouse (unique code to avoid duplicate-entry across runs)
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
            DeliveryDate = DateTime.UtcNow,
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
            UnitPrice = null,
            LineTotal = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.DeliveryLines.Add(line);
        await context.SaveChangesAsync();
        var lineId = line.Id;

        // Act
        var service = new DeliveryService(_fixture.Factory);
        var updatedLines = new List<DeliveryLine>
        {
            new()
            {
                Id = lineId,
                DeliveryId = deliveryId,
                ContainerType = "BARREL",
                ContainerCount = 1,
                TotalNetWeight = 100m,
                UnitPrice = 10.50m,
            }
        };

        await service.UpdatePricesAsync(deliveryId, updatedLines, barrelsOwed: 5);

        // Assert with fresh context (cleanup in finally ensures rows are removed even if assertions fail)
        try
        {
            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();

            var reloadedLine = await verifyContext.DeliveryLines
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == lineId);
            Assert.NotNull(reloadedLine);
            Assert.Equal(10.50m, reloadedLine!.UnitPrice);
            Assert.Equal(1050m, reloadedLine.LineTotal);

            var reloadedDelivery = await verifyContext.Deliveries
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == deliveryId);
            Assert.NotNull(reloadedDelivery);
            Assert.Equal(1050m, reloadedDelivery!.TotalAmount);
            Assert.Equal(5, reloadedDelivery.BarrelsOwed);
            Assert.Equal("RECEIVED", reloadedDelivery.Status);
        }
        finally
        {
            await using var cleanupContext = await _fixture.Factory.CreateDbContextAsync();
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
}
