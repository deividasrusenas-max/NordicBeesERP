using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// OCR Etapas 1 S2a: the manual-edit path recomputes only the flags it owns and carries every other
/// stored flag over (before, an allow-list of five dropped OWN_COMPANY and INVALID_VAT_RATE on every
/// edit-form save — invoice 370 on staging). Integration tests against nordic_bees_erp_test.
/// </summary>
[Collection("RealDatabase")]
public class ExpenseEditFlagCarryOverTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ExpenseEditFlagCarryOverTests(DbTestFixture fixture)
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
            Name = $"CarryOver Supplier {Guid.NewGuid():N}",
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

    /// <summary>Invoice 100 / 21 / 121 (or the given amounts), PENDING, with the given stored flags.</summary>
    private async Task<int> InsertInvoiceAsync(int supplierId, IEnumerable<string> flags,
        decimal excl = 100m, decimal vat = 21m, decimal incl = 121m, string status = "PENDING")
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var number = $"CARRY-{Guid.NewGuid():N}";
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices " +
            "(invoice_number, invoice_date, due_date, amount_excl_vat, vat_rate, vat_amount, amount_incl_vat, status, supplier_id, ocr_flags, currency, source, ocr_status, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, NOW(), NOW())",
            number, DateTime.Today, DateTime.Today.AddDays(30), excl, 21m, vat, incl, status, supplierId,
            JsonSerializer.Serialize(flags.ToList()), "EUR", "MANUAL", "COMPLETED");
        return await context.ExpenseInvoices.Where(i => i.InvoiceNumber == number).Select(i => i.Id).FirstAsync();
    }

    private async Task InsertLineAsync(int invoiceId, decimal excl)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoice_lines (invoice_id, description, quantity, amount_excl_vat, vat_rate, amount_incl_vat, sort_order) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6})",
            invoiceId, "Eilutė", 1m, excl, 21m, Math.Round(excl * (1 + 21m / 100), 2), 1);
    }

    private async Task<ExpenseInvoice> ReloadAsync(int id)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoices.AsNoTracking().FirstAsync(i => i.Id == id);
    }

    private async Task<List<ExpenseInvoiceLine>> LinesAsync(int invoiceId)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoiceLines.AsNoTracking().Where(l => l.InvoiceId == invoiceId).OrderBy(l => l.Id).ToListAsync();
    }

    private static List<string> FlagsOf(ExpenseInvoice invoice) =>
        string.IsNullOrEmpty(invoice.OcrFlags) ? new() : JsonSerializer.Deserialize<List<string>>(invoice.OcrFlags) ?? new();

    private async Task CleanupAsync(int invoiceId, int supplierId)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_audit WHERE invoice_id = {0}", invoiceId);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_lines WHERE invoice_id = {0}", invoiceId);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE id = {0}", invoiceId);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", supplierId);
    }

    [Fact]
    public async Task EditFormSave_OwnCompanyAndInvalidVatRate_Survive()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync(supplierId, new[] { OcrFlag.OwnCompany, OcrFlag.InvalidVatRate });
        await InsertLineAsync(id, 100m);
        try
        {
            var invoice = await ReloadAsync(id);
            invoice.Notes = "Tik pastaba";
            await CreateService().SaveInvoiceEditAsync(invoice, await LinesAsync(id), "Test User");

            var flags = FlagsOf(await ReloadAsync(id));
            Assert.Contains(OcrFlag.OwnCompany, flags);
            Assert.Contains(OcrFlag.InvalidVatRate, flags);
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Fact]
    public async Task EditFormSave_UnknownFutureFlag_Survives()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync(supplierId, new[] { "SOME_FUTURE_FLAG", OcrFlag.ViesUnavailable, OcrFlag.Duplicate });
        await InsertLineAsync(id, 100m);
        try
        {
            await CreateService().SaveInvoiceEditAsync(await ReloadAsync(id), await LinesAsync(id), "Test User");

            var flags = FlagsOf(await ReloadAsync(id));
            Assert.Contains("SOME_FUTURE_FLAG", flags);
            Assert.Contains(OcrFlag.ViesUnavailable, flags);
            Assert.Contains(OcrFlag.Duplicate, flags);
            Assert.Equal(flags.Count, flags.Distinct().Count());
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Fact]
    public async Task EditFormSave_RecomputedFlag_DisappearsWhenCauseFixed()
    {
        var supplierId = await InsertSupplierAsync();
        // Stored: broken header arithmetic (100 + 21 ≠ 130) and its flag, plus a carried flag
        var id = await InsertInvoiceAsync(supplierId,
            new[] { OcrFlag.AmountArithmeticMismatch, OcrFlag.OwnCompany }, incl: 130m, status: "NEEDS_REVIEW");
        await InsertLineAsync(id, 100m);
        try
        {
            var invoice = await ReloadAsync(id);
            invoice.AmountInclVat = 121m;
            await CreateService().SaveInvoiceEditAsync(invoice, await LinesAsync(id), "Test User");

            var after = await ReloadAsync(id);
            var flags = FlagsOf(after);
            Assert.DoesNotContain(OcrFlag.AmountArithmeticMismatch, flags);
            Assert.Contains(OcrFlag.OwnCompany, flags);
            Assert.Equal("PENDING", after.Status);
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Fact]
    public async Task EditFormSave_RecomputedFlag_AddedWhenCauseAppears_CarriedFlagKept()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync(supplierId, new[] { OcrFlag.OwnCompany });
        await InsertLineAsync(id, 100m);
        try
        {
            var invoice = await ReloadAsync(id);
            invoice.AmountInclVat = 130m; // 100 + 21 ≠ 130
            await CreateService().SaveInvoiceEditAsync(invoice, await LinesAsync(id), "Test User");

            var after = await ReloadAsync(id);
            var flags = FlagsOf(after);
            Assert.Contains(OcrFlag.AmountArithmeticMismatch, flags);
            Assert.Contains(OcrFlag.OwnCompany, flags);
            Assert.Equal("NEEDS_REVIEW", after.Status);
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Fact]
    public async Task EditFormSave_LowConfidence_StillDropped()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync(supplierId, new[] { OcrFlag.LowConfidence, OcrFlag.OwnCompany }, status: "NEEDS_REVIEW");
        await InsertLineAsync(id, 100m);
        try
        {
            await CreateService().SaveInvoiceEditAsync(await ReloadAsync(id), await LinesAsync(id), "Test User");

            var flags = FlagsOf(await ReloadAsync(id));
            Assert.DoesNotContain(OcrFlag.LowConfidence, flags);
            Assert.Contains(OcrFlag.OwnCompany, flags);
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Fact]
    public async Task UpdateInvoiceAsync_SameCarryOver()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync(supplierId, new[] { OcrFlag.OwnCompany, "SOME_FUTURE_FLAG" });
        try
        {
            var invoice = await ReloadAsync(id);
            invoice.Notes = "Tik pastaba";
            await CreateService().UpdateInvoiceAsync(invoice);

            var flags = FlagsOf(await ReloadAsync(id));
            Assert.Contains(OcrFlag.OwnCompany, flags);
            Assert.Contains("SOME_FUTURE_FLAG", flags);
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
