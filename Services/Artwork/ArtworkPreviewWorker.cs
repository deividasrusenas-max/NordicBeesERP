using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NordicBeesERP.Data;
using NordicBeesERP.Models.Artwork;
using NordicBeesERP.Services.Artwork;

namespace NordicBeesERP.Services.Artwork;

public class ArtworkPreviewWorker : BackgroundService
{
    private readonly ILogger<ArtworkPreviewWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;
    private readonly ArtworkPreviewOptions _options;

    public ArtworkPreviewWorker(
        ILogger<ArtworkPreviewWorker> logger,
        IServiceScopeFactory scopeFactory,
        IDbContextFactory<NordicBeesERPContext> dbFactory,
        IOptions<ArtworkPreviewOptions> options)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _dbFactory = dbFactory;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Artwork Preview Worker starting at {Time}", DateTimeOffset.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingVersionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Artwork Preview Worker iteration");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        _logger.LogInformation("Artwork Preview Worker stopping at {Time}", DateTimeOffset.Now);
    }

    private async Task ProcessPendingVersionsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var storageService = scope.ServiceProvider.GetRequiredService<IArtworkStorageService>();
        using var context = _dbFactory.CreateDbContext();

        var pendingVersions = await context.ArtworkVersions
            .Where(v => v.Status == "pending" && (v.PreviewPath == null || v.ThumbnailPath == null))
            .OrderBy(v => v.UploadedAt)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        if (!pendingVersions.Any())
        {
            _logger.LogInformation("No pending artwork versions for preview generation");
            return;
        }

        _logger.LogInformation("Processing {Count} artwork versions for preview generation", pendingVersions.Count);

        foreach (var version in pendingVersions)
        {
            try
            {
                await GeneratePreviewsForVersionAsync(version, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate previews for version {VersionId}", version.Id);
                // Don't fail the whole batch - just log and continue
            }
        }
    }

    private async Task GeneratePreviewsForVersionAsync(ArtworkVersion version, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var storageService = scope.ServiceProvider.GetRequiredService<IArtworkStorageService>();

        // Check if file exists
        if (!await storageService.FileExistsAsync(version.FilePath))
        {
            _logger.LogWarning("Source file not found for version {VersionId}: {FilePath}", version.Id, version.FilePath);
            return;
        }

        // Generate preview and thumbnail paths
        var directory = Path.GetDirectoryName(version.FilePath);
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(Path.GetFileName(version.FilePath));
        
        var previewPath = Path.Combine(directory!, $"{fileNameWithoutExt}_preview.png");
        var thumbnailPath = Path.Combine(directory!, $"{fileNameWithoutExt}_thumb.png");

        // Use Ghostscript to generate previews
        var sourcePath = Path.Combine(storageService.GetStorageRoot(), version.FilePath);
        
        try
        {
            // Generate thumbnail (400px wide)
            var thumbRawPath = Path.Combine(Path.GetTempPath(), $"thumb_raw_{version.Id}.png");
            await RunGhostscriptAsync(sourcePath, thumbRawPath, "-r72", cancellationToken);
            
            // Downscale to 400px width (simplified - in production use ImageSharp)
            // For MVP, we'll just copy the raw thumbnail
            File.Copy(thumbRawPath, Path.Combine(storageService.GetStorageRoot(), thumbnailPath), overwrite: true);
            File.Delete(thumbRawPath);
            
            // Generate full preview (higher resolution)
            var previewRawPath = Path.Combine(Path.GetTempPath(), $"preview_raw_{version.Id}.png");
            await RunGhostscriptAsync(sourcePath, previewRawPath, "-r150", cancellationToken);
            File.Copy(previewRawPath, Path.Combine(storageService.GetStorageRoot(), previewPath), overwrite: true);
            File.Delete(previewRawPath);

            // Update database
            version.PreviewPath = previewPath;
            version.ThumbnailPath = thumbnailPath;
            await _dbFactory.CreateDbContext().SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Generated previews for version {VersionId}", version.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate previews for version {VersionId}", version.Id);
            // Leave preview paths null - UI will show placeholder
        }
    }

    private async Task RunGhostscriptAsync(string inputPath, string outputPath, string resolution, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "gs",
            Arguments = $"-dNOPAUSE -dBATCH -sDEVICE=png16m {resolution} -dFirstPage=1 -dLastPage=1 -o \"{outputPath}\" \"{inputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();
        
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        
        await process.WaitForExitAsync(cancellationToken);
        
        if (process.ExitCode != 0)
        {
            var error = await errorTask;
            throw new InvalidOperationException($"Ghostscript failed with exit code {process.ExitCode}: {error}");
        }
    }
}

public class ArtworkPreviewOptions
{
    public int BatchSize { get; set; } = 5;
}
