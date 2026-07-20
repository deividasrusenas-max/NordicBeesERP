namespace NordicBeesERP.Services;

/// <summary>
/// Thrown when a bank import row matches an invoice that has existing manual payments
/// requiring human review (ambiguous amount match or amount mismatch).
/// </summary>
public class NeedsReviewException : InvalidOperationException
{
    public int BankImportRowId { get; }
    public int InvoiceId { get; }
    public int ManualPaymentCount { get; }
    public decimal? BankAmount { get; }
    public decimal? ManualAmount { get; }

    public NeedsReviewException(
        int bankImportRowId,
        int invoiceId,
        int manualPaymentCount,
        decimal? bankAmount = null,
        decimal? manualAmount = null,
        string? message = null)
        : base(message ?? $"Bank import row {bankImportRowId} for invoice {invoiceId} requires manual review ({manualPaymentCount} existing manual payment(s))")
    {
        BankImportRowId = bankImportRowId;
        InvoiceId = invoiceId;
        ManualPaymentCount = manualPaymentCount;
        BankAmount = bankAmount;
        ManualAmount = manualAmount;
    }
}
