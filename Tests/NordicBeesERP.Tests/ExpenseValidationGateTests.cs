using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// OCR Etapas 1 S4 — EN 16931 gate path parity (PLAN-ETAPAS1 §0, §1; D-040). Every flag owned by
/// <see cref="ExpenseService.RecomputeValidationFlags"/> must come out the same on all three write paths —
/// create (<c>CreateFromOcrAsync</c>), re-OCR (<c>UpdateFromOcrAsync</c>) and manual edit
/// (<c>SaveInvoiceEditAsync</c>) — for the same header, lines and incoming flags, with the same status.
/// Approval retention (PLAN §8): a gate does not reopen an approved invoice whose gate fields were not
/// edited. Integration tests against nordic_bees_erp_test.
/// </summary>
public class ExpenseValidationGateTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ExpenseValidationGateTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    public enum WritePath { Create, ReOcr, Edit }

    /// <summary>One invoice line of a case.</summary>
    public sealed record GateLine(decimal Net, decimal? Quantity = 1m, decimal? UnitPrice = null);

    private ExpenseService CreateService() =>
        new(_fixture.Factory, new NullAuthService(), new DefaultCompanySettingsService());

    private async Task<int> InsertSupplierAsync()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Supplier,
            Name = $"ValidationGate Supplier {Guid.NewGuid():N}",
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

    private async Task<int> InsertInvoiceAsync(int supplierId, decimal excl, decimal vat, decimal incl,
        IEnumerable<string> flags, string status = "NEEDS_REVIEW", string? approvedBy = null)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var number = $"VGATE-{Guid.NewGuid():N}";
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices " +
            "(invoice_number, invoice_date, due_date, amount_excl_vat, vat_rate, vat_amount, amount_incl_vat, status, supplier_id, ocr_flags, approved_by, approved_at, currency, source, ocr_status, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}, {14}, NOW(), NOW())",
            number, DateTime.Today, DateTime.Today.AddDays(30), excl, 21m, vat, incl, status, supplierId,
            JsonSerializer.Serialize(flags), approvedBy, approvedBy != null ? DateTime.Now : null, "EUR", "MANUAL", "COMPLETED");
        return await context.ExpenseInvoices.Where(i => i.InvoiceNumber == number).Select(i => i.Id).FirstAsync();
    }

    private async Task InsertLinesAsync(int invoiceId, IEnumerable<GateLine> lines)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var order = 1;
        foreach (var line in lines)
        {
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO expense_invoice_lines (invoice_id, description, quantity, unit_price, amount_excl_vat, vat_rate, amount_incl_vat, sort_order) " +
                "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7})",
                invoiceId, $"Eilutė {order}", line.Quantity, line.UnitPrice, line.Net, 21m, Math.Round(line.Net * 1.21m, 2), order);
            order++;
        }
    }

    private static OcrResultDto NewOcrResult(int supplierId, decimal excl, decimal vat, decimal incl,
        IEnumerable<GateLine> lines, IEnumerable<string> flags) => new()
    {
        InvoiceNumber = $"VGATE-OCR-{Guid.NewGuid():N}",
        InvoiceDate = DateTime.Today.ToString("yyyy-MM-dd"),
        DueDate = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd"),
        Currency = "EUR",
        AmountExclVat = excl,
        VatRate = 21m,
        VatAmount = vat,
        AmountInclVat = incl,
        SupplierId = supplierId,
        SupplierName = "ValidationGate OCR Supplier",
        Confidence = new OcrConfidenceDto { Amounts = 90, InvoiceNumber = 90, SupplierName = 90, InvoiceDate = 90 },
        Flags = flags.ToList(),
        Lines = lines.Select(l => new OcrLineDto
        {
            Description = "Eilutė",
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            AmountExclVat = l.Net,
            VatRate = 21m,
            AmountInclVat = Math.Round(l.Net * 1.21m, 2)
        }).ToList()
    };

    private async Task<ExpenseInvoice> ReloadAsync(int id)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoices.AsNoTracking().FirstAsync(i => i.Id == id);
    }

    private async Task<List<ExpenseInvoiceLine>> LinesAsync(int invoiceId)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoiceLines.AsNoTracking().Where(l => l.InvoiceId == invoiceId).OrderBy(l => l.SortOrder).ToListAsync();
    }

    private static List<string> FlagsOf(ExpenseInvoice invoice) =>
        string.IsNullOrEmpty(invoice.OcrFlags) ? new() : JsonSerializer.Deserialize<List<string>>(invoice.OcrFlags) ?? new();

    private async Task CleanupAsync(int? invoiceId, int supplierId)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        if (invoiceId.HasValue)
        {
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_audit WHERE invoice_id = {0}", invoiceId.Value);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_lines WHERE invoice_id = {0}", invoiceId.Value);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE id = {0}", invoiceId.Value);
        }
        await context.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", supplierId);
    }

    /// <summary>
    /// Writes one invoice through <paramref name="path"/> with the given final header, lines and incoming
    /// flags (the OCR result's flags, or the stored flags for an edit), and returns the stored flags and status.
    /// </summary>
    private async Task<(List<string> Flags, string Status)> RunAsync(WritePath path, decimal excl, decimal vat, decimal incl,
        GateLine[] lines, params string[] incomingFlags)
    {
        var supplierId = await InsertSupplierAsync();
        int? id = null;
        try
        {
            var service = CreateService();
            switch (path)
            {
                case WritePath.Create:
                    id = (await service.CreateFromOcrAsync(NewOcrResult(supplierId, excl, vat, incl, lines, incomingFlags))).Id;
                    break;
                case WritePath.ReOcr:
                    // the stored row is unrelated to the case; re-OCR replaces its header, lines and flags
                    id = await InsertInvoiceAsync(supplierId, 10m, 2.1m, 12.1m, Array.Empty<string>());
                    await InsertLinesAsync(id.Value, new[] { new GateLine(10m) });
                    await service.UpdateFromOcrAsync(id.Value, NewOcrResult(supplierId, excl, vat, incl, lines, incomingFlags));
                    break;
                case WritePath.Edit:
                    id = await InsertInvoiceAsync(supplierId, excl, vat, incl, incomingFlags);
                    await InsertLinesAsync(id.Value, lines);
                    await service.SaveInvoiceEditAsync(await ReloadAsync(id.Value), await LinesAsync(id.Value), "Test User");
                    break;
            }
            var after = await ReloadAsync(id!.Value);
            return (FlagsOf(after), after.Status);
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    // ---------- (a) BR-CO-15: AMOUNT_ARITHMETIC_MISMATCH, exact per Schematron (D-040) ----------

    [Theory]
    [InlineData(WritePath.Create)]
    [InlineData(WritePath.ReOcr)]
    [InlineData(WritePath.Edit)]
    public async Task BrCo15_OneCentOff_ArithmeticMismatch_NeedsReview(WritePath path)
    {
        var (flags, status) = await RunAsync(path, 100m, 21m, 121.01m, new[] { new GateLine(100m) });

        Assert.Contains(OcrFlag.AmountArithmeticMismatch, flags);
        Assert.Equal("NEEDS_REVIEW", status);
    }

    [Theory]
    [InlineData(WritePath.Create)]
    [InlineData(WritePath.ReOcr)]
    [InlineData(WritePath.Edit)]
    public async Task BrCo15_RealInvoice_ASF0021438_Header_Passes_Pending(WritePath path)
    {
        // 934,22 + 196,18 = 1 130,40
        var (flags, status) = await RunAsync(path, 934.22m, 196.18m, 1130.40m, new[] { new GateLine(934.22m) });

        Assert.DoesNotContain(OcrFlag.AmountArithmeticMismatch, flags);
        Assert.DoesNotContain(OcrFlag.MissingMoneyField, flags);
        Assert.Equal("PENDING", status);
    }

    // ---------- (a) MISSING_MONEY_FIELD (unchanged meaning: net or gross ≤ 0) ----------

    [Theory]
    [InlineData(WritePath.Create)]
    [InlineData(WritePath.ReOcr)]
    [InlineData(WritePath.Edit)]
    public async Task RealInvoice_213_ZeroNetHugeGross_MissingMoneyField_NeedsReview(WritePath path)
    {
        // 0,00 / 465 374,45: net → null, BR-CO-15 not applicable, MISSING_MONEY_FIELD stops the invoice
        var (flags, status) = await RunAsync(path, 0m, 0m, 465374.45m, new[] { new GateLine(0m) });

        Assert.Contains(OcrFlag.MissingMoneyField, flags);
        Assert.DoesNotContain(OcrFlag.AmountArithmeticMismatch, flags);
        Assert.Equal("NEEDS_REVIEW", status);
    }

    // ---------- (a) owned flags are recomputed on every path (a stale copy never sticks) ----------

    [Theory]
    [InlineData(WritePath.Create)]
    [InlineData(WritePath.ReOcr)]
    [InlineData(WritePath.Edit)]
    public async Task StaleOwnedFlags_Dropped_WhenValuesAreConsistent_Pending(WritePath path)
    {
        // TOTALS_OUT_OF_RANGE cannot be produced through a stored row (the decimal(12,2) columns cap
        // amounts far below the overflow range), so its path parity is proven by ownership: every path
        // drops a stale copy and recomputes it with the same helper.
        var (flags, status) = await RunAsync(path, 100m, 21m, 121m, new[] { new GateLine(100m) },
            OcrFlag.TotalsOutOfRange, OcrFlag.AmountArithmeticMismatch, OcrFlag.MissingMoneyField, OcrFlag.OwnCompany);

        Assert.DoesNotContain(OcrFlag.TotalsOutOfRange, flags);
        Assert.DoesNotContain(OcrFlag.AmountArithmeticMismatch, flags);
        Assert.DoesNotContain(OcrFlag.MissingMoneyField, flags);
        Assert.Contains(OcrFlag.OwnCompany, flags); // not owned by the gate — kept
        Assert.Equal("PENDING", status);
    }

    // ---------- Approval retention (PLAN §8) ----------

    [Fact]
    public async Task BrCo15_ApprovedInvoice_GateFieldsNotEdited_StaysApproved()
    {
        // Approved before D-040 with a one-cent header difference: the new exact gate flags it on the next
        // save, but a notes-only edit changes no gate field, so the approval stands.
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync(supplierId, 100m, 21m, 121.01m, Array.Empty<string>(), "PENDING", "Approver");
        await InsertLinesAsync(id, new[] { new GateLine(100m) });
        try
        {
            var invoice = await ReloadAsync(id);
            invoice.Notes = "Tik pastaba";
            await CreateService().SaveInvoiceEditAsync(invoice, await LinesAsync(id), "Test User");

            var after = await ReloadAsync(id);
            Assert.Equal("PENDING", after.Status);
            Assert.Equal("Approver", after.ApprovedBy);
            Assert.Contains(OcrFlag.AmountArithmeticMismatch, FlagsOf(after));
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Fact]
    public async Task BrCo15_ApprovedInvoice_GateFieldEdited_NeedsReview_ApprovalVoided()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync(supplierId, 100m, 21m, 121m, Array.Empty<string>(), "PENDING", "Approver");
        await InsertLinesAsync(id, new[] { new GateLine(100m) });
        try
        {
            var invoice = await ReloadAsync(id);
            invoice.AmountInclVat = 121.01m;
            await CreateService().SaveInvoiceEditAsync(invoice, await LinesAsync(id), "Test User");

            var after = await ReloadAsync(id);
            Assert.Equal("NEEDS_REVIEW", after.Status);
            Assert.Null(after.ApprovedBy);
            Assert.Contains(OcrFlag.AmountArithmeticMismatch, FlagsOf(after));
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
