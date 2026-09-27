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
/// OCR Etapas 2 S5 (PLAN-ETAPAS2 §2, §7.2; D-017, D-044 Q4): learned supplier aliases. Confirmations only from explicit human actions
/// (assign, change, create-for-this-invoice) — never the sweep, never a matcher assignment; N = 2 distinct invoices promote; a
/// re-OCR / repeat does not count twice; a conflict freezes both; frozen and revoked never apply; an alias never overrides a
/// contradicting VAT; revoke and unfreeze write events. Real nordic_bees_erp_test (supplier_aliases, supplier_alias_events).
/// </summary>
public class ExpenseSupplierAliasTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;
    public ExpenseSupplierAliasTests(DbTestFixture fixture) { _fixture = fixture; }

    private ExpenseService CreateService() => new(_fixture.Factory, new NullAuth(), new DefaultSettings());
    private ExpenseOcrService CreateOcr() => new(_fixture.Factory, new StubVies(), null!, NullLogger<ExpenseOcrService>.Instance);

    private sealed class StubVies : IViesService
    {
        public Task<ViesResult> LookupAsync(string vatCode) => Task.FromResult(new ViesResult { ServiceAvailable = true, IsValid = false });
    }

    private static string UniqueVat() => "LT9" + Random.Shared.NextInt64(10_000_000_000, 99_999_999_999);
    private static string RawName() => $"S5 Alias Tiekejas {Guid.NewGuid():N} UAB";

    private readonly List<int> _partners = new();
    private readonly List<int> _invoices = new();

    private async Task<int> InsertPartnerAsync(string? vat = null, bool active = true, bool supplier = true)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Supplier, Name = $"S5 Partner {Guid.NewGuid():N}", VatCode = vat, Country = "Lithuania", CountryCode = "LT",
            DefaultLanguage = "LT", PaymentTermDays = 14, DefaultVatRate = 21m, IsSupplier = supplier, IsActive = active,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();
        _partners.Add(partner.Id);
        return partner.Id;
    }

    private async Task<int> InsertInvoiceAsync(string status, int? supplierId, string? pendingName, string? pendingVat = null)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var marker = $"S5-{Guid.NewGuid():N}";
        var date = DateTime.Today;
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices " +
            "(invoice_number, invoice_date, due_date, amount_excl_vat, vat_rate, vat_amount, amount_incl_vat, status, supplier_id, ocr_flags, notes, currency, source, ocr_status, pending_supplier_name, pending_supplier_vat, pending_supplier_country_code, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, 100, 21, 21, 121, {3}, {4}, '[]', {5}, 'EUR', 'MANUAL', 'COMPLETED', {6}, {7}, 'LT', NOW(), NOW())",
            marker, date, date.AddDays(30), status, supplierId, marker, pendingName, pendingVat);
        var id = await context.ExpenseInvoices.Where(i => i.Notes == marker).Select(i => i.Id).FirstAsync();
        _invoices.Add(id);
        return id;
    }

    private async Task<List<SupplierAlias>> AliasesAsync(string rawName)
    {
        var key = SupplierAliases.KeyFor(rawName)!;
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.SupplierAliases.AsNoTracking().Where(a => a.AliasKey == key).ToListAsync();
    }

    private async Task<SupplierAlias?> AliasAsync(string rawName, int partnerId) =>
        (await AliasesAsync(rawName)).FirstOrDefault(a => a.PartnerId == partnerId);

    private async Task<List<SupplierAliasEvent>> EventsAsync(int aliasId)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await SupplierAliases.EventsAsync(context, aliasId);
    }

    private async Task AssignAsync(int invoice, int partner) => await CreateService().AssignSupplierAsync(invoice, partner, "Test User");

    private async Task<int> PromoteAsync(string rawName, int partner)
    {
        for (var i = 0; i < SupplierAliases.PromotionThreshold; i++)
            await AssignAsync(await InsertInvoiceAsync("PENDING_SUPPLIER", null, rawName), partner);
        return (await AliasAsync(rawName, partner))!.Id;
    }

    private async Task CleanupAsync()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        foreach (var partner in _partners)
        {
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM supplier_alias_events WHERE alias_id IN (SELECT id FROM supplier_aliases WHERE partner_id = {0})", partner);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM supplier_aliases WHERE partner_id = {0}", partner);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", partner);
        }
        foreach (var invoice in _invoices)
        {
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_audit WHERE invoice_id = {0}", invoice);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_lines WHERE invoice_id = {0}", invoice);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE id = {0}", invoice);
        }
    }

    private async Task<OcrResultDto> ResolveAsync(string name, string? vat = null)
    {
        var result = new OcrResultDto { SupplierName = name, SupplierVatCode = vat ?? "", SupplierCountryCode = "LT" };
        await CreateOcr().ResolveSupplierAsync(result, new CompanySettings());
        ExpenseOcrService.ApplySupplierFlags(result);
        return result;
    }

    // ---------------------------------------------------------------- confirmation and promotion

    [Fact]
    public async Task TwoDistinctInvoicesAssignedByAHuman_PromoteTheAlias_AndItIsThenApplied()
    {
        try
        {
            var raw = RawName();
            var partner = await InsertPartnerAsync();
            var first = await InsertInvoiceAsync("PENDING_SUPPLIER", null, raw);
            await AssignAsync(first, partner);

            var afterOne = (await AliasAsync(raw, partner))!;
            Assert.Equal("CANDIDATE", afterOne.State);
            Assert.Equal(1, afterOne.Confirmations);
            Assert.Equal(raw, afterOne.RawExample);
            Assert.Equal(SupplierAliases.KeyFor(raw), afterOne.AliasKey);
            Assert.Equal(MatchOutcome.NotFound, (await ResolveAsync(raw)).SupplierMatch!.Outcome);   // a candidate is never applied

            await AssignAsync(await InsertInvoiceAsync("PENDING_SUPPLIER", null, raw), partner);

            var active = (await AliasAsync(raw, partner))!;
            Assert.Equal("ACTIVE", active.State);
            Assert.Equal(2, active.Confirmations);
            Assert.Equal(new[] { "CONFIRMED", "CONFIRMED", "PROMOTED" }, (await EventsAsync(active.Id)).Select(e => e.Event));

            var applied = await ResolveAsync(raw);
            Assert.Equal(partner, applied.SupplierId);
            Assert.Equal(MatchTier.Alias, applied.SupplierMatch!.Tier);
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task ARepeatedConfirmationOfTheSameInvoice_DoesNotCountTwice()
    {
        try
        {
            var raw = RawName();
            var partner = await InsertPartnerAsync();
            var invoice = await InsertInvoiceAsync("PENDING_SUPPLIER", null, raw);
            await AssignAsync(invoice, partner);

            var service = CreateService();
            await service.ConfirmSupplierAliasAsync(invoice, partner, "Test User");   // the create-supplier path on the same invoice
            await service.ConfirmSupplierAliasAsync(invoice, partner, "Test User");   // and once more

            var alias = (await AliasAsync(raw, partner))!;
            Assert.Equal(1, alias.Confirmations);
            Assert.Equal("CANDIDATE", alias.State);
            Assert.Single((await EventsAsync(alias.Id)), e => e.Event == "CONFIRMED");
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task ChangeSupplier_IsAConfirmation_ForAnInvoiceThatStillHoldsItsOcrName()
    {
        try
        {
            var raw = RawName();
            var wrong = await InsertPartnerAsync();
            var right = await InsertPartnerAsync();
            var invoice = await InsertInvoiceAsync("NEEDS_REVIEW", wrong, raw);

            await CreateService().ChangeSupplierAsync(invoice, right, "Test User");

            var alias = (await AliasAsync(raw, right))!;
            Assert.Equal(1, alias.Confirmations);
            Assert.Null(await AliasAsync(raw, wrong));
        }
        finally { await CleanupAsync(); }
    }

    // ---------------------------------------------------------------- what is NOT a confirmation

    [Fact]
    public async Task TheCreateSupplierSweep_AndMatcherAssignments_AreNotConfirmations()
    {
        try
        {
            var raw = RawName();
            var vat = UniqueVat();
            var partner = await InsertPartnerAsync(vat);
            var swept = await InsertInvoiceAsync("PENDING_SUPPLIER", null, raw, vat);
            Assert.Equal(1, await CreateService().AutoAssignSupplierAsync(vat, raw, partner));
            Assert.Empty(await AliasesAsync(raw));

            // a matcher assignment at create time (VAT tier)
            var dto = new OcrResultDto
            {
                InvoiceNumber = $"S5-{Guid.NewGuid():N}", SupplierName = raw, SupplierVatCode = vat, SupplierCountryCode = "LT",
                InvoiceDate = DateTime.Today.ToString("yyyy-MM-dd"), DueDate = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd"),
                Currency = "EUR", AmountExclVat = 100m, VatRate = 21m, VatAmount = 21m, AmountInclVat = 121m,
                Confidence = new OcrConfidenceDto { Amounts = 90, InvoiceNumber = 90, SupplierName = 90, InvoiceDate = 90 }
            };
            await CreateOcr().ResolveSupplierAsync(dto, new CompanySettings());
            Assert.Equal(partner, dto.SupplierId);
            var created = await CreateService().CreateFromOcrAsync(dto);
            _invoices.Add(created.Id);
            Assert.Empty(await AliasesAsync(raw));
            Assert.Equal(partner, (await new ExpenseServiceReader(_fixture).SupplierOfAsync(swept)));
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task CreateSupplierForThisInvoice_ConfirmsOnlyWhenTheInvoiceHasThatSupplier()
    {
        try
        {
            var raw = RawName();
            var partner = await InsertPartnerAsync();
            var other = await InsertPartnerAsync();
            var invoice = await InsertInvoiceAsync("PENDING_SUPPLIER", null, raw);

            await CreateService().ConfirmSupplierAliasAsync(invoice, partner, "Test User");   // not assigned yet: nothing
            Assert.Empty(await AliasesAsync(raw));

            await CreateService().AutoAssignSupplierAsync(null, raw, partner);                  // the sweep assigns it (not a confirmation)
            Assert.Empty(await AliasesAsync(raw));

            await CreateService().ConfirmSupplierAliasAsync(invoice, other, "Test User");     // assigned to someone else: nothing
            Assert.Empty(await AliasesAsync(raw));
            await CreateService().ConfirmSupplierAliasAsync(invoice, partner, "Test User");   // the dialog's explicit confirmation
            Assert.Equal(1, (await AliasAsync(raw, partner))!.Confirmations);
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task AnAliasOfThePartnersOwnName_AndAnEmptyKey_AreNeverCreated()
    {
        try
        {
            var partner = await InsertPartnerAsync();
            string partnerName;
            await using (var context = await _fixture.Factory.CreateDbContextAsync())
                partnerName = await context.BusinessPartners.Where(b => b.Id == partner).Select(b => b.Name).FirstAsync();

            await AssignAsync(await InsertInvoiceAsync("PENDING_SUPPLIER", null, partnerName.ToUpperInvariant() + ", UAB"), partner);
            await AssignAsync(await InsertInvoiceAsync("PENDING_SUPPLIER", null, "UAB"), partner);      // nothing left after normalisation
            await AssignAsync(await InsertInvoiceAsync("PENDING_SUPPLIER", null, null), partner);

            await using var check = await _fixture.Factory.CreateDbContextAsync();
            Assert.False(await check.SupplierAliases.AnyAsync(a => a.PartnerId == partner));
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task Promotion_ReassignsNothing_NoRetroactiveSweep()
    {
        try
        {
            var raw = RawName();
            var partner = await InsertPartnerAsync();
            var waiting = await InsertInvoiceAsync("PENDING_SUPPLIER", null, raw);
            await PromoteAsync(raw, partner);

            Assert.Null(await new ExpenseServiceReader(_fixture).SupplierOfAsync(waiting));
        }
        finally { await CleanupAsync(); }
    }

    // ---------------------------------------------------------------- conflict, frozen, revoke, unfreeze

    [Fact]
    public async Task AConfirmationForADifferentPartner_FreezesBoth_AndAFrozenAliasIsNeverApplied()
    {
        try
        {
            var raw = RawName();
            var a = await InsertPartnerAsync();
            var b = await InsertPartnerAsync();
            await PromoteAsync(raw, a);
            Assert.Equal(a, (await ResolveAsync(raw)).SupplierId);

            await AssignAsync(await InsertInvoiceAsync("PENDING_SUPPLIER", null, raw), b);

            var aliasA = (await AliasAsync(raw, a))!;
            var aliasB = (await AliasAsync(raw, b))!;
            Assert.Equal("FROZEN", aliasA.State);
            Assert.Equal("FROZEN", aliasB.State);
            Assert.StartsWith("Konfliktas", aliasA.FrozenReason);
            Assert.Contains((await EventsAsync(aliasA.Id)), e => e.Event == "CONFLICT_FROZEN");
            Assert.Contains((await EventsAsync(aliasB.Id)), e => e.Event == "CONFLICT_FROZEN");

            var result = await ResolveAsync(raw);
            Assert.Null(result.SupplierId);
            Assert.Equal(MatchOutcome.NotFound, result.SupplierMatch!.Outcome);
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task ConfirmingAFrozenAlias_StaysFrozen_ButCounts()
    {
        try
        {
            var raw = RawName();
            var a = await InsertPartnerAsync();
            var b = await InsertPartnerAsync();
            await PromoteAsync(raw, a);
            await AssignAsync(await InsertInvoiceAsync("PENDING_SUPPLIER", null, raw), b);      // freezes both
            await AssignAsync(await InsertInvoiceAsync("PENDING_SUPPLIER", null, raw), b);

            var aliasB = (await AliasAsync(raw, b))!;
            Assert.Equal("FROZEN", aliasB.State);
            Assert.Equal(2, aliasB.Confirmations);
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task Revoke_StopsTheApplication_WritesAnEvent_AndAFreshConfirmationStartsOver()
    {
        try
        {
            var raw = RawName();
            var partner = await InsertPartnerAsync();
            var aliasId = await PromoteAsync(raw, partner);

            await using (var context = await _fixture.Factory.CreateDbContextAsync())
                await SupplierAliases.RevokeAsync(context, aliasId, "Test User");

            Assert.Equal("REVOKED", (await AliasAsync(raw, partner))!.State);
            Assert.Contains(await EventsAsync(aliasId), e => e.Event == "REVOKED" && e.Actor == "Test User");
            Assert.Null((await ResolveAsync(raw)).SupplierId);
            await using (var context = await _fixture.Factory.CreateDbContextAsync())
                await Assert.ThrowsAsync<InvalidOperationException>(() => SupplierAliases.RevokeAsync(context, aliasId, "Test User"));

            await AssignAsync(await InsertInvoiceAsync("PENDING_SUPPLIER", null, raw), partner);
            var again = (await AliasAsync(raw, partner))!;
            Assert.Equal("CANDIDATE", again.State);
            Assert.Equal(1, again.Confirmations);
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task Unfreeze_IsRefusedWhileAnotherPartnerHoldsTheActiveAlias_ElseRestoresTheState()
    {
        try
        {
            var raw = RawName();
            var a = await InsertPartnerAsync();
            var b = await InsertPartnerAsync();
            await PromoteAsync(raw, a);
            await AssignAsync(await InsertInvoiceAsync("PENDING_SUPPLIER", null, raw), b);       // both frozen
            var aliasA = (await AliasAsync(raw, a))!;
            var aliasB = (await AliasAsync(raw, b))!;

            await using var context = await _fixture.Factory.CreateDbContextAsync();
            await SupplierAliases.UnfreezeAsync(context, aliasA.Id, "Test User");   // A had 2 confirmations -> ACTIVE
            Assert.Equal("ACTIVE", (await AliasAsync(raw, a))!.State);
            Assert.Contains(await EventsAsync(aliasA.Id), e => e.Event == "UNFROZEN");

            await Assert.ThrowsAsync<InvalidOperationException>(() => SupplierAliases.UnfreezeAsync(context, aliasB.Id, "Test User"));
            Assert.Equal("FROZEN", (await AliasAsync(raw, b))!.State);
            await Assert.ThrowsAsync<InvalidOperationException>(() => SupplierAliases.UnfreezeAsync(context, aliasA.Id, "Test User")); // not frozen any more

            await SupplierAliases.RevokeAsync(context, aliasA.Id, "Test User");
            await SupplierAliases.UnfreezeAsync(context, aliasB.Id, "Test User");   // B had 1 confirmation -> CANDIDATE
            Assert.Equal("CANDIDATE", (await AliasAsync(raw, b))!.State);
        }
        finally { await CleanupAsync(); }
    }

    // ---------------------------------------------------------------- application rules

    [Fact]
    public async Task AnActiveAlias_NeverOverridesAContradictingDocumentVat()
    {
        try
        {
            var raw = RawName();
            var partner = await InsertPartnerAsync(UniqueVat());
            await PromoteAsync(raw, partner);

            var result = await ResolveAsync(raw, UniqueVat());     // a different VAT than the partner's

            Assert.Null(result.SupplierId);
            Assert.Equal(MatchOutcome.Suggested, result.SupplierMatch!.Outcome);
            Assert.Equal(MatchTier.Alias, result.SupplierMatch.Tier);
            Assert.Equal(MatchReason.ConflictingIdentifier, result.SupplierMatch.Reason);
            Assert.Contains(OcrFlag.VendorSuggested, result.Flags);
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task AnAliasOfAnInactivePartner_OnlySuggests()
    {
        try
        {
            var raw = RawName();
            var partner = await InsertPartnerAsync();
            await PromoteAsync(raw, partner);
            await using (var context = await _fixture.Factory.CreateDbContextAsync())
                await context.Database.ExecuteSqlRawAsync("UPDATE business_partners SET is_active = 0 WHERE id = {0}", partner);

            var result = await ResolveAsync(raw);
            Assert.Null(result.SupplierId);
            Assert.Equal(MatchReason.PartnerNotEligible, result.SupplierMatch!.Reason);
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task ApplyingAnAlias_IsRecordedAsAnAppliedEvent_OnCreate()
    {
        try
        {
            var raw = RawName();
            var partner = await InsertPartnerAsync();
            var aliasId = await PromoteAsync(raw, partner);

            var dto = await ResolveAsync(raw);
            dto.InvoiceNumber = $"S5-{Guid.NewGuid():N}";
            dto.InvoiceDate = DateTime.Today.ToString("yyyy-MM-dd");
            dto.DueDate = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd");
            dto.Currency = "EUR"; dto.AmountExclVat = 100m; dto.VatRate = 21m; dto.VatAmount = 21m; dto.AmountInclVat = 121m;
            dto.Confidence = new OcrConfidenceDto { Amounts = 90, InvoiceNumber = 90, SupplierName = 90, InvoiceDate = 90 };
            var invoice = await CreateService().CreateFromOcrAsync(dto);
            _invoices.Add(invoice.Id);

            var applied = Assert.Single(await EventsAsync(aliasId), e => e.Event == "APPLIED");
            Assert.Equal(invoice.Id, applied.InvoiceId);
            // and the alias did not confirm itself: applying is not a confirmation
            Assert.Equal(2, (await AliasAsync(raw, partner))!.Confirmations);
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task CandidatesInTheDetailDialog_ListTheAliasPartnerWithTierAlias()
    {
        try
        {
            var raw = RawName();
            var partner = await InsertPartnerAsync(UniqueVat());
            await PromoteAsync(raw, partner);
            var invoice = await InsertInvoiceAsync("PENDING_SUPPLIER", null, raw, UniqueVat());     // contradicting VAT: suggested, not assigned

            var candidate = Assert.Single(await CreateService().GetSupplierCandidatesAsync(invoice));
            Assert.Equal(partner, candidate.PartnerId);
            Assert.Equal(MatchTier.Alias, candidate.Tier);
        }
        finally { await CleanupAsync(); }
    }

    // a tiny reader so the tests do not reload invoices through the service under test
    private sealed class ExpenseServiceReader
    {
        private readonly DbTestFixture _fixture;
        public ExpenseServiceReader(DbTestFixture fixture) { _fixture = fixture; }
        public async Task<int?> SupplierOfAsync(int invoiceId)
        {
            await using var context = await _fixture.Factory.CreateDbContextAsync();
            return await context.ExpenseInvoices.AsNoTracking().Where(i => i.Id == invoiceId).Select(i => i.SupplierId).FirstAsync();
        }
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
