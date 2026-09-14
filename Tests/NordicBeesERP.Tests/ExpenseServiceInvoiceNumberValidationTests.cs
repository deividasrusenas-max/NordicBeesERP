using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// N2/NF-10 regression tests: OCR persistence must reject a blank invoice
/// number in the service itself and must never allow null to reach the
/// NOT NULL expense_invoices.invoice_number column.
/// </summary>
public class ExpenseServiceInvoiceNumberValidationTests : IClassFixture<DbTestFixture>
{
    private const string ExpectedErrorMessage =
        "Sąskaitos numeris negali būti tuščias. Įveskite numerį rankiniu būdu.";

    private readonly DbTestFixture _fixture;

    public ExpenseServiceInvoiceNumberValidationTests(DbTestFixture fixture)
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

    private async Task<(int Id, string InvoiceNumber)> InsertExistingInvoiceAsync()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        var invoiceNumber = $"N2-EXIST-{Guid.NewGuid():N}";
        var invoiceDate = DateTime.UtcNow.Date;
        var dueDate = invoiceDate.AddDays(30);

        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices " +
            "(invoice_number, invoice_date, due_date, amount_excl_vat, vat_rate, vat_amount, amount_incl_vat, notes, status, currency, source, ocr_status, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, NOW(), NOW())",
            invoiceNumber,
            invoiceDate,
            dueDate,
            100m,
            21m,
            21m,
            121m,
            "",
            "DRAFT",
            "EUR",
            "MANUAL",
            "PENDING");

        var id = await context.ExpenseInvoices
            .FromSqlRaw("SELECT * FROM expense_invoices WHERE invoice_number = {0}", invoiceNumber)
            .AsNoTracking()
            .Select(x => x.Id)
            .FirstOrDefaultAsync();

        Assert.True(id > 0);

        return (id, invoiceNumber);
    }

    [Fact]
    public async Task CreateFromOcrAsync_BlankInvoiceNumber_ThrowsAndWritesNoRow()
    {
        var service = CreateService();
        var marker = $"N2-CREATE-{Guid.NewGuid():N}";

        var ocrResult = new OcrResultDto
        {
            InvoiceNumber = string.Empty,
            OriginalFilename = marker
        };

        try
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateFromOcrAsync(ocrResult));

            Assert.Equal(ExpectedErrorMessage, ex.Message);
            Assert.Contains(OcrFlag.MissingInvNumber, ocrResult.Flags);

            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
            var written = await verifyContext.ExpenseInvoices
                .AsNoTracking()
                .AnyAsync(i => i.OriginalFilename == marker);

            Assert.False(written);
        }
        finally
        {
            await using var cleanupContext = await _fixture.Factory.CreateDbContextAsync();
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM expense_invoices WHERE original_filename = {0}",
                marker);
        }
    }

    [Fact]
    public async Task UpdateFromOcrAsync_BlankInvoiceNumber_ThrowsAndKeepsExistingInvoiceNumber()
    {
        var (id, existingNumber) = await InsertExistingInvoiceAsync();

        try
        {
            var service = CreateService();

            var ocrResult = new OcrResultDto
            {
                InvoiceNumber = "   "
            };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.UpdateFromOcrAsync(id, ocrResult));

            Assert.Equal(ExpectedErrorMessage, ex.Message);
            Assert.Contains(OcrFlag.MissingInvNumber, ocrResult.Flags);

            await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
            var reloaded = await verifyContext.ExpenseInvoices
                .AsNoTracking()
                .FirstAsync(i => i.Id == id);

            Assert.Equal(existingNumber, reloaded.InvoiceNumber);
        }
        finally
        {
            await using var cleanupContext = await _fixture.Factory.CreateDbContextAsync();
            await cleanupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM expense_invoices WHERE id = {0}",
                id);
        }
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
