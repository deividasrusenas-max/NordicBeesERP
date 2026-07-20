// =====================================================
// NORDIC BEES ERP - EXPENSE MODULE
// Framework: .NET 10
// ORM: Entity Framework Core
// =====================================================

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models.Expenses
{
    // =====================================================
    // INVOICE ADD RESULT (for duplicate detection)
    // =====================================================

    public class InvoiceAddResult
    {
        public bool IsDuplicate { get; set; }
        public int OriginalInvoiceId { get; set; }
        public int ThisInvoiceId { get; set; }
    }

    // =====================================================
    // INVOICE AUDIT LOG
    // =====================================================

    [Table("expense_invoice_audit")]
    public class ExpenseInvoiceAudit
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("invoice_id")]
        public int InvoiceId { get; set; }

        [Column("invoice_number")]
        public string? InvoiceNumber { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("action")]
        public string Action { get; set; } = string.Empty;

        [Column("action_details")]
        public string? ActionDetails { get; set; }

        [MaxLength(30)]
        [Column("old_status")]
        public string? OldStatus { get; set; }

        [MaxLength(30)]
        [Column("new_status")]
        public string? NewStatus { get; set; }

        [MaxLength(100)]
        [Column("performed_by")]
        public string? PerformedBy { get; set; }

        [Column("performed_at")]
        public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
    }

    // =====================================================
    // IŠLAIDŲ PASKIRSTYMAS (EXPENSE LINE ALLOCATION)
    // =====================================================

    [Table("expense_line_allocations")]
    public class ExpenseLineAllocation
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("invoice_line_id")]
        public int InvoiceLineId { get; set; }

        [Required]
        [Column("category_id")]
        public int CategoryId { get; set; }

        [Required]
        [Column("cost_center_id")]
        public int CostCenterId { get; set; }

        [Required]
        [Column("allocated_amount", TypeName = "decimal(12,2)")]
        public decimal AllocatedAmount { get; set; }

        [Column("allocated_percent", TypeName = "decimal(5,2)")]
        public decimal AllocatedPercent { get; set; }

        // Navigation
        [NotMapped]
        public virtual ExpenseInvoiceLine? InvoiceLine { get; set; }
        [NotMapped]
        public virtual NordicBeesERP.Models.Expenses.ExpenseCategory? Category { get; set; }
        [NotMapped]
        public virtual NordicBeesERP.Models.Expenses.ExpenseCostCenter? CostCenter { get; set; }
    }

    // =====================================================
    // IŠLAIDŲ MOKĖJIMAS (EXPENSE PAYMENT)
    // =====================================================

    [Table("expense_payments")]
    public class ExpensePayment
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("invoice_id")]
        public int InvoiceId { get; set; }

        [Required]
        [Column("payment_date")]
        public DateTime PaymentDate { get; set; }

        [Required]
        [Column("amount", TypeName = "decimal(12,2)")]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("payment_method")]
        public string PaymentMethod { get; set; } = string.Empty;

        [MaxLength(100)]
        [Column("reference")]
        public string? Reference { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [MaxLength(20)]
        [Column("source")]
        public string Source { get; set; } = "manual";

        [Column("bank_confirmed")]
        public bool BankConfirmed { get; set; } = false;

        [Column("bank_import_row_id")]
        public int? BankImportRowId { get; set; }

        [Column("bank_import_id")]
        public int? BankImportId { get; set; }

        // Navigation
        [NotMapped]
        public virtual ExpenseInvoice? Invoice { get; set; }
    }

    // =====================================================
    // IŠLAIDŲ BIUDŽETAS (EXPENSE BUDGET)
    // =====================================================

    [Table("expense_budgets")]
    public class ExpenseBudget
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("category_id")]
        public int CategoryId { get; set; }

        [Required]
        [Column("year")]
        public int Year { get; set; }

        [Required]
        [Column("month")]
        public int Month { get; set; }

        [Required]
        [Column("planned_amount", TypeName = "decimal(12,2)")]
        public decimal PlannedAmount { get; set; }

        // Navigation
        [NotMapped]
        public virtual ExpenseCategory? Category { get; set; }
    }

    // =====================================================
    // OCR EILUTĖ (EXPENSE OCR QUEUE)
    // =====================================================

    [Table("expense_ocr_queue")]
    public class ExpenseOcrQueue
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("invoice_id")]
        public int? InvoiceId { get; set; }

        [Column("file_content")]
        public string? FileContent { get; set; }

        [MaxLength(255)]
        [Column("file_name")]
        public string? FileName { get; set; }

        [Column("attempts")]
        public int Attempts { get; set; } = 0;

        [Column("max_attempts")]
        public int MaxAttempts { get; set; } = 3;

        [Required]
        [MaxLength(20)]
        [Column("status")]
        public string Status { get; set; } = "WAITING";

        [Column("error_message")]
        public string? ErrorMessage { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("processed_at")]
        public DateTime? ProcessedAt { get; set; }
    }

    // =====================================================
    // SISTEMOS NUSTATYMAI (APP SETTINGS)
    // =====================================================

    [Table("app_settings")]
    public class AppSetting
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        [Column("setting_key")]
        public string SettingKey { get; set; } = string.Empty;

        [Column("setting_value")]
        public string? SettingValue { get; set; }
    }
}