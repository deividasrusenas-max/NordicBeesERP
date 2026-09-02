# Task: Apply standard filter bar design to 3 warehouse pages

Type: BUILD (Razor pages only)

## Context
`Docs/FILTER_STANDARDIZATION_PLAN.md` was just rewritten to define the one
correct filter-bar look, based on the reference page `/invoices/sales`
(`Components/Pages/Invoices.razor`). Read that doc first — it has the exact
markup pattern to copy, plus a "What NOT to do" list. Also read
`Components/Shared/StandardListFilterBar.razor` (the shared wrapper) and
`Components/Pages/Invoices.razor` lines ~110-135 for the literal reference
markup.

Fix these 3 pages so their filter bars conform to that standard:

## A. `Components/Pages/Warehouse/StockOverview.razor` (`/warehouse/stock`)

Current problems (per the doc's conformance table):
- Filter card is a bare `MudPaper Style="background:#f8fafc; border-radius:8px;"` —
  replace with `<StandardListFilterBar>`.
- Column split is custom (warehouse md3 / search md4 / date md3 / clear md2) —
  change to: warehouse dropdown `xs=12 sm=6 md=4`, search `xs=12 sm=6 md=4`,
  date range `xs=12 sm=6 md=4` (three roughly-even fields on this page since it
  has one extra dropdown beyond the standard two — keep them all inside the
  same `<StandardListFilterBar><MudGrid>`).
- "Išvalyti filtrus" is currently always visible — make it conditional
  (`MudItem xs="12"`, only rendered when warehouse filter, search, date range,
  or the status chip selection is non-default). Reuse/adapt whatever
  "is anything filtered" check already exists, or add one.
- Do NOT touch the status-chip row below the card (`_containerStatusDisplay`
  foreach) — it already correctly sits outside the filter card per the
  standard. Leave StockOverview's warehouse dropdown and status chips as-is
  functionally — this task is layout/wrapper only, not a field-removal task.

## B. `Components/Pages/Warehouse/WriteOff.razor` (`/warehouse/write-off`)

Current problems:
- Filter section is a bare `MudGrid` with no card at all — wrap it in
  `<StandardListFilterBar>`.
- Search field is `md="8"`, date range is `md="4"` — change both to
  `xs="12" sm="6" md="4"` to match the standard exactly (this page only has
  these 2 fields, so it's a direct copy of the reference pattern).
- "Išvalyti filtrus" is always visible — make it conditional on search
  non-empty OR date range set.

## C. `Components/Pages/Warehouse/WriteOffHistory.razor` (`/warehouse/write-off-history`)

Same fixes as (B): wrap in `<StandardListFilterBar>`, search + date range both
`xs="12" sm="6" md="4"`, make "Išvalyti filtrus" conditional.

## Guardrails
- Do NOT change any data-loading logic, service calls, or the underlying
  filter *behavior* (what gets searched/filtered) — this is purely visual/
  layout: card wrapper, column widths, conditional clear button.
- Do NOT remove or add any filter fields (no touching the warehouse dropdown
  on StockOverview, no re-adding status chips to the write-off pages).
- Do NOT touch any other page.
- Do NOT touch `ContainerService`, `IContainerService`, `Container.cs`, or any
  migration/DB.
- After the fix, update the "Conformance status" table in
  `Docs/FILTER_STANDARDIZATION_PLAN.md` — flip the 3 pages from ❌ to ✅.

## Verification
- `dotnet build` → 0 errors.
- Grep each of the 3 files to confirm `<StandardListFilterBar>` wraps the
  filter fields and no bare `MudPaper Style="background:#f8fafc"` filter card
  remains.
- Grep to confirm no `md="8"` remains on the WriteOff.razor/WriteOffHistory.razor
  search fields.
- No Playwright/browser verification — Deividas checks the UI manually.
- End with `./bump-version.sh patch`.

## Output
Write a report to `.opencode/reports/filter-standard-rollout-<timestamp>.md`
with a before/after summary per file, confirmation of the guardrails, build
result, commit hash, version bump result.
