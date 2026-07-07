# Plan: Add "New Asset" button to ArtworkBrandPage

## Context
- Current version: `0.9.34` → patch bump to `0.9.35`
- Stack: Blazor Server, MudBlazor, .NET
- No migration changes needed — `artwork_assets` table already exists with all required columns

## Files to modify/create

### 1. Create `Components/Dialogs/AssetCreateDialog.razor`
- Dialog with `IMudDialogInstance`, title "Naujas asset'as"
- Fields:
  - **Asset name** — `MudTextField`, required, string
  - **Asset type** — `MudSelect<T>` with 5 items: `label`, `brochure`, `box`, `sticker`, `other`
  - **Description** — `MudTextField`, optional (textarea or text field)
- On confirm: call `ArtworkService.CreateAssetAsync(BrandId, Name, Type, Description, UserId)`
- On success: `MudDialog.Close(DialogResult.Ok(newAssetId))`
- On cancel: `MudDialog.Cancel()`
- Inject: `IArtworkService`, `ISnackbar`, `IAuthService`

### 2. Modify `Services/Artwork/IArtworkService.cs`
- Add method signature:
  ```csharp
  Task<int> CreateAssetAsync(int brandId, string name, string type, string? description, int userId);
  ```

### 3. Modify `Services/Artwork/ArtworkService.cs`
- Implement `CreateAssetAsync`:
  - Create new `ArtworkAsset` entity with provided fields + `CreatedBy = userId`
  - `Status = "active"`, `CreatedAt = DateTime.UtcNow`
  - `_context.ArtworkAssets.Add(asset)` + `await _context.SaveChangesAsync()`
  - Return `asset.Id`

### 4. Modify `Components/Pages/Artwork/ArtworkBrandPage.razor`
- Add `@inject IAuthService AuthService`
- Add button in page header area (next to "Įkelti"):
  ```razor
  <MudButton OnClick="OpenCreateAsset" StartIcon="@Icons.Material.Filled.Add" Variant="Variant.Filled" Color="Color.Primary">
      Naujas asset'as
  </MudButton>
  ```
- Add `OpenCreateAsset` method:
  - Get `await AuthService.GetUserIdAsync()`
  - Show dialog: `DialogService.ShowAsync<AssetCreateDialog>("Naujas asset'as", new DialogParameters { { "BrandId", BrandId } }, new DialogOptions { CloseOnEscapeKey = true, BackdropClick = true, MaxWidth = MaxWidth.Small })`
  - On ok: `if (result?.Data is int assetId) NavigationManager.NavigateTo($"/artwork/asset/{assetId}")`
  - On cancel/error: reload assets list (`await LoadDataAsync()`)
  - Add `MudDialogContainer` component at bottom of page if not present
- Add `LoadDataAsync` helper method (refactor existing `OnParametersSetAsync` logic into reusable method)

### 5. Run `./bump-version.sh patch`
- Updates `appsettings.json` and `NordicBeesERP.csproj` from `0.9.34` to `0.9.35`

## Risks & notes
- `artwork_assets.created_by` is NOT NULL — must always pass valid userId; handle unauthenticated case gracefully
- No migration file changes needed
- Follow existing MudBlazor dialog pattern from `ExpenseCategoryEditDialog.razor`
