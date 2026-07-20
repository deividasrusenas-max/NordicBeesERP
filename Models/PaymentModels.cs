// =====================================================
// NORDIC BEES ERP - PAYMENT MODULE MODELS
// Framework: .NET 10
// ORM: Entity Framework Core
// =====================================================

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models
{
    // =====================================================
    // PAYMENT ALLOCATION - Links payments to invoices
    // Supports one payment covering multiple invoices
    // =====================================================

    [Table("payment_allocations")]
    public class PaymentAllocation
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("payment_id")]
        public int PaymentId { get; set; }

        [Required]
        [Column("invoice_id")]
        public int InvoiceId { get; set; }

        [Required]
        [Column("allocated_amount")]
        public decimal AllocatedAmount { get; set; }

        [Column("allocated_at")]
        public DateTime AllocatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("PaymentId")]
        public virtual Payment Payment { get; set; } = null!;

        [ForeignKey("InvoiceId")]
        public virtual Invoice Invoice { get; set; } = null!;
    }

    // =====================================================
    // BANK IMPORTS - Import session header
    // =====================================================

    [Table("bank_imports")]
    public class BankImport
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("import_date")]
        public DateTime ImportDate { get; set; } = DateTime.Now;

        [Required]
        [MaxLength(255)]
        [Column("file_name")]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(64)]
        [Column("file_hash")]
        public string FileHash { get; set; } = string.Empty;

        [Required]
        [Column("total_rows")]
        public int TotalRows { get; set; }

        [Required]
        [Column("matched_rows")]
        public int MatchedRows { get; set; }

        [Required]
        [Column("unmatched_rows")]
        public int UnmatchedRows { get; set; }

        [Required]
        [Column("processed_rows")]
        public int ProcessedRows { get; set; }

        [Column("status")]
        public string Status { get; set; } = "pending";

        [Column("error_message")]
        public string? ErrorMessage { get; set; }

        [Required]
        [Column("created_by")]
        public int CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation
         public virtual ICollection<BankImportRow> Rows { get; set; } = new List<BankImportRow>();
    }

    // =====================================================
    // BANK IMPORT ROWS - Individual bank statement rows
    // DB: SHOW CREATE TABLE bank_import_rows
    // =====================================================

    [Table("bank_import_rows")]
    public class BankImportRow
    {
        [Key][Column("id")] public int Id { get; set; }
        [Column("import_id")] public int ImportId { get; set; }
        [Column("row_date")] public DateTime RowDate { get; set; }
        [Column("payer_name")] public string? PayerName { get; set; }
        [Column("payer_account")] public string? PayerAccount { get; set; }
        [Column("amount")] public decimal Amount { get; set; }
        [Column("currency")] public string Currency { get; set; } = PdfLocalization.CurrencyCode;
        [Column("reference")] public string? Reference { get; set; }
        [Column("bank_ref")] public string? BankRef { get; set; }
        [Column("description")] public string? Description { get; set; }
        [Column("match_status")] public string MatchStatus { get; set; } = "unmatched";
        [Column("matched_invoice_id")] public int? MatchedInvoiceId { get; set; }
        [Column("payment_id")] public int? PaymentId { get; set; }
        [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("ImportId")]
        public virtual BankImport BankImport { get; set; } = null!;
    }

    // =====================================================
    // PAYMENT AUDIT LOG - Full audit trail
    // =====================================================

    [Table("payment_audit_log")]
    public class PaymentAuditLog
    {
        [Key][Column("id")] public int Id { get; set; }
        [Column("payment_id")] public int? PaymentId { get; set; }
        [Column("invoice_id")] public int? InvoiceId { get; set; }
        [Required][MaxLength(50)][Column("action")] public string Action { get; set; } = string.Empty;
        [Column("old_amount")] public decimal? OldAmount { get; set; }
        [Column("new_amount")] public decimal? NewAmount { get; set; }
        [Column("changed_by")] public int? ChangedBy { get; set; }
        [Column("changed_at")] public DateTime ChangedAt { get; set; } = DateTime.Now;
        [Column("notes")] public string? Notes { get; set; }
    }

    // =====================================================
    // PAYMENT HISTORY ITEM - For displaying payment history
    // NOTE: This was moved to Dtos/PaymentDtos.cs to avoid naming conflict
    // with the record type in this file. Use NordicBeesERP.Services.Dtos.PaymentHistoryItem
    // =====================================================
}