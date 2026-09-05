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

---

## 8. MIGRATIONS POLICY (effective 2026-08-27)

**Status:** POLICY — governs how all future schema changes are handled. Not a frozen code block.

- Migrations policy (effective 2026-08-27): every schema change must be paired with
  `dotnet ef migrations add <DescriptiveName>` at the time the change is made — do NOT
  batch schema changes into the InitialCreate convention anymore. Old migration history
  (pre-2026-08-27) is left as-is and will not be reconciled.
- Before adding a new migration, always run `dotnet ef migrations add <name> --dry-run`
  if available, or inspect the generated Up()/Down() methods before applying, to catch
  unintended diffs (e.g. decimal precision, missing [Table] attributes) before they
  reach the DB.
- Origin: this policy was established following the audit in
  `todo-audit-investigation-20260827-1911.md` (see `.opencode/reports/`).
- Auto-migrate on startup is dev-only (IsDevelopment() gated). Staging and prod require a manual
  `dotnet ef database update` after deploying code that includes a new migration — the app will log
  a warning listing pending migrations but will NOT apply them automatically outside development.

### Staging / Prod deployment steps
- If this deploy includes a new EF migration, run `dotnet ef database update` manually on the target
  server BEFORE or immediately after the container restart — check the startup logs for a
  'pending migrations' warning to confirm.

---

## 9. BLAZOR NAVIGATION — forceLoad:true usage

**Status:** ✅ WORKS (after fix — `forceLoad: true` removed from `UploadFirstVersion`)

**Do not touch:** any `Navigation.NavigateTo` call with `forceLoad: true` outside genuine
file/PDF download endpoints.

Working mechanism:
- SPA navigation (the default, no `forceLoad`) does NOT trigger a full browser reload —
  it updates the URL via the History API and re-renders in place, so the ASP.NET cookie
  authentication `LoginPath="/login"` redirect is never triggered for an already-authenticated
  session.
- `forceLoad: true` forces a real full browser reload (fresh HTTP GET) — on staging/prod this
  hits the auth middleware, and unauthenticated requests are redirected to `/login`. This is
  correct behavior ONLY for genuine file/PDF download endpoints that need a real browser GET
  (e.g. `document.pdf` downloads); it must NOT be used for ordinary in-app page navigation.

Reference: BUGLOG.md entry 2026-08-18 "Artwork Upload first version button redirects to /login
on prod" — error class `blazor-forceload-fullreload-auth-redirect`. The sibling button that
used SPA navigation (no `forceLoad`) worked correctly; the buggy one used `forceLoad: true`
and redirected to `/login` on staging/prod. No mechanical guardrail exists for this — it is a
documentation-only guardrail.

---

## 10. EF CORE LINQ — avoid enum array `.Contains()`

**Status:** ⚠️ KNOWN RUNTIME BUG — do not use this pattern

**Do not touch / do not write:** any EF Core LINQ `Where()`/`Any()`/`All()` predicate that
uses `enumArray.Contains(x.EnumProperty)` or `enumList.Contains(x.EnumProperty)` (where the
collection is an array/list of enum values and the property being tested is that same enum
type).

Working mechanism:
- The .NET runtime (on this stack) throws an exception during EF Core's LINQ parameter
  extraction phase when it encounters this pattern — the JIT/interpreter fails while
  evaluating `array.Contains(enumValue)` against a `ReadOnlySpan<InvoiceStatus>` that EF
  internally creates for the `.Contains()` call on an enum-typed collection. This is a
  genuine CLR/interpreter issue, not an EF Core bug and not application logic — the code is
  syntactically valid C# and compiles cleanly, so the failure only surfaces at runtime when
  the query executes.
- The fix is to replace with explicit `!=` comparisons chained together, e.g.:
  ```csharp
  // WRONG — triggers the interpreter bug at runtime:
  var statuses = new[] { InvoiceStatus.Draft, InvoiceStatus.Cancelled };
  invoices.Where(x => statuses.Contains(x.Status))

  // CORRECT — expand to explicit comparisons:
  invoices.Where(x => x.Status != InvoiceStatus.Draft && x.Status != InvoiceStatus.Cancelled)
  ```

Reference: BUGLOG.md entry 2026-08-29 — error class `enum-array-contains-readonlyspan-interpreter-bug`
(`InvoiceService.GetMonthlySalesVolumeAsync()`). No mechanical guardrail exists for this — it is a
documentation-only guardrail because the bug lives in the .NET runtime/interpreter itself and the
pattern is syntactically valid C#, so semgrep cannot reliably distinguish it from legitimate,
non-EF `.Contains()` usage.
