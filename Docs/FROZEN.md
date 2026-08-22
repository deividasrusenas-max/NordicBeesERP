# NordicBeesERP — Frozen blocks (do not touch without permission)

> Different concept from AGENTS.md's DB-write/migration rules — this file
> lists specific, working code blocks that must not be modified without
> explicit human permission, because they're fragile/hard-won and any
> "improvement" attempt has historically broken them.

---

## 1. DRAG & DROP — `wwwroot/js/dropzone.js`

**Status:** ✅ WORKS (Chrome + Firefox)
**Do not touch:** the entire file

Working mechanism:
- `setupDropZone(dotNetHelper, elementId)` — assigns event listeners
- `getDropFileBase64(elementId)` — returns file base64
- `cleanupDropZone(elementId)` — cleans up listeners

---

## 2. DRAG & DROP — `Components/App.razor` (global script block)

**Status:** ✅ WORKS
**Do not touch:** the `<script>` block with `dragover`/`drop` global preventDefault

---

## 3. DRAG & DROP — `Components/Dialogs/ExpenseUploadDialog.razor` (@code block)

**Status:** ✅ WORKS
**Do not touch these methods:**
- `OnAfterRenderAsync` — setupDropZone with retry logic
- `OnFileDropped` — [JSInvokable], receives metadata + calls getDropFileBase64
- `DisposeAsync` — cleanupDropZone
- `DroppedFile` — IBrowserFile wrapper class

**OK to change:** only the HTML upload phase and other @code methods

---

## 4. ULAK MODULE

**Status:** ✅ WORKS
**Do not touch:** the entire ULAK invoice generation and processing logic

ULAK rules:
- VAT = 6% (not 0%)
- Suppliers = physical persons (beekeepers)
- `ZERO_VAT` flag NOT needed for ULAK invoices — this is normal, not a bug

---

## 5. `OcrQueueWorker.cs`

**Status:** ✅ WORKS
**Do not touch:** the entire file
**Important:** uses `IExpenseOcrService.ExtractInvoiceDataAsync` — do not
remove this method from the interface (it's a compatibility alias for
`ProcessAsync` kept specifically because this worker calls it).

---

## 6. `ViesService.cs`

**Status:** ✅ WORKS
**Do not touch:** the entire file (timeout already configured — 15s)

---

## 7. Bank import module (`BankImport.razor`, `BankImportService.cs`)

**Status:** ✅ WORKS
**Do not touch:** without explicit permission

---

## How to handle these files

If you need to change a nearby function:
1. Read this file first
2. If you need to change a frozen block — ask the user for permission
3. If a bug is IN a frozen block — report it, do not change it silently

## How to identify the working version

If unsure whether a change broke something — check git log:
```bash
git log --oneline Components/Dialogs/ExpenseUploadDialog.razor | head -5
```
Look for commit: `feat: restore working drag and drop using setupDropZone JS interop`
