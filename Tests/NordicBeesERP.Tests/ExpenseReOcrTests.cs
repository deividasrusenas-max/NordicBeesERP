using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Etapas 0c C1: re-OCR (UpdateFromOcrAsync) must not release quarantined invoices (D-027)
/// or overwrite paid ones, and a duplicate found on re-OCR quarantines the invoice exactly
/// like CreateFromOcrAsync. Integration tests against nordic_bees_erp_test.
/// </summary>
public class ExpenseReOcrTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ExpenseReOcrTests(DbTestFixture fixture)
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
            Name = $"ReOcr Supplier {Guid.NewGuid():N}",
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

    private async Task<int> InsertInvoiceAsync(string status, int? supplierId, string? number = null,
        decimal incl = 50m, string? rejectedReason = null)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        number ??= $"REOCR-{Guid.NewGuid():N}";
        var marker = $"REOCR-MARK-{Guid.NewGuid():N}";
        var date = DateTime.Today;
        var excl = Math.Round(incl / 1.21m, 2);

        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices " +
            "(invoice_number, invoice_date, due_date, amount_excl_vat, vat_rate, vat_amount, amount_incl_vat, status, supplier_id, rejected_reason, notes, currency, source, ocr_status, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}, NOW(), NOW())",
            number, date, date.AddDays(30), excl, 21m, incl - excl, incl, status, supplierId, rejectedReason, marker,
            "EUR", "MANUAL", "COMPLETED");

        return await context.ExpenseInvoices.Where(i => i.Notes == marker).Select(i => i.Id).FirstAsync();
    }

    private static OcrResultDto NewOcrResult(int? supplierId, string? number = null, decimal incl = 121m) => new()
    {
        InvoiceNumber = number ?? $"REOCR-OCR-{Guid.NewGuid():N}",
        InvoiceDate = DateTime.Today.ToString("yyyy-MM-dd"),
        DueDate = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd"),
        Currency = "EUR",
        AmountExclVat = Math.Round(incl / 1.21m, 2),
        VatRate = 21m,
        VatAmount = incl - Math.Round(incl / 1.21m, 2),
        AmountInclVat = incl,
        SupplierId = supplierId,
        SupplierName = "ReOcr OCR Supplier",
        Confidence = new OcrConfidenceDto { Amounts = 90, InvoiceNumber = 90, SupplierName = 90, InvoiceDate = 90 }
    };

    private async Task<ExpenseInvoice> ReloadAsync(int id)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoices.AsNoTracking().FirstAsync(i => i.Id == id);
    }

    private async Task<List<ExpenseInvoiceAudit>> AuditsAsync(int id)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoiceAudits.AsNoTracking().Where(a => a.InvoiceId == id).ToListAsync();
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

    [Theory]
    [InlineData("PAID")]
    [InlineData("PARTIAL")]
    [InlineData("OVERDUE")]
    public async Task PaidInvoice_Refused_NothingWritten(string status)
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync(status, supplierId, incl: 50m);
        try
        {
            var before = await ReloadAsync(id);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateService().UpdateFromOcrAsync(id, NewOcrResult(supplierId, incl: 999m)));
            Assert.Equal("Apmokėtos sąskaitos OCR pakartoti negalima", ex.Message);

            var after = await ReloadAsync(id);
            Assert.Equal(status, after.Status);
            Assert.Equal(before.AmountInclVat, after.AmountInclVat);
            Assert.Equal(before.InvoiceNumber, after.InvoiceNumber);
            Assert.Empty(await AuditsAsync(id));
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    [Fact]
    public async Task RejectedInvoice_DataUpdated_StatusAndReasonKept_Audited()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("REJECTED", supplierId, rejectedReason: "Dublikatas: X-1");
        try
        {
            var ocr = NewOcrResult(supplierId, incl: 121m);
            await CreateService().UpdateFromOcrAsync(id, ocr);

            var after = await ReloadAsync(id);
            Assert.Equal("REJECTED", after.Status);
            Assert.Equal("Dublikatas: X-1", after.RejectedReason);
            Assert.Equal(121m, after.AmountInclVat);
            Assert.Equal(ocr.InvoiceNumber, after.InvoiceNumber);

            var audit = Assert.Single(await AuditsAsync(id), a => a.Action == "OCR_RETRIED");
            Assert.Equal("REJECTED", audit.NewStatus);
            Assert.Contains("paliktas", audit.ActionDetails);
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    [Fact]
    public async Task DuplicatePendingInvoice_DataUpdated_StatusKept()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("DUPLICATE_PENDING", supplierId);
        try
        {
            await CreateService().UpdateFromOcrAsync(id, NewOcrResult(supplierId, incl: 121m));

            var after = await ReloadAsync(id);
            Assert.Equal("DUPLICATE_PENDING", after.Status);
            Assert.Equal(121m, after.AmountInclVat);
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    [Fact]
    public async Task DuplicateOfAnotherInvoiceFound_GoesToDuplicatePending()
    {
        var supplierId = await InsertSupplierAsync();
        var number = $"REOCR-DUP-{Guid.NewGuid():N}";
        var original = await InsertInvoiceAsync("PENDING", supplierId, number, incl: 121m);
        var target = await InsertInvoiceAsync("NEEDS_REVIEW", supplierId, incl: 50m);
        try
        {
            await CreateService().UpdateFromOcrAsync(target, NewOcrResult(supplierId, number, 121m));

            var after = await ReloadAsync(target);
            Assert.Equal("DUPLICATE_PENDING", after.Status);
            Assert.Contains(OcrFlag.Duplicate, FlagsOf(after));
            Assert.Equal("PENDING", (await ReloadAsync(original)).Status);
        }
        finally
        {
            await CleanupAsync(new[] { original, target }, supplierId);
        }
    }

    [Fact]
    public async Task ReOcrOfItself_IsNotADuplicate()
    {
        var supplierId = await InsertSupplierAsync();
        var number = $"REOCR-SELF-{Guid.NewGuid():N}";
        var id = await InsertInvoiceAsync("NEEDS_REVIEW", supplierId, number, incl: 121m);
        try
        {
            await CreateService().UpdateFromOcrAsync(id, NewOcrResult(supplierId, number, 121m));

            var after = await ReloadAsync(id);
            Assert.Equal("PENDING", after.Status);
            Assert.DoesNotContain(OcrFlag.Duplicate, FlagsOf(after));
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    [Fact]
    public async Task NormalInvoice_StatusRuleDerived()
    {
        var supplierId = await InsertSupplierAsync();
        var clean = await InsertInvoiceAsync("NEEDS_REVIEW", supplierId);
        var gated = await InsertInvoiceAsync("PENDING", supplierId);
        var noSupplier = await InsertInvoiceAsync("PENDING", null);
        try
        {
            await CreateService().UpdateFromOcrAsync(clean, NewOcrResult(supplierId, incl: 121m));
            Assert.Equal("PENDING", (await ReloadAsync(clean)).Status);

            var broken = NewOcrResult(supplierId, incl: 121m);
            broken.AmountExclVat = 0m; // MISSING_MONEY_FIELD
            await CreateService().UpdateFromOcrAsync(gated, broken);
            Assert.Equal("NEEDS_REVIEW", (await ReloadAsync(gated)).Status);

            await CreateService().UpdateFromOcrAsync(noSupplier, NewOcrResult(null, incl: 121m));
            Assert.Equal("PENDING_SUPPLIER", (await ReloadAsync(noSupplier)).Status);
        }
        finally
        {
            await CleanupAsync(new[] { clean, gated, noSupplier }, supplierId);
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
