using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Helpers;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// D-027: DUPLICATE_PENDING and REJECTED expense invoices are quarantined — they are not
/// payables, so cash flow and totals must leave them out. Integration tests against
/// nordic_bees_erp_test.
/// </summary>
public class ExpenseQuarantineTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    // Far-future window so rows from other tests / fixtures never fall inside it.
    private static readonly DateTime WindowStart = new(2091, 3, 1);
    private static readonly DateTime WindowEnd = new(2091, 3, 31);

    public ExpenseQuarantineTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(int id, string number)> InsertInvoiceAsync(string status)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var number = $"QUAR-{status}-{Guid.NewGuid():N}";
        var date = WindowStart.AddDays(10);

        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices " +
            "(invoice_number, invoice_date, due_date, amount_excl_vat, vat_rate, vat_amount, amount_incl_vat, status, currency, source, ocr_status, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, NOW(), NOW())",
            number, date, date, 100m, 21m, 21m, 121m, status, "EUR", "MANUAL", "COMPLETED");

        var id = await context.ExpenseInvoices
            .Where(i => i.InvoiceNumber == number)
            .Select(i => i.Id)
            .FirstAsync();
        return (id, number);
    }

    private async Task DeleteInvoicesAsync(params int[] ids)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        foreach (var id in ids)
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE id = {0}", id);
    }

    private ExpenseService CreateExpenseService() =>
        new(_fixture.Factory, new NullAuthService(), new DefaultCompanySettingsService());

    [Fact]
    public async Task GetCashFlowAsync_ExcludesDuplicatePendingAndRejected()
    {
        var pending = await InsertInvoiceAsync("PENDING");
        var duplicate = await InsertInvoiceAsync("DUPLICATE_PENDING");
        var rejected = await InsertInvoiceAsync("REJECTED");
        try
        {
            var result = await CreateExpenseService().GetCashFlowAsync(WindowStart, WindowEnd);
            var ids = result.Select(i => i.Id).ToList();

            Assert.Contains(pending.id, ids);
            Assert.DoesNotContain(duplicate.id, ids);
            Assert.DoesNotContain(rejected.id, ids);
        }
        finally
        {
            await DeleteInvoicesAsync(pending.id, duplicate.id, rejected.id);
        }
    }

    // The quarantine predicate is tested directly against the real database.

    [Fact]
    public async Task WhereCountsAsPayable_TranslatesToSql_AndExcludesQuarantined()
    {
        var pending = await InsertInvoiceAsync("PENDING");
        var duplicate = await InsertInvoiceAsync("DUPLICATE_PENDING");
        var rejected = await InsertInvoiceAsync("REJECTED");
        try
        {
            await using var context = await _fixture.Factory.CreateDbContextAsync();
            var ids = await context.ExpenseInvoices
                .Where(i => i.InvoiceDate >= WindowStart && i.InvoiceDate <= WindowEnd)
                .WhereCountsAsPayable()
                .Select(i => i.Id)
                .ToListAsync();

            Assert.Contains(pending.id, ids);
            Assert.DoesNotContain(duplicate.id, ids);
            Assert.DoesNotContain(rejected.id, ids);
        }
        finally
        {
            await DeleteInvoicesAsync(pending.id, duplicate.id, rejected.id);
        }
    }

    [Theory]
    [InlineData("PENDING", true)]
    [InlineData("NEEDS_REVIEW", true)]
    [InlineData("PENDING_SUPPLIER", true)]
    [InlineData("PAID", true)]
    [InlineData("DUPLICATE_PENDING", false)]
    [InlineData("REJECTED", false)]
    public void CountsAsPayable_InMemory(string status, bool expected)
    {
        Assert.Equal(expected, ExpenseStatusHelper.CountsAsPayable(status));
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
