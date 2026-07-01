using Microsoft.Extensions.Options;
using NordicBeesERP.Data;
using NordicBeesERP.Models.Artwork;

namespace NordicBeesERP.Services.Artwork;

public interface IArtworkStorageService
{
    Task<string> SaveFileAsync(string brandSlug, int assetId, int versionNumber, string originalFilename, Stream fileStream, CancellationToken cancellationToken = default);
    Task<(string filePath, string previewPath, string thumbnailPath)> SaveFileWithPreviewsAsync(string brandSlug, int assetId, int versionNumber, string originalFilename, Stream fileStream, CancellationToken cancellationToken = default);
    Task<Stream> GetFileStreamAsync(string filePath);
    Task<bool> FileExistsAsync(string filePath);
    Task DeleteFileAsync(string filePath);
    string GetStorageRoot();
}

public class ArtworkStorageService : IArtworkStorageService
{
    private readonly string _storageRoot;
    private readonly ILogger<ArtworkStorageService> _logger;

    public ArtworkStorageService(IOptions<ArtworkStorageOptions> options, ILogger<ArtworkStorageService> logger)
    {
        _storageRoot = options.Value.StorageRoot ?? throw new InvalidOperationException("StorageRoot is not configured");
        _logger = logger;
    }

    public string GetStorageRoot() => _storageRoot;

    public async Task<string> SaveFileAsync(string brandSlug, int assetId, int versionNumber, string originalFilename, Stream fileStream, CancellationToken cancellationToken = default)
    {
        var relativePath = GetRelativePath(brandSlug, assetId, versionNumber, originalFilename);
        var fullPath = Path.Combine(_storageRoot, relativePath);
        
        // Ensure directory exists
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Stream the file
        using var fileStreamOut = File.Create(fullPath);
        await fileStream.CopyToAsync(fileStreamOut, cancellationToken);
        
        _logger.LogInformation("Saved artwork file to {Path}", fullPath);
        
        return relativePath;
    }

    public async Task<(string filePath, string previewPath, string thumbnailPath)> SaveFileWithPreviewsAsync(string brandSlug, int assetId, int versionNumber, string originalFilename, Stream fileStream, CancellationToken cancellationToken = default)
    {
        var relativePath = GetRelativePath(brandSlug, assetId, versionNumber, originalFilename);
        var fullPath = Path.Combine(_storageRoot, relativePath);
        
        // Ensure directory exists
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Stream the file
        using var fileStreamOut = File.Create(fullPath);
        await fileStream.CopyToAsync(fileStreamOut, cancellationToken);
        
        // Preview and thumbnail paths
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(originalFilename);
        var previewPath = Path.Combine(Path.GetDirectoryName(relativePath)!, $"{fileNameWithoutExt}_preview.png");
        var thumbnailPath = Path.Combine(Path.GetDirectoryName(relativePath)!, $"{fileNameWithoutExt}_thumb.png");
        
        _logger.LogInformation("Saved artwork file to {Path}", fullPath);
        
        return (relativePath, previewPath, thumbnailPath);
    }

    public async Task<Stream> GetFileStreamAsync(string filePath)
    {
        var fullPath = Path.Combine(_storageRoot, filePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {fullPath}", filePath);
        }
        
        return File.OpenRead(fullPath);
    }

    public Task<bool> FileExistsAsync(string filePath)
    {
        var fullPath = Path.Combine(_storageRoot, filePath);
        return Task.FromResult(File.Exists(fullPath));
    }

    public async Task DeleteFileAsync(string filePath)
    {
        var fullPath = Path.Combine(_storageRoot, filePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("Deleted artwork file: {Path}", fullPath);
        }
    }

    private string GetRelativePath(string brandSlug, int assetId, int versionNumber, string originalFilename)
    {
        // Path convention: {brand_slug}/{asset_id}/v{version_number}/{original_filename}
        return Path.Combine(brandSlug, assetId.ToString(), $"v{versionNumber}", originalFilename);
    }
}

public class ArtworkStorageOptions
{
    public string? StorageRoot { get; set; }
}
