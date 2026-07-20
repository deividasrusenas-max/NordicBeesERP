using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Models.WarehouseModule;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// FROZEN.md behavior tests for SupplierPaymentService. These are integration
/// tests against the real nordic_bees_erp_test database (global
/// QueryTrackingBehavior.NoTracking, same as production) — they exist to
/// catch the exact bug class this codebase has hit repeatedly: a write
/// method that appears to succeed (no exception) but silently persists
/// zero rows.
/// </summary>
public class SupplierPaymentServiceTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public SupplierPaymentServiceTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UpdateAsync_PersistsChangesToRealDatabase()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        // 1. Create Warehouse (deliveries.warehouse_id FK)
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

        // 2. Create BusinessPartner (supplier_id FK for both deliveries and supplier_payments)
        var supplier = new BusinessPartner
        {
            PartnerType = PartnerType.Supplier,
            Name = $"Test Supplier {DateTime.UtcNow.Ticks}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.BusinessPartners.Add(supplier);
        await context.SaveChangesAsync();
        var supplierId = supplier.Id;

        // 3. Create Delivery (supplier_payments.delivery_id FK)
        var delivery = new Delivery
        {
            DeliveryDate = DateTime.UtcNow,
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            Status = "RECEIVED",
            TotalAmount = 0m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Deliveries.Add(delivery);
        await context.SaveChangesAsync();
        var deliveryId = delivery.Id;

        // 4. Create SupplierPayment with real FK IDs
        var payment = new SupplierPayment
        {
            DeliveryId = deliveryId,
            SupplierId = supplierId,
            Amount = 100.50m,
            PaymentDate = DateTime.UtcNow,
            PaymentMethod = "bank_transfer",
            Notes = "original notes",
            CreatedAt = DateTime.UtcNow
        };
        context.SupplierPayments.Add(payment);
        await context.SaveChangesAsync();
        var id = payment.Id;

        var deliveryService = new DeliveryService(_fixture.Factory);
        var service = new SupplierPaymentService(_fixture.Factory, deliveryService);

        payment.Amount = 250.75m;
        payment.Notes = "updated notes";
        await service.UpdateAsync(payment);

        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var reloaded = await verifyContext.SupplierPayments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        Assert.NotNull(reloaded);
        Assert.Equal(250.75m, reloaded!.Amount);
        Assert.Equal("updated notes", reloaded.Notes);

        // Cleanup: delete in reverse FK order
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM supplier_payments WHERE id = {0}", id);
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM deliveries WHERE id = {0}", deliveryId);
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM business_partners WHERE id = {0}", supplierId);
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM warehouses WHERE id = {0}", warehouseId);
    }
}
