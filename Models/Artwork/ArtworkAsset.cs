using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models.Artwork;

[Table("artwork_assets")]
public class ArtworkAsset
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    [Column("brand_id")]
    public int BrandId { get; set; }
    [Column("name")]
    public string Name { get; set; } = "";
    [Column("asset_type")]
    public string AssetType { get; set; } = "label";
    [Column("description")]
    public string? Description { get; set; }
    [Column("predecessor_asset_id")]
    public int? PredecessorAssetId { get; set; }
    [Column("status")]
    public string Status { get; set; } = "active";
    [Column("created_by")]
    public int CreatedBy { get; set; }
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties (ignored per standards)
    [NotMapped]
    public ArtworkBrand? Brand { get; set; }
    [NotMapped]
    public ArtworkAsset? Predecessor { get; set; }
}
