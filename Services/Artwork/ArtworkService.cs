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

    public async Task<ArtworkAssetDetailDto> GetAssetDetailAsync(int assetId)
    {
        var asset = await _context.ArtworkAssets
            .FirstOrDefaultAsync(a => a.Id == assetId);
        if (asset == null)
            return new ArtworkAssetDetailDto { Asset = new ArtworkAsset() };

        var brand = await _context.ArtworkBrands
            .FirstOrDefaultAsync(b => b.Id == asset.BrandId);

        var allVersions = await _context.ArtworkVersions
            .Where(v => v.AssetId == assetId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync();

        var actualVersion = allVersions.FirstOrDefault(v => v.Status == "approved");
        var pendingVersion = allVersions.FirstOrDefault(v => v.Status == "pending");

        var history = allVersions
            .Where(v => v.Status != "pending" && v.Id != (actualVersion?.Id ?? 0))
            .ToList();

        return new ArtworkAssetDetailDto
        {
            Asset = asset,
            Brand = brand,
            ActualVersion = actualVersion,
            PendingVersion = pendingVersion,
            History = history
        };
    }

    public async Task ApproveVersionAsync(int versionId, int reviewerId)
    {
        // Get the version to approve
        var versionToApprove = await _context.ArtworkVersions.FindAsync(versionId);
        if (versionToApprove == null)
            throw new ArgumentException($"Version with ID {versionId} not found.");

        // Get current user
        var user = await _authService.GetAuthenticatedUserAsync();
        var performedBy = user?.FullName ?? user?.Email ?? "system";

        var currentTimestamp = DateTime.UtcNow;

        // Find all currently approved versions for this asset
        var approvedVersions = await _context.ArtworkVersions
            .Where(v => v.AssetId == versionToApprove.AssetId && v.Status == "approved")
            .ToListAsync();

        // Supersede old approved versions
        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE artwork_versions SET status = @p0, uploaded_at = @p1 WHERE asset_id = @p2 AND status = @p3",
            "superseded", currentTimestamp, versionToApprove.AssetId, "approved");

        // Approve the new version
        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE artwork_versions SET status = @p0, uploaded_at = @p1 WHERE id = @p2",
            "approved", currentTimestamp, versionId);

        // Insert audit logs for superseding
        foreach (var oldVersion in approvedVersions)
        {
            _context.ArtworkVersionAudits.Add(new ArtworkVersionAudit
            {
                VersionId = oldVersion.Id,
                Action = "STATUS_CHANGED",
                ActionDetails = $"Superseded by version {versionToApprove.VersionNumber}",
                OldStatus = "approved",
                NewStatus = "superseded",
                PerformedBy = performedBy,
                PerformedAt = currentTimestamp
            });
        }

        // Insert approval audit
        _context.ArtworkVersionAudits.Add(new ArtworkVersionAudit
        {
            VersionId = versionId,
            Action = "APPROVED",
            ActionDetails = $"Approved by reviewer ID {reviewerId}",
            OldStatus = "pending",
            NewStatus = "approved",
            PerformedBy = performedBy,
            PerformedAt = currentTimestamp
        });

        await _context.SaveChangesAsync();
    }

    public async Task RejectVersionAsync(int versionId, int reviewerId, string comment)
    {
        var version = await _context.ArtworkVersions.FindAsync(versionId);
        if (version == null)
            throw new ArgumentException($"Version with ID {versionId} not found.");

        var oldStatus = version.Status;

        // Update version status
        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE artwork_versions SET status = @p0 WHERE id = @p1",
            "rejected", versionId);

        var user = await _authService.GetAuthenticatedUserAsync();
        var performedBy = user?.FullName ?? user?.Email ?? "system";

        var audit = new ArtworkVersionAudit
        {
            VersionId = versionId,
            Action = "REJECTED",
            ActionDetails = $"Rejected by reviewer ID {reviewerId}: {comment}",
            OldStatus = oldStatus,
            NewStatus = "rejected",
            PerformedBy = performedBy,
            PerformedAt = DateTime.UtcNow
        };
        _context.ArtworkVersionAudits.Add(audit);

        await _context.SaveChangesAsync();
    }

    public async Task<List<ArtworkComment>> GetCommentsAsync(int versionId)
    {
        return await _context.ArtworkComments
            .Where(c => c.VersionId == versionId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task AddCommentAsync(int versionId, int userId, string body)
    {
        var version = await _context.ArtworkVersions.FindAsync(versionId);
        if (version == null)
            throw new ArgumentException($"Version with ID {versionId} not found.");

        var comment = new ArtworkComment
        {
            VersionId = versionId,
            UserId = userId,
            Body = body,
            CreatedAt = DateTime.UtcNow
        };

        _context.ArtworkComments.Add(comment);
        await _context.SaveChangesAsync();
    }

    public async Task ArchiveAssetAsync(int assetId, int userId)
    {
        var asset = await _context.ArtworkAssets.FindAsync(assetId);
        if (asset == null)
            throw new ArgumentException($"Asset with ID {assetId} not found.");

        // Update asset status
        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE artwork_assets SET status = @p0 WHERE id = @p1",
            "archived", assetId);

        // Also archive all versions of this asset
        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE artwork_versions SET status = @p0 WHERE asset_id = @p1",
            "archived", assetId);
    }

    public async Task<List<ArtworkBrandWithCounts>> GetBrandsWithCountsAsync()
    {
        var brands = await _context.ArtworkBrands
            .Where(b => b.IsActive)
            .ToListAsync();

        var result = new List<ArtworkBrandWithCounts>();
        foreach (var brand in brands)
        {
            var assets = await _context.ArtworkAssets
                .Where(a => a.BrandId == brand.Id)
                .ToListAsync();

            var assetsCount = assets.Count;
            var pendingCount = 0;

            foreach (var asset in assets)
            {
                var assetVersions = await _context.ArtworkVersions
                    .Where(v => v.AssetId == asset.Id && v.Status == "pending")
                    .ToListAsync();
                pendingCount += assetVersions.Count;
            }

            result.Add(new ArtworkBrandWithCounts
            {
                Id = brand.Id,
                Name = brand.Name,
                AssetsCount = assetsCount,
                PendingCount = pendingCount
            });
        }

        return result;
    }

    public async Task<ArtworkBrand?> GetBrandByIdAsync(int id)
    {
        return await _context.ArtworkBrands.FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<List<ArtworkAssetWithSummary>> GetAssetsByBrandAsync(int brandId)
    {
        var assets = await _context.ArtworkAssets
            .Where(a => a.BrandId == brandId && a.Status == "active")
            .ToListAsync();

        var result = new List<ArtworkAssetWithSummary>();
        foreach (var asset in assets)
        {
            var approvedVersions = await _context.ArtworkVersions
                .Where(v => v.AssetId == asset.Id && v.Status == "approved")
                .OrderByDescending(v => v.VersionNumber)
                .ToListAsync();

            var pendingVersions = await _context.ArtworkVersions
                .Where(v => v.AssetId == asset.Id && v.Status == "pending")
                .ToListAsync();

            var actualVersionNumber = approvedVersions.Any()
                ? approvedVersions.First().VersionNumber
                : (int?)null;

            result.Add(new ArtworkAssetWithSummary
            {
                Id = asset.Id,
                Name = asset.Name,
                AssetType = asset.AssetType,
                Status = asset.Status,
                ActualVersionNumber = actualVersionNumber,
                HasPendingVersion = pendingVersions.Any()
            });
        }

        return result;
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