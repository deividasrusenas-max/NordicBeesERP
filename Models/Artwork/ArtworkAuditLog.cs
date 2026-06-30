using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models.Artwork;

[Table("artwork_audit_log")]
public class ArtworkAuditLog
{
    [Key]
    [Column("id")]
    public long Id { get; set; }
    [Column("entity_type")]
    public string EntityType { get; set; } = "";
    [Column("entity_id")]
    public int EntityId { get; set; }
    [Column("action")]
    public string Action { get; set; } = "";
    [Column("user_id")]
    public int UserId { get; set; }
    [Column("details")]
    public string? Details { get; set; }
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties (ignored per standards)
    [NotMapped]
    public ErpUser? User { get; set; }
}
