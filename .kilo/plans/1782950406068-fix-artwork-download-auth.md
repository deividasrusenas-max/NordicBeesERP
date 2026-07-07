# Fix Artwork Download Endpoint forceLoad Authentication

## Problem
When `NavigateTo(..., forceLoad: true)` is used on `/artwork/download/{versionId}`, the endpoint returns 401/redirect to login. The `ArtworkAccess` policy requires Admin/Manager/Designer role, but the navigation may trigger role re-evaluation before cookie is accepted.

## Fix

### 1. Program.cs line 169

Replace:
```csharp
.RequireAuthorization("ArtworkAccess");
```

With:
```csharp
.RequireAuthorization();
```

This changes from role-specific policy (Admin/Manager/Designer) to generic authenticated-user check. Any logged-in user can download artwork files.

### 2. Version bump

Already done: v0.9.44 → v0.9.45

### 3. Validation

- Login as a non-Admin/Manager/Designer user with artwork access
- Navigate to an artwork detail page
- Click download — file should download without redirect to login
