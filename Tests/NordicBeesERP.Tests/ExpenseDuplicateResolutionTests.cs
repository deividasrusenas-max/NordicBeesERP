using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// D-027 §2–§3 (B3): a DUPLICATE_PENDING invoice is resolved only through audited service
/// methods — "different invoice" removes the DUPLICATE flag and sets the rule-derived status,
/// "reject" keeps the row as REJECTED, and approving an unresolved duplicate is refused.
/// Integration tests against nordic_bees_erp_test.
/// </summary>
[Collection("RealDatabase")]
public class ExpenseDuplicateResolutionTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ExpenseDuplicateResolutionTests(DbTestFixture fixture)
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
            Name = $"DupRes Supplier {Guid.NewGuid():N}",
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

    private async Task<int> InsertInvoiceAsync(string status, int? supplierId, IEnumerable<string> flags)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var number = $"DUPRES-{Guid.NewGuid():N}";
        var date = DateTime.Today;

        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices " +
            "(invoice_number, invoice_date, due_date, amount_excl_vat, vat_rate, vat_amount, amount_incl_vat, status, supplier_id, ocr_flags, currency, source, ocr_status, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, NOW(), NOW())",
            number, date, date.AddDays(30), 100m, 21m, 21m, 121m, status, supplierId,
            JsonSerializer.Serialize(flags), "EUR", "MANUAL", "COMPLETED");

        return await context.ExpenseInvoices
            .Where(i => i.InvoiceNumber == number)
            .Select(i => i.Id)
            .FirstAsync();
    }

    private async Task<ExpenseInvoice?> ReloadAsync(int id)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
    }

    private static List<string> FlagsOf(ExpenseInvoice invoice) =>
        string.IsNullOrEmpty(invoice.OcrFlags) ? new() : JsonSerializer.Deserialize<List<string>>(invoice.OcrFlags) ?? new();

    private async Task<List<ExpenseInvoiceAudit>> AuditsAsync(int id)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoiceAudits.AsNoTracking().Where(a => a.InvoiceId == id).ToListAsync();
    }

    private async Task CleanupAsync(int invoiceId, int? supplierId)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_audit WHERE invoice_id = {0}", invoiceId);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE id = {0}", invoiceId);
        if (supplierId.HasValue)
            await context.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", supplierId.Value);
    }

    [Fact]
    public async Task ResolveAsDifferent_RemovesFlag_SetsRuleDerivedStatus_Audited()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("DUPLICATE_PENDING", supplierId, new[] { OcrFlag.Duplicate, OcrFlag.LinesNotFound });
        try
        {
            await CreateService().ResolveDuplicateAsDifferentAsync(id, "Test User");

            var reloaded = await ReloadAsync(id);
            Assert.NotNull(reloaded);
            Assert.Equal("PENDING", reloaded!.Status);
            Assert.DoesNotContain(OcrFlag.Duplicate, FlagsOf(reloaded));
            Assert.Contains(OcrFlag.LinesNotFound, FlagsOf(reloaded));

            var audit = Assert.Single(await AuditsAsync(id), a => a.Action == "DUPLICATE_DISMISSED");
            Assert.Equal("Test User", audit.PerformedBy);
            Assert.Equal("DUPLICATE_PENDING", audit.OldStatus);
            Assert.Equal("PENDING", audit.NewStatus);
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Fact]
    public async Task ResolveAsDifferent_ReviewFlagRemains_GoesToNeedsReview()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("DUPLICATE_PENDING", supplierId, new[] { OcrFlag.Duplicate, OcrFlag.ZeroVat });
        try
        {
            await CreateService().ResolveDuplicateAsDifferentAsync(id, "Test User");

            Assert.Equal("NEEDS_REVIEW", (await ReloadAsync(id))!.Status);
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Fact]
    public async Task ResolveAsDifferent_NoSupplier_GoesToPendingSupplier()
    {
        var id = await InsertInvoiceAsync("DUPLICATE_PENDING", null, new[] { OcrFlag.Duplicate, OcrFlag.VendorNotFound });
        try
        {
            await CreateService().ResolveDuplicateAsDifferentAsync(id, "Test User");

            Assert.Equal("PENDING_SUPPLIER", (await ReloadAsync(id))!.Status);
        }
        finally
        {
            await CleanupAsync(id, null);
        }
    }

    [Fact]
    public async Task Reject_KeepsRowAsRejected()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("DUPLICATE_PENDING", supplierId, new[] { OcrFlag.Duplicate });
        try
        {
            await CreateService().RejectAsync(id, "Dublikatas: TEST-1", "Test User");

            var reloaded = await ReloadAsync(id);
            Assert.NotNull(reloaded); // never hard-deleted
            Assert.Equal("REJECTED", reloaded!.Status);
            Assert.Equal("Dublikatas: TEST-1", reloaded.RejectedReason);
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Fact]
    public async Task Approve_DuplicatePending_IsRefused_StatusUnchanged()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("DUPLICATE_PENDING", supplierId, new[] { OcrFlag.Duplicate });
        try
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateService().ApproveAsync(id, "Test User"));
            Assert.Equal("Dublikatą pirmiausia reikia išspręsti", ex.Message);

            Assert.Equal("DUPLICATE_PENDING", (await ReloadAsync(id))!.Status);
            Assert.DoesNotContain(await AuditsAsync(id), a => a.Action == "APPROVED");
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
