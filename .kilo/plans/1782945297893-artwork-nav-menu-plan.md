# Artwork Module Navigation Plan

## Task
Add Artwork module nav section to `NavMenu.razor`, replacing the existing Designer-only Artwork group.

## Steps

1. **Replace existing Artwork section** (lines 5-11) in `Components/Layout/NavMenu.razor`:
   - Change visibility from `@if (_isDesigner)` to `@if (_isAdmin || _isDesigner)`
   - Replace links with:
     - `/artwork` — "Brand Dashboard", icon `Icons.Material.Filled.Palette`
     - `/artwork/upload` — "Upload", icon `Icons.Material.Filled.CloudUpload`

2. **Run version bump:**
   ```bash
   ./bump-version.sh patch
   ```

## Diff

```diff
@@ -1,13 +1,13 @@
 @using MudBlazor
 @inject AuthenticationStateProvider AuthStateProvider

 <MudNavMenu Style="display:flex; flex-direction:column; height:100%">
-    @if (_isDesigner)
-    {
-        <MudNavGroup Title="Artwork" Icon="@Icons.Material.Filled.Palette">
-            <MudNavLink Href="/artwork/brands" Icon="@Icons.Material.Filled.Brush">Brands</MudNavLink>
-            <MudNavLink Href="/artwork/assets" Icon="@Icons.Material.Filled.Image">Assets</MudNavLink>
-        </MudNavGroup>
-    }
+    @if (_isAdmin || _isDesigner)
+    {
+        <MudNavGroup Title="Artwork" Icon="@Icons.Material.Filled.Palette">
+            <MudNavLink Href="/artwork" Icon="@Icons.Material.Filled.Palette">Brand Dashboard</MudNavLink>
+            <MudNavLink Href="/artwork/upload" Icon="@Icons.Material.Filled.CloudUpload">Upload</MudNavLink>
+        </MudNavGroup>
+    }
     else
```

## Validation
- NavMenu renders Artwork section for both Admin and Designer roles
- Links point to `/artwork` and `/artwork/upload`
- Existing non-Artwork nav structure unchanged
- Version bumped by patch
