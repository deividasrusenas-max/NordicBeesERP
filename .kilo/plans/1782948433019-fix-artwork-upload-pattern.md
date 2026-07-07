# Fix ArtworkUpload.razor File Upload Pattern

## Goal
Replace the broken drag & drop + raw `InputFile` pattern with `MudFileUpload` + `IBrowserFile` pattern, matching the approach in `ExpenseUploadDialog.razor`.

## Affected File
- `Components/Pages/Artwork/ArtworkUpload.razor` (223 lines → ~130 lines)

---

## Changes

### 1. Replace imports (lines 1-8)

**Remove:**
- `@using Microsoft.AspNetCore.Components.Forms`

**Add:**
- `@using MudBlazor`
- `@inject ISnackbar Snackbar`

### 2. Replace entire UI section (lines 10-48)

**Remove the entire `<div class="card p-4">` block and replace with:**

```razor
<MudContainer MaxWidth="MaxWidth.Small" Class="mt-4">
    <MudText Typo="Typo.h5" Class="mb-4">Upload New Version for "@AssetName"</MudText>

    @if (_uploading)
    {
        <MudProgressLinear Color="Color.Primary" Indeterminate="true" Class="mb-4" />
        <MudText Typo="Typo.caption" Color="Color.Secondary" Class="text-center">Uploading...</MudText>
    }

    <MudPaper Class="pa-4 mb-4" Elevation="0" Style="background:#f8fafc; border-radius:12px">
        @if (_file == null)
        {
            <div class="text-center">
                <MudIcon Icon="@Icons.Material.Filled.CloudUpload" Color="Color.Primary" Style="font-size:48px" />
                <MudText Class="mt-2">Drop a PDF file or</MudText>
                <MudFileUpload T="IBrowserFile" Accept=".pdf" MaximumFileCount="1"
                               FilesChanged="OnFileChanged" Class="mt-2">
                    <ActivatorContent>
                        <MudButton Variant="Variant.Outlined" Color="Color.Primary"
                                   StartIcon="@Icons.Material.Filled.FolderOpen">
                            Select File
                        </MudButton>
                    </ActivatorContent>
                </MudFileUpload>
            </div>
        }
        else
        {
            <MudPaper Elevation="0" Class="d-flex align-center pa-3"
                      Style="background:#f0fdf4; border:2px solid #86efac; border-radius:8px;">
                <MudIcon Icon="@Icons.Material.Filled.PictureAsPdf" Color="Color.Success" Style="font-size:32px;" />
                <div class="ml-3 flex-grow-1" style="text-align:left;">
                    <MudText Typo="Typo.body1" Style="font-weight:600;">@_file.Name</MudText>
                    <MudText Typo="Typo.caption" Color="Color.Secondary">@GetFileSizeString()</MudText>
                </div>
                @if (!_uploading)
                {
                    <MudIconButton Icon="@Icons.Material.Filled.Close" Color="Color.Error"
                                   Size="Size.Small" OnClick="RemoveFile" />
                }
            </MudPaper>
        }
    </MudPaper>

    @if (_file != null && !_uploading)
    {
        <div class="mt-3">
            <MudTextField @bind-Value="ChangeDescription" Label="Change Description"
                          Placeholder="Describe changes in this version..."
                          Rows="3" Variant="Variant.Outlined" FullWidth="true" />
            <MudText Typo="Typo.caption" Color="Color.Secondary" Class="mt-1">
                @ChangeDescription.Length / 500
            </MudText>
        </div>

        <MudButton Variant="Variant.Filled" Color="Color.Primary"
                   Disabled="@(_uploading || string.IsNullOrWhiteSpace(ChangeDescription))"
                   OnClick="UploadFileAsync"
                   StartIcon="@Icons.Material.Filled.CloudUpload"
                   FullWidth="true" Class="mt-3">
            Upload
        </MudButton>
    }
</MudContainer>
```

### 3. Replace `@code` block (lines 50-223)

**Remove entirely and replace with:**

```csharp
@code {
    [Parameter] public int? AssetId { get; set; }
    public string? AssetName { get; set; }

    private bool _uploading;
    private IBrowserFile? _file;
    private string ChangeDescription { get; set; } = "";
    private ArtworkVersionUploadResult? _result;

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

    private async Task OnFileChanged(IBrowserFile? file)
    {
        _file = file;
        _result = null;
        _uploading = false;
        ChangeDescription = "";
        StateHasChanged();
    }

    private async Task UploadFileAsync()
    {
        if (_file == null) return;

        if (string.IsNullOrWhiteSpace(ChangeDescription))
        {
            Snackbar.Add("Change description is required.", Severity.Warning);
            return;
        }
        if (ChangeDescription.Length > 500)
        {
            Snackbar.Add("Description must be <= 500 characters.", Severity.Warning);
            return;
        }

        _uploading = true;
        _result = null;
        StateHasChanged();

        try
        {
            using var stream = _file.OpenReadStream(500 * 1024 * 1024); // 500MB max
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var base64 = Convert.ToBase64String(ms.ToArray());

            if (!AssetId.HasValue)
            {
                _uploading = false;
                StateHasChanged();
                return;
            }

            _result = await ArtworkService.UploadVersionAsync(
                AssetId.Value,
                ChangeDescription,
                base64,
                _file.Name,
                _file.Size,
                "print_ready"
            );

            if (_result?.Success == true)
            {
                Snackbar.Add("Version uploaded successfully", Severity.Success);
            }
            else
            {
                Snackbar.Add($"Upload failed: {_result?.Message}", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add("Upload error: " + ex.Message, Severity.Error);
            _result = new ArtworkVersionUploadResult { Success = false, Message = ex.Message };
        }
        finally
        {
            _uploading = false;
            StateHasChanged();
        }
    }

    private void RemoveFile()
    {
        _file = null;
        _result = null;
        ChangeDescription = "";
        StateHasChanged();
    }

    private string GetFileSizeString()
    {
        if (_file == null) return "";
        var size = _file.Size;
        if (size < 1024 * 1024)
            return $"{size / 1024:0.0} KB";
        return $"{size / (1024 * 1024):0.0} MB";
    }
}
```

---

## What gets removed (no longer needed)

- `DotNetObjectReference<DropZoneHelper>` — JS interop reference
- `OnAfterRenderAsync` — JS dropzone setup
- `Dispose()` — JS cleanup
- `SelectFile()` — JS click trigger on hidden input
- `OnFileSelected(InputFileChangeEventArgs e)` — raw InputFile handler
- `DropZoneHelper` nested class with `[JSInvokable]`
- `ChangeDescriptionError` — no more inline validation errors
- `ChangeDescriptionLength` computed property — replaced with inline `@ChangeDescription.Length`
- Bootstrap CSS classes: `card`, `border`, `rounded-3`, `bg-light`, `form-label`, `form-control`, `text-danger`, `progress`, `progress-bar`, `alert`, `alert-success`, `alert-danger`

## Key differences from current implementation

| Aspect | Before | After |
|--------|--------|-------|
| File picker | Hidden `<InputFile>` + JS click trigger | `MudFileUpload` component |
| Max file size | 100MB (`file.OpenReadStream(100 * 1024 * 1024)`) | 500MB (`_file.OpenReadStream(500 * 1024 * 1024)`) |
| File reading | Direct `ReadAsync` into byte buffer | `CopyToAsync` into `MemoryStream` |
| Validation feedback | Inline `div.invalid-feedback` | `Snackbar` with severity levels |
| Progress | Simulated percentage bar | `MudProgressLinear` indeterminate |
| Success/Error | Bootstrap `alert-success`/`alert-danger` | Snackbar |
| JS dependencies | `window.setupDropZone`, `window.getDropFileBase64`, `window.cleanupDropZone` | None |
| Imports | `Microsoft.AspNetCore.Components.Forms` | `MudBlazor` |

---

## Verification

After applying changes:
1. File should compile without errors
2. `@using Microsoft.AspNetCore.Components.Forms` should not appear
3. No references to `DropZoneHelper`, `JS`, or `DotNetObjectReference`
4. Only `.pdf` files accepted (`Accept=".pdf"` on MudFileUpload)
