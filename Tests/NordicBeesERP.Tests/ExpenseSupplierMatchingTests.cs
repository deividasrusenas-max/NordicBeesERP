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
/// OCR Etapas 2 S3 (PLAN-ETAPAS2 §7.2, D-044, D-045): the supplier cascade wired into the OCR pipeline — one match per upload,
/// the flags of its outcome (VENDOR_NOT_FOUND / VENDOR_AMBIGUOUS / VENDOR_SUGGESTED), and the SUPPLIER_MATCHED audit row.
/// Integration tests against nordic_bees_erp_test.
/// </summary>
[Collection("RealDatabase")]
public class ExpenseSupplierMatchingTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ExpenseSupplierMatchingTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed class StubVies : IViesService
    {
        public Task<ViesResult> LookupAsync(string vatCode) =>
            Task.FromResult(new ViesResult { ServiceAvailable = true, IsValid = false });
    }

    private ExpenseOcrService CreateOcr() =>
        new(_fixture.Factory, new StubVies(), null!, NullLogger<ExpenseOcrService>.Instance);

    private ExpenseService CreateService() =>
        new(_fixture.Factory, new NullAuthService(), new DefaultCompanySettingsService());

    // an unused 12-digit LT code per call — unique so the shared test DB never produces accidental second hits
    private static string UniqueVat() => "LT9" + Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString();

    private async Task<int> InsertPartnerAsync(string name, string? vat, bool isSupplier = true, bool isActive = true,
        string? companyCode = null, string? bankAccount = null)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Supplier,
            Name = name,
            VatCode = vat,
            CompanyCode = companyCode,
            BankAccount = bankAccount,
            Country = "Lithuania",
            CountryCode = "LT",
            DefaultLanguage = "LT",
            PaymentTermDays = 14,
            DefaultVatRate = 21m,
            IsSupplier = isSupplier,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();
        return partner.Id;
    }

    private async Task DeleteAsync(IEnumerable<int> invoiceIds, params int[] partnerIds)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        foreach (var id in invoiceIds)
        {
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_audit WHERE invoice_id = {0}", id);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_lines WHERE invoice_id = {0}", id);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE id = {0}", id);
        }
        foreach (var id in partnerIds)
            await context.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", id);
    }

    private static OcrResultDto Doc(string name, string? vat, string? companyCode = null, string? iban = null) => new()
    {
        SupplierName = name,
        SupplierVatCode = vat ?? "",
        SupplierCompanyCode = companyCode ?? "",
        SupplierBankAccount = iban ?? "",
        SupplierCountryCode = "LT"
    };

    private async Task<OcrResultDto> ResolveAsync(OcrResultDto result)
    {
        await CreateOcr().ResolveSupplierAsync(result, new CompanySettings());
        ExpenseOcrService.ApplySupplierFlags(result);
        return result;
    }

    // ---------------------------------------------------------------- outcomes → flags

    [Fact]
    public async Task VatMatchOnActiveSupplier_Assigns_NoSupplierFlags_TierIsVat()
    {
        var vat = UniqueVat();
        var id = await InsertPartnerAsync($"S3 Match {Guid.NewGuid():N}", vat);
        try
        {
            var result = await ResolveAsync(Doc($"Visai Kitas Pavadinimas {Guid.NewGuid():N}", vat.ToLowerInvariant().Insert(2, " ")));

            Assert.Equal(id, result.SupplierId);
            Assert.Equal(MatchOutcome.Assigned, result.SupplierMatch!.Outcome);
            Assert.Equal(MatchTier.Vat, result.SupplierMatch.Tier);
            Assert.DoesNotContain(OcrFlag.VendorNotFound, result.Flags);
            Assert.DoesNotContain(OcrFlag.VendorAmbiguous, result.Flags);
            Assert.DoesNotContain(OcrFlag.VendorSuggested, result.Flags);
        }
        finally
        {
            await DeleteAsync(Array.Empty<int>(), id);
        }
    }

    [Fact]
    public async Task SameNameDifferentVat_IsNotAssigned_ItIsSuggested_WithReason()
    {
        // PLAN §1.3 / D-044 Q1: today's name fallback assigned this silently; now it is loud
        var name = $"S3 SameName {Guid.NewGuid():N}";
        var id = await InsertPartnerAsync(name, UniqueVat());
        try
        {
            var documentVat = UniqueVat();
            var result = await ResolveAsync(Doc(name, documentVat));

            Assert.Null(result.SupplierId);
            Assert.Equal(MatchOutcome.Suggested, result.SupplierMatch!.Outcome);
            Assert.Equal(MatchReason.ConflictingIdentifier, result.SupplierMatch.Reason);
            Assert.Equal(new[] { id }, result.SupplierMatch.CandidateIds);
            Assert.Contains(OcrFlag.VendorNotFound, result.Flags);
            Assert.Contains(OcrFlag.VendorSuggested, result.Flags);
            Assert.DoesNotContain(OcrFlag.VendorAmbiguous, result.Flags);

            // the wrapper the interface keeps agrees
            var (supplierId, _) = await CreateOcr().FindSupplierIdAsync(name, documentVat);
            Assert.Null(supplierId);
        }
        finally
        {
            await DeleteAsync(Array.Empty<int>(), id);
        }
    }

    [Fact]
    public async Task DuplicatePartnersWithSameVat_AreAmbiguous_BothAreCandidates_NothingAssigned()
    {
        var vat = UniqueVat();
        var a = await InsertPartnerAsync($"S3 DupA {Guid.NewGuid():N}", vat);
        var b = await InsertPartnerAsync($"S3 DupB {Guid.NewGuid():N}", vat.Insert(2, " ").ToLowerInvariant());
        try
        {
            var result = await ResolveAsync(Doc($"Dokumento Vardas {Guid.NewGuid():N}", vat));

            Assert.Null(result.SupplierId);
            Assert.Equal(MatchOutcome.Ambiguous, result.SupplierMatch!.Outcome);
            Assert.Equal(MatchReason.DuplicatePartners, result.SupplierMatch.Reason);
            Assert.Equal(new[] { a, b }.OrderBy(x => x), result.SupplierMatch.CandidateIds.OrderBy(x => x));
            Assert.Contains(OcrFlag.VendorNotFound, result.Flags);
            Assert.Contains(OcrFlag.VendorAmbiguous, result.Flags);
            Assert.DoesNotContain(OcrFlag.VendorSuggested, result.Flags);
        }
        finally
        {
            await DeleteAsync(Array.Empty<int>(), a, b);
        }
    }

    [Theory]
    [InlineData(false, true)]   // customer-only partner
    [InlineData(true, false)]   // deactivated supplier
    public async Task IneligiblePartner_IsOnlySuggested_D044Q7(bool isSupplier, bool isActive)
    {
        var vat = UniqueVat();
        var id = await InsertPartnerAsync($"S3 Ineligible {Guid.NewGuid():N}", vat, isSupplier: isSupplier, isActive: isActive);
        try
        {
            var result = await ResolveAsync(Doc($"Kitas {Guid.NewGuid():N}", vat));

            Assert.Null(result.SupplierId);
            Assert.Equal(MatchOutcome.Suggested, result.SupplierMatch!.Outcome);
            Assert.Equal(MatchReason.PartnerNotEligible, result.SupplierMatch.Reason);
            Assert.Contains(id, result.SupplierMatch.CandidateIds);
            Assert.Contains(OcrFlag.VendorSuggested, result.Flags);
        }
        finally
        {
            await DeleteAsync(Array.Empty<int>(), id);
        }
    }

    [Fact]
    public async Task CompanyCodeMatch_AssignsWhenTheVatCodeIsUnknown_TierIsCompanyCode()
    {
        var code = Random.Shared.NextInt64(100_000_000, 999_999_999).ToString();
        var id = await InsertPartnerAsync($"S3 Code {Guid.NewGuid():N}", null, companyCode: code);
        try
        {
            var result = await ResolveAsync(Doc($"Kitas {Guid.NewGuid():N}", null, companyCode: code));

            Assert.Equal(id, result.SupplierId);
            Assert.Equal(MatchTier.CompanyCode, result.SupplierMatch!.Tier);
        }
        finally
        {
            await DeleteAsync(Array.Empty<int>(), id);
        }
    }

    [Fact]
    public async Task IbanAlone_OnlySuggests_NeverAssigns_D044Q2()
    {
        const string iban = "LT601010012345678901";
        var id = await InsertPartnerAsync($"S3 Iban {Guid.NewGuid():N}", null, bankAccount: iban);
        try
        {
            var result = await ResolveAsync(Doc($"Visai Kitoks {Guid.NewGuid():N}", null, iban: iban));

            Assert.Null(result.SupplierId);
            Assert.Equal(MatchTier.Iban, result.SupplierMatch!.Tier);
            Assert.Equal(MatchOutcome.Suggested, result.SupplierMatch.Outcome);
            Assert.Contains(OcrFlag.VendorSuggested, result.Flags);
        }
        finally
        {
            await DeleteAsync(Array.Empty<int>(), id);
        }
    }

    [Fact]
    public async Task NoMatch_OnlyVendorNotFound()
    {
        var result = await ResolveAsync(Doc($"Nera Tokio {Guid.NewGuid():N}", UniqueVat()));

        Assert.Null(result.SupplierId);
        Assert.Equal(MatchOutcome.NotFound, result.SupplierMatch!.Outcome);
        Assert.Equal(new[] { OcrFlag.VendorNotFound }, result.Flags);
    }

    // ---------------------------------------------------------------- one match, no stale flag (PLAN §7.2 S3)

    [Fact]
    public async Task MalformedVatStoredOnAPartner_NeverMatches_OnEitherEntryPoint()
    {
        // Before S3 the dialog called FindSupplierIdAsync with the raw VAT (no format gate) while ProcessAsync gated it: the dialog
        // matched, ProcessAsync did not, and VENDOR_NOT_FOUND stayed next to a supplier. Both entry points now agree.
        var malformed = "LT12345"; // LT needs 9 or 12 digits
        var id = await InsertPartnerAsync($"S3 Malformed {Guid.NewGuid():N}", malformed);
        try
        {
            var name = $"Kitas Pavadinimas {Guid.NewGuid():N}";
            var result = await ResolveAsync(Doc(name, malformed));
            var (viaWrapper, _) = await CreateOcr().FindSupplierIdAsync(name, malformed);

            Assert.Null(result.SupplierId);
            Assert.Null(viaWrapper);
            Assert.Contains(OcrFlag.VendorNotFound, result.Flags);
        }
        finally
        {
            await DeleteAsync(Array.Empty<int>(), id);
        }
    }

    [Fact]
    public async Task AssignedSupplier_NeverCarriesVendorFlags()
    {
        var vat = UniqueVat();
        var id = await InsertPartnerAsync($"S3 Clean {Guid.NewGuid():N}", vat);
        try
        {
            var result = Doc($"X {Guid.NewGuid():N}", vat);
            result.Flags.Add("SOMETHING_ELSE");
            await ResolveAsync(result);

            Assert.Equal(id, result.SupplierId);
            Assert.Equal(new[] { "SOMETHING_ELSE" }, result.Flags);
        }
        finally
        {
            await DeleteAsync(Array.Empty<int>(), id);
        }
    }

    [Fact]
    public void UploadDialog_TakesTheOcrResultSupplier_AndNeverMatchesAgain()
    {
        var dialog = File.ReadAllText(Path.Combine(RepoRoot(), "Components", "Dialogs", "ExpenseUploadDialog.razor"));

        Assert.DoesNotContain("FindSupplierIdAsync", dialog);
        Assert.Contains("result.SupplierId.HasValue", dialog);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "NordicBeesERP.csproj"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }

    // ---------------------------------------------------------------- audit SUPPLIER_MATCHED

    private static OcrResultDto ForSave(int? supplierId, SupplierMatch? match) => new()
    {
        InvoiceNumber = $"S3M-{Guid.NewGuid():N}",
        InvoiceDate = DateTime.Today.ToString("yyyy-MM-dd"),
        DueDate = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd"),
        Currency = "EUR",
        AmountExclVat = 100m,
        VatRate = 21m,
        VatAmount = 21m,
        AmountInclVat = 121m,
        SupplierId = supplierId,
        SupplierName = "S3 OCR Supplier",
        SupplierMatch = match,
        Confidence = new OcrConfidenceDto { Amounts = 90, InvoiceNumber = 90, SupplierName = 90, InvoiceDate = 90 }
    };

    private async Task<List<ExpenseInvoiceAudit>> AuditsAsync(int invoiceId)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoiceAudits.AsNoTracking().Where(a => a.InvoiceId == invoiceId).ToListAsync();
    }

    [Fact]
    public async Task CreateFromOcr_WritesSupplierMatchedAudit_WithTierReasonAndCandidates()
    {
        var partner = await InsertPartnerAsync($"S3 Audit {Guid.NewGuid():N}", UniqueVat());
        var match = new SupplierMatch(MatchOutcome.Assigned, partner, MatchTier.Vat, MatchReason.None, new[] { partner }, null, 0);
        var invoice = await CreateService().CreateFromOcrAsync(ForSave(partner, match));
        try
        {
            var audit = Assert.Single(await AuditsAsync(invoice.Id), a => a.Action == "SUPPLIER_MATCHED");
            Assert.Equal($"outcome=Assigned; tier=Vat; reason=None; candidates={partner}; supplier={partner}", audit.ActionDetails);
        }
        finally
        {
            await DeleteAsync(new[] { invoice.Id }, partner);
        }
    }

    [Fact]
    public async Task CreateFromOcr_Ambiguous_AuditListsBothCandidates_StatusPendingSupplier()
    {
        var a = await InsertPartnerAsync($"S3 AmbA {Guid.NewGuid():N}", UniqueVat());
        var b = await InsertPartnerAsync($"S3 AmbB {Guid.NewGuid():N}", UniqueVat());
        var match = new SupplierMatch(MatchOutcome.Ambiguous, null, MatchTier.Vat, MatchReason.DuplicatePartners, new[] { a, b }, null, 0);
        var dto = ForSave(null, match);
        dto.Flags.AddRange(new[] { OcrFlag.VendorNotFound, OcrFlag.VendorAmbiguous });
        var invoice = await CreateService().CreateFromOcrAsync(dto);
        try
        {
            var audit = Assert.Single(await AuditsAsync(invoice.Id), x => x.Action == "SUPPLIER_MATCHED");
            Assert.Equal($"outcome=Ambiguous; tier=Vat; reason=DuplicatePartners; candidates={a},{b}; supplier=none", audit.ActionDetails);

            await using var context = await _fixture.Factory.CreateDbContextAsync();
            var stored = await context.ExpenseInvoices.AsNoTracking().FirstAsync(i => i.Id == invoice.Id);
            Assert.Equal("PENDING_SUPPLIER", stored.Status);
            Assert.Contains(OcrFlag.VendorAmbiguous, ExpenseStatusHelper.ParseFlags(stored.OcrFlags));
        }
        finally
        {
            await DeleteAsync(new[] { invoice.Id }, a, b);
        }
    }

    [Fact]
    public async Task CreateFromOcr_WithoutAMatch_WritesNoSupplierMatchedAudit()
    {
        var partner = await InsertPartnerAsync($"S3 NoMatch {Guid.NewGuid():N}", UniqueVat());
        var invoice = await CreateService().CreateFromOcrAsync(ForSave(partner, null));
        try
        {
            Assert.DoesNotContain(await AuditsAsync(invoice.Id), a => a.Action == "SUPPLIER_MATCHED");
        }
        finally
        {
            await DeleteAsync(new[] { invoice.Id }, partner);
        }
    }

    [Fact]
    public async Task ReOcr_WritesSupplierMatchedAudit_AndKeepsTheAssignedSupplierWithoutVendorFlags()
    {
        var assigned = await InsertPartnerAsync($"S3 ReOcr {Guid.NewGuid():N}", UniqueVat());
        var other = await InsertPartnerAsync($"S3 ReOcrB {Guid.NewGuid():N}", UniqueVat());
        var created = await CreateService().CreateFromOcrAsync(ForSave(assigned, null));
        try
        {
            // the fresh match is a tie between two other partners: nothing to suggest, nothing to flag
            var fresh = ForSave(null, new SupplierMatch(MatchOutcome.Ambiguous, null, MatchTier.Name, MatchReason.DuplicatePartners,
                new[] { other, other + 1 }, null, 0));
            fresh.InvoiceNumber = created.InvoiceNumber!;
            fresh.Flags.AddRange(new[] { OcrFlag.VendorNotFound, OcrFlag.VendorAmbiguous });
            await CreateService().UpdateFromOcrAsync(created.Id, fresh);

            await using var context = await _fixture.Factory.CreateDbContextAsync();
            var stored = await context.ExpenseInvoices.AsNoTracking().FirstAsync(i => i.Id == created.Id);
            Assert.Equal(assigned, stored.SupplierId);
            var flags = ExpenseStatusHelper.ParseFlags(stored.OcrFlags);
            Assert.DoesNotContain(OcrFlag.VendorNotFound, flags);
            Assert.DoesNotContain(OcrFlag.VendorAmbiguous, flags);
            Assert.DoesNotContain(OcrFlag.VendorSuggested, flags);

            var audit = Assert.Single(await AuditsAsync(created.Id), a => a.Action == "SUPPLIER_MATCHED");
            Assert.EndsWith($"; supplier={assigned}", audit.ActionDetails);
        }
        finally
        {
            await DeleteAsync(new[] { created.Id }, assigned, other);
        }
    }

    // ---------------------------------------------------------------- flags are information, never review; labels

    [Fact]
    public void VendorAmbiguous_HasLithuanianLabel_IsInformationOnly()
    {
        Assert.Equal("VENDOR_AMBIGUOUS", OcrFlag.VendorAmbiguous);
        Assert.Equal("Keli galimi tiekėjai", ExpenseStatusHelper.GetFlagLabel(OcrFlag.VendorAmbiguous));
        Assert.Equal(MudBlazor.Color.Info, ExpenseStatusHelper.GetFlagColor(OcrFlag.VendorAmbiguous));
        Assert.False(ExpenseStatusHelper.IsCriticalFlag(OcrFlag.VendorAmbiguous));
    }

    [Fact]
    public async Task VendorAmbiguous_IsCarriedOverByTheEditPath_WhileThereIsNoSupplier()
    {
        var dto = ForSave(null, null);
        dto.Flags.AddRange(new[] { OcrFlag.VendorNotFound, OcrFlag.VendorAmbiguous });
        var invoice = await CreateService().CreateFromOcrAsync(dto);
        try
        {
            await using var context = await _fixture.Factory.CreateDbContextAsync();
            var toEdit = await context.ExpenseInvoices.AsNoTracking().FirstAsync(i => i.Id == invoice.Id);
            toEdit.Notes = "edited";
            await CreateService().SaveInvoiceEditAsync(toEdit, new List<ExpenseInvoiceLine>(), "Test User");

            var stored = await context.ExpenseInvoices.AsNoTracking().FirstAsync(i => i.Id == invoice.Id);
            Assert.Equal("PENDING_SUPPLIER", stored.Status);
            Assert.Contains(OcrFlag.VendorAmbiguous, ExpenseStatusHelper.ParseFlags(stored.OcrFlags));
        }
        finally
        {
            await DeleteAsync(new[] { invoice.Id });
        }
    }

    [Fact]
    public async Task AssignSupplier_RemovesVendorAmbiguous()
    {
        var partner = await InsertPartnerAsync($"S3 Assign {Guid.NewGuid():N}", UniqueVat());
        var dto = ForSave(null, null);
        dto.Flags.AddRange(new[] { OcrFlag.VendorNotFound, OcrFlag.VendorAmbiguous });
        var invoice = await CreateService().CreateFromOcrAsync(dto);
        try
        {
            await CreateService().AssignSupplierAsync(invoice.Id, partner, "Test User");

            await using var context = await _fixture.Factory.CreateDbContextAsync();
            var stored = await context.ExpenseInvoices.AsNoTracking().FirstAsync(i => i.Id == invoice.Id);
            Assert.Equal(partner, stored.SupplierId);
            Assert.DoesNotContain(OcrFlag.VendorAmbiguous, ExpenseStatusHelper.ParseFlags(stored.OcrFlags));
        }
        finally
        {
            await DeleteAsync(new[] { invoice.Id }, partner);
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
