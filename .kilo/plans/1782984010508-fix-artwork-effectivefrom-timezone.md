# Fix Artwork: effectiveFrom Date Bug + Lithuanian Timezone Display

## 1. Fix `effectiveFrom` Not Passed to `ApproveVersionAsync`

**Root cause:** `ApproveVersionDialog.razor:38` returns `DialogResult.Ok(_effectiveFrom)` where `_effectiveFrom` is `DateTime?`. In `ArtworkAssetDetail.razor:264`, the pattern `result.Data is DateTime effectiveFrom` fails because `DateTime?` does not match `DateTime`. The entire block is skipped and `effectiveFrom` defaults to `DateTime.MinValue`.

**File:** `Components/Pages/Artwork/ArtworkAssetDetail.razor` — line 264

**Change:**
```diff
-        if (result.Data is DateTime effectiveFrom)
+        if (result.Data is DateTime? effectiveFrom && effectiveFrom.HasValue)
         {
             var user = await AuthService.GetAuthenticatedUserAsync();
             var userId = await AuthService.GetUserIdAsync();
             if (userId.HasValue)
             {
-                await ArtworkService.ApproveVersionAsync(versionId, userId.Value, effectiveFrom);
+                await ArtworkService.ApproveVersionAsync(versionId, userId.Value, effectiveFrom.Value.Date);
                 await LoadDataAsync();
                 Snackbar.Add($"Versija patvirtinta (galioja nuo {effectiveFrom:yyyy-MM-dd})", Severity.Success);
             }
         }
```

**Data flow:** `ApproveVersionDialog._effectiveFrom` (DateTime?) → `DialogResult.Ok()` → `result.Data` (DateTime?) → pattern match `is DateTime? effectiveFrom` → `effectiveFrom.Value.Date` → `ApproveVersionAsync(versionId, userId, effectiveFrom)` → SQL `effective_from = @p2`

## 2. Add Lithuanian Timezone Helper for Display

**File:** `Helpers/LithuanianTimeHelper.cs` — create new

```csharp
namespace NordicBeesERP.Helpers;

public static class LithuanianTimeHelper
{
    private static readonly TimeZoneInfo _lithuanianTz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vilnius");

    public static DateTime ToLithuanianTime(DateTime utcDateTime) => TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, _lithuanianTz);

    public static string ToLithuanianTimeString(this DateTime utcDateTime) =>
        ToLithuanianTime(utcDateTime).ToString("yyyy-MM-dd HH:mm");
}
```

**File:** `Components/_Imports.razor` — add `@using NordicBeesERP.Helpers`

## 3. Update Date Display in `ArtworkAssetDetail.razor`

Three locations need conversion from UTC to Lithuanian display time. DB storage remains UTC.

**Line 69** — current version uploaded_at:
```diff
-                            <MudText Typo="Typo.body2">@_currentVersion.UploadedAt.ToString("yyyy-MM-dd HH:mm")</MudText>
+                            <MudText Typo="Typo.body2">@_currentVersion.UploadedAt.ToLithuanianTimeString()</MudText>
```

**Line 138** — pending version uploaded_at:
```diff
-                            <MudText Typo="Typo.body2">@_pendingVersion.UploadedAt.ToString("yyyy-MM-dd HH:mm")</MudText>
+                            <MudText Typo="Typo.body2">@_pendingVersion.UploadedAt.ToLithuanianTimeString()</MudText>
```

**Line 207** — version history table uploaded_at column:
```diff
-                    <MudTd>@version.UploadedAt.ToString("yyyy-MM-dd HH:mm")</MudTd>
+                    <MudTd>@version.UploadedAt.ToLithuanianTimeString()</MudTd>
```

**Note:** `EffectiveFrom`/`EffectiveTo` are already stored as local dates (no time component), so no conversion needed for those columns — they display correctly as-is.

## Validation

1. Approve a pending version with a future date (e.g., July 10) — verify `effective_from` in DB equals that chosen date, not today
2. Check uploaded_at timestamps on artwork pages show Lithuanian local time (UTC+3 in summer) matching browser clock
3. DB storage must still be UTC (verify via direct SQL query)
