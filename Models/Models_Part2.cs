// =====================================================
// NORDIC BEES ERP - C# ENTITY MODELS (PART 2)
// Gamyba, Sąskaitos, Užsakymai
// =====================================================

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models.WarehouseModule;

namespace NordicBeesERP.Models
{
    // =====================================================
    // GAMYBA IR LOT
    // =====================================================

    [Table("production_batches")]
    public class ProductionBatch
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("batch_number")]
        public string BatchNumber { get; set; } = string.Empty;

        [Required]
        [Column("production_date")]
        public DateTime ProductionDate { get; set; }

        [NotMapped]
        public DateTime? DueDate { get; set; }

        [Column("warehouse_id")]
        public int? WarehouseId { get; set; }

        [Required]
        [Column("quantity")]
        public decimal Quantity { get; set; }

        [MaxLength(100)]
        [Column("product_code")]
        public string? ProductCode { get; set; }

        [MaxLength(255)]
        [Column("product_name")]
        public string? ProductName { get; set; }

        [Column("batch_status")]
        public string BatchStatus { get; set; } = "Active";

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("WarehouseId")]
        [NotMapped]
        public virtual Warehouse? Warehouse { get; set; }

        [NotMapped]
        public virtual ICollection<ProductionBatchIngredient> Ingredients { get; set; } = new List<ProductionBatchIngredient>();
    }

    [Table("production_batch_ingredients")]
    public class ProductionBatchIngredient
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("batch_id")]
        public int BatchId { get; set; }

        [Required]
        [Column("honey_delivery_id")]
        public int HoneyDeliveryId { get; set; }

        [Required]
        [Column("quantity")]
        public decimal Quantity { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("BatchId")]
        [NotMapped]
        public virtual ProductionBatch Batch { get; set; } = null!;

        [ForeignKey("HoneyDeliveryId")]
        [NotMapped]
        public virtual HoneyDelivery HoneyDelivery { get; set; } = null!;
    }

    // =====================================================
    // TIEKĖJAI (SUPPLIERS - alias for BusinessPartner with Supplier type)
    // =====================================================

    // Note: Supplier is essentially a BusinessPartner where PartnerType = Supplier
    // This is a read-only model for UI purposes, not a separate database table
    public class Supplier
    {
        public int Id { get; set; }
        
        // Partnerio tipas (BusinessPartner)
        public PartnerType PartnerType { get; set; } = PartnerType.Supplier;
        
        public string Name { get; set; } = string.Empty;
        public string? CompanyCode { get; set; }
        public string? VatCode { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? PostalCode { get; set; }
        public string? Phone { get; set; }
        public string? ContactPhone { get; set; }
        public string? Email { get; set; }
        public string? InvoiceEmail { get; set; }
        public string? BankAccount { get; set; }
        public int PaymentTermDays { get; set; } = 7;
        public string DefaultLanguage { get; set; } = "lt";
        [Precision(5, 2)]
        public decimal DefaultVatRate { get; set; } = 0m;
        public string CountryCode { get; set; } = "LT";
        public string Country { get; set; } = PdfLocalization.CountryLt;
        public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;
        
        // Default expense category for supplier expenses
        public int? DefaultExpenseCategoryId { get; set; }
        
        // Role flag'ai (Phase 3) — atspindi BusinessPartner.Is* stulpelius
        public bool IsCustomer { get; set; }
        public bool IsSupplier { get; set; }
        public bool IsExpenseSupplier { get; set; }
        public bool IsIndividual { get; set; }

        // Tiekėjo specifiniai laukai (iš BusinessPartner)
        public string? SupplierFirstName { get; set; }
        public string? SupplierLastName { get; set; }
        public string? NationalIdNumber { get; set; }
        public string? SupplierType { get; set; }
        
        // Laikini laukai (ne duomenų bazėje)
        [NotMapped]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        [NotMapped]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    // =====================================================
    // UŽSAKOMI PRODUKTAI (ORDERED PRODUCTS)
    // =====================================================

    [Table("ordered_products")]
    public class OrderedProduct
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("order_date")]
        public DateTime OrderDate { get; set; }

        [Column("estimated_delivery_date")]
        public DateTime? EstimatedDeliveryDate { get; set; }

        [Column("actual_delivery_date")]
        public DateTime? ActualDeliveryDate { get; set; }

        [Column("supplier_id")]
        public int? SupplierId { get; set; }

        [Column("status")]
        public string Status { get; set; } = "Pending";

        [Column("total_amount")]
        public decimal TotalAmount { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("SupplierId")]
        [NotMapped]
        public virtual BusinessPartner? Supplier { get; set; }

        [NotMapped]
        public virtual ICollection<OrderedProductLine> Lines { get; set; } = new List<OrderedProductLine>();
    }

    [Table("ordered_product_lines")]
    public class OrderedProductLine
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("order_id")]
        public int OrderId { get; set; }

        [Column("product_id")]
        public int? ProductId { get; set; }

        [MaxLength(100)]
        [Column("product_name")]
        public string? ProductName { get; set; }

        [Column("quantity")]
        public decimal Quantity { get; set; }

        [Column("unit_price")]
        public decimal UnitPrice { get; set; }

        [Column("total_price")]
        public decimal TotalPrice { get; set; }

        [Column("received_quantity")]
        public decimal ReceivedQuantity { get; set; }

        [Column("status")]
        public string Status { get; set; } = "Pending";

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("OrderId")]
        [NotMapped]
        public virtual OrderedProduct Order { get; set; } = null!;

        [ForeignKey("ProductId")]
        [NotMapped]
        public virtual Product? Product { get; set; }
    }

    // =====================================================
    // SĄSKAITOS (INVOICES)
    // =====================================================

    public enum InvoiceStatus
    {
        Draft = 0,
        Confirmed = 1,  // DB value 1 (was Issued)
        Paid = 3,       // DB value 3 (unchanged)
        Disputed = 6,   // DB value 6 (new)
        Cancelled = 4,  // DB value 4
    }
    [Table("invoices")]
public class Invoice
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("invoice_number")]
    public string InvoiceNumber { get; set; } = string.Empty;

    [Required]
    [Column("invoice_date")]
    public DateTime InvoiceDate { get; set; }

    [Required]
    [Column("customer_id")]
    public int CustomerId { get; set; }

    [Column("customer_vat_code")]
    public string? CustomerVatCode { get; set; }

    [Column("currency_id")]
    public int? CurrencyId { get; set; }

    [Column("payment_due_date")]
    public DateTime? PaymentDueDate { get; set; }

    [Column("payment_term_days")]
    public int PaymentTermDays { get; set; } = 7;

    [MaxLength(5)]
    [Column("language")]
    public string Language { get; set; } = "lt";

    [Column("invoice_type")]
    public string InvoiceType { get; set; } = "PVM SĄSKAITA FAKTŪRA";

    [Column("reverse_charge")]
    public bool ReverseCharge { get; set; } = false;

    [Column("subtotal_excl_vat")]
    public decimal SubtotalExclVat { get; set; }

    [Column("total_vat")]
    public decimal TotalVat { get; set; }

    [Column("total_incl_vat")]
    public decimal TotalInclVat { get; set; }

    [Column("paid_amount")]
    public decimal PaidAmount { get; set; }

    [Column("payment_status")]
    public string PaymentStatus { get; set; } = "unpaid";

    [Column("last_payment_date")]
    public DateTime? LastPaymentDate { get; set; }

    [Column("status")]
    public InvoiceStatus Status { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;


    [Column("delivery_id")]
    public int? DeliveryId { get; set; }

    [Column("due_date")]
    public DateTime? DueDate { get; set; }
    
    [ForeignKey("CustomerId")]
    public virtual BusinessPartner? Customer { get; set; }
    [ForeignKey("CurrencyId")]
    public virtual Currency? Currency { get; set; }
    public virtual ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual ICollection<PaymentAllocation> PaymentAllocations { get; set; } = new List<PaymentAllocation>();

    // Navigation
    [ForeignKey("DeliveryId")]
    public virtual Delivery? Delivery { get; set; }
}

    [Table("invoice_lines")]
    public class InvoiceLine
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("invoice_id")]
        public int InvoiceId { get; set; }

        [Column("product_id")]
        public int? ProductId { get; set; }

        [MaxLength(100)]
        [Column("product_code")]
        public string? ProductCode { get; set; }

        [Column("lot_number")]
        public string? LotNumber { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [NotMapped]
        public int? UnitId { get; set; }

        [MaxLength(20)]
        [Column("unit")]
        public string Unit { get; set; } = "kg";

        [Column("line_number")]
        public int LineNumber { get; set; }

        [Required]
        [Column("quantity")]
        public decimal Quantity { get; set; }

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

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("InvoiceId")]
        public virtual Invoice Invoice { get; set; } = null!;

        [ForeignKey("ProductId")]
        [NotMapped]
        public virtual Product? Product { get; set; }

        [ForeignKey("WarehouseId")]
        [NotMapped]
        public virtual Warehouse? Warehouse { get; set; }

        [NotMapped]
        public virtual ICollection<CreditNoteLine> CreditNoteLines { get; set; } = new List<CreditNoteLine>();
    }

    // =====================================================
    // MOKĖJIMAI (PAYMENTS)
    // =====================================================

    public enum PaymentMethod
    {
        Cash,
        BankTransfer,
        Card,
        ElectronicMoney,
        Other
    }

    [Table("payments")]
    public class Payment
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("payment_date")]
        public DateTime PaymentDate { get; set; }

        [Column("invoice_id")]
        public int? InvoiceId { get; set; }

        [Required]
        [Column("customer_id")]
        public int CustomerId { get; set; }

        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("payment_method")]
        public PaymentMethod PaymentMethod { get; set; }

        [Column("source")]
        public string Source { get; set; } = "manual";

        [Column("bank_import_row_id")]
        public int? BankImportRowId { get; set; }

        [Column("bank_import_id")]
        public int? BankImportId { get; set; }

        [Column("bank_confirmed")]
        public bool BankConfirmed { get; set; } = false;

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [MaxLength(100)]
        [Column("reference_number")]
        public string? ReferenceNumber { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("InvoiceId")]
        [NotMapped]
        public virtual Invoice? Invoice { get; set; }

        [ForeignKey("BankImportId")]
        [NotMapped]
        public virtual BankImport? BankImport { get; set; }

        [ForeignKey("CustomerId")]
        [NotMapped]
        public virtual BusinessPartner Customer { get; set; } = null!;

         [ForeignKey("BankImportRowId")]
         public virtual BankImportRow? BankImportRow { get; set; }
 
         [ForeignKey("CreatedBy")]
         [NotMapped]
         public virtual ErpUser? CreatedByNavigation { get; set; }
 
          public virtual ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();
         public virtual ICollection<PaymentAuditLog> AuditLogs { get; set; } = new List<PaymentAuditLog>();
     }

    // =====================================================
    // IŠLAIKŲ KATEGORIJOS (EXPENSE CATEGORIES)
    // =====================================================

    [Table("expense_categories")]
    public class ExpenseCategory
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("parent_id")]
        public int? ParentId { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;

        // Navigation
        [ForeignKey("ParentId")]
        [NotMapped]
        public virtual ExpenseCategory? Parent { get; set; }

        [NotMapped]
        public virtual ICollection<ExpenseCategory> Children { get; set; } = new List<ExpenseCategory>();

        [NotMapped]
        public virtual ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    }

    // =====================================================
    // IŠLAIDŲ KAINŲ CENTRAS (EXPENSE COST CENTER)
    // =====================================================

    [Table("expense_cost_centers")]
    public class ExpenseCostCenter
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;
    }

    // =====================================================
    // IŠLAIKAI (EXPENSES)
    // =====================================================

    [Table("expenses")]
    public class Expense
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("expense_date")]
        public DateTime ExpenseDate { get; set; }

        [Column("category_id")]
        public int? CategoryId { get; set; }

        [Column("supplier_id")]
        public int? SupplierId { get; set; }

        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("vat_amount")]
        public decimal VatAmount { get; set; }

        [Column("total_amount")]
        public decimal TotalAmount { get; set; }

        [MaxLength(255)]
        [Column("description")]
        public string? Description { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("CategoryId")]
        [NotMapped]
        public virtual ExpenseCategory? Category { get; set; }

        [ForeignKey("SupplierId")]
        [NotMapped]
        public virtual BusinessPartner? Supplier { get; set; }
    }

    // =====================================================
    // VIENETAI (UNITS)
    // =====================================================

    [Table("units")]
    public class Unit
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;
    }

    // =====================================================
    // CASH FLOW FORECAST - WEEKLY VIEW MODEL
    // =====================================================

    public class WeeklyForecastItem
    {
        public string WeekLabel { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int InvoiceCount { get; set; }
        public bool IsCurrentWeek { get; set; }
        public bool IsPast { get; set; }
        public DateTime WeekStart { get; set; }
        public DateTime WeekEnd { get; set; }
    }
}
