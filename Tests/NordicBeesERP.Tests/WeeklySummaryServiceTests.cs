using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Etapas 4 Part C, C3: integration tests against the real nordic_bees_erp_test database, fixed
/// synthetic data (RESEARCH-2026-09-25-reliability.md §7's five numbers, PLAN-ETAPAS4.md §5).
/// </summary>
[Collection("RealDatabase")]
public class WeeklySummaryServiceTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public WeeklySummaryServiceTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetSummaryAsync_FixedWeek_ComputesAllFiveNumbersFromKnownData()
    {
        var weekStart = new DateTime(2027, 2, 1); // a Monday
        var tag = DateTime.UtcNow.Ticks;

        await using var setupContext = await _fixture.Factory.CreateDbContextAsync();

        // 1 auto-accepted (PAID, no edit), 1 edited-but-confirmed (PAID, has an EDITED row),
        // 1 still in a hard gate (PENDING_SUPPLIER), 1 with an arithmetic flag (NEEDS_REVIEW).
        var autoAcceptedNumber = $"INV-WS-AUTO-{tag}";
        var editedNumber = $"INV-WS-EDIT-{tag}";
        var pendingSupplierNumber = $"INV-WS-PSUP-{tag}";
        var arithmeticNumber = $"INV-WS-ARITH-{tag}";

        var withinWeek = weekStart.AddDays(2);

        var autoAcceptedId = await InsertInvoiceAsync(setupContext, autoAcceptedNumber, "PAID", withinWeek, null);
        var editedId = await InsertInvoiceAsync(setupContext, editedNumber, "PAID", withinWeek, null);
        await InsertInvoiceAsync(setupContext, pendingSupplierNumber, "PENDING_SUPPLIER", withinWeek, null);
        await InsertInvoiceAsync(setupContext, arithmeticNumber, "NEEDS_REVIEW", withinWeek, "[\"AMOUNT_MISMATCH\"]");

        await setupContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoice_audit (invoice_id, invoice_number, action, performed_at) VALUES ({0}, {1}, {2}, {3})",
            editedId, editedNumber, "EDITED", withinWeek);

        try
        {
            var reviewQueueService = new ReviewQueueAgingService(_fixture.Factory);
            var service = new WeeklySummaryService(_fixture.Factory, reviewQueueService,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<WeeklySummaryService>.Instance);

            var summary = await service.GetSummaryAsync(weekStart);

            Assert.Equal(weekStart, summary.WeekStart);
            Assert.Equal(weekStart.AddDays(6), summary.WeekEnd);

            // Processed/auto-accepted: only autoAcceptedId is confirmed + unedited.
            Assert.True(summary.ProcessedCount >= 4);
            Assert.True(summary.AutoAcceptedCount >= 1);
            Assert.True(summary.EditedInvoiceCount >= 1);

            Assert.True(summary.HardGateTriggerCounts["supplier"] >= 1);
            Assert.True(summary.HardGateTriggerCounts["arithmetic"] >= 1);
            Assert.Equal(0, summary.HardGateTriggerCounts["duplicate"]); // none inserted
            Assert.Equal(0, summary.HardGateTriggerCounts["date"]); // none inserted

            // RollingSilentErrorConfidenceBoundPercent: expense_audit_samples doesn't exist in
            // this DB yet (owner DDL, PLAN-ETAPAS4.md §3) — must be null, never fabricated.
            Assert.Null(summary.RollingSilentErrorConfidenceBoundPercent);

            _ = autoAcceptedId; // silence unused-variable warning; kept for clarity of intent
        }
        finally
        {
            foreach (var number in new[] { autoAcceptedNumber, editedNumber, pendingSupplierNumber, arithmeticNumber })
            {
                await CleanupAsync(number);
            }
        }
    }

    [Fact]
    public async Task GetSummaryAsync_NoInvoicesInWindow_ReturnsZeroesNotDivideByZeroError()
    {
        // A week far in the past with (very likely) nothing created in it.
        var weekStart = new DateTime(2001, 1, 1);

        var reviewQueueService = new ReviewQueueAgingService(_fixture.Factory);
        var service = new WeeklySummaryService(_fixture.Factory, reviewQueueService,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WeeklySummaryService>.Instance);

        var summary = await service.GetSummaryAsync(weekStart);

        Assert.Equal(0, summary.ProcessedCount);
        Assert.Equal(0, summary.AutoAcceptedCount);
        Assert.Equal(0, summary.AutoAcceptedPercent);
        Assert.Equal(0, summary.FieldCorrectionRate);
    }

    [Fact]
    public async Task GetSummaryAsync_DefaultWeekStart_IsMostRecentMonday()
    {
        var reviewQueueService = new ReviewQueueAgingService(_fixture.Factory);
        var service = new WeeklySummaryService(_fixture.Factory, reviewQueueService,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WeeklySummaryService>.Instance);

        var summary = await service.GetSummaryAsync();

        Assert.Equal(DayOfWeek.Monday, summary.WeekStart.DayOfWeek);
        Assert.True(summary.WeekStart <= DateTime.Today);
        Assert.True(DateTime.Today - summary.WeekStart < TimeSpan.FromDays(7));
    }

    private static async Task<int> InsertInvoiceAsync(NordicBeesERP.Data.NordicBeesERPContext context, string invoiceNumber, string status, DateTime createdAt, string? ocrFlags)
    {
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices (invoice_number, invoice_date, due_date, status, ocr_flags, amount_excl_vat, vat_amount, amount_incl_vat, created_at, updated_at) VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9})",
            invoiceNumber, createdAt.Date, createdAt.Date.AddDays(14), status, ocrFlags, 100.00m, 21.00m, 121.00m, createdAt, createdAt);

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
