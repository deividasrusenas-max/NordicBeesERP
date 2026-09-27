using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NordicBeesERP.Helpers;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// OCR Etapas 2 fix-up A1: the document's OCR supplier name / VAT / company code are stored on <c>pending_supplier_*</c>
/// even when a supplier is matched (create and re-OCR) — IBAN was already kept since S4 (PLAN §1.5); this closes the alias
/// learning gap it left (a matcher-assigned invoice had no OCR name for <c>ChangeSupplierAsync</c> to teach an alias with).
/// Address / city / postal code / country are unaffected — still pending-only. Real nordic_bees_erp_test.
/// </summary>
public class ExpenseSupplierIdentityKeptTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;
    public ExpenseSupplierIdentityKeptTests(DbTestFixture fixture) { _fixture = fixture; }

    private ExpenseService CreateService() => new(_fixture.Factory, new NullAuth(), new DefaultSettings());
    private ExpenseOcrService CreateOcr() => new(_fixture.Factory, new StubVies(), null!, NullLogger<ExpenseOcrService>.Instance);

    private sealed class StubVies : IViesService
    {
        public Task<ViesResult> LookupAsync(string vatCode) => Task.FromResult(new ViesResult { ServiceAvailable = true, IsValid = false });
    }

    private static string UniqueVat() => "LT9" + Random.Shared.NextInt64(10_000_000_000, 99_999_999_999);
    private static string RawName() => $"A1 Tiekejas {Guid.NewGuid():N} UAB";

    private readonly List<int> _partners = new();
    private readonly List<int> _invoices = new();

    private async Task<int> InsertPartnerAsync(string? vat = null)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Supplier, Name = $"A1 Partner {Guid.NewGuid():N}", VatCode = vat, Country = "Lithuania",
            CountryCode = "LT", DefaultLanguage = "LT", PaymentTermDays = 14, DefaultVatRate = 21m, IsSupplier = true,
            IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();
        _partners.Add(partner.Id);
        return partner.Id;
    }

    private static OcrResultDto Doc(int? supplierId, string name, string vat, string companyCode, string address = "", string city = "") => new()
    {
        InvoiceNumber = $"A1-{Guid.NewGuid():N}", InvoiceDate = DateTime.Today.ToString("yyyy-MM-dd"),
        DueDate = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd"), Currency = "EUR",
        AmountExclVat = 100m, VatRate = 21m, VatAmount = 21m, AmountInclVat = 121m,
        SupplierId = supplierId, SupplierName = name, SupplierVatCode = vat, SupplierCompanyCode = companyCode,
        SupplierAddress = address, SupplierCity = city, SupplierCountryCode = "LT",
        Confidence = new OcrConfidenceDto { Amounts = 90, InvoiceNumber = 90, SupplierName = 90, InvoiceDate = 90 }
    };

    private async Task<ExpenseInvoice> ReloadAsync(int id)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoices.AsNoTracking().FirstAsync(i => i.Id == id);
    }

    private async Task<List<SupplierAlias>> AliasesAsync(string rawName)
    {
        var key = SupplierAliases.KeyFor(rawName)!;
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.SupplierAliases.AsNoTracking().Where(a => a.AliasKey == key).ToListAsync();
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

    // ---------------------------------------------------------------- A1: create / re-OCR keep the identifiers

    [Fact]
    public async Task CreateFromOcr_WithAMatchedSupplier_StillStoresTheOcrNameVatAndCompanyCode()
    {
        var vat = UniqueVat();
        var partner = await InsertPartnerAsync(vat);
        var name = RawName();
        var invoice = await CreateService().CreateFromOcrAsync(Doc(partner, name, vat, "123456789", "Gatvė 1", "Vilnius"));
        _invoices.Add(invoice.Id);
        try
        {
            var stored = await ReloadAsync(invoice.Id);
            Assert.Equal(partner, stored.SupplierId);
            Assert.Equal(name, stored.PendingSupplierName);
            Assert.Equal(vat, stored.PendingSupplierVat);
            Assert.Equal("123456789", stored.PendingSupplierCompanyCode);
            // unaffected by A1 — still pending-only
            Assert.Null(stored.PendingSupplierAddress);
            Assert.Null(stored.PendingSupplierCity);
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task ReOcr_KeptSupplier_UpdatesTheStoredOcrIdentifiersFromTheFreshDocument()
    {
        var partner = await InsertPartnerAsync();
        var invoice = await CreateService().CreateFromOcrAsync(Doc(partner, RawName(), UniqueVat(), "111111111"));
        _invoices.Add(invoice.Id);
        try
        {
            var freshName = RawName();
            var freshVat = UniqueVat();
            var fresh = Doc(partner, freshName, freshVat, "222222222");
            fresh.InvoiceNumber = invoice.InvoiceNumber!;
            await CreateService().UpdateFromOcrAsync(invoice.Id, fresh);

            var stored = await ReloadAsync(invoice.Id);
            Assert.Equal(partner, stored.SupplierId);
            Assert.Equal(freshName, stored.PendingSupplierName);
            Assert.Equal(freshVat, stored.PendingSupplierVat);
            Assert.Equal("222222222", stored.PendingSupplierCompanyCode);
        }
        finally { await CleanupAsync(); }
    }

    // ---------------------------------------------------------------- the fix itself: ChangeSupplierAsync can now teach an alias

    [Fact]
    public async Task ChangeSupplierAsync_OnAMatcherAssignedInvoice_NowConfirmsAnAlias()
    {
        // the invoice was NEVER PENDING_SUPPLIER — the matcher assigned `oldPartner` at create time, exactly the gap A1 closes
        var oldPartner = await InsertPartnerAsync();
        var newPartner = await InsertPartnerAsync();
        var name = RawName();
        var invoice = await CreateService().CreateFromOcrAsync(Doc(oldPartner, name, UniqueVat(), "999999999"));
        _invoices.Add(invoice.Id);
        try
        {
            Assert.Empty(await AliasesAsync(name));   // nothing to confirm yet — sanity check on the fixture

            await CreateService().ChangeSupplierAsync(invoice.Id, newPartner, "Test User");

            var alias = Assert.Single(await AliasesAsync(name), a => a.PartnerId == newPartner);
            Assert.Equal(1, alias.Confirmations);
            Assert.Equal("CANDIDATE", alias.State);
            Assert.Equal(name, alias.RawExample);
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task ChangeSupplierAsync_TwiceOnMatcherAssignedInvoices_PromotesTheAlias_AndTheMatcherThenAppliesIt()
    {
        var oldA = await InsertPartnerAsync();
        var oldB = await InsertPartnerAsync();
        var newPartner = await InsertPartnerAsync();
        var name = RawName();

        var service = CreateService();
        var first = await service.CreateFromOcrAsync(Doc(oldA, name, UniqueVat(), "111111111"));
        var second = await service.CreateFromOcrAsync(Doc(oldB, name, UniqueVat(), "222222222"));
        _invoices.Add(first.Id); _invoices.Add(second.Id);
        try
        {
            await service.ChangeSupplierAsync(first.Id, newPartner, "Test User");
            await service.ChangeSupplierAsync(second.Id, newPartner, "Test User");

            var alias = Assert.Single(await AliasesAsync(name), a => a.PartnerId == newPartner);
            Assert.Equal("ACTIVE", alias.State);
            Assert.Equal(2, alias.Confirmations);

            // ACTIVE aliases feed the matcher (S5b/c): the same OCR name on a fresh document is now assigned by tier Alias
            var (matchedId, _) = await CreateOcr().FindSupplierIdAsync(name, "");
            Assert.Equal(newPartner, matchedId);
        }
        finally { await CleanupAsync(); }
    }

    // ---------------------------------------------------------------- readers verified unaffected (D-039 item 2, the sweep)

    [Fact]
    public async Task EditPath_DocumentFlagLogic_StaysUnaffected_InvalidVatFormatRemainsInformationWithASupplier()
    {
        // ComputeManualEditFlags builds its SupplierDocumentInput only when stored.SupplierId == null (ExpenseService.cs ~:544-546);
        // it must keep returning null for a matched invoice even though pending_supplier_vat is no longer null after A1.
        var partner = await InsertPartnerAsync();
        var dto = Doc(partner, RawName(), "LT12345", "123456789");   // malformed VAT -> INVALID_VAT_FORMAT is a plain flag on the DTO here
        dto.Flags.Add(OcrFlag.InvalidVatFormat);
        var invoice = await CreateService().CreateFromOcrAsync(dto);
        _invoices.Add(invoice.Id);
        try
        {
            var stored = await ReloadAsync(invoice.Id);
            Assert.NotNull(stored.PendingSupplierVat);   // A1: no longer null — the precondition this test actually exercises
            Assert.Equal("PENDING", stored.Status);      // D-039 item 2: information, not review, once a supplier exists

            stored.Notes = "edited";
            await CreateService().SaveInvoiceEditAsync(stored, new List<ExpenseInvoiceLine>(), "Test User");

            var afterEdit = await ReloadAsync(invoice.Id);
            Assert.Contains(OcrFlag.InvalidVatFormat, ExpenseStatusHelper.ParseFlags(afterEdit.OcrFlags));
            Assert.Equal("PENDING", afterEdit.Status);    // still information after the edit path recomputes flags
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task AutoAssignSweep_StillIgnoresInvoicesThatAlreadyHaveASupplier_EvenWithAMatchingPendingVat()
    {
        // guards against a naive A1 implementation accidentally widening the sweep's WHERE (ExpenseService.cs ~:1576-1578):
        // it must stay scoped to PENDING_SUPPLIER && SupplierId == null regardless of what pending_supplier_vat now holds.
        var vat = UniqueVat();
        var alreadyAssigned = await InsertPartnerAsync(vat);
        var sweepTarget = await InsertPartnerAsync();
        var invoice = await CreateService().CreateFromOcrAsync(Doc(alreadyAssigned, RawName(), vat, "123456789"));
        _invoices.Add(invoice.Id);
        try
        {
            var count = await CreateService().AutoAssignSupplierAsync(vat, null, sweepTarget);

            Assert.Equal(0, count);
            Assert.Equal(alreadyAssigned, (await ReloadAsync(invoice.Id)).SupplierId);
        }
        finally { await CleanupAsync(); }
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
