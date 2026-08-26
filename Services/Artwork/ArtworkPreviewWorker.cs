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
            .Where(v => v.PreviewPath == null || v.ThumbnailPath == null)
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

    private const int MaxRetries = 3;
    private readonly Dictionary<int, int> _retryCount = new();

    private async Task GeneratePreviewsForVersionAsync(ArtworkVersion version, CancellationToken cancellationToken)
    {
        // Skip permanently-failed rows after MaxRetries consecutive failures.
        if (_retryCount.TryGetValue(version.Id, out var priorFailures) && priorFailures >= MaxRetries)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var storageService = scope.ServiceProvider.GetRequiredService<IArtworkStorageService>();

        var normalizedFilePath = NormalizeRelativePath(version.FilePath);
        if (!await storageService.FileExistsAsync(normalizedFilePath))
        {
            _logger.LogWarning("Source file not found for version {VersionId}: {FilePath}", version.Id, version.FilePath);
            RecordFailure(version.Id, "Source file not found");
            return;
        }

        // Generate preview and thumbnail paths (relative to storage root).
        var directory = Path.GetDirectoryName(normalizedFilePath);
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(Path.GetFileName(normalizedFilePath));

        var previewPath = Path.Combine(directory!, $"{fileNameWithoutExt}_preview.png");
        var thumbnailPath = Path.Combine(directory!, $"{fileNameWithoutExt}_thumb.png");

        // Use Ghostscript to generate previews.
        var sourcePath = Path.Combine(storageService.GetStorageRoot(), normalizedFilePath);

        var thumbRawPath = Path.Combine(Path.GetTempPath(), $"thumb_raw_{version.Id}.png");
        var previewRawPath = Path.Combine(Path.GetTempPath(), $"preview_raw_{version.Id}.png");

        try
        {
            await RunGhostscriptAsync(sourcePath, thumbRawPath, "-r200", cancellationToken);
            File.Copy(thumbRawPath, Path.Combine(storageService.GetStorageRoot(), thumbnailPath), overwrite: true);

            await RunGhostscriptAsync(sourcePath, previewRawPath, "-r300", cancellationToken);
            File.Copy(previewRawPath, Path.Combine(storageService.GetStorageRoot(), previewPath), overwrite: true);

            // Update database via ExecuteSqlRawAsync because NoTracking is configured globally on the context.
            await using var ctx = _dbFactory.CreateDbContext();
            await ctx.Database.ExecuteSqlRawAsync(
                "UPDATE artwork_versions SET preview_path = @p0, thumbnail_path = @p1 WHERE id = @p2",
                previewPath, thumbnailPath, version.Id);

            _retryCount.Remove(version.Id);
            _logger.LogInformation("Generated previews for version {VersionId}", version.Id);
        }
        catch (Exception ex)
        {
            RecordFailure(version.Id, ex.Message);
            _logger.LogError(ex, "Failed to generate previews for version {VersionId}", version.Id);
            // Leave preview paths null - UI will show placeholder / spinner.
        }
        finally
        {
            // Always clean up the intermediate raw files, even on failure or timeout.
            SafeDelete(thumbRawPath);
            SafeDelete(previewRawPath);
        }
    }

    private void RecordFailure(int versionId, string reason)
    {
        var count = _retryCount.TryGetValue(versionId, out var c) ? c + 1 : 1;
        _retryCount[versionId] = count;
        if (count >= MaxRetries)
        {
            _logger.LogError(
                "Artwork preview generation for version {VersionId} failed {Count} times; stopping retries. Last failure: {Reason}",
                versionId, count, reason);
        }
    }

    private static void SafeDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }

    private static string NormalizeRelativePath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? string.Empty : path.TrimStart('/', '\\');

    private const int GsTimeoutSeconds = 60;

    private async Task RunGhostscriptAsync(string inputPath, string outputPath, string resolution, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "gs",
            Arguments = $"-dNOPAUSE -dBATCH -dFITPAGE -dPDFFitPage -sDEVICE=png16m {resolution} -dFirstPage=1 -dLastPage=1 -o \"{outputPath}\" \"{inputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        // Read both streams concurrently to avoid a pipe-buffer deadlock under any output volume.
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(GsTimeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timed out (not an external shutdown request) — kill the hung process tree.
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            await Task.WhenAny(outputTask, errorTask); // best-effort drain, results ignored
            throw new TimeoutException($"Ghostscript did not exit within {GsTimeoutSeconds}s for input '{inputPath}'.");
        }

        // Ensure both streams are fully drained before inspecting the exit code.
        await Task.WhenAll(outputTask, errorTask);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Ghostscript failed with exit code {process.ExitCode}: {errorTask.Result}");
        }
    }
}

public class ArtworkPreviewOptions
{
    public int BatchSize { get; set; } = 5;
}
