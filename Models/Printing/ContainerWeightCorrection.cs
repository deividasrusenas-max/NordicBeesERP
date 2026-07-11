using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NordicBeesERP.Models.WarehouseModule;

namespace NordicBeesERP.Models.Printing;

/// <summary>
/// BRC8 Clause 3.7 — Weight correction audit trail for containers.
/// Tracks all weight components (gross, tare, net) before and after correction.
/// Never deleted — immutable audit record.
/// </summary>
[Table("container_weight_corrections")]
public class ContainerWeightCorrection
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("container_id")]
    public int ContainerId { get; set; }

    [Required]
    [Column("old_gross_weight", TypeName = "decimal(10,3)")]
    public decimal OldGrossWeight { get; set; }

    [Required]
    [Column("new_gross_weight", TypeName = "decimal(10,3)")]
    public decimal NewGrossWeight { get; set; }

    [Required]
    [Column("old_tare_weight", TypeName = "decimal(10,3)")]
    public decimal OldTareWeight { get; set; }

    [Required]
    [Column("new_tare_weight", TypeName = "decimal(10,3)")]
    public decimal NewTareWeight { get; set; }

    [Required]
    [Column("old_net_weight", TypeName = "decimal(10,3)")]
    public decimal OldNetWeight { get; set; }

    [Required]
    [Column("new_net_weight", TypeName = "decimal(10,3)")]
    public decimal NewNetWeight { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("reason")]
    public string Reason { get; set; } = string.Empty;

    [Required]
    [Column("corrected_by")]
    public int CorrectedBy { get; set; }

    [Required]
    [Column("corrected_at")]
    public DateTime CorrectedAt { get; set; } = DateTime.Now;

    // Navigation properties — not mapped to DB
    [NotMapped]
    public Container? Container { get; set; }

    [NotMapped]
    public ErpUser? CorrectedByUser { get; set; }
}
