# ArtworkVersion Effective Dates Implementation

## Goal
Add `effective_from`/`effective_to` date tracking to artwork versions. When approving a version, the user selects the effective-from date (default: today), which is used in the filename and stored in the DB. Previously approved versions get their `effective_to` set to the day before the new version's `effective_from`.

---

## Step 1 — Migration file

**Create:** `Migrations/20260702120000_ArtworkVersionEffectiveDates.cs`

```csharp
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    public partial class ArtworkVersionEffectiveDates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE artwork_versions 
                ADD COLUMN effective_from DATE NULL AFTER reviewed_at,
                ADD COLUMN effective_to DATE NULL AFTER effective_from;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE artwork_versions 
                DROP COLUMN effective_to,
                DROP COLUMN effective_from;
            ");
        }
    }
}
```

---

## Step 2 — Model properties

**Edit:** `Models/Artwork/ArtworkVersion.cs` — add after `ReviewComment`:

```csharp
[Column("effective_from")]
public DateTime? EffectiveFrom { get; set; }
[Column("effective_to")]
public DateTime? EffectiveTo { get; set; }
```

---

## Step 3 — ApproveVersionDialog.razor

**Create:** `Components/Pages/Artwork/ApproveVersionDialog.razor`

- Parameters: `VersionId` (int), `InitialComment` (string)
- Uses `<MudDatePicker>` for `effective_from`, default = `DateTime.Today`
- Uses same dialog structure as `RejectCommentDialog.razor`
- On confirm: `MudDialog.Close(DialogResult.Ok(effectiveFrom))`

```razor
@using MudBlazor

<MudDialog>
    <TitleContent>
        <div class="d-flex align-center gap-2">
            <MudIcon Icon="@Icons.Material.Filled.CheckCircle" Color="Color.Success" />
            <MudText Typo="Typo.h6">Patvirtinti versiją</MudText>
        </div>
    </TitleContent>
    <DialogContent>
        <div style="min-width:320px;">
            <MudDatePicker @bind-Value="@_effectiveFrom"
                           Label="Galioja nuo"
                           FirstDayOfWeek="FirstDayOfWeek.Monday"
                           DisabledDaysOfTheWeek="@_disabledDays"
                           MinDate="DateTime.Today"
                           Variant="Variant.Outlined"
                           FullWidth="true"
                           AdornmentIcon="@Icons.Material.Filled.Event" />
        </div>
    </DialogContent>
    <DialogActions>
        <MudButton Variant="Variant.Text" Color="Color.Default" OnClick="CancelAsync">Atšaukti</MudButton>
        <MudButton Variant="Variant.Filled" Color="Color.Success" OnClick="ConfirmAsync" StartIcon="@Icons.Material.Filled.CheckCircle">Patvirtinti</MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] IMudDialogInstance MudDialog { get; set; } = default!;
    [Parameter] public int VersionId { get; set; }
    [Parameter] public string InitialComment { get; set; } = "";

    private DateTime? _effectiveFrom = DateTime.Today;
    private DayOfWeek[] _disabledDays = [DayOfWeek.Saturday, DayOfWeek.Sunday];

    private void ConfirmAsync()
    {
        MudDialog.Close(DialogResult.Ok(_effectiveFrom));
    }

    private void CancelAsync()
    {
        MudDialog.Cancel();
    }
}
```

---

## Step 4 — Service interface

**Edit:** `Services/Artwork/IArtworkService.cs` — update method signature:

```csharp
Task ApproveVersionAsync(int versionId, int userId, DateTime effectiveFrom);
```

---

## Step 5 — Service implementation

**Edit:** `Services/Artwork/ArtworkService.cs` — update `ApproveVersionAsync`:

```csharp
public async Task ApproveVersionAsync(int versionId, int reviewerId, DateTime effectiveFrom)
{
    var versionToApprove = await _context.ArtworkVersions.FindAsync(versionId);
    if (versionToApprove == null)
        throw new ArgumentException($"Version with ID {versionId} not found.");

    var user = await _authService.GetAuthenticatedUserAsync();
    var performedBy = user?.FullName ?? user?.Email ?? "system";
    var userId = await _authService.GetUserIdAsync();

    var currentTimestamp = DateTime.UtcNow;

    // Calculate effective_to for the old approved version (day before new effective_from)
    DateTime? effectiveToForOld = effectiveFrom.Date.AddDays(-1);

    // Find all currently approved versions for this asset
    var approvedVersions = await _context.ArtworkVersions
        .Where(v => v.AssetId == versionToApprove.AssetId && v.Status == "approved")
        .ToListAsync();

    // Supersede old approved versions AND set effective_to
    await _context.Database.ExecuteSqlRawAsync(
        "UPDATE artwork_versions SET status = @p0, uploaded_at = @p1, effective_to = @p2 WHERE asset_id = @p3 AND status = @p4",
        "superseded", currentTimestamp, effectiveToForOld, versionToApprove.AssetId, "approved");

    // Approve the new version with effective_from
    await _context.Database.ExecuteSqlRawAsync(
        "UPDATE artwork_versions SET status = @p0, uploaded_at = @p1, effective_from = @p2 WHERE id = @p3",
        "approved", currentTimestamp, effectiveFrom.Date, versionId);

    // ... rest of audit log and file rename logic unchanged ...

    // Rename approved file — use effectiveFrom in filename
    var approvedVersion = await _context.ArtworkVersions.FindAsync(versionId);
    if (approvedVersion != null && !string.IsNullOrEmpty(approvedVersion.FilePath))
    {
        var asset = await _context.ArtworkAssets.FindAsync(approvedVersion.AssetId);
        if (asset != null)
        {
            var brand = await _context.ArtworkBrands.FindAsync(asset.BrandId);
            if (brand != null && !string.IsNullOrEmpty(brand.Slug))
            {
                var newFilename = $"LK{asset.Id:D4}-{brand.Slug}-v{approvedVersion.VersionNumber}-{effectiveFrom:yyyyMMdd}.pdf";
                // ... rest unchanged ...
            }
        }
    }
}
```

Key SQL changes in `ApproveVersionAsync`:
1. Supersede SQL: add `, effective_to = @p2` — sets `effective_to` to `effectiveFrom.AddDays(-1)` on all previously approved versions
2. Approve SQL: add `, effective_from = @p2` — sets `effective_from` to the chosen date
3. Filename: change `approvalDate` to `effectiveFrom:yyyyMMdd`

---

## Step 6 — UI in ArtworkAssetDetail.razor

**Edit:** `Components/Pages/Artwork/ArtworkAssetDetail.razor`

### 6a. Approve button — open dialog instead of direct call

Replace the direct `OnClick="() => ApproveVersion(_pendingVersion.Id)"` on the approve button (line 95) with a dialog reference.

### 6b. ApproveVersion method — open dialog, get date, then call service

Replace the `ApproveVersion` method in `@code` block:

```csharp
private async Task ApproveVersion(int versionId)
{
    var parameters = new DialogParameters
    {
        { x => x.VersionId, versionId },
        { x => x.InitialComment, "" }
    };
    var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };
    var dialogRef = await DialogService.ShowAsync<ApproveVersionDialog>("Patvirtinti versiją", parameters, options);
    var result = await dialogRef.Result;

    if (result.Canceled) return;

    if (result.Data is DateTime effectiveFrom)
    {
        var user = await AuthService.GetAuthenticatedUserAsync();
        var userId = await AuthService.GetUserIdAsync();
        if (userId.HasValue)
        {
            await ArtworkService.ApproveVersionAsync(versionId, userId.Value, effectiveFrom);
            await LoadDataAsync();
            Snackbar.Add($"Versija patvirtinta (galioja nuo {effectiveFrom:yyyy-MM-dd})", Severity.Success);
        }
    }
}
```

### 6c. Version history table — add two columns

In the `<HeaderContent>` of the version history MudTable (line 135), add two new `<MudTh>`:

```razor
<MudTh>Galioja nuo</MudTh>
<MudTh>Galioja iki</MudTh>
```

In the `<RowTemplate>`, add two `<MudTd>` after the "Sukurta" column:

```razor
<MudTd>
    @(version.EffectiveFrom.HasValue ? version.EffectiveFrom.Value.ToString("yyyy-MM-dd") : "-")
</MudTd>
<MudTd>
    @(version.EffectiveTo.HasValue ? version.EffectiveTo.Value.ToString("yyyy-MM-dd") : "-")
</MudTd>
```

---

## Step 7 — Run migration

```bash
cd /Users/deividasru/Projects/NordicBeesERP
dotnet ef database update --project . --context NordicBeesERP.Data.NordicBeesErpContext --migrations-path Migrations
```

---

## Step 8 — Bump version

```bash
./bump-version.sh patch
```

---

## Checklist

- [x] Plan created
- [x] Create migration `Migrations/20260702120000_ArtworkVersionEffectiveDates.cs` — raw SQL, applied
- [x] Add `EffectiveFrom`/`EffectiveTo` to `ArtworkVersion.cs` model
- [x] Create `ApproveVersionDialog.razor` — date picker with Sat/Sun disabled
- [x] Update `IArtworkService.ApproveVersionAsync` signature (add `DateTime effectiveFrom`)
- [x] Update `ArtworkService.ApproveVersionAsync` — SQL + filename
- [x] Update `ArtworkAssetDetail.razor` — approve dialog + table columns
- [x] Run `dotnet ef database update` — columns exist in DB
- [ ] Run `./bump-version.sh patch`
- [ ] Commit
