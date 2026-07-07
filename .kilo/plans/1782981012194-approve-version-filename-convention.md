# ApproveVersionAsync Filename Convention

## Goal
When an artwork version is approved, rename the physical PDF file on disk and update `file_path` + `original_filename` in DB to follow: `LK{assetId:D4}-{brandSlug}-v{versionNumber}-{approvalDate:yyyyMMdd}.pdf`

Example: `LK0001-nordic-bees-v2-20260702.pdf`

---

## Task List

### 1. Inject `IConfiguration` into `ArtworkService`

**File:** `Services/Artwork/ArtworkService.cs`

- Add `using Microsoft.Extensions.Configuration;` to imports
- Add field: `private readonly IConfiguration _configuration;`
- Add constructor parameter and assignment: `_configuration = configuration;`

### 2. Add filename rename logic in `ApproveVersionAsync`

**File:** `Services/Artwork/ArtworkService.cs`, after the approval audit log insert (line 92)

Insert this block. Note: `ArtworkAsset` has `[NotMapped]` navigation property, so per standards (`Include()` does NOT work) we load asset and brand separately via `FindAsync`:

```csharp
// Rename approved file on disk and update DB with new filename
var approvedVersion = await _context.ArtworkVersions.FindAsync(versionId);
if (approvedVersion != null && !string.IsNullOrEmpty(approvedVersion.FilePath))
{
    var asset = await _context.ArtworkAssets.FindAsync(approvedVersion.AssetId);
    if (asset != null)
    {
        var brand = await _context.ArtworkBrands.FindAsync(asset.BrandId);
        if (brand != null && !string.IsNullOrEmpty(brand.Slug))
        {
            var approvalDate = DateTime.UtcNow.ToString("yyyyMMdd");
            var newFilename = $"LK{asset.Id:D4}-{brand.Slug}-v{approvedVersion.VersionNumber}-{approvalDate}.pdf";

            var storageRoot = _configuration["ArtworkStorage:StorageRoot"] ?? _storageService.GetStorageRoot();
            var oldFullPath = Path.Combine(storageRoot, approvedVersion.FilePath.TrimStart('/'));
            var directory = Path.GetDirectoryName(oldFullPath)!;
            var newFullPath = Path.Combine(directory, newFilename);

            if (File.Exists(oldFullPath))
            {
                File.Move(oldFullPath, newFullPath);
            }

            var newRelativePath = Path.Combine(
                Path.GetDirectoryName(approvedVersion.FilePath.TrimStart('/'))!,
                newFilename).Replace('\\', '/');

            await _context.Database.ExecuteSqlRawAsync(
                "UPDATE artwork_versions SET file_path = @p0, original_filename = @p1 WHERE id = @p2",
                newRelativePath, newFilename, versionId);
        }
    }
}
```

### 3. Run version bump

```bash
./bump-version.sh patch
```

Changes version from v0.9.3.15 → v0.9.3.16.

---

## Key Decisions

- **Navigation properties:** Per project standards, `ArtworkAsset` has `[NotMapped]` on navigation properties and `Include()` does NOT work. Load via separate `FindAsync` calls.
- **Config fallback:** `IConfiguration["ArtworkStorage:StorageRoot"]` takes precedence; falls back to `_storageService.GetStorageRoot()`.
- **Guard rails:** Only proceed if `brand.Slug` is non-empty (avoids `LK0001--v2.pdf`). File existence check guards against missing disk files.
- **File extension:** Hardcoded `.pdf` in the convention as per the spec. All artwork versions are PDFs.
- **Per standards:** Uses `ExecuteSqlRawAsync` (not `FindAsync` + modify + `SaveChangesAsync`).

---

## Validation

- `dotnet build` — compile passes
- Approve an existing pending version → verify disk file renamed to new convention
- Query DB → verify `file_path` and `original_filename` updated
- Revert an approved version → old behavior unaffected (rename only on approval)
