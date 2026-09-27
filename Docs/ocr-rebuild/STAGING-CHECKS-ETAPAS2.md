# Staging checks — Etapas 2 (supplier recognition, gate 3, known IBANs, aliases)

Written 2026-09-27 by Claude Code (S7) for the owner. **Nothing here has been run on staging.** The DDL of S4/S5 was run
by Claude on the two **dev** databases only (owner exception, `.opencode/reports/etapas2-s4-*.md`, `-s5-*.md`).
Code under test: `main` at the commit that contains this file (S1 `fe7e72d` … S6 `9feddc2`; the SHAs are in `STATE.md`).
No version bump was made (`bump-version.sh` was out of scope for the autonomous run): the owner bumps and pushes before
staging can run this. Staging is `nordic_bees_erp_staging` (a production clone, D-029).
Per **D-037** this run closes Etapas 2 **on staging only**; production stays v0.17.91 until Etapai 1–4 are done.

Order of the document (D-045): **1** owner DDL → **2** deploy check → **3** master-data cleanup (owner + accountant) →
**4** clean start (delete all expense invoices, re-upload, measure) → **5** rule checks → **6** go / no-go.

## How to read this document

- SQL is `SELECT`-only unless a step says **CHANGES STAGING DATA**; it is run as
  `sudo mariadb nordic_bees_erp_staging -e "…"` on `lakstena-dev`. Queries use no double quotes, `!`, `$` or backticks, so
  they paste safely inside `-e "…"`.
- `ocr_flags` is a JSON array stored as text: test a flag with `JSON_CONTAINS(ocr_flags, JSON_QUOTE('FLAG'))`.
- Table and column names come from the model and migrations. If a query fails with "Unknown column", run `DESCRIBE <table>`
  and tell Claude.
- **Untested on MariaDB:** the recursive-CTE backfill (1.4) and the `JSON_CONTAINS` queries were validated on the dev
  MySQL 8.0.46 only. Staging is MariaDB (D-030). If a statement is rejected, stop and send Claude the error text.
- Statuses: `WRONG_RECIPIENT` → REJECTED; no supplier → PENDING_SUPPLIER; any **review** flag → NEEDS_REVIEW; else PENDING.
  New in Etapas 2: **`SUPPLIER_NEW_IBAN` is a review flag even with a supplier** (it is not covered by the D-039 item 2
  exemption that turns INVALID_IBAN / INVALID_VAT_FORMAT into information once a supplier exists).
  `VENDOR_AMBIGUOUS` and `VENDOR_SUGGESTED` are information (the invoice is PENDING_SUPPLIER anyway).

### New flags and messages

| Flag | Kind | Chip / message |
|---|---|---|
| VENDOR_AMBIGUOUS | information | Keli galimi tiekėjai |
| VENDOR_SUGGESTED | information | Siūlomas kitas tiekėjas |
| SUPPLIER_NEW_IBAN | **review** | Naujas tiekėjo IBAN |

New audit actions: `SUPPLIER_MATCHED` (create and re-OCR; `outcome=…; tier=…; reason=…; candidates=…; supplier=…`),
`SUPPLIER_CHANGED`, `SUPPLIER_IBAN_ADDED` (masked IBAN), plus the existing `SUPPLIER_ASSIGNED`, `SUPPLIER_AUTO_ASSIGNED`,
`APPROVAL_VOIDED`. Alias history is in `supplier_alias_events` (CONFIRMED, PROMOTED, CONFLICT_FROZEN, REVOKED, UNFROZEN, APPLIED).

New UI: detail dialog — „Galimi tiekėjai" (candidates with tier / reason and „Priskirti"), „Pakeisti tiekėją",
„Pridėti IBAN prie tiekėjo", ranked pickers; partner edit dialog — „Išmokti pavadinimai (aliasai)" with „Atblokuoti" /
„Atšaukti aliasą".

---

# 1. Owner DDL for staging (CHANGES STAGING SCHEMA — apply BEFORE the new code runs)

**Why first:** the upload, assign, change and partner-save paths read `supplier_bank_accounts` and `supplier_aliases`. Without the
tables the new code throws on the first upload. FROZEN §8: apply the migration SQL before or immediately after the container
restart; do it **before** here.

1.1 **Backup** (staging, cheap insurance):

```bash
sudo mariadb-dump nordic_bees_erp_staging | gzip > ~/backup/staging-before-etapas2-ddl.sql.gz
ls -la ~/backup/staging-before-etapas2-ddl.sql.gz
```

1.2 **Pre-check** — the three tables must not exist and the last history row must be the Etapas 1 one:

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name IN ('supplier_bank_accounts', 'supplier_aliases', 'supplier_alias_events');"
sudo mariadb nordic_bees_erp_staging -e "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 3;"
```

Expected: no rows from the first; `20260926131956_AlterExpenseInvoiceLineUnitPricePrecision` on top of the second. Anything else → stop.

1.3 **DDL** (these are the statements of the two EF migrations `20260926234735_AddSupplierBankAccounts` and
`20260927011841_AddSupplierAliases`, with the indexes inline and the table collation set to `utf8mb4_unicode_ci` like
`business_partners`; the dev databases run the same statements with the server default collation — harmless). No foreign keys
(D-044 Q11). Run as one file:

```bash
cat > /tmp/etapas2-ddl.sql <<'EOF'
CREATE TABLE `supplier_bank_accounts` (
    `id` int NOT NULL AUTO_INCREMENT,
    `partner_id` int NOT NULL,
    `iban` varchar(34) NOT NULL,
    `source` varchar(20) NOT NULL,
    `source_invoice_id` int NULL,
    `is_active` tinyint(1) NOT NULL,
    `created_at` datetime(6) NOT NULL,
    `created_by` varchar(100) NULL,
    CONSTRAINT `PK_supplier_bank_accounts` PRIMARY KEY (`id`),
    UNIQUE KEY `uq_partner_iban` (`partner_id`, `iban`),
    KEY `idx_iban` (`iban`)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `supplier_aliases` (
    `id` int NOT NULL AUTO_INCREMENT,
    `partner_id` int NOT NULL,
    `alias_key` varchar(255) NOT NULL,
    `raw_example` varchar(255) NOT NULL,
    `state` varchar(12) NOT NULL,
    `confirmations` int NOT NULL,
    `frozen_reason` varchar(255) NULL,
    `created_at` datetime(6) NOT NULL,
    `updated_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_supplier_aliases` PRIMARY KEY (`id`),
    KEY `idx_alias_key` (`alias_key`),
    UNIQUE KEY `uq_alias_partner` (`alias_key`, `partner_id`)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `supplier_alias_events` (
    `id` int NOT NULL AUTO_INCREMENT,
    `alias_id` int NOT NULL,
    `invoice_id` int NULL,
    `event` varchar(20) NOT NULL,
    `actor` varchar(100) NULL,
    `details` longtext NULL,
    `created_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_supplier_alias_events` PRIMARY KEY (`id`),
    KEY `idx_alias_events_alias` (`alias_id`),
    KEY `idx_alias_events_invoice` (`invoice_id`)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES ('20260926234735_AddSupplierBankAccounts', '8.0.0');
INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES ('20260927011841_AddSupplierAliases', '8.0.0');
EOF
sudo mariadb nordic_bees_erp_staging < /tmp/etapas2-ddl.sql
```

1.4 **Verification SELECTs:**

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT table_name, COUNT(*) AS columns_n FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name IN ('supplier_bank_accounts', 'supplier_aliases', 'supplier_alias_events') GROUP BY table_name;"
sudo mariadb nordic_bees_erp_staging -e "SELECT table_name, index_name, non_unique, GROUP_CONCAT(column_name ORDER BY seq_in_index) AS cols FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name IN ('supplier_bank_accounts', 'supplier_aliases', 'supplier_alias_events') GROUP BY table_name, index_name, non_unique;"
sudo mariadb nordic_bees_erp_staging -e "SELECT MigrationId FROM __EFMigrationsHistory WHERE MigrationId LIKE '%SupplierBankAccounts' OR MigrationId LIKE '%SupplierAliases';"
```

Expected: 8 / 9 / 7 columns; indexes `PRIMARY`, `uq_partner_iban` (unique), `idx_iban`, `PRIMARY`, `idx_alias_key`, `uq_alias_partner` (unique), `PRIMARY`,
`idx_alias_events_alias`, `idx_alias_events_invoice`; two history rows. No foreign keys anywhere.

1.5 **IBAN backfill** (CHANGES STAGING DATA — inserts rows into the new table only). The file
`Migrations/Scripts/20260927_backfill_supplier_bank_accounts.sql` in the repo holds two statements: a **dry-run SELECT**
(which partners would get a row) and the **INSERT**. Run them one at a time — copy each block from the file (they are marked
`-- ---- dry run` and `-- ---- backfill`). The `/*SCOPE*/` token is a comment; leave it. Rules: whitespace stripped, upper-cased,
only A–Z0–9, the per-country length table of `IbanValidator`, ISO 13616 mod-97 = 1; the row is `source = 'MIGRATED'`. Values that
fail stay only in the legacy `bank_account` column. Idempotent (skips existing pairs).

Before the INSERT, compare with what you expect (dev: 57 of 61 values passed):

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT COUNT(*) AS partners, SUM(bank_account IS NOT NULL AND TRIM(bank_account) <> '') AS with_bank_account FROM business_partners;"
```

After the INSERT:

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT source, COUNT(*) AS rows_n, COUNT(DISTINCT partner_id) AS partners FROM supplier_bank_accounts GROUP BY source;"
sudo mariadb nordic_bees_erp_staging -e "SELECT COUNT(*) AS malformed FROM supplier_bank_accounts WHERE iban NOT REGEXP '^[A-Z][A-Z][0-9][0-9][0-9A-Z]+$';"
sudo mariadb nordic_bees_erp_staging -e "SELECT partner_id, iban, COUNT(*) AS n FROM supplier_bank_accounts GROUP BY partner_id, iban HAVING n > 1;"
sudo mariadb nordic_bees_erp_staging -e "SELECT id, name, bank_account FROM business_partners WHERE bank_account IS NOT NULL AND TRIM(bank_account) <> '' AND id NOT IN (SELECT partner_id FROM supplier_bank_accounts) ORDER BY id;"
```

Expected: only `MIGRATED`; `malformed` = 0; no duplicate pairs; the last query lists the values that are **not valid IBANs**
(give them to the accountant — they cannot be used to detect a new IBAN until fixed). D-045 count: 66 of 120 supplier partners
have a bank account, so `SUPPLIER_NEW_IBAN` is meaningful from day one.

Failure: the INSERT is rejected (MariaDB syntax) → report the error text verbatim; do not improvise.

---

# 2. Deploy check and baselines

2.1 **CI** green for the pushed commit; container running the new image, `RestartCount` 0, footer shows the new version:

```bash
sudo docker ps --filter name=nordicbees_staging --format '{{.Names}} {{.Image}} {{.CreatedAt}} {{.Status}}'
sudo docker logs nordicbees_staging 2>&1 | grep -E 'Kritinė klaida|Pending EF migrations|Migration warning|Unhandled exception|fail:'
```

Expected: no `Kritinė klaida`, no `Unhandled exception`, **no pending-migrations warning** (both new migrations are already in
`__EFMigrationsHistory` from step 1).

2.2 **Baselines** — write them down (used by the measurements in step 4 and the checks in step 5):

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT (SELECT MAX(id) FROM business_partners) AS BP0, (SELECT MAX(id) FROM expense_invoices) AS INV0, (SELECT MAX(id) FROM expense_invoice_audit) AS AUD0, (SELECT MAX(id) FROM files) AS FILE0;"
sudo mariadb nordic_bees_erp_staging -e "SELECT status, COUNT(*) AS n FROM expense_invoices GROUP BY status ORDER BY n DESC;"
sudo mariadb nordic_bees_erp_staging -e "SELECT COUNT(*) AS suppliers, SUM(is_active = 1) AS active FROM business_partners WHERE is_supplier = 1 OR is_expense_supplier = 1;"
```

Record: the status distribution before the clean start (the PLAN §5 query 1 baseline) and the supplier count.

2.3 **Smoke:** open an invoice in the list, open the detail dialog — it must render (candidates / „Pakeisti tiekėją" are
conditional). Any red error toast on opening = stop and report.

---

# 3. Master-data cleanup (D-045 step 1) — owner + accountant, BEFORE the clean start

**Nothing in this step is changed by Claude and this document contains no `UPDATE` or `DELETE` for partners.** The SELECTs show
what is wrong and — for each duplicate pair — how many rows reference each id, so the owner sees what a merge would have to move.
Merging is careful, separate work (beekeeper duplicates may carry deliveries and payments); do it with the accountant and only
then continue to step 4. Why it must precede the re-upload: with duplicate partners (same VAT) the cascade correctly refuses to
pick one (`VENDOR_AMBIGUOUS`), so the measurement in step 4 would mostly measure the duplicates.

## 3.1 Duplicates

Known pairs (D-045 Q9). By VAT: **Rotoma 369 / 381**, **Rokiškio vandenys 370 / 377**, **HONEYMARK PL 36 / 386** (386 has no name).
By name: **Deltamark 396 / 399** (396 has a junk VAT) and six beekeepers — **Bernotas 79 / 328**, **Žalalis 92 / 173**,
**Žilinskienė 89 / 170**, **Balčiūnas 78 / 326**, **Arbutavičius 65 / 333**, **Macijauskas 85 / 185**.

The pair rows:

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT id, name, vat_code, company_code, country_code, is_supplier, is_expense_supplier, is_customer, is_active, bank_account FROM business_partners WHERE id IN (369, 381, 370, 377, 36, 386, 396, 399, 79, 328, 92, 173, 89, 170, 78, 326, 65, 333, 85, 185) ORDER BY FIELD(id, 369, 381, 370, 377, 36, 386, 396, 399, 79, 328, 92, 173, 89, 170, 78, 326, 65, 333, 85, 185);"
```

**What references a partner.** These tables carry a partner id (foreign-key list read on the dev database, 2026-09-27;
`containers` and `deliveries` are the warehouse module): `expense_invoices.supplier_id`, `honey_deliveries.supplier_id`,
`supplier_payments.supplier_id`, `supplier_approvals.supplier_id`, `containers.supplier_id`,
`containers.reservation_customer_id`, `deliveries.supplier_id`, `invoices.customer_id`, `credit_notes.customer_id`,
`orders.customer_id`, `payments.customer_id`, `lots.customer_id`; and the two new tables `supplier_bank_accounts.partner_id`,
`supplier_aliases.partner_id`. Check that staging has the same set (any extra row = a table the counts below miss):

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT table_name, column_name FROM information_schema.columns WHERE table_schema = DATABASE() AND column_name IN ('supplier_id', 'customer_id', 'partner_id', 'reservation_customer_id') ORDER BY table_name, column_name;"
```

The counts per duplicate id (one row per partner; every column is a `COUNT(*)` of the rows that reference that id):

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT p.id, p.name, (SELECT COUNT(*) FROM expense_invoices t WHERE t.supplier_id = p.id) AS expense_invoices, (SELECT COUNT(*) FROM honey_deliveries t WHERE t.supplier_id = p.id) AS honey_deliveries, (SELECT COUNT(*) FROM supplier_payments t WHERE t.supplier_id = p.id) AS supplier_payments, (SELECT COUNT(*) FROM supplier_approvals t WHERE t.supplier_id = p.id) AS supplier_approvals, (SELECT COUNT(*) FROM containers t WHERE t.supplier_id = p.id) AS containers_supplier, (SELECT COUNT(*) FROM containers t WHERE t.reservation_customer_id = p.id) AS containers_reserved, (SELECT COUNT(*) FROM deliveries t WHERE t.supplier_id = p.id) AS deliveries, (SELECT COUNT(*) FROM invoices t WHERE t.customer_id = p.id) AS sales_invoices, (SELECT COUNT(*) FROM credit_notes t WHERE t.customer_id = p.id) AS credit_notes, (SELECT COUNT(*) FROM orders t WHERE t.customer_id = p.id) AS orders, (SELECT COUNT(*) FROM payments t WHERE t.customer_id = p.id) AS payments, (SELECT COUNT(*) FROM lots t WHERE t.customer_id = p.id) AS lots, (SELECT COUNT(*) FROM supplier_bank_accounts t WHERE t.partner_id = p.id) AS known_ibans, (SELECT COUNT(*) FROM supplier_aliases t WHERE t.partner_id = p.id) AS aliases FROM business_partners p WHERE p.id IN (369, 381, 370, 377, 36, 386, 396, 399, 79, 328, 92, 173, 89, 170, 78, 326, 65, 333, 85, 185) ORDER BY FIELD(p.id, 369, 381, 370, 377, 36, 386, 396, 399, 79, 328, 92, 173, 89, 170, 78, 326, 65, 333, 85, 185);"
```

Read it per pair: the id with more references is normally the survivor; **`expense_invoices` counts do not matter** — step 4 deletes
every expense invoice. `honey_deliveries`, `supplier_payments` and the warehouse tables do matter.

Are there more duplicates than the known ones? (PLAN §5 query 7 — normalised VAT, then name, then company code, then IBAN):

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT REGEXP_REPLACE(UPPER(vat_code), '[^0-9A-Z]', '') AS v, COUNT(*) AS c, GROUP_CONCAT(id ORDER BY id) AS ids, GROUP_CONCAT(name ORDER BY id SEPARATOR ' / ') AS names FROM business_partners WHERE vat_code IS NOT NULL AND vat_code <> '' GROUP BY v HAVING c > 1;"
sudo mariadb nordic_bees_erp_staging -e "SELECT name, COUNT(*) AS c, GROUP_CONCAT(id ORDER BY id) AS ids, GROUP_CONCAT(IFNULL(vat_code, '-') ORDER BY id SEPARATOR ' / ') AS vats FROM business_partners GROUP BY name HAVING c > 1;"
sudo mariadb nordic_bees_erp_staging -e "SELECT company_code, COUNT(*) AS c, GROUP_CONCAT(id ORDER BY id) AS ids FROM business_partners WHERE company_code IS NOT NULL AND company_code <> '' GROUP BY company_code HAVING c > 1;"
sudo mariadb nordic_bees_erp_staging -e "SELECT partner_id, iban FROM supplier_bank_accounts WHERE iban IN (SELECT iban FROM supplier_bank_accounts GROUP BY iban HAVING COUNT(DISTINCT partner_id) > 1) ORDER BY iban, partner_id;"
```

The last query lists an IBAN known for two partners (the matcher sees that as ambiguity, not as an error) — decide per case with the
accountant (a factoring company's IBAN legitimately appears on several suppliers' invoices).

## 3.2 Country codes (D-042 list)

To fix by hand with the accountant (the code never changes a partner's country, D-043): **RABEN LIETUVA**, **Xirgo Global**,
**OÜ Nordic Hotels**, **partner 386**, and the **8 partners with no country**. **KONICK RETAIL HUB stays CZ** (a Czech company with
an LT VAT registration — do not "correct" it from the VAT prefix).

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT id, name, vat_code, country, country_code FROM business_partners WHERE name LIKE 'RABEN%' OR name LIKE 'Xirgo%' OR name LIKE '%Nordic Hotels%' OR name LIKE 'KONICK%' OR id = 386 ORDER BY id;"
sudo mariadb nordic_bees_erp_staging -e "SELECT id, name, vat_code, country, country_code FROM business_partners WHERE country_code IS NULL OR TRIM(country_code) = '' ORDER BY id;"
sudo mariadb nordic_bees_erp_staging -e "SELECT country_code, COUNT(*) AS n, GROUP_CONCAT(id ORDER BY id) AS ids FROM business_partners GROUP BY country_code ORDER BY n DESC;"
```

The last query is the whole distribution: any code that is not an ISO 3166-1 alpha-2 code, and the suspicious `LI` / `IR`
(11 partners were `LI` — D-039), goes to the accountant.

## 3.3 The one partner with a prefix-less VAT (D-045 Q8)

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT id, name, vat_code, country_code FROM business_partners WHERE vat_code IS NOT NULL AND vat_code <> '' AND vat_code NOT REGEXP '^[A-Za-z][A-Za-z]' ORDER BY id;"
sudo mariadb nordic_bees_erp_staging -e "SELECT id, name, vat_code FROM business_partners WHERE vat_code IS NOT NULL AND vat_code <> '' AND (vat_code REGEXP '[^0-9A-Za-z]' OR vat_code <> UPPER(vat_code)) ORDER BY id;"
```

Expected on the D-045 data: 1 row without a letter prefix and 1 with a separator. Give the correct prefixed code to the
accountant. After Etapas 2 a prefix-less stored code only equals a prefix-less document code — it will **not** match an
`LT`-prefixed one (no LT assumption).

**Exit criterion for step 3:** no duplicate pair left (3.1 queries empty apart from IBANs the accountant accepted), the D-042 list
resolved, no prefix-less VAT. Write down what was merged and how.

---

# 4. Clean start on staging (D-045 step 2)

**CHANGES STAGING DATA — deletes every expense invoice.** Justified by D-045: production has 247 expense invoices, 0 payments, 0
allocations, 0 paid; the owner holds every original. Staging is a clone, so the same holds here **only if the checks below show
it — if any of them shows a payment or an allocation, stop and tell Claude.**

## 4.1 Backup and pre-checks

```bash
sudo mariadb-dump nordic_bees_erp_staging | gzip > ~/backup/staging-before-clean-start.sql.gz
ls -la ~/backup/staging-before-clean-start.sql.gz
sudo tar czf ~/backup/staging-blobs-before-clean-start.tgz -C /var/lib/nordicbees/staging blobs
ls -la ~/backup/staging-blobs-before-clean-start.tgz
```

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT (SELECT COUNT(*) FROM expense_invoices) AS invoices, (SELECT COUNT(*) FROM expense_invoice_lines) AS lines_n, (SELECT COUNT(*) FROM expense_line_allocations) AS allocations, (SELECT COUNT(*) FROM expense_payments) AS payments, (SELECT COUNT(*) FROM expense_invoice_audit) AS audit_rows, (SELECT COUNT(*) FROM expense_ocr_queue) AS ocr_queue, (SELECT COUNT(*) FROM email_invoice_imports WHERE expense_invoice_id IS NOT NULL) AS email_imports, (SELECT COUNT(*) FROM files WHERE module = 'expenses') AS files_rows;"
sudo mariadb nordic_bees_erp_staging -e "SELECT status, COUNT(*) AS n, SUM(paid_amount > 0) AS with_paid FROM expense_invoices GROUP BY status;"
```

**Stop conditions:** `payments` > 0, `allocations` > 0, `with_paid` > 0, or `email_imports` > 0. Otherwise write the numbers down.

Which file rows and blobs belong to expense invoices — save the list **before** deleting the rows:

```bash
sudo mariadb nordic_bees_erp_staging -N -B -e "SELECT DISTINCT sha256 FROM files WHERE module = 'expenses';" > ~/expense-blob-shas.txt
wc -l ~/expense-blob-shas.txt
sudo mariadb nordic_bees_erp_staging -e "SELECT module, entity_type, COUNT(*) AS n, SUM(entity_id IS NULL) AS unlinked FROM files GROUP BY module, entity_type;"
```

Older invoices may also point at `original_file_path` (the pre-`files` layout, `wwwroot/uploads/invoices/…` inside the
container); those loose PDFs are harmless and are **not** deleted here:

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT COUNT(*) AS with_legacy_path, SUM(file_id IS NULL) AS without_file_id FROM expense_invoices WHERE original_file_path IS NOT NULL AND original_file_path <> '';"
```

## 4.2 The deletes — ordered, each followed by a verification SELECT (**CHANGES STAGING DATA**)

Dependent rows first (allocations → payments → lines → audit → queue), then the invoices, then the file rows. Run each pair in order and
compare the count with 4.1.

```bash
sudo mariadb nordic_bees_erp_staging -e "DELETE FROM expense_line_allocations WHERE invoice_line_id IN (SELECT id FROM expense_invoice_lines);"
sudo mariadb nordic_bees_erp_staging -e "SELECT COUNT(*) AS allocations_left FROM expense_line_allocations;"

sudo mariadb nordic_bees_erp_staging -e "DELETE FROM expense_payments;"
sudo mariadb nordic_bees_erp_staging -e "SELECT COUNT(*) AS payments_left FROM expense_payments;"

sudo mariadb nordic_bees_erp_staging -e "DELETE FROM expense_invoice_lines;"
sudo mariadb nordic_bees_erp_staging -e "SELECT COUNT(*) AS lines_left FROM expense_invoice_lines;"

sudo mariadb nordic_bees_erp_staging -e "DELETE FROM expense_invoice_audit;"
sudo mariadb nordic_bees_erp_staging -e "SELECT COUNT(*) AS audit_left FROM expense_invoice_audit;"

sudo mariadb nordic_bees_erp_staging -e "DELETE FROM expense_ocr_queue;"
sudo mariadb nordic_bees_erp_staging -e "SELECT COUNT(*) AS queue_left FROM expense_ocr_queue;"

sudo mariadb nordic_bees_erp_staging -e "DELETE FROM expense_invoices;"
sudo mariadb nordic_bees_erp_staging -e "SELECT COUNT(*) AS invoices_left FROM expense_invoices;"

sudo mariadb nordic_bees_erp_staging -e "DELETE FROM files WHERE module = 'expenses';"
sudo mariadb nordic_bees_erp_staging -e "SELECT COUNT(*) AS expense_files_left FROM files WHERE module = 'expenses';"
```

Expected: every `*_left` = 0. Alias history that was written by earlier tests of Etapas 2 refers to deleted invoices — on a fresh staging
the alias tables are empty; **if step 5 was already run once**, also clear them so the measurement starts clean:

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT (SELECT COUNT(*) FROM supplier_aliases) AS aliases, (SELECT COUNT(*) FROM supplier_alias_events) AS events;"
```

(Only if both are test data: `DELETE FROM supplier_alias_events;` then `DELETE FROM supplier_aliases;` — a clean measurement wants none
left. `supplier_bank_accounts` stays: the backfill and partner-saved IBANs are master data.)

## 4.3 Blobs on disk

Blob path: `/var/lib/nordicbees/staging/blobs/<sha[0:2]>/<sha[2:4]>/<sha>` (`FileStore.BlobPath`). Delete only blobs whose sha no longer
appears in any `files` row (another module may share a PDF):

```bash
while read -r sha; do n=$(sudo mariadb nordic_bees_erp_staging -N -B -e "SELECT COUNT(*) FROM files WHERE sha256 = '$sha';"); if [ "$n" = "0" ]; then sudo rm -f "/var/lib/nordicbees/staging/blobs/${sha:0:2}/${sha:2:2}/$sha"; fi; done < ~/expense-blob-shas.txt
sudo find /var/lib/nordicbees/staging/blobs -type f | wc -l
```

Verification: the file count dropped by about the number of lines in `~/expense-blob-shas.txt` (minus shared blobs). If a later upload says a
PDF is already stored, that is the `files` dedupe working — not an error.

## 4.4 Re-upload

The owner uploads **all originals** through Išlaidų sąskaitos → Įkelti (one at a time; ≈ 247 Azure calls on the production key, D-029;
scanned PDFs are refused by D-032 before Azure — list those, they need a digital copy). Uploads now create partners on demand:
**for every `PENDING_SUPPLIER` invoice, either create the supplier (Sukurti tiekėją) or assign an existing one (Priskirti)** and
count how many you had to create. Do not rush this: it is the real load test of the whole module and the raw material for
Etapas 3–4 (D-045).

## 4.4a Measurements (SELECT-only; run after the last upload and before step 5)

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT status, COUNT(*) AS n, ROUND(SUM(amount_incl_vat), 2) AS total FROM expense_invoices GROUP BY status ORDER BY n DESC;"
```

Status distribution (clean = PENDING / stopped = NEEDS_REVIEW, PENDING_SUPPLIER, DUPLICATE_PENDING, REJECTED). Compare with the
2.2 baseline.

Flags — review vs information (one query, one column per flag; a flag is counted once per invoice):

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT COUNT(*) AS invoices, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('SUPPLIER_NEW_IBAN'))) AS SUPPLIER_NEW_IBAN, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('VENDOR_NOT_FOUND'))) AS VENDOR_NOT_FOUND, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('VENDOR_AMBIGUOUS'))) AS VENDOR_AMBIGUOUS, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('VENDOR_SUGGESTED'))) AS VENDOR_SUGGESTED, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('AMOUNT_MISMATCH'))) AS AMOUNT_MISMATCH, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('AMOUNT_ARITHMETIC_MISMATCH'))) AS AMOUNT_ARITHMETIC_MISMATCH, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('MISSING_MONEY_FIELD'))) AS MISSING_MONEY_FIELD, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('NUMBER_MISREAD'))) AS NUMBER_MISREAD, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('NUMBER_AMBIGUOUS'))) AS NUMBER_AMBIGUOUS, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('INVALID_IBAN'))) AS INVALID_IBAN, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('INVALID_VAT_FORMAT'))) AS INVALID_VAT_FORMAT, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('DUPLICATE'))) AS DUPLICATE, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('WRONG_RECIPIENT'))) AS WRONG_RECIPIENT, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('VAT_RATE_UNCHECKED'))) AS VAT_RATE_UNCHECKED, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('VAT_COUNTRY_MISMATCH'))) AS VAT_COUNTRY_MISMATCH, SUM(JSON_CONTAINS(ocr_flags, JSON_QUOTE('LINE_AMOUNT_IMPLAUSIBLE'))) AS LINE_AMOUNT_IMPLAUSIBLE FROM expense_invoices WHERE ocr_flags IS NOT NULL;"
```

Review flags are: `SUPPLIER_NEW_IBAN`, `AMOUNT_MISMATCH`, `AMOUNT_ARITHMETIC_MISMATCH`, `MISSING_MONEY_FIELD`, `NUMBER_MISREAD`, `NUMBER_AMBIGUOUS`
(and `INVALID_IBAN` / `INVALID_VAT_FORMAT` only while there is no supplier); the rest above are information / quarantine.

The cascade itself — every create wrote one `SUPPLIER_MATCHED` row (`outcome=…; tier=…; reason=…; candidates=…; supplier=…`):

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT SUBSTRING_INDEX(SUBSTRING_INDEX(action_details, 'outcome=', -1), ';', 1) AS outcome, SUBSTRING_INDEX(SUBSTRING_INDEX(action_details, 'tier=', -1), ';', 1) AS tier, SUBSTRING_INDEX(SUBSTRING_INDEX(action_details, 'reason=', -1), ';', 1) AS reason, COUNT(*) AS n FROM expense_invoice_audit WHERE action = 'SUPPLIER_MATCHED' AND id > <AUD0> GROUP BY outcome, tier, reason ORDER BY n DESC;"
```

How the workload split — assigned by the matcher vs by a human vs suppliers created:

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT action, COUNT(*) AS n FROM expense_invoice_audit WHERE action IN ('SUPPLIER_ASSIGNED', 'SUPPLIER_AUTO_ASSIGNED', 'SUPPLIER_CHANGED', 'SUPPLIER_IBAN_ADDED', 'SUPPLIER_SUGGESTED', 'APPROVAL_VOIDED') AND id > <AUD0> GROUP BY action;"
sudo mariadb nordic_bees_erp_staging -e "SELECT COUNT(*) AS suppliers_created FROM business_partners WHERE id > <BP0>;"
sudo mariadb nordic_bees_erp_staging -e "SELECT COUNT(*) AS still_waiting_for_a_supplier FROM expense_invoices WHERE status = 'PENDING_SUPPLIER';"
sudo mariadb nordic_bees_erp_staging -e "SELECT status, COUNT(*) AS n, SUM(approved_by IS NOT NULL) AS approved FROM expense_invoices WHERE supplier_id IS NULL GROUP BY status;"
```

The last query is gate 3: a supplier-less invoice is only PENDING_SUPPLIER / REJECTED / DUPLICATE_PENDING and **approved = 0 everywhere**.

Aliases learned during the re-upload (from your „Priskirti" clicks):

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT state, COUNT(*) AS n, SUM(confirmations) AS confirmations FROM supplier_aliases GROUP BY state;"
```

**Record and decide (nothing to fix here):** the clean / stopped split and *why* (which flag holds the most invoices); how many
`Assigned` per tier; how many `Ambiguous` / `Suggested` (each is a master-data or document question); how many suppliers you had to
create; how many `SUPPLIER_NEW_IBAN` (are they real new IBANs, or factoring, or IBANs the backfill could not store?).

---

# 5. Rule checks (PLAN §7.3) — each: one that must stop, one that must pass, on create, re-OCR and edit

Every check needs concrete invoices; pick them with the queries in 5.0, then follow the steps. Because step 4 emptied the expense
invoices, the "every corpus PDF is already in production → DUPLICATE_PENDING" caveat of Etapas 1 does **not** apply here — a document
uploaded once in step 4 will be a duplicate if uploaded again, so **use re-OCR (PAKARTOTI OCR) for the re-OCR cases and the edit form
for the edit cases; create cases use the invoices of step 4.** Data created here stays on staging (re-clone from prod when needed).

## 5.0 Pick the invoices

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT id, invoice_number, status, supplier_id, pending_supplier_name, pending_supplier_vat, pending_supplier_bank_account FROM expense_invoices WHERE status = 'PENDING_SUPPLIER' ORDER BY id DESC LIMIT 20;"
sudo mariadb nordic_bees_erp_staging -e "SELECT pending_supplier_name, COUNT(*) AS n, GROUP_CONCAT(id ORDER BY id) AS ids FROM expense_invoices WHERE status = 'PENDING_SUPPLIER' AND pending_supplier_name IS NOT NULL GROUP BY pending_supplier_name HAVING n >= 2 ORDER BY n DESC LIMIT 10;"
sudo mariadb nordic_bees_erp_staging -e "SELECT id, invoice_number, status, supplier_id, ocr_flags FROM expense_invoices WHERE status IN ('PENDING', 'NEEDS_REVIEW') AND supplier_id IS NOT NULL ORDER BY id DESC LIMIT 20;"
```

Name what you picked: `<PS>` (PENDING_SUPPLIER with a VAT code), `<PS2>`, `<PS3>` (PENDING_SUPPLIER, valid pending IBAN),
`<N1>`, `<N2>` (two PENDING_SUPPLIER invoices with the **same** OCR name, from the second query), `<OK>` (PENDING with a supplier).

## 5.1 VAT variants match exactly one partner (tier 1)

**Must pass — create:** in step 4 the `SUPPLIER_MATCHED` distribution has `outcome=Assigned; tier=Vat` rows.
**Must pass — re-OCR:** pick `<PS>`'s twin: a PENDING_SUPPLIER invoice whose VAT the partner already stores in another spelling (spaces,
dashes, lower case): edit that partner's VAT in the partner dialog to `lt 123-456-789`-style spelling (keep the digits!), press
PAKARTOTI OCR on an invoice of that partner that is not yet assigned, expect it to be assigned again (status PENDING / NEEDS_REVIEW, audit
`SUPPLIER_MATCHED … tier=Vat`). Put the VAT back.
**Must stop — no LT assumption (D-045 Q8):** a document VAT with the digits only and no address country must **not** match a partner stored
as `LT`+digits. Record the case if you find one (`VENDOR_NOT_FOUND` / `VENDOR_SUGGESTED`).

## 5.2 Same VAT on two partners → nothing assigned, both shown (VENDOR_AMBIGUOUS)

**Setup (CHANGES STAGING DATA):** in the partner list create two throw-away partners with the same VAT as `<PS>` (different names,
both „Tiekėjas", active).
**Must stop — re-OCR:** PAKARTOTI OCR on `<PS>`. Expect: status PENDING_SUPPLIER, chips „Nežinomas tiekėjas" (hidden while pending) and
**„Keli galimi tiekėjai"**, the detail dialog lists **both** partners under „Galimi tiekėjai" with tier „PVM kodas" and reason „keli tiekėjai su tais
pačiais duomenimis"; audit `SUPPLIER_MATCHED outcome=Ambiguous`. **Edit path:** save an edit on `<PS>` — the flag stays, the status stays.
**Must pass:** click „Priskirti" next to one candidate → assigned, `VENDOR_AMBIGUOUS` gone, audit `SUPPLIER_ASSIGNED`. Then delete the two
throw-away partners' duplicate (leave one, or none) afterwards.

## 5.3 Same name, different VAT → not assigned (VENDOR_SUGGESTED)

**Setup:** create a partner whose name equals `<PS2>`'s `pending_supplier_name` exactly but with a **different** VAT.
**Must stop — re-OCR:** PAKARTOTI OCR on `<PS2>`: PENDING_SUPPLIER, chip **„Siūlomas kitas tiekėjas"**, the candidate shown with reason
**„kodai nesutampa"**; `SUPPLIER_MATCHED outcome=Suggested; reason=ConflictingIdentifier`. **Must pass:** the same partner without a VAT of its own and a
document without a VAT → assigned by name (tier Name). (Today's silent wrong assignment is exactly what this replaces.)

## 5.4 Inactive / customer-only partner → suggestion only (Q7)

Deactivate the throw-away partner of 5.3 (or make it customer-only) and re-OCR: the outcome must be `Suggested; reason=PartnerNotEligible`, never `Assigned`.

## 5.5 Known supplier + new IBAN → NEEDS_REVIEW; „Pridėti IBAN prie tiekėjo"; PATVIRTINTI keeps it

**Setup:** create a throw-away partner with a **valid** IBAN (partner dialog, e.g. `LT121000011101001000`) → the IBAN is stored as a known
account (`SELECT * FROM supplier_bank_accounts WHERE partner_id = <P>` shows `MANUAL`). Assign it to `<PS2>` and `<PS3>` (each has a different, valid
IBAN in `pending_supplier_bank_account`) with „Priskirti esamam".
**Must stop — assign:** both invoices become **NEEDS_REVIEW** with the chip **„Naujas tiekėjo IBAN"** although a supplier is assigned.
**Add the IBAN — `<PS2>`:** the detail dialog shows „Dokumento IBAN … nėra tarp tiekėjo žinomų sąskaitų" and **„Pridėti IBAN prie tiekėjo"**. Click it: the
flag disappears, the status follows the shared rules (NEEDS_REVIEW → PENDING when nothing else holds it), a `supplier_bank_accounts` row
`INVOICE_CONFIRMED` with `source_invoice_id = <PS2>` exists, audit `SUPPLIER_IBAN_ADDED` shows a **masked** IBAN (`LT12…1000`); clicking again is not offered.
**PATVIRTINTI without adding — `<PS3>`:** press PATVIRTINTI: the invoice becomes PENDING, the chip **stays** (a record — the NUMBER_* pattern).
**Edit path:** save an edit on a flagged invoice that was not approved: flag and NEEDS_REVIEW stay.
**Must pass:** an invoice of the same partner whose document IBAN **is** known (upload/re-OCR one, or use the backfilled partners) → no
`SUPPLIER_NEW_IBAN`. A supplier with **no** known account → no flag, but the button is offered (the first IBAN). An **invalid** document IBAN
(check `INVALID_IBAN` invoices from step 4) → never offered, never a `SUPPLIER_NEW_IBAN`.

## 5.6 „Pakeisti tiekėją" (D-045)

**Must pass:** on `<OK>` (PENDING or NEEDS_REVIEW, no payments) press „Pakeisti tiekėją", pick another partner → snackbar „Tiekėjas pakeistas", audit `SUPPLIER_CHANGED
Tiekėjo ID: old → new`, rate flags re-derived for the new partner's country, status by the shared rules. If it was **approved**: `APPROVAL_VOIDED` (audit) and
`approved_by` empty:

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT id, supplier_id, status, approved_by, ocr_flags FROM expense_invoices WHERE id = <OK>;"
sudo mariadb nordic_bees_erp_staging -e "SELECT action, action_details, old_status, new_status FROM expense_invoice_audit WHERE invoice_id = <OK> ORDER BY id DESC LIMIT 5;"
```

**Must stop:** the button is **not shown** for PAID / PARTIAL / OVERDUE / REJECTED / DUPLICATE_PENDING / PENDING_SUPPLIER invoices, and the service refuses in
Lithuanian („Tiekėjo keisti negalima: …") — there are no payments in staging after step 4, so to see the payment refusal register one payment on a
PENDING invoice first (Etapas 0 flow), then the button disappears.
The picker is ranked: the partners most like the OCR name are on top (5.9).

## 5.7 Aliases: promotion at two confirmations, conflict freeze, revoke / unfreeze

**Promotion:** take `<N1>` and `<N2>` (same OCR name, different invoices). Assign **both** to the same partner whose own name is **different** from
that OCR name („Priskirti esamam"):

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT id, partner_id, alias_key, raw_example, state, confirmations, frozen_reason FROM supplier_aliases ORDER BY id DESC LIMIT 10;"
sudo mariadb nordic_bees_erp_staging -e "SELECT alias_id, invoice_id, event, actor FROM supplier_alias_events ORDER BY id DESC LIMIT 10;"
```

Expected after the first: `CANDIDATE`, `confirmations = 1` (candidates are **never** applied); after the second: **`ACTIVE`, `confirmations = 2`**, events `CONFIRMED`, `CONFIRMED`,
`PROMOTED`. Repeating „Priskirti" on the same invoice does not exist (it is no longer PENDING_SUPPLIER); a re-OCR of an assigned invoice does not count.
**Applied:** upload — or re-OCR a still-unassigned invoice with — the same OCR name: it is assigned by tier **Alias** (`SUPPLIER_MATCHED … tier=Alias`) and an `APPLIED` event appears.
**Never over a contradicting code:** an invoice with that name but a **different VAT** than the partner's is only suggested (`reason=ConflictingIdentifier`).
**Conflict freeze:** assign a third invoice with the **same OCR name** to a **different** partner: both aliases become **FROZEN** (`frozen_reason` „Konfliktas: …",
events `CONFLICT_FROZEN`); a further invoice with that name is **not** assigned by alias.
**Revoke / unfreeze:** open the partner in the partner dialog: „Išmokti pavadinimai (aliasai)" lists the alias with state, confirmations and the raw example.
„Atblokuoti" on one → `UNFROZEN` (ACTIVE if it has ≥ 2 confirmations); on the second it is **refused** while the first is ACTIVE („Kitas tiekėjas jau turi aktyvų …");
„Atšaukti aliasą" → `REVOKED` (event), never applied again.
**Not a confirmation:** „Sukurti tiekėją" on an invoice assigns its siblings by the sweep — only the invoice you clicked on gets an alias confirmation:

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT a.alias_key, a.confirmations, COUNT(DISTINCT e.invoice_id) AS confirming_invoices FROM supplier_aliases a JOIN supplier_alias_events e ON e.alias_id = a.id AND e.event = 'CONFIRMED' GROUP BY a.id, a.alias_key, a.confirmations;"
```

`confirmations` must equal `confirming_invoices` on every row. **No retroactive sweep:** after a promotion, other PENDING_SUPPLIER invoices with that name stay PENDING_SUPPLIER.

## 5.8 Gate 3 — no invoice leaves PENDING_SUPPLIER without a supplier (S1)

**Must stop:** on a PENDING_SUPPLIER invoice the **PATVIRTINTI button is not shown** (ATMESTI stays). Reject one supplier-less invoice, then use the **„Atstatyti"** icon on its REJECTED row in the invoice list: it returns
to **PENDING_SUPPLIER**, never NEEDS_REVIEW/PENDING; a rejected `WRONG_RECIPIENT` invoice is restored **and stays restored** (D-044 Q13, audit `WRONG_RECIPIENT_DISMISSED` +
`RESTORED`). The service refuses `AssignSupplierAsync` on DUPLICATE_PENDING / REJECTED / already-assigned invoices („Tiekėją galima priskirti tik sąskaitai, kuri laukia tiekėjo").
**Must pass:** an invoice with a supplier is approved normally. **Re-OCR keeps a human-assigned supplier:** PAKARTOTI OCR on an assigned invoice never unassigns it; if the fresh match is a
*different* partner the chip **„Siūlomas kitas tiekėjas"** appears and the detail dialog says „Pakartotinis OCR siūlo kitą tiekėją: …", the assigned supplier is kept.
Quarantine (D-027): a DUPLICATE_PENDING invoice stays DUPLICATE_PENDING through re-OCR and assign attempts.

```bash
sudo mariadb nordic_bees_erp_staging -e "SELECT status, COUNT(*) AS n, SUM(approved_by IS NOT NULL) AS approved FROM expense_invoices WHERE supplier_id IS NULL GROUP BY status;"
```

## 5.9 Picker ranking (S6)

Open „Priskirti esamam" on a PENDING_SUPPLIER invoice: the list is **filled when opened**, the matcher's candidates (5.2 / 5.3) are on top, the rest is ordered by name similarity
to the OCR name (`UAB Rotoma` before `UAB Rotoma Plius` for the query `Rotoma`, never equal); typing filters and keeps the order; picking is always yours — **nothing is assigned by ranking.**

## 5.10 Cleanup of the test data

Delete the throw-away partners of 5.2–5.5 (partner list), and if you want a clean alias table again: `DELETE FROM supplier_alias_events; DELETE FROM supplier_aliases;`
(test data only). Record what you left.

---

# 6. Go / no-go for closing Etapas 2 on staging

All must be true:

- [ ] 1: the DDL applied; 8 / 9 / 7 columns, both history rows, no foreign keys; the IBAN backfill ran (or its rejection was reported) and `malformed` = 0.
- [ ] 2: new container, no `Kritinė klaida`, no pending-migrations warning; baselines recorded.
- [ ] 3: duplicates merged or explicitly accepted with the accountant; the D-042 list and the prefix-less VAT resolved; what was done is written down.
- [ ] 4: backups made; the stop conditions were clear; every `*_left` = 0; re-upload complete; the measurements are recorded (status split, review vs information flags,
      `SUPPLIER_MATCHED` outcome / tier distribution, `VENDOR_AMBIGUOUS` / `VENDOR_SUGGESTED` / `SUPPLIER_NEW_IBAN` counts, suppliers created).
- [ ] 4: gate 3 query — supplier-less invoices only PENDING_SUPPLIER / REJECTED / DUPLICATE_PENDING and **approved = 0**.
- [ ] 5.1–5.4: VAT variants match; the same VAT twice → ambiguous; same name + other VAT → suggested; ineligible partner → suggested — on create, re-OCR and edit.
- [ ] 5.5: new IBAN → NEEDS_REVIEW (with a supplier), „Pridėti IBAN" clears it and stores `INVOICE_CONFIRMED`, PATVIRTINTI keeps it, invalid IBAN never offered.
- [ ] 5.6: „Pakeisti tiekėją" allowed / refused matrix, approval voided, audited.
- [ ] 5.7: alias promotion at 2, conflict freeze, revoke / unfreeze, sweep ≠ confirmation (`confirmations = confirming_invoices`).
- [ ] 5.8: gate 3 — PATVIRTINTI hidden, restore by the shared rules, assign guard, re-OCR keeps the supplier.
- [ ] 5.9: pickers ranked, nothing auto-assigned.
- [ ] The measurements are **explainable**: every stopped invoice has a reason the owner accepts (a real master-data gap, a real document problem, or a deliberate loud stop) — no
      unexplained wrong assignment (spot-check ≥ 20 `Assigned` rows against the PDF: same supplier?).
- [ ] Every "record and decide" note above reviewed.

Any unchecked box = **no-go** for closing Etapas 2; report the step number and the SQL output.
Cannot be exercised on staging (automated tests only): a concurrent double-confirmation of an alias, the transaction rollback of `ChangeSupplierAsync`.

## What remains before the Etapai 1–4 production deploy (D-037)

- **Production DDL at the final deploy**, with a fresh backup: the `unit_price` `ALTER`, `supplier_bank_accounts`, `supplier_aliases`, `supplier_alias_events` + their history rows (this document,
  step 1), the IBAN backfill (1.5) and the production sentinel `Production`.
- **Production master-data cleanup** with the accountant (the same lists, run against production by the owner — production is never touched by an agent), then the **production clean
  start** (D-045 step 3: backup, delete, re-upload; the accountant warned in advance, Q-010).
- **VAT rates confirmed** by the accountant against EC TEDB (all `VatRateTable` rows are still UNCONFIRMED, so the rate gate only produces `VAT_RATE_UNCHECKED`); EE 22 → 24 % change date.
- **LI partners** (11, suspected master-data errors) — settled in step 3.2.
- **Drag & drop** (FROZEN §3 change needs the owner's permission), **orphan dialogs** (Etapas 1 Q11), the flaky `CreditNoteServiceTests…` test.
- Etapas 3 (extraction) and 4 (measurement) planned, run and verified on staging.
- Open code questions from the S3–S6 reports: correcting a *matcher-assigned* supplier cannot teach an alias (the OCR name is not stored on such invoices); the test database's `expense_payments`
  lacks the `source` column; the backfill was validated on MySQL 8 only.

## Mapping: what each step covers

| Step | Covers |
|---|---|
| 1 | staging DDL (bank accounts, aliases, events), IBAN backfill |
| 2 | deploy, baselines |
| 3 | duplicates (Rotoma, Rokiškio vandenys, HONEYMARK PL, Deltamark, six beekeepers), D-042 countries, prefix-less VAT |
| 4 | backup, ordered deletes, blobs, re-upload, measurements |
| 5.1–5.4 | matcher tiers, contradiction rule, ambiguity, eligibility |
| 5.5 | SUPPLIER_NEW_IBAN, add-IBAN, PATVIRTINTI record |
| 5.6 | ChangeSupplierAsync |
| 5.7 | aliases |
| 5.8 | gate 3 |
| 5.9 | picker ranking |

**Steps that CHANGE staging data:** 1.3, 1.5 (schema / backfill), 4.2–4.4 (deletes, blobs, uploads), 5.2–5.7 (throw-away partners, assignments, re-OCR, one manual payment), 5.10.
**No data change:** 2, 3 (SELECTs only — the cleanup itself is the owner's manual work in the UI / accountant's process), 4.1, 4.4a.
