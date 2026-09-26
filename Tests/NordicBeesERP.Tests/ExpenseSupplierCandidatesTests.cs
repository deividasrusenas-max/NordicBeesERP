using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NordicBeesERP.Helpers;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services;
using NordicBeesERP.Services.Validation;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// OCR Etapas 2 S3b (D-044): the candidates the detail dialog shows for a PENDING_SUPPLIER invoice, the create-supplier sweep
/// (AutoAssignSupplierAsync) on the matcher's normalisers, and the three S3a rules the first review found unguarded
/// (IsExpenseSupplier role, sweep flag removal, re-OCR dropping a stale VENDOR_SUGGESTED). Real nordic_bees_erp_test.
/// </summary>
public class ExpenseSupplierCandidatesTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;
    public ExpenseSupplierCandidatesTests(DbTestFixture fixture) { _fixture = fixture; }

    private ExpenseService CreateService() => new(_fixture.Factory, new NullAuth(), new DefaultSettings());

    private static string UniqueVat() => "LT9" + Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString();

    private async Task<int> InsertPartnerAsync(string name, string? vat, bool isSupplier = true, bool isExpenseSupplier = false)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Supplier, Name = name, VatCode = vat, Country = "Lithuania", CountryCode = "LT",
            DefaultLanguage = "LT", PaymentTermDays = 14, DefaultVatRate = 21m, IsSupplier = isSupplier,
            IsExpenseSupplier = isExpenseSupplier, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();
        return partner.Id;
    }

    private async Task<int> InsertInvoiceAsync(string status, int? supplierId, string? pendingName, string? pendingVat,
        string? pendingCountry = "LT", IEnumerable<string>? flags = null)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var marker = $"S3B-{Guid.NewGuid():N}";
        var date = DateTime.Today;
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices " +
            "(invoice_number, invoice_date, due_date, amount_excl_vat, vat_rate, vat_amount, amount_incl_vat, status, supplier_id, ocr_flags, notes, currency, source, ocr_status, " +
            "pending_supplier_name, pending_supplier_vat, pending_supplier_country_code, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, 100, 21, 21, 121, {3}, {4}, {5}, {6}, 'EUR', 'MANUAL', 'COMPLETED', {7}, {8}, {9}, NOW(), NOW())",
            marker, date, date.AddDays(30), status, supplierId, JsonSerializer.Serialize(flags ?? Array.Empty<string>()), marker,
            pendingName, pendingVat, pendingCountry);
        return await context.ExpenseInvoices.Where(i => i.Notes == marker).Select(i => i.Id).FirstAsync();
    }

    private async Task<ExpenseInvoice> ReloadAsync(int id)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoices.AsNoTracking().FirstAsync(i => i.Id == id);
    }

    private async Task CleanupAsync(IEnumerable<int> invoices, params int[] partners)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        foreach (var id in invoices)
        {
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_audit WHERE invoice_id = {0}", id);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE id = {0}", id);
        }
        foreach (var id in partners)
            await context.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", id);
    }

    // ------------------------------------------------------------ candidates

    [Fact]
    public async Task Candidates_DuplicatePartners_BothListedWithTierAndReason()
    {
        var vat = UniqueVat();
        var a = await InsertPartnerAsync($"S3B DupA {Guid.NewGuid():N}", vat);
        var b = await InsertPartnerAsync($"S3B DupB {Guid.NewGuid():N}", vat);
        var invoice = await InsertInvoiceAsync("PENDING_SUPPLIER", null, $"Dok {Guid.NewGuid():N}", vat);
        try
        {
            var candidates = await CreateService().GetSupplierCandidatesAsync(invoice);

            Assert.Equal(new[] { a, b }.OrderBy(x => x), candidates.Select(c => c.PartnerId).OrderBy(x => x));
            Assert.All(candidates, c =>
            {
                Assert.Equal(MatchOutcome.Ambiguous, c.Outcome);
                Assert.Equal(MatchTier.Vat, c.Tier);
                Assert.Equal(MatchReason.DuplicatePartners, c.Reason);
                Assert.Equal(vat, c.VatCode);
            });
        }
        finally { await CleanupAsync(new[] { invoice }, a, b); }
    }

    [Fact]
    public async Task Candidates_SameNameDifferentVat_IsSuggestedWithConflictReason()
    {
        var name = $"S3B Same {Guid.NewGuid():N}";
        var partner = await InsertPartnerAsync(name, UniqueVat());
        var invoice = await InsertInvoiceAsync("PENDING_SUPPLIER", null, name, UniqueVat());
        try
        {
            var candidate = Assert.Single(await CreateService().GetSupplierCandidatesAsync(invoice));
            Assert.Equal(partner, candidate.PartnerId);
            Assert.Equal(MatchOutcome.Suggested, candidate.Outcome);
            Assert.Equal(MatchReason.ConflictingIdentifier, candidate.Reason);
        }
        finally { await CleanupAsync(new[] { invoice }, partner); }
    }

    [Fact]
    public async Task Candidates_NothingMatched_OrNotPendingSupplier_OrAlreadyAssigned_AreEmpty()
    {
        var partner = await InsertPartnerAsync($"S3B Any {Guid.NewGuid():N}", UniqueVat());
        var none = await InsertInvoiceAsync("PENDING_SUPPLIER", null, $"Nieko {Guid.NewGuid():N}", UniqueVat());
        var name = $"S3B Match {Guid.NewGuid():N}";
        var named = await InsertPartnerAsync(name, null);
        var pending = await InsertInvoiceAsync("PENDING", null, name, null);
        var assigned = await InsertInvoiceAsync("PENDING_SUPPLIER", partner, name, null);
        try
        {
            var service = CreateService();
            Assert.Empty(await service.GetSupplierCandidatesAsync(none));
            Assert.Empty(await service.GetSupplierCandidatesAsync(pending));
            Assert.Empty(await service.GetSupplierCandidatesAsync(assigned));
            Assert.Empty(await service.GetSupplierCandidatesAsync(int.MaxValue));
        }
        finally { await CleanupAsync(new[] { none, pending, assigned }, partner, named); }
    }

    // ------------------------------------------------------------ the sweep on the matcher's normalisers

    [Fact]
    public async Task AutoAssign_VatInAnotherSpelling_IsAssigned_AndClearsVendorFlags()
    {
        var vat = UniqueVat();
        var partner = await InsertPartnerAsync($"S3B Sweep {Guid.NewGuid():N}", vat);
        var invoice = await InsertInvoiceAsync("PENDING_SUPPLIER", null, $"Kitas {Guid.NewGuid():N}", vat.ToLowerInvariant().Insert(2, " "),
            flags: new[] { OcrFlag.VendorNotFound, OcrFlag.VendorSuggested, OcrFlag.VendorAmbiguous });
        try
        {
            var count = await CreateService().AutoAssignSupplierAsync(vat, null, partner);

            Assert.Equal(1, count);
            var after = await ReloadAsync(invoice);
            Assert.Equal(partner, after.SupplierId);
            var flags = ExpenseStatusHelper.ParseFlags(after.OcrFlags);
            Assert.DoesNotContain(OcrFlag.VendorNotFound, flags);
            Assert.DoesNotContain(OcrFlag.VendorSuggested, flags);
            Assert.DoesNotContain(OcrFlag.VendorAmbiguous, flags);
        }
        finally { await CleanupAsync(new[] { invoice }, partner); }
    }

    [Fact]
    public async Task AutoAssign_SameNameWithContradictingVat_IsNotAssigned_SameNameWithoutVat_Is()
    {
        var name = $"S3B Name {Guid.NewGuid():N}";
        var partner = await InsertPartnerAsync(name, UniqueVat());
        var contradicting = await InsertInvoiceAsync("PENDING_SUPPLIER", null, name, UniqueVat());
        var noVat = await InsertInvoiceAsync("PENDING_SUPPLIER", null, name.ToUpperInvariant(), null);
        try
        {
            var count = await CreateService().AutoAssignSupplierAsync(UniqueVat(), name, partner);

            Assert.Equal(1, count);
            Assert.Null((await ReloadAsync(contradicting)).SupplierId);
            Assert.Equal(partner, (await ReloadAsync(noVat)).SupplierId);
        }
        finally { await CleanupAsync(new[] { contradicting, noVat }, partner); }
    }

    [Fact]
    public async Task AutoAssign_PrefixlessPendingVat_DoesNotMatchAPrefixedCode_NoLtAssumption()
    {
        var vat = UniqueVat();
        var partner = await InsertPartnerAsync($"S3B NoLt {Guid.NewGuid():N}", vat);
        var invoice = await InsertInvoiceAsync("PENDING_SUPPLIER", null, $"Kitas {Guid.NewGuid():N}", vat[2..], pendingCountry: null);
        try
        {
            Assert.Equal(0, await CreateService().AutoAssignSupplierAsync(vat, null, partner));
            Assert.Null((await ReloadAsync(invoice)).SupplierId);
        }
        finally { await CleanupAsync(new[] { invoice }, partner); }
    }

    // ------------------------------------------------------------ gaps of the S3a review

    [Fact]
    public async Task ExpenseSupplierRole_IsEligible_ForAutomaticAssignment()
    {
        var vat = UniqueVat();
        var partner = await InsertPartnerAsync($"S3B Expense {Guid.NewGuid():N}", vat, isSupplier: false, isExpenseSupplier: true);
        try
        {
            var (id, _) = await new ExpenseOcrService(_fixture.Factory, null!, null!, NullLogger<ExpenseOcrService>.Instance)
                .FindSupplierIdAsync("", vat);
            Assert.Equal(partner, id);
        }
        finally { await CleanupAsync(Array.Empty<int>(), partner); }
    }

    [Fact]
    public async Task ReOcr_KeptSupplier_DropsAStaleVendorSuggested_WhenNoDifferentPartnerWasAssigned()
    {
        var assigned = await InsertPartnerAsync($"S3B Kept {Guid.NewGuid():N}", UniqueVat());
        var invoice = await InsertInvoiceAsync("PENDING", assigned, null, null, flags: new[] { OcrFlag.VendorSuggested });
        try
        {
            var fresh = new OcrResultDto
            {
                InvoiceNumber = $"S3B-{Guid.NewGuid():N}", InvoiceDate = DateTime.Today.ToString("yyyy-MM-dd"),
                DueDate = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd"), Currency = "EUR",
                AmountExclVat = 100m, VatRate = 21m, VatAmount = 21m, AmountInclVat = 121m, SupplierName = "x",
                SupplierMatch = new SupplierMatch(MatchOutcome.Suggested, null, MatchTier.NormalizedName, MatchReason.NormalizedNameOnly, new[] { 1 }, null, 0),
                Confidence = new OcrConfidenceDto { Amounts = 90, InvoiceNumber = 90, SupplierName = 90, InvoiceDate = 90 }
            };
            fresh.Flags.AddRange(new[] { OcrFlag.VendorNotFound, OcrFlag.VendorSuggested });

            await CreateService().UpdateFromOcrAsync(invoice, fresh);

            var after = await ReloadAsync(invoice);
            Assert.Equal(assigned, after.SupplierId);
            var flags = ExpenseStatusHelper.ParseFlags(after.OcrFlags);
            Assert.DoesNotContain(OcrFlag.VendorSuggested, flags);
            Assert.DoesNotContain(OcrFlag.VendorNotFound, flags);
        }
        finally { await CleanupAsync(new[] { invoice }, assigned); }
    }

    private sealed class NullAuth : IAuthService
    {
        public Task<ErpUser?> ValidateUserAsync(string email, string password) => Task.FromResult<ErpUser?>(null);
        public Task SeedAdminAsync(string email, string password) => Task.CompletedTask;
        public Task<ErpUser?> GetAuthenticatedUserAsync() => Task.FromResult<ErpUser?>(null);
        public Task<int?> GetCustomerIdAsync() => Task.FromResult<int?>(null);
        public Task<int?> GetUserIdAsync() => Task.FromResult<int?>(null);
        public Task<ErpUser?> GetUserByIdAsync(int userId) => Task.FromResult<ErpUser?>(null);
        public Task<string> GetRequiredActorNameAsync() => throw new NotImplementedException();
    }

    private sealed class DefaultSettings : ICompanySettingsService
    {
        public Task<CompanySettings> GetSettingsAsync() => Task.FromResult(new CompanySettings());
        public Task UpdateSettingsAsync(CompanySettings settings) => Task.CompletedTask;
    }
}
