using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models.Artwork;

[Table("artwork_files")]
public class ArtworkFile
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("asset_id")]
    public int AssetId { get; set; }

    [Column("label_type")]
    public string LabelType { get; set; } = "";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Known artwork label types for the multi-file (label-set) feature.
/// Stored as a free VARCHAR in artwork_files.label_type; extend this list as business needs grow.
/// Legacy versions whose artwork_file_id is NULL are displayed under <see cref="General"/>.
/// </summary>
public static class ArtworkLabelTypes
{
    public const string General = "Bendra";

    public static readonly List<string> All = new() { "Sverimo", "Klijavimo" };
}
