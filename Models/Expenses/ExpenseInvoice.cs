// =====================================================
// NORDIC BEES ERP - EXPENSE MODULE
// Framework: .NET 10
// ORM: Entity Framework Core
// =====================================================

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NordicBeesERP.Models;

namespace NordicBeesERP.Models.Expenses
{
    // =====================================================
    // IŠLAIDŲ SĄSKAITA FAKTŪRA (EXPENSE INVOICE)
    // =====================================================

    [Table("expense_invoices")]
    public class ExpenseInvoice
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("supplier_id")]
        public int? SupplierId { get; set; }

        [Column("invoice_type")]
        [MaxLength(20)]
        public string InvoiceType { get; set; } = "STANDARD";

        [Column("pending_supplier_name")]
        public string? PendingSupplierName { get; set; }

        [Column("pending_supplier_vat")]
        public string? PendingSupplierVat { get; set; }

        [Column("pending_supplier_address")]
        public string? PendingSupplierAddress { get; set; }

        [Column("pending_supplier_company_code")]
        public string? PendingSupplierCompanyCode { get; set; }

        [Column("pending_supplier_bank_account")]
        public string? PendingSupplierBankAccount { get; set; }

        [Column("pending_supplier_city")]
        public string? PendingSupplierCity { get; set; }

        [Column("pending_supplier_postal_code")]
        public string? PendingSupplierPostalCode { get; set; }

        [Column("pending_supplier_country_code")]
        public string? PendingSupplierCountryCode { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("invoice_number")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required]
        [Column("invoice_date")]
        public DateTime InvoiceDate { get; set; }

        [Required]
        [Column("due_date")]
        public DateTime DueDate { get; set; }

        [Required]
        [Column("amount_excl_vat", TypeName = "decimal(12,2)")]
        public decimal AmountExclVat { get; set; }

        [Column("vat_rate", TypeName = "decimal(5,2)")]
        public decimal VatRate { get; set; } = 21.00m;

        [Required]
        [Column("vat_amount", TypeName = "decimal(12,2)")]
        public decimal VatAmount { get; set; }

        [Required]
        [Column("amount_incl_vat", TypeName = "decimal(12,2)")]
        public decimal AmountInclVat { get; set; }

        [Column("currency")]
        [MaxLength(3)]
        public string Currency { get; set; } = PdfLocalization.CurrencyCode;

        [Required]
        [Column("paid_amount", TypeName = "decimal(12,2)")]
        public decimal PaidAmount { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("status")]
        public string Status { get; set; } = "DRAFT";

        [Required]
        [MaxLength(20)]
        [Column("ocr_status")]
        public string OcrStatus { get; set; } = "PENDING";

        [Column("ocr_confidence")]
        public int? OcrConfidence { get; set; }

        [Column("ocr_raw_json")]
        public string? OcrRawJson { get; set; }

        [Column("ocr_flags")]
        public string? OcrFlags { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("original_file_path")]
        public string? OriginalFilePath { get; set; }

        [Column("supplier_vat_verified")]
        public bool SupplierVatVerified { get; set; }

        [Column("supplier_vat_verified_name")]
        public string? SupplierVatVerifiedName { get; set; }

        [Column("category_id")]
        public int? CategoryId { get; set; }

        [Column("approved_by")]
        [MaxLength(100)]
        public string? ApprovedBy { get; set; }

        [Column("approved_at")]
        public DateTime? ApprovedAt { get; set; }

        [Column("rejected_reason")]
        [MaxLength(500)]
        public string? RejectedReason { get; set; }

        [Column("source")]
        [MaxLength(10)]
        public string Source { get; set; } = "MANUAL";

        [Column("original_filename")]
        [MaxLength(255)]
        public string? OriginalFilename { get; set; }

        [Column("ocr_pipeline")]
        [MaxLength(50)]
        public string? OcrPipeline { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        // Duplicate detection (not mapped - used for UI display only)
        [NotMapped]
        public int? DuplicateOfId { get; set; }

        // NotMapped properties (populated via join in ExpenseService)
        [NotMapped]
        public string? SupplierName { get; set; }

        // Navigation
        [NotMapped] 
        public virtual Supplier? Supplier { get; set; }
        [NotMapped]
        public virtual ICollection<ExpenseInvoiceLine> ExpenseInvoiceLines { get; set; } = new List<ExpenseInvoiceLine>();
        [NotMapped]
        public virtual ICollection<ExpensePayment> ExpensePayments { get; set; } = new List<ExpensePayment>();
        [NotMapped]
        public virtual ExpenseInvoice? DuplicateOf { get; set; }
    }

    // =====================================================
    // IŠLAIDŲ SĄSKAITOS EILUTĖ (EXPENSE INVOICE LINE)
    // =====================================================

    [Table("expense_invoice_lines")]
    public class ExpenseInvoiceLine
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("invoice_id")]
        public int InvoiceId { get; set; }

        [Required]
        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Column("amount_excl_vat", TypeName = "decimal(12,2)")]
        public decimal AmountExclVat { get; set; }

        [Required]
        [Column("vat_rate", TypeName = "decimal(5,2)")]
        public decimal VatRate { get; set; }

        [Required]
        [Column("amount_incl_vat", TypeName = "decimal(12,2)")]
        public decimal AmountInclVat { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;

        [Column("quantity", TypeName = "decimal(10,3)")]
        public decimal? Quantity { get; set; }

        [Column("unit_price", TypeName = "decimal(12,2)")]
        public decimal? UnitPrice { get; set; }

        [Column("category_id")]
        public int? CategoryId { get; set; }

        [Column("unit_of_measure")]
        [MaxLength(20)]
        public string? UnitOfMeasure { get; set; }

        // Navigation
        [NotMapped]
        public virtual ExpenseInvoice? Invoice { get; set; }
        [NotMapped]
        public virtual ICollection<ExpenseLineAllocation> ExpenseLineAllocations { get; set; } = new List<ExpenseLineAllocation>();
    }
}