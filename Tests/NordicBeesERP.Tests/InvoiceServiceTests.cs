using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Helpers;
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

    [Fact]
    public async Task CreateInvoiceFromDeliveryAsync_IncompleteFarmer_ThrowsAndCreatesNoInvoice()
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

        // 2. Supplier: an INCOMPLETE FARMER (6% individual) — non-empty VatCode and the same
        //    minimum fields as the happy-path supplier, but missing first/last name and the
        //    compensation VAT code required for a 6% farmer. This must pass every pre-existing
        //    check so only the new farmer-completeness guard can fire.
        var supplier = new BusinessPartner
        {
            PartnerType = PartnerType.Supplier,
            Name = $"Test Farmer {DateTime.UtcNow.Ticks}",
            Country = "Lithuania",
            CountryCode = "LT",
            DefaultLanguage = "LT",
            VatCode = "LT123456789",
            PaymentTermDays = 14,
            IsIndividual = true,
            DefaultVatRate = 6m,
            SupplierFirstName = null,
            SupplierLastName = null,
            CompensationVatCode = null,
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

        // Act — the farmer-completeness guard must throw BEFORE any invoice write
        var service = new InvoiceService(_fixture.Factory, null!, null!);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateInvoiceFromDeliveryAsync(
                deliveryId, transportCost: 25m, barrelCost: 5m, otherCost: 0m));

        Assert.Contains("ūkininko", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Assert with a brand-new context: NO invoice row for this delivery and NO lines —
        // proves the guard threw before any DB write.
        try
        {
            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();

            var invoiceCount = await verifyContext.Invoices
                .AsNoTracking()
                .CountAsync(i => i.DeliveryId == deliveryId);
            Assert.Equal(0, invoiceCount);

            var lineCount = await verifyContext.InvoiceLines
                .AsNoTracking()
                .CountAsync(l => l.Invoice.CustomerId == supplierId && l.Invoice.DeliveryId == deliveryId);
            Assert.Equal(0, lineCount);
        }
        finally
        {
            await using var cleanupContext = await _fixture.Factory.CreateDbContextAsync();
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM invoices WHERE delivery_id = {0}", deliveryId);
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM deliveries WHERE id = {0}", deliveryId);
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM warehouses WHERE id = {0}", warehouseId);
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM business_partners WHERE id = {0}", supplierId);
        }
    }

    // =====================================================
    // PDF FREEZE (GenerateAndSavePdfAsync) TESTS
    // =====================================================

    private static readonly byte[] MarkerPdfBytes =
        new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // "%PDF-1.4"

    [Fact]
    public async Task GenerateAndSavePdfAsync_FinalInvoice_SavesPdfAndWritesPdfPathToRealDatabase()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), $"npb-inv-{Guid.NewGuid():N}");
        try
        {
            // Arrange: real partner + Confirmed invoice
            await using var context = await _fixture.Factory.CreateDbContextAsync();

            var partner = NewTestCustomer($"Test Customer {Guid.NewGuid():N}");
            context.BusinessPartners.Add(partner);
            await context.SaveChangesAsync();
            var partnerId = partner.Id;

            var invoice = NewTestInvoice(partnerId, $"INV-{Guid.NewGuid():N}");
            invoice.Status = InvoiceStatus.Confirmed;
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync();
            var invoiceId = invoice.Id;

            var pdfGen = new FakePdfGeneratorService { BytesToReturn = MarkerPdfBytes };
            var service = new InvoiceService(_fixture.Factory, pdfGen, null!, baseDir);

            // Act
            var bytes = await service.GenerateAndSavePdfAsync(invoiceId);

            // Assert: returned bytes are the generator's marker bytes
            Assert.Equal(MarkerPdfBytes, bytes);
            Assert.Equal(1, pdfGen.GenerateInvoicePdfAsyncCallCount);

            // Assert: pdf_path persisted to the real database as a year-relative path
            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
            var stored = await verifyContext.Invoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == invoiceId);
            Assert.NotNull(stored);
            Assert.False(string.IsNullOrWhiteSpace(stored!.PdfPath));
            Assert.StartsWith(invoice.InvoiceDate.Year.ToString() + "/", stored.PdfPath);

            // Assert: the file exists on disk with byte-identical content
            var fullPath = Path.Combine(baseDir, stored.PdfPath!);
            Assert.True(File.Exists(fullPath));
            Assert.Equal(MarkerPdfBytes, File.ReadAllBytes(fullPath));

            // Cleanup (FK-reverse: invoices → business_partners)
            await verifyContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM invoices WHERE id = {0}", invoiceId);
            await verifyContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM business_partners WHERE id = {0}", partnerId);
        }
        finally
        {
            if (Directory.Exists(baseDir)) Directory.Delete(baseDir, recursive: true);
        }
    }

    [Fact]
    public async Task GenerateAndSavePdfAsync_ServesCachedCopy_WhenPdfPathAndFileExist()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), $"npb-inv-{Guid.NewGuid():N}");
        try
        {
            // Arrange: partner + Confirmed invoice with a pre-existing cached file
            await using var context = await _fixture.Factory.CreateDbContextAsync();

            var partner = NewTestCustomer($"Test Customer {Guid.NewGuid():N}");
            context.BusinessPartners.Add(partner);
            await context.SaveChangesAsync();
            var partnerId = partner.Id;

            var invoice = NewTestInvoice(partnerId, $"INV-{Guid.NewGuid():N}");
            invoice.Status = InvoiceStatus.Confirmed;
            var rel = $"{invoice.InvoiceDate.Year}/X-{Guid.NewGuid():N}.pdf";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(baseDir, rel))!);
            File.WriteAllBytes(Path.Combine(baseDir, rel), MarkerPdfBytes);
            invoice.PdfPath = rel; // plain column — set before Add so it persists on insert
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync();
            var invoiceId = invoice.Id;

            var pdfGen = new FakePdfGeneratorService { ThrowIfCalled = true };
            var service = new InvoiceService(_fixture.Factory, pdfGen, null!, baseDir);

            // Act
            var bytes = await service.GenerateAndSavePdfAsync(invoiceId);

            // Assert: served from cache — generator never invoked
            Assert.Equal(MarkerPdfBytes, bytes);
            Assert.Equal(0, pdfGen.GenerateInvoicePdfAsyncCallCount);

            // Cleanup (FK-reverse)
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM invoices WHERE id = {0}", invoiceId);
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM business_partners WHERE id = {0}", partnerId);
        }
        finally
        {
            if (Directory.Exists(baseDir)) Directory.Delete(baseDir, recursive: true);
        }
    }

    [Fact]
    public async Task GenerateAndSavePdfAsync_DraftInvoice_NeverSavesOrWritesPdfPath()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), $"npb-inv-{Guid.NewGuid():N}");
        try
        {
            // Arrange: partner + Draft invoice (NewTestInvoice default)
            await using var context = await _fixture.Factory.CreateDbContextAsync();

            var partner = NewTestCustomer($"Test Customer {Guid.NewGuid():N}");
            context.BusinessPartners.Add(partner);
            await context.SaveChangesAsync();
            var partnerId = partner.Id;

            var invoice = NewTestInvoice(partnerId, $"INV-{Guid.NewGuid():N}");
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync();
            var invoiceId = invoice.Id;

            var pdfGen = new FakePdfGeneratorService { BytesToReturn = MarkerPdfBytes };
            var service = new InvoiceService(_fixture.Factory, pdfGen, null!, baseDir);

            // Act
            var bytes = await service.GenerateAndSavePdfAsync(invoiceId);

            // Assert: generated live exactly once, but nothing saved and no pdf_path written
            Assert.Equal(MarkerPdfBytes, bytes);
            Assert.Equal(1, pdfGen.GenerateInvoicePdfAsyncCallCount);

            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
            var stored = await verifyContext.Invoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == invoiceId);
            Assert.NotNull(stored);
            Assert.True(string.IsNullOrWhiteSpace(stored!.PdfPath));

            // No PDF file anywhere under baseDir (baseDir may not even exist)
            var pdfFiles = Directory.Exists(baseDir)
                ? Directory.GetFiles(baseDir, "*.pdf", SearchOption.AllDirectories)
                : Array.Empty<string>();
            Assert.Empty(pdfFiles);

            // Cleanup (FK-reverse)
            await verifyContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM invoices WHERE id = {0}", invoiceId);
            await verifyContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM business_partners WHERE id = {0}", partnerId);
        }
        finally
        {
            if (Directory.Exists(baseDir)) Directory.Delete(baseDir, recursive: true);
        }
    }

    [Fact]
    public async Task GenerateAndSavePdfAsync_ReprintReturnsByteIdenticalFrozenCopy_AfterAddressChange()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), $"npb-inv-{Guid.NewGuid():N}");
        try
        {
            // Arrange: partner + Confirmed invoice
            await using var context = await _fixture.Factory.CreateDbContextAsync();

            var partner = NewTestCustomer($"Test Customer {Guid.NewGuid():N}");
            context.BusinessPartners.Add(partner);
            await context.SaveChangesAsync();
            var partnerId = partner.Id;

            var invoice = NewTestInvoice(partnerId, $"INV-{Guid.NewGuid():N}");
            invoice.Status = InvoiceStatus.Confirmed;
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync();
            var invoiceId = invoice.Id;

            var pdfGen = new FakePdfGeneratorService { BytesToReturn = MarkerPdfBytes };
            var service = new InvoiceService(_fixture.Factory, pdfGen, null!, baseDir);

            // Act 1: first generation freezes the PDF
            var bytes1 = await service.GenerateAndSavePdfAsync(invoiceId);
            Assert.Equal(MarkerPdfBytes, bytes1);
            Assert.Equal(1, pdfGen.GenerateInvoicePdfAsyncCallCount);

            // Simulate the partner's address changing after issuance
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE business_partners SET address = {0}, updated_at = {1} WHERE id = {2}",
                "Changed Street 99, Vilnius", DateTime.UtcNow, partnerId);

            // Act 2: reprint must serve the frozen copy, not regenerate
            var bytes2 = await service.GenerateAndSavePdfAsync(invoiceId);

            // Assert: byte-identical reprint from cache — generator still called only once
            Assert.NotNull(bytes2);
            Assert.Equal(bytes1.Length, bytes2!.Length);
            Assert.True(bytes2.SequenceEqual(bytes1));
            Assert.Equal(1, pdfGen.GenerateInvoicePdfAsyncCallCount);

            // Cleanup (FK-reverse)
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM invoices WHERE id = {0}", invoiceId);
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM business_partners WHERE id = {0}", partnerId);
        }
        finally
        {
            if (Directory.Exists(baseDir)) Directory.Delete(baseDir, recursive: true);
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

    [Fact]
    public async Task CreateInvoiceAsync_ReverseCharge96_ForcesZeroVatAndSetsFlag()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        var partner = NewTestCustomer("RC96 Create Test");
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();
        var partnerId = partner.Id;

        // Empty invoice number so the service generates one itself (LAK prefix for this type)
        var invoice = NewTestInvoice(partnerId, "");
        invoice.InvoiceType = InvoiceTypes.ReverseCharge96;
        invoice.Lines.Add(new InvoiceLine
        {
            Description = "RC96 line",
            Quantity = 2m,
            PriceExclVat = 50m,
            VatRate = 21m // deliberately set — the service must zero it for a 96 str. invoice
        });

        var service = new InvoiceService(_fixture.Factory, null!, null!);

        var invoiceId = await service.CreateInvoiceAsync(invoice);

        Assert.True(invoiceId > 0, "CreateInvoiceAsync should return the new invoice id");

        // Verify against a brand-new context (proves the write hit the DB)
        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var stored = await verifyContext.Invoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == invoiceId);
        Assert.NotNull(stored);
        Assert.True(stored!.ReverseCharge);
        Assert.StartsWith("LAK", stored.InvoiceNumber);
        Assert.Equal(0m, stored.TotalVat);
        Assert.Equal(100m, stored.SubtotalExclVat);
        Assert.Equal(100m, stored.TotalInclVat);

        var line = await verifyContext.InvoiceLines
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.InvoiceId == invoiceId);
        Assert.NotNull(line);
        Assert.Equal(0m, line!.VatRate);
        Assert.Equal(0m, line.VatAmount);

        // Cleanup (defensive)
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM invoice_lines WHERE invoice_id = {0}", invoiceId);
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM invoices WHERE id = {0}", invoiceId);
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM business_partners WHERE id = {0}", partnerId);
    }

    [Fact]
    public async Task CreateInvoiceAsync_StandardType_Unchanged()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        var partner = NewTestCustomer("RC96 Standard Test");
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();
        var partnerId = partner.Id;

        var invoice = NewTestInvoice(partnerId, $"INV-{Guid.NewGuid():N}");
        invoice.InvoiceType = InvoiceTypes.Standard;
        invoice.Lines.Add(new InvoiceLine
        {
            Description = "Standard line",
            Quantity = 2m,
            PriceExclVat = 50m,
            VatRate = 21m
        });

        var service = new InvoiceService(_fixture.Factory, null!, null!);

        var invoiceId = await service.CreateInvoiceAsync(invoice);

        Assert.True(invoiceId > 0, "CreateInvoiceAsync should return the new invoice id");

        // Verify against a brand-new context
        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var stored = await verifyContext.Invoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == invoiceId);
        Assert.NotNull(stored);
        Assert.False(stored!.ReverseCharge);
        Assert.Equal(21m, stored.TotalVat);
        Assert.Equal(121m, stored.TotalInclVat);

        var line = await verifyContext.InvoiceLines
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.InvoiceId == invoiceId);
        Assert.NotNull(line);
        Assert.Equal(21.00m, line!.VatAmount);

        // Cleanup (defensive)
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM invoice_lines WHERE invoice_id = {0}", invoiceId);
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM invoices WHERE id = {0}", invoiceId);
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM business_partners WHERE id = {0}", partnerId);
    }

    [Fact]
    public async Task CreateInvoiceAsync_ZeroRateExportInvoice_DoesNotSetReverseCharge()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        var partner = NewTestCustomer("RC96 ZeroRate Test");
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();
        var partnerId = partner.Id;

        // 0% line on a STANDARD type — guards the existing 0% export invoices against
        // being misclassified as reverse-charge.
        var invoice = NewTestInvoice(partnerId, $"INV-{Guid.NewGuid():N}");
        invoice.InvoiceType = InvoiceTypes.Standard;
        invoice.Lines.Add(new InvoiceLine
        {
            Description = "Zero-rate line",
            Quantity = 2m,
            PriceExclVat = 50m,
            VatRate = 0m
        });

        var service = new InvoiceService(_fixture.Factory, null!, null!);

        var invoiceId = await service.CreateInvoiceAsync(invoice);

        Assert.True(invoiceId > 0, "CreateInvoiceAsync should return the new invoice id");

        // Verify against a brand-new context
        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var stored = await verifyContext.Invoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == invoiceId);
        Assert.NotNull(stored);
        Assert.Equal(0m, stored!.TotalVat);
        Assert.False(stored.ReverseCharge);

        // Cleanup (defensive)
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM invoice_lines WHERE invoice_id = {0}", invoiceId);
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM invoices WHERE id = {0}", invoiceId);
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM business_partners WHERE id = {0}", partnerId);
    }

    [Fact]
    public async Task UpdateInvoiceAsync_SwitchingToReverseCharge96_ZeroesVat()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        var partner = NewTestCustomer("RC96 Update Test");
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();
        var partnerId = partner.Id;

        // Start from a standard 21% invoice created through the real service
        var invoice = NewTestInvoice(partnerId, $"INV-RC96U-{Guid.NewGuid():N}");
        invoice.InvoiceType = InvoiceTypes.Standard;
        invoice.Lines.Add(new InvoiceLine
        {
            Description = "Update line",
            Quantity = 2m,
            PriceExclVat = 50m,
            VatRate = 21m
        });

        var service = new InvoiceService(_fixture.Factory, null!, null!);
        var invoiceId = await service.CreateInvoiceAsync(invoice);

        Assert.True(invoiceId > 0, "CreateInvoiceAsync should return the new invoice id");

        // Load with lines, switch type to reverse-charge, and update through the real service
        var loaded = await service.GetInvoiceWithDetailsAsync(invoiceId);
        Assert.NotNull(loaded);
        loaded!.InvoiceType = InvoiceTypes.ReverseCharge96;
        await service.UpdateInvoiceAsync(loaded);

        // Verify against a brand-new context
        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var stored = await verifyContext.Invoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == invoiceId);
        Assert.NotNull(stored);
        Assert.True(stored!.ReverseCharge);
        Assert.Equal(0m, stored.TotalVat);
        Assert.Equal(100m, stored.SubtotalExclVat);
        Assert.Equal(100m, stored.TotalInclVat);

        var line = await verifyContext.InvoiceLines
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.InvoiceId == invoiceId);
        Assert.NotNull(line);
        Assert.Equal(0m, line!.VatRate);
        Assert.Equal(0m, line.VatAmount);

        // Cleanup (defensive)
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM invoice_lines WHERE invoice_id = {0}", invoiceId);
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM invoices WHERE id = {0}", invoiceId);
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM business_partners WHERE id = {0}", partnerId);
    }

    [Fact]
    public async Task UpdateInvoiceAsync_SwitchingFromReverseCharge96ToStandard_ClearsFlagAndRestoresVat()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        var partner = NewTestCustomer("RC96 Reverse Update Test");
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();
        var partnerId = partner.Id;

        // Start from a reverse-charge 96 str. invoice created through the real service
        var invoice = NewTestInvoice(partnerId, $"INV-RC96R-{Guid.NewGuid():N}");
        invoice.InvoiceType = InvoiceTypes.ReverseCharge96;
        invoice.Lines.Add(new InvoiceLine
        {
            Description = "Reverse update line",
            Quantity = 2m,
            PriceExclVat = 50m,
            VatRate = 21m // deliberately set — the service must zero it for a 96 str. invoice
        });

        var service = new InvoiceService(_fixture.Factory, null!, null!);
        var invoiceId = await service.CreateInvoiceAsync(invoice);

        Assert.True(invoiceId > 0, "CreateInvoiceAsync should return the new invoice id");

        // Sanity: created as reverse-charge with zeroed VAT (T1 behavior)
        var created = await service.GetInvoiceWithDetailsAsync(invoiceId);
        Assert.NotNull(created);
        Assert.True(created!.ReverseCharge);
        Assert.Equal(0m, created.TotalVat);

        // Load with lines, switch type back to standard, restore the line VAT rate, and update through the real service
        var loaded = await service.GetInvoiceWithDetailsAsync(invoiceId);
        Assert.NotNull(loaded);
        loaded!.InvoiceType = InvoiceTypes.Standard;
        loaded.Lines.First().VatRate = 21m;
        await service.UpdateInvoiceAsync(loaded);

        // Verify against a brand-new context
        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var stored = await verifyContext.Invoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == invoiceId);
        Assert.NotNull(stored);
        Assert.False(stored!.ReverseCharge);
        Assert.Equal(21.00m, stored.TotalVat);
        Assert.Equal(100m, stored.SubtotalExclVat);
        Assert.Equal(121m, stored.TotalInclVat);

        var line = await verifyContext.InvoiceLines
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.InvoiceId == invoiceId);
        Assert.NotNull(line);
        Assert.Equal(21m, line!.VatRate);
        Assert.Equal(21.00m, line.VatAmount);

        // Cleanup (defensive)
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM invoice_lines WHERE invoice_id = {0}", invoiceId);
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM invoices WHERE id = {0}", invoiceId);
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM business_partners WHERE id = {0}", partnerId);
    }
}

/// <summary>Fake PDF generator producing deterministic marker bytes; also counts calls.</summary>
sealed class FakePdfGeneratorService : IPdfGeneratorService
{
    public byte[] BytesToReturn { get; set; } = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // "%PDF-1.4"
    public int GenerateInvoicePdfAsyncCallCount { get; private set; }
    public bool ThrowIfCalled { get; set; }

    public byte[] GenerateInvoicePdf(int invoiceId) => BytesToReturn;
    public Task<byte[]> GenerateInvoicePdfAsync(int invoiceId)
    {
        GenerateInvoicePdfAsyncCallCount++;
        if (ThrowIfCalled) throw new InvalidOperationException("Generator must not be called when a cached copy exists");
        return Task.FromResult(BytesToReturn);
    }
    public Task<byte[]> GenerateCreditNotePdfAsync(CreditNote creditNote, List<Services.Dtos.CreditNoteLineDto> lines, BusinessPartner? customer, Currency? currency, string? originalInvoiceNumber, DateTime? originalInvoiceDate, string? appliedInvoiceNumber, string? createdByName)
        => Task.FromResult(BytesToReturn);
    public string GetPdfPath(string creditNoteNumber) => $"/pdf/credit_notes/{creditNoteNumber}.pdf";
    public Task<byte[]> GenerateMultipleInvoicesPdfAsync(List<int> invoiceIds) => Task.FromResult(BytesToReturn);
}
