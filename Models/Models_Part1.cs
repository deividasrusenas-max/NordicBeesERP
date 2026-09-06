// =====================================================
// NORDIC BEES ERP - C# ENTITY MODELS
// Framework: .NET 10
// ORM: Entity Framework Core
// =====================================================

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models.Honey;

namespace NordicBeesERP.Models
{
    // =====================================================
    // ĮMONĖS INFORMACIJA
    // =====================================================

    [Table("companies")]
    public class Company
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        [Column("company_code")]
        public string CompanyCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        [Column("vat_code")]
        public string VatCode { get; set; } = string.Empty;

        [Required]
        [Column("address")]
        public string Address { get; set; } = string.Empty;

        [MaxLength(100)]
        [Column("city")]
        public string? City { get; set; }

        [MaxLength(20)]
        [Column("postal_code")]
        public string? PostalCode { get; set; }

        [MaxLength(100)]
        [Column("country")]
        public string Country { get; set; } = PdfLocalization.CountryEn;

        [MaxLength(10)]
        [Column("country_code")]
        public string CountryCode { get; set; } = "LT";

        [Required]
        [MaxLength(50)]
        [Column("bank_account")]
        public string BankAccount { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        [Column("swift")]
        public string Swift { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        [Column("bank_name")]
        public string BankName { get; set; } = string.Empty;

        [MaxLength(50)]
        [Column("phone")]
        public string? Phone { get; set; }

        [MaxLength(100)]
        [Column("email")]
        public string? Email { get; set; }

        [MaxLength(255)]
        [Column("website")]
        public string? Website { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    // =====================================================
    // VERSLO PARTNERIAI (KLIENTAI + TIEKĖJAI)
    // =====================================================

    public enum PartnerType
    {
        Customer,
        Supplier,
        Both,
        ExpenseSupplier
    }

    [Table("business_partners")]
    public class BusinessPartner
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("partner_type")]
        public PartnerType PartnerType { get; set; }

        [Column("is_customer")]
        public bool IsCustomer { get; set; }

        [Column("is_supplier")]
        public bool IsSupplier { get; set; }

        [Column("is_expense_supplier")]
        public bool IsExpenseSupplier { get; set; }

        [Column("is_individual")]
        public bool IsIndividual { get; set; }

        [Required]
        [MaxLength(255)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        [Column("company_code")]
        public string? CompanyCode { get; set; }

        [MaxLength(50)]
        [Column("vat_code")]
        public string? VatCode { get; set; }

        [Column("vat_verified")]
        public bool? VatVerified { get; set; }        // null = never checked, true = valid, false = confirmed invalid

        [Column("vat_verified_at")]
        public DateTime? VatVerifiedAt { get; set; }

        [MaxLength(255)]
        [Column("vat_verified_name")]
        public string? VatVerifiedName { get; set; }  // official registered name from VIES

        [Column("address")]
        public string? Address { get; set; }

        [MaxLength(100)]
        [Column("city")]
        public string? City { get; set; }

        [MaxLength(20)]
        [Column("postal_code")]
        public string? PostalCode { get; set; }

        [MaxLength(100)]
        [Column("country")]
        public string Country { get; set; } = PdfLocalization.CountryEn;

        [MaxLength(10)]
        [Column("country_code")]
        public string CountryCode { get; set; } = "LT";

        [MaxLength(50)]
        [Column("phone")]
        public string? Phone { get; set; }

        [MaxLength(50)]
        [Column("contact_phone")]
        public string? ContactPhone { get; set; }

        [MaxLength(255)]
        [Column("email")]
        public string? Email { get; set; }

        [MaxLength(255)]
        [Column("invoice_email")]
        public string? InvoiceEmail { get; set; }

        [Column("no_email")]
        public bool NoEmail { get; set; } = false;

        [MaxLength(50)]
        [Column("bank_account")]
        public string? BankAccount { get; set; }

        [Column("payment_term_days")]
        public int PaymentTermDays { get; set; } = 7;

        [MaxLength(5)]
        [Column("default_language")]
        public string DefaultLanguage { get; set; } = "LT";

        [Column("default_vat_rate")]
        [Precision(5, 2)]
        public decimal DefaultVatRate { get; set; } = 0m;

        // Tiekėjo specifiniai laukai
        [MaxLength(255)]
        [Column("supplier_first_name")]
        public string? SupplierFirstName { get; set; }

        [MaxLength(255)]
        [Column("supplier_last_name")]
        public string? SupplierLastName { get; set; }

        [MaxLength(50)]
        [Column("national_id_number")]
        public string? NationalIdNumber { get; set; }

        [MaxLength(100)]
        [Column("supplier_type")]
        public string? SupplierType { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [Column("default_expense_category_id")]
        public int? DefaultExpenseCategoryId { get; set; }

        // Navigation properties
        public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
        [NotMapped]
        public virtual ICollection<HoneyDelivery> HoneyDeliveries { get; set; } = new List<HoneyDelivery>();
    }

    // =====================================================
    // PRODUKTAI
    // =====================================================

    public enum ProductType
    {
        RawMaterial,
        Packaging,
        SemiFinished,
        FinishedGood,
        Service
    }

    [Table("product_categories")]
    public class ProductCategory
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
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

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        // Navigation
        [ForeignKey("ParentId")]
        [NotMapped]
        public virtual ProductCategory? Parent { get; set; }
        
        [NotMapped]
        public virtual ICollection<ProductCategory> Children { get; set; } = new List<ProductCategory>();
        [NotMapped]
        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }

    [Table("products")]
    public class Product
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [Required]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        [Column("ean_code")]
        public string? EanCode { get; set; }

        [Required]
        [Column("product_type")]
        public ProductType ProductType { get; set; }

        [Column("category_id")]
        public int? CategoryId { get; set; }

        [Column("unit_id")]
        public int? UnitId { get; set; }

        [MaxLength(20)]
        [Column("unit")]
        public string Unit { get; set; } = "kg";

        [Column("cost_price")]
        public decimal CostPrice { get; set; } = 0;

        [Column("sale_price")]
        public decimal SalePrice { get; set; } = 0;

        [Column("purchase_price")]
        public decimal PurchasePrice { get; set; } = 0;

        [MaxLength(100)]
        [NotMapped]
        public string Category { get; set; } = string.Empty;

        [Column("warehouse_managed")]
        public bool WarehouseManaged { get; set; } = false;

        [Column("track_lots")]
        public bool TrackLots { get; set; } = false;

        [Column("min_stock_level")]
        public decimal MinStockLevel { get; set; } = 0;

        [Column("description")]
        public string? Description { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        // Navigation - commented out for raw SQL compatibility
        // [ForeignKey("CategoryId")]
        // public virtual ProductCategory? Category { get; set; }

        [NotMapped]
        public virtual ICollection<InvoiceLine> InvoiceLines { get; set; } = new List<InvoiceLine>();
        [NotMapped]
        public virtual ICollection<WarehouseStock> WarehouseStocks { get; set; } = new List<WarehouseStock>();
    }

    // =====================================================
    // SANDĖLIAI
    // =====================================================

    [Table("warehouses")]
    public class Warehouse
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("warehouse_type_id")]
        public int? WarehouseTypeId { get; set; }

        [Column("address")]
        public string? Address { get; set; }

        [MaxLength(100)]
        [Column("city")]
        public string? City { get; set; }

        [MaxLength(100)]
        [Column("country")]
        public string Country { get; set; } = PdfLocalization.CountryEn;

        [Column("description")]
        public string? Description { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [MaxLength(255)]
        [Column("email")]
        public string? Email { get; set; }

        // Navigation
        [NotMapped]
        public virtual ICollection<WarehouseStock> Stocks { get; set; } = new List<WarehouseStock>();
        [NotMapped]
        public virtual ICollection<HoneyDelivery> HoneyDeliveries { get; set; } = new List<HoneyDelivery>();
        [NotMapped]
        public virtual ICollection<ProductionBatch> ProductionBatches { get; set; } = new List<ProductionBatch>();
        [NotMapped]
        public virtual ICollection<HoneyBatch> HoneyBatches { get; set; } = new List<HoneyBatch>();
    }

    [Table("warehouse_stocks")]
public class WarehouseStock
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("warehouse_id")]
    public int WarehouseId { get; set; }

    [Required]
    [Column("product_id")]
    public int ProductId { get; set; }

    [MaxLength(100)]
    [Column("lot_number")]
    public string? LotNumber { get; set; }

    [Column("quantity")]
    public decimal Quantity { get; set; } = 0;

    [Column("reserved_quantity")]
    public decimal ReservedQuantity { get; set; } = 0;

    [Column("available_quantity")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public decimal AvailableQuantity { get; set; }

    [Column("last_movement_date")]
    public DateTime? LastMovementDate { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [Required]
    [Column("honey_batch_id")]
    public int HoneyBatchId { get; set; }

    [Column("expiration_date")]
    public DateTime? ExpirationDate { get; set; }

    // Navigation
    [ForeignKey("WarehouseId")]
    [NotMapped]
    public virtual Warehouse Warehouse { get; set; } = null!;

    [ForeignKey("ProductId")]
    [NotMapped]
    public virtual Product Product { get; set; } = null!;

    [ForeignKey("HoneyBatchId")]
    [NotMapped]
    public virtual HoneyBatch HoneyBatch { get; set; } = null!;
}
    // =====================================================
    // MEDAUS SUPIRKIMAS
    // =====================================================

    [Table("honey_types")]
    public class HoneyType
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        [Column("name_en")]
        public string? NameEn { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [MaxLength(20)]
        [Column("color")]
        public string? Color { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation
        [NotMapped]
        public virtual ICollection<HoneyDelivery> HoneyDeliveries { get; set; } = new List<HoneyDelivery>();
    }

    [Table("honey_deliveries")]
    public class HoneyDelivery
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("delivery_date")]
        public DateTime DeliveryDate { get; set; }

        [MaxLength(50)]
        [Column("delivery_number")]
        public string? DeliveryNumber { get; set; }

        [Required]
        [Column("supplier_id")]
        public int SupplierId { get; set; }

        [Column("product_id")]
        public int? ProductId { get; set; }

        [Column("honey_type_id")]
        public int? HoneyTypeId { get; set; }

        [Required]
        [Column("gross_weight")]
        public decimal GrossWeight { get; set; }

        [Required]
        [Column("tare_weight")]
        public decimal TareWeight { get; set; }

        [Required]
        [Column("net_weight")]
        public decimal NetWeight { get; set; }

        [Required]
        [Column("container_quantity")]
        public int ContainerQuantity { get; set; }

        [Required]
        [Column("warehouse_id")]
        public int WarehouseId { get; set; }

        [Column("price_per_kg")]
        public decimal? PricePerKg { get; set; }

        [Column("total_cost")]
        public decimal? TotalCost { get; set; }

        [Column("transport_cost")]
        public decimal TransportCost { get; set; } = 0;

        [Column("is_soured")]
        public bool IsSoured { get; set; } = false;

        [MaxLength(50)]
        [Column("quality_grade")]
        public string? QualityGrade { get; set; }

        [Column("beehive_location")]
        public string? BeehiveLocation { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        // Navigation
        [ForeignKey("SupplierId")]
        [NotMapped]
        public virtual BusinessPartner Supplier { get; set; } = null!;

        [ForeignKey("ProductId")]
        [NotMapped]
        public virtual Product? Product { get; set; }

        [ForeignKey("HoneyTypeId")]
        [NotMapped]
        public virtual HoneyType? HoneyType { get; set; }

        [ForeignKey("WarehouseId")]
        [NotMapped]
        public virtual Warehouse Warehouse { get; set; } = null!;

        [NotMapped]
        public virtual ICollection<ProductionBatchIngredient> UsedInBatches { get; set; } = new List<ProductionBatchIngredient>();
        
        [NotMapped]
        public virtual ICollection<HoneyBatchIngredient> UsedInHoneyBatches { get; set; } = new List<HoneyBatchIngredient>();
    }
}
