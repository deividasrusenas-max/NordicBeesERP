// =====================================================
// NORDIC BEES ERP - DASHBOARD DAILY SNAPSHOT ENTITY
// Framework: .NET 10
// ORM: Entity Framework Core
// =====================================================

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models
{
    [Table("dashboard_daily_snapshots")]
    public class DashboardDailySnapshot
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("snapshot_date", TypeName = "date")]
        public DateTime SnapshotDate { get; set; }

        [Column("barrels_count")]
        public int BarrelsCount { get; set; }

        [Column("barrels_kg", TypeName = "decimal(12,2)")]
        public decimal BarrelsKg { get; set; }

        [Column("buckets_count")]
        public int BucketsCount { get; set; }

        [Column("buckets_kg", TypeName = "decimal(12,2)")]
        public decimal BucketsKg { get; set; }

        [Column("unpriced_deliveries_count")]
        public int UnpricedDeliveriesCount { get; set; }

        [Column("supplier_debt_total", TypeName = "decimal(12,2)")]
        public decimal SupplierDebtTotal { get; set; }

        [Column("supplier_debt_count")]
        public int SupplierDebtCount { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
