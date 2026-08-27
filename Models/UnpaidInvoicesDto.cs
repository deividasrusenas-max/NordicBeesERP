namespace NordicBeesERP.Models;

/// <summary>
/// A single unpaid LAK sales invoice within the statement of unpaid invoices.
/// Amounts are in the invoice currency. RemainingAmount is computed
/// (TotalInclVat - PaidAmount - credited), NOT read from the payment_status column.
/// </summary>
public class UnpaidInvoiceLine
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal RemainingAmount { get; set; }
}

/// <summary>
/// Full result of the statement-of-unpaid-invoices report for one partner over a
/// period. Computed live; the service is read-only against the database.
/// </summary>
public class UnpaidInvoicesResult
{
    public string PartnerName { get; set; } = string.Empty;
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerAddress { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public List<UnpaidInvoiceLine> Lines { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public decimal TotalRemaining { get; set; }
}
