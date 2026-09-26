# Staging checks — Etapas 1 (gates, locale numbers, re-OCR by file_id)

Written 2026-09-26 by Claude Code (S8) for the owner. Nothing here has been run.
Code under test: `main` `e5cfb95` (Etapas 1 S1–S7) + the version bump `b3e19ca` (**0.17.92**).
Run it on **staging** (`nordic_bees_erp_staging`, a production clone — D-029) after the owner's push.
Per **D-037** this run closes Etapas 1 **on staging only**: there is no production deploy until Etapai 1–4
are done and verified on staging (production stays v0.17.91).

Staging has the `unit_price decimal(18,6)` DDL applied (S3 report). Uploads and re-OCR call Azure DI with the
production key (D-029).

## How to read this document

- SQL is `SELECT`-only unless a check says **CHANGES STAGING DATA**; it is run as
  `sudo mariadb nordic_bees_erp_staging -e "…"` on `lakstena-dev`. The SQL uses no double quotes, `!`, `$` or
  backticks, so it pastes safely inside `-e "…"`.
- `ocr_flags` is a JSON array stored as text. Test a flag with `JSON_CONTAINS(ocr_flags, JSON_QUOTE('FLAG'))`.
- Table/column names come from the model and migrations (`expense_invoices`, `expense_invoice_lines`,
  `expense_invoice_audit`, `expense_line_allocations`, `files`, `__EFMigrationsHistory`;
  `pending_supplier_vat`, `pending_supplier_country_code`, `pending_supplier_bank_account`, `file_id`,
  `original_filename`). If a query fails with "Unknown column", run `DESCRIBE <table>` and tell Claude.
- `<INV0>`, `<AUD0>`, `<FILE0>`, `<ALLOC0>` are the baseline ids from check 1.5.
- Documents are named by **file name only** (the local digital PDFs of the corpus: `wwwroot/uploads/invoices/2026/07/`
  and `Docs/Invoice pvz/`). "Expected" flags for them come from the raw Azure responses of 2026-09-26 (the corpus,
  outside git). **A fresh Azure call can differ slightly** — if a flag differs, record what appeared; that is data,
  not automatically a failure.
- Status rules: `WRONG_RECIPIENT` → REJECTED; no supplier → PENDING_SUPPLIER; any **review** flag → NEEDS_REVIEW;
  else PENDING. Review: MISSING_AMOUNT, AMOUNT_MISMATCH, LOW_CONFIDENCE, ZERO_VAT, MISSING_INV_NUMBER,
  AMOUNT_ARITHMETIC_MISMATCH, MISSING_MONEY_FIELD, FUTURE_DATE, STALE_DATE, MISSING_INV_DATE, TOTALS_OUT_OF_RANGE,
  VAT_RATE_NOT_ALLOWED, NUMBER_MISREAD, NUMBER_AMBIGUOUS, and — **only while there is no supplier** — INVALID_IBAN,
  INVALID_VAT_FORMAT (D-039 item 2). Everything else is information: it never holds an invoice in review.

### Flag → chip label (what you should see)

| Flag | Kind | Chip / message |
|---|---|---|
| AMOUNT_ARITHMETIC_MISMATCH | review | Sumos nesutampa (be PVM + PVM ≠ su PVM) |
| AMOUNT_MISMATCH | review | Sumos nesutampa |
| LINE_SUM_ROUNDING | information | Eilučių suma skiriasi keliais centais |
| MISSING_MONEY_FIELD | review | Trūksta sumos duomenų |
| TOTALS_OUT_OF_RANGE | review | Sumos neįtikėtinai didelės |
| LINE_AMOUNT_IMPLAUSIBLE | information | Eilutė: kiekis × kaina ≠ suma |
| INVALID_IBAN | review without supplier, information with | Neteisingas IBAN |
| INVALID_VAT_FORMAT | review without supplier, information with | Neteisingas PVM kodo formatas |
| VAT_FORMAT_UNCHECKED | information | PVM kodo formatas netikrintas |
| VAT_COUNTRY_MISMATCH | information | PVM kodo šalis nesutampa su tiekėjo šalimi |
| VAT_RATE_UNCHECKED | information | PVM tarifas netikrintas |
| VAT_RATE_NOT_ALLOWED | review | PVM tarifas negalimas šaliai ir datai |
| NUMBER_MISREAD | review | Skaičius nesutampa su dokumento tekstu |
| NUMBER_AMBIGUOUS | review | Dviprasmiškas skaičius |
| LINE_LARGE_QUANTITY | information | Didelis kiekis (> 1000) — eilutė palikta |
| LINE_DUPLICATE_DESCRIPTION | information | Pasikartojantis aprašymas — eilutė palikta |

The detail dialog also has a „Sumų patikra" block (view mode) listing the rule messages, incl. „nepatikrinta: …"
and, for NUMBER_* flags, one line per flagged number: „Skaičius nesutampa su dokumento tekstu — dokumento N eilutė,
vieneto kaina: Azure perskaitė 115, dokumente atspausdinta „0,115“ (galimas skaitymas: 0,115)".

## Warnings (read before starting)

1. **VAT_RATE_NOT_ALLOWED cannot fire.** Every row of `VatRateTable` is UNCONFIRMED (no accountant confirmation yet),
   so the rate gate produces only the information flag VAT_RATE_UNCHECKED. Check 15 records that and nothing more.
2. **TOTALS_OUT_OF_RANGE cannot be produced on staging** — the amount columns are `decimal(12,2)`; the flag needs a
   value beyond ~7.9e26. It is covered by automated tests only.
3. **LINE_LARGE_QUANTITY** needs a document whose lines exceed the header **and** that has a quantity > 1000. No
   local PDF has both (Eurovertis has three quantities > 1000 but its lines equal the header). It is exercised only
   if the owner has ASF0021438 (check 4 optional row); otherwise it stays covered by automated tests.
4. **Every upload and every re-OCR calls Azure** (~13 calls in this document). Data created here stays on staging
   (re-cloned from prod when needed, D-029).
5. Duplicate detection can fire on re-uploads of a PDF that was already uploaded to staging (`Failas jau įkeltas` /
   DUPLICATE_PENDING) — record and skip that file.
6. **Bank import: never save.** Nothing in this document saves one.

---

# Part A — deploy (no data change)

## 1. Deploy check

**Proves:** staging runs `b3e19ca`, started cleanly, storage mounted, schema as expected.

1.1 **CI.** "Build and Deploy" for the pushed commit is green. (The "Hardcode Check" run is separate; record what it
shows.) Failure: red → staging still runs the old image; stop.

1.2 **Container:**

```bash
sudo docker ps --filter name=nordicbees_staging --format '{{.Names}} {{.Image}} {{.CreatedAt}} {{.Status}}'
sudo docker inspect -f '{{.Image}} {{.RestartCount}}' nordicbees_staging
sudo docker image inspect -f '{{.Id}} {{.Created}}' nordicbees-erp:staging
```

Expected: `CreatedAt` after the push, `Up …`, RestartCount `0`, image ids equal. The UI footer shows **0.17.92**.

1.3 **Startup log:**

```bash
sudo docker logs nordicbees_staging 2>&1 | grep -E 'Kritinė klaida|Pending EF migrations|Migration warning|Unhandled exception|fail:'
```

Expected: no `Kritinė klaida`, no `Unhandled exception`. Since v0.17.91 the only migration is
`20260926131956_AlterExpenseInvoiceLineUnitPricePrecision` (already applied by the owner); a pending-migrations
warning naming anything else is a failure.

1.4 **Sentinel and schema:**

```bash
sudo cat /var/lib/nordicbees/staging/.nordicbees-storage
sudo mariadb nordic_bees_erp_staging -e "SELECT COLUMN_TYPE, IS_NULLABLE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'expense_invoice_lines' AND COLUMN_NAME = 'unit_price';"
sudo mariadb nordic_bees_erp_staging -e "SELECT MigrationId FROM __EFMigrationsHistory WHERE MigrationId LIKE '20260926131956%';"
```

Expected: `Staging`; `decimal(18,6)` / `YES`; one history row.

1.5 **Baseline ids** — write them down:

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT (SELECT MAX(id) FROM expense_invoices) AS INV0, (SELECT MAX(id) FROM expense_invoice_audit) AS AUD0, (SELECT MAX(id) FROM files) AS FILE0, (SELECT MAX(id) FROM expense_line_allocations) AS ALLOC0;"
```

## 2. Read-only reconnaissance

**Proves nothing by itself** — it tells you which later checks have data.

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT id, invoice_number, status, file_id, original_filename, ocr_flags FROM expense_invoices WHERE id = 376;"
sudo mariadb nordic_bees_erp_staging -e "SELECT id, invoice_number, status, ocr_flags FROM expense_invoices WHERE JSON_CONTAINS(ocr_flags, JSON_QUOTE('OWN_COMPANY')) ORDER BY id;"
sudo mariadb nordic_bees_erp_staging -e "SELECT e.id, e.status, e.file_id, COUNT(a.id) AS allocations FROM expense_invoices e JOIN expense_invoice_lines l ON l.invoice_id = e.id JOIN expense_line_allocations a ON a.invoice_line_id = l.id WHERE e.file_id IS NOT NULL GROUP BY e.id, e.status, e.file_id;"
sudo mariadb nordic_bees_erp_staging -e "SELECT id, invoice_number, status, pending_supplier_vat, pending_supplier_country_code FROM expense_invoices WHERE status = 'PENDING_SUPPLIER' AND pending_supplier_vat IS NOT NULL AND pending_supplier_vat <> '' ORDER BY id DESC LIMIT 10;"
```

Record: 376's status and `file_id` (re-OCR case, check 10); whether any `OWN_COMPANY` row exists (check 12 — 370's
flag was already dropped by the old save code); whether any stored-file invoice has allocations (check 11);
one PENDING_SUPPLIER invoice with a VAT code for the manual-data checks (check 14) — call its id `<PS>`.

---

# Part B — uploads with analysis (CHANGES STAGING DATA, CALLS AZURE)

After **each** upload run:

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT id, invoice_number, invoice_date, supplier_id, status, amount_excl_vat, vat_amount, amount_incl_vat, ocr_flags, file_id FROM expense_invoices WHERE id > <INV0> ORDER BY id;"
sudo mariadb nordic_bees_erp_staging -e "SELECT id, invoice_id, action, action_details, old_status, new_status, performed_at FROM expense_invoice_audit WHERE id > <AUD0> ORDER BY id;"
```

The `CREATED` audit row lists the flags („požymiai: …"). Open each new invoice's detail view and read the chips and
the „Sumų patikra" block. The status depends on whether the supplier matches an existing partner (PENDING_SUPPLIER
when not) — **compare flags, not only status.** Every new invoice must have `file_id` set (re-OCR needs it).

Upload order matters for the follow-up checks: keep U1 (re-OCR and edit follow-ups: checks 9, 12a) and the clean
invoice U6 (checks 12–14); U2–U5 are create-path checks only.

## 3. U1 — fuel invoice `20260707_235156_Eurovertis.pdf` (the real NUMBER_MISREAD case)

**Proves:** locale detection on the create path (S7b), on the document where Azure reads „0,115" as 115.

Header 473,84 / 99,51 / 573,35 €; 4 lines: three unit prices printed „0,115", „0,118", „0,006" (Azure: 115, 118, 6)
and one „11,990" (Azure: 11990); three quantities > 1000; lines equal the header.

| Expect | Value |
|---|---|
| Flags | **NUMBER_MISREAD**, **NUMBER_AMBIGUOUS**, LINE_AMOUNT_IMPLAUSIBLE (information: quantity × the misread price ≠ line), VAT_RATE_UNCHECKED (information) |
| Not expected | LINE_LARGE_QUANTITY (lines equal the header), AMOUNT_MISMATCH |
| Status | NEEDS_REVIEW with a matched supplier, else PENDING_SUPPLIER |
| Detail view | „Sumų patikra": three „Skaičius nesutampa …" lines (candidates 0,115 / 0,118 / 0,006) and one „Dviprasmiškas skaičius …" (galimi skaitymai: 11,99 arba 11990). Values in the lines table are still Azure's (115 …) — **nothing is replaced**. |
| Line count | 4 |

Failure: no NUMBER_* flag; a value silently changed to the candidate; an invoice with NUMBER_MISREAD in status
PENDING (with a supplier).

## 4. U2 — `20260707_224213_Rabenas.pdf` (BR-CO-10 + kept duplicate lines)

**Proves:** BR-CO-10 > 0,05 € → AMOUNT_MISMATCH; reconcile no longer deletes (D-038 Q7, S7c).

Header net 2 060,33; the lines sum to 2 385,61 (10 Azure items; two of them have no amount — both become zero-amount lines and are dropped).

| Expect | Value |
|---|---|
| Flags | **AMOUNT_MISMATCH**, **LINE_DUPLICATE_DESCRIPTION** (information), VAT_RATE_UNCHECKED |
| Status | NEEDS_REVIEW / PENDING_SUPPLIER |
| Lines stored | 8 (the zero-amount line is still dropped; the repeated-description lines are **kept** — before S7c three of them were deleted silently) |
| Detail view | „Sumų patikra" shows the BR-CO-10 message |

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT COUNT(*) AS lines, SUM(amount_excl_vat) AS lines_net FROM expense_invoice_lines WHERE invoice_id = <ID>;"
```

Optional row — **ASF0021438**, if the owner has the PDF (the invoice PLAN §3.1 is built on): expect
**NUMBER_MISREAD** (quantity „3 888,000" read as 3), **NUMBER_AMBIGUOUS** („9,000" read as 9000),
**LINE_LARGE_QUANTITY**, AMOUNT_MISMATCH, and **both lines stored** (before S7c the 9000-quantity line was deleted).
This is the only document known to exercise LINE_LARGE_QUANTITY.

## 5. U3 — `20260707_235326_EGO transport.pdf` and `20260707_234744_UTA PL.pdf` (line amounts in the gross column)

**Proves:** BR-CO-10 catches lines taken from the „Suma su PVM" column (D-023) on two more real documents; a PL
supplier with 23 %.

| File | Header net / VAT / gross | Lines sum | Expect |
|---|---|---|---|
| EGO transport | 430,00 / 90,30 / 520,30 | 520,30 | AMOUNT_MISMATCH (diff 90,30), LINE_AMOUNT_IMPLAUSIBLE (1 × 430 against a 520,30 line), VAT_RATE_UNCHECKED |
| UTA PL | 268,30 / 61,70 / 330,00 (23 %) | 330,00 | AMOUNT_MISMATCH (diff 61,70), LINE_AMOUNT_IMPLAUSIBLE, VAT_RATE_UNCHECKED, **VAT_FORMAT_UNCHECKED** (Azure returns the VAT code on two lines and no address country, so the country is unknown); no INVALID_VAT_FORMAT |

Failure: no AMOUNT_MISMATCH; INVALID_VAT_FORMAT on UTA PL (VAT_FORMAT_UNCHECKED there is expected).

## 6. U4 — `20260708_004344_LD.pdf` (missing net and VAT)

**Proves:** MISSING_MONEY_FIELD (Azure returned only the gross total, 158,49).

Expect: **MISSING_MONEY_FIELD** („Trūksta sumos duomenų"), no AMOUNT_ARITHMETIC_MISMATCH; **ZERO_VAT** (no rate, no net); status
NEEDS_REVIEW / PENDING_SUPPLIER. Note in the "Sumų patikra" block: „nepatikrinta: BR-CO-…" for rules that could not
run — never shown as passed.
Failure: no MISSING_MONEY_FIELD; an arithmetic flag computed from a net that was never read.

## 7. U5 — `Docs/Invoice pvz/Invoice (1231).PDF` (old, non-LT supplier, 0 % VAT)

**Proves:** STALE_DATE; a non-LT VAT code is „unchecked", not invalid.

Invoice date 2015-05-04, supplier VAT code with prefix NL, VAT 0 %, 256,00 €.
Expect: **STALE_DATE**, **VAT_FORMAT_UNCHECKED** (information; NL is outside the format table), **ZERO_VAT**
(0 %). **No** VAT_RATE_* flag (0 % is never checked; unknown country / date before 2025). No INVALID_VAT_FORMAT.
Failure: no STALE_DATE; INVALID_VAT_FORMAT on the NL code.

## 8. U6 — a clean document (the "must pass" case): `20260707_224024_Taurobilis.pdf` (or `…_224106_Gerunda.pdf` / `…_224355_Venipak.pdf`)

**Proves:** a consistent LT invoice is not stopped by any Etapas 1 gate; the only new chip is information.

Taurobilis: 546,46 + 114,76 = 661,22; 10 lines summing to 546,46. Expect: **no** review flag from Etapas 1; flags
limited to information (VAT_RATE_UNCHECKED, possibly LINE_AMOUNT_IMPLAUSIBLE where a printed price disagrees);
status PENDING with a matched supplier (PENDING_SUPPLIER without).
Failure: any Etapas 1 **review** flag on this consistent document. Keep this invoice as the scratch for checks 12–14.

---

# Part C — re-OCR by file_id (CHANGES STAGING DATA, CALLS AZURE)

## 9. Re-OCR of U1 (path: re-OCR)

**Proves:** the button now appears for stored-file invoices (S3a); the same flags come out on re-OCR as on create
(path parity, S7b); the stored filename is kept (S3b).

Before: `SELECT id, original_filename, ocr_flags, status FROM expense_invoices WHERE id = <U1>;`
Browser: U1 detail → **„PAKARTOTI OCR"** (shown only for NEEDS_REVIEW / PENDING / PENDING_SUPPLIER **with a
`file_id`**) → the review dialog → save without editing.
Expect: NUMBER_MISREAD, NUMBER_AMBIGUOUS, LINE_AMOUNT_IMPLAUSIBLE as after the upload (same set); `original_filename`
unchanged (not NULL, not empty); one new audit row **`OCR_RETRIED`**; the invoice is still one row (no new invoice).
Note: the review dialog exposes only quantity (Kiekis), line net (Be PVM), VAT % and the header totals — **not the
unit price**, and U1's misreads are all unit prices, so on U1 the NUMBER_* flags are expected to **stay** after
re-OCR (a flag clears on the OCR paths only when every flagged field is changed by hand; see the ASF0021438 row in
check 4 for a quantity case, where correcting the two quantities before saving clears both flags).
Failure: no button; filename lost; a duplicate invoice row; different flags than on create.

## 10. Re-OCR of invoice 376 (Artea `PL99810705.pdf`, already on staging)

**Proves:** re-OCR of an invoice created before Etapas 1 — old flags are replaced by the new gates.

Record 376's flags/status/`original_filename` first (check 2). Re-OCR as in 9 (if 376 is not NEEDS_REVIEW / PENDING /
PENDING_SUPPLIER there is no button — record its status; then this check is not runnable).
Expect: `OCR_RETRIED` audit row; `original_filename` unchanged; the flags are recomputed by the current code — the
Artea document was the header-total-0 case (gross 0, lines > 0): MISSING_MONEY_FIELD is the expected new chip if the
header net or gross is still 0; VAT_RATE_UNCHECKED information. Record all changes.
Failure: no button on a NEEDS_REVIEW / PENDING invoice with `file_id`; filename lost; the old flags kept unchanged.

## 11. Re-OCR with allocations (confirmation dialog)

**Proves:** re-OCR warns before deleting allocations (D-038 Q8, S3d).

Only if check 2 found a stored-file invoice with `allocations > 0` (or you allocate a line of a new upload in the
expenses UI first): „PAKARTOTI OCR" → message box **„Pakartotinis OCR ištrins paskirstymus"** („Šios sąskaitos
eilutės turi paskirstymų: N …") with **„Tęsti ir ištrinti" / „Atšaukti"**. Cancel → nothing changes. Confirm →
`OCR_RETRIED` audit row ends with „pašalinta paskirstymų: N (eilutės: …)" and the allocation rows are gone:

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT COUNT(*) FROM expense_line_allocations a JOIN expense_invoice_lines l ON l.id = a.invoice_line_id WHERE l.invoice_id = <ID>;"
```

If no such invoice exists and creating one is not worth it: **skip — covered by `ExpenseOcrPersistTests` (automated),
not blocking.**
Failure: allocations deleted without the message box, or the audit row without the removed count.

---

# Part D — edit path and manual cases (CHANGES STAGING DATA, no Azure)

The gates below have no real document that triggers them. Each row gives the smallest change that does. Use the U6
invoice (`<U6>`) unless stated. Edit form = the invoice detail dialog in edit mode; upload dialog review phase = the
same fields before saving (create / re-OCR path).

## 12. Edit-path carry-over (S2a)

**Proves:** the edit form no longer drops flags it does not own; NUMBER_* survive an edit; PATVIRTINTI clears the review.

a) **NUMBER_* carried.** U1 detail → edit → change only the notes → save. Expect: NUMBER_MISREAD and
   NUMBER_AMBIGUOUS still in `ocr_flags`; status NEEDS_REVIEW (PENDING_SUPPLIER if U1 has no supplier — then compare flags only); audit `EDITED`.
   Then **„PATVIRTINTI"** → status PENDING, `approved_by` set, audit `APPROVED`; the flags **stay** in `ocr_flags`
   (they are a record). (Correcting a value in the edit form does *not* clear them — only re-OCR/the upload dialog
   recompute them; documented in D-041.)
b) **OWN_COMPANY survives.** Take a row from check 2's OWN_COMPANY query, edit → save. Expect `OWN_COMPANY` still
   present (the 370 finding of the Etapas 0 run). If the query returned no row, **CHANGES STAGING DATA** — add the
   flag to a scratch invoice by hand, then edit-save it:

```bash
sudo mariadb nordic_bees_erp_staging -e "UPDATE expense_invoices SET ocr_flags = JSON_ARRAY('OWN_COMPANY') WHERE id = <U6>;"
```

   (this overwrites `<U6>`'s other flags — fine for a scratch invoice; the edit recomputes the owned ones.)
c) Failure: any of NUMBER_*, OWN_COMPANY, INVALID_VAT_RATE missing after the edit; NUMBER_* cleared by the edit.

## 13. Arithmetic gates on three paths (S4)

**Proves:** BR-CO-15 is exact (0,01 flags) and BR-CO-10 has two bands, on create and re-OCR (BR-CO-15) and on create and edit (BR-CO-10 bands).

| Path | How | Expect |
|---|---|---|
| Create | **new upload of `20260707_224106_Gerunda.pdf`** (a second clean document; re-uploading `<U6>`'s file is refused as a duplicate); in the review phase change **„PVM suma"** by +0,01 → save | **AMOUNT_ARITHMETIC_MISMATCH**, status NEEDS_REVIEW (with supplier) |
| Edit | **not producible**: in the edit form „PVM suma“ is read-only and net / gross are derived from each other, so BR-CO-15 cannot be violated there | covered by automated tests only (`ExpenseValidationGateTests`, path parity) |
| Re-OCR | `<U6>` „PAKARTOTI OCR“ → review phase: „PVM suma“ +0,01 → save | AMOUNT_ARITHMETIC_MISMATCH; then re-OCR again without edits → flag gone |
| BR-CO-10 band | `<U6>` edit form: change header „Be PVM“ (gross follows) so it differs from the line sum by **0,03 €** | **LINE_SUM_ROUNDING** (information), status **not** held (PENDING) |
| BR-CO-10 band | same, difference **0,06 €** | **AMOUNT_MISMATCH**, NEEDS_REVIEW; restore → gone |

Failure: 0,01 not flagged on create / re-OCR; 0,03 holding the invoice; 0,06 not flagged; a path that behaves
differently. (BR-CO-10 on the create path can also be seen with U2 / U3.)

## 14. IBAN and VAT-code gates + supplier-create prefill (S5) — on `<PS>` (a PENDING_SUPPLIER invoice with a VAT code, from check 2)

**Proves:** malformed codes are flagged, stop the invoice only while it has no supplier, and are never copied into a new supplier.

| Step | Do | Expect |
|---|---|---|
| a | edit `<PS>`, no change → save | no new gates fire on a correct code; note its flags |
| b | **CHANGES:** `UPDATE expense_invoices SET pending_supplier_vat = 'LT123' WHERE id = <PS>;` → edit → save | **INVALID_VAT_FORMAT**, status stays PENDING_SUPPLIER |
| c | **CHANGES:** `UPDATE expense_invoices SET pending_supplier_bank_account = 'LT000000000000000000' WHERE id = <PS>;` → edit → save | **INVALID_IBAN** (Neteisingas IBAN), PENDING_SUPPLIER |
| d | `<PS>` → **„Sukurti tiekėją"** | IBAN and VAT fields are **empty**; alerts „Sąskaitoje nurodytas IBAN neteisingas — įveskite ranka" and „Sąskaitoje nurodytas PVM kodas neteisingas — įveskite ranka" |
| e | Create the supplier / assign one by hand (a real partner) | flags stay stored (information now); status **PENDING** if no other review flag remains (D-039 item 2) |
| f | **CHANGES:** restore the original `pending_supplier_vat`, set `pending_supplier_country_code = 'LV'` (VAT code has the LT prefix), on a PENDING_SUPPLIER invoice → edit → save | **VAT_COUNTRY_MISMATCH** (information) |

`<PS>`'s original values are in check 2's output — write them down before step b and restore them after step f.
VAT_FORMAT_UNCHECKED has a real case: U5. (The upload dialog shows the VAT code read-only, so a malformed code
cannot be typed there; the `UPDATE` above is the way. Step f assumes `<PS>` has a well-formed LT-prefixed code.)
Failure: the malformed VAT code reaching VIES / matching a supplier; an invalid IBAN/VAT prefilled in the supplier
dialog; an invalid code holding a supplier-assigned invoice in NEEDS_REVIEW.

## 15. VAT-rate gate

**Proves:** every rate is UNCHECKED today and nothing is rejected; ULAK exempt.

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('VAT_RATE_UNCHECKED'))) AS unchecked, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('VAT_RATE_NOT_ALLOWED'))) AS not_allowed FROM expense_invoices WHERE id > <INV0>;"
```

Expect: `unchecked` ≈ every new invoice with a non-zero rate (U1–U4, U6); **`not_allowed` = 0** — by design (warning 1).
Any `not_allowed` > 0 is a failure. 0 % invoices (U5) carry neither flag.

---

# Part E — Etapas 0 checks that were not done (carry-over)

State at 2026-09-26 (STATE.md): checks 1, 3, 4, 8 and the 370 edit PASSED; 9 FAILED; 2, 5, 6, 7 and 10–19 not done.

| Etapas 0 check | State | Decision |
|---|---|---|
| 2 — cash flow / supplier history (quarantine out of totals) | dialogs **unreachable from the UI** (Q11 not done) | **known, not blocking Etapas 1** — the SQL half of Etapas 0 check 2 may still be run |
| 5 — budget actuals | same (`ExpenseBudgetDialog` unreachable) | **known, not blocking Etapas 1** |
| 6 — bank import candidates (stop before saving) | not run | **check** (no data change; Etapas 0 doc check 6) |
| 7 — JPG / scanned PDF refused before Azure (`Ratukų kronšteinų centras`, `Sanitex`, `20260707_234528_ .pdf` are scans) | not run | **check** (no data change; Etapas 0 doc check 7; `MAX(id)` unchanged afterwards) |
| 9 — drag & drop | **FAILED** (known: dropzone listeners lost after ✕; FROZEN §3, needs owner permission) | **known, not blocking Etapas 1**; blocks the production deploy |
| 10–19 — duplicate dialog, arithmetic/date-gate edits, approval voiding, edit-save, wrong recipient, uploads | not run | **check** where cheap: 10–12, 15–17 (Etapas 0 doc). **Expectations changed:** arithmetic flags now come from BR-CO-15 (exact, 0,01) and BR-CO-10 (bands) — use this document's check 13 instead of Etapas 0 check 13. |

---

# Part F — noise check (after the run; no data change)

**Proves:** the information flags are not drowning the review flags.

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT COUNT(*) AS new_invoices, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('VAT_RATE_UNCHECKED'))) AS vat_rate_unchecked, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('VAT_FORMAT_UNCHECKED'))) AS vat_format_unchecked, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('VAT_COUNTRY_MISMATCH'))) AS vat_country_mismatch, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('LINE_AMOUNT_IMPLAUSIBLE'))) AS line_implausible, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('LINE_SUM_ROUNDING'))) AS line_sum_rounding, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('LINE_LARGE_QUANTITY'))) AS line_large_qty, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('LINE_DUPLICATE_DESCRIPTION'))) AS line_dup_desc FROM expense_invoices WHERE id > <INV0>;"
sudo mariadb nordic_bees_erp_staging -e "SELECT COUNT(*) AS new_invoices, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('NUMBER_MISREAD'))) AS number_misread, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('NUMBER_AMBIGUOUS'))) AS number_ambiguous, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('AMOUNT_MISMATCH'))) AS amount_mismatch, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('AMOUNT_ARITHMETIC_MISMATCH'))) AS arithmetic, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('MISSING_MONEY_FIELD'))) AS missing_money FROM expense_invoices WHERE id > <INV0>;"
sudo mariadb nordic_bees_erp_staging -e "SELECT status, COUNT(*) FROM expense_invoices WHERE id > <INV0> GROUP BY status;"
```

Read-out for the owner: with all VAT rows unconfirmed, VAT_RATE_UNCHECKED will be on ≈ every new invoice — expected,
information only (grey chip). The question is whether the **review** counts (second query) are explainable by the
documents (U1 → NUMBER_*, U2/U3 → AMOUNT_MISMATCH, U4 → MISSING_MONEY_FIELD). Unexplained review flags on U6 = a
false positive to report.

---

# Go / no-go for closing Etapas 1 on staging

All must be true:

- [ ] 1.1–1.5: green CI, new container 0.17.92, no `Kritinė klaida`, no other pending migration, sentinel `Staging`,
      `unit_price` = `decimal(18,6)`.
- [ ] 3: Eurovertis → NUMBER_MISREAD + NUMBER_AMBIGUOUS, candidates shown, values untouched.
- [ ] 4–6: Rabenas → AMOUNT_MISMATCH + LINE_DUPLICATE_DESCRIPTION with all 8 lines stored; EGO / UTA PL →
      AMOUNT_MISMATCH; LD → MISSING_MONEY_FIELD.
- [ ] 7–8: STALE_DATE + VAT_FORMAT_UNCHECKED on the 2015 invoice; the clean invoice has no review flag.
- [ ] 9–10: re-OCR by `file_id` works (button, filename kept, `OCR_RETRIED`, same flags as create); allocation
      warning either checked (11) or skipped with the reason recorded.
- [ ] 12: NUMBER_* and OWN_COMPANY survive the edit form; PATVIRTINTI clears the review.
- [ ] 13: BR-CO-15 (0,01) and both BR-CO-10 bands behave the same on create, edit and re-OCR.
- [ ] 14: invalid VAT/IBAN flagged, hold only without a supplier, never prefilled into a new supplier.
- [ ] 15: `VAT_RATE_NOT_ALLOWED` = 0 on all new invoices.
- [ ] Part E: checks 6, 7 and the cheap 10–19 items done or explicitly deferred by the owner; 2, 5, 9 recorded as known.
- [ ] Part F: no unexplained review flag; the owner accepts the VAT_RATE_UNCHECKED volume.
- [ ] Every "record and decide" note above reviewed by the owner.

Any unchecked box = **no-go** for closing Etapas 1; report the check number and the SQL output.
Cannot be exercised on staging (accepted, automated tests only): TOTALS_OUT_OF_RANGE, VAT_RATE_NOT_ALLOWED (all rows
UNCONFIRMED), LINE_LARGE_QUANTITY unless ASF0021438 is available.

## Must be finished before the Etapai 1–4 production deploy (D-037)

- **VAT rates confirmed** by the accountant against EC TEDB (rows are UNCONFIRMED; how-to in the S5–S6 report /
  `VatRateTable.cs` header); until then the gate stops nothing. **EE**: the 22 → 24 % change date must be known and
  the row split first. RO has two rows — confirm each.
- **LI partners** (11 partners with country „LI", suspected master-data errors, D-039) — decide; they are outside
  the rate and format tables.
- **Drag & drop** (Etapas 0 check 9, FAILED; FROZEN §3 change needs owner permission).
- **Orphan dialogs** (budget / cash flow / supplier history, Q11) wired into the UI — otherwise the Etapas 0 quarantine
  and D-036 budget changes are invisible.
- **Flaky test** `CreditNoteServiceTests.UpdateCreditNoteAsync_NonExistentInvoiceLineId_ThrowsAndLeavesDatabaseUnchanged`.
- **Production data cleanup with the accountant** (`analysis/PROD-DATA-FINDINGS-2026-09-25.md` §6, Q-010).
- Production DDL at the final deploy: the `unit_price` `ALTER` + history row (S3 report), a fresh backup, sentinel
  `Production`.
- Etapas 2–4 verified on staging.

## Mapping: what each check covers

| Check | Covers |
|---|---|
| 1, 2 | deploy, baseline, reconnaissance |
| 3 | NUMBER_MISREAD, NUMBER_AMBIGUOUS, LINE_AMOUNT_IMPLAUSIBLE, detail-view candidates |
| 4 | AMOUNT_MISMATCH (BR-CO-10), LINE_DUPLICATE_DESCRIPTION, LINE_LARGE_QUANTITY (optional ASF0021438), kept lines |
| 5 | BR-CO-10 on gross-column lines, PL supplier |
| 6 | MISSING_MONEY_FIELD |
| 7 | STALE_DATE, VAT_FORMAT_UNCHECKED |
| 8 | clean pass, VAT_RATE_UNCHECKED |
| 9, 10, 11 | re-OCR by file_id, filename, allocation warning, path parity |
| 12 | edit-path carry-over (NUMBER_*, OWN_COMPANY), PATVIRTINTI |
| 13 | BR-CO-15 exact, BR-CO-10 bands, three paths |
| 14 | INVALID_VAT_FORMAT, INVALID_IBAN, VAT_COUNTRY_MISMATCH, supplier-create prefill, Q5 |
| 15 | VAT_RATE_UNCHECKED / VAT_RATE_NOT_ALLOWED |
| Part E | Etapas 0 leftovers |
| Part F | noise |

**Checks that CHANGE staging data:** 3–8 (uploads), 9–10 (re-OCR), 11 (only if an allocation is added or confirmed away), 12 (the edit; 12b's `UPDATE` only if no OWN_COMPANY row exists), 13, 14 (manual `UPDATE`s in b, c and f), and the Etapas 0 items 10–12 and 15–17 of Part E.
**No data change:** 1, 2, 15, Part F, and the Part E items 6 and 7.
