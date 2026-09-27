namespace NordicBeesERP.Services.Labeling;

/// <summary>
/// Builds and writes the Etapas 4 criterion-3/5 labelling CSV from real invoices, and computes
/// the silent-error report from a filled-in copy (PLAN-ETAPAS4.md §1/§3). No public UI — an
/// admin-only page or a direct call is the intended caller (D-046 OQ-6).
/// </summary>
public interface IOcrLabelExportService
{
    /// <summary>Writes one row per money/identity field and per line, for exactly the given
    /// invoice ids, to <paramref name="outputCsvPath"/>. Returns the row count written.</summary>
    Task<int> ExportAsync(IReadOnlyCollection<int> invoiceIds, string outputCsvPath, CancellationToken ct = default);

    /// <summary>Draws a seeded dev/hold-out split from all invoices with a stored raw OCR
    /// response and exports both sets to <paramref name="outputCsvPath"/> in one CSV (the
    /// hold-out ids are not marked in the file — PLAN-ETAPAS4.md §1's blinding rule). Returns the
    /// chosen ids so the caller can record the split for later comparison.</summary>
    Task<(IReadOnlyList<int> DevIds, IReadOnlyList<int> HoldoutIds, int RowCount)> ExportRandomSampleAsync(
        int devCount, int holdoutCount, int seed, string outputCsvPath, CancellationToken ct = default);

    /// <summary>Reads a filled-in CSV, looks up each labelled invoice's current flagged status,
    /// and returns the silent-error report (PLAN-ETAPAS4.md §1/§3). <paramref name="holdoutInvoiceIds"/>
    /// is optional — pass the ids recorded from ExportRandomSampleAsync to get the D-031
    /// criterion-3 hold-out number specifically.</summary>
    Task<SilentErrorReport> CompareAsync(string filledCsvPath, IReadOnlySet<int>? holdoutInvoiceIds = null, CancellationToken ct = default);
}
