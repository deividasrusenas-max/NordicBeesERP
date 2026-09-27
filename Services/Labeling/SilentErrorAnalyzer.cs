namespace NordicBeesERP.Services.Labeling;

/// <summary>
/// Computes D-031 criterion 3/5's "silent error" count from a filled labelling CSV
/// (PLAN-ETAPAS4.md §1/§3): a money field the labeller marked wrong, on an invoice that was
/// never flagged for review — i.e. the pipeline would have let the wrong value through
/// unnoticed. A wrong field that WAS flagged (the gate did its job) is not a silent error; it is
/// still reported separately so the labelling round is not wasted information.
///
/// Pure — takes already-loaded rows and an already-computed "was this invoice flagged" set, so
/// it needs no database access and is fully testable with synthetic data.
/// </summary>
public static class SilentErrorAnalyzer
{
    public static SilentErrorReport Analyze(
        IReadOnlyList<OcrLabelRow> rows,
        IReadOnlySet<int> flaggedInvoiceIds,
        IReadOnlySet<int>? holdoutInvoiceIds = null)
    {
        var perField = new Dictionary<string, FieldStats>(StringComparer.OrdinalIgnoreCase);
        var silentErrorInvoiceIds = new HashSet<int>();
        var flaggedWrongCount = 0;
        var labelledCount = 0;
        var wrongCount = 0;

        foreach (var row in rows)
        {
            if (row.IsWrong is null) continue; // not labelled yet
            labelledCount++;

            if (!row.IsWrong.Value) continue;
            wrongCount++;

            if (!OcrLabelFields.IsMoneyField(row.Field)) continue;

            if (!perField.TryGetValue(row.Field, out var stats))
            {
                stats = new FieldStats();
                perField[row.Field] = stats;
            }
            stats.WrongCount++;

            var isFlagged = flaggedInvoiceIds.Contains(row.InvoiceId);
            if (isFlagged)
            {
                flaggedWrongCount++;
            }
            else
            {
                stats.SilentErrorCount++;
                silentErrorInvoiceIds.Add(row.InvoiceId);
            }
        }

        var holdoutSilentErrorCount = holdoutInvoiceIds is null
            ? (int?)null
            : silentErrorInvoiceIds.Count(id => holdoutInvoiceIds.Contains(id));

        var holdoutLabelledCount = holdoutInvoiceIds is null
            ? (int?)null
            : rows.Where(r => r.IsWrong is not null && holdoutInvoiceIds.Contains(r.InvoiceId))
                  .Select(r => r.InvoiceId)
                  .Distinct()
                  .Count();

        return new SilentErrorReport
        {
            LabelledFieldCount = labelledCount,
            WrongFieldCount = wrongCount,
            FlaggedWrongFieldCount = flaggedWrongCount,
            SilentErrorInvoiceIds = silentErrorInvoiceIds.OrderBy(x => x).ToList(),
            PerField = perField.ToDictionary(kv => kv.Key, kv => (kv.Value.WrongCount, kv.Value.SilentErrorCount)),
            HoldoutSilentErrorInvoiceCount = holdoutSilentErrorCount,
            HoldoutLabelledInvoiceCount = holdoutLabelledCount
        };
    }

    private sealed class FieldStats
    {
        public int WrongCount;
        public int SilentErrorCount;
    }
}

public sealed class SilentErrorReport
{
    public int LabelledFieldCount { get; init; }
    public int WrongFieldCount { get; init; }
    public int FlaggedWrongFieldCount { get; init; }
    public IReadOnlyList<int> SilentErrorInvoiceIds { get; init; } = Array.Empty<int>();
    public IReadOnlyDictionary<string, (int WrongCount, int SilentErrorCount)> PerField { get; init; }
        = new Dictionary<string, (int, int)>();

    /// <summary>Null when no hold-out set was supplied to Analyze.</summary>
    public int? HoldoutSilentErrorInvoiceCount { get; init; }
    public int? HoldoutLabelledInvoiceCount { get; init; }
}
