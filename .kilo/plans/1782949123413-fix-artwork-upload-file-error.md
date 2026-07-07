# Fix ArtworkUpload.razor File Upload Error: "_blazorFilesById null"

## Problem
`IBrowserFile` reference becomes invalid between file selection (`OnFileChanged`) and stream opening (`UploadFileAsync`), causing `_blazorFilesById null` error in Blazor's internal file management.

## Root Cause
ArtworkUpload stores `IBrowserFile` reference in `OnFileChanged`, then tries to call `OpenReadStream` on it later in `UploadFileAsync`. Blazor only keeps file references valid within the same event handler that created them.

## Working Reference
`ExpenseUploadDialog.razor:419-431` reads file content immediately in `OnFileChanged` and stores base64.
`BankImport.razor:354-389` follows the same pattern with `_selectedFile`.

## Changes — `Components/Pages/Artwork/ArtworkUpload.razor`

1. **Add `_uploadKey` field** for MudFileUpload reset:
   ```csharp
   private int _uploadKey = 0;
   ```

2. **Add `@key="@_uploadKey"`** to the `<MudFileUpload>` component (line 25).

3. **Replace `private IBrowserFile? _file;`** with:
   ```csharp
   private string? _fileName;
   private string? _fileSizeString;
   private byte[]? _fileBytes;
   ```

4. **Rewrite `OnFileChanged`** (line 95) — read file content immediately, store base64-ready bytes:
   ```csharp
   private async Task OnFileChanged(IBrowserFile? file)
   {
       _file = null;
       _result = null;
       _uploading = false;
       ChangeDescription = "";

       if (file != null)
       {
           using var stream = file.OpenReadStream(500 * 1024 * 1024);
           using var ms = new MemoryStream();
           await stream.CopyToAsync(ms);
           _fileBytes = ms.ToArray();
           _fileName = file.Name;
           _fileSizeString = $"{file.Size / (1024.0):0.0} KB";
       }
       StateHasChanged();
   }
   ```

5. **Rewrite `UploadFileAsync`** (line 104) — use stored `_fileBytes` directly, no `OpenReadStream`:
   ```csharp
   private async Task UploadFileAsync()
   {
       if (_fileBytes == null) return;

       if (string.IsNullOrWhiteSpace(ChangeDescription))
       {
           Snackbar.Add("Change description is required.", Severity.Warning);
           return;
       }
       if (ChangeDescription.Length > 500)
       {
           Snackbar.Add("Description must be <= 500 characters.", Severity.Warning);
           return;
       }

       _uploading = true;
       _result = null;
       StateHasChanged();

       try
       {
           var base64 = Convert.ToBase64String(_fileBytes);

           if (!AssetId.HasValue)
           {
               _uploading = false;
               StateHasChanged();
               return;
           }

           _result = await ArtworkService.UploadVersionAsync(
               AssetId.Value,
               ChangeDescription,
               base64,
               _fileName,
               _fileBytes.Length,
               "print_ready"
           );

           if (_result?.Success == true)
           {
               Snackbar.Add("Version uploaded successfully", Severity.Success);
               _uploadKey++; // Reset MudFileUpload component
           }
           else
           {
               Snackbar.Add($"Upload failed: {_result?.Message}", Severity.Error);
           }
       }
       catch (Exception ex)
       {
           Snackbar.Add("Upload error: " + ex.Message, Severity.Error);
           _result = new ArtworkVersionUploadResult { Success = false, Message = ex.Message };
       }
       finally
       {
           _uploading = false;
           StateHasChanged();
       }
   }
   ```

6. **Rewrite `RemoveFile`** (line 167) — clear new fields:
   ```csharp
   private void RemoveFile()
   {
       _fileBytes = null;
       _fileName = null;
       _fileSizeString = null;
       _result = null;
       ChangeDescription = "";
       StateHasChanged();
   }
   ```

7. **Update file display** — change `@_file.Name` to `@_fileName` and `@GetFileSizeString()` to `@_fileSizeString` in the template.

8. **Remove `GetFileSizeString()` method** entirely (no longer needed).

## Validation Steps
- Select a PDF file — should display filename and size
- Click Upload with description — should upload successfully
- Click Close on selected file — should clear selection and allow re-selection
- Verify no `_blazorFilesById null` errors in browser console

## Rollback
Single commit revert — no data migration or config changes.

## Post-Fix
Run `./bump-version.sh patch` to increment version.
