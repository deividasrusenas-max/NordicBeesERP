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
/// Sentinel value for "no specific label type / legacy" artwork.
/// This is NOT a selectable option in the artwork_label_types settings table —
/// it represents the absence of a specific type. Real, selectable label types
/// now live in the artwork_label_types DB table (managed via /settings/artwork-label-types);
/// their names are seeded there and must match the historical artwork_files.label_type string values.
/// Legacy versions whose artwork_file_id is NULL are displayed under <see cref="General"/>.
/// </summary>
public static class ArtworkLabelTypes
{
    public const string General = "Bendra";
}
