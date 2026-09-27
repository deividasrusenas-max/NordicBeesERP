using System.Text.Json;

namespace NordicBeesERP.Services.Validation;

public enum TableRepairOutcome
{
    /// <summary>No usable table, no tables at all, or the existing (Items-derived) lines already reconcile — a
    /// correct extraction is never touched.</summary>
    NotAttempted,
    /// <summary>At least one candidate line table was found, but none of its rows reconciled against the header —
    /// nothing repaired (never prefer a non-reconciling table).</summary>
    NotReconciling,
    /// <summary>A table's rows reconciled against the header where the Items-derived lines did not; the listed line
    /// indexes had their net (and recomputed gross) replaced with the table's values.</summary>
    Repaired
}

public sealed record TableLineRepairResult(TableRepairOutcome Outcome, IReadOnlyList<int> RepairedLineIndexes)
{
    public static readonly TableLineRepairResult None = new(TableRepairOutcome.NotAttempted, Array.Empty<int>());
}

/// <summary>
/// Etapas 3, S3 (PLAN-ETAPAS3.md §1 option (c), §8.1 S3): deterministic repair of a line's net amount from
/// <c>analyzeResult.tables[]</c> when Azure's <c>documents[0].fields.Items</c> mapped the wrong column — the exact
/// defect documented for ASF0021438 (gross column read as net) and the same class as EGO transport / UTA PL /
/// invoice 370 (D-023). Pure, no I/O — the same "read the raw response instead of a typed field" pattern
/// <see cref="SupplierCompanyCodeExtractor"/> already established for the supplier's registration code.
/// <para>
/// <b>Never invents a line, never replaces a value that already reconciles.</b> The table's net column is preferred
/// for ALL of a document's lines together, and only when: (1) the current Items-derived lines do NOT reconcile
/// against the header total (BR-CO-10, via <see cref="En16931TotalsValidator"/> — reused, not reimplemented), (2) a
/// table has a net-amount (or, failing that, a generic unlabelled-amount) column whose per-row values DO reconcile
/// against the same header total, and (3) that table's data-row count exactly matches the current line count — any
/// mismatch means "do not know how to align rows to lines" and the repair is skipped, never guessed.
/// </para>
/// <para>
/// <b>Known limitation (S1 finding, not fixed in this session):</b> a table with only a gross-amount column and no
/// net-amount column (EGO transport's exact shape) is deliberately never used as a net source — the real net value
/// there sits in a footer "Iš viso suma be PVM" label row, not a per-line column, and this class does not parse
/// footer labels. EGO-shaped documents are correctly left unrepaired rather than risk reading gross as net.
/// </para>
/// </summary>
public static class TableLineRepair
{
    private const string Net = "net_amount";
    private const string Gross = "gross_amount";
    private const string Generic = "amount_generic";
    private const string Quantity = "quantity";
    private const string UnitPrice = "unit_price";
    private const string Vat = "vat";

    // Header vocabulary, LT/DE/RO/LV/EE/PL per PLAN-ETAPAS3.md §8.1 S1, plus EN — added after Etapas 3 S1 found real
    // corpus documents (freight/logistics suppliers) using English column headers ("Net Amount", "Sum gross"),
    // which the plan's original list did not anticipate (.opencode/reports/etapas3-s1-20260927-1220.md).
    private static readonly Dictionary<string, string[]> Vocabulary = new()
    {
        [Quantity] = new[] { "kiekis", "quantity", "qty", "menge", "anzahl", "cantitate", "daudzums", "kogus", "ilość", "ilosc", "units" },
        [UnitPrice] = new[] { "vieneto kaina", "unit price", "kaina", "price", "einzelpreis", "preis", "pret unitar", "vienas cena", "ühikuhind", "uhikuhind", "cena jednostkowa" },
        [Net] = new[] { "suma be pvm", "net amount", "net value", "netto", "suma neto", "valoare neta", "summa bez pvn", "netosumma", "kwota netto", "neto" },
        [Gross] = new[] { "suma su pvm", "gross amount", "sum gross", "brutto", "suma totala", "summa ar pvn", "brutosumma", "kwota brutto", "bruto" },
        // Bare "amount"-shaped headers with no explicit net/gross qualifier (real corpus finding, S1): used as a
        // NET-column fallback only when the table has neither an explicit net nor a gross column, so it can never
        // be confused with an explicit gross column that happens to sit next to it.
        [Generic] = new[] { "suma", "viso", "total", "amount", "summa", "sum" },
        [Vat] = new[] { "pvm", "vat", "mwst", "ust", "tva", "pvn", "käibemaks", "kaibemaks", "podatek" }
    };

    private static readonly string[] SupportCategories = { Quantity, UnitPrice, Vat };

    // A trailing grand-total row that repeats the same figures as the data row(s) above it (S1 finding: the UTA PL
    // corpus document's "Łączna wartość / Bendra suma" row, which doubled the net sum when counted as a second line).
    private static readonly string[] TotalRowLabels =
        { "łączna", "laczna", "iš viso", "is viso", "total", "gesamt", "summe", "razem", "insgesamt" };

    public static TableLineRepairResult Repair(JsonElement analyzeResult, OcrResultDto result)
    {
        if (result.Lines.Count == 0) return TableLineRepairResult.None;
        if (!TryGetTables(analyzeResult, out var tables)) return TableLineRepairResult.None;

        var headerNet = result.AmountExclVat;
        var currentNets = result.Lines.Select(l => (decimal?)l.AmountExclVat).ToList();
        if (Reconciles(headerNet, currentNets)) return TableLineRepairResult.None; // already correct — never override

        var sawAnyCandidate = false;
        foreach (var table in tables.EnumerateArray())
        {
            if (!TryReadNetColumnRows(table, out var rows)) continue;
            sawAnyCandidate = true;
            if (rows.Count != result.Lines.Count) continue; // cannot align rows to lines — never guess
            var candidateNets = rows.Select(r => (decimal?)r.Value).ToList();
            if (!Reconciles(headerNet, candidateNets)) continue;

            var repaired = new List<int>();
            for (var i = 0; i < result.Lines.Count; i++)
            {
                var line = result.Lines[i];
                if (line.AmountExclVat == rows[i].Value) continue; // already correct for this line
                line.AmountExclVat = rows[i].Value;
                line.AmountInclVat = line.VatRate > 0
                    ? Math.Round(rows[i].Value * (1 + line.VatRate / 100m), 2)
                    : rows[i].Value;
                // Replace the printed-text read too: it pointed at the wrong column's text before the repair, and
                // NUMBER_MISREAD's own comparison (ExpenseService.RecomputeNumberReadFlags, run right after this)
                // must judge the corrected value against the TABLE's printed text, not the discarded one — matching
                // the plan's own requirement that the locale-number check runs against the corrected value.
                line.AmountRead = new OcrNumberRead(rows[i].Printed, rows[i].Value, rows[i].Value);
                repaired.Add(i);
            }
            if (repaired.Count > 0)
                return new TableLineRepairResult(TableRepairOutcome.Repaired, repaired);
        }

        return sawAnyCandidate ? new TableLineRepairResult(TableRepairOutcome.NotReconciling, Array.Empty<int>()) : TableLineRepairResult.None;
    }

    private static bool Reconciles(decimal headerNet, IReadOnlyList<decimal?> lineNets)
    {
        if (lineNets.Count == 0 || lineNets.Any(v => v is null)) return false;
        var validation = En16931TotalsValidator.Validate(new En16931TotalsInput
        {
            SumOfLineNet = headerNet,
            LineNetAmounts = lineNets
        });
        return validation.PassedRules.Contains(En16931TotalsValidator.BrCo10);
    }

    /// <summary>
    /// This table's per-data-row net value, in row order, or false when this table is not a usable line table at
    /// all (no columnHeader cells, no amount-like column with quantity/unit-price/VAT support, or a data row with no
    /// parseable value in the chosen column — never guessed).
    /// </summary>
    private static bool TryReadNetColumnRows(JsonElement table, out List<(decimal Value, string Printed)> rowNets)
    {
        rowNets = new List<(decimal, string)>();
        if (!table.TryGetProperty("cells", out var cellsEl) || cellsEl.ValueKind != JsonValueKind.Array) return false;
        var cells = cellsEl.EnumerateArray().ToList();

        var headerCells = cells.Where(c => Str(c, "kind") == "columnHeader").ToList();
        if (headerCells.Count == 0) return false;

        var columnCategories = new Dictionary<int, HashSet<string>>();
        foreach (var hc in headerCells)
        {
            var col = Int(hc, "columnIndex");
            var text = (Str(hc, "content") ?? "").ToLowerInvariant();
            foreach (var (category, words) in Vocabulary)
            {
                if (!words.Any(w => text.Contains(w))) continue;
                if (!columnCategories.TryGetValue(col, out var set)) columnCategories[col] = set = new HashSet<string>();
                set.Add(category);
            }
        }

        var hasSupport = columnCategories.Values.Any(cats => cats.Overlaps(SupportCategories));
        if (!hasSupport) return false;

        var netColumns = columnCategories.Where(kv => kv.Value.Contains(Net)).Select(kv => kv.Key).ToList();
        var grossColumns = columnCategories.Where(kv => kv.Value.Contains(Gross)).Select(kv => kv.Key).ToList();
        List<int> amountColumns;
        if (netColumns.Count > 0) amountColumns = netColumns;
        else if (grossColumns.Count == 0) amountColumns = columnCategories.Where(kv => kv.Value.Contains(Generic)).Select(kv => kv.Key).ToList();
        else return false; // only a gross column exists — never treat gross as net (see class doc, EGO limitation)
        if (amountColumns.Count == 0) return false;

        var byPosition = cells.ToLookup(c => (Int(c, "rowIndex"), Int(c, "columnIndex")));
        var headerRow = headerCells.Min(hc => Int(hc, "rowIndex"));
        var dataRows = cells.Select(c => Int(c, "rowIndex")).Distinct().Where(r => r > headerRow).OrderBy(r => r).ToList();

        foreach (var row in dataRows)
        {
            var rowText = string.Join(" ", cells.Where(c => Int(c, "rowIndex") == row).Select(c => (Str(c, "content") ?? "").ToLowerInvariant()));
            if (TotalRowLabels.Any(l => rowText.Contains(l))) continue; // grand-total row (S1 finding), not a line

            decimal? value = null;
            string? printed = null;
            foreach (var col in amountColumns)
            {
                var cell = byPosition[(row, col)].FirstOrDefault();
                if (cell.ValueKind == JsonValueKind.Undefined) continue;
                printed = Str(cell, "content");
                if (ParseAmount(printed) is { } parsed) { value = parsed; break; }
            }
            if (value is null) return false; // a data row with nothing parseable in the amount column — do not guess
            rowNets.Add((value.Value, printed ?? ""));
        }

        return rowNets.Count > 0;
    }

    /// <summary>European-first decimal parse ("1.234,56" / "1234,56" / "1234.56"), matching the S1 script's rule.</summary>
    private static decimal? ParseAmount(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var cleaned = raw.Trim().Replace(" ", "").Replace(" ", "");
        var candidates = new List<string>();
        if (cleaned.Contains(',') && cleaned.Contains('.'))
            candidates.Add(cleaned.Replace(".", "").Replace(",", "."));
        else if (cleaned.Contains(','))
            candidates.Add(cleaned.Replace(",", "."));
        candidates.Add(cleaned);

        foreach (var candidate in candidates)
            if (decimal.TryParse(candidate, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var value))
                return value;
        return null;
    }

    private static bool TryGetTables(JsonElement analyzeResult, out JsonElement tables) =>
        (analyzeResult.TryGetProperty("tables", out tables) || analyzeResult.TryGetProperty("Tables", out tables))
        && tables.ValueKind == JsonValueKind.Array && tables.GetArrayLength() > 0;

    private static string? Str(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int Int(JsonElement element, string property) => element.GetProperty(property).GetInt32();
}
