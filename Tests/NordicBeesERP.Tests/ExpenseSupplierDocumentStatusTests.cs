using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// OCR Etapas 1 S5(c) — D-039 item 2, as ONE rule in <c>ExpenseService.HasReviewFlag(flags, hasSupplier)</c>:
/// whenever an invoice has a supplier (however assigned), the document's INVALID_IBAN / INVALID_VAT_FORMAT do
/// not hold it in NEEDS_REVIEW on ANY path — create, re-OCR, manual edit, assignment (manual and automatic),
/// a later edit after assignment, and the two status decisions of DismissWrongRecipient / ResolveDuplicateAsDifferent.
/// The flags stay stored, and a real review flag still holds the invoice. Integration tests against nordic_bees_erp_test.
/// </summary>
public class ExpenseSupplierDocumentStatusTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ExpenseSupplierDocumentStatusTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    public enum WritePath { Create, ReOcr, Edit }

    private ExpenseService CreateService() =>
        new(_fixture.Factory, new NullAuthService(), new DefaultCompanySettingsService());

    private async Task<int> InsertSupplierAsync()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Supplier,
            Name = $"DocStatus Supplier {Guid.NewGuid():N}",
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

    private async Task<int> InsertInvoiceAsync(string status, int? supplierId, IEnumerable<string> flags,
        decimal excl = 100m, decimal vat = 21m, decimal incl = 121m, string? pendingVat = null)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var number = $"DOCST-{Guid.NewGuid():N}";
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices " +
            "(invoice_number, invoice_date, due_date, amount_excl_vat, vat_rate, vat_amount, amount_incl_vat, status, supplier_id, ocr_flags, pending_supplier_vat, currency, source, ocr_status, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}, NOW(), NOW())",
            number, DateTime.Today, DateTime.Today.AddDays(30), excl, 21m, vat, incl, status, supplierId,
            JsonSerializer.Serialize(flags), pendingVat, "EUR", "MANUAL", "COMPLETED");
        var id = await context.ExpenseInvoices.Where(i => i.InvoiceNumber == number).Select(i => i.Id).FirstAsync();
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoice_lines (invoice_id, description, quantity, unit_price, amount_excl_vat, vat_rate, amount_incl_vat, sort_order) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7})", id, "Eilutė", 1m, null, excl, 21m, incl, 1);
        return id;
    }

    private static OcrResultDto NewOcrResult(int? supplierId, decimal incl, IEnumerable<string> flags) => new()
    {
        InvoiceNumber = $"DOCST-OCR-{Guid.NewGuid():N}",
        InvoiceDate = DateTime.Today.ToString("yyyy-MM-dd"),
        DueDate = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd"),
        Currency = "EUR",
        AmountExclVat = 100m,
        VatRate = 21m,
        VatAmount = 21m,
        AmountInclVat = incl,
        SupplierId = supplierId,
        SupplierName = "DocStatus OCR Supplier",
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

    /// <summary>Writes an invoice WITH a supplier through the path; the incoming flags are the OCR flags / stored flags.</summary>
    private async Task<(List<string> Flags, string Status)> RunWithSupplierAsync(WritePath path, decimal incl, params string[] incomingFlags)
    {
        var supplierId = await InsertSupplierAsync();
        int? id = null;
        try
        {
            var service = CreateService();
            switch (path)
            {
                case WritePath.Create:
                    id = (await service.CreateFromOcrAsync(NewOcrResult(supplierId, incl, incomingFlags))).Id;
                    break;
                case WritePath.ReOcr:
                    id = await InsertInvoiceAsync("PENDING", supplierId, Array.Empty<string>());
                    await service.UpdateFromOcrAsync(id.Value, NewOcrResult(supplierId, incl, incomingFlags));
                    break;
                case WritePath.Edit:
                    id = await InsertInvoiceAsync("PENDING", supplierId, incomingFlags, 100m, 21m, incl);
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
            await CleanupAsync(id, supplierId);
        }
    }

    [Theory]
    [InlineData(WritePath.Create, OcrFlag.InvalidIban)]
    [InlineData(WritePath.ReOcr, OcrFlag.InvalidIban)]
    [InlineData(WritePath.Edit, OcrFlag.InvalidIban)]
    [InlineData(WritePath.Create, OcrFlag.InvalidVatFormat)]
    [InlineData(WritePath.ReOcr, OcrFlag.InvalidVatFormat)]
    [InlineData(WritePath.Edit, OcrFlag.InvalidVatFormat)]
    public async Task WithSupplier_DocumentFlag_IsInformation_OnEveryPath(WritePath path, string flag)
    {
        var (flags, status) = await RunWithSupplierAsync(path, 121m, flag);

        Assert.Equal("PENDING", status);
        Assert.Contains(flag, flags); // stays stored
    }

    [Theory]
    [InlineData(WritePath.Create)]
    [InlineData(WritePath.ReOcr)]
    [InlineData(WritePath.Edit)]
    public async Task WithSupplier_DocumentFlag_DoesNotMaskARealReviewFlag(WritePath path)
    {
        // gross 121,01 breaks BR-CO-15 → AMOUNT_ARITHMETIC_MISMATCH keeps the invoice in review
        var (flags, status) = await RunWithSupplierAsync(path, 121.01m, OcrFlag.InvalidIban, OcrFlag.InvalidVatFormat);

        Assert.Contains(OcrFlag.AmountArithmeticMismatch, flags);
        Assert.Equal("NEEDS_REVIEW", status);
    }

    [Fact]
    public async Task WithoutSupplier_Create_StaysPendingSupplier()
    {
        var service = CreateService();
        var invoice = await service.CreateFromOcrAsync(NewOcrResult(null, 121m, new[] { OcrFlag.InvalidIban, OcrFlag.VendorNotFound }));
        try
        {
            Assert.Equal("PENDING_SUPPLIER", (await ReloadAsync(invoice.Id)).Status);
        }
        finally
        {
            await CleanupAsync(invoice.Id, null);
        }
    }

    [Fact]
    public async Task Assignment_Manual_DocumentFlagsAreInformation_ThenLaterEditKeepsPending()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("PENDING_SUPPLIER", null, new[] { OcrFlag.VendorNotFound, OcrFlag.InvalidIban, OcrFlag.InvalidVatFormat });
        try
        {
            var service = CreateService();
            await service.AssignSupplierAsync(id, supplierId, "TEST");
            var assigned = await ReloadAsync(id);
            Assert.Equal("PENDING", assigned.Status);
            Assert.Contains(OcrFlag.InvalidIban, FlagsOf(assigned));

            // a later notes-only edit of the same invoice must not put it back into review
            assigned.Notes = "vėlesnė pastaba";
            await using (var context = await _fixture.Factory.CreateDbContextAsync())
            {
                var lines = await context.ExpenseInvoiceLines.AsNoTracking().Where(l => l.InvoiceId == id).ToListAsync();
                await service.SaveInvoiceEditAsync(assigned, lines, "Test User");
            }
            var edited = await ReloadAsync(id);
            Assert.Equal("PENDING", edited.Status);
            Assert.Contains(OcrFlag.InvalidIban, FlagsOf(edited));
            Assert.Contains(OcrFlag.InvalidVatFormat, FlagsOf(edited));
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Fact]
    public async Task Assignment_Auto_DocumentFlagsAreInformation()
    {
        var supplierId = await InsertSupplierAsync();
        var vat = $"LT{Random.Shared.NextInt64(100_000_000, 999_999_999)}";
        var id = await InsertInvoiceAsync("PENDING_SUPPLIER", null, new[] { OcrFlag.VendorNotFound, OcrFlag.InvalidIban }, pendingVat: vat);
        try
        {
            var count = await CreateService().AutoAssignSupplierAsync(vat, null, supplierId);

            Assert.True(count >= 1);
            Assert.Equal("PENDING", (await ReloadAsync(id)).Status);
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Fact]
    public async Task DismissWrongRecipient_WithSupplier_DocumentFlagDoesNotHold()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("NEEDS_REVIEW", supplierId, new[] { OcrFlag.WrongRecipient, OcrFlag.InvalidIban });
        try
        {
            await CreateService().DismissWrongRecipientAsync(id, "TEST");

            Assert.Equal("PENDING", (await ReloadAsync(id)).Status);
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Fact]
    public async Task ResolveDuplicateAsDifferent_WithSupplier_DocumentFlagDoesNotHold()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("DUPLICATE_PENDING", supplierId, new[] { OcrFlag.Duplicate, OcrFlag.InvalidVatFormat });
        try
        {
            await CreateService().ResolveDuplicateAsDifferentAsync(id, "TEST");

            Assert.Equal("PENDING", (await ReloadAsync(id)).Status);
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
