using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services;
using NordicBeesERP.Services.Validation;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// D-042 — the supplier country is an ISO code, never a truncated name. The OCR resolve step
/// (<see cref="ExpenseOcrService.ResolveSupplierAsync"/>) gives the VAT prefix priority over the address, and the
/// result reaches create / re-OCR / manual edit unchanged, so VAT_COUNTRY_MISMATCH and VAT_RATE_UNCHECKED come out the
/// same on every path. Staging evidence: LT codes stored with "LI", IE with "IR". Integration tests against
/// nordic_bees_erp_test. The rate whitelist is injected as CONFIRMED rows so that a wrong country (LI) is visible as
/// VAT_RATE_UNCHECKED, while LT with a legal 21 % is not.
/// </summary>
public class ExpenseCountryCodeParityTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ExpenseCountryCodeParityTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    public enum WritePath { Create, ReOcr, Edit }

    private static readonly IReadOnlyList<VatRateRow> ConfirmedRows =
        VatRateTable.Rows.Select(r => r with { Status = VatRateRowStatus.Confirmed }).ToList();

    private const string ValidLtIban = "LT121000011101001000";

    private sealed class NoViesLookup : IViesService
    {
        public Task<ViesResult> LookupAsync(string vatCode) =>
            Task.FromResult(new ViesResult { ServiceAvailable = true, IsValid = false });
    }

    private static string Digits(int count) =>
        string.Concat(Enumerable.Range(0, count).Select(_ => Random.Shared.Next(1, 10)));

    private ExpenseService CreateService() =>
        new(_fixture.Factory, new NullAuthService(), new DefaultCompanySettingsService(), ConfirmedRows);

    private ExpenseOcrService CreateOcrService() =>
        new(_fixture.Factory, new NoViesLookup(), null!, NullLogger<ExpenseOcrService>.Instance);

    /// <summary>What the OCR pipeline does with an address country and a VAT code: extraction, then the resolve step.</summary>
    private async Task<OcrResultDto> OcrResultAsync(string vat, string addressCountryFromAzure)
    {
        var result = new OcrResultDto
        {
            InvoiceNumber = $"CTRY-OCR-{Guid.NewGuid():N}",
            InvoiceDate = DateTime.Today.ToString("yyyy-MM-dd"),
            DueDate = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd"),
            Currency = "EUR",
            AmountExclVat = 100m,
            VatRate = 21m,
            VatAmount = 21m,
            AmountInclVat = 121m,
            SupplierName = $"Country Test Supplier {Guid.NewGuid():N}",
            SupplierVatCode = vat,
            SupplierCountryCode = CountryCodeResolver.FromAddress(addressCountryFromAzure) ?? "", // ExpenseOcrService address extraction
            SupplierBankAccount = ValidLtIban,
            Confidence = new OcrConfidenceDto { Amounts = 90, InvoiceNumber = 90, SupplierName = 90, InvoiceDate = 90 },
            Lines = { new OcrLineDto { Description = "Eilutė", Quantity = 1m, AmountExclVat = 100m, VatRate = 21m, AmountInclVat = 121m } }
        };
        await CreateOcrService().ResolveSupplierAsync(result, new CompanySettings());
        return result;
    }

    private async Task<int> InsertInvoiceAsync(string? pendingVat, string? pendingCountry)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var number = $"CTRY-{Guid.NewGuid():N}";
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices " +
            "(invoice_number, invoice_date, due_date, amount_excl_vat, vat_rate, vat_amount, amount_incl_vat, status, supplier_id, ocr_flags, " +
            "pending_supplier_vat, pending_supplier_country_code, pending_supplier_bank_account, currency, source, ocr_status, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}, {14}, {15}, NOW(), NOW())",
            number, DateTime.Today, DateTime.Today.AddDays(30), 100m, 21m, 21m, 121m, "PENDING_SUPPLIER", null,
            JsonSerializer.Serialize(Array.Empty<string>()), pendingVat, pendingCountry, ValidLtIban, "EUR", "MANUAL", "COMPLETED");
        var id = await context.ExpenseInvoices.Where(i => i.InvoiceNumber == number).Select(i => i.Id).FirstAsync();
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoice_lines (invoice_id, description, quantity, unit_price, amount_excl_vat, vat_rate, amount_incl_vat, sort_order) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7})", id, "Eilutė", 1m, null, 100m, 21m, 121m, 1);
        return id;
    }

    private async Task<ExpenseInvoice> ReloadAsync(int id)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoices.AsNoTracking().FirstAsync(i => i.Id == id);
    }

    private async Task CleanupAsync(int id)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_audit WHERE invoice_id = {0}", id);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_lines WHERE invoice_id = {0}", id);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE id = {0}", id);
    }

    /// <summary>Writes the OCR result through the path (edit: as the pending_* columns it produced) and returns the stored invoice.</summary>
    private async Task<ExpenseInvoice> RunAsync(WritePath path, OcrResultDto ocr)
    {
        int? id = null;
        try
        {
            var service = CreateService();
            switch (path)
            {
                case WritePath.Create:
                    id = (await service.CreateFromOcrAsync(ocr)).Id;
                    break;
                case WritePath.ReOcr:
                    id = await InsertInvoiceAsync(null, null);
                    await service.UpdateFromOcrAsync(id.Value, ocr);
                    break;
                case WritePath.Edit:
                    id = await InsertInvoiceAsync(ocr.SupplierVatCode, ocr.SupplierCountryCode);
                    var stored = await ReloadAsync(id.Value);
                    stored.Notes = "pastaba";
                    await using (var context = await _fixture.Factory.CreateDbContextAsync())
                    {
                        var lines = await context.ExpenseInvoiceLines.AsNoTracking().Where(l => l.InvoiceId == id.Value).ToListAsync();
                        await service.SaveInvoiceEditAsync(stored, lines, "Test User");
                    }
                    break;
            }
            return await ReloadAsync(id!.Value);
        }
        finally
        {
            if (id.HasValue) await CleanupAsync(id.Value);
        }
    }

    private static List<string> FlagsOf(ExpenseInvoice invoice) =>
        string.IsNullOrEmpty(invoice.OcrFlags) ? new() : JsonSerializer.Deserialize<List<string>>(invoice.OcrFlags) ?? new();

    // ---------- OCR resolve step ----------

    [Theory]
    [InlineData("LT237375410", "Lietuva", "LT")]       // staging: was "LI"
    [InlineData("LT333702811", "Lithuania", "LT")]
    [InlineData("LT100001772414", "Lietuva", "LT")]
    [InlineData("IE8256796U", "Ireland", "IE")]        // staging: was "IR"
    [InlineData("LT237375410", "Ireland", "LT")]       // VAT prefix wins over a conflicting address
    [InlineData("EL123456789", "Lietuva", "GR")]
    [InlineData("", "Lietuva", "LT")]                  // no VAT code: the address name, not its first two letters
    [InlineData("", "Atlantis", "")]                   // unknown name: empty, never a truncation
    [InlineData("", "", "")]
    public async Task ResolveStep_GivesIsoCodeNeverATruncatedName(string vat, string azureCountry, string expected)
    {
        var result = await OcrResultAsync(vat, azureCountry);

        Assert.Equal(expected, result.SupplierCountryCode);
    }

    // ---------- path parity ----------

    [Theory]
    [InlineData(WritePath.Create)]
    [InlineData(WritePath.ReOcr)]
    [InlineData(WritePath.Edit)]
    public async Task LtSupplierWithLietuvaAddress_NoCountryMismatch_NoRateUnchecked(WritePath path)
    {
        var ocr = await OcrResultAsync("LT" + Digits(9), "Lietuva");

        var stored = await RunAsync(path, ocr);
        var flags = FlagsOf(stored);

        Assert.Equal("LT", stored.PendingSupplierCountryCode);
        Assert.DoesNotContain(OcrFlag.VatCountryMismatch, flags);
        Assert.DoesNotContain(OcrFlag.VatRateUnchecked, flags);   // LT is in the (confirmed) table and 21 % is legal there
        Assert.DoesNotContain(OcrFlag.VatRateNotAllowed, flags);
    }

    [Theory]
    [InlineData(WritePath.Create)]
    [InlineData(WritePath.ReOcr)]
    [InlineData(WritePath.Edit)]
    public async Task LtVatCodeWithForeignAddress_VatPrefixWins_NoCountryMismatch(WritePath path)
    {
        var ocr = await OcrResultAsync("LT" + Digits(9), "Ireland");

        var stored = await RunAsync(path, ocr);
        var flags = FlagsOf(stored);

        Assert.Equal("LT", stored.PendingSupplierCountryCode);
        Assert.DoesNotContain(OcrFlag.VatCountryMismatch, flags);
        Assert.DoesNotContain(OcrFlag.VatRateUnchecked, flags);
    }

    [Theory]
    [InlineData(WritePath.Create)]
    [InlineData(WritePath.ReOcr)]
    [InlineData(WritePath.Edit)]
    public async Task IrishSupplier_StoredAsIe_NoMismatch_RateUncheckedBecauseIeIsNotInTheTable(WritePath path)
    {
        var ocr = await OcrResultAsync("IE8256796U", "Ireland");

        var stored = await RunAsync(path, ocr);
        var flags = FlagsOf(stored);

        Assert.Equal("IE", stored.PendingSupplierCountryCode);   // was "IR"
        Assert.DoesNotContain(OcrFlag.VatCountryMismatch, flags);
        Assert.Contains(OcrFlag.VatRateUnchecked, flags);
    }

    // ---------- storage guard ----------

    [Theory]
    [InlineData(WritePath.Create, "Lietuva", "LT")]
    [InlineData(WritePath.ReOcr, "Lietuva", "LT")]
    [InlineData(WritePath.Create, "Ireland", "IE")]
    [InlineData(WritePath.ReOcr, "Ireland", "IE")]
    [InlineData(WritePath.Create, "XX", null)]           // two letters that are not an ISO code are never stored
    [InlineData(WritePath.ReOcr, "XX", null)]
    [InlineData(WritePath.Create, "Atlantis", null)]
    [InlineData(WritePath.ReOcr, "Atlantis", null)]
    public async Task PendingCountry_IsStoredAsIsoCodeOrNull(WritePath path, string rawCountry, string? expected)
    {
        var ocr = await OcrResultAsync("", "");
        ocr.SupplierCountryCode = rawCountry;   // a caller handing the service a raw value

        var stored = await RunAsync(path, ocr);

        Assert.Equal(expected, stored.PendingSupplierCountryCode);
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
