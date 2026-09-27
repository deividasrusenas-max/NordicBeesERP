using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Helpers;

namespace NordicBeesERP.Services;

/// <inheritdoc cref="IReviewQueueAgingService"/>
public sealed class ReviewQueueAgingService : IReviewQueueAgingService
{
    private const string ThresholdSettingKey = "expense_review_queue_aging_working_days";
    private const int DefaultThresholdWorkingDays = 5;

    private static readonly HashSet<string> UnresolvedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "PENDING_SUPPLIER", "NEEDS_REVIEW", "DUPLICATE_PENDING"
    };

    private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;

    public ReviewQueueAgingService(IDbContextFactory<NordicBeesERPContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<int> GetThresholdWorkingDaysAsync(CancellationToken ct = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(ct);

        var raw = await context.AppSettings
            .Where(s => s.SettingKey == ThresholdSettingKey)
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync(ct);

        return int.TryParse(raw, out var value) && value > 0 ? value : DefaultThresholdWorkingDays;
    }

    public async Task<IReadOnlyList<AgedInvoice>> GetUnresolvedInvoicesAsync(CancellationToken ct = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;

        var unresolved = await context.ExpenseInvoices
            .Where(i => UnresolvedStatuses.Contains(i.Status))
            .Select(i => new { i.Id, i.InvoiceNumber, i.Status, i.CreatedAt })
            .ToListAsync(ct);

        if (unresolved.Count == 0) return Array.Empty<AgedInvoice>();

        var invoiceIds = unresolved.Select(i => i.Id).ToList();

        // The most recent transition INTO each invoice's current status — the "since when" clock
        // (PLAN-ETAPAS4.md §4). An invoice can cycle back into an unresolved status (re-OCR,
        // "change supplier"), so this is not simply the first-ever audit row.
        var transitions = await context.ExpenseInvoiceAudits
            .Where(a => invoiceIds.Contains(a.InvoiceId) && a.NewStatus != null)
            .Select(a => new { a.InvoiceId, a.NewStatus, a.PerformedAt })
            .ToListAsync(ct);

        var latestTransitionByInvoiceAndStatus = transitions
            .GroupBy(t => (t.InvoiceId, Status: t.NewStatus!))
            .ToDictionary(g => g.Key, g => g.Max(t => t.PerformedAt));

        var result = new List<AgedInvoice>(unresolved.Count);
        foreach (var invoice in unresolved)
        {
            var since = latestTransitionByInvoiceAndStatus.TryGetValue((invoice.Id, invoice.Status), out var enteredAt)
                ? enteredAt
                : invoice.CreatedAt;

            result.Add(new AgedInvoice
            {
                InvoiceId = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                Status = invoice.Status,
                SinceStatusEnteredAt = since,
                WorkingDaysUnresolved = LithuanianWorkingDayCalculator.WorkingDaysElapsed(since, now)
            });
        }

        return result.OrderByDescending(a => a.WorkingDaysUnresolved).ToList();
    }

    public async Task<IReadOnlyList<AgedInvoice>> GetUnresolvedLongerThanThresholdAsync(int? overrideThresholdWorkingDays = null, CancellationToken ct = default)
    {
        var threshold = overrideThresholdWorkingDays ?? await GetThresholdWorkingDaysAsync(ct);
        var all = await GetUnresolvedInvoicesAsync(ct);
        return all.Where(a => a.WorkingDaysUnresolved > threshold).ToList();
    }
}
