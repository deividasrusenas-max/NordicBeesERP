// =====================================================
// NORDIC BEES ERP - INVOICE SUPPORT MODELS
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
    // VALIUTOS (CURRENCIES)
    // =====================================================

    [Table("currencies")]
    public class Currency
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [MaxLength(3)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [Column("name")]
        public string? Name { get; set; } = string.Empty;

        [Column("symbol")]
        public string? Symbol { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        // Navigation
        public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    }

    // =====================================================
    // MOKĖJIMO SĄLYGOS (PAYMENT TERMS)
    // =====================================================

    [Table("payment_terms")]
    public class PaymentTerm
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("days")]
        public int Days { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        // Navigation
        public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    }

    // =====================================================
    // KLIENTAI (CUSTOMERS - alias for BusinessPartner with Customer type)
    // =====================================================

    // Note: Customer is essentially a BusinessPartner where PartnerType = Customer
    // This is a read-only model for UI purposes, not a separate database table
    public class Customer
    {
        public int Id { get; set; }
        
        // Partnerio tipas (BusinessPartner)
        public string PartnerType { get; set; } = "Klientas";
        
        public string Name { get; set; } = string.Empty;
        public string? CompanyCode { get; set; }
        public string? VatCode { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? PostalCode { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? BankAccount { get; set; }
        public int PaymentTermDays { get; set; } = 7;
        public string? DefaultLanguage { get; set; } = "lt";
        public decimal DefaultVatRate { get; set; } = 21.00m;
        public string? CountryCode { get; set; } = "LT";
        public string? Country { get; set; } = "Lietuva";
        public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;
        
        // Laikini laukai (ne duomenų bazėje)
        [NotMapped]
        public DateTime CreatedAt { get; set; }
        [NotMapped]
        public DateTime UpdatedAt { get; set; }
    }
}
