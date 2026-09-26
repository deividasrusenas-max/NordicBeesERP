using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using NordicBeesERP.Helpers;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// OCR Etapas 1 S2b: the new flag codes, their Lithuanian labels and colours, and their review vs
/// information classification (D-038). Nothing sets these flags yet, so the status tests seed them
/// as stored flags. The status tests are integration tests against nordic_bees_erp_test.
/// </summary>
public class ExpenseEtapas1FlagTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ExpenseEtapas1FlagTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    public static readonly TheoryData<string> ReviewFlags = new()
    {
        OcrFlag.TotalsOutOfRange, OcrFlag.InvalidIban, OcrFlag.InvalidVatFormat,
        OcrFlag.VatRateNotAllowed, OcrFlag.NumberMisread, OcrFlag.NumberAmbiguous
    };

    public static readonly TheoryData<string> InformationFlags = new()
    {
        OcrFlag.LineAmountImplausible, OcrFlag.VatFormatUnchecked, OcrFlag.VatRateUnchecked
    };

    // --- Codes, labels, colours (pure) ---

    [Fact]
    public void Codes_AreTheDocumentedStrings()
    {
        Assert.Equal("TOTALS_OUT_OF_RANGE", OcrFlag.TotalsOutOfRange);
        Assert.Equal("INVALID_IBAN", OcrFlag.InvalidIban);
        Assert.Equal("INVALID_VAT_FORMAT", OcrFlag.InvalidVatFormat);
        Assert.Equal("VAT_RATE_NOT_ALLOWED", OcrFlag.VatRateNotAllowed);
        Assert.Equal("NUMBER_MISREAD", OcrFlag.NumberMisread);
        Assert.Equal("NUMBER_AMBIGUOUS", OcrFlag.NumberAmbiguous);
        Assert.Equal("LINE_AMOUNT_IMPLAUSIBLE", OcrFlag.LineAmountImplausible);
        Assert.Equal("VAT_FORMAT_UNCHECKED", OcrFlag.VatFormatUnchecked);
        Assert.Equal("VAT_RATE_UNCHECKED", OcrFlag.VatRateUnchecked);
    }

    [Theory]
    [InlineData("TOTALS_OUT_OF_RANGE", "Sumos neįtikėtinai didelės")]
    [InlineData("INVALID_IBAN", "Neteisingas IBAN")]
    [InlineData("INVALID_VAT_FORMAT", "Neteisingas PVM kodo formatas")]
    [InlineData("VAT_RATE_NOT_ALLOWED", "PVM tarifas negalimas šaliai ir datai")]
    [InlineData("NUMBER_MISREAD", "Skaičius nesutampa su dokumento tekstu")]
    [InlineData("NUMBER_AMBIGUOUS", "Dviprasmiškas skaičius")]
    [InlineData("LINE_AMOUNT_IMPLAUSIBLE", "Eilutė: kiekis × kaina ≠ suma")]
    [InlineData("VAT_FORMAT_UNCHECKED", "PVM kodo formatas netikrintas")]
    [InlineData("VAT_RATE_UNCHECKED", "PVM tarifas netikrintas")]
    public void Labels_Lithuanian(string flag, string label)
    {
        Assert.Equal(label, ExpenseStatusHelper.GetFlagLabel(flag));
    }

    [Theory]
    [MemberData(nameof(ReviewFlags))]
    public void ReviewFlags_Critical_AndColoured(string flag)
    {
        Assert.True(ExpenseStatusHelper.IsCriticalFlag(flag));
        Assert.NotEqual(Color.Default, ExpenseStatusHelper.GetFlagColor(flag));
        // a critical flag makes even a paid invoice need attention
        Assert.True(ExpenseStatusHelper.NeedsAttention("PAID", JsonSerializer.Serialize(new[] { flag })));
    }

    [Theory]
    [MemberData(nameof(InformationFlags))]
    public void InformationFlags_NotCritical_DefaultColour(string flag)
    {
        Assert.False(ExpenseStatusHelper.IsCriticalFlag(flag));
        Assert.Equal(Color.Default, ExpenseStatusHelper.GetFlagColor(flag));
        Assert.False(ExpenseStatusHelper.NeedsAttention("PAID", JsonSerializer.Serialize(new[] { flag })));
    }

    // --- Status classification (DB) ---

    private ExpenseService CreateService() =>
        new(_fixture.Factory, new NullAuthService(), new DefaultCompanySettingsService());

    private async Task<int> InsertSupplierAsync()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Supplier,
            Name = $"Etapas1Flag Supplier {Guid.NewGuid():N}",
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

    /// <summary>A consistent 100 / 21 / 121 invoice with one matching line and the given stored flags.</summary>
    private async Task<int> InsertInvoiceAsync(int? supplierId, string status, params string[] flags)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var number = $"E1FLAG-{Guid.NewGuid():N}";
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices " +
            "(invoice_number, invoice_date, due_date, amount_excl_vat, vat_rate, vat_amount, amount_incl_vat, status, supplier_id, ocr_flags, currency, source, ocr_status, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, NOW(), NOW())",
            number, DateTime.Today, DateTime.Today.AddDays(30), 100m, 21m, 21m, 121m, status, supplierId,
            JsonSerializer.Serialize(flags), "EUR", "MANUAL", "COMPLETED");
        var id = await context.ExpenseInvoices.Where(i => i.InvoiceNumber == number).Select(i => i.Id).FirstAsync();
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoice_lines (invoice_id, description, quantity, amount_excl_vat, vat_rate, amount_incl_vat, sort_order) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6})",
            id, "Eilutė", 1m, 100m, 21m, 121m, 1);
        return id;
    }

    private async Task<ExpenseInvoice> ReloadAsync(int id)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoices.AsNoTracking().FirstAsync(i => i.Id == id);
    }

    private async Task<List<ExpenseInvoiceLine>> LinesAsync(int invoiceId)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoiceLines.AsNoTracking().Where(l => l.InvoiceId == invoiceId).ToListAsync();
    }

    private async Task CleanupAsync(int invoiceId, int supplierId)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_audit WHERE invoice_id = {0}", invoiceId);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_lines WHERE invoice_id = {0}", invoiceId);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE id = {0}", invoiceId);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", supplierId);
    }

    [Theory]
    [MemberData(nameof(ReviewFlags))]
    public async Task EditSave_StoredReviewFlag_NeedsReview(string flag)
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync(supplierId, "PENDING", flag);
        try
        {
            await CreateService().SaveInvoiceEditAsync(await ReloadAsync(id), await LinesAsync(id), "Test User");

            Assert.Equal("NEEDS_REVIEW", (await ReloadAsync(id)).Status);
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Theory]
    [MemberData(nameof(InformationFlags))]
    public async Task EditSave_StoredInformationFlag_StaysPending(string flag)
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync(supplierId, "NEEDS_REVIEW", flag);
        try
        {
            await CreateService().SaveInvoiceEditAsync(await ReloadAsync(id), await LinesAsync(id), "Test User");

            var after = await ReloadAsync(id);
            Assert.Equal("PENDING", after.Status);
            Assert.Contains(flag, after.OcrFlags ?? "");
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Theory]
    [InlineData("INVALID_IBAN")]
    [InlineData("INVALID_VAT_FORMAT")]
    public async Task AssignSupplier_DocumentIdentityFlag_BecomesInformation(string flag)
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync(null, "PENDING_SUPPLIER", flag, OcrFlag.VendorNotFound);
        try
        {
            await CreateService().AssignSupplierAsync(id, supplierId, "TEST");

            var after = await ReloadAsync(id);
            Assert.Equal("PENDING", after.Status);
            Assert.Contains(flag, after.OcrFlags ?? ""); // kept as a stored fact (D-038 Q5)
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Theory]
    [InlineData("TOTALS_OUT_OF_RANGE")]
    [InlineData("VAT_RATE_NOT_ALLOWED")]
    [InlineData("NUMBER_MISREAD")]
    [InlineData("NUMBER_AMBIGUOUS")]
    public async Task AssignSupplier_OtherReviewFlag_StillNeedsReview(string flag)
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync(null, "PENDING_SUPPLIER", flag, OcrFlag.VendorNotFound);
        try
        {
            await CreateService().AssignSupplierAsync(id, supplierId, "TEST");

            Assert.Equal("NEEDS_REVIEW", (await ReloadAsync(id)).Status);
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

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
