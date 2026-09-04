# Task: Migration risk assessment for PartnerType → role-flags redesign

## Type: READ-ONLY INVESTIGATION (no code changes) — DATA RISK FOCUS

## Context
`Docs/PARTNER_TYPE_ARCHITECTURE_PLAN.md` proposes replacing the single
`PartnerType` enum with independent boolean role flags (`IsCustomer`,
`IsSupplier`, `IsExpenseSupplier`, `IsIndividual`), based on how SAP and
Odoo model business partners.

Before any schema/code change, we need a HONEST risk assessment specific
to THIS database and codebase — the plan must not be taken on faith. The
user is specifically worried about (1) existing invoices/expense invoices
that reference suppliers and their expense category assignments, and
(2) whether the migration could silently break something. Find out for
real, don't assume the plan is safe.

## Critical questions to answer with evidence (file:line, or actual SQL
query results if you run read-only queries against the dev DB per the
project's normal read-only DB conventions)

### 1. Duplicate-record risk (HIGHEST PRIORITY)
The old `PartnerType` enum could only hold ONE value per row. If a single
real-world business needed to be BOTH a goods supplier AND an expense
supplier, the only way to represent that under the old model may have
been to create TWO separate `BusinessPartner` rows for the same entity
(one `Supplier`, one `ExpenseSupplier`).

- Query (or write the exact SQL for the human to run) to find pairs of
  `business_partners` rows that share the same `VatCode` or `CompanyCode`
  or a near-identical `Name`, but have different `PartnerType` values.
  Report every such pair found, with their ids, names, and types.
- If duplicates exist: check whether they have DIFFERENT sets of
  invoices/expense invoices/payments attached (i.e. real historical data
  split across the "same" business partner) — this determines whether a
  future merge step would need to reassign foreign keys.

### 2. Do invoices/expense invoices snapshot partner type, or read it live?
- Check `Invoice`, `ExpenseInvoice`, `CreditNote`, `Payment` models for
  any column that stores a copy of the partner's type/category at
  document-creation time (similar to the known `customer_vat_code`
  snapshot pattern already in this project).
- If no snapshot exists, confirm every report/PDF/list that displays
  "Klientas"/"Tiekėjas" for a historical document is reading it live from
  `BusinessPartner.PartnerType` at render time (meaning old documents
  would automatically reflect new fields with no migration needed for
  display) — find the actual query and quote it.

### 3. Full inventory of `PartnerType` usage (not the partial list from
   the prior audit — a genuinely complete grep)
- Grep the entire codebase (all `.cs` and `.razor` files) for:
  `PartnerType`, `partner_type`, `"customer"`, `"supplier"`,
  `"expense_supplier"`, `"both"` (case-insensitive, scoped to files that
  also reference `BusinessPartner`/`Supplier`/`Customer` to avoid noise).
- For EVERY hit, classify it as: (a) EF LINQ comparison, (b) raw SQL
  string (`ExecuteSqlRawAsync`/`FromSqlRaw`), (c) UI conditional
  (Razor `@if`/binding), (d) enum/switch statement, (e) seed/test data.
- Flag anything in category (b) — raw SQL is NOT caught by the compiler
  if the column semantics change, so these are the highest silent-break
  risk.
- Check `NordicBeesErpContext.cs` `OnModelCreating` for any unique
  constraint or index involving `PartnerType` (the user's memory notes a
  composite index on `(IsActive, PartnerType)` — confirm exact definition
  and whether any UNIQUE constraint depends on it, which would affect
  whether duplicate-role rows are even allowed today).

### 4. Expense supplier matching logic — exact current behavior
- Read `ExpenseService.AutoAssignSupplierAsync` in full (not just the
  audit's summary) and quote the EXACT current filter/candidate-selection
  logic — does it filter candidate suppliers by `PartnerType ==
  ExpenseSupplier || PartnerType == Both` today? Confirm precisely.
- Read `AssignSupplierDialog` and `ResolveSupplierDialog` in full for the
  same — exact current filter predicates, quoted.
- Determine: if candidate filtering changed from
  "`PartnerType == ExpenseSupplier`" to "`IsExpenseSupplier == true`",
  would the CANDIDATE SET actually change for current production data,
  or would it be identical (i.e. is this a safe no-op rename, or would it
  suddenly surface previously-hidden goods suppliers as expense-invoice
  matches)? Answer with actual counts from the dev DB if possible
  (e.g. `SELECT COUNT(*) FROM business_partners WHERE partner_type =
  'expense_supplier'` vs what a hypothetical `IsExpenseSupplier` backfill
  would produce).

### 5. DefaultExpenseCategoryId — current real usage on existing data
- Query (or provide SQL): how many `ExpenseInvoice` rows currently have a
  `CategoryId` that was auto-assigned from a supplier's
  `DefaultExpenseCategoryId` vs manually set vs still null? (If this
  can't be determined precisely from data alone, say so and explain what
  would be needed to find out.)
- Confirm: does any report (XLSX/PDF) group or total expenses BY
  supplier's CURRENT `DefaultExpenseCategoryId` rather than by the
  invoice's own stored `CategoryId`? If so, that's a live dependency
  that would be affected by any future category-reassignment feature —
  quote the exact code.

### 6. Blast radius outside Suppliers/Customers/Expense pages
- Grep for `PartnerType` usage in: `Reports/` folder (DebtReconciliation,
  SalesByCustomer, UnpaidInvoices, etc.), any dashboard/KPI snapshot code,
  and `Services/SearchBusinessPartners`-style search endpoints.
- For each hit found outside the pages already covered by the prior
  audit, note it — these are the ones NOT yet accounted for in the
  migration plan.

## Honest verdict (required section in the report)

After gathering the above, give a direct, non-diplomatic answer to:
**"Is the role-flags migration safe to do incrementally as planned, or
does the duplicate-record risk (question 1) mean a data-merge step must
happen FIRST, before any schema change?"**

If duplicates are found: do NOT propose the merge implementation in this
task — just quantify the problem (how many pairs, how much historical
data on each side) so the human can decide whether to merge, keep both
records, or take a different approach entirely.

## Output

Write the full report to
`.opencode/reports/partner-type-migration-risk-<YYYYMMDD>-<HHMM>.md`.

Do NOT modify any files. Do NOT write any migration or backfill SQL in
this task — this is risk discovery only.

## Final step (required)

Run `./bump-version.sh patch` at the end of this task (no code changed,
just a version marker for the investigation checkpoint).
