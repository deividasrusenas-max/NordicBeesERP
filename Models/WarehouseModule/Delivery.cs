using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models.WarehouseModule;

[Table("deliveries")]
public class Delivery
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [MaxLength(50)]
    [Column("delivery_number")]
    public string? DeliveryNumber { get; set; }

    [Required]
    [Column("delivery_date")]
    public DateTime DeliveryDate { get; set; } = DateTime.Now;

    [Column("supplier_id")]
    public int SupplierId { get; set; }

    [Column("warehouse_id")]
    public int WarehouseId { get; set; }

    [Column("raw_material_type_id")]
    public int? RawMaterialTypeId { get; set; }

    [ForeignKey("RawMaterialTypeId")]
    public RawMaterialType? RawMaterialType { get; set; }

    [Column("status")]
    public string Status { get; set; } = "RECEIVED";

    [Column("total_net_weight")]
    public decimal TotalNetWeight { get; set; }

    [Column("total_amount")]
    public decimal TotalAmount { get; set; }

    [Column("paid_amount")]
    public decimal PaidAmount { get; set; }

    [Column("barrels_owed")]
    public int BarrelsOwed { get; set; }

    [Column("barrels_returned")]
    public int BarrelsReturned { get; set; }

    [Column("need_return_barrels")]
    public bool NeedReturnBarrels { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("invoice_id")]
    public int? InvoiceId { get; set; }

    [Column("invoice_number")]
    public string? InvoiceNumber { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}