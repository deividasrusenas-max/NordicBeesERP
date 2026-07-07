# Fix: `effective_from` date never saved — nullable pattern match failure

## Root Cause

**`ApproveVersionDialog.razor:33`** — `_effectiveFrom` is `DateTime?` (nullable):
```csharp
private DateTime? _effectiveFrom = DateTime.Today;
```
Returned via `DialogResult.Ok(_effectiveFrom)` — `result.Data` is a boxed `DateTime?`.

**`ArtworkAssetDetail.razor:304`** — pattern match against non-nullable `DateTime`:
```csharp
if (result.Data is DateTime effectiveFrom)
```
A boxed `DateTime?` does **not** match `is DateTime` in C#. The condition is always `false`, so `ApproveVersionAsync` is never called and the date is never saved.

## Fix

**File:** `Components/Pages/Artwork/ArtworkAssetDetail.razor`, line 304

```diff
-        if (result.Data is DateTime effectiveFrom)
+        if (result.Data is DateTime? effectiveFrom)
```

## Notes

- `ArtworkService.cs:59` already has `_logger.LogInformation("ApproveVersion effectiveFrom: {date}", effectiveFrom)` — no additional logging needed.
- Run `./bump-version.sh patch` after applying the fix.
