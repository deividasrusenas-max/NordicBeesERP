# Fix ArtworkBrandPage.razor StateHasChanged() calls

## Task

Replace `StateHasChanged()` with `await InvokeAsync(StateHasChanged)` in `LoadDataAsync` method of `ArtworkBrandPage.razor`. Calling `StateHasChanged()` directly from an async method in Blazor Server can cause synchronization context issues.

## Changes

**File:** `Components/Pages/Artwork/ArtworkBrandPage.razor`

**Lines 89 and 101 — inside `LoadDataAsync` method:**

1. Line 89: `StateHasChanged();` → `await InvokeAsync(StateHasChanged);`
2. Line 101: `StateHasChanged();` → `await InvokeAsync(StateHasChanged);`

## Post-change

Run `./bump-version.sh patch` to increment the version.
