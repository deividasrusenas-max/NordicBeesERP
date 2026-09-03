# Task: Restructure Suppliers & Customers table columns (Šalis, Balansas, Išlaidų grupė)

## Type: BUILD (code changes — includes one new service method, not just markup)

## Context
Follow-up to `.opencode/reports/suppliers-customers-columns-audit-20260904-1200.md`.
Read that report in full first — it has exact line numbers, current
column diffs, and confirms all backing data already exists (no missing
model fields).

## Target column layout

### `/customers` table
`Pavadinimas | Šalis | PVM | Mokėjimo term. | Balansas | Veiksmai`

### `/suppliers` table — ALL THREE tab variants (Ūkininkai / Įmonės / Visi)
`Pavadinimas | Šalis | PVM | Mokėjimo term. | Balansas | Išlaidų grupė | Veiksmai`

**Decision (already made, do not re-litigate):** unify all three Suppliers
tab table variants to this single column set. This means:
- Tab "Ūkininkai" (currently: Vardas / Asmens kodas / PVM% / Mokėjimo
  term. / Aktyvus / Veiksmai) loses its `Asmens kodas` column and gains
  `Šalis`, `Balansas`, `Išlaidų grupė`.
- Tab "Įmonės" (currently: Pavadinimas / Įm. kodas / PVM kodas /
  Numatyta kategorija / Mokėjimo term. / Aktyvus / Veiksmai) loses
  `Įm. kodas` and `PVM kodas` as separate text columns, and its existing
  `Numatyta kategorija` column becomes the `Išlaidų grupė` column (same
  data — `ExpenseCategory.Name` via `DefaultExpenseCategoryId` — do NOT
  render it twice; the audit's draft diff had it duplicated, that's a
  drafting mistake, fix it to appear once).
- Tab "Visi" (currently: Pavadinimas / Kodas / PVM% / Mokėjimo term. /
  Aktyvus / Veiksmai) loses `Kodas`, gains `Šalis`, `Balansas`,
  `Išlaidų grupė`.
- All three use `Name` for "Pavadinimas" header (rename "Vardas" header
  on the Ūkininkai tab to "Pavadinimas" for consistency).
- All three show `DefaultVatRate` as the "PVM" column (percentage,
  `N0` format + `%`), same as today.
- `PaymentTermDays` stays as "Mokėjimo term." (already present, no change
  needed to the underlying field).
- The existing "Aktyvus" IsActive chip column is REMOVED from the table
  (active/inactive filtering already lives in the external chip row from
  the previous filter-bar task — confirmed no dependency in the audit).

### `/customers` table changes
- Remove `Kodas` (CompanyCode) and `PVM kodas` (VatCode) columns.
- Add `Šalis` (Country).
- Keep `PVM%` (rename header to "PVM" for consistency with the spec) and
  `Mokėjimo terminas` (or align header text to "Mokėjimo term." to match
  Suppliers — pick one consistent label across both pages).
- Remove `Aktyvus` column (status filtering already external, per prior
  task).
- Add `Balansas`.

## Balance column — REQUIRED bulk implementation, not per-row

Per the audit: `DebtReconciliationService.GetReconciliationAsync` is
per-partner and would cause an N+1 query problem if called per table
row. Instead:

1. Add to `IDebtReconciliationService` (create this interface if it
   doesn't already exist — check first, don't duplicate) a new method:
   ```csharp
   Task<Dictionary<int, decimal>> GetBalancesBulkAsync(IEnumerable<int> partnerIds, int? year = null);
   ```
   Implement with a single aggregate query (or a small fixed number of
   queries) across Invoice/CreditNote/Payment tables, grouped by partner
   id — do not loop calling the existing single-partner method N times.
   Default `year` to the current year if null, matching the existing
   method's convention.
2. Register the interface in `Program.cs` if it's newly created.
3. In `Suppliers.razor` and `Customers.razor`: inject the service, call
   `GetBalancesBulkAsync` once after the partner list loads (in
   `OnParametersSet`/`LoadSuppliers`/`LoadCustomers`), store as
   `Dictionary<int, decimal> _balances`, and read from it in the row
   template via `_balances.TryGetValue(id, out var b) ? ... : "—"`.
4. **Sign convention (confirmed from existing code):** positive =
   partner owes us, negative = we owe partner. Render as e.g.
   `+1234.56 €` in a success-colored span when positive, `-1234.56 €` in
   an error-colored span when negative, and plain `0.00 €` (or "—") when
   zero. Use `MudText`/inline style with the project's existing color
   tokens from `Docs/DESIGN_SYSTEM.md` — do NOT hardcode new hex colors;
   reuse whatever class/token the project already uses for
   positive/negative amounts (check `FormatAmount` helper /
   `formatamount-trim-methods-20260825-1200.md` report for the existing
   pattern before inventing a new one).

## Do NOT
- Do not remove `CompanyCode`, `VatCode`, `NationalIdNumber` from the
  underlying models or from the edit dialogs — only from the LIST TABLE
  columns. They must remain editable in Create/Edit dialogs.
- Do not touch `FROZEN.md`-scoped files (not needed for this change per
  the audit).
- Do not add a new xUnit test for `GetBalancesBulkAsync` as mandatory —
  it's a read-only aggregate query, not a DB-write method, so the
  project's "every DB-write method needs a test" rule doesn't apply.
  Adding one is optional/nice-to-have, not required to finish this task.

## Verification (required before finishing)
- `dotnet build` — actual output pasted in the report, 0 errors.
- Manually confirm no N+1 query pattern was introduced (balances loaded
  once per page load, not per row).
- Reviewer subagent: quote every changed/added Lithuanian header string
  verbatim in its verdict (Šalis, PVM, Mokėjimo term., Balansas,
  Išlaidų grupė, Pavadinimas).
- Confirm `?vat=21` filter on `/suppliers` still works correctly with the
  new column set (manual check, no code change expected there).

## Report
Write the full work report to
`.opencode/reports/suppliers-customers-columns-fix-<YYYYMMDD>-<HHMM>.md`,
including actual `dotnet build` output and a diff summary per file.

## Final step (required)
Run `./bump-version.sh patch` at the end of this task. Before running it,
confirm `.opencode/tasks/latest.md` itself has no uncommitted diff
blocking the script (this blocked the previous filter-bar task — check
`git status --porcelain` first and report if it blocks again instead of
silently skipping the bump).
