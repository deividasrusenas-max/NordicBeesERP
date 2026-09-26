# Etapas 1 — plan (for review before any code)

Written 2026-09-26 by Claude Code for the owner and the planning advisor. **Nothing here is
implemented.** Base: `analysis/RESEARCH-2026-09-25-reliability.md` §3 and §10 (Etapas 1, items
5–8), D-022, D-028, D-031, D-032, D-037, the four validators in `Services/Validation/` (in
production since v0.17.91, called from nowhere), and the code at `main` `6fb92a5`.
Every `file:line` was read in this session. Where a claim could not be checked, it says so.

Deploy context (D-037): nothing from this plan goes to production until Etapai 1–4 are done
and verified on staging. Etapas 1 is closed on **staging**, not in production.

## Contents

0. How the OCR pipeline decides today (the facts the plan builds on)
1. Wiring the three validators as gates
2. VAT-rate whitelist by country and date
3. Locale number disambiguation via arithmetic
4. Re-OCR for stored files (via `file_id`)
5. Open items from `STATE.md`: in Etapas 1 or not
6. Proposed split into sessions and commits
7. Staging verification for Etapas 1
8. Risks across the whole stage
9. Open questions for the owner

---

## 0. How the pipeline decides today

**Three write paths set `ocr_flags` and `status`.** Every gate must behave the same on all
three, or an invoice can dodge a gate by taking a different path.

| Path | Entry | Flag computation | Status |
|---|---|---|---|
| OCR create | `ExpenseUploadDialog.SaveAsync` → `ExpenseService.CreateFromOcrAsync` (`ExpenseService.cs:1655`) | flags from `ExpenseOcrService.ProcessAsync` (`ExpenseOcrService.cs:759-827`), then amount and date flags recomputed from the final values (`ExpenseService.cs:1670-1671`) | `DecideOcrStatus` (`:1596`), duplicate → `DUPLICATE_PENDING` |
| Re-OCR | `ExpenseUploadDialog.SaveAsync` (`ExpenseUploadDialog.razor:964-975`) → `UpdateFromOcrAsync` (`ExpenseService.cs:1773`) | same as create (`:1817-1819`) | same, quarantine kept (`:1797-1832`) |
| Manual edit | `InvoiceDetailDialog.SaveAsync` → `SaveInvoiceEditAsync` (`:310`); `InvoiceEdit.razor` → `UpdateInvoiceAsync` (`:252`) | `ComputeManualEditFlags` (`:498-529`): **rebuilds the list from scratch** and carries over only 5 stored flags | `StatusAfterManualEdit` (`:536`) |

Supplier assignment (`AssignSupplierAsync` `:1331`, `AutoAssignSupplierAsync` `:1370`) uses the
**stored** flags (`StatusAfterSupplierAssigned` `:1605`).

The shared status rules live in one region, `ExpenseService.cs:1582-1642`: `HasReviewFlag`
(`:1588`), `DecideOcrStatus` (`:1596`), `StatusAfterSupplierAssigned` (`:1605`),
`RecomputeAmountConsistencyFlags` (`:1612`), `RecomputeDateFlags` (`:1627`).

**Why the service must recompute, not trust `ProcessAsync`.** The upload dialog lets the user
change amounts, VAT rate, dates, supplier and the pending VAT code before saving
(`SyncOcrResultFromUi`, `ExpenseUploadDialog.razor:1011-1038`), and copies the OCR flag list
back unchanged. So any gate that depends on an editable field has to be recomputed in the
service from the final values — the pattern already used for the arithmetic and date gates.

**Two facts that shape the whole stage:**

1. **The manual-edit path drops flags it does not know.** `ComputeManualEditFlags` carries over
   only `WRONG_RECIPIENT`, `VIES_UNAVAILABLE`, `VENDOR_NOT_FOUND` (without a supplier),
   `DUPLICATE` and `MISSING_DUE_DATE` (`ExpenseService.cs:514-523`). `OWN_COMPANY` and
   `INVALID_VAT_RATE` are silently lost on every edit-form save — the cause of the 370 chip
   finding (`STATE.md`). Every new flag in this plan would be lost the same way unless this is
   fixed first (§1.4, §5).
2. **`OcrQueueWorker` bypasses every gate** (`OcrQueueWorker.cs:84-137`): for a row with an
   existing invoice it writes amounts and the supplier straight onto the invoice with no flags
   and no status decision, and sets `PaidAmount = AmountInclVat` (`:110`). The only producer is
   the n8n webhook `POST api/expense/webhook` (`Controllers/ExpenseController.cs:24-58`, API key
   from `app_settings.n8n_api_key`), which always writes `InvoiceId = 0` (`:51`) — so the
   invoice lookup (`OcrQueueWorker.cs:84-87`) never matches and no invoice is written. By
   reading (not executed): the worker's status/`Attempts` changes are made on an untracked
   entity without `Update` (`:73-74`, `:81`, `:148-157`), so they are never saved (0c report §2) — a
   webhook row would stay `WAITING` and be sent to Azure again on every 30 s cycle (`:39`,
   `:50`). The dev DB queue is empty (read-only query, 2026-09-26); `n8n_api_key` is set on
   dev; production is unknown to me. The file is frozen (FROZEN §5). The plan does not touch
   it; §5 records it as an owner decision.

---

## 1. Wiring the three validators as gates

### 1.1 What exists

| Validator | API | Notes from its reviews |
|---|---|---|
| `IbanValidator.Validate(string?)` | reasons `Empty`, `BadCharacters`, `UnknownCountryLength` (warning), `WrongLength`, `ChecksumFailed` (`IbanValidator.cs:8-15`) | never throws (fuzzed) |
| `VatCodeFormatValidator.Validate(raw, countryHint)` | `Empty`, `UnknownCountry`, `WrongFormat`; table LT {9,12}, LV 11, EE 9, DE 9, PL 10, RO 2–10 (`VatCodeFormatValidator.cs:48-56`) | open notes: en-dash not stripped; separator-only → UnknownCountry; hint not checked to be 2 letters; a prefix that disagrees with the hint passes |
| `En16931TotalsValidator.Validate(En16931TotalsInput)` | BR-CO-10/13/15/16, literal Schematron port; every rule lands in exactly one of `PassedRules` / `Violations` / `NotApplicable` / `OutOfRange` (`En16931TotalsValidator.cs:64-69`) | never throws; exact comparison after XPath rounding, **no flat tolerance** |
| `LineAmountPlausibilityRule.Check(lines)` | project rule `PROJ-LINE-QTY-PRICE`, tolerance `max(0.01, 0.5 % × |line net|)` (`LineAmountPlausibilityRule.cs:31-50`) | **can still throw `OverflowException`** at `quantity * unitPrice` (`:48`) |

### 1.2 Where each is called (proposal)

One new private helper in the STATUS RULES region of `ExpenseService.cs`, next to
`RecomputeAmountConsistencyFlags` — working name `RecomputeValidationFlags(flags, input)` — that
drops the flags it owns and recomputes them. It is called at the three places that already
call `RecomputeAmountConsistencyFlags`: `CreateFromOcrAsync` (`:1670`), `UpdateFromOcrAsync`
(`:1817`), `ComputeManualEditFlags` (`:509`). This keeps "one source for the review list"
(the stated intent of `:1582-1584`).

| Validator | Called where | Input on the OCR paths | Input on the manual-edit path |
|---|---|---|---|
| EN 16931 totals | the helper, at all three places | header `AmountExclVat`, `VatAmount`, `AmountInclVat`; line `AmountExclVat` | same, from the values being saved (`finalLines`) |
| Line rule | the helper | line `Quantity`, `UnitPrice`, `AmountExclVat` | same |
| VAT format | **(a)** in `ProcessAsync` before the VIES call (`ExpenseOcrService.cs:676-680`) and before `FindSupplierIdAsync` (`:732`), to skip VIES and VAT matching for a malformed code; **(b)** again in the helper from the final `SupplierVatCode` (editable in the dialog) | `SupplierVatCode`, hint `SupplierCountryCode` (set from the address at `:178` or from the VAT prefix at `:719-720`) | `PendingSupplierVat` + `PendingSupplierCountryCode` when no supplier; **carried over** when a supplier is set (the document's code is not stored anywhere else except `ocr_raw_json`) |
| IBAN | the helper (OCR paths) | `SupplierBankAccount` (read at `ExpenseOcrService.cs:260-273`; not editable in the upload dialog) | `PendingSupplierBankAccount` when no supplier; carried over otherwise (same reason) |

`ViesService.cs` (FROZEN §6) is **not** modified — only whether it is called changes.

### 1.3 New flags, review vs information, Lithuanian labels

Labels and colours go into `ExpenseStatusHelper.GetFlagLabel` / `GetFlagColor`
(`Helpers/ExpenseStatusHelper.cs:58-100`); both dialogs render chips through them
(`InvoiceDetailDialog.razor:46-51`, `ExpenseUploadDialog.razor:181-184`), so no frozen markup
changes. Review flags are added to `HasReviewFlag` (`ExpenseService.cs:1588`) and to
`IsCriticalFlag` (`ExpenseStatusHelper.cs:118-121`).

| Flag | When | Kind | Label (proposal) |
|---|---|---|---|
| existing `AMOUNT_ARITHMETIC_MISMATCH` | BR-CO-15 violation (replaces the flat 0.02 check in `AddAmountConsistencyFlags`, `ExpenseOcrService.cs:860-871`) | review (already) | keep „Sumos nesutampa (be PVM + PVM ≠ su PVM)" |
| existing `AMOUNT_MISMATCH` | BR-CO-10 violation (replaces the ad-hoc 0.05 check at `ExpenseOcrService.cs:796-810` and the 0.01 check at `ExpenseService.cs:506-507`) | review (already) | keep „Sumos nesutampa" |
| existing `MISSING_MONEY_FIELD` | unchanged meaning | review (already) | keep |
| new `TOTALS_OUT_OF_RANGE` | any rule in `OutOfRange` (amounts beyond decimal range — nonsense input) | review | „Sumos neįtikėtinai didelės" |
| new `LINE_AMOUNT_IMPLAUSIBLE` | line rule violation | **information** in Etapas 1 (see Q3) | „Eilutė: kiekis × kaina ≠ suma" |
| new `INVALID_IBAN` | `WrongLength`, `BadCharacters`, `ChecksumFailed` | review | „Neteisingas IBAN" |
| (none) | `UnknownCountryLength` | information only, no flag (mod-97 still passed) | — |
| new `INVALID_VAT_FORMAT` | `WrongFormat` | review | „Neteisingas PVM kodo formatas" |
| new `VAT_FORMAT_UNCHECKED` | `UnknownCountry` (e.g. GB, UA, CH suppliers) | information | „PVM kodo formatas netikrintas" |
| (none) | `Empty` | no flag — suppliers without a VAT code are normal (physical persons, non-VAT payers) | — |

**Rule messages.** `ocr_flags` stores codes only. The validator messages („BR-CO-15 pažeista:
suma su PVM … ≠ …") are deterministic, so the detail dialog can recompute them from the stored
invoice and lines at display time — no schema change. The `CREATED` / `EDITED` /
`OCR_RETRIED` audit rows already list the flags.

### 1.4 Not-applicable, out-of-range and "0 means missing" at the call site

- **NotApplicable is never "passed".** A rule that could not run adds no review flag, but it
  must not be reported as checked. Proposal: the detail dialog lists "nepatikrinta: BR-CO-10
  (nėra eilučių)" from the same recomputation; no extra flag, because `LINES_NOT_FOUND` and
  `MISSING_MONEY_FIELD` already make the cause visible.
- **The "0 = missing" problem.** `OcrResultDto` and `ExpenseInvoice` store the amounts as
  non-nullable `decimal`; `ProcessAsync` leaves 0 when Azure returned nothing
  (`ExpenseOcrService.cs:373-402`). The validator distinguishes null (not extracted) from 0.
  The existing convention is `MISSING_MONEY_FIELD` when net or gross ≤ 0
  (`ExpenseOcrService.cs:862`). Proposal: map net/gross ≤ 0 → null (NotApplicable; the
  existing flag already stops the invoice); VAT 0 → **present 0** (a legitimate 0 % invoice
  must still satisfy net = gross under BR-CO-15). This mapping must be written down and
  tested; it cannot be derived from the validator.
- **BT-106 vs BT-109.** The invoice has one net figure (`AmountExclVat`, Azure `SubTotal`). The
  plan feeds it as both BT-106 and BT-109 and passes no allowance/charge (BT-107/108), so
  BR-CO-13 compares BT-109 with BT-106 → always passes, and BR-CO-16 has no BT-115 → always
  NotApplicable. **In practice Etapas 1 gains BR-CO-10 and BR-CO-15.** Extracting Azure
  `AmountDue` / `TotalDiscount` to make BR-CO-13/16 real is possible but not proposed (no
  column to store them on the edit path; Etapas 3 changes extraction anyway). Stated so the
  owner is not told "four rules are live" when two are.
- **Derived line nets.** When Azure gives no line amount, `ProcessAsync` computes it as
  `UnitPrice × Quantity` (`ExpenseOcrService.cs:564-566`). BR-CO-10 on such lines checks a
  number the code made up. Proposal: mark such lines as derived in the DTO and exclude them
  from the line rule (it would pass trivially).
- **Overflow.** `En16931TotalsValidator` already maps overflow to `OutOfRange`. The line rule
  does not (`LineAmountPlausibilityRule.cs:48`). Fix it **inside the rule** (same pattern,
  tested), not with a try/catch at the call site.

### 1.5 Behaviour change the owner must accept

Replacing the flat tolerances with the Schematron comparison changes who is flagged:

- header: today `|net + VAT − gross| > 0.02` (`ExpenseOcrService.cs:869`); BR-CO-15 flags any
  difference after rounding, i.e. **0.01 now flags**;
- lines: today 0.05 on both net and gross on the OCR path (`:809`), 0.01 on net on the edit
  path (`ExpenseService.cs:506`); BR-CO-10 flags any difference after rounding the line sum.

How many invoices this moves is unknown. It can be measured on staging before wiring (read-only
query, owner runs it — §7).

### 1.6 Files, frozen conflicts, tests, estimate

- **Files:** `Services/ExpenseService.cs`, `Services/ExpenseOcrService.cs`,
  `Services/Dtos/OcrResultDto.cs` (flag constants; "derived" marker on lines),
  `Helpers/ExpenseStatusHelper.cs`, `Services/Validation/LineAmountPlausibilityRule.cs`,
  `Services/Validation/VatCodeFormatValidator.cs` (the four open notes),
  `Components/Dialogs/InvoiceDetailDialog.razor` (rule messages; not frozen).
- **Frozen:** none if `ViesService.cs` and `OcrQueueWorker.cs` stay untouched.
- **Tests:** existing `ExpenseOcrServiceAmountConsistencyTests`, `ExpenseStatusGateTests`,
  `ExpenseEditSaveTests`, `ExpenseManualEditTests`, `ExpenseReOcrTests` will need updated
  expectations where the tolerance changes; new tests per flag on all three paths (create,
  re-OCR, edit) — the path-parity test is the one that matters. DB-backed tests need the
  Tailscale link to the dev test DB (the prepush session had 180 failures when it was down).
- **Estimate:** ~10–14 h (RESEARCH gave 8–12 h for EN 16931 alone including writing the
  validator, which is done; the wiring across three paths, the mapping rules and the test
  updates are the bulk).

---

## 2. VAT-rate whitelist by country and date

### 2.1 What exists

- Rates are parsed from line `TaxRate` strings (`ParseVatRate`, `ExpenseOcrService.cs:843-858`,
  rejects < 0 and > 100 → `INVALID_VAT_RATE`, not a review flag), otherwise derived as
  `round(VAT / net × 100, 0)` for the header (`:456-458`) and per line (`:542-552`).
- The invoice header has one `VatRate`; lines have their own. There is no table of legal rates
  anywhere (`default_vat_rate` on company settings and partners is a default for *our* sales,
  `Models/CompanySettings.cs:88-90`, `Models/Models_Part1.cs:206-208`).

### 2.2 Proposal

- **Data source:** a static table in a new pure file `Services/Validation/VatRateTable.cs`:
  `(Country, ValidFrom, ValidTo, Rates[])`, each row with a source comment. No DB table, no
  migration — changes are rare, reviewed in git, and the table is unit-testable. Values to
  start from (RESEARCH §3, which cites secondary sources only — **each must be confirmed
  against the national tax authority before merging**): LT 21 / 9 / 5; DE 19 / 7;
  RO 21 / 11 from 2025-08-01 (19 / 9 / 5 before); LV 21 / 12 / 5; EE 24 / 13 / 9 (the EE
  change date must be looked up — RESEARCH gives only the 2026 values); PL (in the VAT-format
  table but not in RESEARCH) — rates to be sourced.
- **Which country:** the supplier's country — `SupplierCountryCode` on the OCR paths, the
  partner's `CountryCode` once a supplier is set (`BusinessPartner.CountryCode`, default "LT",
  `Models/Models_Part1.cs:173-174`). **Risk:** a foreign partner created without a country is
  stored as LT and would be checked against LT rates.
- **Which date:** the invoice date (the supply date is not extracted).
- **Which rates are checked:** each line's rate when lines carry a parsed rate; the header rate
  only when no line has one. 0 % is never flagged here — it is `ZERO_VAT`'s job (D-026).
  Derived rates (rounded to whole percent) are checked too; a mixed-rate invoice without
  line rates derives a blend (e.g. 17 %) and is flagged — correct, it needs a look.
- **Flags:** new `VAT_RATE_NOT_ALLOWED` (review) „PVM tarifas negalimas šaliai ir datai";
  unknown country or a date outside the table → new `VAT_RATE_UNCHECKED` (information)
  „PVM tarifas netikrintas". Non-EU suppliers normally charge no LT-relevant VAT; flagging
  them for review would be noise.
- **Maintenance:** a `LastVerified` date in the file and a line in `STATE.md`'s periodic list;
  review on every known rate change and at least yearly. A time-dependent failing test is
  deliberately **not** proposed (it would break CI on a calendar date).

### 2.3 Files, frozen, tests, staging, estimate

- New `Services/Validation/VatRateTable.cs` + tests; wiring in the §1.2 helper;
  `OcrResultDto.cs` / `ExpenseStatusHelper.cs` for the two flags. No frozen file.
- Tests: table lookups at boundary dates (RO 2025-07-31 vs 2025-08-01), unknown country,
  0 % not flagged, per-line vs header, all three paths.
- Staging: which supplier countries actually occur (owner query, §7) — this decides how much
  of the table matters.
- Estimate: ~4–6 h, plus the owner's time to confirm rates.

---

## 3. Locale number disambiguation via arithmetic

### 3.1 Where parsing happens today — it is Azure, not our code

`ProcessAsync` never parses number strings for amounts. It reads Azure's typed values:
header `valueCurrency.amount` (`ExpenseOcrService.cs:373-402`), line `Quantity.valueNumber`
(`:490-492`), `UnitPrice` and `Amount` `valueCurrency.amount` (`:499-518`), all via
`GetDouble()` cast to `decimal`. Azure is called with `locale: "lt-LT"` (`:87`). The only
string parsing is the VAT rate (`ParseVatRate`, `:843`).

The one real raw response we have (`.opencode/reports/raw-asf0021438.json`, git-ignored,
ASF0021438, `prebuilt-invoice` API 2024-11-30) shows the failure is Azure's:

| Field | printed `content` | Azure value | correct |
|---|---|---|---|
| line 1 `Quantity` | „3 888,000" | **3** | 3888 |
| line 2 `Quantity` | „9,000" | **9000** | 9 |
| line 1 `Amount` | „972,00" | 972.00 | 803,31 (972,00 is the „Suma su PVM" column — D-023) |
| `SubTotal` / `TotalTax` / `InvoiceTotal` | „934,22" / „196,18" / „1 130,40" | same | correct |

So both directions of the ambiguity occur on one invoice, **and** the line amount is from the
wrong column. The printed `content` string is in scope where each value is read (the field
element is at hand), and the full response is stored in `ocr_raw_json`
(`ExpenseService.cs:1719`, `:1891`).

**A related silent deletion (found by reading, not executed).** On this invoice the lines sum
to 972,00 + 158,40 = 1 130,40 > header net 934,22, so the reconcile step removes every line
with quantity > 1000 as "likely weight" (`ExpenseOcrService.cs:629-640`) — i.e. the real
„9,000" line that Azure read as 9000. The invoice is still flagged (`AMOUNT_MISMATCH` on the
remaining 972,00), but a real line is dropped without a trace. Any fix here must run
**before** the reconcile step (`:601`), and the "qty > 1000" heuristic itself is an owner
decision (Q7).

### 3.2 How the fix slots in

A pure helper (new file, e.g. `Services/Validation/LocaleNumberCandidates.cs`):

1. **Candidates from `content`.** Parse the printed string strictly under the conventions that
   occur on our invoices — decimal comma with space / dot / no thousands separator; decimal
   point with comma / no thousands separator — enforcing 3-digit grouping (RESEARCH §3: .NET's
   own parser does not). „3 888,000" → {3888}; „9,000" → {9, 9000}; „972,00" → {972}.
2. **Detection (cheap, certain).** If Azure's value is not among the candidates (3 ∉ {3888}),
   the value is wrong. If there are several candidates, the value is ambiguous.
3. **Disambiguation by arithmetic.** Choose the candidate for which `quantity × unit price`
   matches the line net within the line rule's tolerance, widened by the unit price's printed
   precision (0,2066 has 4 decimals → ± quantity × 0,00005). If no line-level choice works
   (ASF0021438: the line net is itself wrong), try the combination of candidates whose
   `Σ quantity × unit price` matches the header net (BR-CO-10 shape); on ASF0021438 that gives
   3888 × 0,2066 + 9 × 14,5456 = 934,17 vs 934,22 — within tolerance, but only barely, and it
   still leaves line 1's net at 972,00.
4. **Never silent.** If a value is replaced, a flag records it. Proposal: new
   `NUMBER_REINTERPRETED` (review in Etapas 1) „Skaičius perskaitytas iš dokumento teksto";
   unresolved ambiguity → `NUMBER_AMBIGUOUS` (review) „Dviprasmiškas skaičius".

**Honest scope.** Detection (step 2) is well-defined and would have caught both quantities on
ASF0021438. Disambiguation (step 3) fixes the quantity but not the wrong-column line net, so
that invoice would still end in NEEDS_REVIEW via BR-CO-10. The line-net column problem is
D-023 / Etapas 3. Recommendation: Etapas 1 ships **detection for all numeric fields +
line-level disambiguation**; the header-level combination search waits for more data (Q6).

### 3.3 Corpus

| Where | Raw Azure JSONs | Source |
|---|---|---|
| repo / local | 1 (`raw-asf0021438.json`, git-ignored); no JSON fixtures under `Tests/` | file search 2026-09-26 |
| dev DB (100.110.26.80) | **1** of 14 invoices (`ocr_raw_json` not null), 1 `files` row | read-only query 2026-09-26 |
| staging | **unknown** — not queried (not allowed in this session); at least invoice 376 (storage gate) and the uploads made during the staging checks | `STATE.md` |
| production | **unknown** — agents never query production; D-016 (2026-09-15) recorded `ocr_raw_json` empty in all 247 invoices then | D-016 |

Local PDFs: 13 expense invoices in `wwwroot/uploads/invoices/2026/07/` and 1 in
`Docs/Invoice pvz/` (untracked); the 6 in `.playwright-mcp/` look like our own sales invoices
(LAK/KLAK numbers) — not checked.

**How to collect more (proposal, needs the owner):**

1. Owner runs the §7 count query on staging (and, if wanted, production) to know what exists.
2. A one-off, git-ignored dev script sends the ~11 digital local PDFs through Azure and saves
   the JSON under an ignored corpus folder — ~11 Azure calls; this is the start of the D-005
   corpus that Etapas 4 needs anyway (and D-016's "~10 raw responses" condition).
3. From now on every staging upload stores its JSON; the staging checks for Etapas 1 add ~10
   uploads of real supplier PDFs.
4. Unit tests use hand-built JSON fragments with the real ASF0021438 strings; they do not need
   the corpus. The corpus is for judging the false-positive rate before switching
   `NUMBER_REINTERPRETED` from review to information.

### 3.4 Files, frozen, tests, staging, estimate

- New pure helper + tests; `ExpenseOcrService.cs` line loop (`:462-597`) and header block
  (`:373-402`) to read `content` next to the typed value; `OcrResultDto.cs` flag constants.
  No frozen file.
- Tests: the ASF0021438 strings, both directions, grouping violations („1,1.1"), negative
  amounts, the reconcile interaction.
- Staging: upload ASF0021438 (owner has the PDF?) and 2–3 invoices with thousands in
  quantities.
- Estimate: ~8–12 h for detection + line-level disambiguation (RESEARCH: 6–10 h); +4–6 h for
  the header-level search if chosen.

---

## 4. Re-OCR for stored files

### 4.1 Why it is unreachable today

The whole chain already works by `file_id`: `RerunOcrAsync` refuses when `FileId` is null
(`InvoiceDetailDialog.razor:1004-1008`) and passes `ExistingFileId`
(`:1014`); the upload dialog reads the blob through `IFileStore.OpenAsync`
(`ExpenseUploadDialog.razor:593-613`), runs the D-032 intake check (`:617-621`) and Azure
(`:632`). The **only** blocker is the button's condition:

`InvoiceDetailDialog.razor:430` — `!string.IsNullOrEmpty(_invoice?.OriginalFilePath) && …`

New uploads never set `original_file_path` (`CreateFromOcrAsync` copies
`ocrResult.OriginalFilePath`, `ExpenseService.cs:1694`, which a new upload never fills), so
by the code no invoice gets both: one with a stored file has no button, one with a path has no file.

### 4.2 Proposal

1. Button condition → `_invoice?.FileId != null && status in (NEEDS_REVIEW, PENDING,
   PENDING_SUPPLIER)`. The 247 old invoices lose a button that could only ever refuse.
2. **Fix a data loss that becomes reachable at the same moment:** on re-OCR,
   `SaveAsync` sets `OriginalFilename = _ocrResult.OriginalFilename ?? ""`
   (`ExpenseUploadDialog.razor:971`), `ProcessAsync` never sets it, and `UpdateFromOcrAsync`
   writes it unconditionally (`ExpenseService.cs:1866`) → the stored original filename is
   overwritten with "". Keep the stored value when the OCR result has none.
3. **Allocations are deleted on re-OCR:** `UpdateFromOcrAsync` deletes all lines
   (`:1910`) and the FK cascade removes their allocations (0c report §1, C2b inventory).
   Options: refuse re-OCR when allocations exist, or warn and require confirmation. Owner
   decision (Q8).
4. `UpdateFromOcrAsync` is not one transaction (several `SaveChangesAsync` / raw SQL calls,
   `:1839-1946`), unlike `SaveInvoiceEditAsync` (`:313`) — D-010. Wrap it, same pattern.

### 4.3 Files, frozen, tests, staging, estimate

- `InvoiceDetailDialog.razor` (not frozen), `ExpenseUploadDialog.razor` `SaveAsync` — a
  non-frozen `@code` method (FROZEN §3 allows "other @code methods"), `ExpenseService.cs`.
- Tests: `ExpenseReOcrTests` — filename kept, transaction rollback, allocation rule.
- Staging: re-OCR on an invoice uploaded since the storage change (e.g. 376) — it now calls
  Azure with the production key (D-029).
- Estimate: ~3–5 h.

---

## 5. Open items from `STATE.md`: in Etapas 1 or not

| Item | In Etapas 1? | Argument |
|---|---|---|
| `OWN_COMPANY` (and `INVALID_VAT_RATE`) dropped on edit | **Yes — first** | Same function every new gate must pass through (`ComputeManualEditFlags`). Proposal: replace the 5-flag allow-list with an explicit list of flags the edit path *recomputes*; every other stored flag is carried over. Unknown flags then survive by default instead of vanishing. |
| Re-OCR unreachable for stored files | **Yes** (§4) | Needed to verify the new gates on staging against stored files without new uploads. |
| Drag & drop does not work | **No** (separate task, before Etapas 1 or alongside) | Not a gate. Code is FROZEN §3 (`OnAfterRenderAsync`, `dropzone.js`, `App.razor`); needs diagnosis in a real browser first and the owner's permission. It does block staging check 18.3 for Etapas 1 only if drops are part of the check — they need not be. |
| Budget / cash-flow / supplier-history dialogs unreachable | **No** | Not a gate. Needs a product decision: wire them (where, for whom) or delete them. Until then Etapas 0 checks 2 and 5 cannot be done. |
| „Patikrinkite ar visi serveriai veikia…" | **No** | FROZEN §3 markup; one-line change; bundle it with the drag & drop task under the same permission. |
| `OcrQueueWorker` `Attempts++` + gate bypass + webhook | **No** (owner decision, but soon) | FROZEN §5. The n8n webhook (`ExpenseController.cs`) can enqueue; each such row would be re-sent to Azure every cycle and never create an invoice (§0). D-007 said the queue path is to be revived — if it is, it must call `CreateFromOcrAsync` / `UpdateFromOcrAsync` so the gates apply. Recommend deciding "retire, or route through the service" and checking whether n8n posts to production at all. |
| decimal-precision findings | **No** | None is in `Models/Expenses/` (0c report appendix: 57 hits of the rule, not 12 — the count needs reconciling); model/migration hygiene, own task. |
| Data Protection keys, 3.8 GB RAM | **No** | Infrastructure, outside OCR. (With no production deploy until Etapas 4, the logout-on-deploy pain is staging-only for now.) |
| Production data cleanup, „248" counter | **No** | Separate work with the accountant (Q-010). The new gates act on new, re-OCR'd and edited invoices only; they do not rewrite old rows. |
| ULAK `DeliveryList`, CI `--exclude-path` | **No** | Outside OCR (recorded only). The CI grep fix is small and makes a silent gate loud — worth a separate one-commit task. |

---

## 6. Proposed split (sessions → commits), in order

Each commit: task spec from the planning chat → agent → fresh reviewer → full
`dotnet test --filter "Category!=E2E"` → guardrail + semgrep. Parallel work only for new-file,
no-DB items (D-031).

| # | Session | Commits | Depends on | Est. |
|---|---|---|---|---|
| S1 | **Pure prep** (new files / validator files only, no DB) | (a) `LineAmountPlausibilityRule` never throws; (b) `VatCodeFormatValidator` four notes; (c) `VatRateTable` + tests (after Q2); (d) `LocaleNumberCandidates` + tests | owner rate confirmation for (c) | 6–9 h |
| S2 | **Flag plumbing** | (a) `ComputeManualEditFlags` carry-over redesign + `OWN_COMPANY` / `INVALID_VAT_RATE` regression tests; (b) new flag constants, labels, colours, `HasReviewFlag`, `IsCriticalFlag` (no producer yet) | S1 not needed | 2–3 h |
| S3 | **Re-OCR by `file_id`** | (a) button condition; (b) filename kept; (c) transaction; (d) allocation rule (Q8) | S2 (so re-OCR does not meet the old carry-over) | 3–5 h |
| S4 | **EN 16931 gate** | (a) the §1.2 helper with BR-CO-15 replacing `AddAmountConsistencyFlags`'s tolerance; (b) BR-CO-10 replacing both `AMOUNT_MISMATCH` checks; (c) line rule as information; (d) rule messages in the detail dialog | S1a, S2; Q1 | 8–10 h |
| S5 | **IBAN + VAT format gates** | (a) VAT format before VIES / supplier match in `ProcessAsync`; (b) both in the helper, carry-over on the edit path | S1b, S2, S4a | 3–4 h |
| S6 | **VAT-rate whitelist gate** | wiring only | S1c, S4a | 2–3 h |
| S7 | **Locale detection + line-level disambiguation** | (a) read `content` next to typed values; (b) detection flags; (c) line-level choice; (d) reconcile step decision (Q7) | S1d, S4 (uses the line rule tolerance) | 8–12 h |
| S8 | **Staging checks document** `STAGING-CHECKS-ETAPAS1.md`, then the owner's run | — | all | 2–3 h + owner |

**Total ≈ 34–49 h** of agent work plus reviews and the owner's staging time — above
RESEARCH's ~25 h because the validators' wiring touches three write paths and the tests of
five existing classes, and because §4 and the carry-over fix were not in RESEARCH's list.

Why this order: S2 first because every later flag would otherwise be erased by the first
edit; S3 early because it gives a way to re-run stored invoices on staging; S4 before S5–S7
because they reuse its helper; S7 last because it has the weakest evidence base (one raw
response) and benefits from the corpus collected meanwhile.

---

## 7. Staging verification for Etapas 1

Staging is a production clone (D-029), uploads call Azure with the production key, and
staging is now the only place Etapas 1 is proven (D-037). Read-only queries the owner can run
**before** S4 to size the behaviour change (not run by me — staging is off-limits in this
session):

```sql
-- raw JSON corpus on staging
SELECT COUNT(*) AS invoices, SUM(ocr_raw_json IS NOT NULL) AS with_raw_json, SUM(file_id IS NOT NULL) AS with_file FROM expense_invoices;
-- how many invoices a 0.01 header rule would newly flag (today's tolerance is 0.02)
SELECT COUNT(*) FROM expense_invoices WHERE amount_excl_vat > 0 AND amount_incl_vat > 0 AND ABS(amount_excl_vat + vat_amount - amount_incl_vat) > 0 AND ABS(amount_excl_vat + vat_amount - amount_incl_vat) <= 0.02;
-- supplier countries that actually occur
SELECT bp.country_code, COUNT(*) FROM expense_invoices e JOIN business_partners bp ON bp.id = e.supplier_id GROUP BY bp.country_code;
-- VAT rates that actually occur
SELECT vat_rate, COUNT(*) FROM expense_invoices GROUP BY vat_rate ORDER BY 2 DESC;
```

The Etapas 1 checks themselves (per gate: one invoice that must be stopped, one that must
pass, on create, re-OCR and edit) are written in S8 against the final code, like
`STAGING-CHECKS-ETAPAS0.md`.

---

## 8. Risks across the stage

- **More NEEDS_REVIEW.** Exact BR-CO comparisons, IBAN, VAT format and rate gates all add
  review. D-031 accepts loud over silent, but RESEARCH §6 warns that review fatigue makes
  soft flags useless — hence information-only for the line rule, `VAT_FORMAT_UNCHECKED`,
  `VAT_RATE_UNCHECKED` until measured.
- **Carry-over by default** (S2) means a flag set once stays until a path recomputes it.
  Every recomputed flag must be listed explicitly; a missing entry would make a stale flag
  sticky (loud, not silent).
- **Supplier assignment uses stored flags** (`:1605`). New review flags stored at OCR time are
  honoured there; flags that depend on the supplier (VAT rate by the partner's country) are
  not recomputed on assignment unless S6 adds it. Proposal: recompute the rate gate on
  assignment too.
- **D-016 tension.** D-016 deferred "validators" until ~10 raw responses had been read; one
  has been. D-022 later put gates first. The deterministic gates (§1, §2) do not depend on how
  good Azure is — they stop whatever arrives. §3 does depend on it, which is why it is last
  and scoped to detection. The owner should confirm D-022 supersedes D-016 for §1–§2 (Q9).
- **Queue path.** A webhook row loops through Azure without creating an invoice; if the worker
  is ever pointed at real invoices, it writes them past every gate (§0).
- **Tests depend on Tailscale** to the dev test DB.

---

## 9. Open questions for the owner

- **Q1 — Tolerance.** Accept the literal Schematron comparison (0.01 differences now flag) for
  both header and lines, replacing today's 0.02 / 0.05 / 0.01? Or keep a project tolerance on
  top? (The §7 query sizes it.)
- **Q2 — VAT rates.** Which countries must the table cover (the §7 country query answers what
  occurs), who confirms the rates against primary sources, and is PL in or out?
- **Q3 — Line rule.** `LINE_AMOUNT_IMPLAUSIBLE` as information (proposed) or as a review gate?
  Line discounts and per-1000 unit prices make false positives likely.
- **Q4 — Unknown country / unknown format.** Information only (proposed) for
  `VAT_FORMAT_UNCHECKED` and `VAT_RATE_UNCHECKED`, or review?
- **Q5 — After a supplier is assigned.** Should `INVALID_VAT_FORMAT` / `INVALID_IBAN` from the
  document still hold the invoice in review once a human picked or created the supplier? And
  should an invalid IBAN be refused when `SupplierCreateDialog` is prefilled from it
  (`SupplierCreateDialog.razor:296`, via `InvoiceDetailDialog.razor:813`) — that is where it
  would enter master data.
- **Q6 — Disambiguation depth.** Detection + line-level only in Etapas 1 (proposed), or also the
  header-level combination search? And may the code **replace** Azure's number when arithmetic
  decides (with `NUMBER_REINTERPRETED`), or only flag?
- **Q7 — Reconcile heuristics.** The "remove lines with quantity > 1000" step
  (`ExpenseOcrService.cs:629-640`) and the "remove duplicate descriptions" step silently delete
  lines. Keep, flag, or remove?
- **Q8 — Re-OCR with allocations.** Refuse, or warn and confirm?
- **Q9 — D-016.** Confirm that D-022 supersedes D-016's "validators wait for ~10 raw responses"
  for the deterministic gates; and approve the ~11-call Azure run on the local PDFs (§3.3) to
  start the corpus.
- **Q10 — Queue worker and n8n webhook.** Does n8n post to `api/expense/webhook` in production?
  Retire the worker (and the webhook), or route it through `CreateFromOcrAsync` (FROZEN §5
  permission either way)?
- **Q11 — Orphan dialogs.** Wire the budget / cash-flow / supplier-history dialogs into the UI
  (where?) or delete them? Not Etapas 1, but Etapas 0 checks 2 and 5 wait on it.
