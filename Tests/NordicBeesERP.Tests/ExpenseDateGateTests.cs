using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Helpers;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Invoice-date gates (Etapas 0b, B1): FUTURE_DATE, STALE_DATE and MISSING_INV_DATE are
/// recomputed from the final date and send the invoice to NEEDS_REVIEW; correcting the date
/// on manual edit clears the gate. Production evidence: invoices 167, 168, 341.
/// Integration tests against nordic_bees_erp_test.
/// </summary>
[Collection("RealDatabase")]
public class ExpenseDateGateTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ExpenseDateGateTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    private static DateTime VilniusToday => LithuanianTimeHelper.ToLithuanianTime(DateTime.UtcNow).Date;

    private ExpenseService CreateService() =>
        new(_fixture.Factory, new NullAuthService(), new DefaultCompanySettingsService());

    private async Task<int> InsertSupplierAsync()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Supplier,
            Name = $"DateGate Supplier {Guid.NewGuid():N}",
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

    private static OcrResultDto NewOcrResult(int supplierId, string invoiceDate) => new()
    {
        InvoiceNumber = $"DATE-OCR-{Guid.NewGuid():N}",
        InvoiceDate = invoiceDate,
        DueDate = VilniusToday.AddDays(30).ToString("yyyy-MM-dd"),
        Currency = "EUR",
        AmountExclVat = 100m,
        VatRate = 21m,
        VatAmount = 21m,
        AmountInclVat = 121m,
        SupplierId = supplierId,
        SupplierName = "DateGate OCR Supplier",
        Confidence = new OcrConfidenceDto { Amounts = 90, InvoiceNumber = 90, SupplierName = 90, InvoiceDate = 90 }
    };

    private async Task<ExpenseInvoice> ReloadAsync(int id)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoices.AsNoTracking().FirstAsync(i => i.Id == id);
    }

    private static List<string> FlagsOf(ExpenseInvoice invoice) =>
        string.IsNullOrEmpty(invoice.OcrFlags) ? new() : JsonSerializer.Deserialize<List<string>>(invoice.OcrFlags) ?? new();

    private async Task CleanupAsync(int? invoiceId, int supplierId)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        if (invoiceId.HasValue)
        {
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_audit WHERE invoice_id = {0}", invoiceId.Value);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_lines WHERE invoice_id = {0}", invoiceId.Value);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE id = {0}", invoiceId.Value);
        }
        await context.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", supplierId);
    }

    private async Task<ExpenseInvoice> CreateWithDateAsync(int supplierId, string invoiceDate)
    {
        var invoice = await CreateService().CreateFromOcrAsync(NewOcrResult(supplierId, invoiceDate));
        return await ReloadAsync(invoice.Id);
    }

    [Fact]
    public async Task FutureDate_FlagAndNeedsReview()
    {
        var supplierId = await InsertSupplierAsync();
        int? id = null;
        try
        {
            var reloaded = await CreateWithDateAsync(supplierId, VilniusToday.AddDays(1).ToString("yyyy-MM-dd"));
            id = reloaded.Id;

            Assert.Equal("NEEDS_REVIEW", reloaded.Status);
            Assert.Contains(OcrFlag.FutureDate, FlagsOf(reloaded));
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Fact]
    public async Task Today_NoDateFlag()
    {
        var supplierId = await InsertSupplierAsync();
        int? id = null;
        try
        {
            var reloaded = await CreateWithDateAsync(supplierId, VilniusToday.ToString("yyyy-MM-dd"));
            id = reloaded.Id;

            Assert.Equal("PENDING", reloaded.Status);
            Assert.DoesNotContain(OcrFlag.FutureDate, FlagsOf(reloaded));
            Assert.DoesNotContain(OcrFlag.StaleDate, FlagsOf(reloaded));
            Assert.DoesNotContain(OcrFlag.MissingInvDate, FlagsOf(reloaded));
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Fact]
    public async Task NineteenMonthsOld_StaleDate()
    {
        var supplierId = await InsertSupplierAsync();
        int? id = null;
        try
        {
            var reloaded = await CreateWithDateAsync(supplierId, VilniusToday.AddMonths(-19).ToString("yyyy-MM-dd"));
            id = reloaded.Id;

            Assert.Equal("NEEDS_REVIEW", reloaded.Status);
            Assert.Contains(OcrFlag.StaleDate, FlagsOf(reloaded));
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Fact]
    public async Task UnparseableDate_MissingInvDate_PlaceholderKept()
    {
        var supplierId = await InsertSupplierAsync();
        int? id = null;
        try
        {
            var reloaded = await CreateWithDateAsync(supplierId, "not-a-date");
            id = reloaded.Id;

            Assert.Equal("NEEDS_REVIEW", reloaded.Status);
            Assert.Contains(OcrFlag.MissingInvDate, FlagsOf(reloaded));
            Assert.NotEqual(default, reloaded.InvoiceDate); // non-null column still gets a placeholder
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Fact]
    public async Task UpdateInvoice_CorrectedDate_ClearsFutureDate()
    {
        var supplierId = await InsertSupplierAsync();
        int? id = null;
        try
        {
            var stored = await CreateWithDateAsync(supplierId, VilniusToday.AddMonths(2).ToString("yyyy-MM-dd"));
            id = stored.Id;
            Assert.Equal("NEEDS_REVIEW", stored.Status);

            await CreateService().UpdateInvoiceAsync(new ExpenseInvoice
            {
                Id = stored.Id,
                SupplierId = stored.SupplierId,
                InvoiceNumber = stored.InvoiceNumber,
                InvoiceDate = VilniusToday,
                DueDate = stored.DueDate,
                AmountExclVat = stored.AmountExclVat,
                VatRate = stored.VatRate,
                VatAmount = stored.VatAmount,
                AmountInclVat = stored.AmountInclVat,
                Status = stored.Status,
                OcrFlags = stored.OcrFlags
            });

            var reloaded = await ReloadAsync(stored.Id);
            Assert.DoesNotContain(OcrFlag.FutureDate, FlagsOf(reloaded));
            Assert.Equal("PENDING", reloaded.Status);
        }
        finally
        {
            await CleanupAsync(id, supplierId);
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
