using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models;

/// <summary>"Tarpusavio užskaitos aktas": LAK sales invoices (they owe us) netted against expense invoices (we owe them), same partner.</summary>
[Table("settlement_offsets")]
public class SettlementOffset
{
    public const string StatusConfirmed = "confirmed";
    public const string StatusCancelled = "cancelled";

    [Key][Column("id")] public int Id { get; set; }
    [Required][MaxLength(30)][Column("offset_number")] public string OffsetNumber { get; set; } = string.Empty;
    [Column("offset_date")] public DateTime OffsetDate { get; set; }
    [Column("partner_id")] public int PartnerId { get; set; }
    [Required][MaxLength(3)][Column("currency")] public string Currency { get; set; } = "EUR";
    [Column("amount", TypeName = "decimal(12,2)")] public decimal Amount { get; set; }
    [Required][MaxLength(20)][Column("status")] public string Status { get; set; } = StatusConfirmed;
    [Column("notes")] public string? Notes { get; set; }
    [Column("created_by")] public int? CreatedBy { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("confirmed_by")] public int? ConfirmedBy { get; set; }
    [Column("confirmed_at")] public DateTime? ConfirmedAt { get; set; }
    [Column("cancelled_by")] public int? CancelledBy { get; set; }
    [Column("cancelled_at")] public DateTime? CancelledAt { get; set; }
    [Column("cancel_reason")] public string? CancelReason { get; set; }

    [NotMapped] public List<SettlementOffsetLine> Lines { get; set; } = new();
}

[Table("settlement_offset_lines")]
public class SettlementOffsetLine
{
    public const string SideReceivable = "receivable";
    public const string SidePayable = "payable";

    [Key][Column("id")] public int Id { get; set; }
    [Column("offset_id")] public int OffsetId { get; set; }
    [Required][MaxLength(20)][Column("side")] public string Side { get; set; } = SideReceivable;
    [Column("invoice_id")] public int? InvoiceId { get; set; }
    [Column("expense_invoice_id")] public int? ExpenseInvoiceId { get; set; }
    [Column("amount", TypeName = "decimal(12,2)")] public decimal Amount { get; set; }
    [Column("open_before", TypeName = "decimal(12,2)")] public decimal OpenBefore { get; set; }
    [Column("payment_id")] public int? PaymentId { get; set; }
    [Column("expense_payment_id")] public int? ExpensePaymentId { get; set; }
}
