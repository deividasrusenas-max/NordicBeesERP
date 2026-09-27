using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using NordicBeesERP.Data;
using NordicBeesERP.Services.Dtos;

namespace NordicBeesERP.Services;

/// <inheritdoc cref="IWeeklySummaryService"/>
public sealed class WeeklySummaryService : IWeeklySummaryService
{
    private static readonly HashSet<string> ConfirmedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "PENDING", "PARTIAL", "PAID", "OVERDUE"
    };

    private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;
    private readonly IReviewQueueAgingService _reviewQueueAgingService;
    private readonly ILogger<WeeklySummaryService> _logger;

    public WeeklySummaryService(
        IDbContextFactory<NordicBeesERPContext> dbFactory,
        IReviewQueueAgingService reviewQueueAgingService,
        ILogger<WeeklySummaryService> logger)
    {
        _dbFactory = dbFactory;
        _reviewQueueAgingService = reviewQueueAgingService;
        _logger = logger;
    }

    public async Task<WeeklySummary> GetSummaryAsync(DateTime? weekStart = null, CancellationToken ct = default)
    {
        var start = (weekStart ?? MostRecentMonday(DateTime.Today)).Date;
        var end = start.AddDays(7); // exclusive upper bound

        await using var context = await _dbFactory.CreateDbContextAsync(ct);

        var windowInvoices = await context.ExpenseInvoices
            .Where(i => i.CreatedAt >= start && i.CreatedAt < end)
            .Select(i => new { i.Id, i.Status, i.OcrFlags })
            .ToListAsync(ct);

        var processedCount = windowInvoices.Count;

        var invoiceIds = windowInvoices.Select(i => i.Id).ToList();
        var editedInvoiceIds = invoiceIds.Count == 0
            ? new HashSet<int>()
            : (await context.ExpenseInvoiceAudits
                .Where(a => invoiceIds.Contains(a.InvoiceId) && a.Action == "EDITED")
                .Select(a => a.InvoiceId)
                .Distinct()
                .ToListAsync(ct)).ToHashSet();

        var autoAcceptedCount = windowInvoices.Count(i =>
            ConfirmedStatuses.Contains(i.Status) && !editedInvoiceIds.Contains(i.Id));

        var hardGateCounts = new Dictionary<string, int>
        {
            ["arithmetic"] = windowInvoices.Count(i => HasFlag(i.OcrFlags, OcrFlag.AmountMismatch) || HasFlag(i.OcrFlags, OcrFlag.TotalsOutOfRange)),
            ["duplicate"] = windowInvoices.Count(i => string.Equals(i.Status, "DUPLICATE_PENDING", StringComparison.OrdinalIgnoreCase)),
            ["supplier"] = windowInvoices.Count(i => string.Equals(i.Status, "PENDING_SUPPLIER", StringComparison.OrdinalIgnoreCase)),
            ["date"] = windowInvoices.Count(i => HasFlag(i.OcrFlags, OcrFlag.StaleDate) || HasFlag(i.OcrFlags, OcrFlag.FutureDate))
        };

        var unresolved = await _reviewQueueAgingService.GetUnresolvedLongerThanThresholdAsync(ct: ct);

        var confidenceBound = await TryGetRollingSilentErrorConfidenceBoundAsync(context, ct);

        return new WeeklySummary
        {
            WeekStart = start,
            WeekEnd = end.AddDays(-1),
            ProcessedCount = processedCount,
            AutoAcceptedCount = autoAcceptedCount,
            AutoAcceptedPercent = processedCount == 0 ? 0 : 100.0 * autoAcceptedCount / processedCount,
            HardGateTriggerCounts = hardGateCounts,
            UnresolvedLongerThanThresholdCount = unresolved.Count,
            EditedInvoiceCount = editedInvoiceIds.Count,
            FieldCorrectionRate = processedCount == 0 ? 0 : (double)editedInvoiceIds.Count / processedCount,
            RollingSilentErrorConfidenceBoundPercent = confidenceBound
        };
    }

    private static bool HasFlag(string? ocrFlagsJson, string flag) =>
        !string.IsNullOrEmpty(ocrFlagsJson) && ocrFlagsJson.Contains(flag, StringComparison.Ordinal);

    private static DateTime MostRecentMonday(DateTime date)
    {
        var offset = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-offset);
    }

    /// <summary>Number 5 (PLAN-ETAPAS4.md §5/§3): expense_audit_samples is proposed, owner-DDL
    /// (no agent may create it, AGENTS.md). Returns null — never a fabricated number — until the
    /// table exists and has at least one row.</summary>
    private async Task<double?> TryGetRollingSilentErrorConfidenceBoundAsync(NordicBeesERPContext context, CancellationToken ct)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddMonths(-12);
            var counts = await context.Database.SqlQueryRaw<SampleCounts>(
                "SELECT COUNT(*) AS Total, SUM(CASE WHEN is_silent_error = 1 THEN 1 ELSE 0 END) AS SilentErrors " +
                "FROM expense_audit_samples WHERE checked_at >= {0}", cutoff)
                .FirstOrDefaultAsync(ct);

            if (counts is null || counts.Total <= 0) return null;

            // Rule of three (RESEARCH §7): 0 errors in n samples -> upper bound 3/n; with k errors,
            // the conservative bound this project uses is (k+3)/n, kept simple and always safe.
            var errors = counts.SilentErrors ?? 0;
            return 100.0 * (errors + 3) / counts.Total;
        }
        catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.NoSuchTable)
        {
            _logger.LogInformation("expense_audit_samples does not exist yet (owner DDL pending) — number 5 reported as null");
            return null;
        }
    }

    private sealed class SampleCounts
    {
        public int Total { get; set; }
        public int? SilentErrors { get; set; }
    }
}
