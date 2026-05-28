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
    // EXPENSE UPLOAD DATA (DTO for dialog)
    // =====================================================

    public class ExpenseUploadData
    {
        public int Id { get; set; }
        
        // Duplicate handling
        public bool IsDuplicate { get; set; }
        public int? ExistingInvoiceId { get; set; }
        public int? NewInvoiceId { get; set; }
        
        [Required]
        public int SupplierId { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string InvoiceNumber { get; set; } = string.Empty;
        
        [Required]
        public DateTime InvoiceDate { get; set; }
        
        [Required]
        public DateTime DueDate { get; set; }
        
        [Required]
        public decimal AmountExclVat { get; set; }
        
        [Required]
        public decimal VatRate { get; set; } = 21.00m;
        
        [Required]
        public decimal VatAmount { get; set; }
        
        [Required]
        public decimal AmountInclVat { get; set; }
        
        public int InvoiceId { get; set; }
        
        public string? Notes { get; set; }
        
        // File data
        public string? Base64Content { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        
        // OCR extracted data
        public bool IsOcrExtracted { get; set; }
        
        // Navigation
        public virtual ICollection<ExpenseInvoiceLine> ExpenseInvoiceLines { get; set; } = new List<ExpenseInvoiceLine>();
    }
}
