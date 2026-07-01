using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models.Artwork;

[Table("artwork_versions")]
public class ArtworkVersion
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    [Column("asset_id")]
    public int AssetId { get; set; }
    [Column("version_number")]
    public int VersionNumber { get; set; }
    [Column("file_type")]
    public string FileType { get; set; } = "print_ready";
    [Column("file_path")]
    public string FilePath { get; set; } = "";
    [Column("original_filename")]
    public string OriginalFilename { get; set; } = "";
    [Column("file_size_bytes")]
    public long FileSizeBytes { get; set; }
    [Column("file_sha256")]
    public string FileSha256 { get; set; } = "";
    [Column("preview_path")]
    public string? PreviewPath { get; set; }
    [Column("thumbnail_path")]
    public string? ThumbnailPath { get; set; }
    [Column("page_count")]
    public int? PageCount { get; set; }
    [Column("change_description")]
    public string ChangeDescription { get; set; } = "";
    [Column("status")]
    public string Status { get; set; } = "pending";
    [Column("uploaded_by")]
    public int UploadedBy { get; set; }
    [Column("uploaded_at")]
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    [Column("reviewed_by")]
    public int? ReviewedBy { get; set; }
    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }
    [Column("review_comment")]
    public string? ReviewComment { get; set; }

    // Navigation properties (ignored per standards)
    [NotMapped]
    public ArtworkAsset? Asset { get; set; }
    [NotMapped]
    public ErpUser? UploadedByUser { get; set; }
    [NotMapped]
    public ErpUser? ReviewedByUser { get; set; }
}
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models.Artwork;

[Table("artwork_versions")]
public class ArtworkVersion
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    [Column("asset_id")]
    public int AssetId { get; set; }
    [Column("version_number")]
    public int VersionNumber { get; set; }
    [Column("file_type")]
    public string FileType { get; set; } = "print_ready";
    [Column("file_path")]
    public string FilePath { get; set; } = "";
    [Column("original_filename")]
    public string OriginalFilename { get; set; } = "";
    [Column("file_size_bytes")]
    public long FileSizeBytes { get; set; }
    [Column("file_sha256")]
    public string FileSha256 { get; set; } = "";
    [Column("preview_path")]
    public string? PreviewPath { get; set; }
    [Column("thumbnail_path")]
    public string? ThumbnailPath { get; set; }
    [Column("page_count")]
    public int? PageCount { get; set; }
    [Column("change_description")]
    public string ChangeDescription { get; set; } = "";
    [Column("status")]
    public string Status { get; set; } = "pending";
    [Column("uploaded_by")]
    public int UploadedBy { get; set; }
    [Column("uploaded_at")]
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    [Column("reviewed_by")]
    public int? ReviewedBy { get; set; }
    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }
    [Column("review_comment")]
    public string? ReviewComment { get; set; }

    // Navigation properties (ignored per standards)
    [NotMapped]
    public ArtworkAsset? Asset { get; set; }
    [NotMapped]
    public ErpUser? UploadedByUser { get; set; }
    [NotMapped]
    public ErpUser? ReviewedByUser { get; set; }
}
