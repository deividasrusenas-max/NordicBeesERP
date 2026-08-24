using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
using NordicBeesERP.Services.Dtos;
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
                new TestPdfGeneratorService());
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
                new TestPdfGeneratorService());

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
        public byte[] GenerateInvoicePdf(int invoiceId) => Array.Empty<byte>();
        public Task<byte[]> GenerateInvoicePdfAsync(int invoiceId) => Task.FromResult(Array.Empty<byte>());
        public Task<byte[]> GenerateCreditNotePdfAsync(CreditNote creditNote, List<CreditNoteLineDto> lines, BusinessPartner? customer, Currency? currency, string? originalInvoiceNumber, DateTime? originalInvoiceDate, string? appliedInvoiceNumber, string? createdByName)
            => Task.FromResult(Array.Empty<byte>());
        public string GetPdfPath(string creditNoteNumber) => "/tmp/test.pdf";
        public Task<byte[]> GenerateMultipleInvoicesPdfAsync(List<int> invoiceIds) => Task.FromResult(Array.Empty<byte>());
    }
}
