namespace NordicBeesERP.Models;

/// <summary>
/// Language selection for the debt reconciliation statement rendering (PDF/XLSX).
/// Data itself is language-independent; only labels/headings change.
/// </summary>
public enum ReportLanguage
{
    LT,
    EN
}

/// <summary>
/// Where a ledger line originated.
/// </summary>
public enum ReconciliationSourceType
{
    Invoice,
    CreditNote,
    Payment
}

/// <summary>
/// A single ledger line in the debt reconciliation statement.
/// </summary>
public class DebtReconciliationLine
{
    public string DocumentNumber { get; set; } = string.Empty;
    public DateTime DocumentDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }
    public ReconciliationSourceType SourceType { get; set; }
}

/// <summary>
/// Full result of a per-partner debt reconciliation for a period.
/// Computed live (no stored ledger). All amounts in the partner's invoice currency context.
/// </summary>
public class DebtReconciliationResult
{
    public string PartnerName { get; set; } = string.Empty;
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerAddress { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal OpeningBalance { get; set; }
    public List<DebtReconciliationLine> Lines { get; set; } = new();
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal ClosingBalance { get; set; }
}
