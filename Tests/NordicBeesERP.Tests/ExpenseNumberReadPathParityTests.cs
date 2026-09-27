using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// OCR Etapas 1 S7(b) — path parity (PLAN-ETAPAS1 §0): the locale-number flags (D-041) come out the same on OCR create
/// and on re-OCR, from the ASF0021438 DTO (both lines present). The edit path carries the stored
/// flags over (not recomputed; a human editing the value is the resolution — PATVIRTINTI clears the review). Integration
/// tests against nordic_bees_erp_test.
/// </summary>
[Collection("RealDatabase")]
public class ExpenseNumberReadPathParityTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ExpenseNumberReadPathParityTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    public enum Path { Create, ReOcr }

    private sealed class NullAuthService : IAuthService
    {
        public Task<ErpUser?> ValidateUserAsync(string email, string password) => Task.FromResult<ErpUser?>(null);
        public Task SeedAdminAsync(string email, string password) => Task.CompletedTask;
        public Task<ErpUser?> GetAuthenticatedUserAsync() => Task.FromResult<ErpUser?>(null);
        public Task<int?> GetCustomerIdAsync() => Task.FromResult<int?>(null);
        public Task<int?> GetUserIdAsync() => Task.FromResult<int?>(null);
        public Task<ErpUser?> GetUserByIdAsync(int userId) => Task.FromResult<ErpUser?>(null);
        public Task<string> GetRequiredActorNameAsync() => throw new NotImplementedException();
    }

    private sealed class DefaultCompanySettingsService : ICompanySettingsService
    {
        public Task<CompanySettings> GetSettingsAsync() => Task.FromResult(new CompanySettings());
        public Task UpdateSettingsAsync(CompanySettings settings) => Task.CompletedTask;
    }

    private ExpenseService CreateService() =>
        new(_fixture.Factory, new NullAuthService(), new DefaultCompanySettingsService());

    private async Task<int> InsertSupplierAsync()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Supplier,
            Name = $"NumRead Supplier {Guid.NewGuid():N}",
            Country = "Lithuania",
            CountryCode = "LT",
            DefaultLanguage = "LT",
            PaymentTermDays = 14,
            DefaultVatRate = 21m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();
        return partner.Id;
    }

    private async Task<int> InsertOpenInvoiceAsync(int supplierId)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var marker = $"NUMREAD-MARK-{Guid.NewGuid():N}";
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices " +
            "(invoice_number, invoice_date, due_date, amount_excl_vat, vat_rate, vat_amount, amount_incl_vat, status, supplier_id, notes, currency, source, ocr_status, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, NOW(), NOW())",
            $"NUMREAD-{Guid.NewGuid():N}", DateTime.Today, DateTime.Today.AddDays(30), 100m, 21m, 21m, 121m, "PENDING", supplierId, marker,
            "EUR", "MANUAL", "COMPLETED");
        return await context.ExpenseInvoices.Where(i => i.Notes == marker).Select(i => i.Id).FirstAsync();
    }

    private async Task CleanupAsync(int? invoiceId, int? supplierId)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        if (invoiceId.HasValue)
        {
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_audit WHERE invoice_id = {0}", invoiceId.Value);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_lines WHERE invoice_id = {0}", invoiceId.Value);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE id = {0}", invoiceId.Value);
        }
        if (supplierId.HasValue)
            await context.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", supplierId.Value);
    }

    /// <summary>
    /// ASF0021438 as ProcessAsync fills the DTO (both lines present), pointed at a supplier so the status decision is
    /// not PENDING_SUPPLIER. The preview flags are what ProcessAsync's detection step sets.
    /// </summary>
    private static OcrResultDto Ocr(int supplierId)
    {
        var dto = OcrFixtures.Dto(OcrFixtures.Asf0021438());
        ExpenseService.RecomputeNumberReadFlags(dto.Flags, dto);
        dto.SupplierId = supplierId;
        dto.InvoiceNumber = $"NUMREAD-OCR-{Guid.NewGuid():N}";
        dto.InvoiceDate = DateTime.Today.ToString("yyyy-MM-dd");
        dto.DueDate = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd");
        dto.RawJson = OcrFixtures.Asf0021438();
        return dto;
    }

    /// <summary>Runs the OCR path under test and returns the stored invoice.</summary>
    private async Task<(ExpenseInvoice Invoice, int? ReOcrInvoiceId)> SaveAsync(Path path, OcrResultDto dto, int supplierId)
    {
        var service = CreateService();
        if (path == Path.Create)
            return (await service.CreateFromOcrAsync(dto), null);

        var id = await InsertOpenInvoiceAsync(supplierId);
        await service.UpdateFromOcrAsync(id, dto);
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return (await context.ExpenseInvoices.AsNoTracking().FirstAsync(i => i.Id == id), id);
    }

    private static List<string> FlagsOf(ExpenseInvoice invoice) =>
        string.IsNullOrEmpty(invoice.OcrFlags) ? new() : JsonSerializer.Deserialize<List<string>>(invoice.OcrFlags) ?? new();

    [Theory]
    [InlineData(Path.Create)]
    [InlineData(Path.ReOcr)]
    public async Task Asf0021438_StoresBothFlags_AndHoldsTheInvoiceInReview(Path path)
    {
        var supplierId = await InsertSupplierAsync();
        int? invoiceId = null;
        try
        {
            var dto = Ocr(supplierId);

            var (invoice, reOcrId) = await SaveAsync(path, dto, supplierId);
            invoiceId = reOcrId ?? invoice.Id;

            var flags = FlagsOf(invoice);
            Assert.Contains(OcrFlag.NumberMisread, flags);
            Assert.Contains(OcrFlag.NumberAmbiguous, flags);
            Assert.Equal("NEEDS_REVIEW", invoice.Status);
            Assert.NotNull(invoice.OcrRawJson); // the detail view recomputes its candidates from this
        }
        finally
        {
            await CleanupAsync(invoiceId, supplierId);
        }
    }

    [Theory]
    [InlineData(Path.Create)]
    [InlineData(Path.ReOcr)]
    public async Task HumanCorrectsBothQuantitiesBeforeSaving_NoNumberFlags(Path path)
    {
        var supplierId = await InsertSupplierAsync();
        int? invoiceId = null;
        try
        {
            var dto = Ocr(supplierId);
            Assert.Contains(OcrFlag.NumberMisread, dto.Flags); // the preview flags, as the upload dialog shows them
            dto.Lines[0].Quantity = 3888m;
            dto.Lines[1].Quantity = 9m;

            var (invoice, reOcrId) = await SaveAsync(path, dto, supplierId);
            invoiceId = reOcrId ?? invoice.Id;

            var flags = FlagsOf(invoice);
            Assert.DoesNotContain(OcrFlag.NumberMisread, flags);
            Assert.DoesNotContain(OcrFlag.NumberAmbiguous, flags);
        }
        finally
        {
            await CleanupAsync(invoiceId, supplierId);
        }
    }

    [Theory]
    [InlineData(Path.Create)]
    [InlineData(Path.ReOcr)]
    public async Task StalePreviewFlag_IsDroppedOnSave_WhenTheReadsAreClean(Path path)
    {
        var supplierId = await InsertSupplierAsync();
        int? invoiceId = null;
        try
        {
            var dto = Ocr(supplierId);
            dto.Lines[0].Quantity = 3888m;
            dto.Lines[1].Quantity = 9m;
            dto.Flags.Add(OcrFlag.NumberAmbiguous); // the dialog copied it from the preview and the person then resolved the values

            var (invoice, reOcrId) = await SaveAsync(path, dto, supplierId);
            invoiceId = reOcrId ?? invoice.Id;

            Assert.DoesNotContain(OcrFlag.NumberAmbiguous, FlagsOf(invoice));
        }
        finally
        {
            await CleanupAsync(invoiceId, supplierId);
        }
    }

    [Theory]
    [InlineData(Path.Create)]
    [InlineData(Path.ReOcr)]
    public async Task Asf0021438_ThroughProcessAsync_KeepsTheNineUnitLine_AndStoresAllFlags(Path path)
    {
        // S7(c): the same recorded response through ProcessAsync itself — the reconcile step no longer deletes the
        // real 9-unit line, so both paths store both lines, the two number flags and the information flag
        var supplierId = await InsertSupplierAsync();
        int? invoiceId = null;
        try
        {
            var dto = await new RecordedAzureOcrService(OcrFixtures.Asf0021438(), _fixture.Factory).ProcessAsync("", "fixture.pdf");
            dto.SupplierId = supplierId;
            dto.InvoiceNumber = $"NUMREAD-OCR-{Guid.NewGuid():N}";
            dto.InvoiceDate = DateTime.Today.ToString("yyyy-MM-dd");
            dto.DueDate = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd");
            Assert.Equal(2, dto.Lines.Count);

            var (invoice, reOcrId) = await SaveAsync(path, dto, supplierId);
            invoiceId = reOcrId ?? invoice.Id;

            var flags = FlagsOf(invoice);
            Assert.Contains(OcrFlag.NumberMisread, flags);
            Assert.Contains(OcrFlag.NumberAmbiguous, flags);
            Assert.Contains(OcrFlag.LineLargeQuantity, flags);
            Assert.Equal("NEEDS_REVIEW", invoice.Status);

            await using var context = await _fixture.Factory.CreateDbContextAsync();
            var quantities = await context.ExpenseInvoiceLines.AsNoTracking()
                .Where(l => l.InvoiceId == invoiceId.Value).OrderBy(l => l.SortOrder).Select(l => l.Quantity).ToListAsync();
            Assert.Equal(new decimal?[] { 3m, 9000m }, quantities.ToArray());
        }
        finally
        {
            await CleanupAsync(invoiceId, supplierId);
        }
    }

    [Fact]
    public async Task BothPathsStoreTheSameNumberFlags()
    {
        var supplierId = await InsertSupplierAsync();
        int? reOcrId = null;
        int? createdId = null;
        try
        {
            var (created, _) = await SaveAsync(Path.Create, Ocr(supplierId), supplierId);
            createdId = created.Id;
            var (reOcr, id) = await SaveAsync(Path.ReOcr, Ocr(supplierId), supplierId);
            reOcrId = id;

            static List<string> Number(ExpenseInvoice i) =>
                FlagsOf(i).Where(f => f is OcrFlag.NumberMisread or OcrFlag.NumberAmbiguous).OrderBy(f => f).ToList();
            Assert.Equal(Number(created), Number(reOcr));
            Assert.Equal(2, Number(created).Count);
        }
        finally
        {
            if (reOcrId.HasValue) await CleanupAsync(reOcrId, null);
            await CleanupAsync(createdId, supplierId);
        }
    }
}
