# Artwork Preview Fixes

## Task 1: ArtworkPreviewWorker.cs — Increase thumbnail DPI

**File:** `Services/Artwork/ArtworkPreviewWorker.cs`
**Line 112:** Change `-r72` to `-r150`

```diff
- await RunGhostscriptAsync(sourcePath, thumbRawPath, "-r72", cancellationToken);
+ await RunGhostscriptAsync(sourcePath, thumbRawPath, "-r150", cancellationToken);
```

## Task 2: ArtworkAssetDetail.razor — Loading indicators + auto-refresh

### 2a. Replace PDF icon in current version block (lines 100-106)

```diff
                 else
                 {
-                    <div @onclick="DownloadCurrentVersion" style="cursor:pointer">
-                        <MudIcon Icon="@Icons.Material.Filled.PictureAsPdf"
-                                  Style="font-size:80px; color:#94a3b8" Class="d-block mt-2" />
-                    </div>
+                    @if (string.IsNullOrEmpty(_currentVersion?.ThumbnailPath))
+                    {
+                        <div style="display:flex; flex-direction:column; align-items:center; padding:20px">
+                            <MudProgressCircular Indeterminate="true" Color="Color.Primary" Size="Size.Medium" />
+                            <MudText Typo="Typo.caption" Class="mt-2" Style="color:#64748b">
+                                Peržiūra generuojama...
+                            </MudText>
+                        </div>
+                    }
+                    else
+                    {
+                        <div @onclick="DownloadCurrentVersion" style="cursor:pointer">
+                            <MudIcon Icon="@Icons.Material.Filled.PictureAsPdf"
+                                      Style="font-size:80px; color:#94a3b8" Class="d-block mt-2" />
+                        </div>
+                    }
                 }
```

### 2b. Replace PDF icon in pending version block (lines 162-165)

```diff
                 else
                 {
-                    <div class="d-block mt-2">
-                        <MudIcon Icon="@Icons.Material.Filled.PictureAsPdf"
-                                 Style="font-size:80px; color:#94a3b8" />
-                    </div>
+                    @if (string.IsNullOrEmpty(_pendingVersion?.ThumbnailPath))
+                    {
+                        <div style="display:flex; flex-direction:column; align-items:center; padding:20px">
+                            <MudProgressCircular Indeterminate="true" Color="Color.Primary" Size="Size.Medium" />
+                            <MudText Typo="Typo.caption" Class="mt-2" Style="color:#64748b">
+                                Peržiūra generuojama...
+                            </MudText>
+                        </div>
+                    }
+                    else
+                    {
+                        <div class="d-block mt-2">
+                            <MudIcon Icon="@Icons.Material.Filled.PictureAsPdf"
+                                      Style="font-size:80px; color:#94a3b8" />
+                        </div>
+                    }
                 }
```

### 2c. Add timer field and methods to `@code` block

**Add to fields (after line 227 `private bool _isLoading = true;`):**
```csharp
    private System.Threading.Timer? _refreshTimer;
```

**Replace `OnInitializedAsync` (lines 229-234):**
```diff
     protected override async Task OnInitializedAsync()
     {
         var user = await AuthService.GetAuthenticatedUserAsync();
         _isAdmin = user?.Role == "Admin";
         await LoadDataAsync();
+        if (NeedsPreviewRefresh())
+            _refreshTimer = new System.Threading.Timer(async _ => 
+            {
+                await InvokeAsync(async () => {
+                    await LoadDataAsync();
+                    if (!NeedsPreviewRefresh()) _refreshTimer?.Dispose();
+                    StateHasChanged();
+                });
+            }, null, 5000, 5000);
     }
```

**Add new methods after `LoadDataAsync` (after line 248):**
```csharp
    private bool NeedsPreviewRefresh() =>
        (_currentVersion != null && string.IsNullOrEmpty(_currentVersion.ThumbnailPath)) ||
        (_pendingVersion != null && string.IsNullOrEmpty(_pendingVersion.ThumbnailPath));

    public void Dispose() => _refreshTimer?.Dispose();
```

## Task 3: Run version bump

```bash
./bump-version.sh patch
```
