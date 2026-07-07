# Fix Artwork: effectiveFrom Dialog Pattern Match Bug

## Problem

`ApproveVersionDialog.razor:38` returns `DialogResult.Ok(_effectiveFrom)` where `_effectiveFrom` is `DateTime?`.
In `ArtworkAssetDetail.razor:304`, pattern `result.Data is DateTime` **never matches** `DateTime?` — the entire approval block is skipped, `ApproveVersionAsync` is never called with a date, and `effective_from` defaults to `DateTime.MinValue` in SQL.

## Task 1 — Fix ArtworkAssetDetail.razor pattern match

**File:** `Components/Pages/Artwork/ArtworkAssetDetail.razor`
**Lines:** 304–315

Replace:
```csharp
        if (result.Data != null && result.Data is DateTime)
        {
            var effectiveFrom = (DateTime)result.Data;
            var effectiveDate = effectiveFrom.ToString("yyyy-MM-dd");
            var user = await AuthService.GetAuthenticatedUserAsync();
            var userId = await AuthService.GetUserIdAsync();
            if (userId.HasValue)
            {
                await ArtworkService.ApproveVersionAsync(versionId, userId.Value, effectiveFrom.Date);
                await LoadDataAsync();
                Snackbar.Add($"Versija patvirtinta (galioja nuo {effectiveDate})", Severity.Success);
            }
        }
```

With:
```csharp
        if (result.Data is DateTime? effectiveFrom && effectiveFrom.HasValue)
        {
            var effectiveDate = effectiveFrom.Value.ToString("yyyy-MM-dd");
            var user = await AuthService.GetAuthenticatedUserAsync();
            var userId = await AuthService.GetUserIdAsync();
            if (userId.HasValue)
            {
                await ArtworkService.ApproveVersionAsync(versionId, userId.Value, effectiveFrom.Value.Date);
                await LoadDataAsync();
                Snackbar.Add($"Versija patvirtinta (galioja nuo {effectiveDate})", Severity.Success);
            }
        }
```

## Task 2 — Add logging to ArtworkService

**File:** `Services/Artwork/ArtworkService.cs`

### 2a. Add logger field and constructor parameter

**Lines:** 12–22 — add `private readonly ILogger<ArtworkService> _logger;` and constructor parameter `ILogger<ArtworkService> logger`

```diff
     private readonly NordicBeesERPContext _context;
     private readonly IAuthService _authService;
     private readonly IArtworkStorageService _storageService;
     private readonly IConfiguration _configuration;
+    private readonly ILogger<ArtworkService> _logger;

-    public ArtworkService(NordicBeesERPContext context, IAuthService authService, IArtworkStorageService storageService, IConfiguration configuration)
+    public ArtworkService(NordicBeesERPContext context, IAuthService authService, IArtworkStorageService storageService, IConfiguration configuration, ILogger<ArtworkService> logger)
     {
         _context = context;
         _authService = authService;
         _storageService = storageService;
         _configuration = configuration;
+        _logger = logger;
     }
```

### 2b. Add logging in ApproveVersionAsync

**Line:** 57, immediately after the null check:

```csharp
    public async Task ApproveVersionAsync(int versionId, int reviewerId, DateTime effectiveFrom)
    {
+       _logger.LogInformation("ApproveVersion effectiveFrom: {date}", effectiveFrom);
        // Get the version to approve
        var versionToApprove = await _context.ArtworkVersions.FindAsync(versionId);
```

## Validation

1. Upload a new artwork version, then approve with a future date (e.g., July 15) — verify `effective_from` in DB equals the chosen date
2. Verify `uploaded_at` timestamps on artwork pages show Lithuanian time (UTC+3 in summer) — already fixed via `ToLithuanianTimeString()`
3. Check application logs for `ApproveVersion effectiveFrom` message confirming the date

## Already Fixed (no changes needed)

- `LithuanianTimeHelper.ToLithuanianTimeString()` exists at `Helpers/LithuanianTimeHelper.cs`
- `_Imports.razor:26` has `@using NordicBeesERP.Helpers` — extension method available globally
- `ArtworkAssetDetail.razor` lines 69, 150, 231 already use `ToLithuanianTimeString()`
- ArtworkUpload.razor and ArtworkBrandPage.razor have no date display — no changes needed
