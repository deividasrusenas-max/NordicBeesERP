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
/// OCR Etapas 2 S4 (PLAN-ETAPAS2 §1.4–§1.5, §7.2; D-044 Q2, Q5): known IBANs. SUPPLIER_NEW_IBAN only when the supplier has at least
/// one known account and the document's valid IBAN is not among them (review, even with a supplier); the supplier's first IBAN is
/// offered, not flagged; an invalid IBAN is never used or added; „Pridėti IBAN prie tiekėjo" inserts INVOICE_CONFIRMED and clears the
/// flag; PATVIRTINTI keeps it as a record; the partner save upserts a valid IBAN. Real nordic_bees_erp_test (supplier_bank_accounts).
/// </summary>
public class ExpenseKnownIbanTests : IClassFixture<DbTestFixture>
{
    private const string IbanA = "LT121000011101001000";     // valid (Lietuvos bankas example)
    private const string IbanB = "DE89370400440532013000";    // valid
    private const string IbanBad = "LT121000011101001001";    // checksum fails

    private readonly DbTestFixture _fixture;
    public ExpenseKnownIbanTests(DbTestFixture fixture) { _fixture = fixture; }

    private ExpenseService CreateService() => new(_fixture.Factory, new NullAuth(), new DefaultSettings());
    private ExpenseOcrService CreateOcr() => new(_fixture.Factory, new StubVies(), null!, NullLogger<ExpenseOcrService>.Instance);

    private sealed class StubVies : IViesService
    {
        public Task<ViesResult> LookupAsync(string vatCode) => Task.FromResult(new ViesResult { ServiceAvailable = true, IsValid = false });
    }

    private static string UniqueVat() => "LT9" + Random.Shared.NextInt64(10_000_000_000, 99_999_999_999);

    private async Task<int> InsertPartnerAsync(string? vat = null, string? bankAccount = null)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Supplier, Name = $"S4 {Guid.NewGuid():N}", VatCode = vat, BankAccount = bankAccount,
            Country = "Lithuania", CountryCode = "LT", DefaultLanguage = "LT", PaymentTermDays = 14, DefaultVatRate = 21m,
            IsSupplier = true, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();
        return partner.Id;
    }

    private async Task AddKnownAsync(int partnerId, string iban)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        await SupplierBankAccounts.EnsureKnownAsync(context, partnerId, iban, SupplierBankAccount.SourceManual, null, "test");
    }

    private async Task<List<SupplierBankAccount>> KnownRowsAsync(int partnerId)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.SupplierBankAccounts.AsNoTracking().Where(a => a.PartnerId == partnerId).ToListAsync();
    }

    private async Task<int> InsertInvoiceAsync(string status, int? supplierId, string? pendingIban, IEnumerable<string>? flags = null,
        string? approvedBy = null)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var marker = $"S4-{Guid.NewGuid():N}";
        var date = DateTime.Today;
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices " +
            "(invoice_number, invoice_date, due_date, amount_excl_vat, vat_rate, vat_amount, amount_incl_vat, status, supplier_id, ocr_flags, approved_by, notes, currency, source, ocr_status, pending_supplier_bank_account, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, 100, 21, 21, 121, {3}, {4}, {5}, {6}, {7}, 'EUR', 'MANUAL', 'COMPLETED', {8}, NOW(), NOW())",
            marker, date, date.AddDays(30), status, supplierId, JsonSerializer.Serialize(flags ?? Array.Empty<string>()),
            approvedBy, marker, pendingIban);
        return await context.ExpenseInvoices.Where(i => i.Notes == marker).Select(i => i.Id).FirstAsync();
    }

    private async Task<ExpenseInvoice> ReloadAsync(int id)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoices.AsNoTracking().FirstAsync(i => i.Id == id);
    }

    private static List<string> FlagsOf(ExpenseInvoice i) => ExpenseStatusHelper.ParseFlags(i.OcrFlags);

    private async Task<List<ExpenseInvoiceAudit>> AuditsAsync(int id)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoiceAudits.AsNoTracking().Where(a => a.InvoiceId == id).ToListAsync();
    }

    private async Task CleanupAsync(IEnumerable<int> invoices, params int[] partners)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        foreach (var id in invoices)
        {
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_audit WHERE invoice_id = {0}", id);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_lines WHERE invoice_id = {0}", id);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE id = {0}", id);
        }
        foreach (var id in partners)
        {
            await context.Database.ExecuteSqlRawAsync("DELETE FROM supplier_bank_accounts WHERE partner_id = {0}", id);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", id);
        }
    }

    private static OcrResultDto Doc(string vat, string? iban) => new()
    {
        SupplierName = $"Dok {Guid.NewGuid():N}", SupplierVatCode = vat, SupplierBankAccount = iban ?? "", SupplierCountryCode = "LT",
        InvoiceNumber = $"S4-{Guid.NewGuid():N}", InvoiceDate = DateTime.Today.ToString("yyyy-MM-dd"),
        DueDate = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd"), Currency = "EUR",
        AmountExclVat = 100m, VatRate = 21m, VatAmount = 21m, AmountInclVat = 121m,
        Confidence = new OcrConfidenceDto { Amounts = 90, InvoiceNumber = 90, SupplierName = 90, InvoiceDate = 90 }
    };

    private async Task<OcrResultDto> ResolveAsync(OcrResultDto result)
    {
        await CreateOcr().ResolveSupplierAsync(result, new CompanySettings());
        ExpenseOcrService.ApplySupplierFlags(result);
        return result;
    }

    // ---------------------------------------------------------------- matching: known / new / first IBAN

    [Fact]
    public async Task KnownIban_AssignsByVat_NoNewIbanFlag()
    {
        var vat = UniqueVat();
        var partner = await InsertPartnerAsync(vat);
        await AddKnownAsync(partner, IbanA);
        try
        {
            var result = await ResolveAsync(Doc(vat, IbanA.Insert(4, " ")));
            Assert.Equal(partner, result.SupplierId);
            Assert.True(result.SupplierMatch!.DocumentIbanKnown);
            Assert.DoesNotContain(OcrFlag.SupplierNewIban, result.Flags);
        }
        finally { await CleanupAsync(Array.Empty<int>(), partner); }
    }

    [Fact]
    public async Task NewIban_ForASupplierWithAKnownAccount_FlagsReview_OnCreate_AndStoresTheIban()
    {
        var vat = UniqueVat();
        var partner = await InsertPartnerAsync(vat);
        await AddKnownAsync(partner, IbanA);
        var dto = await ResolveAsync(Doc(vat, IbanB));
        Assert.Contains(OcrFlag.SupplierNewIban, dto.Flags);          // preview
        var invoice = await CreateService().CreateFromOcrAsync(dto);
        try
        {
            var stored = await ReloadAsync(invoice.Id);
            Assert.Equal(partner, stored.SupplierId);
            Assert.Equal("NEEDS_REVIEW", stored.Status);              // review even though a supplier is assigned (not the D-039 item 2 exemption)
            Assert.Contains(OcrFlag.SupplierNewIban, FlagsOf(stored));
            Assert.Equal(IbanB, stored.PendingSupplierBankAccount);   // PLAN §1.5: kept with a supplier so the dialog knows what to add
        }
        finally { await CleanupAsync(new[] { invoice.Id }, partner); }
    }

    [Fact]
    public async Task FirstIban_OfASupplierWithoutKnownAccounts_RaisesNoFlag_ButIsOffered()
    {
        var vat = UniqueVat();
        var partner = await InsertPartnerAsync(vat);
        var dto = await ResolveAsync(Doc(vat, IbanA));
        var invoice = await CreateService().CreateFromOcrAsync(dto);
        try
        {
            var stored = await ReloadAsync(invoice.Id);
            Assert.Equal("PENDING", stored.Status);
            Assert.DoesNotContain(OcrFlag.SupplierNewIban, FlagsOf(stored));
            Assert.Equal(IbanA, await CreateService().GetAddableIbanAsync(invoice.Id));
        }
        finally { await CleanupAsync(new[] { invoice.Id }, partner); }
    }

    [Fact]
    public async Task InvalidDocumentIban_NeverFlags_NeverOffered_NeverAdded()
    {
        var vat = UniqueVat();
        var partner = await InsertPartnerAsync(vat);
        await AddKnownAsync(partner, IbanA);
        var dto = await ResolveAsync(Doc(vat, IbanBad));
        var invoice = await CreateService().CreateFromOcrAsync(dto);
        try
        {
            Assert.DoesNotContain(OcrFlag.SupplierNewIban, FlagsOf(await ReloadAsync(invoice.Id)));
            Assert.Null(await CreateService().GetAddableIbanAsync(invoice.Id));
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().AddSupplierIbanAsync(invoice.Id, "Test User"));
            Assert.Single(await KnownRowsAsync(partner));
        }
        finally { await CleanupAsync(new[] { invoice.Id }, partner); }
    }

    // ---------------------------------------------------------------- "Pridėti IBAN prie tiekėjo"

    [Fact]
    public async Task AddIban_InsertsInvoiceConfirmed_ClearsFlag_StatusFollowsTheRules_AuditIsMasked_SecondCallRefused()
    {
        var partner = await InsertPartnerAsync();
        await AddKnownAsync(partner, IbanA);
        var invoice = await InsertInvoiceAsync("NEEDS_REVIEW", partner, IbanB, new[] { OcrFlag.SupplierNewIban });
        try
        {
            var service = CreateService();
            Assert.Equal(IbanB, await service.GetAddableIbanAsync(invoice));

            await service.AddSupplierIbanAsync(invoice, "Test User");

            var rows = await KnownRowsAsync(partner);
            var added = Assert.Single(rows, r => r.Iban == IbanB);
            Assert.Equal("INVOICE_CONFIRMED", added.Source);
            Assert.Equal(invoice, added.SourceInvoiceId);
            Assert.True(added.IsActive);

            var after = await ReloadAsync(invoice);
            Assert.DoesNotContain(OcrFlag.SupplierNewIban, FlagsOf(after));
            Assert.Equal("PENDING", after.Status);                    // the flag was the only reason for review
            var audit = Assert.Single(await AuditsAsync(invoice), a => a.Action == "SUPPLIER_IBAN_ADDED");
            Assert.DoesNotContain(IbanB, audit.ActionDetails);
            Assert.Contains("DE89…3000", audit.ActionDetails);

            Assert.Null(await service.GetAddableIbanAsync(invoice));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddSupplierIbanAsync(invoice, "Test User"));
            Assert.Equal(2, (await KnownRowsAsync(partner)).Count);
        }
        finally { await CleanupAsync(new[] { invoice }, partner); }
    }

    [Fact]
    public async Task AddIban_KeepsNeedsReview_WhenAnotherReviewFlagRemains()
    {
        var partner = await InsertPartnerAsync();
        await AddKnownAsync(partner, IbanA);
        var invoice = await InsertInvoiceAsync("NEEDS_REVIEW", partner, IbanB, new[] { OcrFlag.SupplierNewIban, OcrFlag.ZeroVat });
        try
        {
            await CreateService().AddSupplierIbanAsync(invoice, "Test User");
            Assert.Equal("NEEDS_REVIEW", (await ReloadAsync(invoice)).Status);
        }
        finally { await CleanupAsync(new[] { invoice }, partner); }
    }

    [Fact]
    public async Task ApproveWithoutAdding_KeepsTheFlagAsARecord_AndTheEditPathCarriesIt()
    {
        var partner = await InsertPartnerAsync();
        await AddKnownAsync(partner, IbanA);
        var invoice = await InsertInvoiceAsync("NEEDS_REVIEW", partner, IbanB, new[] { OcrFlag.SupplierNewIban });
        try
        {
            var service = CreateService();
            // the edit path carries it and it keeps the invoice in review
            var toEdit = await ReloadAsync(invoice);
            toEdit.Notes = "edited";
            await service.SaveInvoiceEditAsync(toEdit, new List<ExpenseInvoiceLine>(), "Test User");
            var edited = await ReloadAsync(invoice);
            Assert.Contains(OcrFlag.SupplierNewIban, FlagsOf(edited));
            Assert.Equal("NEEDS_REVIEW", edited.Status);

            await service.ApproveAsync(invoice, "Test User");
            var approved = await ReloadAsync(invoice);
            Assert.Equal("PENDING", approved.Status);
            Assert.Contains(OcrFlag.SupplierNewIban, FlagsOf(approved)); // the NUMBER_* pattern: approval does not touch flags
            Assert.Single(await KnownRowsAsync(partner));                // nothing was added
        }
        finally { await CleanupAsync(new[] { invoice }, partner); }
    }

    // ---------------------------------------------------------------- the other paths recompute it

    [Fact]
    public async Task Assign_And_Change_RecomputeTheFlagAgainstTheNewSupplier()
    {
        var withKnown = await InsertPartnerAsync();
        await AddKnownAsync(withKnown, IbanA);
        var empty = await InsertPartnerAsync();
        var invoice = await InsertInvoiceAsync("PENDING_SUPPLIER", null, IbanB, new[] { OcrFlag.VendorNotFound });
        try
        {
            var service = CreateService();
            await service.AssignSupplierAsync(invoice, withKnown, "Test User");
            var assigned = await ReloadAsync(invoice);
            Assert.Contains(OcrFlag.SupplierNewIban, FlagsOf(assigned));
            Assert.Equal("NEEDS_REVIEW", assigned.Status);

            await service.ChangeSupplierAsync(invoice, empty, "Test User");   // no known account: nothing to compare with
            var changed = await ReloadAsync(invoice);
            Assert.DoesNotContain(OcrFlag.SupplierNewIban, FlagsOf(changed));
            Assert.Equal("PENDING", changed.Status);
        }
        finally { await CleanupAsync(new[] { invoice }, withKnown, empty); }
    }

    [Fact]
    public async Task ReOcr_RecomputesAgainstTheKeptSupplier_NotTheFreshMatch()
    {
        var kept = await InsertPartnerAsync();
        await AddKnownAsync(kept, IbanA);
        var invoice = await InsertInvoiceAsync("PENDING", kept, null);
        try
        {
            var fresh = Doc(UniqueVat(), IbanB);
            fresh.InvoiceNumber = (await ReloadAsync(invoice)).InvoiceNumber!;
            await CreateService().UpdateFromOcrAsync(invoice, fresh);

            var after = await ReloadAsync(invoice);
            Assert.Equal(kept, after.SupplierId);
            Assert.Contains(OcrFlag.SupplierNewIban, FlagsOf(after));
            Assert.Equal(IbanB, after.PendingSupplierBankAccount);
        }
        finally { await CleanupAsync(new[] { invoice }, kept); }
    }

    // ---------------------------------------------------------------- D-039 document-flag logic is unaffected

    [Fact]
    public async Task InvalidIbanFlag_WithASupplier_StaysInformation_AndTheStoredPendingIbanDoesNotRecomputeIt()
    {
        var partner = await InsertPartnerAsync();
        // a supplier is assigned, the document IBAN (kept since S4) is invalid and INVALID_IBAN is stored as a carried fact
        var invoice = await InsertInvoiceAsync("PENDING", partner, IbanBad, new[] { OcrFlag.InvalidIban });
        var clean = await InsertInvoiceAsync("PENDING", partner, IbanBad);
        try
        {
            var service = CreateService();
            foreach (var id in new[] { invoice, clean })
            {
                var toEdit = await ReloadAsync(id);
                toEdit.Notes = "edited";
                await service.SaveInvoiceEditAsync(toEdit, new List<ExpenseInvoiceLine>(), "Test User");
            }

            var carried = await ReloadAsync(invoice);
            Assert.Contains(OcrFlag.InvalidIban, FlagsOf(carried));    // carried, as before S4
            Assert.Equal("PENDING", carried.Status);                    // information once a supplier exists
            var notAdded = await ReloadAsync(clean);
            Assert.DoesNotContain(OcrFlag.InvalidIban, FlagsOf(notAdded)); // keyed on SupplierId == null: a supplier'd invoice recomputes nothing
        }
        finally { await CleanupAsync(new[] { invoice, clean }, partner); }
    }

    [Fact]
    public void NewIbanFlag_HasLabelColour_IsCritical_AndReviewOnly()
    {
        Assert.Equal("SUPPLIER_NEW_IBAN", OcrFlag.SupplierNewIban);
        Assert.Equal("Naujas tiekėjo IBAN", ExpenseStatusHelper.GetFlagLabel(OcrFlag.SupplierNewIban));
        Assert.Equal(MudBlazor.Color.Warning, ExpenseStatusHelper.GetFlagColor(OcrFlag.SupplierNewIban));
        Assert.True(ExpenseStatusHelper.IsCriticalFlag(OcrFlag.SupplierNewIban));
    }

    // ---------------------------------------------------------------- partner save upserts a valid IBAN

    [Fact]
    public async Task PartnerSave_UpsertsAValidIban_IgnoresAnInvalidOne_NoDuplicateOnResave()
    {
        var service = new SupplierService(_fixture.Factory);
        var supplier = new Supplier
        {
            Name = $"S4 Save {Guid.NewGuid():N}", City = "Vilnius", PaymentTermDays = 14, DefaultVatRate = 21m,
            IsSupplier = true, IsActive = true, BankAccount = IbanA.Insert(4, " "), PartnerType = PartnerType.Supplier
        };
        var saved = await service.SaveSupplierAsync(supplier);
        try
        {
            var rows = await KnownRowsAsync(saved.Id);
            var row = Assert.Single(rows);
            Assert.Equal(IbanA, row.Iban);
            Assert.Equal("MANUAL", row.Source);

            await service.SaveSupplierAsync(saved);                         // update path, same IBAN: no duplicate
            Assert.Single(await KnownRowsAsync(saved.Id));

            saved.BankAccount = IbanBad;                                    // invalid: never added
            await service.SaveSupplierAsync(saved);
            saved.BankAccount = IbanB;
            await service.SaveSupplierAsync(saved);                         // a second valid IBAN is added
            Assert.Equal(new[] { IbanA, IbanB }.OrderBy(x => x), (await KnownRowsAsync(saved.Id)).Select(r => r.Iban).OrderBy(x => x));
        }
        finally { await CleanupAsync(Array.Empty<int>(), saved.Id); }
    }

    [Fact]
    public async Task BusinessPartnerCreateAndUpdate_UpsertAValidIban()
    {
        var service = new SupplierService(_fixture.Factory);
        var created = await service.CreateBusinessPartnerAsync(new BusinessPartner
        {
            PartnerType = PartnerType.Supplier, Name = $"S4 BP {Guid.NewGuid():N}", Country = "Lithuania", CountryCode = "LT",
            DefaultLanguage = "LT", PaymentTermDays = 14, DefaultVatRate = 21m, IsSupplier = true, IsActive = true, BankAccount = IbanA
        });
        try
        {
            Assert.Equal(IbanA, Assert.Single(await KnownRowsAsync(created.Id)).Iban);
            created.BankAccount = IbanB;
            await service.UpdateBusinessPartnerAsync(created);
            Assert.Equal(2, (await KnownRowsAsync(created.Id)).Count);
        }
        finally { await CleanupAsync(Array.Empty<int>(), created.Id); }
    }

    // ---------------------------------------------------------------- backfill SQL == IbanValidator

    [Fact]
    public async Task BackfillSql_AgreesWithIbanValidator_OnEverySample_AndIsIdempotent()
    {
        var samples = new[]
        {
            IbanA, IbanA.Insert(4, " ").ToLowerInvariant(), IbanB, IbanBad,
            "LT12100001110100100", "LT1210000111010010000",            // 19 / 21 chars: wrong length for LT
            "GB82WEST12345698765432", "GB82 WEST 1234 5698 7654 32",   // valid, with spaces
            "NO9386011117947", "NO9386011117948",                       // unknown-length country (15 chars): mod-97 only
            "NO93860111179", "XX", "12", "  ", "LT12-1000-0111-0100-1000", // too short / no letters / blank / bad characters
            "EE382200221020145685", "PL61109010140000071219812874"
        };
        var partners = new List<int>();
        try
        {
            foreach (var sample in samples) partners.Add(await InsertPartnerAsync(null, sample));

            var sql = File.ReadAllText(Path.Combine(RepoRoot(), "Migrations", "Scripts", "20260927_backfill_supplier_bank_accounts.sql"));
            var insert = sql[sql.IndexOf("-- ---- backfill", StringComparison.Ordinal)..]
                .Replace("/*SCOPE*/", $"AND id IN ({string.Join(",", partners)})");
            Assert.Contains("AND id IN (", insert);

            await using (var context = await _fixture.Factory.CreateDbContextAsync())
                await context.Database.ExecuteSqlRawAsync(insert);
            await using (var context = await _fixture.Factory.CreateDbContextAsync())
                await context.Database.ExecuteSqlRawAsync(insert);     // idempotent

            for (var i = 0; i < samples.Length; i++)
            {
                var expected = NordicBeesERP.Services.Validation.IbanValidator.Validate(samples[i]);
                var rows = await KnownRowsAsync(partners[i]);
                if (expected.IsValid)
                {
                    var row = Assert.Single(rows);
                    Assert.Equal(expected.NormalizedIban, row.Iban);
                    Assert.Equal("MIGRATED", row.Source);
                    Assert.True(row.IsActive);
                    Assert.Null(row.SourceInvoiceId);
                }
                else
                {
                    Assert.True(rows.Count == 0, $"sample '{samples[i]}' must not be migrated ({expected.Reason})");
                }
            }
        }
        finally { await CleanupAsync(Array.Empty<int>(), partners.ToArray()); }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "NordicBeesERP.csproj"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
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
