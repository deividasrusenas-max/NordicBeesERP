using NordicBeesERP.Models.Artwork;

namespace NordicBeesERP.Services.Artwork;

public interface IArtworkService
{
    Task<List<ArtworkBrandWithCounts>> GetBrandsWithCountsAsync();
    Task<ArtworkBrand?> GetBrandByIdAsync(int id);
    Task<List<ArtworkAssetWithSummary>> GetAssetsByBrandAsync(int brandId);
    Task<ArtworkVersionUploadResult> UploadVersionAsync(int assetId, string changeDescription, string base64, string fileName, long fileSize, string fileType);
    
    // Asset Detail & Workflow
    Task<ArtworkAssetDetailDto> GetAssetDetailAsync(int assetId);
    Task ApproveVersionAsync(int versionId, int userId);
    Task RejectVersionAsync(int versionId, int userId, string comment);
    Task<List<ArtworkComment>> GetCommentsAsync(int versionId);
    Task AddCommentAsync(int versionId, int userId, string body);
    Task ArchiveAssetAsync(int assetId, int userId);
}

public class ArtworkVersionUploadResult
{
    public bool Success { get; set; }
    public bool IsDuplicate { get; set; }
    public string? Message { get; set; }
    public ArtworkVersion? Version { get; set; }
}

public class ArtworkAssetDetailDto
{
    public ArtworkAsset Asset { get; set; } = new();
    public ArtworkBrand? Brand { get; set; }
    public ArtworkVersion? ActualVersion { get; set; }
    public ArtworkVersion? PendingVersion { get; set; }
    public List<ArtworkVersion> History { get; set; } = new();
}
