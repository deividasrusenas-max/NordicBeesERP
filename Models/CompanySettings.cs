// =====================================================
// NORDIC BEES ERP - COMPANY SETTINGS
// Framework: .NET 10
// ORM: Entity Framework Core
// =====================================================

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models
{
    // =====================================================
    // ĮMONĖS NUSTATYMAI (COMPANY SETTINGS)
    // =====================================================

    [Table("company_settings")]
    public class CompanySettings
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        [Column("company_name")]
        public string CompanyName { get; set; } = string.Empty;

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

        [MaxLength(255)]
        [Column("bank_name")]
        public string? BankName { get; set; }

        [MaxLength(100)]
        [Column("bank_iban")]
        public string? BankIban { get; set; }

        [MaxLength(20)]
        [Column("bank_swift")]
        public string? BankSwift { get; set; }

        [MaxLength(20)]
        [Column("bank_account")]
        public string? BankAccount { get; set; }

        [MaxLength(255)]
        [Column("email")]
        public string? Email { get; set; }

        [MaxLength(20)]
        [Column("phone")]
        public string? Phone { get; set; }

        [MaxLength(255)]
        [Column("logo_path")]
        public string? LogoPath { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}