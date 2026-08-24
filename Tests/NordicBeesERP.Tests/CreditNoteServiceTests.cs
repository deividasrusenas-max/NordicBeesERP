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
    }
}
