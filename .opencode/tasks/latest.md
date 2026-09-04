# Task: Full PartnerType code inventory + duplicate-pair decision brief

## Type: READ-ONLY INVESTIGATION (no code changes)

## Context
`Docs/PARTNER_TYPE_ARCHITECTURE_PLAN.md` (read this in full first,
especially section 7 — it has verified PROD numbers) proposes replacing
`PartnerType` with independent role-flag booleans. Before writing any
migration code, we need two things prepared for the human to review:

1. A genuinely COMPLETE inventory of every place `PartnerType` is used in
   code (not a partial/summarized list — actual file:line for every hit).
2. A decision-ready brief for each of the 7 confirmed customer/supplier
   duplicate pairs (listed in the plan doc, section 7.2), with enough
   detail that the human can decide "merge" or "keep separate" for each
   pair without having to go dig through the database himself.

## Part A — Complete PartnerType code inventory

- Grep the ENTIRE codebase (`.cs` and `.razor` files, excluding `bin/`
  and `obj/`) for: `PartnerType`, `partner_type`, and the raw string
  literals `"customer"`, `"supplier"`, `"expense_supplier"`, `"both"`
  (case-insensitive) — but only report string-literal hits that appear
  in a context plausibly related to business partners (skip obvious
  false positives like an unrelated "customer" in a comment about
  something else; use judgement, but err toward including borderline
  cases rather than silently dropping them).
- For every hit, produce a table row: file, line number, a short code
  snippet (the actual line, not a paraphrase), and classification:
  - EF LINQ comparison
  - Raw SQL string (`ExecuteSqlRawAsync`/`FromSqlRaw`/inline SQL text)
  - Razor UI conditional/binding
  - Enum/switch statement
  - Seed/test data
  - DbContext model configuration (`OnModelCreating`)
- Flag every "Raw SQL string" hit with **HIGH RISK** — these won't be
  caught by the C# compiler if the underlying column semantics change.
- Confirm the exact current definition of the `idx_partner_type` index
  and any other index/constraint on `business_partners` involving
  `PartnerType`, quoted from `NordicBeesErpContext.cs` `OnModelCreating`
  (not just described — quote the actual `HasIndex`/`HasKey` call).

## Part B — Decision brief for the 7 duplicate pairs

For EACH of these 7 pairs (ids from `Docs/PARTNER_TYPE_ARCHITECTURE_PLAN.md`
section 7.2 — do not re-derive them, use these exact ids):

| Name | customer id | supplier id |
|---|---|---|
| Zita Rutkauskienė | 12 | 294 |
| Vaidas Arbutavičius | 65 | 333 |
| Tomas Balčiūnas | 78 | 326 |
| AURIMAS BERNOTAS | 79 | 328 |
| Žilvinas Macijauskas | 85 | 185 |
| Regina Žilinskienė | 89 | 170 |
| LAIMUTIS ŽALALIS | 92 | 173 |

Using the project's normal read-only DB convention (write the exact
`mariadb` CLI SELECT statements needed; you may run them against the DEV
DB at `100.110.26.80` per the project's dev-DB read access convention,
since the human already confirmed dev and prod show identical duplicate
ids/pattern for these specific rows), gather for BOTH ids in each pair:

1. Full partner record: address, city, postal code, phone, email, bank
   account, VAT code, company code, national id number, payment terms,
   default VAT rate, default expense category, is_active, created_at.
2. Every invoice (`invoices` table) referencing that id as `customer_id`
   — invoice number, date, status, total amount.
3. Every payment (`payments` table) referencing that id as `customer_id`.
4. Every expense invoice (`expense_invoices`) referencing that id as
   `supplier_id`.
5. Every credit note (`credit_notes`) referencing that id.

Then, for each pair, write a short recommendation (2-4 sentences):
- Does the evidence support "clearly the same real-world person/entity,
  should be merged"? (e.g. identical address+phone, or identical VAT
  code like the Zita pair already confirmed)
- Or does something suggest they might legitimately be different
  contexts that shouldn't be merged?
- If data exists on BOTH sides (the 3 "split" pairs from the plan doc),
  explicitly note that a merge would require reassigning FKs from one id
  to the other, and estimate how many rows across which tables would
  need updating.

## Part C — Check for additional supplier/supplier name-duplicates
While gathering the above, also run this query and report results with
the same decision-brief treatment (name, address, VAT/company code
comparison, and FK counts) for any pairs found:

```sql
SELECT name, COUNT(*) AS cnt, GROUP_CONCAT(id) AS ids
FROM business_partners
WHERE partner_type IN ('supplier','expense_supplier')
GROUP BY name
HAVING COUNT(*) > 1;
```

## Output

Write the full report to
`.opencode/reports/partner-type-code-inventory-and-merge-brief-<YYYYMMDD>-<HHMM>.md`
with two clearly separated sections: "Part A — Code Inventory" and
"Part B/C — Duplicate Pair Decision Brief". Do NOT propose or write any
migration/merge SQL — this is discovery and decision-support only. Do
NOT modify any files or DB rows.

## Final step (required)

Run `./bump-version.sh patch` at the end of this task.
