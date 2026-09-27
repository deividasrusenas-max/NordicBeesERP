namespace NordicBeesERP.Services;

/// <summary>
/// RESEARCH-2026-09-25-reliability.md §7's "five numbers" (PLAN-ETAPAS4.md §5) — the one
/// recurring measure meant to make module health visible without anyone going looking for it.
/// </summary>
public interface IWeeklySummaryService
{
    /// <summary>weekStart defaults to the most recent Monday (inclusive) through today.</summary>
    Task<WeeklySummary> GetSummaryAsync(DateTime? weekStart = null, CancellationToken ct = default);
}

public sealed class WeeklySummary
{
    public DateTime WeekStart { get; init; }
    public DateTime WeekEnd { get; init; }

    /// <summary>Number 1: invoices created in the window, and how many reached a confirmed
    /// status with zero human edits.</summary>
    public int ProcessedCount { get; init; }
    public int AutoAcceptedCount { get; init; }
    public double AutoAcceptedPercent { get; init; }

    /// <summary>Number 2: hard-gate trigger counts this week, by gate name.</summary>
    public IReadOnlyDictionary<string, int> HardGateTriggerCounts { get; init; } = new Dictionary<string, int>();

    /// <summary>Number 3: exactly C2's review-queue aging count — a queue-health snapshot as of
    /// now, not scoped to the week (D-031 criterion 6 is not a weekly-volume metric).</summary>
    public int UnresolvedLongerThanThresholdCount { get; init; }

    /// <summary>Number 4: invoice-level (not field-level) correction rate — see
    /// PLAN-ETAPAS4.md §5's note on why this is an approximation.</summary>
    public int EditedInvoiceCount { get; init; }
    public double FieldCorrectionRate { get; init; }

    /// <summary>Number 5: null until expense_audit_samples (owner DDL, PLAN-ETAPAS4.md §3) exists
    /// and has data — there is no fabricated placeholder number.</summary>
    public double? RollingSilentErrorConfidenceBoundPercent { get; init; }
}
