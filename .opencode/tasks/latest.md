# Task: Phase 2 — service layer reads from new role-flag columns (with fallback)

## Type: BUILD (code changes) — service layer only, NOT UI, NOT DB writes

## Context
`Docs/PARTNER_TYPE_ARCHITECTURE_PLAN.md` (read in full, especially
sections 3, 5, and the new section 8) — Phase 0 (duplicate merge) and
Phase 1 (schema + backfill) are DONE and verified in PROD as of
2026-09-06. `business_partners` now has real, backfilled data in
`is_customer`, `is_supplier`, `is_expense_supplier`, `is_individual`,
alongside the still-untouched `partner_type` enum column.

Goal of THIS task: switch the SERVICE LAYER (not UI, not dialogs yet) to
read from the new boolean columns, while keeping backward-compatible
fallback behavior during the transition. This task does NOT touch
`Components/Pages/Suppliers.razor`, `Components/Pages/Customers.razor`,
or any dialog — those are Phase 3, separate tasks.

## Step 1 — Update the C# model read path
In `Models/Models_Part1.cs`, the `BusinessPartner` class already has the
4 new bool properties (added in Phase 1). Confirm they're still there
and match the DB columns exactly (`is_customer`, `is_supplier`,
`is_expense_supplier`, `is_individual` — snake_case in DB, PascalCase in
C#, standard EF convention already used elsewhere in this project).

## Step 2 — CustomerService.cs
Read the current file in full first. Find every place that filters or
checks `PartnerType` (e.g. `PartnerType == PartnerType.Customer ||
PartnerType == PartnerType.Both`, or the raw-SQL string comparison
flagged as HIGH RISK in
`.opencode/reports/partner-type-code-inventory-and-merge-brief-20260904-1700.md`
around line 106).

Change each to check `IsCustomer == true` (EF LINQ) or `is_customer = 1`
(raw SQL) INSTEAD of the enum comparison. Since Phase 1's backfill
already ran on all existing rows, `IsCustomer` should be authoritative —
but as a transition safety net, if you find a spot where relying purely
on the new flag feels risky (e.g. a code path that could plausibly
create a NEW row without going through a dialog that sets `IsCustomer`
yet — since dialogs aren't updated until Phase 3), add a fallback:
`IsCustomer == true || (IsCustomer == false && IsSupplier == false &&
IsExpenseSupplier == false && PartnerType == PartnerType.Customer)` —
i.e. only fall back to the old enum if ALL new flags are still at their
zero-default, meaning this is a brand-new row created before Phase 3
dialogs learned to set the new flags. Use judgement per call site; not
every comparison needs this — only ones for rows that could have been
created very recently (after Phase 1, before Phase 3).

## Step 3 — SupplierService.cs
Same treatment for `IsSupplier`. Pay special attention to the raw-SQL
`PartnerType.ToString().ToLower()` pattern flagged HIGH RISK in the
inventory report (around line 264) — this is the kind of place the C#
compiler won't catch if left stale, so verify it by hand.

## Step 4 — ExpenseService.cs and AssignSupplierDialog's underlying query
Do NOT touch `Components/Dialogs/AssignSupplierDialog.razor` itself in
this task (that's Phase 3 UI work) — but DO update whatever service
method backs its supplier-candidate query if it lives in a service class
rather than inline in the Razor file. If the filter
`PartnerType == PartnerType.ExpenseSupplier || PartnerType == PartnerType.Both`
lives in a service method, change it to `IsExpenseSupplier == true`.
This directly fixes the real production gap found in section 7.3 of the
plan (31/104 real expense-invoice supplier assignments pointed at
partners whose enum type wouldn't have matched this filter — the new
`IsExpenseSupplier` flag was specifically backfilled to include those,
so switching this filter is expected to noticeably widen the visible
candidate list — that's correct and intended, not a bug).

`AutoAssignSupplierAsync` itself does NOT filter by PartnerType (confirmed
in a prior session by reading the file directly) — leave it as-is, no
change needed there.

## Step 5 — Other services
Grep for any other `.cs` service file referencing `PartnerType` that
wasn't covered above (check `DebtReconciliationService.cs`, any
`Reports/`-adjacent service classes, and anything else the Phase 1 code
inventory report flagged). Apply the same pattern: prefer new flags,
add the narrow fallback only where a brand-new unmigrated row is
plausible.

## Step 6 — Do NOT touch
- Any `.razor` file (Phase 3).
- `PartnerType` property itself, or its enum definition — stays as-is.
- Any DB migration or data.
- `AssignSupplierDialog.razor`'s own Razor markup/query if it's inline
  in the component rather than in a service — flag this in your report
  instead of changing it, so it's queued correctly for Phase 3.

## Verification (required before finishing)
- `dotnet build` — actual output pasted in the report, 0 errors.
- Confirm via a manual read-through that no `.razor` file was modified.
- List every file changed and, for each, quote the before/after for the
  specific `PartnerType` → new-flag switch (not the whole diff, just the
  relevant lines) so a human can review the semantic change easily.
- Confirm the fallback logic (where added) actually compiles and reads
  correctly — walk through what happens for: (a) an old row with
  `PartnerType=Customer, IsCustomer=1` (should just work via the new
  flag), (b) a hypothetical brand-new row created between Phase 1 and
  Phase 3 with `PartnerType=Customer, IsCustomer=0` (should still be
  found via fallback), (c) an old row with `PartnerType=Both,
  IsCustomer=1, IsSupplier=1` (should appear in both customer and
  supplier queries, same as before).

## Report
Write to
`.opencode/reports/partner-role-flags-service-layer-<YYYYMMDD>-<HHMM>.md`
with the file list, before/after snippets, build output, and the
walk-through from the verification step.

## Final step (required)
Run `./bump-version.sh patch`.
