// =====================================================
// NORDIC BEES ERP - HONEY BATCH (LOT) MODELS
// Framework: .NET 10
// ORM: Entity Framework Core
// =====================================================

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models.Honey;

// =====================================================
// MEDŲ Gamybos partijos (LOT tracking)
// =====================================================

[Table("honey_batches")]
public class HoneyBatch
{
    [Key]
    [Column("batch_id")]
    public int BatchId { get; set; }

    [Required]
    [Column("processing_date")]
    public DateTime ProcessingDate { get; set; }

    [Required]
    [Column("quantity")]
    public decimal Quantity { get; set; }

    [Required]
    [Column("warehouse_id")]
    public int WarehouseId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("lot_number")]
    public string LotNumber { get; set; } = string.Empty;

    // Navigation
    [ForeignKey("WarehouseId")]
    [NotMapped]
    public virtual Warehouse? Warehouse { get; set; }

    [NotMapped]
    public virtual ICollection<HoneyBatchIngredient> HoneyBatchIngredients { get; set; } = new List<HoneyBatchIngredient>();

    [NotMapped]
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}

// =====================================================
// HoneyBatchIngredient - Partijos sudėties ingredientas
// =====================================================

[Table("honey_batch_ingredients")]
public class HoneyBatchIngredient
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

    // Navigation
    [ForeignKey("BatchId")]
    [NotMapped]
    public virtual HoneyBatch? Batch { get; set; }

    [ForeignKey("HoneyDeliveryId")]
    [NotMapped]
    public virtual HoneyDelivery? HoneyDelivery { get; set; }
}