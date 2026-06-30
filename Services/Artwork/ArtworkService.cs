using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models.Artwork;

namespace NordicBeesERP.Services.Artwork;

public class ArtworkService : IArtworkService
{
    private readonly NordicBeesERPContext _context;

    public ArtworkService(NordicBeesERPContext context)
    {
        _context = context;
    }

    public async Task<List<ArtworkBrandWithCounts>> GetBrandsWithCountsAsync()
    {
        return await _context.ArtworkBrands
            .Where(b => b.IsActive)
            .Select(b => new ArtworkBrandWithCounts
            {
                Id = b.Id,
                Name = b.Name,
                AssetsCount = b.ArtworkAssets.Count,
                PendingCount = b.ArtworkAssets.SelectMany(a => a.ArtworkVersions).Count(v => v.Status == "pending")
            })
            .ToListAsync();
    }

    public async Task<ArtworkBrand?> GetBrandByIdAsync(int id)
    {
        return await _context.ArtworkBrands.FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<List<ArtworkAssetWithSummary>> GetAssetsByBrandAsync(int brandId)
    {
        return await _context.ArtworkAssets
            .Where(a => a.BrandId == brandId && a.Status == "active")
            .Select(a => new ArtworkAssetWithSummary
            {
                Id = a.Id,
                Name = a.Name,
                AssetType = a.AssetType,
                Status = a.Status,
                ActualVersionNumber = a.ArtworkVersions
                    .Where(v => v.Status == "approved")
                    .OrderByDescending(v => v.VersionNumber)
                    .Select(v => v.VersionNumber)
                    .FirstOrDefault(),
                HasPendingVersion = a.ArtworkVersions.Any(v => v.Status == "pending")
            })
            .ToListAsync();
    }
}

public class ArtworkBrandWithCounts
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int AssetsCount { get; set; }
    public int PendingCount { get; set; }
}

public class ArtworkAssetWithSummary
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string AssetType { get; set; } = "";
    public string Status { get; set; } = "";
    public int? ActualVersionNumber { get; set; }
    public bool HasPendingVersion { get; set; }
}
