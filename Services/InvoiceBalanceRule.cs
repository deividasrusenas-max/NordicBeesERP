using NordicBeesERP.Models;

namespace NordicBeesERP.Services;

/// <summary>
/// THE receivable balance rule shown on every screen (Invoices list, InvoiceView, /payments cards and dialog, aging, /payments list):
///   remaining = total_incl_vat − Σ allocations (invoices.paid_amount) − credited (<see cref="CreditNoteDebtRule"/>)
///   open      = status NOT IN (draft, cancelled, disputed) AND remaining &gt; 0.005
///   due       = due_date ?? payment_due_date
///   overdue   = open AND due &lt; AppClock.TodayLt
/// Receivables only; payables (ULAK / supplier balance) are a separate concern.
/// </summary>
public static class InvoiceBalanceRule
{
    public const decimal OpenThreshold = 0.005m;

    public static decimal Remaining(decimal totalInclVat, decimal allocated, decimal credited) =>
        totalInclVat - allocated - credited;

    /// <summary>Statuses that never count as debt.</summary>
    public static bool CountsAsDebt(InvoiceStatus status) =>
        status is not (InvoiceStatus.Draft or InvoiceStatus.Cancelled or InvoiceStatus.Disputed);

    public static bool IsOpen(InvoiceStatus status, decimal remaining) =>
        CountsAsDebt(status) && remaining > OpenThreshold;

    public static DateTime? DueDate(DateTime? dueDate, DateTime? paymentDueDate) => dueDate ?? paymentDueDate;

    public static bool IsOverdue(InvoiceStatus status, decimal remaining, DateTime? due, DateTime today) =>
        IsOpen(status, remaining) && due.HasValue && due.Value.Date < today.Date;

    /// <summary>Pro-rata net/VAT split of a remaining amount (same method for cards and aging buckets).</summary>
    public static (decimal ExclVat, decimal Vat) SplitRemaining(decimal remaining, decimal subtotalExclVat, decimal totalInclVat)
    {
        var net = totalInclVat > 0 ? remaining * subtotalExclVat / totalInclVat : 0m;
        return (net, remaining - net);
    }

    /// <summary>SQL fragment (invoices alias-free) mirroring <see cref="CountsAsDebt"/>: Draft=0, Cancelled=4, Disputed=6.</summary>
    public const string SqlCountsAsDebt = "status NOT IN (0, 4, 6)";
}
