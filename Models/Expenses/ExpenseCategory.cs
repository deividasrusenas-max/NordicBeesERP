// =====================================================
// NORDIC BEES ERP - EXPENSE MODULE
// Framework: .NET 10
// ORM: Entity Framework Core
// =====================================================

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models.Expenses
{
    // =====================================================
    // IŠLAIDŲ KATEGORIJA (EXPENSE CATEGORY)
    // =====================================================

    [Table("expense_categories")]
    public class ExpenseCategory
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int? ParentId { get; set; }

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

        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;
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
}