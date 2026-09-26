using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Helpers;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// OCR Etapas 2 S3c (D-045): ChangeSupplierAsync — allowed for NEEDS_REVIEW and PENDING invoices without payments, refused for every
/// other status, voids an approval, recomputes flags and status by the shared rules, audits SUPPLIER_CHANGED. Real nordic_bees_erp_test.
/// </summary>
public class ExpenseChangeSupplierTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;
    public ExpenseChangeSupplierTests(DbTestFixture fixture) { _fixture = fixture; }

    private ExpenseService CreateService() => new(_fixture.Factory, new NullAuth(), new DefaultSettings());

    private async Task<int> InsertPartnerAsync(string countryCode = "LT")
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Supplier, Name = $"S3C {Guid.NewGuid():N}", Country = countryCode, CountryCode = countryCode,
            DefaultLanguage = "LT", PaymentTermDays = 14, DefaultVatRate = 21m, IsSupplier = true, IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();
        return partner.Id;
    }

    private async Task<int> InsertInvoiceAsync(string status, int? supplierId, IEnumerable<string>? flags = null, string? approvedBy = null)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var marker = $"S3C-{Guid.NewGuid():N}";
        var date = DateTime.Today;
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices " +
            "(invoice_number, invoice_date, due_date, amount_excl_vat, vat_rate, vat_amount, amount_incl_vat, status, supplier_id, ocr_flags, approved_by, approved_at, notes, currency, source, ocr_status, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, 100, 21, 21, 121, {3}, {4}, {5}, {6}, {7}, {8}, 'EUR', 'MANUAL', 'COMPLETED', NOW(), NOW())",
            marker, date, date.AddDays(30), status, supplierId, JsonSerializer.Serialize(flags ?? Array.Empty<string>()),
            approvedBy, approvedBy == null ? null : DateTime.Now, marker);
        return await context.ExpenseInvoices.Where(i => i.Notes == marker).Select(i => i.Id).FirstAsync();
    }

    private async Task<ExpenseInvoice> ReloadAsync(int id)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoices.AsNoTracking().FirstAsync(i => i.Id == id);
    }

    private async Task<List<ExpenseInvoiceAudit>> AuditsAsync(int id)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoiceAudits.AsNoTracking().Where(a => a.InvoiceId == id).ToListAsync();
    }

    private async Task CleanupAsync(IEnumerable<int> invoices, params int[] partners)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        foreach (var id in invoices)
        {
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_payments WHERE invoice_id = {0}", id);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_audit WHERE invoice_id = {0}", id);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE id = {0}", id);
        }
        foreach (var id in partners)
            await context.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", id);
    }

    [Theory]
    [InlineData("NEEDS_REVIEW")]
    [InlineData("PENDING")]
    public async Task Allowed_ForNeedsReviewAndPending_ChangesSupplier_AuditsOldToNew(string status)
    {
        var oldPartner = await InsertPartnerAsync();
        var newPartner = await InsertPartnerAsync();
        var invoice = await InsertInvoiceAsync(status, oldPartner);
        try
        {
            await CreateService().ChangeSupplierAsync(invoice, newPartner, "Test User");

            var after = await ReloadAsync(invoice);
            Assert.Equal(newPartner, after.SupplierId);
            var audit = Assert.Single(await AuditsAsync(invoice), a => a.Action == "SUPPLIER_CHANGED");
            Assert.Equal($"Tiekėjo ID: {oldPartner} → {newPartner}", audit.ActionDetails);
            Assert.Equal("Test User", audit.PerformedBy);
        }
        finally { await CleanupAsync(new[] { invoice }, oldPartner, newPartner); }
    }

    [Theory]
    [InlineData("PARTIAL")]
    [InlineData("PAID")]
    [InlineData("OVERDUE")]
    [InlineData("REJECTED")]
    [InlineData("DUPLICATE_PENDING")]
    [InlineData("PENDING_SUPPLIER")]
    public async Task Refused_ForEveryOtherStatus_NothingChanges(string status)
    {
        var oldPartner = await InsertPartnerAsync();
        var newPartner = await InsertPartnerAsync();
        var invoice = await InsertInvoiceAsync(status, status == "PENDING_SUPPLIER" ? null : oldPartner);
        try
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().ChangeSupplierAsync(invoice, newPartner, "Test User"));
            Assert.Contains("iekėj", ex.Message);

            var after = await ReloadAsync(invoice);
            Assert.Equal(status, after.Status);
            Assert.Equal(status == "PENDING_SUPPLIER" ? (int?)null : oldPartner, after.SupplierId);
            Assert.Empty(await AuditsAsync(invoice));
        }
        finally { await CleanupAsync(new[] { invoice }, oldPartner, newPartner); }
    }

    [Fact]
    public async Task Refused_WhenTheInvoiceHasPayments_SameSupplier_MissingPartner()
    {
        var oldPartner = await InsertPartnerAsync();
        var newPartner = await InsertPartnerAsync();
        var paid = await InsertInvoiceAsync("PENDING", oldPartner);
        var plain = await InsertInvoiceAsync("PENDING", oldPartner);
        try
        {
            await using (var context = await _fixture.Factory.CreateDbContextAsync())
                await context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO expense_payments (invoice_id, payment_date, amount, payment_method, created_at) VALUES ({0}, NOW(), 1, 'BANK', NOW())", paid);

            var service = CreateService();
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChangeSupplierAsync(paid, newPartner, "Test User"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChangeSupplierAsync(plain, oldPartner, "Test User"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChangeSupplierAsync(plain, int.MaxValue, "Test User"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChangeSupplierAsync(int.MaxValue, newPartner, "Test User"));

            Assert.Equal(oldPartner, (await ReloadAsync(paid)).SupplierId);
            Assert.Equal(oldPartner, (await ReloadAsync(plain)).SupplierId);
        }
        finally { await CleanupAsync(new[] { paid, plain }, oldPartner, newPartner); }
    }

    [Fact]
    public async Task ApprovedInvoice_ApprovalIsVoided_AuditedAndStatusByTheSharedRules()
    {
        var oldPartner = await InsertPartnerAsync();
        var newPartner = await InsertPartnerAsync();
        var approved = await InsertInvoiceAsync("PENDING", oldPartner, approvedBy: "Approver");
        var reviewFlag = await InsertInvoiceAsync("PENDING", oldPartner, new[] { OcrFlag.ZeroVat }, approvedBy: "Approver");
        try
        {
            var service = CreateService();
            await service.ChangeSupplierAsync(approved, newPartner, "Test User");
            await service.ChangeSupplierAsync(reviewFlag, newPartner, "Test User");

            var clean = await ReloadAsync(approved);
            Assert.Null(clean.ApprovedBy);
            Assert.Null(clean.ApprovedAt);
            Assert.Equal("PENDING", clean.Status);   // no review flag → PENDING by the shared rules, but no longer approved
            Assert.Contains(await AuditsAsync(approved), a => a.Action == "APPROVAL_VOIDED" && a.ActionDetails == "Pakeisti laukai: supplier_id");

            var flagged = await ReloadAsync(reviewFlag);
            Assert.Null(flagged.ApprovedBy);
            Assert.Equal("NEEDS_REVIEW", flagged.Status);
        }
        finally { await CleanupAsync(new[] { approved, reviewFlag }, oldPartner, newPartner); }
    }

    [Fact]
    public async Task NotApproved_NoApprovalVoidedAudit_AndVendorFlagsAreCleared_OthersKept()
    {
        var oldPartner = await InsertPartnerAsync();
        var newPartner = await InsertPartnerAsync();
        var invoice = await InsertInvoiceAsync("NEEDS_REVIEW", oldPartner, new[] { OcrFlag.VendorNotFound, OcrFlag.VendorSuggested, OcrFlag.VendorAmbiguous, OcrFlag.ZeroVat });
        try
        {
            await CreateService().ChangeSupplierAsync(invoice, newPartner, "Test User");

            Assert.DoesNotContain(await AuditsAsync(invoice), a => a.Action == "APPROVAL_VOIDED");
            var flags = ExpenseStatusHelper.ParseFlags((await ReloadAsync(invoice)).OcrFlags);
            Assert.DoesNotContain(OcrFlag.VendorNotFound, flags);
            Assert.DoesNotContain(OcrFlag.VendorSuggested, flags);
            Assert.DoesNotContain(OcrFlag.VendorAmbiguous, flags);
            Assert.Contains(OcrFlag.ZeroVat, flags);
        }
        finally { await CleanupAsync(new[] { invoice }, oldPartner, newPartner); }
    }

    [Fact]
    public async Task RateFlags_AreRecomputedForTheNewPartnersCountry()
    {
        // a 12 % rate: not allowed for LT (21 %), allowed for LV — the change of country must move the flag
        var ltPartner = await InsertPartnerAsync("LT");
        var lvPartner = await InsertPartnerAsync("LV");
        var invoice = await InsertInvoiceAsync("PENDING", ltPartner);
        try
        {
            await using (var context = await _fixture.Factory.CreateDbContextAsync())
                await context.Database.ExecuteSqlRawAsync("UPDATE expense_invoices SET vat_rate = 12 WHERE id = {0}", invoice);

            // the shipped table has no CONFIRMED row, so the gate is exercised with the real rows flipped to Confirmed
            var confirmed = NordicBeesERP.Services.Validation.VatRateTable.Rows
                .Select(r => r with { Status = NordicBeesERP.Services.Validation.VatRateRowStatus.Confirmed }).ToList();
            var service = new ExpenseService(_fixture.Factory, new NullAuth(), new DefaultSettings(), confirmed);
            await service.ChangeSupplierAsync(invoice, lvPartner, "Test User");
            var toLv = ExpenseStatusHelper.ParseFlags((await ReloadAsync(invoice)).OcrFlags);
            await service.ChangeSupplierAsync(invoice, ltPartner, "Test User");
            var backToLt = ExpenseStatusHelper.ParseFlags((await ReloadAsync(invoice)).OcrFlags);

            Assert.DoesNotContain(OcrFlag.VatRateNotAllowed, toLv);
            Assert.Contains(OcrFlag.VatRateNotAllowed, backToLt);
        }
        finally { await CleanupAsync(new[] { invoice }, ltPartner, lvPartner); }
    }

    // The two sweep restrictions the S3b review found unguarded: only PENDING_SUPPLIER, only invoices without a supplier
    [Fact]
    public async Task AutoAssignSweep_OnlyTouchesPendingSupplierInvoicesWithoutASupplier()
    {
        var vat = "LT9" + Random.Shared.NextInt64(10_000_000_000, 99_999_999_999);
        var partner = await InsertPartnerAsync();
        var other = await InsertPartnerAsync();
        var waiting = await InsertInvoiceAsync("PENDING_SUPPLIER", null);
        var pending = await InsertInvoiceAsync("PENDING", other);
        var assignedWaiting = await InsertInvoiceAsync("PENDING_SUPPLIER", other);
        var noSupplierReview = await InsertInvoiceAsync("NEEDS_REVIEW", null); // status is not PENDING_SUPPLIER although there is no supplier
        try
        {
            await using (var context = await _fixture.Factory.CreateDbContextAsync())
                foreach (var id in new[] { waiting, pending, assignedWaiting, noSupplierReview })
                    await context.Database.ExecuteSqlRawAsync(
                        "UPDATE expense_invoices SET pending_supplier_vat = {0}, pending_supplier_country_code = 'LT' WHERE id = {1}", vat, id);

            var count = await CreateService().AutoAssignSupplierAsync(vat, null, partner);

            Assert.Equal(1, count);
            Assert.Equal(partner, (await ReloadAsync(waiting)).SupplierId);
            Assert.Equal(other, (await ReloadAsync(pending)).SupplierId);
            Assert.Equal(other, (await ReloadAsync(assignedWaiting)).SupplierId);
            var untouched = await ReloadAsync(noSupplierReview);
            Assert.Null(untouched.SupplierId);
            Assert.Equal("NEEDS_REVIEW", untouched.Status);
        }
        finally { await CleanupAsync(new[] { waiting, pending, assignedWaiting, noSupplierReview }, partner, other); }
    }

    private sealed class NullAuth : IAuthService
    {
        public Task<ErpUser?> ValidateUserAsync(string email, string password) => Task.FromResult<ErpUser?>(null);
        public Task SeedAdminAsync(string email, string password) => Task.CompletedTask;
        public Task<ErpUser?> GetAuthenticatedUserAsync() => Task.FromResult<ErpUser?>(null);
        public Task<int?> GetCustomerIdAsync() => Task.FromResult<int?>(null);
        public Task<int?> GetUserIdAsync() => Task.FromResult<int?>(null);
        public Task<ErpUser?> GetUserByIdAsync(int userId) => Task.FromResult<ErpUser?>(null);
        public Task<string> GetRequiredActorNameAsync() => throw new NotImplementedException();
    }

    private sealed class DefaultSettings : ICompanySettingsService
    {
        public Task<CompanySettings> GetSettingsAsync() => Task.FromResult(new CompanySettings());
        public Task UpdateSettingsAsync(CompanySettings settings) => Task.CompletedTask;
    }
}
