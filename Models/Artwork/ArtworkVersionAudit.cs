using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models.Artwork;

[Table("artwork_version_audits")]
public class ArtworkVersionAudit
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("version_id")]
    public int VersionId { get; set; }

    [Column("action")]
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty;

    [Column("action_details")]
    [MaxLength(500)]
    public string? ActionDetails { get; set; }

    [Column("old_status")]
    [MaxLength(30)]
    public string? OldStatus { get; set; }

    [Column("new_status")]
    [MaxLength(30)]
    public string? NewStatus { get; set; }

    [Column("performed_by")]
    [MaxLength(100)]
    public string? PerformedBy { get; set; }

    [Column("performed_at")]
    public DateTime PerformedAt { get; set; }

    // Navigation property
    [NotMapped]
    public ArtworkVersion? Version { get; set; }
}