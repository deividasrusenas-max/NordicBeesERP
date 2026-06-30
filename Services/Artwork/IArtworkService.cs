using NordicBeesERP.Models.Artwork;

namespace NordicBeesERP.Services.Artwork;

public interface IArtworkService
{
    Task<List<ArtworkBrandWithCounts>> GetBrandsWithCountsAsync();
    Task<ArtworkBrand?> GetBrandByIdAsync(int id);
    Task<List<ArtworkAssetWithSummary>> GetAssetsByBrandAsync(int brandId);
    Task<ArtworkVersionUploadResult> UploadVersionAsync(int assetId, string changeDescription, string base64, string fileName, long fileSize, string fileType);
}

public class ArtworkVersionUploadResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public ArtworkVersion? Version { get; set; }
    public bool IsDuplicate { get; set; }
}
