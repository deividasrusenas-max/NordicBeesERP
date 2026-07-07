# PNG Preview Display in Artwork Module

## Goal
Show PNG thumbnail previews of artwork versions in the brand asset cards and asset detail page. Clicking a thumbnail opens a full-size preview dialog.

## Tasks

### 1. Add preview endpoint in `Program.cs`
**File:** `Program.cs` — insert before `app.MapRazorComponents<App>()` (~line 150)

Add `GET /artwork/preview/{versionId:int}` endpoint:
- Look up `ArtworkVersion` by `versionId` via `IDbContextFactory<NordicBeesERPContext>`
- Return 404 if version not found or `ThumbnailPath` is null/empty
- Resolve physical path: `Path.Combine(config["ArtworkStorage:StorageRoot"] ?? "/var/lib/nordicbees/artwork", version.ThumbnailPath)`
- Return 404 if file doesn't exist
- Return `Results.File(stream, "image/png")`
- `.AllowAnonymous()`

### 2. Extend `ArtworkAssetWithSummary` DTO
**File:** `Services/Artwork/ArtworkService.cs`

Add to `ArtworkAssetWithSummary` class (~line 397):
```csharp
public int? ActualVersionId { get; set; }
public bool HasThumbnail { get; set; }
```

Update `GetAssetsByBrandAsync` (~line 265) where the DTO is populated:
```csharp
ActualVersionId = approvedVersions.Any() ? approvedVersions.First().Id : (int?)null,
HasThumbnail = approvedVersions.Any(v => !string.IsNullOrEmpty(v.ThumbnailPath)),
```

### 3. Add preview image in `ArtworkAssetDetail.razor`
**File:** `Components/Pages/Artwork/ArtworkAssetDetail.razor`

Inside the current version block (`@if (_currentVersion != null)`, after the `</MudGrid>` ~line 81, before the closing `}` of the `@if` block):

```razor
<MudSpacer />
<MudText Typo="Typo.caption" Color="Color.Secondary" Class="mt-2">Prelimininė peržiūra</MudText>
@if (!string.IsNullOrEmpty(_currentVersion.ThumbnailPath))
{
    <img src="/artwork/preview/@_currentVersion.Id"
         style="max-height:300px; cursor:pointer; border-radius:8px; margin-top:8px"
         @onclick="OpenFullPreview"
         class="d-block" />
}
else
{
    <MudIcon Icon="@Icons.Material.Filled.PictureAsPdf"
             Style="font-size:80px; color:#e2e8f0" Class="d-block mt-2" />
}
```

### 4. Add `OpenFullPreview` method + preview dialog in `ArtworkAssetDetail.razor`
**File:** `Components/Pages/Artwork/ArtworkAssetDetail.razor` — add to `@code` block:

```csharp
private async Task OpenFullPreview()
{
    var parameters = new DialogParameters { { "VersionId", _currentVersion!.Id } };
    var options = new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true };
    var dialogRef = await DialogService.ShowAsync<ArtworkPreviewDialog>("Prelimininė peržiūra", parameters, options);
    await dialogRef.Result;
}
```

**New file:** `Components/Pages/Artwork/ArtworkPreviewDialog.razor` — follows existing dialog pattern (see `ApproveVersionDialog.razor`):
```razor
@page "/artwork-preview"
@rendermode InteractiveServer
@using MudBlazor
@inject NavigationManager Navigation

<MudDialog>
    <TitleContent>
        <div class="d-flex align-center gap-2">
            <MudIcon Icon="@Icons.Material.Filled.PictureAsPdf" Color="Color.Primary" />
            <MudText Typo="Typo.h6">Prelimininė peržiūra</MudText>
        </div>
    </TitleContent>
    <DialogContent>
        <div style="display:flex; justify-content:center; align-items:center; min-width:500px; min-height:300px;">
            @if (_versionId.HasValue)
            {
                <img src="@($"/artwork/preview/{_versionId}")"
                     style="max-width:100%; max-height:70vh; border-radius:8px"
                     alt="Preview" />
            }
            else
            {
                <MudText>Peržiūra nerasta</MudText>
            }
        </div>
    </DialogContent>
    <DialogActions>
        <MudButton Variant="Variant.Text" Color="Color.Default" OnClick="CancelAsync">Uždaryti</MudButton>
        @if (_versionId.HasValue)
        {
            <MudButton Variant="Variant.Filled" Color="Color.Primary"
                       OnClick="DownloadAsync" StartIcon="@Icons.Material.Filled.Download">
                Atsisiųsti
            </MudButton>
        }
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] IMudDialogInstance MudDialog { get; set; } = default!;
    [Parameter] public int VersionId { get; set; }
    private int? _versionId;

    protected override void OnParametersSet()
    {
        _versionId = VersionId;
    }

    private void CancelAsync() => MudDialog.Cancel();

    private void DownloadAsync() => Navigation.NavigateTo($"/artwork/download/{_versionId}");
}
```

### 5. Add thumbnail to `ArtworkBrandPage.razor` asset cards
**File:** `Components/Pages/Artwork/ArtworkBrandPage.razor`

Inside the asset card `MudPaper` (~line 33), as the first element before the `d-flex` row:

```razor
@if (asset.HasThumbnail && asset.ActualVersionId.HasValue)
{
    <img src="@($"/artwork/preview/{asset.ActualVersionId}")"
         style="width:60px; height:60px; object-fit:cover; border-radius:4px; margin-bottom:8px"
         class="d-block mx-auto" />
}
```

### 6. Bump version
Run `./bump-version.sh patch` (v0.9.3.15 → v0.9.3.16)

## Files Changed
1. `Program.cs` — add `/artwork/preview/{versionId:int}` endpoint
2. `Services/Artwork/ArtworkService.cs` — extend `ArtworkAssetWithSummary`, populate in `GetAssetsByBrandAsync`
3. `Components/Pages/Artwork/ArtworkAssetDetail.razor` — preview image in current version block, `OpenFullPreview` method
4. `Components/Pages/Artwork/ArtworkPreviewDialog.razor` — **new file** (full preview MudDialog with download button)
5. `Components/Pages/Artwork/ArtworkBrandPage.razor` — thumbnail in asset cards

## Risks / Edge Cases
- **Thumbnail not ready:** `ArtworkPreviewWorker` generates thumbnails async. `ThumbnailPath` may be null — handled by `HasThumbnail` check and placeholder icon.
- **Thumbnail file missing:** If worker wrote path but file I/O failed, endpoint returns 404 (browser shows broken image). Acceptable — worker should be resilient.
- **Dialog in Pages folder:** Artwork dialogs live in `Components/Pages/Artwork/` (not `Components/Dialogs/`), matching `ApproveVersionDialog.razor` pattern.
