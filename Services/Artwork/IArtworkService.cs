using NordicBeesERP.Models.Artwork;

namespace NordicBeesERP.Services.Artwork;

public interface IArtworkService
{
    Task<List<ArtworkBrandWithCounts>> GetBrandsWithCountsAsync();
    Task<ArtworkBrand?> GetBrandByIdAsync(int id);
    Task<List<ArtworkAssetWithSummary>> GetAssetsByBrandAsync(int brandId);
}
