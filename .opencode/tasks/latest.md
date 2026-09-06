# Task: Bug fix — "Numatyta išlaidų kategorija" shows category ID instead of name after save

## Type: INVESTIGATE THEN FIX (small, scoped)

## Symptom (reported by human, 2026-09-06)
After saving a supplier (via `SupplierEditDialog` or `SupplierCreateDialog`),
the "Numatyta išlaidų kategorija" (default expense category) field/display
shows a raw number (the `DefaultExpenseCategoryId` value) instead of the
category's name (e.g. "Transportas", "Komunalinės paslaugos"). This is a
display bug — investigate where it manifests exactly before fixing.

## Step 1 — Reproduce and locate exactly where the number appears
Check ALL of these locations, since the symptom could be in any one (or
more) of them — read each file's relevant section and report which ones
actually show the raw ID vs the resolved name:

1. `Components/Dialogs/SupplierEditDialog.razor` — the expense category
   field itself, right after a save completes (does the dialog stay open
   and re-render with stale data, or does it close and the LIST behind it
   show the bug?).
2. `Components/Pages/Suppliers.razor` — the "Išlaidų grupė" column added
   during the earlier columns-restructure work
   (`.opencode/reports/suppliers-customers-columns-fix-20260904-*.md`) —
   check how it resolves `DefaultExpenseCategoryId` to a display name, and
   whether that resolution only happens on initial page load but not
   after a save-triggered reload (e.g. a `Dictionary<int,string>` built
   once and not refreshed, or a missing `.Include()`/separate lookup call
   after `SaveSupplierAsync` returns).
3. Confirm whether `Supplier` DTO has a `DefaultExpenseCategoryName` (or
   similarly named) property that's supposed to carry the resolved name,
   and whether `SupplierService.SaveSupplierAsync`'s return value (or the
   subsequent reload call) actually populates it — per this project's
   known `[NotMapped]` navigation property gotcha (EF `Include()` silently
   returns empty for these — always loaded separately via dedicated
   service methods, per `FROZEN.md`/skill notes), check if this is exactly
   that pattern biting here.

## Step 2 — Root-cause and fix
Once located, fix the root cause — likely one of:
- The post-save reload path doesn't re-fetch/join the expense category
  name (fix: call the correct dedicated lookup method after save, per the
  `[NotMapped]` navigation convention already established in this
  codebase).
- The dialog's own local state after `SaveSupplierAsync` sets the field
  back to showing `Id` instead of keeping/re-resolving the name for
  display (fix: re-populate the display-name field from the already-known
  `ExpenseCategories` list the dialog already loads for its dropdown,
  matching by `Id`).

Keep the fix minimal and scoped — do not refactor unrelated expense
category code.

## Step 3 — Verification
- `dotnet build` — 0 errors.
- `dotnet test` — both with and without `TEST_DB_CONNECTION`, same as the
  prior Phase 3 verification pattern.
- Manually confirm (read-only DB query, dev) a sample supplier's
  `default_expense_category_id` and cross-reference against
  `expense_categories.name` (or whatever the actual table/column is
  called — confirm real names first) to state in the report exactly what
  name SHOULD have displayed for the case that triggered this bug report.

## Report
Write to
`.opencode/reports/expense-category-name-display-fix-<YYYYMMDD>-<HHMM>.md`
with: exact location(s) of the bug, root cause, before/after code, and
verification output.

## Final step (required)
Run `./bump-version.sh patch`.
