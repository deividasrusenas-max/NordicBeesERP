namespace NordicBeesERP.Services;

/// <summary>
/// D-031 criterion 6 (PLAN-ETAPAS4.md §4): no invoice unresolved longer than N working days.
/// "Unresolved" means sitting in PENDING_SUPPLIER, NEEDS_REVIEW or DUPLICATE_PENDING — the three
/// statuses <see cref="Helpers.ExpenseStatusHelper.NeedsAttention"/> already treats as
/// attention-worthy. REJECTED is excluded (a human decision already made); confirmed statuses
/// are a different, already-existing metric (the dashboard's aging buckets).
/// </summary>
public interface IReviewQueueAgingService
{
    /// <summary>The configured threshold (app_settings key
    /// "expense_review_queue_aging_working_days"), defaulting to 5 when not set.</summary>
    Task<int> GetThresholdWorkingDaysAsync(CancellationToken ct = default);

    /// <summary>Every unresolved invoice, with how many working days it has been unresolved —
    /// regardless of the threshold, for display; use <see cref="AgedInvoice.WorkingDaysUnresolved"/>
    /// against the threshold to decide what counts as overdue.</summary>
    Task<IReadOnlyList<AgedInvoice>> GetUnresolvedInvoicesAsync(CancellationToken ct = default);

    /// <summary>Convenience: only the invoices past the configured (or given) threshold.</summary>
    Task<IReadOnlyList<AgedInvoice>> GetUnresolvedLongerThanThresholdAsync(int? overrideThresholdWorkingDays = null, CancellationToken ct = default);
}

public sealed class AgedInvoice
{
    public int InvoiceId { get; init; }
    public string InvoiceNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;

    /// <summary>When the invoice most recently entered its current status (from
    /// expense_invoice_audit), or its creation time if no such transition was ever recorded.</summary>
    public DateTime SinceStatusEnteredAt { get; init; }

    public int WorkingDaysUnresolved { get; init; }
}
