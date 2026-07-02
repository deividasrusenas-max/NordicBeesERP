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
    private readonly IArtworkStorageService _storageService;

    public ArtworkService(NordicBeesERPContext context, IAuthService authService, IArtworkStorageService storageService)
    {
        _context = context;
        _authService = authService;
        _storageService = storageService;
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

        var history = allVersions.ToList();

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
        var userId = await _authService.GetUserIdAsync();

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
            await _context.Database.ExecuteSqlRawAsync(
                "INSERT INTO artwork_audit_log (entity_type, entity_id, action, user_id, details, created_at) VALUES (@p0, @p1, @p2, @p3, @p4, @p5)",
                "version", oldVersion.Id, "STATUS_CHANGED", userId.Value, $"Superseded by version {versionToApprove.VersionNumber} (approved→superseded)", currentTimestamp);
        }

        // Insert approval audit
        await _context.Database.ExecuteSqlRawAsync(
            "INSERT INTO artwork_audit_log (entity_type, entity_id, action, user_id, details, created_at) VALUES (@p0, @p1, @p2, @p3, @p4, @p5)",
            "version", versionId, "APPROVED", userId.Value, $"Approved by reviewer ID {reviewerId} (pending→approved)", currentTimestamp);
    }

    public async Task RejectVersionAsync(int versionId, int reviewerId, string comment)
    {
        var version = await _context.ArtworkVersions.FindAsync(versionId);
        if (version == null)
            throw new ArgumentException($"Version with ID {versionId} not found.");

        var oldStatus = version.Status;

        // Update version status
        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE artwork_versions SET status = @p0, review_comment = @p1, reviewed_by = @p2, reviewed_at = @p3 WHERE id = @p4",
            "rejected", comment, reviewerId, DateTime.UtcNow, versionId);

        var user = await _authService.GetAuthenticatedUserAsync();
        var performedBy = user?.FullName ?? user?.Email ?? "system";
        var userId = await _authService.GetUserIdAsync();

        await _context.Database.ExecuteSqlRawAsync(
            "INSERT INTO artwork_audit_log (entity_type, entity_id, action, user_id, details, created_at) VALUES (@p0, @p1, @p2, @p3, @p4, @p5)",
            "version", versionId, "REJECTED", userId.Value, $"Rejected by reviewer ID {reviewerId} ({oldStatus}→rejected): {comment}", DateTime.UtcNow);
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

        await _context.Database.ExecuteSqlRawAsync(
            "INSERT INTO artwork_comments (version_id, user_id, body, created_at) VALUES (@p0, @p1, @p2, @p3)",
            versionId, userId, body, DateTime.UtcNow);
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

    public async Task<int> CreateAssetAsync(int brandId, string name, string type, string? description, int userId)
    {
        await _context.Database.ExecuteSqlRawAsync(
            "INSERT INTO artwork_assets (brand_id, name, asset_type, description, status, created_by, created_at) VALUES (@p0, @p1, @p2, @p3, 'active', @p4, NOW())",
            brandId, name, type, description ?? "", userId);

        var result = await _context.Database.SqlQueryRaw<int>("SELECT LAST_INSERT_ID() as Value").FirstAsync();
        return result;
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

        // Get brand slug for file path
        var asset = await _context.ArtworkAssets.FindAsync(assetId);
        if (asset == null)
            return new ArtworkVersionUploadResult { Success = false, Message = "Asset not found." };

        var brand = await _context.ArtworkBrands.FindAsync(asset.BrandId);
        if (brand == null)
            return new ArtworkVersionUploadResult { Success = false, Message = "Brand not found." };

        // Save file to disk
        using var stream = new MemoryStream(fileBytes);
        var relativePath = await _storageService.SaveFileAsync(brand.Slug, assetId, nextVersionNumber, fileName, stream);

        // Save version record
        var newVersion = new ArtworkVersion
        {
            AssetId = assetId,
            VersionNumber = nextVersionNumber,
            FileType = fileType,
            FilePath = relativePath,
            OriginalFilename = fileName,
            FileSizeBytes = fileSize,
            FileSha256 = sha256,
            ChangeDescription = changeDescription,
            Status = "pending",
            UploadedBy = userId.Value
        };

        await _context.Database.ExecuteSqlRawAsync(
            "INSERT INTO artwork_versions (asset_id, version_number, file_type, file_path, original_filename, file_size_bytes, file_sha256, change_description, status, uploaded_by, uploaded_at) VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, 'pending', @p8, @p9)",
            assetId, nextVersionNumber, fileType, relativePath, fileName, fileSize, sha256, changeDescription, userId.Value, DateTime.UtcNow);

        var versionId = await _context.Database.SqlQueryRaw<int>("SELECT LAST_INSERT_ID() as Value").FirstAsync();

        var resultVersion = new ArtworkVersion
        {
            Id = versionId,
            AssetId = assetId,
            VersionNumber = nextVersionNumber,
            FileType = fileType,
            FilePath = relativePath,
            OriginalFilename = fileName,
            FileSizeBytes = fileSize,
            FileSha256 = sha256,
            ChangeDescription = changeDescription,
            Status = "pending",
            UploadedBy = userId.Value,
            UploadedAt = DateTime.UtcNow
        };

        return new ArtworkVersionUploadResult
        {
            Success = true,
            Version = resultVersion,
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