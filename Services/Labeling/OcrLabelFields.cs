namespace NordicBeesERP.Services.Labeling;

/// <summary>
/// Field-name constants and the money-field classification for the Etapas 4 criterion-3/5
/// labelling CSV (PLAN-ETAPAS4.md §1). Money fields are exactly the ones D-031 criterion 3 and
/// RESEARCH §7's "silent error" definition care about — not every labelled field (a wrong
/// description or a wrong non-money identifier is still worth labelling, just not counted here).
/// </summary>
public static class OcrLabelFields
{
    public const string InvoiceNumber = "invoice_number";
    public const string InvoiceDate = "invoice_date";
    public const string DueDate = "due_date";
    public const string AmountExclVat = "amount_excl_vat";
    public const string VatRate = "vat_rate";
    public const string VatAmount = "vat_amount";
    public const string AmountInclVat = "amount_incl_vat";
    public const string SupplierName = "supplier_name";
    public const string SupplierVatCode = "supplier_vat_code";

    private static readonly HashSet<string> MoneyHeaderFields = new(StringComparer.OrdinalIgnoreCase)
    {
        AmountExclVat, VatRate, VatAmount, AmountInclVat
    };

    private static readonly HashSet<string> MoneyLineSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "amount_excl_vat", "vat_rate", "amount_incl_vat"
    };

    public static string LineField(int lineNumber, string suffix) => $"line_{lineNumber}_{suffix}";

    /// <summary>True for the header/line fields that count toward D-031 criterion 3's "0 silent
    /// errors in money fields" — false for identity/description fields, which are still labelled
    /// but tracked separately (PLAN-ETAPAS4.md §1).</summary>
    public static bool IsMoneyField(string field)
    {
        if (string.IsNullOrEmpty(field)) return false;

        if (MoneyHeaderFields.Contains(field)) return true;

        if (field.StartsWith("line_", StringComparison.OrdinalIgnoreCase))
        {
            var parts = field.Split('_', 3);
            if (parts.Length == 3 && MoneyLineSuffixes.Contains(parts[2]))
                return true;
        }

        return false;
    }
}
