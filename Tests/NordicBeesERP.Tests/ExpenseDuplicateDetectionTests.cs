using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// B4: CheckDuplicateAsync compares normalised invoice numbers (upper-case, no spaces,
/// '-', '/', '.') in SQL and never matches on a non-positive amount (production false
/// positive 277 ↔ 173, both „1" / 0,00 €). Integration tests against nordic_bees_erp_test.
/// </summary>
public class ExpenseDuplicateDetectionTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ExpenseDuplicateDetectionTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    private ExpenseService CreateService() =>
        new(_fixture.Factory, new NullAuthService(), new DefaultCompanySettingsService());

    private async Task<int> InsertInvoiceAsync(string number, decimal amountInclVat, string status = "PENDING")
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var date = DateTime.Today;
        var marker = $"DUPDET-{Guid.NewGuid():N}";

        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices " +
            "(invoice_number, invoice_date, due_date, amount_excl_vat, vat_rate, vat_amount, amount_incl_vat, status, notes, currency, source, ocr_status, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, NOW(), NOW())",
            number, date, date.AddDays(30), amountInclVat, 0m, 0m, amountInclVat, status, marker, "EUR", "MANUAL", "COMPLETED");

        return await context.ExpenseInvoices
            .Where(i => i.Notes == marker)
            .Select(i => i.Id)
            .FirstAsync();
    }

    private async Task DeleteAsync(int id)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE id = {0}", id);
    }

    [Fact]
    public async Task SpacedNumber_MatchesUnspacedNumber_WithEqualAmount()
    {
        var suffix = Guid.NewGuid().ToString("N").ToUpperInvariant();
        var id = await InsertInvoiceAsync($"PRD 0015764 {suffix}", 113.74m);
        try
        {
            var found = await CreateService().CheckDuplicateAsync(null, null, $"PRD0015764{suffix}", 113.74m);
            Assert.Equal(id, found);

            // punctuation and case are normalised on both sides too
            var foundPunct = await CreateService().CheckDuplicateAsync(null, null, $"prd-0015764/{suffix.ToLowerInvariant()}.", 113.74m);
            Assert.Equal(id, foundPunct);
        }
        finally
        {
            await DeleteAsync(id);
        }
    }

    [Fact]
    public async Task ZeroAmount_ShortNumber_NeverMatches()
    {
        // Production: 277 (Rotada) ↔ 173 (Franko), both „1" / 0,00 €
        var id = await InsertInvoiceAsync("1", 0m);
        try
        {
            Assert.Null(await CreateService().CheckDuplicateAsync(null, null, "1", 0m));
        }
        finally
        {
            await DeleteAsync(id);
        }
    }

    [Fact]
    public async Task DifferentAmount_DoesNotMatch()
    {
        var number = $"DIFF-{Guid.NewGuid():N}";
        var id = await InsertInvoiceAsync(number, 100m);
        try
        {
            Assert.Null(await CreateService().CheckDuplicateAsync(null, null, number, 100.50m));
        }
        finally
        {
            await DeleteAsync(id);
        }
    }

    [Fact]
    public async Task NormalisedComparison_TranslatesToSql()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var sql = ExpenseService
            .WhereNormalizedNumberEquals(context.ExpenseInvoices, "PRD0015764")
            .ToQueryString();

        Assert.Contains("REPLACE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UPPER", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("PRD 0015764", "PRD0015764")]
    [InlineData("534289/0227", "5342890227")]
    [InlineData("lyt-f070-021243", "LYTF070021243")]
    [InlineData("24- 14", "2414")]
    [InlineData(null, "")]
    public void NormalizeInvoiceNumber_StripsSeparatorsAndUpperCases(string? raw, string expected)
    {
        Assert.Equal(expected, ExpenseService.NormalizeInvoiceNumber(raw));
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
