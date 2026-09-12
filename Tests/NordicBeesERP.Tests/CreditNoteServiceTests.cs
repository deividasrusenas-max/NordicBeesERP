using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.FileProviders;
using NordicBeesERP.Data;
using NordicBeesERP.Helpers;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
using NordicBeesERP.Services.Dtos;
using UglyToad.PdfPig;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// FROZEN.md behavior tests for CreditNoteService. These are integration
/// tests against the real nordic_bees_erp_test database (global
/// QueryTrackingBehavior.NoTracking, same as production) — they exist to
/// catch the exact bug class this codebase has hit repeatedly: a write
/// method that appears to succeed (no exception) but silently persists
/// zero rows.
/// </summary>
public class CreditNoteServiceTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public CreditNoteServiceTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SetPrintedAsync_PersistsStatusChangeToRealDatabase()
    {
        var now = DateTime.UtcNow;
        var invoiceNumber = $"INV-CN-{now.Ticks}";
        var creditNoteNumber = $"CN-{now.Ticks}";

        await using var setupContext = await _fixture.Factory.CreateDbContextAsync();

        // 1. Insert test currency via raw SQL
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO currencies (code, name, symbol, is_active) VALUES ({0}, {1}, {2}, {3})",
            "TST", "Test Currency", "T", 1);

        // 2. Insert business partner (customer) via EF Core model insert
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Customer,
            Name = $"Test Customer {now.Ticks}",
            Country = "Lithuania",
            CountryCode = "LT",
            DefaultLanguage = "LT",
            PaymentTermDays = 14,
            DefaultVatRate = 21m,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        setupContext.BusinessPartners.Add(partner);
        await setupContext.SaveChangesAsync();
        var bpId = partner.Id;

        // 3. Insert invoice via raw SQL
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO invoices (invoice_number, invoice_date, customer_id) VALUES ({0}, {1}, {2})",
            invoiceNumber, now.Date, bpId);

        // 4. Insert credit note via raw SQL with status = 'draft'
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO credit_notes (credit_note_number, credit_date, original_invoice_id, applied_invoice_id, customer_id, currency_id, language, reverse_charge, subtotal_excl_vat, total_vat, total_incl_vat, status, created_by, created_at, updated_at) VALUES ({0}, {1}, (SELECT id FROM invoices WHERE invoice_number = {2}), (SELECT id FROM invoices WHERE invoice_number = {3}), {4}, (SELECT id FROM currencies WHERE code = {5}), {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}, {14})",
            creditNoteNumber, now, invoiceNumber, invoiceNumber, bpId, "TST", "LT", false, 0m, 0m, 0m, "draft", 1, now, now);

        // Get the credit note ID by its unique number
        var creditNoteId = await setupContext.CreditNotes
            .FromSqlRaw("SELECT id FROM credit_notes WHERE credit_note_number = {0}", creditNoteNumber)
            .Select(cn => cn.Id)
            .FirstOrDefaultAsync();

        try
        {
            // 5. Act: call SetPrintedAsync (uses ExecuteSqlRawAsync internally)
            var service = new CreditNoteService(
                _fixture.Factory,
                new TestCreditNoteNumberGenerator(),
                new TestCompanySettingsService(),
                new TestPdfGeneratorService(),
                new TestPaymentService());
            await service.SetPrintedAsync(creditNoteId);

            // 6. Assert: read status back via raw SQL (string enum value in DB)
            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
            var status = await verifyContext.CreditNotes
                .FromSqlRaw("SELECT status FROM credit_notes WHERE id = {0}", creditNoteId)
                .Select(cn => cn.Status)
                .FirstOrDefaultAsync();

            Assert.Equal(CreditNoteStatus.Printed, status);
        }
        finally
        {
            // 7. Cleanup in reverse FK order
            await using var cleanupContext = await _fixture.Factory.CreateDbContextAsync();
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM credit_notes WHERE credit_note_number = {0}", creditNoteNumber);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM invoices WHERE invoice_number = {0}", invoiceNumber);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", bpId);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM currencies WHERE code = {0}", "TST");
        }
    }

    [Fact]
    public async Task CreateCreditNoteAsync_NullAppliedInvoiceId_PersistsOriginalInvoiceId()
    {
        var now = DateTime.UtcNow;
        var invoiceNumber = $"INV-CNCREATE-{now.Ticks}";

        await using var setupContext = await _fixture.Factory.CreateDbContextAsync();

        // 1. Insert test currency via raw SQL
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO currencies (code, name, symbol, is_active) VALUES ({0}, {1}, {2}, {3})",
            "TST", "Test Currency", "T", 1);

        // 2. Insert business partner (customer) via EF Core model insert
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Customer,
            Name = $"Test Customer Create {now.Ticks}",
            Country = "Lithuania",
            CountryCode = "LT",
            DefaultLanguage = "LT",
            PaymentTermDays = 14,
            DefaultVatRate = 21m,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        setupContext.BusinessPartners.Add(partner);
        await setupContext.SaveChangesAsync();
        var bpId = partner.Id;

        // 3. Insert invoice via raw SQL
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO invoices (invoice_number, invoice_date, customer_id) VALUES ({0}, {1}, {2})",
            invoiceNumber, now.Date, bpId);

        var invoiceId = await setupContext.Invoices
            .FromSqlRaw("SELECT id FROM invoices WHERE invoice_number = {0}", invoiceNumber)
            .Select(i => i.Id)
            .FirstOrDefaultAsync();

        var creditNoteNumber = "";

        try
        {
            // 4. Act: call the REAL service create path WITHOUT setting AppliedInvoiceId
            var service = new CreditNoteService(
                _fixture.Factory,
                new TestCreditNoteNumberGenerator(),
                new TestCompanySettingsService(),
                new TestPdfGeneratorService(),
                new TestPaymentService());

            var creditNote = await service.CreateCreditNoteAsync(new CreateCreditNoteRequest
            {
                OriginalInvoiceId = invoiceId,
                CreditDate = now,
                Language = "LT",
                Lines = new List<CreditNoteLineRequest>()
            }, 1);

            creditNoteNumber = creditNote.CreditNoteNumber;

            Assert.NotNull(creditNote);
            Assert.False(string.IsNullOrEmpty(creditNote.CreditNoteNumber));

            // 5. Assert: round-trip read with a BRAND NEW DbContext — both id columns must equal the original invoice id
            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
            var stored = await verifyContext.CreditNotes
                .FromSqlRaw("SELECT id, original_invoice_id, applied_invoice_id FROM credit_notes WHERE credit_note_number = {0}", creditNote.CreditNoteNumber)
                .Select(cn => new { cn.OriginalInvoiceId, cn.AppliedInvoiceId })
                .FirstOrDefaultAsync();

            Assert.NotNull(stored);
            Assert.Equal(invoiceId, stored!.OriginalInvoiceId);
            Assert.Equal(invoiceId, stored.AppliedInvoiceId);
        }
        finally
        {
            // 6. Cleanup in reverse FK order
            await using var cleanupContext = await _fixture.Factory.CreateDbContextAsync();
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM credit_notes WHERE credit_note_number = {0}", creditNoteNumber);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM invoices WHERE invoice_number = {0}", invoiceNumber);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", bpId);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM currencies WHERE code = {0}", "TST");
        }
    }

    [Fact]
    public async Task UpdateCreditNoteAsync_NullOriginalInvoiceId_PreservesExistingInvoiceId()
    {
        var now = DateTime.UtcNow;
        var invoiceNumber = $"INV-CNUPD-{now.Ticks}";
        var creditNoteNumber = $"CN-UPD-{now.Ticks}";

        await using var setupContext = await _fixture.Factory.CreateDbContextAsync();

        // 1. Insert test currency via raw SQL
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO currencies (code, name, symbol, is_active) VALUES ({0}, {1}, {2}, {3})",
            "TST", "Test Currency", "T", 1);

        // 2. Insert business partner (customer) via EF Core model insert
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Customer,
            Name = $"Test Customer Update {now.Ticks}",
            Country = "Lithuania",
            CountryCode = "LT",
            DefaultLanguage = "LT",
            PaymentTermDays = 14,
            DefaultVatRate = 21m,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        setupContext.BusinessPartners.Add(partner);
        await setupContext.SaveChangesAsync();
        var bpId = partner.Id;

        // 3. Insert invoice via raw SQL
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO invoices (invoice_number, invoice_date, customer_id) VALUES ({0}, {1}, {2})",
            invoiceNumber, now.Date, bpId);

        var invoiceId = await setupContext.Invoices
            .FromSqlRaw("SELECT id FROM invoices WHERE invoice_number = {0}", invoiceNumber)
            .Select(i => i.Id)
            .FirstOrDefaultAsync();

        // 4. Insert a DRAFT credit note linked to that invoice via raw SQL,
        //    with original_invoice_id set (the value the fallback must preserve)
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO credit_notes (credit_note_number, credit_date, original_invoice_id, applied_invoice_id, customer_id, currency_id, language, reverse_charge, subtotal_excl_vat, total_vat, total_incl_vat, status, created_by, created_at, updated_at) VALUES ({0}, {1}, (SELECT id FROM invoices WHERE invoice_number = {2}), (SELECT id FROM invoices WHERE invoice_number = {3}), {4}, (SELECT id FROM currencies WHERE code = {5}), {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}, {14})",
            creditNoteNumber, now, invoiceNumber, invoiceNumber, bpId, "TST", "LT", false, 0m, 0m, 0m, "draft", 1, now, now);

        var creditNoteId = await setupContext.CreditNotes
            .FromSqlRaw("SELECT id FROM credit_notes WHERE credit_note_number = {0}", creditNoteNumber)
            .Select(cn => cn.Id)
            .FirstOrDefaultAsync();

        try
        {
            // 5. Act: call the REAL service update path WITHOUT setting OriginalInvoiceId,
            //    so request.OriginalInvoiceId is null and the defensive fallback must kick in
            var service = new CreditNoteService(
                _fixture.Factory,
                new TestCreditNoteNumberGenerator(),
                new TestCompanySettingsService(),
                new TestPdfGeneratorService(),
                new TestPaymentService());

            await service.UpdateCreditNoteAsync(new UpdateCreditNoteRequest
            {
                Id = creditNoteId,
                CreditDate = now,
                Language = "LT"
                // NOTE: OriginalInvoiceId is deliberately NOT set -> stays null (int?)
            }, 1);

            // 6. Assert: read back with a BRAND NEW DbContext — original_invoice_id must
            //    still point at the original invoice (non-null preserved, not overwritten)
            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
            var stored = await verifyContext.CreditNotes
                .FromSqlRaw("SELECT original_invoice_id FROM credit_notes WHERE id = {0}", creditNoteId)
                .Select(cn => cn.OriginalInvoiceId)
                .FirstOrDefaultAsync();

            Assert.NotNull(stored);
            Assert.Equal(invoiceId, stored);
        }
        finally
        {
            // 7. Cleanup in reverse FK order
            await using var cleanupContext = await _fixture.Factory.CreateDbContextAsync();
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM credit_notes WHERE credit_note_number = {0}", creditNoteNumber);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM invoices WHERE invoice_number = {0}", invoiceNumber);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", bpId);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM currencies WHERE code = {0}", "TST");
        }
    }

    [Fact]
    public async Task CreateCreditNoteAsync_FullyCreditedInvoice_BecomesPaidWhilePaidAmountStaysCashOnly()
    {
        var now = DateTime.UtcNow;
        var invoiceNumber = $"INV-CNSETTLE-{now.Ticks}";

        await using var setupContext = await _fixture.Factory.CreateDbContextAsync();

        // 1. Insert test currency via raw SQL
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO currencies (code, name, symbol, is_active) VALUES ({0}, {1}, {2}, {3})",
            "TST", "Test Currency", "T", 1);

        // 2. Insert business partner (customer) via EF Core model insert
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Customer,
            Name = $"Test Customer Settle {now.Ticks}",
            Country = "Lithuania",
            CountryCode = "LT",
            DefaultLanguage = "LT",
            PaymentTermDays = 14,
            DefaultVatRate = 21m,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        setupContext.BusinessPartners.Add(partner);
        await setupContext.SaveChangesAsync();
        var bpId = partner.Id;

        // 3. Insert invoice via raw SQL: fully unpaid (paid_amount = 0), total_incl_vat = 121
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO invoices (invoice_number, invoice_date, customer_id, subtotal_excl_vat, total_vat, total_incl_vat, paid_amount, payment_status) VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7})",
            invoiceNumber, now.Date, bpId, 100m, 21m, 121m, 0m, "unpaid");

        var invoiceId = await setupContext.Invoices
            .FromSqlRaw("SELECT id FROM invoices WHERE invoice_number = {0}", invoiceNumber)
            .Select(i => i.Id)
            .FirstOrDefaultAsync();

        // 4. Insert an invoice line for that invoice (the line the credit note will fully cover)
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO invoice_lines (invoice_id, line_number, description, quantity, unit, price_excl_vat, vat_rate, line_subtotal, vat_amount, line_total, created_at) VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10})",
            invoiceId, 1, "Test product for settlement", 1m, "vnt", 100m, 21m, 100m, 21m, 121m, now);

        var invoiceLineId = await setupContext.InvoiceLines
            .FromSqlRaw("SELECT id FROM invoice_lines WHERE invoice_id = {0}", invoiceId)
            .Select(l => l.Id)
            .FirstOrDefaultAsync();

        var creditNoteNumber = "";

        try
        {
            // 5. Act: build the service with a REAL payment backend so the
            //    RecalculateInvoiceStatusAsync call actually runs against the test DB.
            var realPaymentService = new PaymentService(_fixture.Factory);
            var service = new CreditNoteService(
                _fixture.Factory,
                new TestCreditNoteNumberGenerator(),
                new TestCompanySettingsService(),
                new TestPdfGeneratorService(),
                new TestPaymentService(realPaymentService));

            var creditNote = await service.CreateCreditNoteAsync(new CreateCreditNoteRequest
            {
                OriginalInvoiceId = invoiceId,
                CreditDate = now,
                Language = "LT",
                Lines = new List<CreditNoteLineRequest> { new CreditNoteLineRequest { InvoiceLineId = invoiceLineId, Quantity = 1 } }
            }, 1);

            creditNoteNumber = creditNote.CreditNoteNumber;

            Assert.NotNull(creditNote);
            Assert.False(string.IsNullOrEmpty(creditNote.CreditNoteNumber));

            // 6. Assert: read the ORIGINAL invoice back with a BRAND NEW DbContext —
            //    payment_status must be "paid" (fully credited) while paid_amount stays 0
            //    (real cash only, no allocations).
            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
            var stored = await verifyContext.Invoices
                .FromSqlRaw("SELECT payment_status, paid_amount FROM invoices WHERE id = {0}", invoiceId)
                .Select(i => new { i.PaymentStatus, i.PaidAmount })
                .FirstOrDefaultAsync();

            Assert.NotNull(stored);
            Assert.Equal("paid", stored!.PaymentStatus);
            Assert.Equal(0m, stored.PaidAmount);
        }
        finally
        {
            // 7. Cleanup in reverse FK order
            await using var cleanupContext = await _fixture.Factory.CreateDbContextAsync();
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM credit_note_lines WHERE credit_note_id IN (SELECT id FROM credit_notes WHERE credit_note_number = {0})", creditNoteNumber);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM credit_notes WHERE credit_note_number = {0}", creditNoteNumber);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM invoice_lines WHERE invoice_id = {0}", invoiceId);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM invoices WHERE invoice_number = {0}", invoiceNumber);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", bpId);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM currencies WHERE code = {0}", "TST");
        }
    }

    [Fact]
    public async Task GenerateAndSavePdfAsync_PrintedCreditNote_SavesPdfAndWritesPdfPathToRealDatabase()
    {
        var now = DateTime.UtcNow;
        var invoiceNumber = $"INV-{Guid.NewGuid():N}";
        var creditNoteNumber = $"CN-{Guid.NewGuid():N}";
        var baseDir = Path.Combine(Path.GetTempPath(), $"npb-cn-{Guid.NewGuid():N}");
        if (Directory.Exists(baseDir)) Directory.Delete(baseDir, recursive: true);

        await using var setupContext = await _fixture.Factory.CreateDbContextAsync();

        // 1. Insert test currency via raw SQL
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO currencies (code, name, symbol, is_active) VALUES ({0}, {1}, {2}, {3})",
            "TST", "Test Currency", "T", 1);

        // 2. Insert business partner (customer) via EF Core model insert
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Customer,
            Name = $"Test Customer {Guid.NewGuid():N}",
            Country = "Lithuania",
            CountryCode = "LT",
            DefaultLanguage = "LT",
            PaymentTermDays = 14,
            DefaultVatRate = 21m,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        setupContext.BusinessPartners.Add(partner);
        await setupContext.SaveChangesAsync();
        var bpId = partner.Id;

        // 3. Insert invoice via raw SQL
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO invoices (invoice_number, invoice_date, customer_id) VALUES ({0}, {1}, {2})",
            invoiceNumber, now.Date, bpId);

        // 4. Insert credit note via raw SQL with status = 'printed'
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO credit_notes (credit_note_number, credit_date, original_invoice_id, applied_invoice_id, customer_id, currency_id, language, reverse_charge, subtotal_excl_vat, total_vat, total_incl_vat, status, created_by, created_at, updated_at) VALUES ({0}, {1}, (SELECT id FROM invoices WHERE invoice_number = {2}), (SELECT id FROM invoices WHERE invoice_number = {3}), {4}, (SELECT id FROM currencies WHERE code = {5}), {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}, {14})",
            creditNoteNumber, now, invoiceNumber, invoiceNumber, bpId, "TST", "LT", false, 0m, 0m, 0m, "printed", 1, now, now);

        var creditNoteId = await setupContext.CreditNotes
            .FromSqlRaw("SELECT id FROM credit_notes WHERE credit_note_number = {0}", creditNoteNumber)
            .Select(cn => cn.Id)
            .FirstOrDefaultAsync();

        try
        {
            // 5. Act
            var pdfGen = new TestPdfGeneratorService();
            var service = new CreditNoteService(
                _fixture.Factory,
                new TestCreditNoteNumberGenerator(),
                new TestCompanySettingsService(),
                pdfGen,
                new TestPaymentService(),
                baseDir);
            var pdfBytes = await service.GenerateAndSavePdfAsync(creditNoteId);

            // 6. Assert: returned bytes are the marker bytes
            Assert.Equal(pdfGen.PdfBytes, pdfBytes);

            // 7. Re-read with a BRAND NEW DbContext: PdfPath is set and year-prefixed
            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
            var stored = await verifyContext.CreditNotes
                .AsNoTracking()
                .FirstOrDefaultAsync(cn => cn.Id == creditNoteId);
            Assert.NotNull(stored);
            Assert.NotNull(stored!.PdfPath);
            Assert.StartsWith($"{stored.CreditDate.Year}/", stored.PdfPath!);

            // 8. The file exists on disk under baseDir and matches the marker bytes
            var fullPath = Path.Combine(baseDir, stored.PdfPath!);
            Assert.True(File.Exists(fullPath));
            Assert.Equal(pdfGen.PdfBytes, File.ReadAllBytes(fullPath));
        }
        finally
        {
            // 9. Cleanup in reverse FK order
            await using var cleanupContext = await _fixture.Factory.CreateDbContextAsync();
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM credit_note_lines WHERE credit_note_id IN (SELECT id FROM credit_notes WHERE credit_note_number = {0})", creditNoteNumber);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM credit_notes WHERE credit_note_number = {0}", creditNoteNumber);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM invoices WHERE invoice_number = {0}", invoiceNumber);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", bpId);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM currencies WHERE code = {0}", "TST");
            if (Directory.Exists(baseDir)) Directory.Delete(baseDir, recursive: true);
        }
    }

    [Fact]
    public async Task GenerateAndSavePdfAsync_ServesCachedCopy_WhenPdfPathAndFileExist()
    {
        var now = DateTime.UtcNow;
        var invoiceNumber = $"INV-{Guid.NewGuid():N}";
        var creditNoteNumber = $"CN-{Guid.NewGuid():N}";
        var baseDir = Path.Combine(Path.GetTempPath(), $"npb-cn-{Guid.NewGuid():N}");
        if (Directory.Exists(baseDir)) Directory.Delete(baseDir, recursive: true);

        await using var setupContext = await _fixture.Factory.CreateDbContextAsync();

        // 1. Insert test currency via raw SQL
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO currencies (code, name, symbol, is_active) VALUES ({0}, {1}, {2}, {3})",
            "TST", "Test Currency", "T", 1);

        // 2. Insert business partner (customer) via EF Core model insert
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Customer,
            Name = $"Test Customer {Guid.NewGuid():N}",
            Country = "Lithuania",
            CountryCode = "LT",
            DefaultLanguage = "LT",
            PaymentTermDays = 14,
            DefaultVatRate = 21m,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        setupContext.BusinessPartners.Add(partner);
        await setupContext.SaveChangesAsync();
        var bpId = partner.Id;

        // 3. Insert invoice via raw SQL
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO invoices (invoice_number, invoice_date, customer_id) VALUES ({0}, {1}, {2})",
            invoiceNumber, now.Date, bpId);

        // 4. Insert credit note via raw SQL with status = 'printed'
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO credit_notes (credit_note_number, credit_date, original_invoice_id, applied_invoice_id, customer_id, currency_id, language, reverse_charge, subtotal_excl_vat, total_vat, total_incl_vat, status, created_by, created_at, updated_at) VALUES ({0}, {1}, (SELECT id FROM invoices WHERE invoice_number = {2}), (SELECT id FROM invoices WHERE invoice_number = {3}), {4}, (SELECT id FROM currencies WHERE code = {5}), {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}, {14})",
            creditNoteNumber, now, invoiceNumber, invoiceNumber, bpId, "TST", "LT", false, 0m, 0m, 0m, "printed", 1, now, now);

        var creditNoteId = await setupContext.CreditNotes
            .FromSqlRaw("SELECT id FROM credit_notes WHERE credit_note_number = {0}", creditNoteNumber)
            .Select(cn => cn.Id)
            .FirstOrDefaultAsync();

        try
        {
            // 5. Pre-create the cached file and record pdf_path on the row
            var markerBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // "%PDF-1.4"
            var rel = $"{now.Year}/X-{Guid.NewGuid():N}.pdf";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(baseDir, rel))!);
            File.WriteAllBytes(Path.Combine(baseDir, rel), markerBytes);
            await setupContext.Database.ExecuteSqlRawAsync(
                "UPDATE credit_notes SET pdf_path = {0} WHERE id = {1}", rel, creditNoteId);

            // 6. Act — generator must NOT be called because a cached copy exists
            var pdfGen = new TestPdfGeneratorService { ThrowIfCalled = true };
            var service = new CreditNoteService(
                _fixture.Factory,
                new TestCreditNoteNumberGenerator(),
                new TestCompanySettingsService(),
                pdfGen,
                new TestPaymentService(),
                baseDir);
            var pdfBytes = await service.GenerateAndSavePdfAsync(creditNoteId);

            // 7. Assert: served the cached copy without regenerating
            Assert.Equal(markerBytes, pdfBytes);
            Assert.Equal(0, pdfGen.GenerateCreditNotePdfAsyncCallCount);
        }
        finally
        {
            // 8. Cleanup in reverse FK order
            await using var cleanupContext = await _fixture.Factory.CreateDbContextAsync();
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM credit_note_lines WHERE credit_note_id IN (SELECT id FROM credit_notes WHERE credit_note_number = {0})", creditNoteNumber);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM credit_notes WHERE credit_note_number = {0}", creditNoteNumber);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM invoices WHERE invoice_number = {0}", invoiceNumber);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", bpId);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM currencies WHERE code = {0}", "TST");
            if (Directory.Exists(baseDir)) Directory.Delete(baseDir, recursive: true);
        }
    }

    [Fact]
    public async Task GenerateAndSavePdfAsync_DraftCreditNote_NeverSavesOrWritesPdfPath()
    {
        var now = DateTime.UtcNow;
        var invoiceNumber = $"INV-{Guid.NewGuid():N}";
        var creditNoteNumber = $"CN-{Guid.NewGuid():N}";
        var baseDir = Path.Combine(Path.GetTempPath(), $"npb-cn-{Guid.NewGuid():N}");
        if (Directory.Exists(baseDir)) Directory.Delete(baseDir, recursive: true);

        await using var setupContext = await _fixture.Factory.CreateDbContextAsync();

        // 1. Insert test currency via raw SQL
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO currencies (code, name, symbol, is_active) VALUES ({0}, {1}, {2}, {3})",
            "TST", "Test Currency", "T", 1);

        // 2. Insert business partner (customer) via EF Core model insert
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Customer,
            Name = $"Test Customer {Guid.NewGuid():N}",
            Country = "Lithuania",
            CountryCode = "LT",
            DefaultLanguage = "LT",
            PaymentTermDays = 14,
            DefaultVatRate = 21m,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        setupContext.BusinessPartners.Add(partner);
        await setupContext.SaveChangesAsync();
        var bpId = partner.Id;

        // 3. Insert invoice via raw SQL
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO invoices (invoice_number, invoice_date, customer_id) VALUES ({0}, {1}, {2})",
            invoiceNumber, now.Date, bpId);

        // 4. Insert credit note via raw SQL with status = 'draft'
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO credit_notes (credit_note_number, credit_date, original_invoice_id, applied_invoice_id, customer_id, currency_id, language, reverse_charge, subtotal_excl_vat, total_vat, total_incl_vat, status, created_by, created_at, updated_at) VALUES ({0}, {1}, (SELECT id FROM invoices WHERE invoice_number = {2}), (SELECT id FROM invoices WHERE invoice_number = {3}), {4}, (SELECT id FROM currencies WHERE code = {5}), {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}, {14})",
            creditNoteNumber, now, invoiceNumber, invoiceNumber, bpId, "TST", "LT", false, 0m, 0m, 0m, "draft", 1, now, now);

        var creditNoteId = await setupContext.CreditNotes
            .FromSqlRaw("SELECT id FROM credit_notes WHERE credit_note_number = {0}", creditNoteNumber)
            .Select(cn => cn.Id)
            .FirstOrDefaultAsync();

        try
        {
            // 5. Act
            var pdfGen = new TestPdfGeneratorService();
            var service = new CreditNoteService(
                _fixture.Factory,
                new TestCreditNoteNumberGenerator(),
                new TestCompanySettingsService(),
                pdfGen,
                new TestPaymentService(),
                baseDir);
            var pdfBytes = await service.GenerateAndSavePdfAsync(creditNoteId);

            // 6. Assert: generated live exactly once, but never saved / no pdf_path
            Assert.Equal(pdfGen.PdfBytes, pdfBytes);
            Assert.Equal(1, pdfGen.GenerateCreditNotePdfAsyncCallCount);

            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
            var stored = await verifyContext.CreditNotes
                .AsNoTracking()
                .FirstOrDefaultAsync(cn => cn.Id == creditNoteId);
            Assert.NotNull(stored);
            Assert.Null(stored!.PdfPath);

            // No *.pdf files exist anywhere under baseDir
            Assert.False(Directory.Exists(baseDir) && Directory.GetFiles(baseDir, "*.pdf", SearchOption.AllDirectories).Length > 0);
        }
        finally
        {
            // 7. Cleanup in reverse FK order
            await using var cleanupContext = await _fixture.Factory.CreateDbContextAsync();
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM credit_note_lines WHERE credit_note_id IN (SELECT id FROM credit_notes WHERE credit_note_number = {0})", creditNoteNumber);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM credit_notes WHERE credit_note_number = {0}", creditNoteNumber);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM invoices WHERE invoice_number = {0}", invoiceNumber);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", bpId);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM currencies WHERE code = {0}", "TST");
            if (Directory.Exists(baseDir)) Directory.Delete(baseDir, recursive: true);
        }
    }

    // ---------------------------------------------------------------
    // PDF rendering tests — real PdfGeneratorService + PdfPig text
    // extraction (mirrors Tests/PdfGeneratorServiceTests.cs). These seed
    // the credit note via direct INSERT because production never stores
    // reverse_charge = 1 on a credit note (hard-coded false at creation,
    // Services/CreditNoteService.cs:177) and CreateCreditNoteAsync throws
    // for a full-quantity RC96 credit — the PDF derives RC96 from the
    // ORIGINAL invoice instead.
    // ---------------------------------------------------------------

    private const string Rc96PayableLabelFragment = "apmokėjimui";
    private const string Rc96NoteStableFragment = "mechanizmas";
    private const string PdfOutputDir = "/tmp/rc96-verification";

    private static string ResolveRepoWebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "wwwroot", "logo.png")))
                return Path.Combine(dir.FullName, "wwwroot");
            dir = dir.Parent;
        }
        throw new InvalidOperationException($"Could not locate repo wwwroot from {AppContext.BaseDirectory}");
    }

    private static string ExtractNormalizedText(byte[] pdf)
    {
        using var doc = PdfDocument.Open(pdf);
        var text = string.Join(" ", doc.GetPages().Select(p => p.Text));
        return Regex.Replace(text, @"\s+", " ");
    }

    private static string NoSpaces(string s) => s.Replace(" ", "");

    private async Task<(bool Created, int SettingsId)> EnsureCompanySettingsAsync()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var existing = await context.CompanySettings.FirstOrDefaultAsync();
        if (existing != null)
            return (false, existing.Id);

        var settings = new CompanySettings
        {
            CompanyName = "Test Company UAB",
            CompanyCode = "TC-TEST-001",
            VatCode = "LT000000001",
            Address = "Test street 1",
            City = "Vilnius",
            PostalCode = "12345",
            Country = "Lietuva",
            CountryCode = "LT",
            BankName = "Test Bank",
            BankIban = "LT000000000000000001",
            BankSwift = "TESTLT2X",
            BankAccount = "123456",
            Email = "test@example.com",
            Phone = "+370 123 45678",
            DefaultVatRate = 21m,
            UpdatedAt = DateTime.UtcNow
        };
        context.CompanySettings.Add(settings);
        await context.SaveChangesAsync();
        return (true, settings.Id);
    }

    private async Task<int> SeedPdfCustomerAsync()
    {
        var now = DateTime.UtcNow;
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        // The credit note's currency_id is resolved via (SELECT id FROM currencies WHERE code='TST')
        // in SeedCreditNoteAsync — that subquery returns NULL without a 'TST' row, which then maps to
        // the non-nullable int CurrencyId and throws InvalidCastException. Insert it here (idempotent:
        // clear any leftover from a prior run first) so all three PDF tests have a currency to reference.
        await context.Database.ExecuteSqlRawAsync("DELETE FROM currencies WHERE code = {0}", "TST");
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO currencies (code, name, symbol, is_active) VALUES ({0}, {1}, {2}, {3})",
            "TST", "Test Currency", "T", 1);

        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Customer,
            Name = $"CN PDF Test Customer {now.Ticks}",
            Country = "Lithuania",
            CountryCode = "LT",
            DefaultLanguage = "LT",
            PaymentTermDays = 14,
            DefaultVatRate = 21m,
            IsActive = true,
            NationalIdNumber = "123456789",
            BankAccount = "123456789012",
            CreatedAt = now,
            UpdatedAt = now
        };
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();
        return partner.Id;
    }

    private async Task<int> SeedOriginalInvoiceAsync(int customerId, string invoiceNumber, DateTime date,
        string invoiceType, bool reverseCharge, decimal quantity, decimal price, decimal rate,
        decimal subtotal, decimal vat, decimal total)
    {
        var now = DateTime.UtcNow;
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO invoices (invoice_number, invoice_date, customer_id, language, invoice_type, reverse_charge, subtotal_excl_vat, total_vat, total_incl_vat) VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8})",
            invoiceNumber, date, customerId, "LT", invoiceType, reverseCharge ? 1 : 0, subtotal, vat, total);

        var invoiceId = await context.Invoices
            .FromSqlRaw("SELECT id FROM invoices WHERE invoice_number = {0}", invoiceNumber)
            .Select(i => i.Id)
            .FirstOrDefaultAsync();

        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO invoice_lines (invoice_id, line_number, description, quantity, unit, price_excl_vat, vat_rate, line_subtotal, vat_amount, line_total, created_at) VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10})",
            invoiceId, 1, "CN PDF verification line", quantity, "vnt", price, rate, subtotal, vat, total, now);

        return invoiceId;
    }

    private async Task<int> SeedCreditNoteAsync(int customerId, string creditNoteNumber, DateTime date, int originalInvoiceId,
        decimal subtotal, decimal vat, decimal total)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        // reverse_charge is hardcoded 0 in the SQL — production always stores 0 here;
        // the PDF derives RC96 from the ORIGINAL invoice.
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO credit_notes (credit_note_number, credit_date, original_invoice_id, applied_invoice_id, customer_id, currency_id, language, reverse_charge, subtotal_excl_vat, total_vat, total_incl_vat, status, created_by, created_at, updated_at) VALUES ({0}, {1}, {2}, {3}, {4}, (SELECT id FROM currencies WHERE code = {5}), {6}, 0, {7}, {8}, {9}, 'printed', 1, {10}, {11})",
            creditNoteNumber, date, originalInvoiceId, originalInvoiceId, customerId, "TST", "LT", subtotal, vat, total, date, date);

        return await context.CreditNotes
            .FromSqlRaw("SELECT id FROM credit_notes WHERE credit_note_number = {0}", creditNoteNumber)
            .Select(cn => cn.Id)
            .FirstOrDefaultAsync();
    }

    private async Task SeedCreditNoteLineAsync(int creditNoteId, decimal subtotal, decimal vat, decimal total, decimal rate)
    {
        var now = DateTime.UtcNow;
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO credit_note_lines (credit_note_id, line_number, description, quantity, unit, price_excl_vat, vat_rate, line_subtotal, vat_amount, line_total, created_at) VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10})",
            creditNoteId, 1, "CN PDF verification line", 1m, "vnt", subtotal, rate, subtotal, vat, total, now);
    }

    private async Task CleanupPdfSeedAsync(int partnerId, int invoiceId, string creditNoteNumber, bool settingsCreated, int settingsId)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        if (creditNoteNumber.Length > 0)
            await context.Database.ExecuteSqlRawAsync("DELETE FROM credit_note_lines WHERE credit_note_id IN (SELECT id FROM credit_notes WHERE credit_note_number = {0})", creditNoteNumber);
        if (creditNoteNumber.Length > 0)
            await context.Database.ExecuteSqlRawAsync("DELETE FROM credit_notes WHERE credit_note_number = {0}", creditNoteNumber);
        if (invoiceId > 0)
            await context.Database.ExecuteSqlRawAsync("DELETE FROM invoice_lines WHERE invoice_id = {0}", invoiceId);
        if (invoiceId > 0)
            await context.Database.ExecuteSqlRawAsync("DELETE FROM invoices WHERE id = {0}", invoiceId);
        if (partnerId > 0)
            await context.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", partnerId);
        // Remove the 'TST' currency created in SeedPdfCustomerAsync so no leftover row collides
        // with other tests in this class that insert/use their own 'TST' currency (a second row would
        // make the (SELECT id FROM currencies WHERE code='TST') subquery return >1 row).
        await context.Database.ExecuteSqlRawAsync("DELETE FROM currencies WHERE code = {0}", "TST");
        if (settingsCreated)
            await context.Database.ExecuteSqlRawAsync("DELETE FROM company_settings WHERE id = {0}", settingsId);
    }

    [Fact]
    public async Task GenerateCreditNotePdf_Rc96_ShowsDerivedVatAndPayableLabel()
    {
        var now = DateTime.UtcNow;
        var invoiceNumber = $"INV-CNRC96-{now.Ticks}";
        var creditNoteNumber = $"CN-RC96-{now.Ticks}";

        var (settingsCreated, settingsId) = await EnsureCompanySettingsAsync();
        int partnerId = 0;
        int invoiceId = 0;
        try
        {
            partnerId = await SeedPdfCustomerAsync();

            // Original RC96 invoice: stored line 66.00 / VAT 0.00 / total 66.00 (VAT zeroed)
            invoiceId = await SeedOriginalInvoiceAsync(partnerId, invoiceNumber, now.Date,
                InvoiceTypes.ReverseCharge96, true, 660m, 0.10m, 21m, 66m, 0m, 66m);

            // Credit note seeded directly: reverse_charge = 0 (production always stores 0),
            // line mirrors the STORED values; PDF must derive RC96 from the original invoice.
            var creditNoteId = await SeedCreditNoteAsync(partnerId, creditNoteNumber, now, invoiceId, 66m, 0m, 66m);
            await SeedCreditNoteLineAsync(creditNoteId, 66m, 0m, 66m, 21m);

            Directory.CreateDirectory(PdfOutputDir);
            var env = new StubWebHostEnvironment { WebRootPath = ResolveRepoWebRoot() };
            var companySettings = new CompanySettingsService(_fixture.Factory);
            var service = new PdfGeneratorService(_fixture.Factory, companySettings, env);

            await using var genContext = await _fixture.Factory.CreateDbContextAsync();
            // Match the production load path (CreditNoteService.cs:699-702) which includes
            // OriginalInvoice — the PDF derives RC96 from it, so it must be populated here too.
            var creditNote = await genContext.CreditNotes.AsNoTracking()
                .Include(cn => cn.OriginalInvoice)
                .FirstOrDefaultAsync(cn => cn.Id == creditNoteId);
            Assert.NotNull(creditNote);
            var lines = new List<CreditNoteLineDto>
            {
                new CreditNoteLineDto
                {
                    Id = 0,
                    CreditNoteId = creditNoteId,
                    LineNumber = 1,
                    Description = "CN PDF verification line",
                    Quantity = 660m,
                    Unit = "vnt",
                    PriceExclVat = 0.10m,
                    VatRate = 21m,
                    LineSubtotal = 66m,
                    VatAmount = 0m,
                    LineTotal = 66m
                }
            };
            var customer = await genContext.BusinessPartners.AsNoTracking().FirstOrDefaultAsync(bp => bp.Id == partnerId);
            Assert.NotNull(customer);
            var currency = await genContext.Currencies.AsNoTracking().FirstOrDefaultAsync(c => c.Code == "TST");

            var pdf = await service.GenerateCreditNotePdfAsync(
                creditNote!, lines, customer, currency, invoiceNumber, now.Date, invoiceNumber, "");

            var fileName = $"rc-credit-note-rc96-{Guid.NewGuid():N}.pdf";
            File.WriteAllBytes(Path.Combine(PdfOutputDir, fileName), pdf);

            Assert.True(pdf.Length >= 4, "PDF bytes too short");
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));

            var text = ExtractNormalizedText(pdf);
            // Derived line VAT (66.00 * 21%) and derived line total (66.00 + 13.86)
            Assert.Contains("13.86", text);
            Assert.Contains("79.86", text);
            // "Suma apmokėjimui:" label fragment — no 't' glyph, extraction-safe
            Assert.Contains(NoSpaces(Rc96PayableLabelFragment), NoSpaces(text));
            // RC96 legal-note fragment (same stable fragment as the invoice test)
            Assert.Contains(NoSpaces(Rc96NoteStableFragment), NoSpaces(text));
        }
        finally
        {
            await CleanupPdfSeedAsync(partnerId, invoiceId, creditNoteNumber, settingsCreated, settingsId);
        }
    }

    [Fact]
    public async Task GenerateCreditNotePdf_Ulak6_KeepsRealVatAndNoRc96Labels()
    {
        var now = DateTime.UtcNow;
        var invoiceNumber = $"INV-CNULAK-{now.Ticks}";
        var creditNoteNumber = $"CN-ULAK-{now.Ticks}";

        var (settingsCreated, settingsId) = await EnsureCompanySettingsAsync();
        int partnerId = 0;
        int invoiceId = 0;
        try
        {
            partnerId = await SeedPdfCustomerAsync();

            // Original ULAK 6% invoice: stored line 100.00 / VAT 6.00 / total 106.00
            invoiceId = await SeedOriginalInvoiceAsync(partnerId, invoiceNumber, now.Date,
                InvoiceTypes.Ulak6, false, 1m, 100m, 6m, 100m, 6m, 106m);

            var creditNoteId = await SeedCreditNoteAsync(partnerId, creditNoteNumber, now, invoiceId, 100m, 6m, 106m);
            await SeedCreditNoteLineAsync(creditNoteId, 100m, 6m, 106m, 6m);

            Directory.CreateDirectory(PdfOutputDir);
            var env = new StubWebHostEnvironment { WebRootPath = ResolveRepoWebRoot() };
            var companySettings = new CompanySettingsService(_fixture.Factory);
            var service = new PdfGeneratorService(_fixture.Factory, companySettings, env);

            await using var genContext = await _fixture.Factory.CreateDbContextAsync();
            // Match the production load path (CreditNoteService.cs:699-702) which includes
            // OriginalInvoice — the PDF derives RC96 from it, so it must be populated here too.
            var creditNote = await genContext.CreditNotes.AsNoTracking()
                .Include(cn => cn.OriginalInvoice)
                .FirstOrDefaultAsync(cn => cn.Id == creditNoteId);
            Assert.NotNull(creditNote);
            var lines = new List<CreditNoteLineDto>
            {
                new CreditNoteLineDto
                {
                    Id = 0,
                    CreditNoteId = creditNoteId,
                    LineNumber = 1,
                    Description = "CN PDF verification line",
                    Quantity = 1m,
                    Unit = "vnt",
                    PriceExclVat = 100m,
                    VatRate = 6m,
                    LineSubtotal = 100m,
                    VatAmount = 6m,
                    LineTotal = 106m
                }
            };
            var customer = await genContext.BusinessPartners.AsNoTracking().FirstOrDefaultAsync(bp => bp.Id == partnerId);
            Assert.NotNull(customer);
            var currency = await genContext.Currencies.AsNoTracking().FirstOrDefaultAsync(c => c.Code == "TST");

            var pdf = await service.GenerateCreditNotePdfAsync(
                creditNote!, lines, customer, currency, invoiceNumber, now.Date, invoiceNumber, "");

            var fileName = $"rc-credit-note-ulak6-{Guid.NewGuid():N}.pdf";
            File.WriteAllBytes(Path.Combine(PdfOutputDir, fileName), pdf);

            Assert.True(pdf.Length >= 4, "PDF bytes too short");
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));

            var text = ExtractNormalizedText(pdf);
            Assert.Contains("6.00", text);
            Assert.Contains("106.00", text);
            Assert.DoesNotContain(NoSpaces(Rc96PayableLabelFragment), NoSpaces(text));
            Assert.DoesNotContain(NoSpaces(Rc96NoteStableFragment), NoSpaces(text));
        }
        finally
        {
            await CleanupPdfSeedAsync(partnerId, invoiceId, creditNoteNumber, settingsCreated, settingsId);
        }
    }

    [Fact]
    public async Task GenerateCreditNotePdf_Standard_ShowsRealVatAndNoRc96Labels()
    {
        var now = DateTime.UtcNow;
        var invoiceNumber = $"INV-CNSTD-{now.Ticks}";
        var creditNoteNumber = $"CN-STD-{now.Ticks}";

        var (settingsCreated, settingsId) = await EnsureCompanySettingsAsync();
        int partnerId = 0;
        int invoiceId = 0;
        try
        {
            partnerId = await SeedPdfCustomerAsync();

            // Original standard 21% invoice: stored line 100.00 / VAT 21.00 / total 121.00
            invoiceId = await SeedOriginalInvoiceAsync(partnerId, invoiceNumber, now.Date,
                InvoiceTypes.Standard, false, 2m, 50m, 21m, 100m, 21m, 121m);

            var creditNoteId = await SeedCreditNoteAsync(partnerId, creditNoteNumber, now, invoiceId, 100m, 21m, 121m);
            await SeedCreditNoteLineAsync(creditNoteId, 100m, 21m, 121m, 21m);

            Directory.CreateDirectory(PdfOutputDir);
            var env = new StubWebHostEnvironment { WebRootPath = ResolveRepoWebRoot() };
            var companySettings = new CompanySettingsService(_fixture.Factory);
            var service = new PdfGeneratorService(_fixture.Factory, companySettings, env);

            await using var genContext = await _fixture.Factory.CreateDbContextAsync();
            // Match the production load path (CreditNoteService.cs:699-702) which includes
            // OriginalInvoice — the PDF derives RC96 from it, so it must be populated here too.
            var creditNote = await genContext.CreditNotes.AsNoTracking()
                .Include(cn => cn.OriginalInvoice)
                .FirstOrDefaultAsync(cn => cn.Id == creditNoteId);
            Assert.NotNull(creditNote);
            var lines = new List<CreditNoteLineDto>
            {
                new CreditNoteLineDto
                {
                    Id = 0,
                    CreditNoteId = creditNoteId,
                    LineNumber = 1,
                    Description = "CN PDF verification line",
                    Quantity = 2m,
                    Unit = "vnt",
                    PriceExclVat = 50m,
                    VatRate = 21m,
                    LineSubtotal = 100m,
                    VatAmount = 21m,
                    LineTotal = 121m
                }
            };
            var customer = await genContext.BusinessPartners.AsNoTracking().FirstOrDefaultAsync(bp => bp.Id == partnerId);
            Assert.NotNull(customer);
            var currency = await genContext.Currencies.AsNoTracking().FirstOrDefaultAsync(c => c.Code == "TST");

            var pdf = await service.GenerateCreditNotePdfAsync(
                creditNote!, lines, customer, currency, invoiceNumber, now.Date, invoiceNumber, "");

            var fileName = $"rc-credit-note-standard-{Guid.NewGuid():N}.pdf";
            File.WriteAllBytes(Path.Combine(PdfOutputDir, fileName), pdf);

            Assert.True(pdf.Length >= 4, "PDF bytes too short");
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));

            var text = ExtractNormalizedText(pdf);
            Assert.Contains("21.00", text);
            Assert.Contains("121.00", text);
            Assert.DoesNotContain(NoSpaces(Rc96PayableLabelFragment), NoSpaces(text));
            Assert.DoesNotContain(NoSpaces(Rc96NoteStableFragment), NoSpaces(text));
        }
        finally
        {
            await CleanupPdfSeedAsync(partnerId, invoiceId, creditNoteNumber, settingsCreated, settingsId);
        }
    }

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "tests";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = string.Empty;
    }

    // --- Minimal stub implementations for CreditNoteService constructor dependencies ---

    private sealed class TestCreditNoteNumberGenerator : ICreditNoteNumberGenerator
    {
        public Task<string> GenerateNextNumberAsync(DateTime creditDate, IDbContextTransaction? transaction = null)
            => Task.FromResult($"CN-TEST-{DateTime.UtcNow.Ticks}");
    }

    private sealed class TestCompanySettingsService : ICompanySettingsService
    {
        public Task<CompanySettings> GetSettingsAsync()
            => Task.FromResult(new CompanySettings());
        public Task UpdateSettingsAsync(CompanySettings settings)
            => Task.CompletedTask;
    }

    private sealed class TestPdfGeneratorService : IPdfGeneratorService
    {
        public byte[] PdfBytes { get; set; } = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // "%PDF-1.4"
        public int GenerateCreditNotePdfAsyncCallCount { get; private set; }
        public bool ThrowIfCalled { get; set; }

        public byte[] GenerateInvoicePdf(int invoiceId) => PdfBytes;
        public Task<byte[]> GenerateInvoicePdfAsync(int invoiceId) => Task.FromResult(PdfBytes);
        public Task<byte[]> GenerateCreditNotePdfAsync(CreditNote creditNote, List<CreditNoteLineDto> lines, BusinessPartner? customer, Currency? currency, string? originalInvoiceNumber, DateTime? originalInvoiceDate, string? appliedInvoiceNumber, string? createdByName)
        {
            GenerateCreditNotePdfAsyncCallCount++;
            if (ThrowIfCalled) throw new InvalidOperationException("Generator must not be called when a cached copy exists");
            return Task.FromResult(PdfBytes);
        }
        public string GetPdfPath(string creditNoteNumber) => "/tmp/test.pdf";
        public Task<byte[]> GenerateMultipleInvoicesPdfAsync(List<int> invoiceIds) => Task.FromResult(PdfBytes);
    }

    private sealed class TestPaymentService : IPaymentService
    {
        // When a real IPaymentService is supplied, route the recalculation
        // calls to it so the test exercises the actual DB write path. All
        // other methods stay no-op stubs.
        private readonly IPaymentService? _real;

        public TestPaymentService(IPaymentService? real = null)
        {
            _real = real;
        }

        public Task<int> RegisterPaymentAsync(List<int> invoiceIds, decimal amount, DateTime paymentDate, string method, string? reference, string? notes, int userId)
            => Task.FromResult(0);
        public Task RecalculateInvoiceStatusAsync(int invoiceId)
        {
            if (_real != null) return _real.RecalculateInvoiceStatusAsync(invoiceId);
            return Task.CompletedTask;
        }
        public Task RecalculateInvoiceStatusAsync(List<int> invoiceIds)
        {
            if (_real != null) return _real.RecalculateInvoiceStatusAsync(invoiceIds);
            return Task.CompletedTask;
        }
        public Task<List<InvoiceWithPaymentInfo>> GetUnpaidInvoicesAsync(int? customerId = null, string? status = null, DateTime? fromDate = null, DateTime? toDate = null)
            => Task.FromResult(new List<InvoiceWithPaymentInfo>());
        public Task<List<CashFlowWeek>> GetCashFlowForecastAsync(int weeks = 8)
            => Task.FromResult(new List<CashFlowWeek>());
        public Task<List<CashFlowWeek>> GetCashFlowForecastAsync(DateTime fromDate, DateTime toDate)
            => Task.FromResult(new List<CashFlowWeek>());
        public Task<AgingReport> GetAgingReportAsync()
            => Task.FromResult(new AgingReport());
        public Task<PaymentHistoryResult> GetPaymentHistoryAsync(int? customerId = null, DateTime? fromDate = null, DateTime? toDate = null, string? paymentMethod = null, string? source = null, string? searchTerm = null, string? sortBy = null, string? sortDirection = null, int take = 50, int skip = 0)
            => Task.FromResult(new PaymentHistoryResult());
        public Task<PaymentWithDetails?> GetPaymentDetailAsync(int paymentId)
            => Task.FromResult<PaymentWithDetails?>(null);
        public Task<bool> DeletePaymentAsync(int paymentId, int userId)
            => Task.FromResult(false);
        public Task<bool> UpdatePaymentAsync(int paymentId, decimal amount, DateTime date, string method, string? reference, string? notes, int userId)
            => Task.FromResult(false);
        public Task<List<BankImportRow>> GetUnmatchedBankImportRowsAsync(int bankImportId)
            => Task.FromResult(new List<BankImportRow>());
        public Task<BankImportRow> MatchBankImportRowAsync(int bankImportRowId, int invoiceId, int userId)
            => Task.FromResult<BankImportRow>(null!);
        public Task<int> CreatePaymentFromBankImportAsync(int bankImportRowId, int userId)
            => Task.FromResult(0);
        public Task<List<BankImport>> GetBankImportsAsync(string? status = null, int take = 50, int skip = 0)
            => Task.FromResult(new List<BankImport>());
        public Task<int> CreateBankImportAsync(string fileName, string fileHash, int totalRows, int userId)
            => Task.FromResult(0);
        public Task UpdateBankImportAsync(int importId, int totalRows)
            => Task.CompletedTask;
        public Task<BankImport?> GetBankImportWithRowsAsync(int bankImportId)
            => Task.FromResult<BankImport?>(null);
        public Task<InvoiceWithPaymentInfoResult> GetSalesInvoicesAsync(int take = 50, int skip = 0, DateTime? fromDate = null, DateTime? toDate = null, string? searchTerm = null, InvoiceStatus? status = null)
            => Task.FromResult(new InvoiceWithPaymentInfoResult());
        public Task<List<PaymentHistoryItem>> GetPaymentsByInvoiceAsync(int invoiceId)
            => Task.FromResult(new List<PaymentHistoryItem>());
        public Task<List<InvoiceWithPaymentInfo>> SearchAllInvoicesAsync(string searchTerm, int limit = 20)
            => Task.FromResult(new List<InvoiceWithPaymentInfo>());
        public Task<InvoiceWithPaymentInfo?> GetInvoiceByIdAsync(int id)
            => Task.FromResult<InvoiceWithPaymentInfo?>(null);
        public Task<PaymentsDashboardKpi> GetPaymentsDashboardKpiAsync()
            => Task.FromResult(new PaymentsDashboardKpi());
        public Task<DashboardTrendResult> GetDashboardTrendAsync(decimal currentBarrelsKg, decimal currentBucketsKg, int currentUnpricedDeliveries, decimal currentSupplierDebtTotal)
            => Task.FromResult(new DashboardTrendResult());
        public Task<NavBadgeCounts> GetNavBadgeCountsAsync()
            => Task.FromResult(new NavBadgeCounts());
    }
}
