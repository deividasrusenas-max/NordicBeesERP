# NavMenu.razor Navigation Logic Fix

## Problem
Current logic `@if (_isAdmin || _isDesigner)` shows only Artwork to Admins, hiding the full ERP menu.

## Changes

### 1. Line 5: Outer condition
```diff
-    @if (_isAdmin || _isDesigner)
+    @if (_isDesigner && !_isAdmin)
```

### 2. Inside else block: Add Artwork section at top
After the opening `{` of the else block (line 12), before the Home link (line 14), add:
```razor
        @if (_isAdmin || _isDesigner)
        {
            <MudNavGroup Title="Artwork" Icon="@Icons.Material.Filled.Palette">
                <MudNavLink Href="/artwork" Icon="@Icons.Material.Filled.Palette">Brand Dashboard</MudNavLink>
                <MudNavLink Href="/artwork/upload" Icon="@Icons.Material.Filled.CloudUpload">Upload</MudNavLink>
            </MudNavGroup>
        }
```

### 3. Run version bump
```bash
./bump-version.sh patch
```

## Resulting behavior
| Role | Navigation |
|------|-----------|
| Designer (no Admin) | Artwork only |
| Admin (with or without Designer) | Full ERP menu + Artwork |
| Manager / Warehouse / regular | Full ERP menu (no Artwork) |
