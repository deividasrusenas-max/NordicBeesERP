using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services;
using NordicBeesERP.Services.Validation;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// OCR Etapas 1 S6(b)–(d) — the VAT-rate whitelist gate (PLAN-ETAPAS1 §2.2, §8; D-038 Q2; D-039 item 3).
/// Row CONFIRMED and rate not allowed → VAT_RATE_NOT_ALLOWED (review); row UNCONFIRMED / unknown country / date
/// outside the table / no date → VAT_RATE_UNCHECKED (information); 0 % never checked; ULAK exempt. Pure
/// verdict tests, then path parity (create / re-OCR / edit) and supplier assignment against nordic_bees_erp_test.
/// The real table has no CONFIRMED row, so CONFIRMED behaviour is tested with the real rows flipped to Confirmed
/// and injected through <c>ExpenseService</c>'s optional row-set constructor argument.
/// </summary>
public class ExpenseVatRateGateTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ExpenseVatRateGateTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    private static readonly IReadOnlyList<VatRateRow> Confirmed =
        VatRateTable.Rows.Select(r => r with { Status = VatRateRowStatus.Confirmed }).ToList();

    private static ExpenseService.VatRateVerdict Verdict(string? country, DateTime? date, decimal header, decimal[] lines,
        IReadOnlyList<VatRateRow>? rows, string? type = "STANDARD") =>
        ExpenseService.EvaluateVatRates(ExpenseService.ToVatRateInput(country, date, header, lines, type), rows);

    private static readonly DateTime Today2026 = new(2026, 9, 26);

    // ---------- verdict (pure) ----------

    [Fact]
    public void ConfirmedRow_RateNotInRow_NotAllowed_RateInRow_Fine()
    {
        Assert.Equal(new ExpenseService.VatRateVerdict(true, false), Verdict("LT", Today2026, 19m, Array.Empty<decimal>(), Confirmed));
        Assert.Equal(new ExpenseService.VatRateVerdict(false, false), Verdict("LT", Today2026, 21m, Array.Empty<decimal>(), Confirmed));
        Assert.Equal(new ExpenseService.VatRateVerdict(false, false), Verdict("LT", Today2026, 9m, Array.Empty<decimal>(), Confirmed));
    }

    [Fact]
    public void UnconfirmedRow_IsOnlyInformation_ForAllowedAndForbiddenRates()
    {
        // the real table: nothing is confirmed, so the gate never stops an invoice yet
        Assert.False(VatRateTable.Rows.Any(r => r.Status == VatRateRowStatus.Confirmed));
        Assert.Equal(new ExpenseService.VatRateVerdict(false, true), Verdict("LT", Today2026, 21m, Array.Empty<decimal>(), null));
        Assert.Equal(new ExpenseService.VatRateVerdict(false, true), Verdict("LT", Today2026, 19m, Array.Empty<decimal>(), null));
    }

    [Theory]
    [InlineData("FR")]
    [InlineData("LI")]   // the suspected master-data errors on staging: not in the table (D-039)
    [InlineData("")]
    [InlineData(null)]
    public void UnknownCountry_Unchecked_EvenWithConfirmedRows(string? country)
    {
        Assert.Equal(new ExpenseService.VatRateVerdict(false, true), Verdict(country, Today2026, 21m, Array.Empty<decimal>(), Confirmed));
    }

    [Fact]
    public void DateOutsideTable_Or_NoDate_Unchecked()
    {
        Assert.Equal(new ExpenseService.VatRateVerdict(false, true), Verdict("LT", new DateTime(2024, 12, 31), 21m, Array.Empty<decimal>(), Confirmed));
        Assert.Equal(new ExpenseService.VatRateVerdict(false, false), Verdict("LT", new DateTime(2025, 1, 1), 21m, Array.Empty<decimal>(), Confirmed));
        Assert.Equal(new ExpenseService.VatRateVerdict(false, true), Verdict("LT", null, 21m, Array.Empty<decimal>(), Confirmed));
    }

    [Fact]
    public void Ro_BoundaryDates_WithConfirmedRows()
    {
        var dayBefore = new DateTime(2025, 7, 31);
        var changeDay = new DateTime(2025, 8, 1);

        Assert.False(Verdict("RO", dayBefore, 19m, Array.Empty<decimal>(), Confirmed).NotAllowed);
        Assert.True(Verdict("RO", dayBefore, 21m, Array.Empty<decimal>(), Confirmed).NotAllowed);
        Assert.False(Verdict("RO", changeDay, 21m, Array.Empty<decimal>(), Confirmed).NotAllowed);
        Assert.True(Verdict("RO", changeDay, 19m, Array.Empty<decimal>(), Confirmed).NotAllowed);
    }

    [Fact]
    public void ZeroPercent_NeverChecked()
    {
        Assert.Equal(new ExpenseService.VatRateVerdict(false, false), Verdict("LT", Today2026, 0m, Array.Empty<decimal>(), Confirmed));
        Assert.Equal(new ExpenseService.VatRateVerdict(false, false), Verdict("LT", Today2026, 0m, new[] { 0m, 0m }, Confirmed));
        Assert.Equal(new ExpenseService.VatRateVerdict(false, false), Verdict("LT", Today2026, 19m, new[] { 0m, 21m }, Confirmed)); // 0 dropped, header ignored
        Assert.Equal(new ExpenseService.VatRateVerdict(false, false), Verdict("XX", Today2026, 0m, Array.Empty<decimal>(), Confirmed)); // unknown country, but nothing to check
    }

    [Fact]
    public void PerLineRates_WinOverTheHeaderRate_HeaderUsedOnlyWhenNoLineHasOne()
    {
        // header 15 (a derived blend) is not judged when the lines carry their real rates
        Assert.Equal(new ExpenseService.VatRateVerdict(false, false), Verdict("LT", Today2026, 15m, new[] { 21m, 9m }, Confirmed));
        // one bad line rate is enough
        Assert.Equal(new ExpenseService.VatRateVerdict(true, false), Verdict("LT", Today2026, 21m, new[] { 21m, 19m }, Confirmed));
        // no line rate → the header rate is judged
        Assert.Equal(new ExpenseService.VatRateVerdict(true, false), Verdict("LT", Today2026, 15m, new[] { 0m, 0m }, Confirmed));
        Assert.Equal(new ExpenseService.VatRateVerdict(true, false), Verdict("LT", Today2026, 17m, Array.Empty<decimal>(), Confirmed));
    }

    [Fact]
    public void TodoRow_Unchecked()
    {
        var rows = new[] { new VatRateRow("LT", null, null, Array.Empty<decimal>(), VatRateRowStatus.Todo, "test") };

        Assert.Equal(new ExpenseService.VatRateVerdict(false, true), Verdict("LT", Today2026, 21m, Array.Empty<decimal>(), rows));
    }

    [Theory]
    [InlineData("ULAK")]
    [InlineData("ulak")]
    public void Ulak_IsExempt_ButStandardIsNot(string type)
    {
        // ULAK's fixed 6 % is not in the LT list; it must not be judged (unreachable through OCR today — defensive)
        Assert.Equal(new ExpenseService.VatRateVerdict(false, false), Verdict("LT", Today2026, 6m, new[] { 6m }, Confirmed, type));
        Assert.Equal(new ExpenseService.VatRateVerdict(false, false), Verdict("XX", null, 6m, new[] { 6m }, null, type));
        Assert.Equal(new ExpenseService.VatRateVerdict(true, false), Verdict("LT", Today2026, 6m, new[] { 6m }, Confirmed, "STANDARD"));
    }

    [Fact]
    public void PartnerStoredAsLtWithNoRealCountry_IsJudgedAgainstLtRates_Documented()
    {
        // PLAN §2.2 risk: business_partners.country_code defaults to "LT". A Polish supplier stored that way is
        // judged as LT — a correct PL 23 % is then "not allowed" (once LT is confirmed). Known limit, not a bug.
        var country = ExpenseService.ResolveRateCountry("LT", "PL", hasSupplier: true);

        Assert.Equal("LT", country);
        Assert.True(Verdict(country, Today2026, 23m, new[] { 23m }, Confirmed).NotAllowed);
        // with the real country on the partner the same invoice passes
        Assert.False(Verdict(ExpenseService.ResolveRateCountry("PL", "PL", true), Today2026, 23m, new[] { 23m }, Confirmed).NotAllowed);
    }

    [Theory]
    [InlineData("PL", "LT", true, "PL")]     // supplier set: the partner's country wins
    [InlineData("", "LT", true, "LT")]       // partner has no country: the document's
    [InlineData(null, "LV", true, "LV")]
    [InlineData("PL", "LT", false, "LT")]    // no supplier: the document's, whatever the partner value
    public void ResolveRateCountry(string? partner, string? document, bool hasSupplier, string expected)
    {
        Assert.Equal(expected, ExpenseService.ResolveRateCountry(partner, document, hasSupplier));
    }

    [Fact]
    public void RecomputeVatRateFlags_DropsStale_KeepsOthers_AddsCurrent()
    {
        var flags = new List<string> { OcrFlag.VatRateNotAllowed, OcrFlag.VatRateUnchecked, OcrFlag.OwnCompany };

        ExpenseService.RecomputeVatRateFlags(flags, ExpenseService.ToVatRateInput("LT", Today2026, 21m, new[] { 21m }, "STANDARD"), Confirmed);
        Assert.Equal(new[] { OcrFlag.OwnCompany }, flags);

        ExpenseService.RecomputeVatRateFlags(flags, ExpenseService.ToVatRateInput("LT", Today2026, 19m, new[] { 19m }, "STANDARD"), Confirmed);
        Assert.Equal(new[] { OcrFlag.OwnCompany, OcrFlag.VatRateNotAllowed }, flags);
    }

    [Fact]
    public void RateFlags_Kinds_NotAllowedIsReview_UncheckedIsInformation()
    {
        Assert.True(NordicBeesERP.Helpers.ExpenseStatusHelper.IsCriticalFlag(OcrFlag.VatRateNotAllowed));
        Assert.False(NordicBeesERP.Helpers.ExpenseStatusHelper.IsCriticalFlag(OcrFlag.VatRateUnchecked));
    }

    // ---------- DI ----------

    [Fact]
    public void ExpenseService_StillConstructibleByDi_WithTheOptionalRowSetUnregistered()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_fixture.Factory);
        services.AddSingleton<IAuthService>(new NullAuthService());
        services.AddSingleton<ICompanySettingsService>(new DefaultCompanySettingsService());
        services.AddScoped<IExpenseService, ExpenseService>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();

        Assert.IsType<ExpenseService>(scope.ServiceProvider.GetRequiredService<IExpenseService>());
    }

    // ---------- DB: paths and assignment ----------

    public enum WritePath { Create, ReOcr, Edit }

    private ExpenseService CreateService(IReadOnlyList<VatRateRow>? rows) =>
        new(_fixture.Factory, new NullAuthService(), new DefaultCompanySettingsService(), rows);

    private async Task<int> InsertSupplierAsync(string countryCode = "LT")
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Supplier,
            Name = $"RateGate Supplier {Guid.NewGuid():N}",
            Country = "Test",
            CountryCode = countryCode,
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

    private async Task<int> InsertInvoiceAsync(int? supplierId, string status, IEnumerable<string> flags, decimal rate,
        string invoiceType = "STANDARD", string? pendingCountry = null, string? pendingVat = null, DateTime? invoiceDate = null)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var number = $"RATEGATE-{Guid.NewGuid():N}";
        var net = 100m;
        var vat = Math.Round(net * rate / 100m, 2);
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices " +
            "(invoice_number, invoice_date, due_date, amount_excl_vat, vat_rate, vat_amount, amount_incl_vat, status, supplier_id, ocr_flags, invoice_type, " +
            "pending_supplier_country_code, pending_supplier_vat, currency, source, ocr_status, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}, {14}, {15}, NOW(), NOW())",
            number, invoiceDate ?? DateTime.Today, DateTime.Today.AddDays(30), net, rate, vat, net + vat, status, supplierId,
            JsonSerializer.Serialize(flags), invoiceType, pendingCountry, pendingVat, "EUR", "MANUAL", "COMPLETED");
        var id = await context.ExpenseInvoices.Where(i => i.InvoiceNumber == number).Select(i => i.Id).FirstAsync();
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoice_lines (invoice_id, description, quantity, unit_price, amount_excl_vat, vat_rate, amount_incl_vat, sort_order) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7})", id, "Eilutė", 1m, null, net, rate, net + vat, 1);
        return id;
    }

    private static OcrResultDto NewOcrResult(int? supplierId, decimal rate, string country = "LT", string? invoiceDate = null)
    {
        var net = 100m;
        var vat = Math.Round(net * rate / 100m, 2);
        return new OcrResultDto
        {
            InvoiceNumber = $"RATEGATE-OCR-{Guid.NewGuid():N}",
            InvoiceDate = invoiceDate ?? DateTime.Today.ToString("yyyy-MM-dd"),
            DueDate = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd"),
            Currency = "EUR",
            AmountExclVat = net,
            VatRate = rate,
            VatAmount = vat,
            AmountInclVat = net + vat,
            SupplierId = supplierId,
            SupplierName = "RateGate OCR Supplier",
            SupplierCountryCode = country,
            Confidence = new OcrConfidenceDto { Amounts = 90, InvoiceNumber = 90, SupplierName = 90, InvoiceDate = 90 },
            Lines = { new OcrLineDto { Description = "Eilutė", Quantity = 1m, AmountExclVat = net, VatRate = rate, AmountInclVat = net + vat } }
        };
    }

    private async Task<ExpenseInvoice> ReloadAsync(int id)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoices.AsNoTracking().FirstAsync(i => i.Id == id);
    }

    private static List<string> FlagsOf(ExpenseInvoice invoice) =>
        string.IsNullOrEmpty(invoice.OcrFlags) ? new() : JsonSerializer.Deserialize<List<string>>(invoice.OcrFlags) ?? new();

    private async Task CleanupAsync(IEnumerable<int> invoiceIds, params int[] supplierIds)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        foreach (var invoiceId in invoiceIds)
        {
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_audit WHERE invoice_id = {0}", invoiceId);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_lines WHERE invoice_id = {0}", invoiceId);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE id = {0}", invoiceId);
        }
        foreach (var supplierId in supplierIds)
            await context.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", supplierId);
    }

    /// <summary>Writes an invoice with an LT supplier at <paramref name="rate"/> through the path; returns stored flags and status.</summary>
    private async Task<(List<string> Flags, string Status)> RunAsync(WritePath path, decimal rate,
        IReadOnlyList<VatRateRow>? rows, string invoiceType = "STANDARD")
    {
        var supplierId = await InsertSupplierAsync("LT");
        int? id = null;
        try
        {
            var service = CreateService(rows);
            switch (path)
            {
                case WritePath.Create:
                    id = (await service.CreateFromOcrAsync(NewOcrResult(supplierId, rate))).Id;
                    break;
                case WritePath.ReOcr:
                    id = await InsertInvoiceAsync(supplierId, "PENDING", Array.Empty<string>(), 21m, invoiceType);
                    await service.UpdateFromOcrAsync(id.Value, NewOcrResult(supplierId, rate));
                    break;
                case WritePath.Edit:
                    id = await InsertInvoiceAsync(supplierId, "PENDING", Array.Empty<string>(), rate, invoiceType);
                    var stored = await ReloadAsync(id.Value);
                    stored.Notes = "pastaba";
                    await using (var context = await _fixture.Factory.CreateDbContextAsync())
                    {
                        var lines = await context.ExpenseInvoiceLines.AsNoTracking().Where(l => l.InvoiceId == id.Value).ToListAsync();
                        await service.SaveInvoiceEditAsync(stored, lines, "Test User");
                    }
                    break;
            }
            var after = await ReloadAsync(id!.Value);
            return (FlagsOf(after), after.Status);
        }
        finally
        {
            await CleanupAsync(id.HasValue ? new[] { id.Value } : Array.Empty<int>(), supplierId);
        }
    }

    [Theory]
    [InlineData(WritePath.Create)]
    [InlineData(WritePath.ReOcr)]
    [InlineData(WritePath.Edit)]
    public async Task Confirmed_ForbiddenRate_NotAllowed_NeedsReview_OnEveryPath(WritePath path)
    {
        var (flags, status) = await RunAsync(path, 19m, Confirmed);

        Assert.Contains(OcrFlag.VatRateNotAllowed, flags);
        Assert.DoesNotContain(OcrFlag.VatRateUnchecked, flags);
        Assert.Equal("NEEDS_REVIEW", status);
    }

    [Theory]
    [InlineData(WritePath.Create)]
    [InlineData(WritePath.ReOcr)]
    [InlineData(WritePath.Edit)]
    public async Task Confirmed_AllowedRate_NoRateFlag_Pending_OnEveryPath(WritePath path)
    {
        var (flags, status) = await RunAsync(path, 21m, Confirmed);

        Assert.DoesNotContain(OcrFlag.VatRateNotAllowed, flags);
        Assert.DoesNotContain(OcrFlag.VatRateUnchecked, flags);
        Assert.Equal("PENDING", status);
    }

    [Theory]
    [InlineData(WritePath.Create)]
    [InlineData(WritePath.ReOcr)]
    [InlineData(WritePath.Edit)]
    public async Task RealTable_Unconfirmed_ForbiddenRate_OnlyInformation_Pending_OnEveryPath(WritePath path)
    {
        var (flags, status) = await RunAsync(path, 19m, null); // null = the real, all-UNCONFIRMED table

        Assert.Contains(OcrFlag.VatRateUnchecked, flags);
        Assert.DoesNotContain(OcrFlag.VatRateNotAllowed, flags);
        Assert.Equal("PENDING", status);
    }

    [Theory]
    [InlineData(WritePath.ReOcr)]
    [InlineData(WritePath.Edit)]
    public async Task Ulak_Exempt_OnEveryPath_WithConfirmedRows(WritePath path)
    {
        // Create always stores STANDARD, so ULAK is only reachable on re-OCR / edit of an existing row
        var (flags, status) = await RunAsync(path, 6m, Confirmed, invoiceType: "ULAK");

        Assert.DoesNotContain(OcrFlag.VatRateNotAllowed, flags);
        Assert.DoesNotContain(OcrFlag.VatRateUnchecked, flags);
        Assert.Equal("PENDING", status);
    }

    [Fact]
    public async Task Edit_CorrectedRate_DropsTheStaleFlag()
    {
        var supplierId = await InsertSupplierAsync("LT");
        var id = await InsertInvoiceAsync(supplierId, "NEEDS_REVIEW", new[] { OcrFlag.VatRateNotAllowed }, 21m);
        try
        {
            var stored = await ReloadAsync(id);
            stored.Notes = "pastaba";
            await using var context = await _fixture.Factory.CreateDbContextAsync();
            var lines = await context.ExpenseInvoiceLines.AsNoTracking().Where(l => l.InvoiceId == id).ToListAsync();
            await CreateService(Confirmed).SaveInvoiceEditAsync(stored, lines, "Test User");

            var after = await ReloadAsync(id);
            Assert.DoesNotContain(OcrFlag.VatRateNotAllowed, FlagsOf(after));
            Assert.Equal("PENDING", after.Status);
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    [Fact]
    public async Task Edit_ApprovedInvoice_NotesOnlyEdit_KeepsApproval_FlagStillRecomputed()
    {
        // PLAN §8: a gate does not reopen an approved invoice whose gate fields were not edited
        var supplierId = await InsertSupplierAsync("LT");
        var id = await InsertInvoiceAsync(supplierId, "PENDING", Array.Empty<string>(), 19m);
        try
        {
            await using (var context = await _fixture.Factory.CreateDbContextAsync())
                await context.Database.ExecuteSqlRawAsync("UPDATE expense_invoices SET approved_by = {0}, approved_at = NOW() WHERE id = {1}", "Tester", id);
            var stored = await ReloadAsync(id);
            stored.Notes = "tik pastaba";
            await using var ctx = await _fixture.Factory.CreateDbContextAsync();
            var lines = await ctx.ExpenseInvoiceLines.AsNoTracking().Where(l => l.InvoiceId == id).ToListAsync();
            await CreateService(Confirmed).SaveInvoiceEditAsync(stored, lines, "Test User");

            var after = await ReloadAsync(id);
            Assert.Equal("PENDING", after.Status);
            Assert.Equal("Tester", after.ApprovedBy);
            Assert.Contains(OcrFlag.VatRateNotAllowed, FlagsOf(after));
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    [Fact]
    public async Task Assignment_Manual_CountryChangesTheVerdict_BothDirections()
    {
        // document says PL (pending country), rate 23; partner is LT → 23 is not an LT rate → review
        var ltPartner = await InsertSupplierAsync("LT");
        var plPartner = await InsertSupplierAsync("PL");
        var toLt = await InsertInvoiceAsync(null, "PENDING_SUPPLIER", new[] { OcrFlag.VendorNotFound }, 23m, pendingCountry: "PL");
        // document said LT and stored NOT_ALLOWED for 23 %; the partner is PL → 23 is fine → flag dropped, PENDING
        var toPl = await InsertInvoiceAsync(null, "PENDING_SUPPLIER", new[] { OcrFlag.VendorNotFound, OcrFlag.VatRateNotAllowed }, 23m, pendingCountry: "LT");
        try
        {
            var service = CreateService(Confirmed);
            await service.AssignSupplierAsync(toLt, ltPartner, "TEST");
            await service.AssignSupplierAsync(toPl, plPartner, "TEST");

            var a = await ReloadAsync(toLt);
            Assert.Contains(OcrFlag.VatRateNotAllowed, FlagsOf(a));
            Assert.Equal("NEEDS_REVIEW", a.Status);
            var b = await ReloadAsync(toPl);
            Assert.DoesNotContain(OcrFlag.VatRateNotAllowed, FlagsOf(b));
            Assert.Equal("PENDING", b.Status);
        }
        finally
        {
            await CleanupAsync(new[] { toLt, toPl }, ltPartner, plPartner);
        }
    }

    [Fact]
    public async Task Assignment_Auto_RecomputesTheRateGate()
    {
        var ltPartner = await InsertSupplierAsync("LT");
        var vat = $"LT{Random.Shared.NextInt64(100_000_000, 999_999_999)}";
        var id = await InsertInvoiceAsync(null, "PENDING_SUPPLIER", new[] { OcrFlag.VendorNotFound }, 23m, pendingCountry: "PL", pendingVat: vat);
        try
        {
            var count = await CreateService(Confirmed).AutoAssignSupplierAsync(vat, null, ltPartner);

            Assert.True(count >= 1);
            var after = await ReloadAsync(id);
            Assert.Contains(OcrFlag.VatRateNotAllowed, FlagsOf(after));
            Assert.Equal("NEEDS_REVIEW", after.Status);
        }
        finally
        {
            await CleanupAsync(new[] { id }, ltPartner);
        }
    }

    [Fact]
    public async Task Assignment_RealTable_OnlyInformation()
    {
        var ltPartner = await InsertSupplierAsync("LT");
        var id = await InsertInvoiceAsync(null, "PENDING_SUPPLIER", new[] { OcrFlag.VendorNotFound }, 23m, pendingCountry: "PL");
        try
        {
            await CreateService(null).AssignSupplierAsync(id, ltPartner, "TEST");

            var after = await ReloadAsync(id);
            Assert.Contains(OcrFlag.VatRateUnchecked, FlagsOf(after));
            Assert.Equal("PENDING", after.Status);
        }
        finally
        {
            await CleanupAsync(new[] { id }, ltPartner);
        }
    }

    [Fact]
    public async Task Assignment_UsesTheStoredInvoiceDate_OutsideTheTable_IsUnchecked()
    {
        var ltPartner = await InsertSupplierAsync("LT");
        var id = await InsertInvoiceAsync(null, "PENDING_SUPPLIER", new[] { OcrFlag.VendorNotFound }, 19m,
            pendingCountry: "LT", invoiceDate: new DateTime(2024, 12, 31));
        try
        {
            await CreateService(Confirmed).AssignSupplierAsync(id, ltPartner, "TEST");

            var flags = FlagsOf(await ReloadAsync(id));
            Assert.Contains(OcrFlag.VatRateUnchecked, flags);
            Assert.DoesNotContain(OcrFlag.VatRateNotAllowed, flags);
        }
        finally
        {
            await CleanupAsync(new[] { id }, ltPartner);
        }
    }

    [Fact]
    public void OnlyTheConfirmedRow_ChangesBehaviour_OtherRowsStayInformation()
    {
        // the header of VatRateTable.cs promises a per-row flip: confirm LT only
        var ltOnly = VatRateTable.Rows.Select(r => r.Country == "LT" ? r with { Status = VatRateRowStatus.Confirmed } : r).ToList();

        Assert.Equal(new ExpenseService.VatRateVerdict(true, false), Verdict("LT", Today2026, 19m, Array.Empty<decimal>(), ltOnly));
        Assert.Equal(new ExpenseService.VatRateVerdict(false, true), Verdict("DE", Today2026, 21m, Array.Empty<decimal>(), ltOnly));
        Assert.Equal(new ExpenseService.VatRateVerdict(false, true), Verdict("RO", new DateTime(2025, 7, 31), 21m, Array.Empty<decimal>(), ltOnly));
        // and one RO row only: the other RO row (other dates) is still unconfirmed
        var roOld = VatRateTable.Rows.Select(r => r.Country == "RO" && r.ValidTo != null ? r with { Status = VatRateRowStatus.Confirmed } : r).ToList();
        Assert.True(Verdict("RO", new DateTime(2025, 7, 31), 21m, Array.Empty<decimal>(), roOld).NotAllowed);
        Assert.Equal(new ExpenseService.VatRateVerdict(false, true), Verdict("RO", new DateTime(2025, 8, 1), 19m, Array.Empty<decimal>(), roOld));
    }

    [Theory]
    [InlineData(WritePath.Create)]
    [InlineData(WritePath.ReOcr)]
    public async Task MissingInvoiceDate_IsNotJudgedAsToday_OnOcrPaths(WritePath path)
    {
        // no date on the document: the stored invoice_date is only a placeholder (today) and must not be judged
        var supplierId = await InsertSupplierAsync("LT");
        int? id = null;
        try
        {
            var service = CreateService(Confirmed);
            var ocr = NewOcrResult(supplierId, 19m, invoiceDate: "");
            if (path == WritePath.Create)
                id = (await service.CreateFromOcrAsync(ocr)).Id;
            else
            {
                id = await InsertInvoiceAsync(supplierId, "PENDING", Array.Empty<string>(), 21m);
                await service.UpdateFromOcrAsync(id.Value, ocr);
            }

            var flags = FlagsOf(await ReloadAsync(id.Value));
            Assert.Contains(OcrFlag.MissingInvDate, flags);
            Assert.Contains(OcrFlag.VatRateUnchecked, flags);
            Assert.DoesNotContain(OcrFlag.VatRateNotAllowed, flags);
        }
        finally
        {
            await CleanupAsync(id.HasValue ? new[] { id.Value } : Array.Empty<int>(), supplierId);
        }
    }

    [Fact]
    public async Task Assignment_MissingInvoiceDateFlag_IsNotJudgedAsTheStoredPlaceholder()
    {
        var ltPartner = await InsertSupplierAsync("LT");
        var id = await InsertInvoiceAsync(null, "PENDING_SUPPLIER", new[] { OcrFlag.VendorNotFound, OcrFlag.MissingInvDate }, 19m, pendingCountry: "LT");
        try
        {
            await CreateService(Confirmed).AssignSupplierAsync(id, ltPartner, "TEST");

            var flags = FlagsOf(await ReloadAsync(id));
            Assert.Contains(OcrFlag.VatRateUnchecked, flags);
            Assert.DoesNotContain(OcrFlag.VatRateNotAllowed, flags);
        }
        finally
        {
            await CleanupAsync(new[] { id }, ltPartner);
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
