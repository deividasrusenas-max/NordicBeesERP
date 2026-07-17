using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models.WarehouseModule;

public enum NcType { QUALITY, WEIGHT, DOCUMENTATION, OTHER }

public enum NonConformanceStatus { OPEN, INVESTIGATING, RESOLVED, CLOSED }

[Table("non_conformances")]
public class NonConformance
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("delivery_id")]
    public int DeliveryId { get; set; }

    [Column("container_id")]
    public int? ContainerId { get; set; }

    [Required]
    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Column("nc_type")]
    public NcType NcType { get; set; } = NcType.OTHER;

    [Required]
    [Column("discovered_by")]
    public int DiscoveredBy { get; set; }

    [Required]
    [Column("discovered_at")]
    public DateTime DiscoveredAt { get; set; } = DateTime.Now;

    [Required]
    [Column("status")]
    public NonConformanceStatus Status { get; set; } = NonConformanceStatus.OPEN;

    [Column("corrective_action")]
    public string? CorrectiveAction { get; set; }

    [Column("closed_by")]
    public int? ClosedBy { get; set; }

    [Column("closed_at")]
    public DateTime? ClosedAt { get; set; }

    [NotMapped]
    public virtual Delivery? Delivery { get; set; }

    [NotMapped]
    public virtual Container? Container { get; set; }
}
