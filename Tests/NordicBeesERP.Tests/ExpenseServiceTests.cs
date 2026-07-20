using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Integration tests for ExpenseService write methods against the real
/// nordic_bees_erp_test database. Verifies that ExecuteSqlRawAsync-based
/// UPDATE/DELETE actually persist changes (not silent NoTracking no-ops).
/// </summary>
public class ExpenseServiceTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ExpenseServiceTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    private ExpenseService CreateService()
    {
        return new ExpenseService(
            _fixture.Factory,
            new TestAuthService(),
            new TestCompanySettingsService());
    }

    /// <summary>
    /// Inserts a minimal expense_invoices row and returns its id.
    /// The invoice_number is guaranteed unique via Guid.
    /// </summary>
    private async Task<int> InsertTestInvoiceAsync(string? notes = null)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        var invoiceNumber = $"EXP-TEST-{Guid.NewGuid():N}";
        var invoiceDate = DateTime.UtcNow.Date;
        var dueDate = invoiceDate.AddDays(30);
        var amountExclVat = 100m;
        var vatRate = 21m;
        var vatAmount = 21m;
        var amountInclVat = 121m;

        // Notes is nullable — NULL in SQL when null in C#
        var notesParam = notes ?? "";

        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices " +
            "(invoice_number, invoice_date, due_date, amount_excl_vat, vat_rate, vat_amount, amount_incl_vat, notes, status, currency, source, ocr_status, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, NOW(), NOW())",
            invoiceNumber,
            invoiceDate,
            dueDate,
            amountExclVat,
            vatRate,
            vatAmount,
            amountInclVat,
            notesParam,
            "DRAFT",
            "EUR",
            "MANUAL",
            "PENDING");

        // Read back the inserted id by matching on the unique invoice_number
        var id = await context.ExpenseInvoices
            .FromSqlRaw("SELECT * FROM expense_invoices WHERE invoice_number = {0}", invoiceNumber)
            .AsNoTracking()
            .Select(x => x.Id)
            .FirstOrDefaultAsync();

        return id;
    }

    [Fact]
    public async Task UpdateInvoiceAsync_PersistsChangesToRealDatabase()
    {
        var id = await InsertTestInvoiceAsync(notes: null);

        var service = CreateService();

        // Build the ExpenseInvoice object with modified values
        var invoice = new ExpenseInvoice
        {
            Id = id,
            InvoiceNumber = $"EXP-UPDATED-{Guid.NewGuid():N}",
            InvoiceDate = DateTime.UtcNow.Date,
            DueDate = DateTime.UtcNow.Date.AddDays(60),
            AmountExclVat = 200m,
            VatRate = 21m,
            VatAmount = 42m,
            AmountInclVat = 242m,
            Notes = "Updated notes from test",
            Status = "PENDING",
            UpdatedAt = DateTime.UtcNow,
        };

        await service.UpdateInvoiceAsync(invoice);

        // Verify via a completely fresh context that the write actually reached the DB
        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var reloaded = await verifyContext.ExpenseInvoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id);

        Assert.NotNull(reloaded);
        Assert.Equal("Updated notes from test", reloaded!.Notes);
        Assert.Equal("PENDING", reloaded.Status);
        Assert.Equal(200m, reloaded.AmountExclVat);

        // Cleanup
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM expense_invoices WHERE id = {0}", id);
    }

    [Fact]
    public async Task DeleteInvoiceAsync_RemovesInvoiceFromDatabase()
    {
        var id = await InsertTestInvoiceAsync();

        var service = CreateService();

        // Delete the invoice
        var result = await service.DeleteInvoiceAsync(id);
        Assert.True(result);

        // Verify row is actually gone via a fresh context
        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var exists = await verifyContext.ExpenseInvoices
            .AsNoTracking()
            .AnyAsync(i => i.Id == id);

        Assert.False(exists);
    }

    [Fact]
    public async Task DeleteInvoiceAsync_ReturnsFalseForNonExistentId()
    {
        var service = CreateService();

        var result = await service.DeleteInvoiceAsync(999999);
        Assert.False(result);
    }

    // ============ Stub implementations ============

    private sealed class TestAuthService : IAuthService
    {
        public Task<ErpUser?> ValidateUserAsync(string email, string password) => Task.FromResult<ErpUser?>(null);
        public Task SeedAdminAsync(string email, string password) => Task.CompletedTask;
        public Task<ErpUser?> GetAuthenticatedUserAsync() => Task.FromResult<ErpUser?>(null);
        public Task<int?> GetCustomerIdAsync() => Task.FromResult<int?>(null);
        public Task<int?> GetUserIdAsync() => Task.FromResult<int?>(null);
        public Task<ErpUser?> GetUserByIdAsync(int userId) => Task.FromResult<ErpUser?>(null);
        public Task<string> GetRequiredActorNameAsync() => throw new NotImplementedException();
    }

    private sealed class TestCompanySettingsService : ICompanySettingsService
    {
        public Task<CompanySettings> GetSettingsAsync() => Task.FromResult(new CompanySettings());
        public Task UpdateSettingsAsync(CompanySettings settings) => Task.CompletedTask;
    }
}
