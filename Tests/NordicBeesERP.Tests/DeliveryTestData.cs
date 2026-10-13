using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models;
using NordicBeesERP.Models.WarehouseModule;

namespace NordicBeesERP.Tests;

/// <summary>
/// Creates and removes the supplier/warehouse/delivery rows that delivery
/// integration tests need on the nordic_bees_erp_test database.
/// </summary>
internal sealed class DeliveryTestData : IAsyncDisposable
{
    private readonly IDbContextFactory<NordicBeesERPContext> _factory;
    private readonly string _codePrefix = $"T{Guid.NewGuid():N}"[..8];

    public int WarehouseId { get; private set; }
    public int SupplierId { get; private set; }
    public int? DeliveryId { get; set; }

    private DeliveryTestData(IDbContextFactory<NordicBeesERPContext> factory) => _factory = factory;

    /// <summary>Unique container code that cannot collide with other runs.</summary>
    public string Code(string suffix) => $"{_codePrefix}{suffix}";

    public string CodePrefix => _codePrefix;

    public static async Task<DeliveryTestData> CreateAsync(IDbContextFactory<NordicBeesERPContext> factory)
    {
        var data = new DeliveryTestData(factory);
        await using var context = await factory.CreateDbContextAsync();

        var warehouse = new Warehouse
        {
            Code = $"WH-{Guid.NewGuid():N}"[..17],
            Name = $"Test Warehouse {DateTime.UtcNow.Ticks}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Warehouses.Add(warehouse);
        await context.SaveChangesAsync();
        data.WarehouseId = warehouse.Id;

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
        data.SupplierId = supplier.Id;

        return data;
    }

    public Delivery NewDelivery() => new()
    {
        DeliveryDate = DateTime.Today,
        SupplierId = SupplierId,
        WarehouseId = WarehouseId,
        Status = "RECEIVED"
    };

    public Container NewContainer(string suffix, string type, decimal gross, decimal tare) => new()
    {
        ContainerCode = Code(suffix),
        ContainerType = type,
        GrossWeight = gross,
        TareWeight = tare,
        Quantity = 1,
        RemainingQuantity = 1,
        Status = "IN_STOCK"
    };

    public async ValueTask DisposeAsync()
    {
        await using var context = await _factory.CreateDbContextAsync();
        var like = _codePrefix + "%";
        await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM stock_movements WHERE container_id IN (SELECT id FROM containers WHERE container_code LIKE {0})", like);
        await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM containers WHERE container_code LIKE {0}", like);
        await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM delivery_lines WHERE delivery_id IN (SELECT id FROM deliveries WHERE supplier_id = {0})", SupplierId);
        await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM deliveries WHERE supplier_id = {0}", SupplierId);
        await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM warehouses WHERE id = {0}", WarehouseId);
        await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM business_partners WHERE id = {0}", SupplierId);
    }
}
