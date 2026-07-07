# Fix Artwork File Download

## Problem
Files are stored on disk at `/var/lib/nordicbees/artwork/` (not web-accessible). Current code uses `JS.InvokeVoidAsync("open", $"/{version.FilePath}", "_blank")` which relies on static file serving and fails.

## Changes

### 1. Add download endpoint to `Program.cs` (after line 136)

```csharp
app.MapGet("/artwork/download/{versionId:int}", async (int versionId,
    IDbContextFactory<NordicBeesERPContext> dbFactory,
    IConfiguration config) =>
{
    await using var ctx = await dbFactory.CreateDbContextAsync();
    var version = await ctx.ArtworkVersions.FindAsync(versionId);
    if (version == null) return Results.NotFound();

    var root = config["ArtworkStorage:StorageRoot"] ?? "/var/lib/nordicbees/artwork";
    var relativePath = Path.Combine(version.Asset.BrandSlug, version.Asset.Id.ToString(),
        $"v{version.VersionNumber}", version.OriginalFilename);
    var fullPath = Path.Combine(root, relativePath);

    if (!File.Exists(fullPath)) return Results.NotFound();

    var contentType = version.FileType switch
    {
        "pdf" => "application/pdf",
        _ => "application/octet-stream"
    };

    var stream = File.OpenRead(fullPath);
    return Results.File(stream, contentType, version.OriginalFilename);
})
.RequireAuthorization("ArtworkAccess");
```

Key details:
- Uses `ArtworkStorage:StorageRoot` (not `ArtworkStorage:Root`)
- Reconstructs correct relative path using `brandSlug` + `assetId` + `versionNumber` + `originalFilename` (matches `ArtworkStorageService.GetRelativePath`)
- Uses `await using var ctx` per project standards
- Uses `FindAsync` on `ArtworkVersions` which loads `Asset` navigation property via EF Core relationship
- Adds `RequireAuthorization("ArtworkAccess")` for security
- Content type based on `FileType` field

### 2. Fix `ArtworkAssetDetail.razor` — `DownloadCurrentVersion` and `DownloadVersion`

Replace JS interop with `NavigationManager.NavigateTo`:

```csharp
@inject NavigationManager Navigation
```

```csharp
private async Task DownloadCurrentVersion()
{
    if (_currentVersion != null)
    {
        NavigationManager.NavigateTo($"/artwork/download/{_currentVersion.Id}", forceLoad: true);
    }
}

private async Task DownloadVersion(int versionId)
{
    NavigationManager.NavigateTo($"/artwork/download/{versionId}", forceLoad: true);
}
```

Remove `@inject IJSRuntime JS` (no longer needed).

### 3. Run version bump

```bash
./bump-version.sh patch
```

## Validation
- Navigate to any artwork asset detail page
- Click "Atsisiųsti" (download current version) — file should download
- Click download icon on any version in history — file should download
- 404 for non-existent version IDs
