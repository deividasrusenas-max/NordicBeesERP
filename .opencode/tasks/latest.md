# Task: Draft merge SQL for 10 confirmed duplicate business_partners pairs

## Type: DRAFT SQL SCRIPT ONLY — DO NOT EXECUTE, DO NOT MODIFY THE DATABASE

## Context
`.opencode/reports/partner-type-code-inventory-and-merge-brief-20260904-1700.md`
confirmed 10 duplicate pairs (7 customer/supplier + 3 supplier/supplier)
representing the same real-world entities. The human has approved merging
all 10. This task drafts the exact SQL to do it — Deividas will review
and run it himself against dev first, then production. You do NOT run
any UPDATE/DELETE statements against any database in this task.

The 10 pairs (surviving id listed first — see "which id survives" rule
below):

| Name | id A | id B |
|---|---|---|
| Zita Rutkauskienė | 12 (customer) | 294 (supplier) |
| Vaidas Arbutavičius | 65 (customer) | 333 (supplier) |
| Tomas Balčiūnas | 78 (customer) | 326 (supplier) |
| AURIMAS BERNOTAS | 79 (customer) | 328 (supplier) |
| Žilvinas Macijauskas | 85 (customer) | 185 (supplier) |
| Regina Žilinskienė | 89 (customer) | 170 (supplier) |
| LAIMUTIS ŽALALIS | 92 (customer) | 173 (supplier) |
| UAB "Deltamark" | 396 (supplier) | 399 (supplier) |
| UAB "Rokiškio vandenys" | 370 (supplier) | 377 (supplier) |
| UAB "Rotoma" | 369 (supplier) | 381 (supplier) |

**Known open issue (do not fix silently):** Tomas Balčiūnas (326) and
AURIMAS BERNOTAS (328) currently share the identical
`national_id_number` value `302905315` — this is a separate, real data
error (one or both is wrong) unrelated to the merge itself. Do NOT
attempt to guess or correct which one is right. Carry whatever value
each surviving record currently has forward unchanged, and add an
explicit `-- TODO(human): verify correct national_id_number, currently
shared with id X` comment next to those two specific merges in the
output SQL so it isn't forgotten.

## Step 1 — Find every FK-like reference to business_partners.id

Do NOT rely on the 4 tables already checked in prior reports
(`invoices.customer_id`, `expense_invoices.supplier_id`,
`payments.customer_id`, `credit_notes`). Do a genuinely complete pass:

- Query `information_schema.columns` for every column across the whole
  `nordic_bees_erp` schema named like `%customer_id%`, `%supplier_id%`,
  `%partner_id%`, `%business_partner%` (case-insensitive).
- Also check `information_schema.key_column_usage` /
  `information_schema.table_constraints` for actual declared foreign
  keys referencing `business_partners`.
- Also grep the C# codebase for `BusinessPartnerId`-style properties or
  navigation properties pointing at `BusinessPartner`/`Supplier`/
  `Customer` to catch anything EF-mapped that might not show as a
  classic FK constraint (this project uses `ExecuteSqlRawAsync` heavily,
  so some relationships may not have DB-level FK constraints at all).
- Report the full list of tables+columns found, even ones that turn out
  to have zero rows for these 20 ids — completeness matters more than
  brevity here.

## Step 2 — Which id survives each pair

Rule: for customer/supplier pairs, the id whose record has MORE
populated fields (address, VAT/company code, bank account, phone)
survives and gets `PartnerType` set to `Both` after merge (since it now
represents both roles). For the 3 supplier/supplier pairs, the id with
more complete data survives and keeps `PartnerType = Supplier` (or
`ExpenseSupplier` if that's what it already was — check the actual
current value, don't assume).

Based on the prior report's findings, the likely surviving ids are (but
VERIFY each against fresh data before finalizing, the report's data may
be stale by the time you run this):
- Zita: supplier id 294 survives (has more complete farmer/address data) — or customer 12 if it actually has the address; re-check, the report was ambiguous about which side had the address.
- Vaidas: customer 65 survives (has invoices) but pull supplier 333's phone number into it before merge.
- Tomas: supplier 326 survives but note the shared-national-id TODO above.
- Aurimas: supplier 328 survives but note the shared-national-id TODO above.
- Žilvinas: supplier 185 survives (has full data + invoices).
- Regina: supplier 170 survives (has VAT/national id + invoice).
- Laimutis: supplier 173 survives (has full data + invoices).
- Deltamark: pick whichever of 396/399 has the more standard-looking VAT code format (one had a malformed-looking VAT code per the prior report — verify and pick the correctly-formatted one as survivor).
- Rokiškio vandenys: id with bank account + expense category populated survives (per report, that's 370).
- Rotoma: essentially identical, survivor with earlier `created_at` (369 per report).

For each pair, EXPLICITLY state in the output which id you chose as
survivor and WHY (one sentence), so Deividas can override the choice
before running anything.

## Step 3 — Draft the SQL

For EACH pair, produce a self-contained SQL block, wrapped in a
transaction, in this shape (adjust table/column list based on what
Step 1 actually found — this is illustrative, not exhaustive):

```sql
-- ============================================================
-- MERGE: <name> — surviving id <X>, merged-away id <Y>
-- ============================================================
START TRANSACTION;

-- Reassign FKs found in Step 1, one UPDATE per table/column, e.g.:
UPDATE invoices SET customer_id = <X> WHERE customer_id = <Y>;
UPDATE expense_invoices SET supplier_id = <X> WHERE supplier_id = <Y>;
UPDATE payments SET customer_id = <X> WHERE customer_id = <Y>;
-- ... every other table/column found in Step 1 ...

-- Backfill any fields present on the losing record but NULL on the
-- surviving one (e.g. phone, bank account) — one UPDATE per field,
-- only where the survivor's field IS NULL:
UPDATE business_partners
SET phone = (SELECT phone FROM business_partners WHERE id = <Y>)
WHERE id = <X> AND phone IS NULL;
-- ... repeat for other fields identified as "losing record has data,
-- surviving record doesn't" in the prior report ...

-- Set the survivor's PartnerType to reflect both roles (customer/supplier
-- pairs only — skip this UPDATE for the 3 supplier/supplier pairs):
UPDATE business_partners SET partner_type = 'both' WHERE id = <X>;

-- Deactivate (do NOT delete) the merged-away record, with a clear trail:
UPDATE business_partners
SET is_active = 0,
    notes = CONCAT(COALESCE(notes, ''), ' [MERGED into id <X> on 2026-09-04]')
WHERE id = <Y>;

-- Verification query (run manually before COMMIT):
-- SELECT (SELECT COUNT(*) FROM invoices WHERE customer_id = <Y>) AS remaining_invoices_on_old_id,
--        (SELECT COUNT(*) FROM expense_invoices WHERE supplier_id = <Y>) AS remaining_expense_invoices_on_old_id;
-- Both should return 0 before committing.

-- COMMIT;  -- Deividas uncomments this after manually checking the verification query above
ROLLBACK;   -- safety default: script rolls back unless a human removes this line
```

Every block ends in `ROLLBACK` by default — Deividas will edit the file
to change `ROLLBACK` to `COMMIT` for pairs he's approved, per pair, after
reviewing. Do NOT make the script auto-commit anything.

## Step 4 — Do NOT touch `national_id_number` for the Tomas/Aurimas pair
Explicitly skip any field-backfill UPDATE for `national_id_number` in
those two specific merge blocks — leave a comment instead (see the TODO
requirement above).

## Output

Write the complete draft script to
`.opencode/reports/partner-merge-draft-<YYYYMMDD>-<HHMM>.sql` (a `.sql`
file, not `.md`) — and a short companion
`.opencode/reports/partner-merge-draft-<YYYYMMDD>-<HHMM>.md` explaining,
per pair: which id was chosen as survivor and why, what fields get
backfilled, and any open questions/uncertainties Deividas should check
before running it.

Do NOT run this SQL against any database — dev or prod. Do NOT modify
any application code in this task. This is a draft artifact for human
review only.

## Final step (required)

Run `./bump-version.sh patch` at the end of this task (no application
code changed, just a version marker).
