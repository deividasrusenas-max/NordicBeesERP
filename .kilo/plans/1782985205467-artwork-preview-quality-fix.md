# Artwork Preview Quality Fix

## Task List

### 1. Thumbnail DPI: 150 → 200
**File:** `Services/Artwork/ArtworkPreviewWorker.cs`, line 112
```diff
-await RunGhostscriptAsync(sourcePath, thumbRawPath, "-r150", cancellationToken);
+await RunGhostscriptAsync(sourcePath, thumbRawPath, "-r200", cancellationToken);
```

### 2. Preview DPI: 150 → 300
**File:** `Services/Artwork/ArtworkPreviewWorker.cs`, line 121
```diff
-await RunGhostscriptAsync(sourcePath, previewRawPath, "-r150", cancellationToken);
+await RunGhostscriptAsync(sourcePath, previewRawPath, "-r300", cancellationToken);
```

### 3. Add FITPage flags for non-standard PDF sizes
**File:** `Services/Artwork/ArtworkPreviewWorker.cs`, line 145 (in `RunGhostscriptAsync`)
```diff
-Arguments = $"-dNOPAUSE -dBATCH -sDEVICE=png16m {resolution} -dFirstPage=1 -dLastPage=1 -o \"{outputPath}\" \"{inputPath}\"",
+Arguments = $"-dNOPAUSE -dBATCH -dFITPAGE -dPDFFitPage -sDEVICE=png16m {resolution} -dFirstPage=1 -dLastPage=1 -o \"{outputPath}\" \"{inputPath}\"",
```

### 4. Regenerate old PNG previews (manual step — no code changes)
Existing low-quality PNG previews must be regenerated at the new resolution. Options:
- **Option A (delete files):** Delete old `_preview.png` and `_thumb.png` files from storage (via FTP/file manager). The worker will regenerate them on next run.
- **Option B (DB reset):** Run `UPDATE artwork_versions SET preview_path = NULL, thumbnail_path = NULL WHERE preview_path IS NOT NULL;` to mark all versions as pending regeneration.

## Risk Assessment
- Higher DPI → larger files and longer processing time. Monitor storage and worker duration.
- FITPage flags should handle label formats (195×55mm) correctly by fitting the page to the output device resolution.

## Validation
- Upload a standard PDF — verify preview and thumbnail render clearly at 200/300 DPI.
- Upload a label-size PDF (e.g. 195×55mm) — verify no cropping/overflow with FITPage flags.
- Check generated file sizes are reasonable (not excessively large).

## Version bump
After changes: run `./bump-version.sh patch`
