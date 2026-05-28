using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models.WarehouseModule;

[Table("stock_movements")]
public class StockMovement
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("container_id")]
    public int ContainerId { get; set; }

    [Required]
    [Column("movement_type")]
    public string MovementType { get; set; } = "IN";

    [Column("from_warehouse_id")]
    public int? FromWarehouseId { get; set; }

    [Column("to_warehouse_id")]
    public int? ToWarehouseId { get; set; }

    [Column("quantity")]
    public decimal Quantity { get; set; }

    [MaxLength(50)]
    [Column("reference_type")]
    public string? ReferenceType { get; set; }

    [Column("reference_id")]
    public int? ReferenceId { get; set; }

    [Column("lot_id")]
    public int? LotId { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}