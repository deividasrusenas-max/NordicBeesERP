using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models.Artwork;

[Table("artwork_comments")]
public class ArtworkComment
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    [Column("version_id")]
    public int VersionId { get; set; }
    [Column("user_id")]
    public int UserId { get; set; }
    [Column("body")]
    public string Body { get; set; } = "";
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties (ignored per standards)
    [NotMapped]
    public ArtworkVersion? Version { get; set; }
    [NotMapped]
    public ErpUser? User { get; set; }
}
