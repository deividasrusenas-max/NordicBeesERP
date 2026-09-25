using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
using NordicBeesERP.Services.Dtos;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Etapas 0c C7 (D-036): budget actuals — per line allocation → line category → invoice category,
/// net amounts (allocations entered against gross are converted proportionally), quarantined
/// invoices excluded, uncategorised under „Nepriskirta" (CategoryId null), line-less invoices by
/// header net. Year 2093 isolates the data from other tests. Integration tests against
/// nordic_bees_erp_test.
/// </summary>
public class ExpenseBudgetActualsTests : IClassFixture<DbTestFixture>, IAsyncLifetime
{
    private const int Year = 2093;
    private readonly DbTestFixture _fixture;
    private readonly List<int> _invoiceIds = new();
    private readonly List<int> _categoryIds = new();
    private int _costCenterId;
    private int _catA, _catB, _catC;

    public ExpenseBudgetActualsTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    private ExpenseService CreateService() =>
        new(_fixture.Factory, new NullAuthService(), new DefaultCompanySettingsService());

    public async Task InitializeAsync()
    {
        await using var db = await _fixture.Factory.CreateDbContextAsync();
        var code = "BA" + Guid.NewGuid().ToString("N")[..10];
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_cost_centers (name, code, is_active) VALUES ({0}, {1}, 1)", "Budget CC", code);
        _costCenterId = await db.ExpenseCostCenters.Where(c => c.Code == code).Select(c => c.Id).FirstAsync();
        _catA = await InsertCategoryAsync(db, "A");
        _catB = await InsertCategoryAsync(db, "B");
        _catC = await InsertCategoryAsync(db, "C");
    }

    public async Task DisposeAsync()
    {
        await using var db = await _fixture.Factory.CreateDbContextAsync();
        foreach (var id in _invoiceIds)
        {
            await db.Database.ExecuteSqlRawAsync(
                "DELETE a FROM expense_line_allocations a JOIN expense_invoice_lines l ON l.id = a.invoice_line_id WHERE l.invoice_id = {0}", id);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_lines WHERE invoice_id = {0}", id);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE id = {0}", id);
        }
        foreach (var id in _categoryIds)
            await db.Database.ExecuteSqlRawAsync("DELETE FROM expense_categories WHERE id = {0}", id);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM expense_cost_centers WHERE id = {0}", _costCenterId);
    }

    private async Task<int> InsertCategoryAsync(NordicBeesERP.Data.NordicBeesERPContext db, string suffix)
    {
        var code = "BA" + suffix + Guid.NewGuid().ToString("N")[..9];
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_categories (name, code, is_active, sort_order) VALUES ({0}, {1}, 1, 0)", "Budget " + suffix, code);
        var id = await db.ExpenseCategories.Where(c => c.Code == code).Select(c => c.Id).FirstAsync();
        _categoryIds.Add(id);
        return id;
    }

    private async Task<int> InsertInvoiceAsync(int? categoryId, int month, decimal headerNet = 100m, string status = "PENDING")
    {
        await using var db = await _fixture.Factory.CreateDbContextAsync();
        var number = $"BUDGET-{Guid.NewGuid():N}";
        var date = new DateTime(Year, month, 15);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices (invoice_number, invoice_date, due_date, amount_excl_vat, vat_rate, vat_amount, amount_incl_vat, status, category_id, currency, source, ocr_status, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, NOW(), NOW())",
            number, date, date.AddDays(30), headerNet, 21m, headerNet * 21m / 100m, headerNet * 121m / 100m, status, categoryId, "EUR", "MANUAL", "COMPLETED");
        var id = await db.ExpenseInvoices.Where(i => i.InvoiceNumber == number).Select(i => i.Id).FirstAsync();
        _invoiceIds.Add(id);
        return id;
    }

    private async Task<int> InsertLineAsync(int invoiceId, decimal net, decimal gross, int? categoryId = null)
    {
        await using var db = await _fixture.Factory.CreateDbContextAsync();
        var marker = $"BUDGET-LINE-{Guid.NewGuid():N}";
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoice_lines (invoice_id, category_id, description, quantity, amount_excl_vat, vat_rate, amount_incl_vat, sort_order) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7})",
            invoiceId, categoryId, marker, 1m, net, 21m, gross, 1);
        return await db.ExpenseInvoiceLines.Where(l => l.Description == marker).Select(l => l.Id).FirstAsync();
    }

    private async Task AllocateAsync(int lineId, int categoryId, decimal amount)
    {
        await using var db = await _fixture.Factory.CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_line_allocations (invoice_line_id, category_id, cost_center_id, allocated_amount, allocated_percent) VALUES ({0}, {1}, {2}, {3}, {4})",
            lineId, categoryId, _costCenterId, amount, 0m);
    }

    private static decimal Actual(BudgetActualsResult r, int? categoryId, int month) =>
        r.Rows.Where(x => x.CategoryId == categoryId && x.Month == month).Sum(x => x.NetAmount);

    [Fact]
    public async Task FullAllocation_SplitsNetByAllocationShareOfGross()
    {
        var inv = await InsertInvoiceAsync(_catC, month: 1);
        var line = await InsertLineAsync(inv, 100m, 121m, _catC);
        await AllocateAsync(line, _catA, 60.50m);
        await AllocateAsync(line, _catB, 60.50m);

        var r = await CreateService().GetBudgetActualsAsync(Year);

        Assert.Equal(50m, Actual(r, _catA, 1));
        Assert.Equal(50m, Actual(r, _catB, 1));
        Assert.Equal(0m, Actual(r, _catC, 1)); // fully allocated: nothing falls through
    }

    [Fact]
    public async Task PartialAllocation_RemainderFallsThroughToLineCategory()
    {
        var inv = await InsertInvoiceAsync(_catB, month: 2);
        var line = await InsertLineAsync(inv, 100m, 121m, _catC);
        await AllocateAsync(line, _catA, 60.50m);

        var r = await CreateService().GetBudgetActualsAsync(Year);

        Assert.Equal(50m, Actual(r, _catA, 2));
        Assert.Equal(50m, Actual(r, _catC, 2)); // remainder 60.50 gross → 50 net, line category
        Assert.Equal(0m, Actual(r, _catB, 2));
        Assert.Empty(r.OverAllocatedLines);
    }

    [Fact]
    public async Task OverAllocation_ScaledToLineNet_AndReported()
    {
        var inv = await InsertInvoiceAsync(_catC, month: 3);
        var line = await InsertLineAsync(inv, 100m, 121m);
        await AllocateAsync(line, _catA, 150m);
        await AllocateAsync(line, _catB, 50m); // Σ 200 > gross 121

        var r = await CreateService().GetBudgetActualsAsync(Year);

        Assert.Equal(75m, Actual(r, _catA, 3));
        Assert.Equal(25m, Actual(r, _catB, 3));
        Assert.Equal(0m, Actual(r, _catC, 3));
        Assert.Contains(r.OverAllocatedLines, a => a.LineId == line && a.InvoiceId == inv);
    }

    [Fact]
    public async Task ZeroGrossAllocatedLine_ContributesZero_AndReported()
    {
        var inv = await InsertInvoiceAsync(_catC, month: 4);
        var line = await InsertLineAsync(inv, 0m, 0m, _catC);
        await AllocateAsync(line, _catA, 10m);

        var r = await CreateService().GetBudgetActualsAsync(Year);

        Assert.Equal(0m, Actual(r, _catA, 4));
        Assert.Equal(0m, Actual(r, _catC, 4));
        Assert.Contains(r.ZeroGrossAllocatedLines, a => a.LineId == line && a.InvoiceId == inv);
    }

    [Fact]
    public async Task UnallocatedLine_LineCategory_ThenInvoiceCategory_ThenNepriskirta()
    {
        var withLineCat = await InsertInvoiceAsync(_catB, month: 5);
        await InsertLineAsync(withLineCat, 10m, 12.10m, _catA);
        var withInvoiceCat = await InsertInvoiceAsync(_catB, month: 5);
        await InsertLineAsync(withInvoiceCat, 20m, 24.20m);
        var uncategorised = await InsertInvoiceAsync(null, month: 5);
        await InsertLineAsync(uncategorised, 30m, 36.30m);

        var r = await CreateService().GetBudgetActualsAsync(Year);

        Assert.Equal(10m, Actual(r, _catA, 5));
        Assert.Equal(20m, Actual(r, _catB, 5));
        Assert.Equal(30m, Actual(r, null, 5));
    }

    [Fact]
    public async Task LinelessInvoice_HeaderNetUnderInvoiceCategory_OrNepriskirta()
    {
        await InsertInvoiceAsync(_catB, month: 6, headerNet: 80m);
        await InsertInvoiceAsync(null, month: 6, headerNet: 40m);

        var r = await CreateService().GetBudgetActualsAsync(Year);

        Assert.Equal(80m, Actual(r, _catB, 6));
        Assert.Equal(40m, Actual(r, null, 6));
    }

    [Fact]
    public async Task QuarantinedInvoices_Excluded()
    {
        var dup = await InsertInvoiceAsync(_catA, month: 7, status: "DUPLICATE_PENDING");
        await InsertLineAsync(dup, 100m, 121m, _catA);
        await InsertInvoiceAsync(_catA, month: 7, headerNet: 55m, status: "REJECTED");
        var ok = await InsertInvoiceAsync(_catA, month: 7);
        await InsertLineAsync(ok, 12m, 14.52m, _catA);

        var r = await CreateService().GetBudgetActualsAsync(Year);

        Assert.Equal(12m, Actual(r, _catA, 7));
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
