# Staging checks — Etapas 0 + 0c (+ Etapas 1 validators)

Written 2026-09-26 by Claude Code (prepush session) for the owner. Nothing here has been run.
Run it on **staging** (`nordic_bees_erp_staging`, a production clone — D-029) after the push.
Source: "what to check on staging" in `.opencode/reports/ocr-etapas0-20260925-2354.md` §5
(E0-1…E0-9) and `.opencode/reports/ocr-etapas0c-20260926-0136.md` §7 (0c-1…0c-10). Every item
of both lists is mapped below; the mapping table is at the end.

The Etapas 1 validators (IBAN, VAT format, EN 16931 totals) are **not wired in** — nothing to
check on staging for them.

## How to read this document

- Every SQL statement is read-only (`SELECT`). It is run as
  `sudo mariadb nordic_bees_erp_staging -e "…"` on `lakstena-dev`.
  The SQL uses no double quotes, `!`, `$` or backticks, so it pastes safely inside `-e "…"`.
- `ocr_flags` is a JSON array stored as text (e.g. `["ZERO_VAT","AMOUNT_MISMATCH"]`, written by
  `JsonSerializer.Serialize`). Flags are tested with `JSON_CONTAINS(ocr_flags, JSON_QUOTE('FLAG'))`.
- Table/column names were checked against the model `[Table]`/`[Column]` attributes,
  `Migrations/20260602150000_InitialCreate.cs` and
  `Migrations/20260915120000_AddFilesTableAndExpenseInvoiceFileId.cs`. InitialCreate is a
  `CREATE TABLE IF NOT EXISTS` dump and `__EFMigrationsHistory` is not proof of schema (D-029);
  if a query fails with "Unknown column", run `DESCRIBE <table>` and tell Claude — do not guess.
  Exception: `expense_payments.source` exists in the model (`ExpenseModels.cs:145`) and on prod
  (`Docs/infra/SERVER-STATE.md` §7.1) but in no migration file.
- Checks 2–9 do not change data. **Checks 10–19 change staging data** and are listed last.
- `<INV0>`, `<AUD0>`, `<FILE0>`, `<PAY0>` are the baseline ids from check 1.5. Replace them by
  hand.

## Warnings (read before starting)

1. **Line-less invoices and the OLD code.** The old edit-form save zeroed the amounts of an
   invoice with no lines (fixed by 0c C2b, D-035). **Never save a line-less invoice (e.g. 167,
   flag `LINES_NOT_FOUND`) on code older than this push** — not on production until production
   is deployed, and on staging only after check 1 has proved that the new code is running.
2. **Re-OCR cannot be exercised on staging at all.** The „PAKARTOTI OCR" button is shown only
   when `original_file_path` is non-empty and the status is NEEDS_REVIEW / PENDING /
   PENDING_SUPPLIER (`InvoiceDetailDialog.razor:430`), and `RerunOcrAsync` additionally refuses
   when `file_id` is NULL (*„OCR negalima pakartoti – šio dokumento failas nėra saugomas
   centralizuotoje failų saugykloje."*).
   - The 247 prod-clone invoices have a path but `file_id` NULL (PDFs gone,
     `Docs/infra/SERVER-STATE.md` §1.3) → refused with that message.
   - Invoices uploaded since the storage change have a `file_id` but `original_file_path` NULL —
     a new upload never sets it (`ExpenseService.cs:1694` copies `ocrResult.OriginalFilePath`, which
     only the re-OCR path sets, `ExpenseUploadDialog.razor:968`) → the button is not shown.
   So E0-3 (re-OCR one of the 336 invoices), 0c-1's positive re-OCR case and the 0c-6 item
   "re-OCR of an old image-derived invoice → Failas nepriimtas" **cannot be run as the reports
   describe them**. They are replaced by checks 4 and 18.1. This is a product finding for the
   owner (re-OCR unreachable from the UI), not a staging failure.
3. **Every upload calls Azure DI with the production key** (D-029: staging has prod
   `app_settings`). Only check 18 uploads for analysis; refusal checks (7, 8) stop before
   Azure.
4. **Do not save a bank import on staging** (check 6 stops before saving).
5. Duplicate re-uploads (check 18.4) need the **original PDF** of invoice 148 (PRD 0015764) from
   e-mail — it is not on the server.
6. Data changed here stays on staging. Staging is re-cloned from prod when needed (D-029).

---

## 1. Deploy check (no data change)

**Proves:** staging runs the pushed code, started cleanly, and storage is mounted.

1.1 **CI.** In GitHub Actions, the "Build and Deploy" run for the pushed `main` commit must be
green. Also open the "Hardcode Check" run (0c-10): its semgrep step (added in C8) is the last step
of that job, after the company-name step, which exits 1 on hits and has known pre-existing hits
(0c report §2; `hardcode-check.yml` "Check for hardcoded company name"). So:
- if the company-name step is red, the semgrep step was **skipped** — record "C8 semgrep gate did
  not run on this push" for the owner; it does not block the deploy (separate workflow);
- if it reached the semgrep step, record that step's result.
I did not run the workflow; which of the two happens is decided by the real run.
Failure: a red "Build and Deploy" → staging still runs the old image; stop here.

1.2 **Container recreated** (on `lakstena-dev`):

```bash
sudo docker ps --filter name=nordicbees_staging --format '{{.Names}} {{.Image}} {{.CreatedAt}} {{.Status}}'
sudo docker inspect -f '{{.Image}} {{.RestartCount}}' nordicbees_staging
sudo docker image inspect -f '{{.Id}} {{.Created}}' nordicbees-erp:staging
```

Expected: `nordicbees_staging`, image `nordicbees-erp:staging`, `CreatedAt` after the push time,
`Up …`, RestartCount `0`, and the container's image id equals the `nordicbees-erp:staging` id.
The image tag is **not versioned** (`deploy.yml` always tags `:staging`), so the version is
checked in the UI instead: the footer shows the version `bump-version.sh` set (currently 0.17.90
before the bump).
Failure: an old `CreatedAt`, a RestartCount > 0 (restart loop — see 1.3), or different image ids.

1.3 **Startup log:**

```bash
sudo docker logs nordicbees_staging 2>&1 | grep -E 'Kritinė klaida|Pending EF migrations|Migration warning|Unhandled exception|fail:'
```

Expected: no `Kritinė klaida` (storage sentinel), no `Unhandled exception`.
This push adds **no** migration (`git diff cff2d46..HEAD -- Migrations/` is empty), so no
`dotnet ef database update` is needed. A `Pending EF migrations detected in Staging …` line may
already have appeared before this deploy (history rows were hand-maintained, D-029): compare with
the previous container's log if you have it — only a *new* pending migration name would be a
failure.

1.4 **Storage sentinel:**

```bash
sudo cat /var/lib/nordicbees/staging/.nordicbees-storage
```

Expected: `Staging`. The sentinel never logs on success; on failure the process exits with
*„Kritinė klaida: nerastas žymeklio failas …"* or *„… turinys … neatitinka laukiamos aplinkos …"*
and the container restarts in a loop (RestartCount > 0).

1.5 **Baseline ids and status snapshot** — write the numbers down; later checks use them.

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT (SELECT MAX(id) FROM expense_invoices) AS INV0, (SELECT MAX(id) FROM expense_invoice_audit) AS AUD0, (SELECT MAX(id) FROM files) AS FILE0, (SELECT MAX(id) FROM expense_payments) AS PAY0;"
sudo mariadb nordic_bees_erp_staging -e "SELECT status, COUNT(*) AS n, SUM(amount_incl_vat) AS gross FROM expense_invoices GROUP BY status ORDER BY status;"
```

Reference (PROD-DATA-FINDINGS §1, 2026-09-25): DUPLICATE_PENDING 28, NEEDS_REVIEW 60, PENDING 40,
PENDING_SUPPLIER 119. Differences are not a failure by themselves (staging was used since), but
note them.

---

## 2. Quarantine out of totals (E0-1) — no data change

**Proves:** `DUPLICATE_PENDING` and `REJECTED` are no longer counted as payables (Etapas 0
commit 2, D-027), but still listed.

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT id, invoice_number, supplier_id, status, amount_incl_vat FROM expense_invoices WHERE id IN (149,158,201,209,206,207,208,210,211,212,226,231,232,246,249,252,253,255,274,278,311,327,333,340,354,357,361,277) ORDER BY id;"
sudo mariadb nordic_bees_erp_staging -e "SELECT COUNT(*) AS n, SUM(amount_incl_vat) AS gross FROM expense_invoices WHERE id IN (149,158,201,209,206,207,208,210,211,212,226,231,232,246,249,252,253,255,274,278,311,327,333,340,354,357,361,277);"
```

Expected: 28 rows, all `DUPLICATE_PENDING`; the Etapas 0 report gives 28 679,74 € for them
(246 alone is 23 524,80 €, 277 is 0,00 €).

Browser:
- Cash flow (ExpenseCashFlow): 246 (23 524,80 €) and the other ids above do not appear and are
  not in any total.
- Supplier history for 246's `supplier_id`: its totals exclude 246; the table still lists it.
  (Known: the count KPI counts payable invoices, the table lists all rows.)
- Budget dialog for 246's year (see `invoice_date` below): 246's 23 524,80 € (net
  `amount_excl_vat` — budget actuals are net, D-036) is not in any actual
  (`ExpenseBudgetDialog.razor:165` excludes quarantined invoices).
- ExpenseInvoices list: all 28 are still listed.

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT id, invoice_date, amount_excl_vat, amount_incl_vat, category_id FROM expense_invoices WHERE id = 246;"
```

Failure: 23 524,80 € (or any listed id) inside a cash-flow, supplier-history or budget figure, or
a listed id missing from the ExpenseInvoices list.

## 3. Due date shown as assumed (E0-6, 0c-9) — no data change

**Proves:** `MISSING_DUE_DATE` is information and the assumed date is marked (B2, D-025).

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT id, invoice_number, invoice_date, due_date, status FROM expense_invoices WHERE JSON_CONTAINS(ocr_flags, JSON_QUOTE('MISSING_DUE_DATE')) ORDER BY id DESC LIMIT 5;"
```

Browser: open one of them — the detail view shows the chip „Terminas numatytas (+30 d.)" and
„(numatytas)" after the due date. Open the payment dialog (then cancel) — „(numatytas)" there too.
Failure: no marker, or the flag shown as an error. If the query returns no rows, the check cannot
be done on existing data — note it.

## 4. Re-OCR availability and the 336 links (E0-3, 0c-1) — no data change

**Proves:** re-OCR is not offered on paid / quarantined invoices; the old 336 links were not
changed automatically; re-OCR on file-less old invoices is refused before Azure.

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT id, invoice_number, supplier_id, status, file_id, original_file_path FROM expense_invoices WHERE id IN (139,140,145,146,172,173,186,190,197,202,206,237,256,296,319,331) ORDER BY id;"
sudo mariadb nordic_bees_erp_staging -e "SELECT id, invoice_number, status, file_id, original_file_path FROM expense_invoices WHERE file_id IS NOT NULL ORDER BY id;"
```

Expected: the 16 rows still have `supplier_id` 336 (cleanup is PROD-DATA-FINDINGS §6.3, not
automatic) and `file_id` NULL. The second query lists invoices uploaded after the storage change
(e.g. 376); they have `original_file_path` NULL, so no re-OCR button (warning 2) — they are used
in check 8.

Browser:
- A PAID invoice: no „PAKARTOTI OCR" button.
- 149, 246, 361 (DUPLICATE_PENDING): no „PAKARTOTI OCR" button.
- One of the 16 whose status is NEEDS_REVIEW / PENDING / PENDING_SUPPLIER and
  `original_file_path` is non-empty: „PAKARTOTI OCR" → *„OCR negalima pakartoti – šio dokumento
  failas nėra saugomas centralizuotoje failų saugykloje."* Nothing else happens.
- An invoice from the second query: no „PAKARTOTI OCR" button (expected today, warning 2).

Failure: the button on a paid or quarantined invoice, or a re-OCR dialog opening for a
`file_id` NULL invoice. The "not assigned to 336 when the VAT code is missing" part of E0-3 is
checked with a new upload (check 18.1).

## 5. Budget actuals (0c-8) — no data change

**Proves:** actuals come from real lines and allocations, net (C7, D-036).

Browser: open the budget dialog for 2026. Expected: actuals are non-zero where invoices exist; a
„Nepriskirta" row appears if uncategorised expenses exist; a notice lists inconsistent
allocations, if any. Known: „Nepriskirta" rows show an "Ok" chip; a negative-gross line with
allocations is reported as over-allocated.
Failure: all actuals 0 (the old behaviour).
No SQL is given: the category chain (line → allocation → invoice category) is D-036 logic, and I
did not re-derive it as a query.

## 6. Bank import candidates (0c-5) — see warning 4

**Proves:** duplicates and rejected invoices are not offered as payment matches (C3, D-033).

Browser: Bank import → load a statement → **stop at the candidate list; do not save.** The
expense-invoice candidates must not contain 246, 149, 361 (or any id from check 2) nor 277.
Loading a statement without saving writes nothing: `CreateBankImportAsync` is called only from
`SaveMatches` (`BankImport.razor:648`, reviewer-verified).

Read-only guard, useful on its own (payments ever attached to a quarantined invoice):

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT p.id, p.invoice_id, p.amount, p.payment_date, p.source, e.status FROM expense_payments p JOIN expense_invoices e ON e.id = p.invoice_id WHERE e.status IN ('DUPLICATE_PENDING','REJECTED') ORDER BY p.id;"
```

Expected: no row with `p.id > <PAY0>`. Older rows, if any, come from prod data (report them).

## 7. Upload intake refusals, no analysis (0c-6) — no data change

**Proves:** only digital PDFs are accepted; refusals happen before Azure (C5, D-032).

Browser (upload dialog):
- choose a JPG/PNG → refused at selection;
- choose a scanned PDF (e.g. the local „Ratukų kronšteinų centras" / „Sanitex" type) and press
  „Analizuoti" → „Failas nepriimtas" with the scans message (the check runs on „Analizuoti",
  still before Azure — `ExpenseUploadDialog.razor:856-860`);
- a digital PDF → accepted into the dialog (close it before „Analizuoti" for this check).

Afterwards: `SELECT MAX(id) FROM expense_invoices;` and `SELECT MAX(id) FROM files;` still equal
`<INV0>` / `<FILE0>`.
Failure: an image or scan reaches the processing phase, or new rows appear.

## 8. Same PDF again (E0-8, part of 0c-6) — no data change

**Proves:** an identical file is refused before Azure (B5).

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT e.id, e.invoice_number, e.status, f.id AS file_id, f.sha256, f.original_filename FROM expense_invoices e JOIN files f ON f.id = e.file_id ORDER BY e.id;"
```

Take the PDF of one listed invoice (compare locally with `shasum -a 256 file.pdf` against
`sha256`, lowercase hex). Browser: upload it → „Analizuoti" → *„Failas jau įkeltas" / „Šis
failas jau įkeltas: sąskaita Nr. … (…)"* — not „OCR nepavyko". (Known: the error-phase line
„Patikrinkite ar visi serveriai veikia…" may still appear under it — frozen §3, owner decision.)
Afterwards `MAX(id)` of `expense_invoices` and `files` unchanged.
"No Azure call": I found no log line that marks an Azure DI request, so the proof is that the
refusal appears without the processing phase and no rows are created. The Azure portal request
count is the independent check.
Failure: the processing phase starts, or a new invoice/file row appears.

## 9. Drag & drop without analysis (0c-7) — no data change

**Proves:** drag & drop works for the first time (C9, D-034). Chrome **and** Firefox.

- drop a PDF → accepted;
- drop a JPG → „Priimami tik PDF failai.";
- drop a file > 10 MB → refused;
- drop a renamed non-PDF (`.pdf`) → refused;
- known limitation (frozen §3): drop → ✕ remove → drop again does nothing.

Close the dialog without „Analizuoti". The "drop an existing PDF → Failas jau įkeltas" item is
check 8 done by dropping instead of choosing.
Failure: the browser opens the file, or nothing happens on the first drop.

---

# Checks that CHANGE staging data

Run them after checks 1–9, in this order. After each one, the audit query shows what happened:

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT id, invoice_id, invoice_number, action, action_details, old_status, new_status, performed_by, performed_at FROM expense_invoice_audit WHERE id > <AUD0> ORDER BY id;"
```

Before touching an invoice, save its state:

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT id, invoice_number, supplier_id, status, amount_excl_vat, vat_amount, amount_incl_vat, invoice_date, due_date, ocr_flags, approved_by, approved_at, rejected_reason FROM expense_invoices WHERE id = <ID>;"
```

Status rules used below (`ExpenseService`): `DecideOcrStatus` = `WRONG_RECIPIENT` → REJECTED;
no supplier → PENDING_SUPPLIER; any review flag → NEEDS_REVIEW; else PENDING. Review flags:
MISSING_AMOUNT, AMOUNT_MISMATCH, LOW_CONFIDENCE, ZERO_VAT, MISSING_INV_NUMBER,
AMOUNT_ARITHMETIC_MISMATCH, MISSING_MONEY_FIELD, FUTURE_DATE, STALE_DATE, MISSING_INV_DATE.

## 10. „Tai skirtinga sąskaita" on 277 (E0-2) — CHANGES STAGING DATA

**Proves:** the false duplicate is released with an audit row and a rule-based status (B3).

Browser: invoice 277 → duplicate dialog → „Tai skirtinga sąskaita".
SQL: the state query with `<ID>` = 277, then the audit query.
Expected: `status` is no longer DUPLICATE_PENDING. It is decided from the **stored** flags minus
`DUPLICATE` (no recompute, `ExpenseService.cs:1509-1515`): REJECTED if `WRONG_RECIPIENT` is stored,
PENDING_SUPPLIER if `supplier_id` is NULL, NEEDS_REVIEW if any review flag is stored (the report
expects MISSING_AMOUNT, 0,00 €), otherwise PENDING — predict it from the "before" row; `ocr_flags` no longer contains `DUPLICATE`;
one audit row `DUPLICATE_DISMISSED`, details „Pažymėta kaip skirtinga sąskaita (ne dublikatas)",
old DUPLICATE_PENDING, `performed_by` = your name.
Failure: status still DUPLICATE_PENDING, the flag still there, or no audit row.

## 11. „Atmesti kaip dublikatą" (E0-2) — CHANGES STAGING DATA

**Proves:** reject = REJECTED, never DELETE (D-027 §3).

Browser: one of the 27, e.g. 361 → duplicate dialog → „Atmesti kaip dublikatą" → confirm.
SQL: state query for 361, audit query.
Expected: the row still exists, `status` REJECTED, `rejected_reason` „Dublikatas: <original's
number>", audit row `REJECTED` with the same details, old DUPLICATE_PENDING.
Failure: the row is gone, or status unchanged.

## 12. „PATVIRTINTI" on an unresolved duplicate (E0-2) — CHANGES STAGING DATA (only if it fails)

**Proves:** approval is refused while the duplicate is unresolved.

Browser: e.g. 357 → „PATVIRTINTI" → warning *„Dublikatą pirmiausia reikia išspręsti"*.
SQL: state query for 357 — `status` still DUPLICATE_PENDING, `approved_by` NULL; no new audit
row for 357.
Failure: approved_by filled or status changed.

## 13. Arithmetic gate on 213 / 167 / 168 (E0-4) — CHANGES STAGING DATA

**Proves:** broken header arithmetic keeps an invoice in NEEDS_REVIEW, and correcting it clears
the arithmetic flags (Etapas 0 commit 3, D-028).

Warning 1 applies to 167 (line-less) — only after check 1 passed.

Browser: edit form of 213 (0,00 / 0,00 / 465 374,45) → save without changing amounts.
Expected: stays NEEDS_REVIEW; `ocr_flags` contains `AMOUNT_ARITHMETIC_MISMATCH` and/or
`MISSING_MONEY_FIELD` (label „Trūksta sumos duomenų").
Then correct the amounts so that net + VAT = gross → save.
Expected: the arithmetic flags are gone. **Note:** the status leaves NEEDS_REVIEW only if no other
review flag remains — 213 also carries `ZERO_VAT` (and `AMOUNT_MISMATCH`), which are review flags
too; if one of them is still in `ocr_flags`, NEEDS_REVIEW is correct, not a failure. Compare the
flags, not only the status.
Repeat for 167 and 168. Audit: `EDITED` rows.
Failure: 213 saved without correction leaves NEEDS_REVIEW, or the arithmetic flags survive a
correct header.

## 14. Future-date gate on 168 / 341 / 167 (E0-5) — CHANGES STAGING DATA

**Proves:** future dates are flagged on edit; correcting the date clears the flag (B1).

Browser: edit + save 168 (2026-11-02) and 341 (2026-10-06).
Expected: `FUTURE_DATE` in `ocr_flags` („Data ateityje"), status NEEDS_REVIEW (if they have a
supplier). The flag needs a date after today: from 2026-10-06 on, 341 no longer qualifies, and
from 2026-11-02 on, 168 neither.
167 (2026-09-02) does **not** get the flag — the date is already past; it keeps the arithmetic
problem (check 13).
Correct 341's date to a past date → save → `FUTURE_DATE` gone.
Failure: no flag on 168/341, or the flag stays after correction.

## 15. Manual edit rules and approval voiding (0c-2) — CHANGES STAGING DATA

**Proves:** the edit form applies the same review rules as OCR; an approval survives edits of
non-gate fields and is voided and audited otherwise (C2).

Find candidates:

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT id, invoice_number, status, amount_excl_vat, vat_amount, amount_incl_vat, ocr_flags, approved_by FROM expense_invoices WHERE status = 'PENDING' ORDER BY id DESC LIMIT 10;"
sudo mariadb nordic_bees_erp_staging -e "SELECT id, invoice_number, supplier_id, status, ocr_flags, approved_by FROM expense_invoices WHERE JSON_CONTAINS(ocr_flags, JSON_QUOTE('ZERO_VAT')) AND status = 'NEEDS_REVIEW' AND supplier_id IS NOT NULL ORDER BY id DESC LIMIT 10;"
```

a) A PENDING invoice: break the amounts (e.g. 803,31 / 168,69 / 1 000,00) → save → NEEDS_REVIEW.
b) A NEEDS_REVIEW ZERO_VAT invoice with a supplier („PATVIRTINTI" is shown only for NEEDS_REVIEW /
   DUPLICATE_PENDING / PENDING_SUPPLIER, `InvoiceDetailDialog.razor:439`): approve → PENDING with
   `approved_by` set; edit only the
   notes → still PENDING, `approved_by` unchanged, no `APPROVAL_VOIDED`.
c) Same invoice: change an amount → NEEDS_REVIEW, `approved_by` and `approved_at` NULL, audit
   row `APPROVAL_VOIDED` („Pakeisti laukai: …").
Failure: any of the three outcomes differs.

## 16. Edit-form save, header authoritative (0c-3, D-035) — CHANGES STAGING DATA

**Proves:** line-less invoices keep their amounts; a header ≠ lines mismatch is flagged, not
overwritten; lines/categories/allocations persist correctly; one transaction.

Warning 1: only after check 1 passed.

Before/after SQL for the invoice you edit:

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT id, description, quantity, unit_price, amount_excl_vat, vat_rate, amount_incl_vat, category_id, sort_order FROM expense_invoice_lines WHERE invoice_id = <ID> ORDER BY sort_order, id;"
sudo mariadb nordic_bees_erp_staging -e "SELECT a.id, a.invoice_line_id, a.category_id, a.cost_center_id, a.allocated_amount, a.allocated_percent FROM expense_line_allocations a JOIN expense_invoice_lines l ON l.id = a.invoice_line_id WHERE l.invoice_id = <ID>;"
```

a) 167 (`LINES_NOT_FOUND`): open, save → amounts unchanged (5,00 / 0,00 / 5 127,85 before any
   correction from check 13).
b) An invoice with lines: change the header so it no longer equals the line sums → save →
   header kept, `AMOUNT_MISMATCH` in `ocr_flags`.
c) Remove one line → save → that line id is gone from `expense_invoice_lines`.
d) Change a line's category → save → `category_id` updated.
e) A line with an allocation (find one with the allocation query on a few ids) → save → the
   allocation row still exists with the same values.
Failure: amounts zeroed on 167, header overwritten by line sums, a removed line still present, a
lost category or allocation.

## 17. Wrong recipient dismissal (0c-4) — CHANGES STAGING DATA

**Proves:** dismissing a wrong-recipient rejection is audited and restores the rule-based status.

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT id, invoice_number, supplier_id, status, rejected_reason, ocr_flags FROM expense_invoices WHERE status = 'REJECTED' AND rejected_reason LIKE 'Sąskaita ne %' AND JSON_CONTAINS(ocr_flags, JSON_QUOTE('WRONG_RECIPIENT')) ORDER BY id;"
```

Browser: open one → the orange banner „Sistema mano kad sąskaita ne …" → button „✓ Lakštenai"
(shown only when `WRONG_RECIPIENT` is in `ocr_flags`, `InvoiceDetailDialog.razor:65-77`) → snackbar
„Gavėjas patvirtintas".
Expected: status by the rules above (not REJECTED), `WRONG_RECIPIENT` removed from `ocr_flags`,
audit `WRONG_RECIPIENT_DISMISSED` („Gavėjas patvirtintas rankiniu būdu").
If the query returns no rows, the check cannot be done on existing data — note it.

## 18. New uploads with analysis (E0-3, E0-5, E0-7, E0-8 second part, E0-9, 0c-6, 0c-7) — CHANGES STAGING DATA, CALLS AZURE

After each upload:

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT id, invoice_number, invoice_date, supplier_id, pending_supplier_name, pending_supplier_vat, status, amount_incl_vat, ocr_flags, file_id FROM expense_invoices WHERE id > <INV0> ORDER BY id;"
sudo mariadb nordic_bees_erp_staging -e "SELECT id, sha256, byte_size, original_filename, module, entity_type, entity_id FROM files WHERE id > <FILE0> ORDER BY id;"
```

Every new invoice must have `file_id` = a new `files.id`, and that file's `entity_id` = the
invoice id, `module` = `expenses`, `entity_type` = `expense_invoice`.

18.1 **Supplier not guessed (E0-3).** Upload a digital PDF whose supplier has no VAT code on the
document (or a supplier that is not in `business_partners`). Expected: PENDING_SUPPLIER with
`VENDOR_NOT_FOUND`, `supplier_id` NULL — never 336. Expect more „Nežinomas tiekėjas" in general.
18.2 **Future date (E0-5).** A PDF with a future invoice date → NEEDS_REVIEW with `FUTURE_DATE`;
„PATVIRTINTI" is the audited override (`APPROVED` audit row).
18.3 **Digital PDF passes (0c-6), drop path (0c-7).** One of these uploads done by drag & drop in
Chrome and one in Firefox.
18.4 **Duplicate detection (E0-7).** Upload the original PDF of 148 (PRD 0015764, 113,74 €).
Expected: `DUPLICATE_PENDING`, `DUPLICATE` in `ocr_flags`, audit `CREATED` whose details list the
flags („Šaltinis: …, tikslumas: …%, požymiai: …"). (`DUPLICATE_DETECTED` is written only by the
older manual-add path, not by OCR create — so its absence here is expected.) A „1" /
0,00 € document is **not** flagged as a duplicate (if you have one).
18.5 **Same PDF again after upload (E0-8).** (E0-8's "a re-uploaded image is still analysed" no
longer applies: images are refused at selection since C5, check 7.) Upload one of the PDFs from 18.1–18.3 again →
„Failas jau įkeltas"; no new row.
18.6 **Negative amounts (E0-9).** Only if a supplier credit note is at hand: → NEEDS_REVIEW with
`MISSING_MONEY_FIELD`, not duplicate-checked. Intended.
18.7 **Arithmetic gate on supplier assignment (E0-4, Artea case).**

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT e.id, e.invoice_number, e.pending_supplier_name, e.amount_incl_vat, SUM(l.amount_incl_vat) AS lines_gross, e.ocr_flags FROM expense_invoices e JOIN expense_invoice_lines l ON l.invoice_id = e.id WHERE e.status = 'PENDING_SUPPLIER' AND e.amount_incl_vat = 0 GROUP BY e.id, e.invoice_number, e.pending_supplier_name, e.amount_incl_vat, e.ocr_flags HAVING SUM(l.amount_incl_vat) > 0;"
```

Assign a supplier to one of them → expected NEEDS_REVIEW, audit `SUPPLIER_ASSIGNED`. **Note:**
the assignment path uses the **stored** flags (Etapas 0 report, risks); if that row's `ocr_flags`
contains no review flag, PENDING is the documented behaviour — record it as a finding, not as a
regression.
Failure (any 18.x): a supplier guessed (336 or another partner without a VAT match), no flag, a
missing `files` row, or `file_id` NULL on a new invoice.

## 19. Re-OCR on a new invoice (0c-1, 0c-6) — no data change today

a) **Not runnable today** (warning 2): no invoice has both `file_id` and `original_file_path`, so
   „PAKARTOTI OCR" is never offered for a stored file. Confirm on a new invoice from 18.x: no
   button. Record as owner finding.
b) The DUPLICATE_PENDING invoice created in 18.4 (it has a `file_id`): no „PAKARTOTI OCR" button
   (the button is shown only for NEEDS_REVIEW / PENDING / PENDING_SUPPLIER) — 0c-1 "re-OCR is not
   offered in the UI".
The "re-OCR of an old image-derived invoice → Failas nepriimtas" item cannot be run: old
invoices have no stored file (warning 2).

---

## Mapping: report item → check

| Report item | Check |
|---|---|
| E0-1 quarantine totals | 2 |
| E0-2 duplicate dialog | 10, 11, 12 |
| E0-3 supplier 336 | 4 (links, re-OCR refusal), 18.1 |
| E0-4 arithmetic gate | 13, 18.7 |
| E0-5 date gates | 14, 18.2 |
| E0-6 due date | 3 |
| E0-7 duplicate detection | 18.4 |
| E0-8 SHA-256 | 8, 18.5 |
| E0-9 negative amounts | 18.6 |
| 0c-1 re-OCR | 4, 19 ("if the service is reached, the status stays" — not reachable from the UI) |
| 0c-2 manual edit | 15 |
| 0c-3 edit save | 16 |
| 0c-4 wrong recipient | 17 |
| 0c-5 bank import | 6 |
| 0c-6 upload intake | 7, 8, 18.3, 19 (old image re-OCR: not runnable) |
| 0c-7 drag & drop | 9, 18.3 |
| 0c-8 budget | 5 |
| 0c-9 payment dialog due date | 3 |
| 0c-10 CI | 1.1 |

## Names I could not verify from the code

- A log line that proves an Azure DI call happened (check 8) — none found.
- The exact budget-actuals query (check 5) — deliberately not given.
- All column names above come from the model and migration files, not from the live staging
  schema; `DESCRIBE` settles any doubt.

## Go / no-go for production

All must be true:

- [ ] 1.1–1.5: "Build and Deploy" green (Hardcode Check / semgrep result recorded), new
      container, no `Kritinė klaida`, no new pending migration, sentinel `Staging`.
- [ ] 2: the 28 quarantined invoices are out of every total (cash flow, supplier history, budget)
      and still listed.
- [ ] 3, 5, 9: due-date marker, budget actuals, drag & drop behave as described.
- [ ] 4, 19: no re-OCR on paid/quarantined; file-less re-OCR refused cleanly; the owner has
      decided about re-OCR being unreachable for stored files (warning 2).
- [ ] 6: no quarantined invoice among bank candidates; no new payment on a quarantined invoice.
- [ ] 7, 8, 18.5: refusals happen before Azure, no rows created.
- [ ] 10–12: duplicate dialog audited; reject keeps the row; approve refused.
- [ ] 13–17: gates, approval voiding, header-authoritative save and recipient dismissal as
      described, each with its audit row.
- [ ] 18.1–18.4: no guessed supplier; new uploads have `file_id` ↔ `files.entity_id`; duplicates
      quarantined.
- [ ] Any "finding, not regression" note (13 status, 18.7 stored flags) reviewed and accepted by
      the owner.
- [ ] Production prerequisites from D-030 still hold (prod `files` table + `file_id`, sentinel
      `/var/lib/nordicbees/prod/.nordicbees-storage` = `Production`, fresh backup).
- [ ] Warning 1 understood: nobody edits line-less invoices on production before the production
      deploy.

Any unchecked box = **no-go**; report the check number and the SQL output.
