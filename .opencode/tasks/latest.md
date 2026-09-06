# Task: Phase 3 Round 2 — Suppliers.razor tab filters switch to role flags

## Type: BUILD (code changes) — one file's filter logic + verification via full test suite against BOTH dev and isolated test DB

## Context
`Docs/PARTNER_TYPE_ARCHITECTURE_PLAN.md` sections 4.2, 8, 9, 10 — Phases
0/1/2 and Phase 3 Round 1 are DONE and verified (v0.17.51). This is the
last piece of Phase 3: switch `Suppliers.razor`'s tab filter logic from
the `NationalIdNumber`/`CompanyCode` heuristic to the real `IsIndividual`
flag. Round 3 (dialog unification) stays deferred — not part of this
task.

## Step 1 — Read the current filter, verbatim, before changing anything
Read `Components/Pages/Suppliers.razor` in full. The current
`FilteredSuppliers` property (documented in
`.opencode/reports/partner-type-phase3-ui-spec-20260906-0750.md` §2.1,
but RE-READ the live file since it may have shifted lines since that
report) filters:
```
1 (Ūkininkai) => suppliers?.Where(s => !string.IsNullOrEmpty(s.NationalIdNumber))
2 (Įmonės)    => suppliers?.Where(s => !string.IsNullOrEmpty(s.CompanyCode) || (both empty))
_ (Visi)      => suppliers (no filter)
```
Confirm current exact line numbers before editing.

## Step 2 — Switch to flag-based filtering
Replace the tab predicate with:
```
1 (Ūkininkai) => suppliers?.Where(s => s.IsIndividual)
2 (Įmonės)    => suppliers?.Where(s => !s.IsIndividual)
_ (Visi)      => suppliers (no filter — already IsSupplier/IsExpenseSupplier-filtered at the service layer per Phase 2)
```
Keep the `_isWarehouse` special-case branch (forces tab 1) working the
same way, just swap its predicate to `s.IsIndividual` too, for
consistency.

Keep everything else in `FilteredSuppliers` unchanged: search text
filter, `_selectedVatRates` filter, `_filterActive` filter on tab 2 —
these don't depend on the type-detection heuristic and should not
change.

## Step 3 — Confirm `Supplier` DTO actually carries `IsIndividual`
This was added in Phase 3 Round 1 (`Models_Part2.cs`) and is already
populated by `SupplierService.GetSuppliersAsync()` (confirmed in a prior
session by reading the file directly — it maps `IsIndividual = bp.IsIndividual`).
Just confirm this is still true; do not re-add it if already present.

## Step 4 — Do NOT touch
- `Customers.razor` — already correct per Phase 2 (no tabs, filters via
  `IsCustomer` at the service layer).
- Any dialog file (Round 1 already handled these).
- Vaidas Arbutavičius's (id 65) `is_individual` data value — this is a
  known, human-only data correction (plan §10), not a code change. He
  will appear under Įmonės after this change until manually corrected;
  that's expected, not a bug in this task's scope.
- The service-layer `GetSuppliersAsync`/`GetAllSuppliersAsync` filters
  themselves (Phase 2, already correct).

## Step 5 — Full verification, BOTH database contexts (harness testing)
This project has learned (this session) that the isolated test DB can
silently drift from dev/prod schema. Do the following IN ORDER and
report the actual output of each:

1. `dotnet build` — 0 errors.
2. `dotnet test` **without** any `TEST_DB_CONNECTION` override first
   (uses whatever default connection string the test project falls back
   to) — report the actual pass/fail count and any connection errors.
3. `dotnet test` **with** the isolated test DB explicitly:
   ```
   TEST_DB_CONNECTION="Server=100.110.26.80;Port=3306;Database=nordic_bees_erp_test;Uid=erp_user;Pwd=NordicBees2024;SslMode=none;AllowPublicKeyRetrieval=True;" dotnet test
   ```
   Report the actual pass/fail count. If ANY test fails with an
   `Unknown column` or similar schema-drift error, STOP and report it
   clearly — do NOT attempt to fix the test DB schema yourself (DDL is
   human-only per FROZEN.md); just tell the human exactly which
   column/table is missing, the same way the human resolved the
   `is_customer`/`barrel_cost_deduction` gaps in the prior session.
4. Manually verify (read-only DB query, dev DB) that the tab logic will
   produce sane results — e.g.
   `SELECT COUNT(*) FROM business_partners WHERE is_supplier=1 AND is_individual=1`
   vs `is_individual=0`, so the report can state how many suppliers will
   land in each tab after this change, for a sanity gut-check against
   what the human would expect to see when they open the page.

## Verification checklist for the report
- Exact before/after of the `FilteredSuppliers` switch expression.
- Confirmed line numbers.
- `dotnet build` output.
- `dotnet test` output for BOTH runs (with and without TEST_DB_CONNECTION).
- The tab-count sanity query result from Step 5.4.
- Reviewer verdict, quoting the exact diff reviewed.

## Report
Write to
`.opencode/reports/partner-role-flags-phase3-round2-<YYYYMMDD>-<HHMM>.md`.

## Final step (required)
Run `./bump-version.sh patch`. If it's blocked by unrelated uncommitted
files (as happened twice this session with `.opencode/tasks/latest.md`,
`Docs/PARTNER_TYPE_ARCHITECTURE_PLAN.md`, `Docs/BUGLOG.md`), report
exactly which files are blocking it — do not commit anything you did not
yourself change in this task without flagging it to the human first.
Use the `TEST_DB_CONNECTION` env var when running bump-version.sh if its
own internal test gate requires it (confirmed necessary in the prior
session).
