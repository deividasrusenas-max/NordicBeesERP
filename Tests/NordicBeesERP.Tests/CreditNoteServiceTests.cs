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
    public async Task CreateCreditNoteAsync_UnknownInvoiceLineId_ThrowsAndRollsBackEntireCreate()
    {
        var now = DateTime.UtcNow;
        var invoiceNumber = $"INV-CNROLLBACK-{now.Ticks}";

        await using var setupContext = await _fixture.Factory.CreateDbContextAsync();

        // 1. Insert test currency via raw SQL
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO currencies (code, name, symbol, is_active) VALUES ({0}, {1}, {2}, {3})",
            "TST", "Test Currency", "T", 1);

        // 2. Insert business partner (customer) via EF Core model insert
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Customer,
            Name = $"Test Customer Rollback {now.Ticks}",
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

        // 4. Insert one REAL invoice line via raw SQL so the request has a valid
        //    line to credit (quantity 1, price 10, VAT 21%, line total 12.10)
        const decimal lineQuantity = 1m;
        const decimal linePrice = 10m;
        const decimal lineVatRate = 21m;
        const decimal lineSubtotal = 10m;
        const decimal lineVatAmount = 2.10m;
        const decimal lineTotal = 12.10m;

        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO invoice_lines (invoice_id, line_number, product_code, description, quantity, unit, price_excl_vat, vat_rate, line_subtotal, vat_amount, line_total) VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10})",
            invoiceId, 1, "TST-PROD", "Test Product", lineQuantity, "vnt", linePrice, lineVatRate, lineSubtotal, lineVatAmount, lineTotal);

        var invoiceLineId = await setupContext.InvoiceLines
            .FromSqlRaw("SELECT id FROM invoice_lines WHERE invoice_id = {0}", invoiceId)
            .Select(l => l.Id)
            .FirstOrDefaultAsync();

        try
        {
            // 5. Act: call the REAL service create path with the REAL number
            //    generator (MAX+1 over credit_notes) — one VALID line followed by
            //    a BOGUS invoice line id, which must throw mid-loop and roll back
            //    EVERYTHING including the header row inserted before the loop.
            var service = new CreditNoteService(
                _fixture.Factory,
                new CreditNoteNumberGenerator(_fixture.Factory.CreateDbContext()),
                new TestCompanySettingsService(),
                new TestPdfGeneratorService(),
                new TestPaymentService());

            var request = new CreateCreditNoteRequest
            {
                OriginalInvoiceId = invoiceId,
                CreditDate = now,
                Language = "LT",
                Lines = new List<CreditNoteLineRequest>
                {
                    // VALID line: the real fixture line, full quantity and price —
                    // passes the remaining-amount guard and would insert fine.
                    new CreditNoteLineRequest
                    {
                        InvoiceLineId = invoiceLineId,
                        Quantity = lineQuantity,
                        PriceExclVat = linePrice
                    },
                    // BOGUS line: unknown invoice line id — must trigger the throw.
                    new CreditNoteLineRequest
                    {
                        InvoiceLineId = 999999999,
                        Quantity = 1m,
                        PriceExclVat = 1m
                    }
                }
            };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateCreditNoteAsync(request, 1));
            Assert.Contains("nerasta", ex.Message);

            // 6. Assert NOTHING persisted (BRAND NEW context): no header row and no
            //    line rows for this invoice — the rollback must be total, so the
            //    MAX+1 number generator's sequence is effectively reclaimed.
            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();

            var creditNoteCount = await verifyContext.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM credit_notes WHERE original_invoice_id = {0}", invoiceId)
                .SingleAsync();
            Assert.Equal(0, creditNoteCount);

            var lineCount = await verifyContext.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM credit_note_lines WHERE credit_note_id IN (SELECT id FROM credit_notes WHERE original_invoice_id = {0})", invoiceId)
                .SingleAsync();
            Assert.Equal(0, lineCount);
        }
        finally
        {
            // 7. Cleanup in reverse FK order — defensive deletes FIRST (a future
            //    regression that DOES leave rows behind gets cleaned up and does
            //    not poison later test runs), then the standard fixture teardown.
            await using var cleanupContext = await _fixture.Factory.CreateDbContextAsync();
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM credit_note_lines WHERE credit_note_id IN (SELECT id FROM credit_notes WHERE original_invoice_id = {0})", invoiceId);
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM credit_notes WHERE original_invoice_id = {0}", invoiceId);
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
    public async Task UpdateCreditNoteAsync_NonExistentInvoiceLineId_ThrowsAndLeavesDatabaseUnchanged()
    {
        var now = DateTime.UtcNow;
        var invoiceNumber = $"INV-CNMIS-{now.Ticks}";
        var creditNoteNumber = $"CN-MIS-{now.Ticks}";

        await using var setupContext = await _fixture.Factory.CreateDbContextAsync();

        // 1. Insert test currency via raw SQL (delete first for idempotency)
        await setupContext.Database.ExecuteSqlRawAsync("DELETE FROM currencies WHERE code = {0}", "TST");
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO currencies (code, name, symbol, is_active) VALUES ({0}, {1}, {2}, {3})",
            "TST", "Test Currency", "T", 1);

        // 2. Insert business partner (customer) via EF Core model insert
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Customer,
            Name = $"Test Customer MissingLine {now.Ticks}",
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

        // 3. Insert invoice via raw SQL (with currency)
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO invoices (invoice_number, invoice_date, customer_id, currency_id, language, invoice_type, reverse_charge, subtotal_excl_vat, total_vat, total_incl_vat) VALUES ({0}, {1}, {2}, (SELECT id FROM currencies WHERE code = {3}), 'LT', 'PVM SĄSKAITA FAKTŪRA', 0, 110.00, 23.10, 133.10)",
            invoiceNumber, now.Date, bpId, "TST");

        var invoiceId = await setupContext.Invoices
            .FromSqlRaw("SELECT id FROM invoices WHERE invoice_number = {0}", invoiceNumber)
            .Select(i => i.Id)
            .FirstOrDefaultAsync();

        // 4. Insert TWO invoice lines via raw SQL
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO invoice_lines (invoice_id, line_number, description, quantity, unit, price_excl_vat, vat_rate, line_subtotal, vat_amount, line_total, created_at) VALUES ({0}, {1}, {2}, {3}, 'vnt', {4}, 21.0, {5}, {6}, {7}, {8})",
            invoiceId, 1, "Test line A", 5m, 10.00m, 50.00m, 10.50m, 60.50m, now);
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO invoice_lines (invoice_id, line_number, description, quantity, unit, price_excl_vat, vat_rate, line_subtotal, vat_amount, line_total, created_at) VALUES ({0}, {1}, {2}, {3}, 'vnt', {4}, 21.0, {5}, {6}, {7}, {8})",
            invoiceId, 2, "Test line B", 3m, 20.00m, 60.00m, 12.60m, 72.60m, now);

        var lineAId = await setupContext.InvoiceLines
            .FromSqlRaw("SELECT id FROM invoice_lines WHERE invoice_id = {0} AND line_number = {1}", invoiceId, 1)
            .Select(l => l.Id)
            .FirstOrDefaultAsync();
        var lineBId = await setupContext.InvoiceLines
            .FromSqlRaw("SELECT id FROM invoice_lines WHERE invoice_id = {0} AND line_number = {1}", invoiceId, 2)
            .Select(l => l.Id)
            .FirstOrDefaultAsync();

        // 5. Insert a DRAFT credit note linked to that invoice via raw SQL
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO credit_notes (credit_note_number, credit_date, original_invoice_id, applied_invoice_id, customer_id, currency_id, language, reverse_charge, subtotal_excl_vat, total_vat, total_incl_vat, status, created_by, created_at, updated_at) VALUES ({0}, {1}, (SELECT id FROM invoices WHERE invoice_number = {2}), (SELECT id FROM invoices WHERE invoice_number = {3}), {4}, (SELECT id FROM currencies WHERE code = {5}), {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}, {14})",
            creditNoteNumber, now, invoiceNumber, invoiceNumber, bpId, "TST", "LT", false, 40.00m, 8.40m, 48.40m, "draft", 1, now, now);

        var creditNoteId = await setupContext.CreditNotes
            .FromSqlRaw("SELECT id FROM credit_notes WHERE credit_note_number = {0}", creditNoteNumber)
            .Select(cn => cn.Id)
            .FirstOrDefaultAsync();

        // 6. Insert TWO credit note lines via raw SQL
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO credit_note_lines (credit_note_id, invoice_line_id, line_number, description, quantity, unit, price_excl_vat, vat_rate, line_subtotal, vat_amount, line_total, created_at) VALUES ({0}, {1}, {2}, {3}, {4}, 'vnt', {5}, 21.0, {6}, {7}, {8}, {9})",
            creditNoteId, lineAId, 1, "Test line A", 2m, 10.00m, 20.00m, 4.20m, 24.20m, now);
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO credit_note_lines (credit_note_id, invoice_line_id, line_number, description, quantity, unit, price_excl_vat, vat_rate, line_subtotal, vat_amount, line_total, created_at) VALUES ({0}, {1}, {2}, {3}, {4}, 'vnt', {5}, 21.0, {6}, {7}, {8}, {9})",
            creditNoteId, lineBId, 2, "Test line B", 1m, 20.00m, 20.00m, 4.20m, 24.20m, now);

        var maxLineId = await setupContext.InvoiceLines.Select(l => (int?)l.Id).MaxAsync() ?? 0;
        var missingId = maxLineId + 1;

        try
        {
            // 7. Act: the service must throw on the non-existent invoice line id, BEFORE any SaveChangesAsync
            var service = new CreditNoteService(
                _fixture.Factory,
                new TestCreditNoteNumberGenerator(),
                new TestCompanySettingsService(),
                new TestPdfGeneratorService(),
                new TestPaymentService());

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateCreditNoteAsync(new UpdateCreditNoteRequest
            {
                Id = creditNoteId,
                CustomerId = bpId,
                OriginalInvoiceId = invoiceId,
                CreditDate = now,
                Language = "LT",
                Status = CreditNoteStatus.Draft,
                ReverseCharge = false,
                Lines = { new CreditNoteLineRequest { InvoiceLineId = missingId, Quantity = 1m } }
            }, 1));

            Assert.Contains($"{missingId}", ex.Message);

            // 8. Verify with a BRAND NEW context: nothing was partially persisted —
            //    both seeded credit note lines are still there with their seeded amounts
            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();

            var lines = await verifyContext.CreditNoteLines
                .Where(l => l.CreditNoteId == creditNoteId)
                .ToListAsync();

            Assert.Equal(2, lines.Count);
            Assert.Equal(48.40m, lines.Sum(l => l.LineTotal));

            var lineA = lines.Single(l => l.InvoiceLineId == lineAId);
            Assert.Equal(2m, lineA.Quantity);
            Assert.Equal(20.00m, lineA.LineSubtotal);
            Assert.Equal(4.20m, lineA.VatAmount);
            Assert.Equal(24.20m, lineA.LineTotal);

            var lineB = lines.Single(l => l.InvoiceLineId == lineBId);
            Assert.Equal(1m, lineB.Quantity);
            Assert.Equal(20.00m, lineB.LineSubtotal);
            Assert.Equal(4.20m, lineB.VatAmount);
            Assert.Equal(24.20m, lineB.LineTotal);

            // Header amounts unchanged too
            var header = await verifyContext.CreditNotes
                .FromSqlRaw("SELECT subtotal_excl_vat, total_vat, total_incl_vat FROM credit_notes WHERE id = {0}", creditNoteId)
                .Select(cn => new { cn.SubtotalExclVat, cn.TotalVat, cn.TotalInclVat })
                .FirstOrDefaultAsync();

            Assert.NotNull(header);
            Assert.Equal(40.00m, header!.SubtotalExclVat);
            Assert.Equal(8.40m, header.TotalVat);
            Assert.Equal(48.40m, header.TotalInclVat);
        }
        finally
        {
            // 9. Cleanup in reverse FK order
            await using var cleanupContext = await _fixture.Factory.CreateDbContextAsync();
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM credit_note_lines WHERE credit_note_id = (SELECT id FROM credit_notes WHERE credit_note_number = {0})", creditNoteNumber);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM credit_notes WHERE credit_note_number = {0}", creditNoteNumber);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM invoice_lines WHERE invoice_id = {0}", invoiceId);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM invoices WHERE invoice_number = {0}", invoiceNumber);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", bpId);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM currencies WHERE code = {0}", "TST");
        }
    }

    [Fact]
    public async Task UpdateCreditNoteAsync_ZeroQuantityLine_DropsLineAndRecomputesTotals()
    {
        var now = DateTime.UtcNow;
        var invoiceNumber = $"INV-CNZERO-{now.Ticks}";
        var creditNoteNumber = $"CN-ZERO-{now.Ticks}";

        await using var setupContext = await _fixture.Factory.CreateDbContextAsync();

        // 1. Insert test currency via raw SQL (delete first for idempotency)
        await setupContext.Database.ExecuteSqlRawAsync("DELETE FROM currencies WHERE code = {0}", "TST");
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO currencies (code, name, symbol, is_active) VALUES ({0}, {1}, {2}, {3})",
            "TST", "Test Currency", "T", 1);

        // 2. Insert business partner (customer) via EF Core model insert
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Customer,
            Name = $"Test Customer ZeroQty {now.Ticks}",
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

        // 3. Insert invoice via raw SQL (with currency)
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO invoices (invoice_number, invoice_date, customer_id, currency_id, language, invoice_type, reverse_charge, subtotal_excl_vat, total_vat, total_incl_vat) VALUES ({0}, {1}, {2}, (SELECT id FROM currencies WHERE code = {3}), 'LT', 'PVM SĄSKAITA FAKTŪRA', 0, 110.00, 23.10, 133.10)",
            invoiceNumber, now.Date, bpId, "TST");

        var invoiceId = await setupContext.Invoices
            .FromSqlRaw("SELECT id FROM invoices WHERE invoice_number = {0}", invoiceNumber)
            .Select(i => i.Id)
            .FirstOrDefaultAsync();

        // 4. Insert TWO invoice lines via raw SQL
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO invoice_lines (invoice_id, line_number, description, quantity, unit, price_excl_vat, vat_rate, line_subtotal, vat_amount, line_total, created_at) VALUES ({0}, {1}, {2}, {3}, 'vnt', {4}, 21.0, {5}, {6}, {7}, {8})",
            invoiceId, 1, "Test line A", 5m, 10.00m, 50.00m, 10.50m, 60.50m, now);
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO invoice_lines (invoice_id, line_number, description, quantity, unit, price_excl_vat, vat_rate, line_subtotal, vat_amount, line_total, created_at) VALUES ({0}, {1}, {2}, {3}, 'vnt', {4}, 21.0, {5}, {6}, {7}, {8})",
            invoiceId, 2, "Test line B", 3m, 20.00m, 60.00m, 12.60m, 72.60m, now);

        var lineAId = await setupContext.InvoiceLines
            .FromSqlRaw("SELECT id FROM invoice_lines WHERE invoice_id = {0} AND line_number = {1}", invoiceId, 1)
            .Select(l => l.Id)
            .FirstOrDefaultAsync();
        var lineBId = await setupContext.InvoiceLines
            .FromSqlRaw("SELECT id FROM invoice_lines WHERE invoice_id = {0} AND line_number = {1}", invoiceId, 2)
            .Select(l => l.Id)
            .FirstOrDefaultAsync();

        // 5. Insert a DRAFT credit note linked to that invoice via raw SQL
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO credit_notes (credit_note_number, credit_date, original_invoice_id, applied_invoice_id, customer_id, currency_id, language, reverse_charge, subtotal_excl_vat, total_vat, total_incl_vat, status, created_by, created_at, updated_at) VALUES ({0}, {1}, (SELECT id FROM invoices WHERE invoice_number = {2}), (SELECT id FROM invoices WHERE invoice_number = {3}), {4}, (SELECT id FROM currencies WHERE code = {5}), {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}, {14})",
            creditNoteNumber, now, invoiceNumber, invoiceNumber, bpId, "TST", "LT", false, 40.00m, 8.40m, 48.40m, "draft", 1, now, now);

        var creditNoteId = await setupContext.CreditNotes
            .FromSqlRaw("SELECT id FROM credit_notes WHERE credit_note_number = {0}", creditNoteNumber)
            .Select(cn => cn.Id)
            .FirstOrDefaultAsync();

        // 6. Insert TWO credit note lines via raw SQL
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO credit_note_lines (credit_note_id, invoice_line_id, line_number, description, quantity, unit, price_excl_vat, vat_rate, line_subtotal, vat_amount, line_total, created_at) VALUES ({0}, {1}, {2}, {3}, {4}, 'vnt', {5}, 21.0, {6}, {7}, {8}, {9})",
            creditNoteId, lineAId, 1, "Test line A", 2m, 10.00m, 20.00m, 4.20m, 24.20m, now);
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO credit_note_lines (credit_note_id, invoice_line_id, line_number, description, quantity, unit, price_excl_vat, vat_rate, line_subtotal, vat_amount, line_total, created_at) VALUES ({0}, {1}, {2}, {3}, {4}, 'vnt', {5}, 21.0, {6}, {7}, {8}, {9})",
            creditNoteId, lineBId, 2, "Test line B", 1m, 20.00m, 20.00m, 4.20m, 24.20m, now);

        try
        {
            // 7. Act: request line A with zero quantity (legitimate drop) and line B with qty 1;
            //    no throw expected — the service must recompute lines and header totals
            var service = new CreditNoteService(
                _fixture.Factory,
                new TestCreditNoteNumberGenerator(),
                new TestCompanySettingsService(),
                new TestPdfGeneratorService(),
                new TestPaymentService());

            await service.UpdateCreditNoteAsync(new UpdateCreditNoteRequest
            {
                Id = creditNoteId,
                CustomerId = bpId,
                OriginalInvoiceId = invoiceId,
                CreditDate = now,
                Language = "LT",
                Status = CreditNoteStatus.Draft,
                ReverseCharge = false,
                Lines =
                {
                    new CreditNoteLineRequest { InvoiceLineId = lineAId, Quantity = 0m, PriceExclVat = 0m },
                    new CreditNoteLineRequest { InvoiceLineId = lineBId, Quantity = 1m, PriceExclVat = 0m }
                }
            }, 1);

            // 8. Verify with a BRAND NEW context: exactly one line remains (line B),
            //    header totals recomputed from it
            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();

            var lines = await verifyContext.CreditNoteLines
                .Where(l => l.CreditNoteId == creditNoteId)
                .ToListAsync();

            Assert.Single(lines);
            var remainingLine = lines[0];
            Assert.Equal(lineBId, remainingLine.InvoiceLineId);
            Assert.Equal(1m, remainingLine.Quantity);
            // Request price was 0 -> service falls back to the invoice line price
            Assert.Equal(20.00m, remainingLine.PriceExclVat);
            Assert.Equal(20.00m, remainingLine.LineSubtotal);
            Assert.Equal(4.20m, remainingLine.VatAmount);
            Assert.Equal(24.20m, remainingLine.LineTotal);

            // Header totals recomputed: 20.00 / 4.20 / 24.20, status still draft
            var header = await verifyContext.CreditNotes
                .FromSqlRaw("SELECT subtotal_excl_vat, total_vat, total_incl_vat, status FROM credit_notes WHERE id = {0}", creditNoteId)
                .Select(cn => new { cn.SubtotalExclVat, cn.TotalVat, cn.TotalInclVat, cn.Status })
                .FirstOrDefaultAsync();

            Assert.NotNull(header);
            Assert.Equal(20.00m, header!.SubtotalExclVat);
            Assert.Equal(4.20m, header.TotalVat);
            Assert.Equal(24.20m, header.TotalInclVat);
            Assert.Equal(CreditNoteStatus.Draft, header.Status);
        }
        finally
        {
            // 9. Cleanup in reverse FK order
            await using var cleanupContext = await _fixture.Factory.CreateDbContextAsync();
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM credit_note_lines WHERE credit_note_id = (SELECT id FROM credit_notes WHERE credit_note_number = {0})", creditNoteNumber);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM credit_notes WHERE credit_note_number = {0}", creditNoteNumber);
            await cleanupContext.Database.ExecuteSqlRawAsync("DELETE FROM invoice_lines WHERE invoice_id = {0}", invoiceId);
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
    public async Task CreateCreditNoteAsync_Rc96Invoice_FullCredit_StoresNetOnlyLineAndTotals()
    {
        var now = DateTime.UtcNow;
        var invoiceNumber = $"INV-RC96FULL-{now.Ticks}";

        var partnerId = await SeedPdfCustomerAsync();
        var invoiceId = await SeedOriginalInvoiceAsync(
            partnerId, invoiceNumber, now.Date,
            InvoiceTypes.ReverseCharge96, reverseCharge: true,
            quantity: 660m, price: 0.10m, rate: 21m,
            subtotal: 66.00m, vat: 0m, total: 66.00m);

        // The seed helper does not set the paid columns — force the invoice to a known
        // fully-unpaid state so the credit note's status recalculation starts from a clean slate.
        await using var setupContext = await _fixture.Factory.CreateDbContextAsync();
        await setupContext.Database.ExecuteSqlRawAsync(
            "UPDATE invoices SET paid_amount = {0}, payment_status = {1} WHERE id = {2}",
            0m, "unpaid", invoiceId);

        var invoiceLineId = await setupContext.InvoiceLines
            .FromSqlRaw("SELECT id FROM invoice_lines WHERE invoice_id = {0}", invoiceId)
            .Select(l => l.Id)
            .FirstOrDefaultAsync();

        var creditNoteNumber = "";

        try
        {
            // Act: no-arg TestPaymentService — the no-op stub (no status recalculation needed here).
            var service = new CreditNoteService(
                _fixture.Factory,
                new TestCreditNoteNumberGenerator(),
                new TestCompanySettingsService(),
                new TestPdfGeneratorService(),
                new TestPaymentService());

            // PriceExclVat is deliberately left at its default (0) so the service falls back to
            // the original line's stored 0.10 price.
            var creditNote = await service.CreateCreditNoteAsync(new CreateCreditNoteRequest
            {
                OriginalInvoiceId = invoiceId,
                CreditDate = now,
                Language = "LT",
                Lines = new List<CreditNoteLineRequest> { new CreditNoteLineRequest { InvoiceLineId = invoiceLineId, Quantity = 660m } }
            }, 1);

            creditNoteNumber = creditNote.CreditNoteNumber;

            Assert.NotNull(creditNote);
            Assert.False(string.IsNullOrEmpty(creditNote.CreditNoteNumber));

            // Assert: read back with a BRAND NEW DbContext (AsNoTracking) — the RC96 line must be
            // stored NET-only: 21% rate, zero VAT, subtotal == total.
            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
            var line = await verifyContext.CreditNoteLines
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.CreditNoteId == creditNote.Id);

            Assert.NotNull(line);
            Assert.Equal(21m, line!.VatRate);
            Assert.Equal(0m, line.VatAmount);
            Assert.Equal(66.00m, line.LineSubtotal);
            Assert.Equal(66.00m, line.LineTotal);
            Assert.Equal(660m, line.Quantity);

            var note = await verifyContext.CreditNotes
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == creditNote.Id);

            Assert.NotNull(note);
            Assert.Equal(66.00m, note!.SubtotalExclVat);
            Assert.Equal(0m, note.TotalVat);
            Assert.Equal(66.00m, note.TotalInclVat);
        }
        finally
        {
            await CleanupPdfSeedAsync(partnerId, invoiceId, creditNoteNumber);
        }
    }

    [Fact]
    public async Task CreateCreditNoteAsync_Rc96Invoice_PartialCredit_StoresNetOnlyLine()
    {
        var now = DateTime.UtcNow;
        var invoiceNumber = $"INV-RC96PART-{now.Ticks}";

        var partnerId = await SeedPdfCustomerAsync();
        var invoiceId = await SeedOriginalInvoiceAsync(
            partnerId, invoiceNumber, now.Date,
            InvoiceTypes.ReverseCharge96, reverseCharge: true,
            quantity: 660m, price: 0.10m, rate: 21m,
            subtotal: 66.00m, vat: 0m, total: 66.00m);

        // The seed helper does not set the paid columns — force the invoice to a known
        // fully-unpaid state so the credit note's status recalculation starts from a clean slate.
        await using var setupContext = await _fixture.Factory.CreateDbContextAsync();
        await setupContext.Database.ExecuteSqlRawAsync(
            "UPDATE invoices SET paid_amount = {0}, payment_status = {1} WHERE id = {2}",
            0m, "unpaid", invoiceId);

        var invoiceLineId = await setupContext.InvoiceLines
            .FromSqlRaw("SELECT id FROM invoice_lines WHERE invoice_id = {0}", invoiceId)
            .Select(l => l.Id)
            .FirstOrDefaultAsync();

        var creditNoteNumber = "";

        try
        {
            // Act: no-arg TestPaymentService — the no-op stub (no status recalculation needed here).
            var service = new CreditNoteService(
                _fixture.Factory,
                new TestCreditNoteNumberGenerator(),
                new TestCompanySettingsService(),
                new TestPdfGeneratorService(),
                new TestPaymentService());

            // PriceExclVat is deliberately left at its default (0) so the service falls back to
            // the original line's stored 0.10 price. Credit only half the quantity.
            var creditNote = await service.CreateCreditNoteAsync(new CreateCreditNoteRequest
            {
                OriginalInvoiceId = invoiceId,
                CreditDate = now,
                Language = "LT",
                Lines = new List<CreditNoteLineRequest> { new CreditNoteLineRequest { InvoiceLineId = invoiceLineId, Quantity = 330m } }
            }, 1);

            creditNoteNumber = creditNote.CreditNoteNumber;

            Assert.NotNull(creditNote);
            Assert.False(string.IsNullOrEmpty(creditNote.CreditNoteNumber));

            // Assert: read back with a BRAND NEW DbContext (AsNoTracking) — the RC96 line must be
            // stored NET-only: 21% rate, zero VAT, subtotal == total for the partial quantity.
            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
            var line = await verifyContext.CreditNoteLines
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.CreditNoteId == creditNote.Id);

            Assert.NotNull(line);
            Assert.Equal(21m, line!.VatRate);
            Assert.Equal(0m, line.VatAmount);
            Assert.Equal(33.00m, line.LineSubtotal);
            Assert.Equal(33.00m, line.LineTotal);
            Assert.Equal(330m, line.Quantity);

            var note = await verifyContext.CreditNotes
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == creditNote.Id);

            Assert.NotNull(note);
            Assert.Equal(33.00m, note!.SubtotalExclVat);
            Assert.Equal(0m, note.TotalVat);
            Assert.Equal(33.00m, note.TotalInclVat);
        }
        finally
        {
            await CleanupPdfSeedAsync(partnerId, invoiceId, creditNoteNumber);
        }
    }

    [Fact]
    public async Task CreateCreditNoteAsync_Standard21Invoice_CreditKeepsVat()
    {
        var now = DateTime.UtcNow;
        var invoiceNumber = $"INV-STANDARD21-{now.Ticks}";

        var partnerId = await SeedPdfCustomerAsync();
        var invoiceId = await SeedOriginalInvoiceAsync(
            partnerId, invoiceNumber, now.Date,
            InvoiceTypes.Standard, reverseCharge: false,
            quantity: 100m, price: 1.00m, rate: 21m,
            subtotal: 100.00m, vat: 21.00m, total: 121.00m);

        // The seed helper does not set the paid columns — force the invoice to a known
        // fully-unpaid state so the credit note's status recalculation starts from a clean slate.
        await using var setupContext = await _fixture.Factory.CreateDbContextAsync();
        await setupContext.Database.ExecuteSqlRawAsync(
            "UPDATE invoices SET paid_amount = {0}, payment_status = {1} WHERE id = {2}",
            0m, "unpaid", invoiceId);

        var invoiceLineId = await setupContext.InvoiceLines
            .FromSqlRaw("SELECT id FROM invoice_lines WHERE invoice_id = {0}", invoiceId)
            .Select(l => l.Id)
            .FirstOrDefaultAsync();

        var creditNoteNumber = "";

        try
        {
            // Act: no-arg TestPaymentService — the no-op stub (no status recalculation needed here).
            var service = new CreditNoteService(
                _fixture.Factory,
                new TestCreditNoteNumberGenerator(),
                new TestCompanySettingsService(),
                new TestPdfGeneratorService(),
                new TestPaymentService());

            // PriceExclVat is deliberately left at its default (0) so the service falls back to
            // the original line's stored 1.00 price. Credit the full quantity.
            var creditNote = await service.CreateCreditNoteAsync(new CreateCreditNoteRequest
            {
                OriginalInvoiceId = invoiceId,
                CreditDate = now,
                Language = "LT",
                Lines = new List<CreditNoteLineRequest> { new CreditNoteLineRequest { InvoiceLineId = invoiceLineId, Quantity = 100m } }
            }, 1);

            creditNoteNumber = creditNote.CreditNoteNumber;

            Assert.NotNull(creditNote);
            Assert.False(string.IsNullOrEmpty(creditNote.CreditNoteNumber));

            // Assert: read back with a BRAND NEW DbContext (AsNoTracking) — a standard 21%
            // invoice is NOT RC96, so the credit line must keep its real VAT.
            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
            var line = await verifyContext.CreditNoteLines
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.CreditNoteId == creditNote.Id);

            Assert.NotNull(line);
            Assert.Equal(21m, line!.VatRate);
            Assert.Equal(21.00m, line.VatAmount);
            Assert.Equal(100.00m, line.LineSubtotal);
            Assert.Equal(121.00m, line.LineTotal);
            Assert.Equal(100m, line.Quantity);

            var note = await verifyContext.CreditNotes
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == creditNote.Id);

            Assert.NotNull(note);
            Assert.Equal(100.00m, note!.SubtotalExclVat);
            Assert.Equal(21.00m, note.TotalVat);
            Assert.Equal(121.00m, note.TotalInclVat);
        }
        finally
        {
            await CleanupPdfSeedAsync(partnerId, invoiceId, creditNoteNumber);
        }
    }

    [Fact]
    public async Task CreateCreditNoteAsync_Ulak6Invoice_CreditKeepsVat()
    {
        var now = DateTime.UtcNow;
        var invoiceNumber = $"INV-ULAK6-{now.Ticks}";

        var partnerId = await SeedPdfCustomerAsync();
        var invoiceId = await SeedOriginalInvoiceAsync(
            partnerId, invoiceNumber, now.Date,
            InvoiceTypes.Ulak6, reverseCharge: false,
            quantity: 100m, price: 1.00m, rate: 6m,
            subtotal: 100.00m, vat: 6.00m, total: 106.00m);

        // The seed helper does not set the paid columns — force the invoice to a known
        // fully-unpaid state so the credit note's status recalculation starts from a clean slate.
        await using var setupContext = await _fixture.Factory.CreateDbContextAsync();
        await setupContext.Database.ExecuteSqlRawAsync(
            "UPDATE invoices SET paid_amount = {0}, payment_status = {1} WHERE id = {2}",
            0m, "unpaid", invoiceId);

        var invoiceLineId = await setupContext.InvoiceLines
            .FromSqlRaw("SELECT id FROM invoice_lines WHERE invoice_id = {0}", invoiceId)
            .Select(l => l.Id)
            .FirstOrDefaultAsync();

        var creditNoteNumber = "";

        try
        {
            // Act: no-arg TestPaymentService — the no-op stub (no status recalculation needed here).
            var service = new CreditNoteService(
                _fixture.Factory,
                new TestCreditNoteNumberGenerator(),
                new TestCompanySettingsService(),
                new TestPdfGeneratorService(),
                new TestPaymentService());

            // PriceExclVat is deliberately left at its default (0) so the service falls back to
            // the original line's stored 1.00 price. Credit the full quantity.
            var creditNote = await service.CreateCreditNoteAsync(new CreateCreditNoteRequest
            {
                OriginalInvoiceId = invoiceId,
                CreditDate = now,
                Language = "LT",
                Lines = new List<CreditNoteLineRequest> { new CreditNoteLineRequest { InvoiceLineId = invoiceLineId, Quantity = 100m } }
            }, 1);

            creditNoteNumber = creditNote.CreditNoteNumber;

            Assert.NotNull(creditNote);
            Assert.False(string.IsNullOrEmpty(creditNote.CreditNoteNumber));

            // Assert: read back with a BRAND NEW DbContext (AsNoTracking) — the ULAK 6% invoice
            // is NOT RC96 (reverse_charge=0), so the credit line must keep its real VAT.
            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
            var line = await verifyContext.CreditNoteLines
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.CreditNoteId == creditNote.Id);

            Assert.NotNull(line);
            Assert.Equal(6m, line!.VatRate);
            Assert.Equal(6.00m, line.VatAmount);
            Assert.Equal(100.00m, line.LineSubtotal);
            Assert.Equal(106.00m, line.LineTotal);
            Assert.Equal(100m, line.Quantity);

            var note = await verifyContext.CreditNotes
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == creditNote.Id);

            Assert.NotNull(note);
            Assert.Equal(100.00m, note!.SubtotalExclVat);
            Assert.Equal(6.00m, note.TotalVat);
            Assert.Equal(106.00m, note.TotalInclVat);
        }
        finally
        {
            await CleanupPdfSeedAsync(partnerId, invoiceId, creditNoteNumber);
        }
    }

    [Fact]
    public async Task CreateCreditNoteAsync_Rc96Invoice_RealPaymentService_MarksInvoicePaid()
    {
        var now = DateTime.UtcNow;
        var invoiceNumber = $"INV-RC96SETTLE-{now.Ticks}";

        var partnerId = await SeedPdfCustomerAsync();
        var invoiceId = await SeedOriginalInvoiceAsync(
            partnerId, invoiceNumber, now.Date,
            InvoiceTypes.ReverseCharge96, reverseCharge: true,
            quantity: 660m, price: 0.10m, rate: 21m,
            subtotal: 66.00m, vat: 0m, total: 66.00m);

        // The seed helper does not set the paid columns — force the invoice to a known
        // fully-unpaid state so the credit note's status recalculation starts from a clean slate.
        await using var setupContext = await _fixture.Factory.CreateDbContextAsync();
        await setupContext.Database.ExecuteSqlRawAsync(
            "UPDATE invoices SET paid_amount = {0}, payment_status = {1} WHERE id = {2}",
            0m, "unpaid", invoiceId);

        var invoiceLineId = await setupContext.InvoiceLines
            .FromSqlRaw("SELECT id FROM invoice_lines WHERE invoice_id = {0}", invoiceId)
            .Select(l => l.Id)
            .FirstOrDefaultAsync();

        var creditNoteNumber = "";

        try
        {
            // Act: build the service with a REAL payment backend so the
            //    RecalculateInvoiceStatusAsync call actually runs against the test DB.
            var realPaymentService = new PaymentService(_fixture.Factory);
            var service = new CreditNoteService(
                _fixture.Factory,
                new TestCreditNoteNumberGenerator(),
                new TestCompanySettingsService(),
                new TestPdfGeneratorService(),
                new TestPaymentService(realPaymentService));

            // PriceExclVat is deliberately left at its default (0) so the service falls back to
            // the original line's stored 0.10 price. Credit the full quantity.
            var creditNote = await service.CreateCreditNoteAsync(new CreateCreditNoteRequest
            {
                OriginalInvoiceId = invoiceId,
                CreditDate = now,
                Language = "LT",
                Lines = new List<CreditNoteLineRequest> { new CreditNoteLineRequest { InvoiceLineId = invoiceLineId, Quantity = 660m } }
            }, 1);

            creditNoteNumber = creditNote.CreditNoteNumber;

            Assert.NotNull(creditNote);
            Assert.False(string.IsNullOrEmpty(creditNote.CreditNoteNumber));

            // Assert: read the ORIGINAL invoice back with a BRAND NEW DbContext —
            //    payment_status must be "paid" (fully credited) while paid_amount stays 0
            //    (real cash only, no allocations).
            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
            var storedInvoice = await verifyContext.Invoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            Assert.NotNull(storedInvoice);
            Assert.Equal("paid", storedInvoice!.PaymentStatus);
            Assert.Equal(0m, storedInvoice.PaidAmount);

            // The RC96 credit line must be stored NET-only: 21% rate, zero VAT, subtotal == total.
            var line = await verifyContext.CreditNoteLines
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.CreditNoteId == creditNote.Id);

            Assert.NotNull(line);
            Assert.Equal(21m, line!.VatRate);
            Assert.Equal(0m, line.VatAmount);
            Assert.Equal(66.00m, line.LineSubtotal);
            Assert.Equal(66.00m, line.LineTotal);

            var note = await verifyContext.CreditNotes
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == creditNote.Id);

            Assert.NotNull(note);
            Assert.Equal(66.00m, note!.TotalInclVat);
        }
        finally
        {
            await CleanupPdfSeedAsync(partnerId, invoiceId, creditNoteNumber);
        }
    }

    [Fact]
    public async Task UpdateCreditNoteAsync_Rc96Invoice_Increase330To660_StoresNetOnly()
    {
        var now = DateTime.UtcNow;
        var invoiceNumber = $"INV-RC96UPD-{now.Ticks}";

        var partnerId = await SeedPdfCustomerAsync();
        var invoiceId = await SeedOriginalInvoiceAsync(
            partnerId, invoiceNumber, now.Date,
            InvoiceTypes.ReverseCharge96, reverseCharge: true,
            quantity: 660m, price: 0.10m, rate: 21m,
            subtotal: 66.00m, vat: 0m, total: 66.00m);

        // The seed helper does not set the paid columns — force the invoice to a known
        // fully-unpaid state so the credit note's status recalculation starts from a clean slate.
        await using var setupContext = await _fixture.Factory.CreateDbContextAsync();
        await setupContext.Database.ExecuteSqlRawAsync(
            "UPDATE invoices SET paid_amount = {0}, payment_status = {1} WHERE id = {2}",
            0m, "unpaid", invoiceId);

        var invoiceLineId = await setupContext.InvoiceLines
            .FromSqlRaw("SELECT id FROM invoice_lines WHERE invoice_id = {0}", invoiceId)
            .Select(l => l.Id)
            .FirstOrDefaultAsync();

        var creditNoteNumber = "";

        try
        {
            // Act 1: create a partial credit note (330 of 660). This succeeds pre-fix, with the
            //    (wrong) VAT values — it is only the starting point for the update.
            var service = new CreditNoteService(
                _fixture.Factory,
                new TestCreditNoteNumberGenerator(),
                new TestCompanySettingsService(),
                new TestPdfGeneratorService(),
                new TestPaymentService());

            // PriceExclVat is deliberately left at its default (0) so the service falls back to
            // the original line's stored 0.10 price. Credit half the quantity.
            var creditNote = await service.CreateCreditNoteAsync(new CreateCreditNoteRequest
            {
                OriginalInvoiceId = invoiceId,
                CreditDate = now,
                Language = "LT",
                Lines = new List<CreditNoteLineRequest> { new CreditNoteLineRequest { InvoiceLineId = invoiceLineId, Quantity = 330m } }
            }, 1);

            creditNoteNumber = creditNote.CreditNoteNumber;

            Assert.NotNull(creditNote);
            Assert.False(string.IsNullOrEmpty(creditNote.CreditNoteNumber));

            // Act 2: update the created note, increasing the line to the full quantity (660).
            // CreateCreditNoteAsync returns the raw entity whose Lines collection is not populated,
            // so fetch the persisted line id via the detail DTO.
            var detail = await service.GetCreditNoteAsync(creditNote.Id);
            await service.UpdateCreditNoteAsync(new UpdateCreditNoteRequest
            {
                Id = creditNote.Id,
                CustomerId = partnerId,
                OriginalInvoiceId = invoiceId,
                CreditDate = now,
                Language = "LT",
                Status = creditNote.Status,
                ReverseCharge = false,
                Lines = new List<CreditNoteLineRequest> { new CreditNoteLineRequest { Id = detail.Lines.First().Id, InvoiceLineId = invoiceLineId, Quantity = 660m } }
            }, 1);

            // Assert: read back with a BRAND NEW DbContext (AsNoTracking) — the RC96 line must be
            // stored NET-only after the update: 21% rate, zero VAT, subtotal == total.
            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
            var line = await verifyContext.CreditNoteLines
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.CreditNoteId == creditNote.Id);

            Assert.NotNull(line);
            Assert.Equal(21m, line!.VatRate);
            Assert.Equal(0m, line.VatAmount);
            Assert.Equal(66.00m, line.LineSubtotal);
            Assert.Equal(66.00m, line.LineTotal);

            var note = await verifyContext.CreditNotes
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == creditNote.Id);

            Assert.NotNull(note);
            Assert.Equal(66.00m, note!.SubtotalExclVat);
            Assert.Equal(0m, note.TotalVat);
            Assert.Equal(66.00m, note.TotalInclVat);
        }
        finally
        {
            await CleanupPdfSeedAsync(partnerId, invoiceId, creditNoteNumber);
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

    // company_settings is a PERSISTENT seed row shared by the whole test suite: xUnit runs
    // different test classes in parallel against the same test DB, so another class's test may
    // depend on a row this test created. No test cleanup may delete it (2026-09-12
    // "Company settings not found in database" race).
    private async Task EnsureCompanySettingsAsync()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var existing = await context.CompanySettings.FirstOrDefaultAsync();
        if (existing != null)
            return;

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

        // The service resolves a created credit note's currency as invoice.CurrencyId ?? 1.
        // Without a valid currency_id here the note would reference a nonexistent currency,
        // and GetCreditNoteAsync's required Currency include (INNER JOIN) would drop the row.
        // SeedPdfCustomerAsync already inserted the 'TST' currency, so link the invoice to it.
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO invoices (invoice_number, invoice_date, customer_id, currency_id, language, invoice_type, reverse_charge, subtotal_excl_vat, total_vat, total_incl_vat) VALUES ({0}, {1}, {2}, (SELECT id FROM currencies WHERE code = {3}), {4}, {5}, {6}, {7}, {8}, {9})",
            invoiceNumber, date, customerId, "TST", "LT", invoiceType, reverseCharge ? 1 : 0, subtotal, vat, total);

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

    private async Task CleanupPdfSeedAsync(int partnerId, int invoiceId, string creditNoteNumber)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        if (creditNoteNumber.Length > 0)
            await context.Database.ExecuteSqlRawAsync("DELETE FROM credit_note_lines WHERE credit_note_id IN (SELECT id FROM credit_notes WHERE credit_note_number = {0})", creditNoteNumber);
        if (creditNoteNumber.Length > 0)
            await context.Database.ExecuteSqlRawAsync("DELETE FROM credit_notes WHERE credit_note_number = {0}", creditNoteNumber);
        if (invoiceId > 0)
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM credit_note_lines WHERE credit_note_id IN (SELECT id FROM credit_notes WHERE applied_invoice_id = {0})",
                invoiceId);
        if (invoiceId > 0)
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM credit_notes WHERE applied_invoice_id = {0}", invoiceId);
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
    }

    [Fact]
    public async Task GenerateCreditNotePdf_Rc96_ShowsDerivedVatAndPayableLabel()
    {
        var now = DateTime.UtcNow;
        var invoiceNumber = $"INV-CNRC96-{now.Ticks}";
        var creditNoteNumber = $"CN-RC96-{now.Ticks}";

        await EnsureCompanySettingsAsync();
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
            await CleanupPdfSeedAsync(partnerId, invoiceId, creditNoteNumber);
        }
    }

    [Fact]
    public async Task GenerateCreditNotePdf_Ulak6_KeepsRealVatAndNoRc96Labels()
    {
        var now = DateTime.UtcNow;
        var invoiceNumber = $"INV-CNULAK-{now.Ticks}";
        var creditNoteNumber = $"CN-ULAK-{now.Ticks}";

        await EnsureCompanySettingsAsync();
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
            await CleanupPdfSeedAsync(partnerId, invoiceId, creditNoteNumber);
        }
    }

    [Fact]
    public async Task GenerateCreditNotePdf_Standard_ShowsRealVatAndNoRc96Labels()
    {
        var now = DateTime.UtcNow;
        var invoiceNumber = $"INV-CNSTD-{now.Ticks}";
        var creditNoteNumber = $"CN-STD-{now.Ticks}";

        await EnsureCompanySettingsAsync();
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
            await CleanupPdfSeedAsync(partnerId, invoiceId, creditNoteNumber);
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

    [Fact]
    public async Task GenerateNextNumberAsync_CommittedTransaction_ThrowsInvalidOperationException()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var generator = new CreditNoteNumberGenerator(context);

        var tx = context.Database.BeginTransaction();
        await tx.CommitAsync();

        // Premise check: a committed ADO.NET transaction has released its connection reference
        // (Pomelo 8.0.0 / MySqlConnector nulls the transaction's Connection on commit).
        Assert.Null(tx.GetDbTransaction().Connection);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            generator.GenerateNextNumberAsync(DateTime.UtcNow, tx));

        Assert.Equal("Transakcija neturi aktyvaus duomenų bazės ryšio — kreditinės numeris negali būti sugeneruotas.", ex.Message);
    }
}
