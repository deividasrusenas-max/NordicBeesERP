# ArtworkUpload Route Fix Plan

## Goal
Allow `/artwork/upload` to be accessed without an `AssetId` parameter. When accessed without `AssetId`, the page redirects to `/artwork`.

## Changes

### File: `Components/Pages/Artwork/ArtworkUpload.razor`

1. **Route change** (line 1):
   - Replace: `@page "/artwork/upload/{AssetId:int}"`
   - With two routes:
     ```razor
     @page "/artwork/upload"
     @page "/artwork/upload/{AssetId:int}"
     ```

2. **Add injection** (after line 6):
   - Add: `@inject NavigationManager Nav`

3. **AssetId parameter** (line 49):
   - Replace: `[Parameter] public int AssetId { get; set; }`
   - With: `[Parameter] public int? AssetId { get; set; }`

4. **OnParametersSetAsync** (lines 59-63):
   - Guard: if `AssetId` is null, redirect to `/artwork`:
     ```csharp
     protected override async Task OnParametersSetAsync()
     {
         if (!AssetId.HasValue)
         {
             Nav.NavigateTo("/artwork");
             return;
         }
         var brand = await ArtworkService.GetBrandByIdAsync(AssetId.Value);
         AssetName = brand?.Name ?? "Unknown Brand";
     }
     ```

5. **UploadVersionAsync calls** (lines 121-128, 185-192):
   - Add null guard before calling upload:
     ```csharp
     if (!AssetId.HasValue) return;
     ```

### Script: `./bump-version.sh`
- Run: `./bump-version.sh patch` after all changes

## Validation
- `dotnet build` succeeds
- Navigating to `/artwork/upload` redirects to `/artwork`
- Navigating to `/artwork/upload/1` loads brand name and upload works normally
