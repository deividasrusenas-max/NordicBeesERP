using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Header arithmetic gate (D-028): AMOUNT_ARITHMETIC_MISMATCH / MISSING_MONEY_FIELD are
/// recomputed from the final amounts and send the invoice to NEEDS_REVIEW — at creation,
/// on manual edit, and after a supplier is assigned later. Integration tests against
/// nordic_bees_erp_test.
/// </summary>
public class ExpenseStatusGateTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ExpenseStatusGateTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    private ExpenseService CreateService() =>
        new(_fixture.Factory, new NullAuthService(), new DefaultCompanySettingsService());

    private async Task<int> InsertSupplierAsync()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Supplier,
            Name = $"GateTest Supplier {Guid.NewGuid():N}",
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

    private async Task<int> InsertInvoiceAsync(string status, int? supplierId, IEnumerable<string> flags,
        decimal excl, decimal vat, decimal incl, string? pendingSupplierVat = null)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var number = $"GATE-{Guid.NewGuid():N}";
        var date = DateTime.Today;

        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices " +
            "(invoice_number, invoice_date, due_date, amount_excl_vat, vat_rate, vat_amount, amount_incl_vat, status, supplier_id, ocr_flags, pending_supplier_vat, currency, source, ocr_status, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}, NOW(), NOW())",
            number, date, date.AddDays(30), excl, 21m, vat, incl, status, supplierId,
            JsonSerializer.Serialize(flags), pendingSupplierVat, "EUR", "MANUAL", "COMPLETED");

        return await context.ExpenseInvoices
            .Where(i => i.InvoiceNumber == number)
            .Select(i => i.Id)
            .FirstAsync();
    }

    private async Task<ExpenseInvoice> ReloadAsync(int id)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoices.AsNoTracking().FirstAsync(i => i.Id == id);
    }

    private static List<string> FlagsOf(ExpenseInvoice invoice) =>
        string.IsNullOrEmpty(invoice.OcrFlags) ? new() : JsonSerializer.Deserialize<List<string>>(invoice.OcrFlags) ?? new();

    private async Task CleanupAsync(IEnumerable<int> invoiceIds, int? supplierId)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        foreach (var id in invoiceIds)
        {
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_audit WHERE invoice_id = {0}", id);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_lines WHERE invoice_id = {0}", id);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE id = {0}", id);
        }
        if (supplierId.HasValue)
            await context.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", supplierId.Value);
    }

    private static OcrResultDto NewOcrResult(int? supplierId, decimal excl, decimal vat, decimal incl) => new()
    {
        InvoiceNumber = $"GATE-OCR-{Guid.NewGuid():N}",
        InvoiceDate = DateTime.Today.ToString("yyyy-MM-dd"),
        DueDate = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd"),
        Currency = "EUR",
        AmountExclVat = excl,
        VatRate = 21m,
        VatAmount = vat,
        AmountInclVat = incl,
        SupplierId = supplierId,
        SupplierName = "GateTest OCR Supplier",
        Confidence = new OcrConfidenceDto { Amounts = 90, InvoiceNumber = 90, SupplierName = 90, InvoiceDate = 90 }
    };

    // ---------- CreateFromOcrAsync ----------

    [Fact]
    public async Task CreateFromOcr_ZeroNetWithHugeGross_NeedsReviewWithMissingMoneyField()
    {
        var supplierId = await InsertSupplierAsync();
        var created = new List<int>();
        try
        {
            // Production invoice 213: excl 0,00 / incl 465 374,45
            var invoice = await CreateService().CreateFromOcrAsync(NewOcrResult(supplierId, 0m, 0m, 465374.45m));
            created.Add(invoice.Id);

            var reloaded = await ReloadAsync(invoice.Id);
            Assert.Equal("NEEDS_REVIEW", reloaded.Status);
            Assert.Contains(OcrFlag.MissingMoneyField, FlagsOf(reloaded));
        }
        finally
        {
            await CleanupAsync(created, supplierId);
        }
    }

    [Fact]
    public async Task CreateFromOcr_HeaderDoesNotReconcile_NeedsReviewWithArithmeticMismatch()
    {
        var supplierId = await InsertSupplierAsync();
        var created = new List<int>();
        try
        {
            // 803,31 + 168,69 = 972,00 ≠ 1 000,00
            var invoice = await CreateService().CreateFromOcrAsync(NewOcrResult(supplierId, 803.31m, 168.69m, 1000m));
            created.Add(invoice.Id);

            var reloaded = await ReloadAsync(invoice.Id);
            Assert.Equal("NEEDS_REVIEW", reloaded.Status);
            Assert.Contains(OcrFlag.AmountArithmeticMismatch, FlagsOf(reloaded));
        }
        finally
        {
            await CleanupAsync(created, supplierId);
        }
    }

    [Fact]
    public async Task CreateFromOcr_ConsistentAmounts_NoArithmeticFlag_StatusFromOtherRules()
    {
        var supplierId = await InsertSupplierAsync();
        var created = new List<int>();
        try
        {
            var withSupplier = await CreateService().CreateFromOcrAsync(NewOcrResult(supplierId, 100m, 21m, 121m));
            created.Add(withSupplier.Id);
            var withoutSupplier = await CreateService().CreateFromOcrAsync(NewOcrResult(null, 100m, 21m, 121m));
            created.Add(withoutSupplier.Id);

            var a = await ReloadAsync(withSupplier.Id);
            Assert.Equal("PENDING", a.Status);
            Assert.DoesNotContain(OcrFlag.AmountArithmeticMismatch, FlagsOf(a));
            Assert.DoesNotContain(OcrFlag.MissingMoneyField, FlagsOf(a));

            var b = await ReloadAsync(withoutSupplier.Id);
            Assert.Equal("PENDING_SUPPLIER", b.Status);
        }
        finally
        {
            await CleanupAsync(created, supplierId);
        }
    }

    [Fact]
    public async Task CreateFromOcr_StaleOcrTimeFlag_IsRecomputedFromFinalAmounts()
    {
        var supplierId = await InsertSupplierAsync();
        var created = new List<int>();
        try
        {
            // OCR saw a missing amount; the user corrected the amounts in the dialog before saving.
            var ocr = NewOcrResult(supplierId, 100m, 21m, 121m);
            ocr.Flags.Add(OcrFlag.MissingMoneyField);

            var invoice = await CreateService().CreateFromOcrAsync(ocr);
            created.Add(invoice.Id);

            var reloaded = await ReloadAsync(invoice.Id);
            Assert.Equal("PENDING", reloaded.Status);
            Assert.DoesNotContain(OcrFlag.MissingMoneyField, FlagsOf(reloaded));
        }
        finally
        {
            await CleanupAsync(created, supplierId);
        }
    }

    // ---------- UpdateInvoiceAsync (manual edit) ----------

    private static ExpenseInvoice EditOf(ExpenseInvoice stored, decimal excl, decimal vat, decimal incl) => new()
    {
        Id = stored.Id,
        SupplierId = stored.SupplierId,
        InvoiceNumber = stored.InvoiceNumber,
        InvoiceDate = stored.InvoiceDate,
        DueDate = stored.DueDate,
        AmountExclVat = excl,
        VatRate = 21m,
        VatAmount = vat,
        AmountInclVat = incl,
        Status = stored.Status,
        OcrFlags = stored.OcrFlags
    };

    [Fact]
    public async Task UpdateInvoice_CorrectedAmounts_ClearsGate_MovesToPending()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("NEEDS_REVIEW", supplierId, new[] { OcrFlag.MissingMoneyField }, 0m, 0m, 465374.45m);
        try
        {
            var stored = await ReloadAsync(id);
            await CreateService().UpdateInvoiceAsync(EditOf(stored, 100m, 21m, 121m));

            var reloaded = await ReloadAsync(id);
            Assert.Equal("PENDING", reloaded.Status);
            Assert.DoesNotContain(OcrFlag.MissingMoneyField, FlagsOf(reloaded));
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    [Fact]
    public async Task UpdateInvoice_CorrectedAmounts_NoSupplier_GoesToPendingSupplier()
    {
        var id = await InsertInvoiceAsync("NEEDS_REVIEW", null, new[] { OcrFlag.AmountArithmeticMismatch }, 803.31m, 168.69m, 1000m);
        try
        {
            var stored = await ReloadAsync(id);
            await CreateService().UpdateInvoiceAsync(EditOf(stored, 831.31m, 168.69m, 1000m));

            var reloaded = await ReloadAsync(id);
            Assert.Equal("PENDING_SUPPLIER", reloaded.Status);
            Assert.DoesNotContain(OcrFlag.AmountArithmeticMismatch, FlagsOf(reloaded));
        }
        finally
        {
            await CleanupAsync(new[] { id }, null);
        }
    }

    [Fact]
    public async Task UpdateInvoice_AmountsStillBroken_StaysNeedsReview()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("NEEDS_REVIEW", supplierId, new[] { OcrFlag.MissingMoneyField }, 0m, 0m, 465374.45m);
        try
        {
            var stored = await ReloadAsync(id);
            await CreateService().UpdateInvoiceAsync(EditOf(stored, 803.31m, 168.69m, 1000m));

            var reloaded = await ReloadAsync(id);
            Assert.Equal("NEEDS_REVIEW", reloaded.Status);
            Assert.Contains(OcrFlag.AmountArithmeticMismatch, FlagsOf(reloaded));
            Assert.DoesNotContain(OcrFlag.MissingMoneyField, FlagsOf(reloaded));
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    // ---------- Supplier assignment must not bypass the gate ----------

    [Fact]
    public async Task AssignSupplier_InvoiceWithMissingMoneyField_GoesToNeedsReview()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("PENDING_SUPPLIER", null,
            new[] { OcrFlag.VendorNotFound, OcrFlag.MissingMoneyField }, 0m, 0m, 105.41m);
        try
        {
            await CreateService().AssignSupplierAsync(id, supplierId, "TEST");

            var reloaded = await ReloadAsync(id);
            Assert.Equal("NEEDS_REVIEW", reloaded.Status);
            Assert.Equal(supplierId, reloaded.SupplierId);
            Assert.DoesNotContain(OcrFlag.VendorNotFound, FlagsOf(reloaded));
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    [Fact]
    public async Task AssignSupplier_NoReviewFlags_GoesToPending()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("PENDING_SUPPLIER", null, new[] { OcrFlag.VendorNotFound }, 100m, 21m, 121m);
        try
        {
            await CreateService().AssignSupplierAsync(id, supplierId, "TEST");

            var reloaded = await ReloadAsync(id);
            Assert.Equal("PENDING", reloaded.Status);
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    [Fact]
    public async Task AutoAssignSupplier_InvoiceWithMissingMoneyField_GoesToNeedsReview()
    {
        var supplierId = await InsertSupplierAsync();
        var vat = "LT9" + Random.Shared.NextInt64(10_000_000_000, 99_999_999_999);
        var gated = await InsertInvoiceAsync("PENDING_SUPPLIER", null,
            new[] { OcrFlag.VendorNotFound, OcrFlag.MissingMoneyField }, 0m, 0m, 105.41m, vat);
        var clean = await InsertInvoiceAsync("PENDING_SUPPLIER", null,
            new[] { OcrFlag.VendorNotFound }, 100m, 21m, 121m, vat);
        try
        {
            var count = await CreateService().AutoAssignSupplierAsync(vat, null, supplierId);
            Assert.Equal(2, count);

            Assert.Equal("NEEDS_REVIEW", (await ReloadAsync(gated)).Status);
            Assert.Equal("PENDING", (await ReloadAsync(clean)).Status);
        }
        finally
        {
            await CleanupAsync(new[] { gated, clean }, supplierId);
        }
    }

    // ============ Stub implementations ============

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
}
