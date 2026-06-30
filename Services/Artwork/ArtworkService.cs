using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models.Artwork;
using NordicBeesERP.Services;
using System.Security.Cryptography;

namespace NordicBeesERP.Services.Artwork;

public class ArtworkService : IArtworkService
{
    private readonly NordicBeesERPContext _context;
    private readonly IAuthService _authService;

    public ArtworkService(NordicBeesERPContext context, IAuthService authService)
    {
        _context = context;
        _authService = authService;
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

    public async Task<ArtworkVersionUploadResult> UploadVersionAsync(int assetId, string changeDescription, string base64, string fileName, long fileSize, string fileType)
    {
        // Validate change description
        if (string.IsNullOrWhiteSpace(changeDescription))
            return new ArtworkVersionUploadResult { Success = false, Message = "Change description is required." };
        if (changeDescription.Length > 500)
            return new ArtworkVersionUploadResult { Success = false, Message = "Change description must be ≤ 500 characters." };

        // Decode base64 to bytes
        byte[] fileBytes;
        try
        {
            fileBytes = Convert.FromBase64String(base64);
        }
        catch
        {
            return new ArtworkVersionUploadResult { Success = false, Message = "Invalid file data." };
        }

        // Compute SHA-256
        string sha256 = Convert.ToBase64String(SHA256.HashData(fileBytes));

        // Check for duplicate SHA-256 for this asset
        var existingVersion = await _context.ArtworkVersions
            .Where(v => v.AssetId == assetId && v.FileSha256 == sha256)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync();

        if (existingVersion != null)
        {
            return new ArtworkVersionUploadResult
            {
                Success = false,
                IsDuplicate = true,
                Message = $"Duplicate file detected. Same content as version #{existingVersion.VersionNumber} ({existingVersion.UploadedAt:yyyy-MM-dd HH:mm})."
            };
        }

        // Get current user ID
        var userId = await _authService.GetUserIdAsync();
        if (!userId.HasValue)
            return new ArtworkVersionUploadResult { Success = false, Message = "User not authenticated." };

        // Determine next version number
        var latestVersion = await _context.ArtworkVersions
            .Where(v => v.AssetId == assetId)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync();

        int nextVersionNumber = (latestVersion?.VersionNumber ?? 0) + 1;

        // Save version record
        var newVersion = new ArtworkVersion
        {
            AssetId = assetId,
            VersionNumber = nextVersionNumber,
            FileType = fileType,
            FilePath = $"/artwork/{assetId}/v{nextVersionNumber}/{fileName}",
            OriginalFilename = fileName,
            FileSizeBytes = fileSize,
            FileSha256 = sha256,
            ChangeDescription = changeDescription,
            Status = "pending",
            UploadedBy = userId.Value
        };

        _context.ArtworkVersions.Add(newVersion);
        await _context.SaveChangesAsync();

        return new ArtworkVersionUploadResult
        {
            Success = true,
            Version = newVersion,
            Message = $"Version {nextVersionNumber} uploaded successfully."
        };
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
