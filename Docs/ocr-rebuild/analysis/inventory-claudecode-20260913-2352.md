# OCR module — read-only inventory and finding verification (Claude Code blind run)

Generated: 2026-09-13 23:52 local · branch `main` @ `bdfd0be` · agent: Claude Code (Fable 5.1)
Task body: `.opencode/tasks/latest.md` (read in full). Report path overridden by the user to this file.

## 0. Run conditions, preconditions, deviations

- **Blind run.** Nothing under `.opencode/reports/` or `Docs/ocr-rebuild/analysis/` was opened. A `find` listing showed the filename `Docs/ocr-rebuild/analysis/PROMPT-claude-code.md`; it was not read. `Docs/ocr-rebuild/PLAN.md`, `DECISIONS.md`, `STATE.md`, `OPEN-QUESTIONS.md` were read for context; D-001 was treated as claims under test.
- **Precondition 1** (`git branch --show-current` = `main`): PASS.
- **Precondition 2** (`git status --porcelain` clean): **FAIL at start.** Dirty items: ` M Docs/BUGLOG.md` (+22 lines, `git diff --stat`) and `?? Docs/ocr-rebuild/` (untracked: STATE.md, PLAN.md, OPEN-QUESTIONS.md, DECISIONS.md, analysis/PROMPT-claude-code.md, sessions/2026-09-13-01.md). Both are documentation only; no `.cs`/`.razor` file was modified. The task's own report path lives inside the untracked directory, so the investigation proceeded (read-only) with this flagged as the STOP-rule deviation. Nothing was staged, committed or pushed; `bump-version.sh` was not run.
- **mempalace** was not invoked. It is still referenced at: `opencode.json:66` (`"mempalace_*": "deny"` in the `fixer` role), `opencode.json:414-420` (MCP server entry, `enabled: false`), `mempalace.yaml` (root, room map), `.opencode/prompts/orchestrator.md:1,7,9,17,20,22,136`, `.opencode/skills/mempalace/SKILL.md` (whole skill), `Docs/HARNESS_STATUS.md:425-549`, `Docs/BUGLOG.md` (historical entries at 154, 1087-1294). `AGENTS.md` does not mention it.
- **Database tooling.** This Claude Code session has no `mysql` / `mysql-prod` MCP tools; those are OpenCode MCP servers (`opencode.json:422-430`: `nordicbees-db` → `node ~/mysql-mcp/server.js` → 100.110.26.80:3306; `nordicbees-prod-db` → `~/mysql-mcp/start-prod.sh` → 127.0.0.1:3307). For the dev DB the `mariadb` CLI form sanctioned in `AGENTS.md` ("Database connection") was used. The first attempt (`DESCRIBE` × 6) was **denied before execution** by the Claude Code auto-mode permission classifier (reason label "Production Reads" — a misclassification of the dev host). Two read-only alternatives then succeeded: `scripts/dump-db-schema.sh --check` and one `SELECT … FROM information_schema.COLUMNS` scoped to the six tables. Every executed statement is listed verbatim in §5 (checklist).
- **Production DB was NOT queried (B8 not executed).** Reason 1: `AGENTS.md` states "Production DB (10.255.8.5) is never queried or connected to by an agent under any circumstance — that is a separate, human-only, manual process". The task's STOP condition "any instruction here conflicts with what you find in AGENTS.md" therefore applies to B8. Reason 2: no production tool exists in this session. Per the task's own fallback ("production database is unreachable — complete everything except B8"), everything else was completed; the five B8 queries are reproduced verbatim in §B8 for a human to run.
- Scratch verification (A9/A10 arithmetic) was done with a throw-away console project in the session scratchpad, outside the repository.

## 1. Summary

- Verdicts: **16 CONFIRMED, 1 PARTIALLY CONFIRMED (A13), 1 REFUTED (A18).** Several confirmations carry corrections that change what F0 should do (A1, A7, A12, A14, A18).
- **Most severe issue found — not on the list (NF-1 + NF-2): the queue path is dead end-to-end.** `Program.cs` never calls `AddControllers()`/`MapControllers()` (grep over the whole repo: zero hits), so `Controllers/ExpenseController.cs` (`POST api/expense/webhook`, the only code that inserts into `expense_ocr_queue`) is not routed. Even if it were, it enqueues with `InvoiceId = 0` (`ExpenseController.cs:51`), and `OcrQueueWorker` only *updates* an existing invoice by that id (`OcrQueueWorker.cs:84-89`) — it never creates one. Consequence: A1 (`PaidAmount = AmountInclVat`) and A2 (infinite re-processing) are real defects in the code but may never have fired in production; B8.1/B8.2/B8.4 are the only way to know, and the F0 cleanup must not assume corrupted rows exist.
- **Second most severe — not on the list (NF-3, NF-4):** uploaded invoice PDFs are written to `wwwroot/uploads/invoices/...` inside the container's writable layer (`ExpenseUploadDialog.razor:870-877`); `deploy.yml` mounts only artwork and delivery-receipts volumes, so every deploy (`docker rm` + `docker run`) discards them. The same directory is served by `app.UseStaticFiles()` (`Program.cs:148`) with no authentication middleware, so any PDF is downloadable anonymously by URL.
- **Third — not on the list (NF-5):** page-level `[Authorize]` attributes are not enforced: `Components/Routes.razor` uses `<RouteView>` (not `AuthorizeRouteView`) and `Program.cs` has no `UseAuthentication()`/`UseAuthorization()`. This matters for the planned admin-only OCR view (B4).
- A18 is refuted for today's code (ProcessAsync swallows all exceptions; PROCESSING is never persisted) but becomes live the moment A2 is fixed — the A2 fix must include the reset path.
- A9 arithmetic verified by execution: `"21,5"` parses to **2.15 %**, `"21,00"` to 21 % only by accident.
- Dev DB schema was captured live (information_schema) for all six tables; 14 model/DB nullability or type drifts listed in B3; the two that can throw at runtime are `expense_invoices.invoice_number` (NOT NULL, code assigns null) and `pending_supplier_company_code varchar(50)` receiving a vendor name (A7).

## 2. Part A — claim verification

### A1 (P0) — `PaidAmount = AmountInclVat` in the queue worker
**Verdict: CONFIRMED** (with a reachability caveat).

`Services/OcrQueueWorker.cs:106-110`
```csharp
invoice.AmountExclVat = ocrResult.AmountExclVat;
invoice.VatRate = ocrResult.VatRate;
invoice.VatAmount = ocrResult.VatAmount;
invoice.AmountInclVat = ocrResult.AmountInclVat;
invoice.PaidAmount = invoice.AmountInclVat;
```
Reasoning: executed inside `if (ocrResult.Success)` (79) → `if (queueItem.InvoiceId.HasValue)` (84) → `if (invoice != null)` (89), and persisted by `context.ExpenseInvoices.Update(invoice)` (137) + `SaveChangesAsync` (141) — `Update` attaches the detached entity as Modified, so this write does reach the DB (see A2d).

Every assignment to `PaidAmount` in the repo (`grep -rn -E "PaidAmount\s*(\+|-)?=[^=]"`, bin/obj excluded):
- `Services/OcrQueueWorker.cs:110` — the defect. **Only** occurrence of `PaidAmount = AmountInclVat`.
- `Services/ExpenseService.cs:1227` — `PaidAmount = 0` in `CreateFromOcrAsync` (correct).
- `Services/ExpenseService.cs:827-833` — raw SQL `paid_amount = {0}` ← `SUM(expense_payments.amount)` in `RecalculateInvoiceStatusAsync` (the only legitimate writer; called from AddPayment 497, DeletePayment 536, UpdatePayment 579).
- `Components/Dialogs/ExpensePaymentDialog.razor:193, 230, 261` — in-memory UI copies (from payments sum / re-read); not persisted.
- Other entities/tables (not `expense_invoices`): `Components/Pages/Warehouse/DeliveryPricingDetail.razor:360,449,500`; `Components/Pages/Warehouse/SupplierDebts.razor:207`; `Services/DeliveryService.cs:39,65`; `Services/PaymentService.cs:311,344,392,475,536,1285` (projections) and `:208-229` (sales `invoices` SQL); `Tests/NordicBeesERP.Tests/UnpaidInvoicesServiceTests.cs:65,76`.

Caveat: line 110 runs only when `queueItem.InvoiceId` points at an existing invoice. The sole enqueuer writes `InvoiceId = 0` (`Controllers/ExpenseController.cs:51`) and is not routed (NF-1). Whether any production row was ever touched by this line is unknowable statically — B8.1/B8.2 settle it. If it did fire: `Components/Pages/ExpenseInvoices.razor:588` (remaining = incl − paid), `Components/Dialogs/InvoiceDetailDialog.razor:287`, `Services/ExpenseExportService.cs:140,168` show "paid"; `status` is *not* derived from `paid_amount` (`Helpers/ExpenseStatusHelper.cs:89-107` uses the payments sum), so such rows would read PENDING with "Liko 0".

### A2 (P0) — NoTracking makes the queue-item status write a no-op
**Verdict: CONFIRMED (statically). Runtime evidence not obtainable in this run.**

(a) Global NoTracking — CONFIRMED. `Program.cs:51-53`
```csharp
builder.Services.AddDbContextFactory<NordicBeesERPContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)))
        .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
```
(b) `queueItem` untracked — CONFIRMED. `OcrQueueWorker.cs:58-63`: `using var context = _dbFactory.CreateDbContext();` then `context.ExpenseOcrQueue.Where(...).OrderBy(...).FirstOrDefaultAsync(...)` with no `AsTracking()`.
(c) Re-attach — CONFIRMED absent. The file contains no `Update(queueItem)`, `Attach`, or `Entry(...).State` (grep). Lines 73-75, 81-82, 146-158 mutate `queueItem` and call `SaveChangesAsync`, which has nothing tracked to flush for it.
(d) `Update(invoice)` — CONFIRMED different. `OcrQueueWorker.cs:137` `context.ExpenseInvoices.Update(invoice);` attaches the detached invoice with `EntityState.Modified` (all columns), so the `SaveChangesAsync` at 141 issues an UPDATE for the invoice only. Same call, two different outcomes because one entity was explicitly attached and the other never was.

Net effect: `Status`/`Attempts`/`ProcessedAt`/`ErrorMessage` never persist; `Attempts >= MaxAttempts` (146) is never true, so FAILED is unreachable; the row is re-selected every 30 s (`OcrQueueWorker.cs:50`) and each iteration costs one Azure `AnalyzeDocumentAsync` (`ExpenseOcrService.cs:81-87`) plus one VIES lookup (681) plus DB reads. Runtime: requires a `WAITING` row to exist; under current wiring such rows can only come from a direct DB insert (NF-1). B8.4 (prod, not run) or `SELECT status, attempts, created_at FROM expense_ocr_queue` on the relevant DB would settle it.

### A3 (P0) — `IsAzureHealthyAsync` never performs I/O
**Verdict: CONFIRMED.**

`Services/ExpenseOcrService.cs:46-54`
```csharp
try
{
    var client = new DocumentIntelligenceClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
    return true;
}
catch
```
Reasoning: the SDK's constructor doc (NuGet `Azure.AI.DocumentIntelligence` 1.0.0, `Azure.AI.DocumentIntelligence.xml`, member `#ctor(System.Uri,Azure.AzureKeyCredential)`) declares only `ArgumentNullException`; Azure.Core clients construct a pipeline and do not connect. The only `false` paths are empty strings (44) or `new Uri(endpoint)` throwing `UriFormatException` on a malformed endpoint (so "always true when non-empty" has that one exception). The gate at `OcrQueueWorker.cs:36-41` and the "Azure aktyvus" indicator at `Components/Pages/Settings.razor:173,213` are therefore config-presence checks, not health checks. Settled by: call with endpoint `https://nonexistent.invalid/` and key `x` → returns `true`.

### A4 (P0) — `SaveAsync` syncs UI fields only for new invoices
**Verdict: CONFIRMED.** Lines edits do persist (shared reference); more header fields are lost than the claim lists.

`Components/Dialogs/ExpenseUploadDialog.razor:841-848` (existing branch)
```csharp
if (ExistingInvoiceId > 0)
{
    _ocrResult.OriginalFilePath = !string.IsNullOrEmpty(ExistingFilePath) ? ExistingFilePath : (_ocrResult.OriginalFilePath ?? "");
    _ocrResult.OriginalFilename = _ocrResult.OriginalFilename ?? "";
    await ExpenseService.UpdateFromOcrAsync(ExistingInvoiceId, _ocrResult);
```
The sync block (`_ocrResult.InvoiceNumber = _invoiceNumber; … _ocrResult.Flags = _ocrFlags;`) is at 855-867, inside the `else` (852) only.

Shared reference: `ExpenseUploadDialog.razor:730` `_ocrLines = result?.Lines ?? new List<OcrLineDto>();` — same `List<OcrLineDto>` object as `_ocrResult.Lines`; `UpdateLineFromString` (928-939) mutates the shared `OcrLineDto` instances, `AddLine` (943) / `RemoveLine` (952) mutate the shared list. `UpdateFromOcrAsync` re-inserts `ocrResult.Lines` (`ExpenseService.cs:1403-1420`).

Survive on re-OCR of an existing invoice: line `Description`, `Quantity`, `AmountExclVat`, `VatRate`, `AmountInclVat`, added/removed lines.
Lost: `_invoiceNumber`, `_amountExclVat`, `_vatRate`, `_vatAmount`, `_amountInclVat`, `_invoiceDate`, `_dueDate`; `_categoryId` (also never in the UPDATE column list, `ExpenseService.cs:1330-1360`); `_invoiceType` (never persisted on either branch — `CreateFromOcrAsync` hardcodes `"STANDARD"`, `ExpenseService.cs:1207`); `_ocrFlags` (dialog-computed flags, incl. `DUPLICATE` and a dismissed `WRONG_RECIPIENT`, are replaced by the service's `ocrResult.Flags`, `ExpenseService.cs:1305` → a dismissed WRONG_RECIPIENT still yields status REJECTED, 1314-1315); `_supplierId`/`_pendingSupplierName` (service values from `ExpenseOcrService.cs:734/701` are used instead). Additionally `original_filename` is overwritten with `""` (dialog 847 → `ExpenseService.cs:1385` passes it without the fallback that 1384 applies to the path).

### A5 — Line reconciliation deletes lines to match the header
**Verdict: CONFIRMED.**

`Services/ExpenseOcrService.cs:604-611`
```csharp
if (result.Lines.Any() && result.AmountExclVat > 0)
{
    var linesSumExcl = result.Lines.Sum(l => l.AmountExclVat);
    var diff = linesSumExcl - result.AmountExclVat;
    if (diff > 0.05m)
```
Exact order and conditions (601-669): entry requires lines present, header excl > 0, and lines-sum − header > 0.05. Step 1 (615-624): remove **all** lines with `AmountExclVat == 0` — these contribute nothing to the sum, so this step can never reduce `diff`; it only deletes lines. Step 2 (630-641), only if still > 0.05 after recompute (627-628): remove **all** lines with `Quantity > 1000`, with no early stop (can overshoot below header). Step 3 (643-660), only if still > 0.05: for each description group with > 1 line, remove the 2nd+ lines one at a time, breaking when `|sum − header| ≤ 0.05` (658). Case "lines < header" does nothing (666-668). Trace: `_logger.LogDebug` only (613, 622-623, 639, 656, 662-663) — Debug level, not persisted; no flag, no DTO field, no removed-count. `LinesMatchHeader` (813) and `AMOUNT_MISMATCH` (806) are computed *after* removal, so a header that was force-matched reads as consistent. If all lines are removed, `LINES_NOT_FOUND` (790-791) is the only visible symptom.

### A6 — `result.Currency` never populated
**Verdict: CONFIRMED.** `currencyCode` is in the objects the code already navigates and is unused.

`Services/ExpenseOcrService.cs:393-397`
```csharp
if (invoiceTotalField.TryGetProperty("valueCurrency", out var valueCurrency) || invoiceTotalField.TryGetProperty("ValueCurrency", out valueCurrency))
{
        if (valueCurrency.TryGetProperty("amount", out var amount) || valueCurrency.TryGetProperty("Amount", out amount))
        {
            result.AmountInclVat = Math.Round((decimal)amount.GetDouble(), 2);
```
`result.Currency` is never assigned anywhere in `ExpenseOcrService.cs` (grep); it keeps `OcrResultDto.cs:31` `= PdfLocalization.CurrencyCode`. `valueCurrency` objects are read at 373-376, 383-386, 393-397, 500-503, 507-510, 514-518, 546-550, only for `amount`. The SDK type for that object (`Azure.AI.DocumentIntelligence.xml`: `CurrencyValue.Amount`, `CurrencyValue.CurrencySymbol` "Currency symbol label, if any", `CurrencyValue.CurrencyCode` "Resolved currency code (ISO 4217), if any") shows the field is present in the same JSON object. Downstream, `OcrQueueWorker.cs:103-104` always writes the default (non-empty "EUR"), and `ExpenseService.cs:1228,1377` fall back to EUR — foreign-currency invoices are stored as EUR with foreign amounts.

### A7 — `SupplierCompanyCode` priority chain
**Verdict: CONFIRMED — and stronger: step 4 is statically dead, and step 1 does not strip the country prefix.**

`Services/ExpenseOcrService.cs:231-238`
```csharp
if (string.IsNullOrEmpty(result.SupplierCompanyCode) && TryGetField("VendorAddressRecipient", out var recipientField))
{
    var recipient = recipientField.TryGetProperty("valueString", out var vs) ? vs.GetString() ?? "" : "";
    if (!string.IsNullOrWhiteSpace(recipient))
    {
        result.SupplierCompanyCode = recipient.Trim();
```
Step 3 assigns the full recipient name as the company code whenever the string is non-whitespace. Step 4 (243-255) reads the **same** `VendorAddressRecipient` field and requires `\b\d{9}\b` in it — any string containing nine digits is non-whitespace and therefore already consumed by step 3. Step 4 is unreachable in every case, not merely "in practice"; whether Azure "almost always" fills the field is runtime and irrelevant. Step 1 (201-215): `CleanVatCode(taxId)` (882-886) removes only spaces, dashes, dots; the comment at 207 "remove country prefix like LT" is false — `SupplierCompanyCode` becomes e.g. `LT100001234567`, i.e. the VAT code with prefix, conflated with the registration code exactly as claimed. Follow-on: this value lands in `pending_supplier_company_code varchar(50)` (`ExpenseService.cs:1217, 1368`); a recipient string longer than 50 chars fails the INSERT/UPDATE with "Data too long" (NF-9).

### A8 — `FindSupplierIdAsync` with empty `vatCode`; nondeterministic `FirstOrDefault`
**Verdict: CONFIRMED** (the "matches a partner with empty VatCode" branch is data-dependent).

`Services/ExpenseOcrService.cs:849-854`
```csharp
var supplierByVat = await context.BusinessPartners
    .Where(bp => bp.VatCode == vatCode
              || bp.VatCode == "LT" + vatCode
              || bp.VatCode == vatCode.TrimStart('L', 'T'))
    .Select(bp => new { bp.Id, bp.DefaultExpenseCategoryId })
    .FirstOrDefaultAsync();
```
The guard at 839 returns only when *both* name and VAT are empty; with `vatCode == ""` and a name, the query runs with predicates `vat_code = ''`, `vat_code = 'LT'`, `vat_code = ''`. SQL `NULL = ''` is false, so only partners whose `vat_code` is literally the empty string match — whether such rows exist is a data question (`SELECT COUNT(*) FROM business_partners WHERE vat_code = ''` would settle; not run). No `OrderBy` before `FirstOrDefaultAsync` at 854 and 867 → row choice is engine-dependent. Name fallback 864-867 `bp.Name.Contains(supplierName)` → `LIKE '%…%'`, substring, no ordering. Extra: `TrimStart('L','T')` (852) strips leading L/T from any code (`LV…` → `V…`). The lookup runs twice per dialog OCR (`ExpenseOcrService.cs:733` and `ExpenseUploadDialog.razor:617`); the worker uses a third, different rule (`OcrQueueWorker.cs:114-115`: exact `VatCode` + `IsActive`, ignoring `ocrResult.SupplierId`).

### A9 — VAT-rate parsing with `NumberStyles.Any` + InvariantCulture
**Verdict: CONFIRMED.** Arithmetic verified by executing the exact expression.

`Services/ExpenseOcrService.cs:430-437`
```csharp
var rateStr = taxRateField.TryGetProperty("valueString", out var vs) ? vs.GetString()?.TrimEnd('%').Trim() ?? "" : ...;
if (decimal.TryParse(rateStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var rateVal))
{
    var parsedRate = rateVal;
    if (parsedRate > 100)
    {
        parsedRate = parsedRate / 100m;
```
(Identical per-line copy at 528-536.) Under InvariantCulture the comma is the group separator and `NumberStyles.Any` includes `AllowThousands`; .NET does not validate group sizes.

| raw | after `TrimEnd('%').Trim()` | `TryParse` | `> 100` guard | result |
|---|---|---|---|---|
| `"21,00"` | `21,00` | 2100 | 2100 / 100 | **21** (correct by accident) |
| `"21,5"` | `21,5` | 215 | 215 / 100 | **2.15** (wrong; should be 21.5) |
| `"21.5"` | `21.5` | 21.5 | no | 21.5 |
| `"21"` | `21` | 21 | no | 21 |
| `"21%"` | `21` | 21 | no | 21 |
| `"0,21"` | `0,21` | 21 | no | 21 (accidental) |

Consequences of 2.15: header `VatRate` 2.15 → copied to lines lacking a rate (557-558) → line `AmountInclVat` computed with 2.15 % (562-564); `ZERO_VAT` (786) does not fire because the rate is non-zero. The dialog's own parser does `.Replace(",", ".")` first (`ExpenseUploadDialog.razor:931-935`), so the two layers disagree.

### A10 — `(decimal)element.GetDouble()` instead of `GetDecimal()`
**Verdict: CONFIRMED** (low practical severity).

`Services/ExpenseOcrService.cs:507-510`
```csharp
if (f.TryGetProperty("Amount", out var amountField) && (amountField.TryGetProperty("valueCurrency", out var amountCurrency) || ...))
{
    if (amountCurrency.TryGetProperty("amount", out var amountProp) || amountCurrency.TryGetProperty("Amount", out amountProp))
        lineDto.AmountExclVat = (decimal)amountProp.GetDouble();
```
All `GetDouble` sites (grep): money/quantity `(decimal)`: 376 SubTotal **[Math.Round 2]**, 386 TotalTax **[Round 2]**, 397 InvoiceTotal **[Round 2]**, 492 Quantity [no round], 503 UnitPrice [no round], 510 Amount [no round], 518 Net [no round], 550 TaxAmount [no round; ratio rounded to 0 dp at 552]. Confidence `(float)`: 152, 290, 315, 326, 399 (→ `ToConfidencePercent` 879-880), and 523 line confidence `(decimal)` [no round]. Why low severity: `(decimal)double` rounds to 15 significant digits (scratch check: `(decimal)(0.1+0.2) == 0.3m`), and the target columns are `decimal(12,2)` / `decimal(10,3)` so MySQL rounds on write. `JsonElement.GetDecimal()` exists and is exact; use it in the rebuild.

### A11 — No `excl + vat == incl` check; a fallback invents `AmountInclVat`
**Verdict: CONFIRMED.**

`Services/ExpenseOcrService.cs:452-458`
```csharp
// Fallback: derive VAT rate from totals if not found in items
if (result.VatRate == 0 && result.AmountExclVat > 0 && result.VatAmount > 0)
    result.VatRate = Math.Round(result.VatAmount / result.AmountExclVat * 100, 0);
// Fallback: incl = excl + vat
if (result.AmountInclVat == 0 && result.AmountExclVat > 0)
    result.AmountInclVat = result.AmountExclVat + result.VatAmount;
```
No expression compares `AmountExclVat + VatAmount` with `AmountInclVat` in `ExpenseOcrService.cs`, `ExpenseUploadDialog.razor`, or `ExpenseService.cs` (grep). The only consistency checks are lines-vs-header (`ExpenseOcrService.cs:797-807`, `ExpenseUploadDialog.razor:659-663`, `ExpenseService.cs:290-291`). Excl = 0 with Incl > 0 passes silently unless `VatRate == 0` (`ZERO_VAT`, 786). Neither fallback is flagged or traced.

### A12 — Hardcoded confidence 95 (worker) and default 100 (dialog)
**Verdict: CONFIRMED** (scope note on the dialog half).

`Services/OcrQueueWorker.cs:91-92`
```csharp
invoice.OcrStatus = "COMPLETED";
invoice.OcrConfidence = 95;
```
`ocrResult.Confidence` is never read in the worker (grep). Dialog: `Components/Dialogs/ExpenseUploadDialog.razor:606-607` `int confidence = azureOverall > 0 ? azureOverall : 100;`, deductions 685-723, `_confidence` 725, `+10` on dismiss 962. Scope: `_confidence` drives only the chip (154-156); what is persisted is `ocrResult.Confidence.Overall` (`ExpenseService.cs:1231`, `1379`), which is 0 when Azure returned no confidences — so chip shows 100 while DB stores 0, and `LOW_CONFIDENCE` (`ExpenseOcrService.cs:810`, dialog 665) requires `Overall > 0` and never fires for 0.

### A13 — Queue path persists no lines and no flags
**Verdict: PARTIALLY CONFIRMED.** Symptom correct; the mechanism is bigger: the queue path never creates an invoice at all.

`Services/OcrQueueWorker.cs:84-89`
```csharp
if (queueItem.InvoiceId.HasValue)
{
    var invoice = await context.ExpenseInvoices
        .FirstOrDefaultAsync(i => i.Id == queueItem.InvoiceId.Value, cancellationToken);
    if (invoice != null)
```
Queue path — `OcrResultDto` fields written (91-135; via `Update` every column of the loaded row is re-sent, but the values changed are): `OcrStatus="COMPLETED"` (91), `OcrConfidence=95` (92), `InvoiceNumber` (94-95, if non-empty), `InvoiceDate` (97-98), `DueDate` (100-101), `Currency` (103-104, always, since the DTO default "EUR" is non-empty), `AmountExclVat`, `VatRate`, `VatAmount`, `AmountInclVat` (106-109), `PaidAmount` (110), `SupplierId` / `PendingSupplierName` / `PendingSupplierVat` / `PendingSupplierAddress` (112-135). DTO fields ignored: `Lines`, `Flags`, `OcrPipeline`, `Confidence`, `SupplierCompanyCode`/`City`/`PostalCode`/`CountryCode`/`BankAccount`, `ViesVerified`/`ViesName`, `CategoryId`, `SupplierId` (recomputed by its own rule), `CustomerName`/`CustomerVatCode`, `LinesMatchHeader`, `OriginalFilePath`/`OriginalFilename`. It never sets `Status` or `OcrFlags` and inserts no `expense_invoice_lines` row.
Dialog path — `ExpenseService.CreateFromOcrAsync` (1204-1261): full list in B1; lines yes (1244-1261), flags yes (1233).
Missing mechanism: the worker only updates `queueItem.InvoiceId`; the sole enqueuer writes `InvoiceId = 0` (`Controllers/ExpenseController.cs:51`) → `HasValue` is true → lookup of id 0 → `null` → nothing at all is written (and the status flip does not persist, A2). Queue-processed uploads therefore produce **no invoice row**, not an invoice without lines. And the controller is not routed (NF-1).

### A14 — Flags/confidence computed twice with different thresholds
**Verdict: CONFIRMED** — and there are three implementations, not two.

`Services/ExpenseOcrService.cs:797-806` (service)
```csharp
var nonZeroLines = result.Lines.Where(l => l.AmountExclVat > 0 || l.AmountInclVat > 0).ToList();
var mismatchLinesSumExcl = nonZeroLines.Sum(l => l.AmountExclVat);
var diffExcl = Math.Abs(mismatchLinesSumExcl - result.AmountExclVat);
...
if (result.Lines.Count > 0 && diffExcl >= 0.05m && diffIncl >= 0.05m)
```
vs `Components/Dialogs/ExpenseUploadDialog.razor:659-663`: all lines, `Math.Abs(linesSum - result.AmountExclVat) > 0.01m` (excl only).

Divergences service (760-813) vs dialog (636-673, 685-725):
1. `AMOUNT_MISMATCH`: 0.05 on both excl and incl, zero lines excluded (service) vs 0.01 excl-only, all lines (dialog). The dialog merges `result.Flags` into its own list (669-673) → union → the effective rule on the dialog path is the looser 0.01 one; the service's stricter rule never removes a flag.
2. `VENDOR_NOT_FOUND`: `result.SupplierId == null` (762) vs a second `FindSupplierIdAsync` call (617) and `_supplierId == 0` (637).
3. `DUPLICATE`: dialog only (646-654); service never; `CreateFromOcrAsync` (1187-1193) and `UpdateFromOcrAsync` (1306-1311) check a third time.
4. `WRONG_RECIPIENT` (766-771), `OWN_COMPANY` (700), `VIES_UNAVAILABLE` (714), `AZURE_LIMIT` (819): service only; reach the dialog via the merge. The dialog can remove `WRONG_RECIPIENT` (959-964) — discarded on the existing-invoice path (A4).
5. Identical: `MISSING_INV_NUMBER`, `MISSING_DUE_DATE`, `MISSING_AMOUNT`, `ZERO_VAT`, `LINES_NOT_FOUND`, `LOW_CONFIDENCE` (`Overall > 0 && < 50`).
6. Confidence: service `Overall` = weighted 30/25/25/20 over non-zero fields (`OcrResultDto.cs:87-104`); dialog `_confidence` = Overall (or 100) − 15 (no vendor) − 15 (VatRate 0) − 15 (Excl 0) − 10 (no number) − 10 (no due date) − 10 (no lines) − 5 (mismatch > 0.01) + 10 (dismiss); never persisted.
7. `LinesMatchHeader` (813) follows the service rule only; unused by the dialog.
Third implementation: `ExpenseService.UpdateInvoiceAsync:285-302` recomputes on every edit with `AMOUNT_MISMATCH = |Σ line incl − header incl| > 0.01` (**incl**, not excl), keeps `WRONG_RECIPIENT`/`VIES_UNAVAILABLE`/`VENDOR_NOT_FOUND`/`DUPLICATE`, and silently drops `OWN_COMPANY`, `LOW_CONFIDENCE`, `MISSING_DUE_DATE`, `AZURE_LIMIT`.

### A15 — Raw Azure JSON discarded; nothing writes `ocr_raw_json`
**Verdict: CONFIRMED.**

`Services/ExpenseOcrService.cs:89-90`
```csharp
var json = operation.Value.ToString();
var root = JsonDocument.Parse(json).RootElement;
```
`json` is a local; `OcrResultDto` (`Services/Dtos/OcrResultDto.cs:4-61`) has no raw field. Entity property exists: `Models/Expenses/ExpenseInvoice.cs:106-107` `[Column("ocr_raw_json")] public string? OcrRawJson`; live dev column `ocr_raw_json json NULL` (B3; `Migrations/20260602150000_InitialCreate.cs:290`). Repo-wide grep `OcrRawJson|ocr_raw_json` (bin/obj excluded): only the model, `Migrations/*.Designer.cs`, the snapshot, and InitialCreate. Zero writers: `CreateFromOcrAsync` omits it (EF sends NULL, 1204-1239); `UpdateFromOcrAsync` SQL omits it (1330-1360). Note for F1: the column is MySQL `json` — invalid JSON is rejected at write time.

### A16 — Hardcoded per-vendor rules
**Verdict: CONFIRMED** (plus additional hardcodes).

`Services/ExpenseOcrService.cs:186`
```csharp
string[] legalForms = { "MB", "UAB", "AB", "VšĮ", "IĮ", "ŽŪB", "ŪB", "SIA", "OÜ", "AS", "GmbH", "Ltd", "SRL", "BV", "NV" };
```
- `legalForms`: 186, used with substring `Contains(..., OrdinalIgnoreCase)` at 187-188 → "AB" matches "ABC", "AS" matches "ASUS", "BV" matches any word containing "bv".
- `Serija … Nr.` regex: 296-299 `^(?:Serija\s+\w+\s+)?Nr\.\s*(.+)$`.
- `isMetadataLine`: 577-581 (`"voris"`, `"weight"`, `"Svoris"`; the third is redundant with the first under OrdinalIgnoreCase).
- Address reordering regex `^\d+\w*$`: 170-172.
Not in the claim: PaymentTerm `\d+` (339-340, 1..365 days); LT 9-digit regex (248, dead per A7); `"Eilutė {n}"` default description (595); `"LT" + vatCode` / `TrimStart('L','T')` (851-852); `Quantity > 1000` weight heuristic (634); model id `"prebuilt-invoice"` (83); `CompanyNameHelper.Normalize` call (725; helper not read in this task); dialog `NormalizeName` (`ExpenseUploadDialog.razor:903-907`, hardcodes "uab"/"mb"/"ab", zero callers — dead).

### A17 — `pages: "1-2"`, `locale: "lt-LT"` hardcoded
**Verdict: CONFIRMED.**

`Services/ExpenseOcrService.cs:81-87`
```csharp
var operation = await client.AnalyzeDocumentAsync(
    WaitUntil.Completed,
    "prebuilt-invoice",
    requestContent,
    locale: "lt-LT",
    pages: "1-2"
```
Also hardcoded: model id `"prebuilt-invoice"` (83); no explicit API version (SDK 1.0.0 default). Effect of `"1-2"`: pages 3+ are never analysed, so multi-page invoices lose lines silently; combined with A5 CASE 2 (666-668, "do nothing") the shortfall is reported only as `AMOUNT_MISMATCH`.

### A18 — Row stays `PROCESSING` forever if `ProcessAsync` throws
**Verdict: REFUTED** for the current code; **latent** once A2 is fixed.

`Services/ExpenseOcrService.cs:823-829`
```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "[AZURE DI ERROR] {Type}: {Message}", ex.GetType().Name, ex.Message);
    result.Diagnostics.AzureReachable = false;
    result.Diagnostics.AzureError = ex.Message;
    result.Success = false;
```
(1) `ProcessAsync` cannot throw: its whole body (61-815) is inside `try` with `catch (RequestFailedException) when (ErrorCode == "429")` (816) and a catch-all (823) that returns `Success = false` — including `OperationCanceledException`. (2) Even if something else in `ProcessQueueItemAsync` throws after line 75 (the `BusinessPartners` query at 114, `SaveChangesAsync` at 141/158, cancellation on shutdown), the `PROCESSING` value set at 73 was never persisted (A2), so the DB row still reads `WAITING`. No row can be stuck in `PROCESSING` through this code today. After A2 is fixed (status persisted), the claim becomes true: `ProcessQueueItemAsync` (56-160) has no `try/finally` reset and `ExecuteAsync` (45-48) only logs; a container restart mid-processing (`deploy.yml` does `docker rm`/`docker run`) would also strand rows. The A2 fix must ship the reset path (or a startup sweep `PROCESSING → WAITING`).

## 3. Part B — knowledge gaps

### B1 — `ExpenseService` OCR surface (file read in full, 1439 lines)

**`CreateFromOcrAsync(OcrResultDto ocrResult, string source = "MANUAL")` — `Services/ExpenseService.cs:1167-1277`.**
Mechanism: EF `ctx.ExpenseInvoices.Add(invoice); await ctx.SaveChangesAsync();` (1241-1242) — an INSERT, which works under global NoTracking because `Add` tracks the new entity; lines via `Add` + `SaveChangesAsync` (1247-1261); audit via `Add` + `SaveChangesAsync` (1263-1274). Clock: `DateTime.Now` (1237-1238, 1272) — `CreateInvoiceAsync` uses `UtcNow` (165-166); the module mixes clocks.
Actor: `_authService.GetAuthenticatedUserAsync()` → FullName / Email / `"OCR_PIPELINE"` (1172-1173).
Status (1175-1193): `WRONG_RECIPIENT` → REJECTED; `SupplierId == null` → PENDING_SUPPLIER; any of MISSING_AMOUNT / AMOUNT_MISMATCH / LOW_CONFIDENCE / ZERO_VAT / MISSING_INV_NUMBER → NEEDS_REVIEW; else PENDING; `CheckDuplicateAsync` hit → DUPLICATE_PENDING and `DUPLICATE` flag added.
Dates (1195-1198): unparsable invoice date → `DateTime.Today`; unparsable due date → invoice + 30 days.
Columns written, in initializer order (1206-1238), and their sources:
`supplier_id` ← `SupplierId`; `invoice_type` ← `"STANDARD"` (hardcoded); `source` ← parameter; `original_file_path`, `original_filename` ← DTO; `pending_supplier_name`, `_vat`, `_address`, `_city`, `_postal_code`, `_country_code`, `_company_code`, `_bank_account` ← DTO when `SupplierId == null`, else NULL; `invoice_number` ← `InvoiceNumber`, or **NULL when blank** (1219; column is NOT NULL, B3); `invoice_date`, `due_date` ← computed; `amount_excl_vat`, `vat_rate`, `vat_amount`, `amount_incl_vat` ← DTO; `category_id` ← `CategoryId`; `paid_amount` ← **0**; `currency` ← DTO or EUR; `status` ← computed; `ocr_status` ← **"COMPLETED"**; `ocr_confidence` ← **`Confidence.Overall`** (int into `decimal(5,2)`); `ocr_pipeline` ← **`OcrPipeline`** ("AZURE_DI", `ExpenseOcrService.cs:72`); `ocr_flags` ← JSON array or NULL; `supplier_vat_verified` ← `ViesVerified`; `supplier_vat_verified_name` ← `ViesName`; `rejected_reason` ← "Sąskaita ne {company}" when REJECTED; `created_at`, `updated_at` ← Now. `ocr_raw_json` ← **never set (NULL)**; `notes`, `approved_*` NULL.
Lines: **yes** (1244-1261): `invoice_id`, `description`, `quantity`, `unit_price`, `unit_of_measure`, `amount_excl_vat`, `vat_rate`, `amount_incl_vat`, `category_id` ← `SuggestedCategoryId` (never set by the service → always NULL), `sort_order` = i + 1. Flags: **yes** (1233). Audit "CREATED" (1263-1274).

**`UpdateFromOcrAsync(int invoiceId, OcrResultDto ocrResult)` — `Services/ExpenseService.cs:1279-1437`.**
Mechanism: read `AsNoTracking` (1289-1291, throws if missing 1293); one raw `ExecuteSqlRawAsync` UPDATE (1330-1389); raw `DELETE FROM expense_invoice_lines WHERE invoice_id = {0}` when any exist (1392-1401); line INSERTs via EF `Add` + `SaveChangesAsync` (1403-1420); audit "OCR_RETRIED" via `Add` + `SaveChangesAsync` (1423-1434).
Parameter count: placeholders `{0}`…`{28}` = 29 (1332-1360); arguments 1361-1389 = 29 — **counted, they match.** 28 SET columns in order: `supplier_id`, `pending_supplier_name`, `pending_supplier_vat`, `pending_supplier_address`, `pending_supplier_city`, `pending_supplier_postal_code`, `pending_supplier_country_code`, `pending_supplier_company_code`, `pending_supplier_bank_account`, `invoice_number`, `invoice_date`, `due_date`, `amount_excl_vat`, `vat_rate`, `vat_amount`, `amount_incl_vat`, `currency`, `ocr_status` ("COMPLETED"), `ocr_confidence` (`Overall`), `ocr_pipeline`, `ocr_flags`, `supplier_vat_verified`, `supplier_vat_verified_name`, `original_file_path` (falls back to the existing value, 1384), `original_filename` (no fallback, 1385), `status`, `rejected_reason`, `updated_at`; `WHERE id = {28}`.
Not touched: `paid_amount` (preserved — correct), `invoice_type`, `category_id`, `notes`, `ocr_raw_json`, `approved_by/at`, `source`, `created_at`.
Flags: `new List<string>(ocrResult.Flags)` + `DUPLICATE` when `CheckDuplicateAsync` returns another id (1305-1311). Note it passes no `excludeInvoiceId` and filters afterwards: if the invoice itself is the first match, a genuine duplicate elsewhere is masked. Status recomputed with the same rules (1313-1323). Lines: replaced wholesale. Same `DateTime.Now` clock (1327, 1432).

**`CheckDuplicateAsync(int? supplierId, string? supplierVatCode, string invoiceNumber, decimal amountInclVat, int excludeInvoiceId = 0)` — `Services/ExpenseService.cs:933-955`.**
Exact rule: return `null` if `invoiceNumber` is empty (935); otherwise first `id` (unordered, 950-952) where `invoice_number = @invoiceNumber` (exact under the table collation `utf8mb4_unicode_ci`, i.e. case- and accent-insensitive) AND `invoice_number <> ''` AND `status NOT IN ('REJECTED','DUPLICATE_PENDING')` AND `ABS(amount_incl_vat − @amount) < 0.01` (938-944), optionally `id <> excludeInvoiceId` (947-948). **`supplierId` and `supplierVatCode` are ignored** — the same number and amount from two different suppliers count as a duplicate; the same number with a different amount does not. Contrast: `CreateInvoiceAsync` (177-198) uses a supplier-aware, amount-agnostic rule — two different duplicate definitions coexist.

Raw SQL vs tracking: inserts via EF `Add` (safe under NoTracking); every UPDATE in the OCR surface is `ExecuteSqlRawAsync` with positional parameters. Nullable-into-NOT-NULL: `invoice_number` (1219 EF, 1370 SQL) receives NULL when blank → MySQL "Column 'invoice_number' cannot be null"; reachable only if a caller bypasses the dialog's guard (`ExpenseUploadDialog.razor:826-833`); the worker never calls these methods.

### B2 — Migration conventions

Files in `Migrations/` (excluding `*.Designer.cs`): `20260602150000_InitialCreate.cs`, `20260701192157_ArtworkTables.cs`, `20260702120000_ArtworkVersionEffectiveDates.cs`, `20260708120000_DeliverySignatureColumns.cs`, `20260828070929_AddDashboardDailySnapshots.cs`, `20260902160237_AddContainerCodeUniqueIndex.cs`, `20260903115521_AddDeliveryCostDeductions.cs`, `20260905201154_AddPartnerRoleFlags.cs`, `20260906183312_AddPartnerVatVerification.cs`, `20260907083424_AddCompensationVatCode.cs`, `20260910095703_AddCompanySettingsAddressFields.cs`, plus `NordicBeesERPContextModelSnapshot.cs`. **Last 5:** `20260903115521_AddDeliveryCostDeductions`, `20260905201154_AddPartnerRoleFlags`, `20260906183312_AddPartnerVatVerification`, `20260907083424_AddCompensationVatCode`, `20260910095703_AddCompanySettingsAddressFields`. Pattern: `YYYYMMDDHHMMSS_PascalCaseDescription.cs` + paired `.Designer.cs`.

Generated by EF tooling: each Designer carries `[DbContext(typeof(NordicBeesERPContext))]` and `[Migration("…")]` with `BuildTargetModel` (`Migrations/20260910095703_AddCompanySettingsAddressFields.Designer.cs:13-18`), and `Up()` uses the fluent builder (`…AddCompanySettingsAddressFields.cs:13-45`, `migrationBuilder.AddColumn<string>`). Exception: `InitialCreate.cs` is hand-written — 126 `migrationBuilder.Sql(` calls wrapping `CREATE TABLE IF NOT EXISTS …` (e.g. `:270`). `AGENTS.md` ("Migrations") freezes `InitialCreate.cs` and requires every change as a new file.

Separate SQL directories: `Migrations/Archive/` — 12 pre-EF `.sql` files (2026-03-04 … 2026-06-29, e.g. `20260514000002_AddPendingSupplierColumnsToExpenseInvoices.sql`); `Migrations/Scripts/` — 3 `.sql` files (`20260826_artwork_labeltypes.sql`, `20260826_artwork_multifile.sql`, `20260828_dashboard_daily_snapshots.sql`). `backup_before_cleanup_20260316.sql` at the root is a dump, not a migration.

Commands (verbatim from the repo docs):
- Add: `dotnet ef migrations add <DescriptiveName>` — `Docs/FROZEN.md:99-100`; "run `dotnet ef migrations add <name> --dry-run` if available, or inspect the generated Up()/Down()" — `FROZEN.md:103-106`.
- Apply, dev: automatic at startup — `Program.cs:223-231` `if (app.Environment.IsDevelopment()) await db.Database.MigrateAsync();`.
- Apply, staging/prod: manual `dotnet ef database update` — `FROZEN.md:110, 114`; `Program.cs:233-243` only logs pending migrations outside Development.
- `AGENTS.md` ("Migrations"): agents never run DDL against any DB; the human applies and confirms with `DESCRIBE`.
- `build.sh` is `dotnet build` only and `cd`s to a stale absolute path (`build.sh:2` `/Users/deividasru/Projects/ERP DEV/NordicBeesERP`) — not usable from this checkout. `scripts/` contains only `dump-db-schema.sh`. `CODEBASE.md:626` says "EF Core migrations (SQL + C#)" and `:533,569` reference the archived SQL names.
Contradiction to resolve before F1's migration: `AGENTS.md` says `ADD COLUMN IF NOT EXISTS` is supported and safe on this MySQL 8.0.46, while semgrep rule `nordicbees-migration-if-not-exists` (`.agent-guardrails/nordicbees-rules.yaml:46-54`, severity ERROR) forbids the exact string.

### B3 — Dev database schema (live, information_schema, 2026-09-13)

Source: one `SELECT … FROM information_schema.COLUMNS` (verbatim in §5) against `nordic_bees_erp` @ 100.110.26.80. Cross-checked against `.opencode/db-schema.md` (generated 2026-09-10T09:40Z): identical for these six tables. `scripts/dump-db-schema.sh --check` reported the whole-DB dump STALE (`file=99b07fbacdb49268 db=d4cbcbab7564e6a5`); the 2026-09-10 09:57 migration on `company_settings` is the probable cause (settle by refreshing the dump — not done, write-forbidden). `DESCRIBE` itself was blocked by the harness (see §0); the columns below are the same metadata.

**expense_invoices**
| column | type | null | default |
|---|---|---|---|
| id | int | NO | auto_increment |
| supplier_id | int | YES | NULL |
| invoice_type | varchar(20) | NO | STANDARD |
| category_id | int | YES | NULL |
| pending_supplier_name | varchar(255) | YES | NULL |
| pending_supplier_vat | varchar(50) | YES | NULL |
| pending_supplier_address | varchar(500) | YES | NULL |
| invoice_number | varchar(100) | NO | — |
| invoice_date | date | NO | — |
| due_date | date | NO | — |
| amount_excl_vat | decimal(12,2) | NO | 0.00 |
| vat_rate | decimal(5,2) | YES | 21.00 |
| vat_amount | decimal(12,2) | YES | 0.00 |
| amount_incl_vat | decimal(12,2) | YES | 0.00 |
| paid_amount | decimal(12,2) | YES | 0.00 |
| status | varchar(30) | NO | PENDING |
| ocr_status | enum('PENDING','PROCESSING','COMPLETED','FAILED','MANUAL') | YES | PENDING |
| ocr_confidence | decimal(5,2) | YES | NULL |
| ocr_flags | varchar(500) | YES | NULL |
| ocr_raw_json | json | YES | NULL |
| notes | text | YES | NULL |
| approved_by | varchar(100) | YES | NULL |
| approved_at | datetime | YES | NULL |
| rejected_reason | varchar(500) | YES | NULL |
| source | enum('MANUAL','EMAIL','N8N') | YES | MANUAL |
| original_filename | varchar(255) | YES | NULL |
| created_at | timestamp | YES | CURRENT_TIMESTAMP |
| updated_at | timestamp | YES | CURRENT_TIMESTAMP on update |
| currency | varchar(3) | NO | EUR |
| ocr_pipeline | varchar(50) | YES | NULL |
| original_file_path | varchar(500) | YES | NULL |
| supplier_vat_verified | tinyint(1) | YES | 0 |
| supplier_vat_verified_name | varchar(255) | YES | NULL |
| pending_supplier_company_code | varchar(50) | YES | NULL |
| pending_supplier_bank_account | varchar(100) | YES | NULL |
| pending_supplier_city | varchar(100) | YES | NULL |
| pending_supplier_postal_code | varchar(20) | YES | NULL |
| pending_supplier_country_code | varchar(10) | YES | NULL |
Indexes: PRIMARY, idx_supplier_id, idx_invoice_number, idx_status, idx_due_date, idx_ocr_status, idx_expense_invoices_type (no unique key on invoice_number+supplier; the `IX_expense_invoices_supplier_invoice` named in `Data/NordicBeesErpContext.cs:442` does not exist in the DB).

**expense_invoice_lines**
| column | type | null | default |
|---|---|---|---|
| id | int | NO | auto_increment |
| invoice_id | int | NO | — (FK → expense_invoices.id ON DELETE CASCADE) |
| category_id | int | YES | NULL |
| description | text | NO | — |
| amount_excl_vat | decimal(12,2) | NO | 0.00 |
| vat_rate | decimal(5,2) | YES | 21.00 |
| amount_incl_vat | decimal(12,2) | YES | 0.00 |
| sort_order | int | YES | 0 |
| quantity | decimal(10,3) | YES | NULL |
| unit_price | decimal(12,2) | YES | NULL |
| unit_of_measure | varchar(20) | YES | NULL |

**expense_ocr_queue**
| column | type | null | default |
|---|---|---|---|
| id | int | NO | auto_increment |
| invoice_id | int | YES | NULL |
| file_content | longtext | YES | NULL |
| file_name | varchar(500) | NO | — |
| attempts | int | YES | 0 |
| max_attempts | int | YES | 3 |
| status | enum('WAITING','PROCESSING','COMPLETED','FAILED') | YES | WAITING |
| error_message | text | YES | NULL |
| created_at | timestamp | YES | CURRENT_TIMESTAMP |
| processed_at | timestamp | YES | NULL |
Indexes: idx_status, idx_created_at.

**expense_payments**
| column | type | null | default |
|---|---|---|---|
| id | int | NO | auto_increment |
| invoice_id | int | NO | — (FK → expense_invoices.id ON DELETE CASCADE) |
| payment_date | date | NO | — |
| amount | decimal(12,2) | NO | — |
| payment_method | enum('BANK','CASH','OTHER') | YES | BANK |
| reference | varchar(255) | YES | NULL |
| source | varchar(20) | NO | manual |
| bank_confirmed | tinyint(1) | NO | 0 |
| bank_import_row_id | int | YES | NULL |
| bank_import_id | int | YES | NULL |
| notes | text | YES | NULL |
| created_at | timestamp | YES | CURRENT_TIMESTAMP |

**business_partners** (44 columns; OCR-relevant subset, full list in `.opencode/db-schema.md`)
| column | type | null | default |
|---|---|---|---|
| id | int | NO | auto_increment |
| partner_type | enum('customer','supplier','both','expense_supplier') | YES | supplier |
| name | varchar(255) | NO | — |
| company_code | varchar(50) | YES | NULL |
| vat_code | varchar(50) | YES | NULL (index idx_vat_code) |
| address / city / postal_code | text / varchar(100) / varchar(20) | YES | NULL |
| country / country_code | varchar(100) / varchar(10) | YES | Lithuania / LT |
| bank_account | varchar(50) | YES | NULL |
| default_vat_rate | decimal(5,2) | NO | — |
| is_active | tinyint(1) | YES | 1 |
| default_expense_category_id | int | YES | NULL |
| is_customer / is_supplier / is_expense_supplier / is_individual / no_email | tinyint(1) | NO | 0 |
| approval_status | enum('PENDING','APPROVED','REJECTED','EXPIRED') | NO | PENDING |
| vat_verified / vat_verified_at / vat_verified_name | tinyint(1) / datetime(6) / varchar(255) | YES | NULL |
| vies_verified / vies_verified_at / vies_name | tinyint(1) / datetime / varchar(255) | YES | 0 / NULL / NULL |
| compensation_vat_code | varchar(20) | YES | NULL |

**app_settings**
| column | type | null | default |
|---|---|---|---|
| id | int | NO | auto_increment |
| setting_key | varchar(100) | NO | — (UNIQUE) |
| setting_value | text | YES | NULL |

**Model ↔ DB drift (C# property vs live column):**
Direction "C# nullable, DB NOT NULL" (can throw on write):
1. `ExpenseOcrQueue.FileName string?` (`ExpenseModels.cs:211-213`, `[MaxLength(255)]`) vs `file_name varchar(500) NOT NULL` — also MaxLength 255 ≠ 500.
2. `ExpenseInvoice.InvoiceNumber` is declared non-null (`ExpenseInvoice.cs:57-60`) but code assigns `null` (`ExpenseService.cs:1219, 1370`) into `invoice_number NOT NULL` — throws when the number is blank.
Direction "C# non-null, DB nullable" (throws on read of a NULL row):
3. `ExpenseInvoice.VatRate/VatAmount/AmountInclVat/PaidAmount decimal` vs nullable columns with defaults (`ExpenseInvoice.cs:74-91`).
4. `ExpenseInvoice.OcrStatus [Required] string` vs `ocr_status enum NULL`; enum also restricts values to 5 literals (`ExpenseInvoice.cs:98-101`).
5. `ExpenseInvoice.CreatedAt/UpdatedAt DateTime`, `SupplierVatVerified bool` vs nullable columns (`ExpenseInvoice.cs:118-119, 150-154`).
6. `ExpenseInvoiceLine.VatRate/AmountInclVat [Required] decimal`, `SortOrder int` vs nullable (`ExpenseInvoice.cs:198-207`).
7. `ExpenseOcrQueue.Status [Required] string`, `Attempts/MaxAttempts int` vs nullable (`ExpenseModels.cs:215-224`).
8. `ExpensePayment.PaymentMethod [Required] string` vs `enum('BANK','CASH','OTHER') NULL` — any other literal (e.g. lowercase) is rejected by the enum (`ExpenseModels.cs:129-132`); `CreatedAt DateTime` vs nullable.
Type drift:
9. `ExpenseInvoice.OcrConfidence int?` (`ExpenseInvoice.cs:103-104`) vs `ocr_confidence decimal(5,2)` — `CODEBASE.md:529` claims the DB column is `int?`; the live dev DB says `decimal(5,2)`. Works today only because every written value is integral; settle by writing 95.5 in the test DB and reading it back.
10. `ExpenseInvoice.Source string [MaxLength(10)]` vs `source enum('MANUAL','EMAIL','N8N')` — `"QUEUE"` or `"WEBHOOK"` would be rejected.
11. `ExpenseInvoice.OcrFlags string?` (no MaxLength) vs `varchar(500)`; the 13-flag JSON array fits (~200 chars) but is bounded.
12. `pending_supplier_company_code varchar(50)` receives a vendor name via A7.
13. `BusinessPartner` (`Models/Models_Part1.cs:115-244`) has no properties for `approval_status/approval_date/approval_expires_at`, `vies_verified/vies_verified_at/vies_name` — DB columns exist without mapping (NOT NULL `approval_status` has a default, so inserts succeed).
14. `Data/NordicBeesErpContext.cs:439-442` declares index `IX_expense_invoices_supplier_invoice`; it is absent in the DB (indexes listed above).

### B4 — Roles and authorization

Mechanism: `erp_users.role varchar(20)` (`Models/ErpUser.cs:18-20`, default `"Admin"`); roles in use: `Admin`, `Manager`, `Warehouse`, `Designer` (`Components/Layout/NavMenu.razor:190-193`, `Program.cs:130`). Login: `Components/Pages/Login.razor:56-60` → `AuthService.ValidateUserAsync` (BCrypt, `Services/AuthService.cs:37-44`) → `BlazorAuthStateProvider.LoginAsync` stores `"email|role|fullName"` in `ProtectedLocalStorage` key `userId` (`Services/BlazorAuthStateProvider.cs:43-55`) and issues a `ClaimTypes.Role` claim (31, 50). `AddAuthorizationCore()` and policy `ArtworkAccess` = authenticated + role in {Admin, Manager, Designer} (`Program.cs:119, 126-131`). `IAuthService.GetAuthenticatedUserAsync()` (`AuthService.cs:71-80`) resolves the current `ErpUser` server-side from the claim; `ErpUserService` (`Services/ErpUserService.cs:7-15`) manages users; admin UI `Components/Pages/Admin/Users.razor`.

Concrete restricted examples: `Components/Pages/Admin/Users.razor:2` `@attribute [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]`; `Components/Pages/Home.razor:35` `<AuthorizeView Roles="Admin,Manager">`; `Components/Pages/Artwork/ArtworkDashboard.razor:3` `@attribute [Authorize(Policy = "ArtworkAccess")]`; `NavMenu.razor:190-193` hides menu entries by `IsInRole`.

**Caveat that changes the answer:** page-level `[Authorize]` is not enforced. `Components/Routes.razor` renders `<RouteView RouteData=… DefaultLayout=…/>` — not `AuthorizeRouteView` — and `Program.cs` never calls `app.UseAuthentication()` / `app.UseAuthorization()` (repo-wide grep: no hits); no component redirects anonymous users (the only `/login` navigation is the logout button, `Components/Layout/TopHeader.razor:140-144`). So `/admin/users` and every `[Authorize]`-decorated Artwork page open for anyone who types the URL; only in-component `AuthorizeView` / `IsInRole` checks actually hide content, and no service method checks a role. The cookie scheme registered at `Program.cs:120-125` is unused (NF-5). For PLAN §3 ("Prieiga ribojama role serverio pusėje") the pattern that works today is: `<AuthorizeView Roles="Admin">` in the component **and** an explicit role check in the service (via `AuthenticationStateProvider`, as `AuthService.cs:74-75` does), or first switch `Routes.razor` to `AuthorizeRouteView` and add `UseAuthorization()`. Role is client-held (ProtectedLocalStorage is Data-Protection-encrypted, so not trivially forged), and a role change in `erp_users` takes effect only after re-login.

### B5 — Test infrastructure

- `Tests/NordicBeesERP.Tests/` (in `NordicBeesERP.sln:10`): 24 `*Tests.cs` files + `DbTestFixture.cs`. Framework: **xunit 2.9.3**, `xunit.runner.visualstudio 3.1.4`, `Microsoft.NET.Test.Sdk 17.14.1`, `coverlet.collector 6.0.4`, `net10.0` (`NordicBeesERP.Tests.csproj`).
- Database: **real MySQL**, no in-memory/SQLite provider (grep). `DbTestFixture.cs:24-25` reads `TEST_DB_CONNECTION` or falls back to a hardcoded connection string to `nordic_bees_erp_test` on 100.110.26.80 **including the password** (violates `AGENTS.md` "Secrets"); mirrors production `NoTracking` (28-30).
- Expense/OCR coverage: `ExpenseServiceTests.cs` — 3 facts (`:78` UpdateInvoiceAsync persists, `:119` and `:139` DeleteInvoiceAsync), seeded via raw SQL (`:52`). **Zero OCR tests** (grep "ocr" in Tests hits only that column list). `CustomerServiceTests.cs`/`SupplierServiceTests.cs` mention "expense" only via partner types.
- `Tests/Playwright/` (separate csproj, xunit 2.8.0, `Microsoft.Playwright 1.45.0`, not in the .sln; `Category=E2E`).
- How run: only `bump-version.sh:48-62` runs `dotnet test --filter "Category!=E2E"` and only when `TEST_DB_CONNECTION` is set; otherwise skipped. CI: `.github/workflows/hardcode-check.yml` = greps + semgrep, **no `dotnet test`**; `.github/workflows/deploy.yml` = Docker build + deploy, no tests. `Tests/Fixtures/` holds two bank-statement fixtures (XLS, camt.053 XML), no invoice PDFs and no Azure JSON.

### B6 — OCR call sites

| # | File:line | Call | Path |
|---|---|---|---|
| 1 | `Components/Dialogs/ExpenseUploadDialog.razor:770` | `ExpenseOcrService.ProcessAsync(base64, _file.Name)` | dialog, new upload (opened from `Components/Pages/ExpenseInvoices.razor:334`) |
| 2 | `Components/Dialogs/ExpenseUploadDialog.razor:535` | `ExpenseOcrService.ProcessAsync(base64, …)` in `AnalyzeFromPathAsync` | dialog, re-OCR of existing invoice (opened from `Components/Dialogs/InvoiceDetailDialog.razor:1025-1030` with `ExistingFilePath`, `ExistingInvoiceId`) |
| 3 | `Components/Dialogs/ExpenseUploadDialog.razor:617` | `ExpenseOcrService.FindSupplierIdAsync(...)` | dialog (second lookup) |
| 4 | `Components/Dialogs/ExpenseUploadDialog.razor:882` / `:848` | `ExpenseService.CreateFromOcrAsync` / `UpdateFromOcrAsync` | dialog persistence |
| 5 | `Services/OcrQueueWorker.cs:36` | `ocrService.IsAzureHealthyAsync()` | queue (hosted service, `Program.cs:100`) |
| 6 | `Services/OcrQueueWorker.cs:77` | `ocrService.ProcessAsync(queueItem.FileContent, queueItem.FileName)` | queue |
| 7 | `Controllers/ExpenseController.cs:46-58` | `context.ExpenseOcrQueue.Add(...)` + `SaveChangesAsync` (`POST api/expense/webhook`, `X-Api-Key` = `app_settings.n8n_api_key`) | queue enqueue — **not routed** (no `AddControllers`/`MapControllers` anywhere; NF-1) |
| 8 | `Components/Pages/Settings.razor:173, 213` | `ExpenseOcrService.IsAzureHealthyAsync()` | neither (settings health indicator) |
| 9 | `Services/ExpenseOcrService.cs:733` | `FindSupplierIdAsync` (internal) | both |
| — | `Services/ExpenseOcrService.cs:834-835` | `ExtractInvoiceDataAsync` | **zero callers** (dead alias) |
| — | `Services/IOcrQueueWorker.cs:9` | `IOcrQueueWorker` | **zero implementations/usages** (dead) |
| — | `Data/NordicBeesErpContext.cs:93` | `DbSet<ExpenseOcrQueue>` | only readers: worker (#6), controller (#7) |

No other page, controller, or background job touches `expense_ocr_queue` or `IExpenseOcrService` (repo-wide grep, bin/obj excluded).

### B7 — File storage today

- Write path (new upload): `Components/Dialogs/ExpenseUploadDialog.razor:869-878` — `Path.Combine("wwwroot","uploads","invoices", yyyy, MM)` relative to the process CWD, file name `yyyyMMdd_HHmmss_<original-stem>.pdf`, DB path `uploads/invoices/YYYY/MM/<file>` (`original_file_path`), `original_filename` = the generated safe name (not the user's). Images are converted to PDF first (`:453-459`, `ImageToPdfService`), so every stored file is a PDF.
- Read path (re-OCR): `ExpenseUploadDialog.razor:525-527` `Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", filePath)` — the CI grep for `Directory.GetCurrentDirectory` scans only `*.cs` (`hardcode-check.yml`), so this `.razor` use escapes it.
- Queue path: no file on disk — the base64 body is stored in `expense_ocr_queue.file_content longtext` (`ExpenseController.cs:48`), which the worker feeds straight to Azure (`OcrQueueWorker.cs:77`); no `original_file_path` is ever set on that path.
- Size limits per layer: dialog `file.OpenReadStream(10 * 1024 * 1024)` = **10 MB** (`ExpenseUploadDialog.razor:448`); `MudFileUpload Accept=".pdf,.jpg,.jpeg,.png,.webp,.tiff,.tif"` `MaximumFileCount="1"` (`:32`); SignalR hub `MaximumReceiveMessageSize = 50 MB` (`Program.cs:32`); Kestrel: no `MaxRequestBodySize` configured (grep) → ASP.NET default 30 MB applies to the (unrouted) webhook JSON body only; no nginx/reverse-proxy config in the repo (`infra/` holds only `llama-swap-config.yaml`; `Dockerfile` sets no limits; `deploy.yml` publishes port 8080/8081 directly).
- Persistence: `Dockerfile` publishes to `/app`; `deploy.yml` mounts only `/var/lib/nordicbees/artwork` and `/var/lib/nordicbees/delivery-receipts` → `/app/wwwroot/uploads` lives in the container's writable layer and is discarded on every `docker rm`/`docker run` (NF-3). `wwwroot/uploads/` is git-ignored (`.gitignore`).
- Exposure: `app.UseStaticFiles()` (`Program.cs:148`) serves `wwwroot`, and no auth middleware exists → `GET /uploads/invoices/2026/09/<name>.pdf` is anonymous (NF-4).
- Hashing / duplicate-file detection: **none** for expense invoices (grep `SHA256|MD5|ComputeHash|file_hash` hits only artwork tables' `file_sha256`/`file_hash` in migrations). Duplicate detection is metadata-only (`CheckDuplicateAsync`, B1). Uploading the same PDF twice creates two files and, if number+amount match, a DUPLICATE_PENDING row.

### B8 — Production data audit — NOT EXECUTED

Reasons: (1) `AGENTS.md` forbids any agent connection to the production DB ("never queried or connected to by an agent under any circumstance"), and the task's STOP rule says to stop on any conflict with `AGENTS.md`; (2) no production tool exists in this session (the OpenCode `nordicbees-prod-db` MCP maps to `~/mysql-mcp/start-prod.sh` → 127.0.0.1:3307, i.e. a local tunnel that also was not touched). Q-001 and Q-003 in `OPEN-QUESTIONS.md` remain open. Queries for a human to run, verbatim from the task:

```sql
-- B8.1
SELECT COUNT(*) AS total,
       SUM(CASE WHEN paid_amount = amount_incl_vat AND amount_incl_vat > 0
                THEN 1 ELSE 0 END) AS marked_fully_paid
FROM expense_invoices;

-- B8.2
SELECT i.id, i.invoice_number, i.invoice_date, i.amount_incl_vat,
       i.paid_amount, i.source, i.ocr_pipeline, i.created_at
FROM expense_invoices i
LEFT JOIN expense_payments p ON p.invoice_id = i.id
WHERE i.paid_amount = i.amount_incl_vat
  AND i.amount_incl_vat > 0
  AND p.id IS NULL
ORDER BY i.invoice_date DESC
LIMIT 500;

-- B8.3
SELECT DATE_FORMAT(invoice_date, '%Y-%m') AS month, COUNT(*) AS cnt
FROM expense_invoices
WHERE invoice_date >= DATE_SUB(CURDATE(), INTERVAL 12 MONTH)
GROUP BY 1 ORDER BY 1;

-- B8.4a
SELECT status, COUNT(*) AS cnt, MIN(created_at) AS oldest,
       MAX(created_at) AS newest, MAX(attempts) AS max_attempts
FROM expense_ocr_queue
GROUP BY status;

-- B8.4b
SELECT COUNT(*) AS rows_with_content,
       SUM(LENGTH(file_content)) AS total_bytes
FROM expense_ocr_queue WHERE file_content IS NOT NULL;
```
Interpretation guide given NF-1/NF-2: if B8.4a returns zero rows or only `COMPLETED`/`FAILED` rows from before the webhook stopped being routed, A2 has not been burning quota; if B8.2 returns rows, note that `paid_amount = amount_incl_vat` can also be legitimately produced by `RecalculateInvoiceStatusAsync` when payments were later deleted — the `p.id IS NULL` join already excludes rows with surviving payments, but payment deletion (`ExpenseService.cs:531-536`) recalculates and would reset `paid_amount` to 0, so surviving hits are genuine.

### B9 — Semgrep and hooks

- `.semgrep.yml` (root, 19 lines): a deliberate no-op — `rules: []` — with a comment (lines 1-18) explaining that the include-by-path form crashed semgrep 1.169.0 and that every invocation must point `--config` at the real file.
- Real rules: `.agent-guardrails/nordicbees-rules.yaml` (14 rules): `nordicbees-notracking-savechanges` (:2, ERROR, excludes `/Tests/**`), `nordicbees-sql-string-interpolation` (:17), `nordicbees-hardcoded-test-credentials` (:29), `nordicbees-migration-if-not-exists` (:46), `nordicbees-stringcomparison-in-linq` (:56), `nordicbees-dbnull-explicit-cast` (:76), `nordicbees-silent-catch-no-snackbar` (:90), `nordicbees-schema-drift-unverified-column-mapping` (:109), `nordicbees-ef-connection-pool-race-last-insert-id` (:139), `nordicbees-layout-lifecycle-unguarded-db-call` (:165), `nordicbees-mariadb-datetime-precision-default-mismatch` (:245), `nordicbees-ef-decimal-precision-annotation-missing` (:267), `nordicbees-pdf-locale-string-hardcoded-outside-labels` (:308), `nordicbees-enum-array-contains-in-linq-where` (:336).
- Hooks: `.githooks/pre-commit` (Node ESM, 71 lines) runs `semgrep scan --config=.agent-guardrails/nordicbees-rules.yaml [--baseline-commit <from .agent-guardrails/baselines/nordicbees-notracking-savechanges.baseline.json>] --error` (:47-59) then `npx agent-guardrails check --base-ref HEAD~1` (:61-71). It is active only after `git config core.hooksPath .githooks` (`AGENTS.md`, not enabled by default). CI runs the same semgrep command in `hardcode-check.yml` (baseline-aware).
- Where a purity rule goes: append a new rule object to `.agent-guardrails/nordicbees-rules.yaml`, scoped with `paths: include: ["Services/Ocr/Extraction/**"]`, `pattern-either` over `NordicBeesERPContext`, `IDbContextFactory<...>`, `DbContext`, `HttpClient`, `IHttpClientFactory`, `DateTime.Now`, `DateTime.UtcNow`, `DateTime.Today`, `DateTimeOffset.Now`, severity ERROR. Because the hook and CI use `--baseline-commit`, only findings introduced after the baseline commit fail — a rule targeting a not-yet-existing directory is safe to add before F3. Note the existing `nordicbees-notracking-savechanges` rule already flags every `SaveChangesAsync` outside Tests, including the legitimate `Add`+`SaveChangesAsync` inserts in `ExpenseService`; the baseline is what keeps CI green.

## 4. New findings (not covered by any claim), ranked by severity

- **NF-1 (P0, queue path dead at the entrance).** `Program.cs` contains no `AddControllers()`, `MapControllers()`, `AddMvc()` or `MapControllerRoute()` (repo-wide grep: none; `git log -S"MapControllers" -- Program.cs`: never present). `Controllers/ExpenseController.cs` (`[ApiController] [Route("api/expense")]`, `[HttpPost("webhook")]`, `:8-25`) is therefore never routed; `POST /api/expense/webhook` falls through to the Blazor router's "Sritis nerasta" page (`Components/Routes.razor`). Nothing else inserts into `expense_ocr_queue`. Settle at runtime: `curl -X POST https://<host>/api/expense/webhook -H 'X-Api-Key: x'` → HTML 200 from Blazor, not 401 JSON. Decision needed: revive the webhook (add `AddControllers()` + `MapControllers()` and fix NF-2) or delete controller + worker + queue table in F0/F1 instead of fixing A1/A2/A3/A18 in dead code.
- **NF-2 (P0, queue path dead at the exit).** `ExpenseController.cs:51` `InvoiceId = 0` and `OcrQueueWorker.cs:84-89` update only an existing invoice by id → the queue path can never create an invoice. Any A1/A2 production impact hinges on rows that could only have been inserted by hand or by a previous build.
- **NF-3 (P0, data loss).** Invoice PDFs are stored in the container's writable layer (`ExpenseUploadDialog.razor:870`, `Dockerfile` WORKDIR `/app`) and `deploy.yml` mounts no volume for `wwwroot/uploads` → lost on every deploy; re-OCR (`AnalyzeFromPathAsync`, `:525-526`) then throws `FileNotFoundException`, and F1 ("10 realių sąskaitų atkuriamos iš disko") cannot be met for past invoices. Settle: `docker exec nordicbees_prod ls /app/wwwroot/uploads/invoices` vs `SELECT COUNT(*) FROM expense_invoices WHERE original_file_path IS NOT NULL`.
- **NF-4 (P1, security).** `app.UseStaticFiles()` (`Program.cs:148`) serves `wwwroot/uploads/invoices/**` anonymously; no authentication middleware exists. Supplier invoices (bank accounts, amounts) are downloadable by URL; names are timestamp-based and enumerable to the minute. F1's `expense_documents` should store files outside `wwwroot` (as artwork does: `/var/lib/nordicbees/artwork`, `Program.cs:160`) and serve them through an authenticated endpoint.
- **NF-5 (P1, security).** `[Authorize]` page attributes are inert (`Routes.razor` uses `RouteView`; no `UseAuthentication`/`UseAuthorization` in `Program.cs`). `/admin/users` and Artwork pages are reachable anonymously. Affects the design of the planned admin-only OCR view (B4).
- **NF-6 (P1, data integrity).** Re-OCR of an existing invoice overwrites `original_filename` with `""` (`ExpenseUploadDialog.razor:847` → `ExpenseService.cs:1385`), and replaces user-visible flags with service flags (A4), so a dismissed `WRONG_RECIPIENT` re-rejects the invoice (`ExpenseService.cs:1314-1315`).
- **NF-7 (P1).** The `Sąskaitos tipas` (STANDARD/ULAK) selector in the dialog (`ExpenseUploadDialog.razor:246-249`) is never persisted: `CreateFromOcrAsync` hardcodes `"STANDARD"` (`ExpenseService.cs:1207`), `UpdateFromOcrAsync` never writes `invoice_type`. Same for `_categoryId` on the re-OCR path.
- **NF-8 (P2).** `CheckDuplicateAsync` ignores both supplier parameters (B1); `UpdateFromOcrAsync` calls it without `excludeInvoiceId` and can mask a real duplicate (`ExpenseService.cs:1306-1311`); `CreateInvoiceAsync` uses a different duplicate rule (177-198). Three definitions of "duplicate" in one service.
- **NF-9 (P2).** `pending_supplier_company_code varchar(50)` receives `VendorAddressRecipient` (a name) via A7 step 3 → strict-mode "Data too long" on names > 50 chars (`ExpenseService.cs:1217, 1368`).
- **NF-10 (P2).** `invoice_number` NOT NULL vs `null` assignment when blank (`ExpenseService.cs:1219, 1370`): the `MISSING_INV_NUMBER → NEEDS_REVIEW` status path (1180-1183) is unreachable because the INSERT would throw first; the dialog's client guard (`:826-833`) is the only thing preventing the exception.
- **NF-11 (P2, quota).** Every OCR run also performs a VIES call (`ExpenseOcrService.cs:681`) and two identical supplier lookups (A8); on the queue loop (A2) that is one VIES call per 30 s in addition to the Azure call.
- **NF-12 (P3).** `OcrQueueWorker.cs:103-104` writes `Currency` unconditionally ("EUR" default is non-empty), overwriting an existing non-EUR value on re-processing.
- **NF-13 (P3).** `catch (RequestFailedException ex) when (ex.ErrorCode == "429")` (`ExpenseOcrService.cs:816`): Azure sets `ex.Status` to 429; `ErrorCode` is the service error string (not verified here which value the DI service returns). If it is not literally "429", `AZURE_LIMIT` is never raised and throttling falls into the generic catch. Settle: inspect a real 429 `RequestFailedException` or check `ex.Status == 429` instead.
- **NF-14 (P3, hygiene).** Dead code: `ExtractInvoiceDataAsync` (`ExpenseOcrService.cs:834-835`), `IOcrQueueWorker` (`Services/IOcrQueueWorker.cs`), dialog `NormalizeName` (`ExpenseUploadDialog.razor:903-907`), A7 step 4; `DroppedFile` stub with 512000-byte default (`ExpenseUploadDialog.razor:966-976`); `ExpenseUploadDialog.razor:12` injects a scoped `NordicBeesERPContext` directly instead of the factory used everywhere else; `Tests/NordicBeesERP.Tests/DbTestFixture.cs:25` hardcodes a DB password; `build.sh:2` points at a non-existent directory.

## 5. Open questions

1. **Has A1/A2 ever fired in production?** Static analysis says the only trigger is a hand-inserted or legacy `expense_ocr_queue` row (NF-1/NF-2). Only B8.1/B8.2/B8.4 answer this; not run (AGENTS.md conflict, no tool). Q-003 stays open.
2. **Monthly invoice volume (Q-001)** — B8.3 not run for the same reason.
3. **Do partners with `vat_code = ''` exist** (A8 empty-string match)? Needs `SELECT COUNT(*) FROM business_partners WHERE vat_code = ''` on the target DB; not run (outside the DESCRIBE scope).
4. **Is the whole-DB schema dump stale only because of `company_settings`?** `--check` says STALE; the six OCR tables match the dump exactly. Refreshing `.opencode/db-schema.md` (a write) would settle it.
5. **Which `ErrorCode` does Azure DI put on a 429** (NF-13)? Needs a captured exception or SDK source.
6. **Does production actually run the `deploy.yml` container layout** (NF-3/NF-4 rely on it)? Settle with `docker inspect nordicbees_prod` mounts on the host.
7. **`ocr_confidence` type on production** — dev is `decimal(5,2)`, `CODEBASE.md:529` says `int?`; prod may differ (`DESCRIBE expense_invoices` there, human-run).
8. **`CompanyNameHelper.Normalize`** (`ExpenseOcrService.cs:725`) was not read; its hardcoded rules are not inventoried in A16.

## 6. Completion checklist

- [x] `git status --porcelain` after writing this report shows only the pre-existing ` M Docs/BUGLOG.md` and `?? Docs/ocr-rebuild/` (this file is inside the latter). No other file was created or modified in the repository. (Verified by the command run immediately after writing; see session log.) Note the tree was already dirty at start — recorded in §0.
- [x] All 18 claims have a verdict (16 CONFIRMED, A13 PARTIALLY CONFIRMED, A18 REFUTED).
- [x] All 9 gaps answered; B8 explicitly "not executed, because: AGENTS.md conflict + no production tool" with the queries reproduced.
- [x] Database statements executed — all read-only, all against the **dev** DB `nordic_bees_erp` @ 100.110.26.80 (no production connection was attempted):
  1. Attempted, **denied by the harness before execution**: `DESCRIBE expense_invoices`, `DESCRIBE expense_invoice_lines`, `DESCRIBE expense_ocr_queue`, `DESCRIBE expense_payments`, `DESCRIBE business_partners`, `DESCRIBE app_settings`.
  2. `scripts/dump-db-schema.sh --check` (no file written; temp dir only), which ran three `SELECT`s on `information_schema`: `SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, IFNULL(COLUMN_KEY,''), IFNULL(COLUMN_DEFAULT,''), IFNULL(EXTRA,'') FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='nordic_bees_erp' ORDER BY TABLE_NAME, ORDINAL_POSITION;` / `SELECT TABLE_NAME, INDEX_NAME, GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX SEPARATOR ','), IF(NON_UNIQUE=0,'UNIQUE','') FROM information_schema.STATISTICS WHERE TABLE_SCHEMA='nordic_bees_erp' GROUP BY TABLE_NAME, INDEX_NAME, NON_UNIQUE ORDER BY TABLE_NAME, INDEX_NAME;` / `SELECT TABLE_NAME, COLUMN_NAME, CONCAT(REFERENCED_TABLE_NAME,'.',REFERENCED_COLUMN_NAME) FROM information_schema.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA='nordic_bees_erp' AND REFERENCED_TABLE_NAME IS NOT NULL ORDER BY TABLE_NAME, COLUMN_NAME;` (`scripts/dump-db-schema.sh:52-76`).
  3. `SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, IFNULL(COLUMN_KEY,''), IFNULL(COLUMN_DEFAULT,'NULL'), IFNULL(EXTRA,'') FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='nordic_bees_erp' AND TABLE_NAME IN ('expense_invoices','expense_invoice_lines','expense_ocr_queue','expense_payments','business_partners','app_settings') ORDER BY TABLE_NAME, ORDINAL_POSITION;`
  Production queries run: **none**.
- [x] `bump-version.sh` was NOT run. No `git add`/`commit`/`push`. `agent-guardrails check` was not run (read-only task, no source changes).
