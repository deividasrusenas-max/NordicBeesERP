using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Helpers;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// OCR Etapas 1 S5(b) — IBAN and VAT-code format gates owned by the shared helper
/// (<see cref="ExpenseService.RecomputeSupplierDocumentFlags"/> via <c>RecomputeValidationFlags</c>):
/// mapping of validator reasons to flags (pure), and path parity for create / re-OCR / manual edit (DB).
/// On the edit path the inputs are the pending_* columns when there is no supplier; with a supplier the
/// document's codes are stored nowhere else, so the stored flags are carried over.
/// </summary>
public class ExpenseSupplierDocumentFlagsTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ExpenseSupplierDocumentFlagsTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    private const string ValidLtIban = "LT121000011101001000";
    private const string ValidNoIban = "NO9386011117947"; // valid mod-97, country not in the known-length table

    private static List<string> Recompute(string? vat, string? hint, string? iban, params string[] incoming)
    {
        var flags = incoming.ToList();
        ExpenseService.RecomputeSupplierDocumentFlags(flags, new ExpenseService.SupplierDocumentInput(vat, hint, iban));
        return flags;
    }

    // ---------- IBAN mapping (pure) ----------

    [Theory]
    [InlineData("LT12100001110100100")]        // WrongLength
    [InlineData("LT121000011101001000000")]    // WrongLength
    [InlineData("LT35100001110100100")]        // WrongLength (19, LT needs 20) with a valid mod-97: needs the per-country length table
    [InlineData("LT12100001110100100$")]       // BadCharacters
    [InlineData("LT131000011101001000")]       // ChecksumFailed
    public void Iban_Invalid_FlagsInvalidIban(string iban)
    {
        Assert.Contains(OcrFlag.InvalidIban, Recompute(null, null, iban));
    }

    [Theory]
    [InlineData(ValidLtIban)]
    [InlineData("LT12 1000 0111 0100 1000")]   // spaces are stripped
    [InlineData(ValidNoIban)]                  // UnknownCountryLength: mod-97 passed → no flag
    [InlineData("")]                           // Empty → no flag
    [InlineData(null)]
    public void Iban_ValidUnknownOrEmpty_NoFlag(string? iban)
    {
        Assert.DoesNotContain(OcrFlag.InvalidIban, Recompute(null, null, iban));
    }

    // ---------- VAT-code mapping (pure) ----------

    [Theory]
    [InlineData("LT12345", null, OcrFlag.InvalidVatFormat)]          // WrongFormat
    [InlineData("LT12345678A", null, OcrFlag.InvalidVatFormat)]      // WrongFormat (letter in the body)
    [InlineData("123456789", "LV", OcrFlag.InvalidVatFormat)]        // unprefixed: the hint decides (LV needs 11)
    [InlineData("GB123456789", null, OcrFlag.VatFormatUnchecked)]    // UnknownCountry (prefix outside the table)
    [InlineData("123456789", null, OcrFlag.VatFormatUnchecked)]      // UnknownCountry (no prefix, no hint)
    [InlineData("LV12345678901", "LT", OcrFlag.VatCountryMismatch)]  // well-formed, prefix ≠ supplier country
    public void Vat_Reason_MapsToFlag(string vat, string? hint, string expected)
    {
        var flags = Recompute(vat, hint, null);

        Assert.Equal(new[] { expected }, flags);
    }

    [Theory]
    [InlineData("LT123456789", null)]
    [InlineData("LT123456789012", "LT")]
    [InlineData("123456789", "LT")]
    [InlineData("", "LT")]                 // Empty → no flag
    [InlineData(null, null)]
    [InlineData("  - ", null)]             // separators only → Empty
    public void Vat_ValidOrEmpty_NoFlag(string? vat, string? hint)
    {
        Assert.Empty(Recompute(vat, hint, null));
    }

    [Fact]
    public void CountryMismatch_IsInformation_NotAFormatError()
    {
        var flags = Recompute("LV12345678901", "LT", null);

        Assert.DoesNotContain(OcrFlag.InvalidVatFormat, flags);
        Assert.Equal("PVM kodo šalis nesutampa su tiekėjo šalimi", ExpenseStatusHelper.GetFlagLabel(OcrFlag.VatCountryMismatch));
        Assert.False(ExpenseStatusHelper.IsCriticalFlag(OcrFlag.VatCountryMismatch));
    }

    [Fact]
    public void StaleOwnedFlags_AreDroppedAndOthersKept()
    {
        var flags = Recompute("LT123456789", "LT", ValidLtIban,
            OcrFlag.InvalidIban, OcrFlag.InvalidVatFormat, OcrFlag.VatFormatUnchecked, OcrFlag.VatCountryMismatch, OcrFlag.OwnCompany);

        Assert.Equal(new[] { OcrFlag.OwnCompany }, flags);
    }

    [Fact]
    public void BothGates_CanFireTogether()
    {
        var flags = Recompute("LT12345", null, "LT131000011101001000");

        Assert.Contains(OcrFlag.InvalidIban, flags);
        Assert.Contains(OcrFlag.InvalidVatFormat, flags);
    }

    [Fact]
    public void RecomputeValidationFlags_WithoutDocumentInput_LeavesStoredDocumentFlagsAlone()
    {
        var flags = new List<string> { OcrFlag.InvalidIban, OcrFlag.InvalidVatFormat };

        ExpenseService.RecomputeValidationFlags(flags, 100m, 21m, 121m,
            new List<ExpenseService.ValidationLine> { new(100m, 1m, null) });

        Assert.Contains(OcrFlag.InvalidIban, flags);
        Assert.Contains(OcrFlag.InvalidVatFormat, flags);
    }

    // ---------- path parity (DB) ----------

    public enum WritePath { Create, ReOcr, Edit }

    private ExpenseService CreateService() =>
        new(_fixture.Factory, new NullAuthService(), new DefaultCompanySettingsService());

    private async Task<int> InsertSupplierAsync()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Supplier,
            Name = $"DocFlags Supplier {Guid.NewGuid():N}",
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

    private async Task<int> InsertInvoiceAsync(int? supplierId, IEnumerable<string> flags, string status,
        string? pendingVat, string? pendingCountry, string? pendingIban)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var number = $"DOCFL-{Guid.NewGuid():N}";
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices " +
            "(invoice_number, invoice_date, due_date, amount_excl_vat, vat_rate, vat_amount, amount_incl_vat, status, supplier_id, ocr_flags, " +
            "pending_supplier_vat, pending_supplier_country_code, pending_supplier_bank_account, currency, source, ocr_status, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}, {14}, {15}, NOW(), NOW())",
            number, DateTime.Today, DateTime.Today.AddDays(30), 100m, 21m, 21m, 121m, status, supplierId,
            JsonSerializer.Serialize(flags), pendingVat, pendingCountry, pendingIban, "EUR", "MANUAL", "COMPLETED");
        var id = await context.ExpenseInvoices.Where(i => i.InvoiceNumber == number).Select(i => i.Id).FirstAsync();
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoice_lines (invoice_id, description, quantity, unit_price, amount_excl_vat, vat_rate, amount_incl_vat, sort_order) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7})", id, "Eilutė", 1m, null, 100m, 21m, 121m, 1);
        return id;
    }

    private static OcrResultDto NewOcrResult(string vat, string country, string iban, IEnumerable<string> flags) => new()
    {
        InvoiceNumber = $"DOCFL-OCR-{Guid.NewGuid():N}",
        InvoiceDate = DateTime.Today.ToString("yyyy-MM-dd"),
        DueDate = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd"),
        Currency = "EUR",
        AmountExclVat = 100m,
        VatRate = 21m,
        VatAmount = 21m,
        AmountInclVat = 121m,
        SupplierName = "DocFlags OCR Supplier",
        SupplierVatCode = vat,
        SupplierCountryCode = country,
        SupplierBankAccount = iban,
        Confidence = new OcrConfidenceDto { Amounts = 90, InvoiceNumber = 90, SupplierName = 90, InvoiceDate = 90 },
        Flags = flags.ToList(),
        Lines = { new OcrLineDto { Description = "Eilutė", Quantity = 1m, AmountExclVat = 100m, VatRate = 21m, AmountInclVat = 121m } }
    };

    private async Task<ExpenseInvoice> ReloadAsync(int id)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoices.AsNoTracking().FirstAsync(i => i.Id == id);
    }

    private static List<string> FlagsOf(ExpenseInvoice invoice) =>
        string.IsNullOrEmpty(invoice.OcrFlags) ? new() : JsonSerializer.Deserialize<List<string>>(invoice.OcrFlags) ?? new();

    private async Task CleanupAsync(int? invoiceId, int? supplierId)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        if (invoiceId.HasValue)
        {
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_audit WHERE invoice_id = {0}", invoiceId.Value);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_lines WHERE invoice_id = {0}", invoiceId.Value);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE id = {0}", invoiceId.Value);
        }
        if (supplierId.HasValue)
            await context.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", supplierId.Value);
    }

    /// <summary>
    /// Writes an invoice WITHOUT a supplier through the path. The document codes are the OCR result's
    /// (create, re-OCR) or the pending_* columns (edit); incoming flags are the OCR flags / stored flags.
    /// </summary>
    private async Task<(List<string> Flags, string Status)> RunNoSupplierAsync(WritePath path, string vat, string country, string iban,
        params string[] incomingFlags)
    {
        int? id = null;
        try
        {
            var service = CreateService();
            switch (path)
            {
                case WritePath.Create:
                    id = (await service.CreateFromOcrAsync(NewOcrResult(vat, country, iban, incomingFlags))).Id;
                    break;
                case WritePath.ReOcr:
                    id = await InsertInvoiceAsync(null, Array.Empty<string>(), "PENDING_SUPPLIER", null, null, null);
                    await service.UpdateFromOcrAsync(id.Value, NewOcrResult(vat, country, iban, incomingFlags));
                    break;
                case WritePath.Edit:
                    id = await InsertInvoiceAsync(null, incomingFlags, "PENDING_SUPPLIER", vat, country, iban);
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
            await CleanupAsync(id, null);
        }
    }

    [Theory]
    [InlineData(WritePath.Create)]
    [InlineData(WritePath.ReOcr)]
    [InlineData(WritePath.Edit)]
    public async Task NoSupplier_BadIbanAndVat_FlaggedOnEveryPath_HeldAsPendingSupplier(WritePath path)
    {
        var (flags, status) = await RunNoSupplierAsync(path, "LT12345", "LT", "LT131000011101001000");

        Assert.Contains(OcrFlag.InvalidIban, flags);
        Assert.Contains(OcrFlag.InvalidVatFormat, flags);
        Assert.Equal("PENDING_SUPPLIER", status);
    }

    [Theory]
    [InlineData(WritePath.Create)]
    [InlineData(WritePath.ReOcr)]
    [InlineData(WritePath.Edit)]
    public async Task NoSupplier_InformationFlags_OnEveryPath(WritePath path)
    {
        var (flagsUnknown, _) = await RunNoSupplierAsync(path, "GB123456789", "GB", ValidLtIban);
        var (flagsMismatch, _) = await RunNoSupplierAsync(path, "LV12345678901", "LT", ValidLtIban);

        Assert.Contains(OcrFlag.VatFormatUnchecked, flagsUnknown);
        Assert.Contains(OcrFlag.VatCountryMismatch, flagsMismatch);
        Assert.DoesNotContain(OcrFlag.InvalidIban, flagsUnknown.Concat(flagsMismatch));
    }

    [Theory]
    [InlineData(WritePath.Create)]
    [InlineData(WritePath.ReOcr)]
    [InlineData(WritePath.Edit)]
    public async Task NoSupplier_CorrectedCodes_StaleFlagsDropped(WritePath path)
    {
        // the codes are fine now (the user corrected the VAT code before saving / the pending values are valid),
        // so flags left over from an earlier read are not carried
        var (flags, _) = await RunNoSupplierAsync(path, "LT123456789", "LT", ValidLtIban,
            OcrFlag.InvalidIban, OcrFlag.InvalidVatFormat, OcrFlag.VatFormatUnchecked, OcrFlag.VatCountryMismatch);

        Assert.DoesNotContain(OcrFlag.InvalidIban, flags);
        Assert.DoesNotContain(OcrFlag.InvalidVatFormat, flags);
        Assert.DoesNotContain(OcrFlag.VatFormatUnchecked, flags);
        Assert.DoesNotContain(OcrFlag.VatCountryMismatch, flags);
    }

    [Theory]
    [InlineData(WritePath.Create)]
    [InlineData(WritePath.ReOcr)]
    [InlineData(WritePath.Edit)]
    public async Task NoSupplier_EmptyCodes_NoFlags(WritePath path)
    {
        var (flags, _) = await RunNoSupplierAsync(path, "", "", "");

        Assert.DoesNotContain(OcrFlag.InvalidIban, flags);
        Assert.DoesNotContain(OcrFlag.InvalidVatFormat, flags);
        Assert.DoesNotContain(OcrFlag.VatFormatUnchecked, flags);
        Assert.DoesNotContain(OcrFlag.VatCountryMismatch, flags);
    }

    [Fact]
    public async Task Edit_WithSupplier_StoredDocumentFlagsCarriedOver_PendingValuesIgnored()
    {
        // With a supplier the pending_* columns are not the document's codes (they are cleared / stale),
        // so nothing is recomputed: the stored flags stay, and a stored flag is not invented from empty columns.
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync(supplierId,
            new[] { OcrFlag.InvalidIban, OcrFlag.VatCountryMismatch }, "PENDING", pendingVat: null, pendingCountry: null, pendingIban: null);
        try
        {
            var stored = await ReloadAsync(id);
            stored.Notes = "pastaba";
            await using var context = await _fixture.Factory.CreateDbContextAsync();
            var lines = await context.ExpenseInvoiceLines.AsNoTracking().Where(l => l.InvoiceId == id).ToListAsync();
            await CreateService().SaveInvoiceEditAsync(stored, lines, "Test User");

            var after = await ReloadAsync(id);
            Assert.Contains(OcrFlag.InvalidIban, FlagsOf(after));
            Assert.Contains(OcrFlag.VatCountryMismatch, FlagsOf(after));
            Assert.DoesNotContain(OcrFlag.InvalidVatFormat, FlagsOf(after));
            Assert.Equal("PENDING", after.Status); // D-039 item 2
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Fact]
    public async Task Edit_NoSupplier_PendingValuesDriveTheFlags_UpdateInvoiceAsyncToo()
    {
        var id = await InsertInvoiceAsync(null, new[] { OcrFlag.VendorNotFound }, "PENDING_SUPPLIER", "LT12345", "LT", "LT131000011101001000");
        try
        {
            var stored = await ReloadAsync(id);
            stored.Notes = "pastaba";
            await CreateService().UpdateInvoiceAsync(stored);

            var flags = FlagsOf(await ReloadAsync(id));
            Assert.Contains(OcrFlag.InvalidIban, flags);
            Assert.Contains(OcrFlag.InvalidVatFormat, flags);
            Assert.Contains(OcrFlag.VendorNotFound, flags);
        }
        finally
        {
            await CleanupAsync(id, null);
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
