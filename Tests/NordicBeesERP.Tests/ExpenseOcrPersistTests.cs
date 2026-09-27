using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// OCR Etapas 1 S3 (PLAN-ETAPAS1 §4.2, D-010, D-038 Q8): re-OCR keeps the stored filename, both OCR
/// saves are one transaction, and re-OCR over allocations needs confirmation and is audited.
/// Integration tests against nordic_bees_erp_test.
/// </summary>
[Collection("RealDatabase")]
public class ExpenseOcrPersistTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ExpenseOcrPersistTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    private ExpenseService CreateService() =>
        new(_fixture.Factory, new NullAuthService(), new DefaultCompanySettingsService());

    private async Task<int> InsertSupplierAsync()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Supplier,
            Name = $"OcrPersist Supplier {Guid.NewGuid():N}",
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

    /// <summary>NEEDS_REVIEW invoice 100 / 21 / 121 with a stored filename and one 100 € line.</summary>
    private async Task<(int invoiceId, int lineId)> InsertInvoiceWithLineAsync(int supplierId, string filename = "20260901_orig.pdf")
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var number = $"OCRP-{Guid.NewGuid():N}";
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices " +
            "(invoice_number, invoice_date, due_date, amount_excl_vat, vat_rate, vat_amount, amount_incl_vat, status, supplier_id, original_filename, currency, source, ocr_status, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, NOW(), NOW())",
            number, DateTime.Today, DateTime.Today.AddDays(30), 100m, 21m, 21m, 121m, "NEEDS_REVIEW", supplierId,
            filename, "EUR", "MANUAL", "COMPLETED");
        var id = await context.ExpenseInvoices.Where(i => i.InvoiceNumber == number).Select(i => i.Id).FirstAsync();
        var marker = $"OCRP-LINE-{Guid.NewGuid():N}";
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoice_lines (invoice_id, description, quantity, amount_excl_vat, vat_rate, amount_incl_vat, sort_order) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6})",
            id, marker, 1m, 100m, 21m, 121m, 1);
        var lineId = await context.ExpenseInvoiceLines.Where(l => l.Description == marker).Select(l => l.Id).FirstAsync();
        return (id, lineId);
    }

    /// <summary>Category, cost centre and one allocation on the line; returns the ids for cleanup.</summary>
    private async Task<(int categoryId, int costCenterId, int allocationId)> InsertAllocationAsync(int lineId)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var code = "OP" + Guid.NewGuid().ToString("N")[..10];
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_categories (name, code, is_active, sort_order) VALUES ({0}, {1}, 1, 0)", "OcrPersist kat.", code);
        var categoryId = await context.ExpenseCategories.Where(c => c.Code == code).Select(c => c.Id).FirstAsync();
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_cost_centers (name, code, is_active) VALUES ({0}, {1}, 1)", "OcrPersist CC", code);
        var costCenterId = await context.ExpenseCostCenters.Where(c => c.Code == code).Select(c => c.Id).FirstAsync();
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_line_allocations (invoice_line_id, category_id, cost_center_id, allocated_amount, allocated_percent) VALUES ({0}, {1}, {2}, {3}, {4})",
            lineId, categoryId, costCenterId, 100m, 100m);
        var allocationId = await context.ExpenseLineAllocations.Where(a => a.InvoiceLineId == lineId).Select(a => a.Id).FirstAsync();
        return (categoryId, costCenterId, allocationId);
    }

    private static OcrResultDto NewOcrResult(int supplierId, string? number = null, string? originalFilename = null) => new()
    {
        InvoiceNumber = number ?? $"OCRP-OCR-{Guid.NewGuid():N}",
        InvoiceDate = DateTime.Today.ToString("yyyy-MM-dd"),
        DueDate = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd"),
        Currency = "EUR",
        AmountExclVat = 200m,
        VatRate = 21m,
        VatAmount = 42m,
        AmountInclVat = 242m,
        SupplierId = supplierId,
        SupplierName = "OcrPersist OCR Supplier",
        OriginalFilename = originalFilename,
        Confidence = new OcrConfidenceDto { Amounts = 90, InvoiceNumber = 90, SupplierName = 90, InvoiceDate = 90 },
        Lines = new List<OcrLineDto>
        {
            new() { Description = "OCR eilutė", Quantity = 2m, UnitPrice = 100m, AmountExclVat = 200m, VatRate = 21m, AmountInclVat = 242m }
        }
    };

    private async Task<ExpenseInvoice> ReloadAsync(int id)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoices.AsNoTracking().FirstAsync(i => i.Id == id);
    }

    private async Task<List<ExpenseInvoiceLine>> LinesAsync(int invoiceId)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoiceLines.AsNoTracking().Where(l => l.InvoiceId == invoiceId).OrderBy(l => l.Id).ToListAsync();
    }

    private async Task<List<ExpenseInvoiceAudit>> AuditsAsync(int invoiceId)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoiceAudits.AsNoTracking().Where(a => a.InvoiceId == invoiceId).ToListAsync();
    }

    private async Task CleanupAsync(int invoiceId, int supplierId, (int categoryId, int costCenterId, int allocationId)? allocation = null)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        if (allocation is { } a)
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_line_allocations WHERE category_id = {0}", a.categoryId);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_audit WHERE invoice_id = {0}", invoiceId);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_lines WHERE invoice_id = {0}", invoiceId);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE id = {0}", invoiceId);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", supplierId);
        if (allocation is { } b)
        {
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_cost_centers WHERE id = {0}", b.costCenterId);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_categories WHERE id = {0}", b.categoryId);
        }
    }

    // --- (b) stored filename ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ReOcr_OcrResultWithoutFilename_KeepsStoredFilename(string? ocrFilename)
    {
        var supplierId = await InsertSupplierAsync();
        var (id, _) = await InsertInvoiceWithLineAsync(supplierId);
        try
        {
            await CreateService().UpdateFromOcrAsync(id, NewOcrResult(supplierId, originalFilename: ocrFilename));

            Assert.Equal("20260901_orig.pdf", (await ReloadAsync(id)).OriginalFilename);
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Fact]
    public async Task ReOcr_OcrResultWithFilename_Replaces()
    {
        var supplierId = await InsertSupplierAsync();
        var (id, _) = await InsertInvoiceWithLineAsync(supplierId);
        try
        {
            await CreateService().UpdateFromOcrAsync(id, NewOcrResult(supplierId, originalFilename: "naujas.pdf"));

            Assert.Equal("naujas.pdf", (await ReloadAsync(id)).OriginalFilename);
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    // --- (c) one transaction ---

    [Fact]
    public async Task ReOcr_FailureMidSave_RollsBack_InvoiceLinesAndAuditUnchanged()
    {
        var supplierId = await InsertSupplierAsync();
        var (id, lineId) = await InsertInvoiceWithLineAsync(supplierId);
        try
        {
            var before = await ReloadAsync(id);
            var ocr = NewOcrResult(supplierId);
            // description is NOT NULL: the line INSERT fails after the invoice UPDATE and the line DELETE
            ocr.Lines.Add(new OcrLineDto { Description = null!, AmountExclVat = 1m, VatRate = 21m, AmountInclVat = 1.21m });

            await Assert.ThrowsAnyAsync<Exception>(() => CreateService().UpdateFromOcrAsync(id, ocr));

            var after = await ReloadAsync(id);
            Assert.Equal(before.InvoiceNumber, after.InvoiceNumber);
            Assert.Equal(before.AmountInclVat, after.AmountInclVat);
            Assert.Equal(before.Status, after.Status);
            Assert.Equal(new[] { lineId }, (await LinesAsync(id)).Select(l => l.Id).ToArray());
            Assert.Empty(await AuditsAsync(id));
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Fact]
    public async Task Create_FailureMidSave_RollsBack_NoInvoiceNoAudit()
    {
        var supplierId = await InsertSupplierAsync();
        var number = $"OCRP-CREATE-{Guid.NewGuid():N}";
        try
        {
            var ocr = NewOcrResult(supplierId, number);
            // the invoice INSERT succeeds, then the line INSERT fails
            ocr.Lines.Add(new OcrLineDto { Description = null!, AmountExclVat = 1m, VatRate = 21m, AmountInclVat = 1.21m });

            await Assert.ThrowsAnyAsync<Exception>(() => CreateService().CreateFromOcrAsync(ocr));

            await using var context = await _fixture.Factory.CreateDbContextAsync();
            Assert.False(await context.ExpenseInvoices.AnyAsync(i => i.InvoiceNumber == number));
            Assert.False(await context.ExpenseInvoiceAudits.AnyAsync(a => a.InvoiceNumber == number));
        }
        finally
        {
            await using var context = await _fixture.Factory.CreateDbContextAsync();
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_audit WHERE invoice_number = {0}", number);
            await context.Database.ExecuteSqlRawAsync(
                "DELETE l FROM expense_invoice_lines l JOIN expense_invoices i ON i.id = l.invoice_id WHERE i.invoice_number = {0}", number);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE invoice_number = {0}", number);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", supplierId);
        }
    }

    [Fact]
    public async Task Create_Success_InvoiceLinesAndAuditCommitted()
    {
        var supplierId = await InsertSupplierAsync();
        var number = $"OCRP-CREATE-{Guid.NewGuid():N}";
        int id = 0;
        try
        {
            id = (await CreateService().CreateFromOcrAsync(NewOcrResult(supplierId, number))).Id;

            Assert.Equal(number, (await ReloadAsync(id)).InvoiceNumber);
            Assert.Single(await LinesAsync(id));
            Assert.Contains(await AuditsAsync(id), a => a.Action == "CREATED");
        }
        finally
        {
            if (id > 0) await CleanupAsync(id, supplierId);
        }
    }

    // --- (d) allocations (D-038 Q8) ---

    [Fact]
    public async Task ReOcr_WithAllocations_NotConfirmed_Refused_NothingChanged()
    {
        var supplierId = await InsertSupplierAsync();
        var (id, lineId) = await InsertInvoiceWithLineAsync(supplierId);
        var allocation = await InsertAllocationAsync(lineId);
        try
        {
            var before = await ReloadAsync(id);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateService().UpdateFromOcrAsync(id, NewOcrResult(supplierId)));
            Assert.Contains("paskirstym", ex.Message);

            Assert.Equal(before.AmountInclVat, (await ReloadAsync(id)).AmountInclVat);
            Assert.Equal(new[] { lineId }, (await LinesAsync(id)).Select(l => l.Id).ToArray());
            await using var context = await _fixture.Factory.CreateDbContextAsync();
            Assert.True(await context.ExpenseLineAllocations.AnyAsync(a => a.Id == allocation.allocationId));
            Assert.Empty(await AuditsAsync(id));
        }
        finally
        {
            await CleanupAsync(id, supplierId, allocation);
        }
    }

    [Fact]
    public async Task ReOcr_WithAllocations_Confirmed_LinesReplaced_AllocationsAudited()
    {
        var supplierId = await InsertSupplierAsync();
        var (id, lineId) = await InsertInvoiceWithLineAsync(supplierId);
        var allocation = await InsertAllocationAsync(lineId);
        try
        {
            await CreateService().UpdateFromOcrAsync(id, NewOcrResult(supplierId), allocationRemovalConfirmed: true);

            Assert.Equal(242m, (await ReloadAsync(id)).AmountInclVat);
            var lines = await LinesAsync(id);
            Assert.DoesNotContain(lines, l => l.Id == lineId);
            await using var context = await _fixture.Factory.CreateDbContextAsync();
            Assert.False(await context.ExpenseLineAllocations.AnyAsync(a => a.Id == allocation.allocationId));

            var audit = Assert.Single(await AuditsAsync(id), a => a.Action == "OCR_RETRIED");
            Assert.Contains("pašalinta paskirstymų: 1", audit.ActionDetails);
            Assert.Contains(lineId.ToString(), audit.ActionDetails);
        }
        finally
        {
            await CleanupAsync(id, supplierId, allocation);
        }
    }

    [Fact]
    public async Task ReOcr_WithoutAllocations_NoConfirmationNeeded_NotInAudit()
    {
        var supplierId = await InsertSupplierAsync();
        var (id, _) = await InsertInvoiceWithLineAsync(supplierId);
        try
        {
            await CreateService().UpdateFromOcrAsync(id, NewOcrResult(supplierId));

            var audit = Assert.Single(await AuditsAsync(id), a => a.Action == "OCR_RETRIED");
            Assert.DoesNotContain("paskirstym", audit.ActionDetails);
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

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
