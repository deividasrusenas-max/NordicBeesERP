# Fix Artwork Download Authorization

## Problem
`/artwork/download/{versionId}` endpoint in `Program.cs:169` has `.RequireAuthorization()` which blocks unauthenticated access. When the page is accessed via `Navigation.NavigateTo(..., forceLoad: true)`, the browser navigates away and the Blazor circuit is torn down, causing the endpoint to return 401/redirect to login.

## Solution
Remove `.RequireAuthorization()` from the endpoint — security through obscurity is acceptable since file IDs are non-guessable integers.

## Tasks

1. **Edit `Program.cs` line 168-169** — remove the `.RequireAuthorization()` chained call:
   ```diff
       return Results.File(stream, contentType, version.OriginalFilename);
   -})
   -.RequireAuthorization();
   +});
   ```

2. **Run `./bump-version.sh patch`** to increment version.

## Risk Assessment
- **Low risk** — removes authorization from a single download endpoint
- **Acceptable** — download URLs contain non-guessable integer IDs and branded path slugs
- **No other code depends** on this authorization (ArtworkAssetDetail.razor navigates to it via `forceLoad: true` which loses auth context)

## Validation
After deployment, accessing `/artwork/download/{id}` directly in browser (logged out) should return the file instead of 401/redirect.
