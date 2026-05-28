// =====================================================
// NORDIC BEES ERP - CREDIT NOTE MODELS
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
    // CREDIT NOTE STATUS ENUM
    // =====================================================

    public enum CreditNoteStatus
    {
        Draft = 0,     // Juodraštis
        Printed = 1,   // Atspausdinta
        Disputed = 2   // Ginčijama
    }

    // =====================================================
    // CREDIT NOTE - Credit notes for customer refunds
    // =====================================================

    [Table("credit_notes")]
    public class CreditNote
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        [Column("credit_note_number")]
        public string CreditNoteNumber { get; set; } = string.Empty;

        [Required]
        [Column("credit_date")]
        public DateTime CreditDate { get; set; }

        [Column("original_invoice_id")]
        public int? OriginalInvoiceId { get; set; }

        [Column("applied_invoice_id")]
        public int? AppliedInvoiceId { get; set; }

        [Required]
        [Column("customer_id")]
        public int CustomerId { get; set; }

        [Column("currency_id")]
        public int CurrencyId { get; set; }

        [MaxLength(10)]
        [Column("language")]
        public string Language { get; set; } = "LT";

        [Column("reverse_charge")]
        public bool ReverseCharge { get; set; } = false;

        [Column("subtotal_excl_vat")]
        public decimal SubtotalExclVat { get; set; }

        [Column("total_vat")]
        public decimal TotalVat { get; set; }

        [Column("total_incl_vat")]
        public decimal TotalInclVat { get; set; }

        [Column("status")]
        public CreditNoteStatus Status { get; set; } = CreditNoteStatus.Draft;

        [Column("pdf_path")]
        public string? PdfPath { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("issued_by")]
        public int? IssuedBy { get; set; }

        [Column("created_by")]
        public int CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("OriginalInvoiceId")]
        public virtual Invoice? OriginalInvoice { get; set; }

        [ForeignKey("AppliedInvoiceId")]
        public virtual Invoice? AppliedInvoice { get; set; }

        [ForeignKey("CustomerId")]
        public virtual BusinessPartner Customer { get; set; } = null!;
        [ForeignKey("CurrencyId")]
        public virtual Currency Currency { get; set; } = null!;
        public virtual ICollection<CreditNoteLine> Lines { get; set; } = new List<CreditNoteLine>();
    }

    // =====================================================
    // CREDIT NOTE LINE - Individual line items
    // =====================================================

    [Table("credit_note_lines")]
    public class CreditNoteLine
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("credit_note_id")]
        public int CreditNoteId { get; set; }

        [Column("invoice_line_id")]
        public int? InvoiceLineId { get; set; }

        [Column("line_number")]
        public int LineNumber { get; set; }

        [MaxLength(100)]
        [Column("product_code")]
        public string? ProductCode { get; set; }

        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [Column("quantity")]
        public decimal Quantity { get; set; }

        [MaxLength(50)]
        [Column("unit")]
        public string Unit { get; set; } = "vnt";

        [Column("price_excl_vat")]
        public decimal PriceExclVat { get; set; }

        [Column("vat_rate")]
        public decimal VatRate { get; set; }

        [Column("line_subtotal")]
        public decimal LineSubtotal { get; set; }

        [Column("vat_amount")]
        public decimal VatAmount { get; set; }

        [Column("line_total")]
        public decimal LineTotal { get; set; }

        [Column("lot_number")]
        public string? LotNumber { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("CreditNoteId")]
        public virtual CreditNote CreditNote { get; set; } = null!;
        [ForeignKey("InvoiceLineId")]
        public virtual InvoiceLine? InvoiceLine { get; set; }
    }
}