namespace NordicBeesERP.Services.Labeling;

/// <summary>
/// One row of the Etapas 4 criterion-3 labelling CSV (PLAN-ETAPAS4.md §1): one money/identity
/// field of one invoice, the value the pipeline extracted, and — filled in later by a human
/// against the PDF — whether it is wrong and what the correct value is.
/// </summary>
public sealed class OcrLabelRow
{
    public int InvoiceId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string ExtractedValue { get; set; } = string.Empty;
    public string PrintedContent { get; set; } = string.Empty;

    /// <summary>Blank (null) until a human fills the CSV; "true"/"false" once labelled.</summary>
    public bool? IsWrong { get; set; }

    /// <summary>Blank unless IsWrong is true.</summary>
    public string CorrectValue { get; set; } = string.Empty;
}
