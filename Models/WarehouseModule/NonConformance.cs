using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models.WarehouseModule;

/// <summary>
/// BRC8 Clause 3.8 — Non-conformance tracking for deliveries and containers.
/// Supports quarantine workflow and corrective actions.
/// </summary>
[Table("non_conformances")]
public class NonConformance
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("ref_type")]
    public string RefType { get; set; } = "DELIVERY";

    [Required]
    [Column("ref_id")]
    public int RefId { get; set; }

    [Required]
    [Column("detected_at")]
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [Column("detected_by")]
    public int DetectedBy { get; set; }

    [Required]
    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(15)]
    [Column("severity")]
    public string Severity { get; set; } = "MINOR";

    [Required]
    [MaxLength(20)]
    [Column("disposition")]
    public string Disposition { get; set; } = "PENDING";

    [Column("disposition_by")]
    public int? DispositionBy { get; set; }

    [Column("disposition_at")]
    public DateTime? DispositionAt { get; set; }

    [Column("disposition_notes")]
    public string? DispositionNotes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property — not mapped
    [NotMapped]
    public ErpUser? DetectedByUser { get; set; }
}
