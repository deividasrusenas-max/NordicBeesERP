using NordicBeesERP.Models.Artwork;

namespace NordicBeesERP.Services.Artwork;

public interface IArtworkService
{
    Task<List<ArtworkBrandWithCounts>> GetBrandsWithCountsAsync();
    Task<ArtworkBrand?> GetBrandByIdAsync(int id);
    Task<List<ArtworkAssetWithSummary>> GetAssetsByBrandAsync(int brandId, bool showArchived = false);
    Task<ArtworkVersionUploadResult> UploadVersionAsync(int assetId, string changeDescription, string base64, string fileName, long fileSize, string fileType);
    
    // Asset Detail & Workflow
    Task<ArtworkAssetDetailDto> GetAssetDetailAsync(int assetId);
    Task ApproveVersionAsync(int versionId, int userId, DateTime effectiveFrom);
    Task RejectVersionAsync(int versionId, int userId, string comment);
    Task<List<ArtworkComment>> GetCommentsAsync(int versionId);
    Task AddCommentAsync(int versionId, int userId, string body);
    Task ArchiveAssetAsync(int assetId, int userId);
    Task RestoreAssetAsync(int assetId, int userId);
    Task<int> CreateAssetAsync(int brandId, string name, string type, string? description, int userId);
    Task<List<ArtworkGalleryItem>> GetGalleryAsync();
    Task<List<ArtworkBrand>> GetAllBrandsAsync();
    Task CreateBrandAsync(string name, string slug);
    Task UpdateBrandAsync(int id, string name, string slug, bool isActive);
    Task DeleteBrandAsync(int id);
    string GenerateSlug(string name);
}

public class ArtworkVersionUploadResult
{
    public bool Success { get; set; }
    public bool IsDuplicate { get; set; }
    public string? Message { get; set; }
    public ArtworkVersion? Version { get; set; }
}

public class ArtworkGalleryItem
{
    public int AssetId { get; set; }
    public string AssetName { get; set; } = "";
    public int BrandId { get; set; }
    public string BrandName { get; set; } = "";
    public string BrandSlug { get; set; } = "";
    public int VersionId { get; set; }
    public int VersionNumber { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public string? ThumbnailPath { get; set; }
    public string FilePath { get; set; } = "";
}

public class ArtworkAssetDetailDto
{
    public ArtworkAsset Asset { get; set; } = new();
    public ArtworkBrand? Brand { get; set; }
    public ArtworkVersion? ActualVersion { get; set; }
    public ArtworkVersion? PendingVersion { get; set; }
    public List<ArtworkVersion> History { get; set; } = new();
}
