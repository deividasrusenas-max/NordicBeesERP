using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Etapas 4 Part C, C2: integration tests against the real nordic_bees_erp_test database
/// (D-031 criterion 6, PLAN-ETAPAS4.md §4) — synthetic invoices only, no personal data.
/// </summary>
public class ReviewQueueAgingServiceTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ReviewQueueAgingServiceTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetUnresolvedInvoicesAsync_NoAuditRow_UsesCreatedAtAsSinceWhen()
    {
        var now = DateTime.UtcNow;
        var invoiceNumber = $"INV-AGE-NOAUDIT-{now.Ticks}";
        var createdAt = now.AddDays(-10); // several working days ago, no matter the weekday

        await using var setupContext = await _fixture.Factory.CreateDbContextAsync();
        await InsertInvoiceAsync(setupContext, invoiceNumber, "NEEDS_REVIEW", createdAt);

        try
        {
            var service = new ReviewQueueAgingService(_fixture.Factory);
            var all = await service.GetUnresolvedInvoicesAsync();
            var invoice = all.Single(a => a.InvoiceNumber == invoiceNumber);

            Assert.Equal("NEEDS_REVIEW", invoice.Status);
            Assert.True(invoice.WorkingDaysUnresolved > 0);
            Assert.Equal(createdAt.Date, invoice.SinceStatusEnteredAt.Date);
        }
        finally
        {
            await CleanupAsync(invoiceNumber);
        }
    }

    [Fact]
    public async Task GetUnresolvedInvoicesAsync_WithAuditRow_UsesLatestTransitionIntoCurrentStatus()
    {
        var now = DateTime.UtcNow;
        var invoiceNumber = $"INV-AGE-AUDIT-{now.Ticks}";
        var createdAt = now.AddDays(-30); // old creation — should NOT be used as "since"
        var transitionAt = now.AddDays(-3); // the real "since" clock

        await using var setupContext = await _fixture.Factory.CreateDbContextAsync();
        var invoiceId = await InsertInvoiceAsync(setupContext, invoiceNumber, "NEEDS_REVIEW", createdAt);

        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoice_audit (invoice_id, invoice_number, action, old_status, new_status, performed_at) VALUES ({0}, {1}, {2}, {3}, {4}, {5})",
            invoiceId, invoiceNumber, "STATUS_CHANGE", "PENDING_SUPPLIER", "NEEDS_REVIEW", transitionAt);
        // An older, earlier transition into a DIFFERENT status must not be picked.
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoice_audit (invoice_id, invoice_number, action, old_status, new_status, performed_at) VALUES ({0}, {1}, {2}, {3}, {4}, {5})",
            invoiceId, invoiceNumber, "STATUS_CHANGE", null!, "PENDING_SUPPLIER", createdAt);

        try
        {
            var service = new ReviewQueueAgingService(_fixture.Factory);
            var all = await service.GetUnresolvedInvoicesAsync();
            var invoice = all.Single(a => a.InvoiceNumber == invoiceNumber);

            Assert.Equal(transitionAt.Date, invoice.SinceStatusEnteredAt.Date);
        }
        finally
        {
            await CleanupAsync(invoiceNumber);
        }
    }

    [Fact]
    public async Task GetUnresolvedInvoicesAsync_ConfirmedOrRejectedStatus_IsExcluded()
    {
        var now = DateTime.UtcNow;
        var paidNumber = $"INV-AGE-PAID-{now.Ticks}";
        var rejectedNumber = $"INV-AGE-REJ-{now.Ticks}";

        await using var setupContext = await _fixture.Factory.CreateDbContextAsync();
        await InsertInvoiceAsync(setupContext, paidNumber, "PAID", now.AddDays(-30));
        await InsertInvoiceAsync(setupContext, rejectedNumber, "REJECTED", now.AddDays(-30));

        try
        {
            var service = new ReviewQueueAgingService(_fixture.Factory);
            var all = await service.GetUnresolvedInvoicesAsync();

            Assert.DoesNotContain(all, a => a.InvoiceNumber == paidNumber);
            Assert.DoesNotContain(all, a => a.InvoiceNumber == rejectedNumber);
        }
        finally
        {
            await CleanupAsync(paidNumber);
            await CleanupAsync(rejectedNumber);
        }
    }

    [Fact]
    public async Task GetUnresolvedLongerThanThresholdAsync_OverrideThreshold_FiltersCorrectly()
    {
        var now = DateTime.UtcNow;
        var invoiceNumber = $"INV-AGE-THRESH-{now.Ticks}";
        await using var setupContext = await _fixture.Factory.CreateDbContextAsync();
        await InsertInvoiceAsync(setupContext, invoiceNumber, "DUPLICATE_PENDING", now.AddDays(-20));

        try
        {
            var service = new ReviewQueueAgingService(_fixture.Factory);

            var pastLowThreshold = await service.GetUnresolvedLongerThanThresholdAsync(overrideThresholdWorkingDays: 1);
            Assert.Contains(pastLowThreshold, a => a.InvoiceNumber == invoiceNumber);

            var pastHighThreshold = await service.GetUnresolvedLongerThanThresholdAsync(overrideThresholdWorkingDays: 1000);
            Assert.DoesNotContain(pastHighThreshold, a => a.InvoiceNumber == invoiceNumber);
        }
        finally
        {
            await CleanupAsync(invoiceNumber);
        }
    }

    [Fact]
    public async Task GetThresholdWorkingDaysAsync_NoSettingRow_DefaultsToFive()
    {
        await using var setupContext = await _fixture.Factory.CreateDbContextAsync();
        await setupContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM app_settings WHERE setting_key = {0}", "expense_review_queue_aging_working_days");

        var service = new ReviewQueueAgingService(_fixture.Factory);
        var threshold = await service.GetThresholdWorkingDaysAsync();

        Assert.Equal(5, threshold);
    }

    [Fact]
    public async Task GetThresholdWorkingDaysAsync_ConfiguredSetting_IsUsed()
    {
        await using var setupContext = await _fixture.Factory.CreateDbContextAsync();
        await setupContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM app_settings WHERE setting_key = {0}", "expense_review_queue_aging_working_days");
        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO app_settings (setting_key, setting_value) VALUES ({0}, {1})",
            "expense_review_queue_aging_working_days", "10");

        try
        {
            var service = new ReviewQueueAgingService(_fixture.Factory);
            var threshold = await service.GetThresholdWorkingDaysAsync();
            Assert.Equal(10, threshold);
        }
        finally
        {
            await setupContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM app_settings WHERE setting_key = {0}", "expense_review_queue_aging_working_days");
        }
    }

    private static async Task<int> InsertInvoiceAsync(NordicBeesERP.Data.NordicBeesERPContext context, string invoiceNumber, string status, DateTime createdAt)
    {
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices (invoice_number, invoice_date, due_date, status, amount_excl_vat, vat_amount, amount_incl_vat, created_at, updated_at) VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8})",
            invoiceNumber, createdAt.Date, createdAt.Date.AddDays(14), status, 100.00m, 21.00m, 121.00m, createdAt, createdAt);

        return await context.Database.SqlQueryRaw<int>(
            "SELECT id AS Value FROM expense_invoices WHERE invoice_number = {0}", invoiceNumber).FirstAsync();
    }

    private async Task CleanupAsync(string invoiceNumber)
    {
        await using var cleanupContext = await _fixture.Factory.CreateDbContextAsync();
        await cleanupContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM expense_invoice_audit WHERE invoice_number = {0}", invoiceNumber);
        await cleanupContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM expense_invoices WHERE invoice_number = {0}", invoiceNumber);
    }
}
