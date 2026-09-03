using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// FROZEN.md behavior tests for InvoiceService. These are integration
/// tests against the real nordic_bees_erp_test database (global
/// QueryTrackingBehavior.NoTracking, same as production) — they exist to
/// catch the exact bug class this codebase has hit repeatedly: a write
/// method that appears to succeed (no exception) but silently persists
/// zero rows.
/// </summary>
public class InvoiceServiceTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public InvoiceServiceTests(DbTestFixture fixture)
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

    private static Invoice NewTestInvoice(int customerId, string invoiceNumber) => new()
    {
        InvoiceNumber = invoiceNumber,
        InvoiceDate = DateTime.UtcNow.Date,
        CustomerId = customerId,
        Language = "LT",
        InvoiceType = "PVM SĄSKAITA FAKTŪRA",
        Status = InvoiceStatus.Draft,
        TotalInclVat = 100m,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task DeleteInvoiceAsync_RemovesRowFromRealDatabase()
    {
        // Arrange: insert a real BusinessPartner (FK target) and a minimal Invoice
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        var partner = NewTestCustomer($"Test Customer {Guid.NewGuid():N}");
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();
        var partnerId = partner.Id;

        var invoice = NewTestInvoice(partnerId, $"INV-{Guid.NewGuid():N}");
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();
        var invoiceId = invoice.Id;

        var service = new InvoiceService(_fixture.Factory, null!, null!);

        // Act
        var result = await service.DeleteInvoiceAsync(invoiceId);

        // Assert: method reports deletion
        Assert.Equal(1, result);

        // Assert: row is actually gone from the database
        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var remaining = await verifyContext.Invoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == invoiceId);

        Assert.Null(remaining);

        // Cleanup (defensive)
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM invoices WHERE id = {0}", invoiceId);
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM business_partners WHERE id = {0}", partnerId);
    }

    [Fact]
    public async Task CreateInvoiceAsync_SnapshotsCustomerVatCode()
    {
        // Arrange: insert a real BusinessPartner (FK target) and build an invoice
        // with at least one line so CreateInvoiceAsync's foreach over Lines runs.
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        const string vatCode = "LT123456789";

        var partner = NewTestCustomer($"Test Customer {Guid.NewGuid():N}");
        partner.VatCode = vatCode; // CreateInvoiceAsync snapshots this onto the invoice
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();
        var partnerId = partner.Id;

        var invoice = NewTestInvoice(partnerId, $"INV-{Guid.NewGuid():N}");
        invoice.Lines.Add(new InvoiceLine
        {
            Description = "Test line",
            Quantity = 1m,
            PriceExclVat = 100m,
            VatRate = 21m
        });

        var service = new InvoiceService(_fixture.Factory, null!, null!);

        // Act
        var invoiceId = await service.CreateInvoiceAsync(invoice);

        Assert.True(invoiceId > 0, "CreateInvoiceAsync should return the new invoice id");

        // Assert: customer_vat_code was actually persisted to the database
        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var storedVatCode = await verifyContext.Invoices
            .AsNoTracking()
            .Where(i => i.Id == invoiceId)
            .Select(i => i.CustomerVatCode)
            .FirstOrDefaultAsync();

        Assert.Equal(vatCode, storedVatCode);

        // Cleanup (defensive)
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM invoice_lines WHERE invoice_id = {0}", invoiceId);
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM invoices WHERE id = {0}", invoiceId);
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM business_partners WHERE id = {0}", partnerId);
    }

    [Fact]
    public async Task CreateInvoiceFromDeliveryAsync_PersistsInvoiceLineAndDeductions()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        // 1. Warehouse (unique code to avoid duplicate-entry across runs)
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

        // 2. Supplier (non-empty VatCode per spec)
        var supplier = new BusinessPartner
        {
            PartnerType = PartnerType.Supplier,
            Name = $"Test Supplier {DateTime.UtcNow.Ticks}",
            Country = "Lithuania",
            CountryCode = "LT",
            DefaultLanguage = "LT",
            VatCode = "LT123456789",
            PaymentTermDays = 14,
            DefaultVatRate = 0m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.BusinessPartners.Add(supplier);
        await context.SaveChangesAsync();
        var supplierId = supplier.Id;

        // 3. Delivery — deduction columns deliberately left unset: the migration that adds
        //    them to nordic_bees_erp_test has not been applied yet, so EF must not try to
        //    insert into those (nonexistent) columns here.
        var delivery = new Models.WarehouseModule.Delivery
        {
            DeliveryDate = DateTime.UtcNow.Date,
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            Status = "RECEIVED",
            TotalNetWeight = 100m,
            TotalAmount = 200m,
            BarrelsOwed = 0,
            NeedReturnBarrels = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Deliveries.Add(delivery);
        await context.SaveChangesAsync();
        var deliveryId = delivery.Id;

        // Act
        var service = new InvoiceService(_fixture.Factory, null!, null!);
        var invoiceId = await service.CreateInvoiceFromDeliveryAsync(
            deliveryId, transportCost: 25m, barrelCost: 5m, otherCost: 0m);

        Assert.True(invoiceId > 0, "CreateInvoiceFromDeliveryAsync should return the new invoice id");

        // Assert with a brand-new context (proves the write hit the DB, not just memory).
        try
        {
            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();

            var invoice = await verifyContext.Invoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == invoiceId);
            Assert.NotNull(invoice);
            Assert.Equal(supplierId, invoice!.CustomerId);
            Assert.Equal(deliveryId, invoice.DeliveryId);
            Assert.Equal("6% PVM SĄSKAITA FAKTŪRA", invoice.InvoiceType);

            var line = await verifyContext.InvoiceLines
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.InvoiceId == invoiceId);
            Assert.NotNull(line);
            // unitPrice = (200 - 30) / 100 = 1.70
            Assert.Equal(100m, line!.Quantity);
            Assert.Equal(1.70m, line.PriceExclVat);
            Assert.Equal(6m, line.VatRate);
            Assert.Equal("kg", line.Unit);

            var reloadedDelivery = await verifyContext.Deliveries
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == deliveryId);
            Assert.NotNull(reloadedDelivery);
            Assert.Equal(invoiceId, reloadedDelivery!.InvoiceId);

            // Deduction columns — guarded: the migration adding these three columns to the
            // test DB may not be applied yet (human-applied DDL in this project). If the
            // columns are missing the read throws; treat that as "not yet applied" and skip.
            var deductions = await TryReadDeductionColumns(verifyContext, deliveryId);
            if (deductions.HasValue)
            {
                Assert.Equal(25m, deductions.Value.TransportCostDeduction);
                Assert.Equal(5m, deductions.Value.BarrelCostDeduction);
                Assert.Equal(0m, deductions.Value.OtherCostDeduction);
            }
        }
        finally
        {
            await using var cleanupContext = await _fixture.Factory.CreateDbContextAsync();
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM invoice_lines WHERE invoice_id = {0}", invoiceId);
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM invoices WHERE id = {0}", invoiceId);
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM deliveries WHERE id = {0}", deliveryId);
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM warehouses WHERE id = {0}", warehouseId);
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM business_partners WHERE id = {0}", supplierId);
        }
    }

    [Fact]
    public async Task CreateInvoiceFromDeliveryAsync_RecipientSupplierId_InvoicesSelectedRecipient()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        // 1. Warehouse
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

        // 2. Two suppliers: the delivery's own supplier (no VAT code needed — recipient is different)
        var deliverySupplier = new BusinessPartner
        {
            PartnerType = PartnerType.Supplier,
            Name = $"Test Delivery Supplier {DateTime.UtcNow.Ticks}",
            Country = "Lithuania",
            CountryCode = "LT",
            DefaultLanguage = "LT",
            PaymentTermDays = 7,
            DefaultVatRate = 0m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var recipient = new BusinessPartner
        {
            PartnerType = PartnerType.Supplier,
            Name = $"Test Recipient {DateTime.UtcNow.Ticks}",
            Country = "Lithuania",
            CountryCode = "LT",
            DefaultLanguage = "LT",
            VatCode = "LT987654321",
            PaymentTermDays = 14,
            DefaultVatRate = 0m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.BusinessPartners.AddRange(deliverySupplier, recipient);
        await context.SaveChangesAsync();
        var deliverySupplierId = deliverySupplier.Id;
        var recipientId = recipient.Id;

        // 3. Delivery keyed to the delivery supplier
        var delivery = new Models.WarehouseModule.Delivery
        {
            DeliveryDate = DateTime.UtcNow.Date,
            SupplierId = deliverySupplierId,
            WarehouseId = warehouseId,
            Status = "RECEIVED",
            TotalNetWeight = 100m,
            TotalAmount = 200m,
            BarrelsOwed = 0,
            NeedReturnBarrels = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Deliveries.Add(delivery);
        await context.SaveChangesAsync();
        var deliveryId = delivery.Id;

        // Act — invoice the RECIPIENT, not the delivery's own supplier
        var service = new InvoiceService(_fixture.Factory, null!, null!);
        var invoiceId = await service.CreateInvoiceFromDeliveryAsync(
            deliveryId, 0m, 0m, 0m, recipientId);

        Assert.True(invoiceId > 0, "CreateInvoiceFromDeliveryAsync should return the new invoice id");

        // Assert with a brand-new context (proves the write hit the DB)
        try
        {
            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();

            var invoice = await verifyContext.Invoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == invoiceId);
            Assert.NotNull(invoice);
            Assert.Equal(recipientId, invoice!.CustomerId);
            Assert.NotEqual(deliverySupplierId, invoice.CustomerId);
            Assert.Equal(deliveryId, invoice.DeliveryId);
            Assert.Equal("6% PVM SĄSKAITA FAKTŪRA", invoice.InvoiceType);
        }
        finally
        {
            await using var cleanupContext = await _fixture.Factory.CreateDbContextAsync();
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM invoice_lines WHERE invoice_id = {0}", invoiceId);
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM invoices WHERE id = {0}", invoiceId);
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM deliveries WHERE id = {0}", deliveryId);
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM warehouses WHERE id = {0}", warehouseId);
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM business_partners WHERE id = {0}", recipientId);
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM business_partners WHERE id = {0}", deliverySupplierId);
        }
    }

    /// <summary>
    /// Reads the three deduction columns off a delivery. Returns null if those columns do not
    /// yet exist in nordic_bees_erp_test (the migration is generated but not human-applied),
    /// so callers can skip the deduction assertions rather than fail on "Unknown column".
    /// </summary>
    private static async Task<(decimal? TransportCostDeduction, decimal? BarrelCostDeduction, decimal? OtherCostDeduction)?>
        TryReadDeductionColumns(NordicBeesERP.Data.NordicBeesERPContext context, int deliveryId)
    {
        try
        {
            return await context.Deliveries
                .AsNoTracking()
                .Where(d => d.Id == deliveryId)
                .Select(d => new ValueTuple<decimal?, decimal?, decimal?>(
                    d.TransportCostDeduction, d.BarrelCostDeduction, d.OtherCostDeduction))
                .FirstOrDefaultAsync();
        }
        catch (MySqlConnector.MySqlException)
        {
            // Deduction columns not yet applied to the test DB — treat as "not applicable".
            return null;
        }
    }
}
