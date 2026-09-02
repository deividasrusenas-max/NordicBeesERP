# Task: Apply standard filter bar design to /warehouse/deliveries

Type: BUILD (Razor page only)

## Context
`Docs/FILTER_STANDARDIZATION_PLAN.md` defines the one correct filter-bar
layout, based on `/invoices/sales` (`Components/Pages/Invoices.razor`). Read
that doc first for the exact markup pattern and the "What NOT to do" list.

## Target: `Components/Pages/Warehouse/DeliveryList.razor` (`/warehouse/deliveries`)

Current problems:
- Already wrapped in `<StandardListFilterBar>` (good, keep that), but:
- Column split is `md="6"` / `md="5"` / `md="1"` — change search and date
  range to `xs="12" sm="6" md="4"` each, matching the standard exactly (this
  page has only these 2 real filter fields, same shape as `/invoices/sales`).
- Inputs use `Variant="Variant.Outlined"` — change both to
  `Variant="Variant.Text"` per the standard.
- Clear filters is a bare `MudIconButton` with no label, always visible,
  crammed into a 1-column slot — replace with the standard pattern: a labeled
  `MudButton` ("Išvalyti filtrus", `StartIcon="@Icons.Material.Filled.FilterAltOff"`,
  `Variant="Variant.Text"`, `Color="Color.Secondary"`, `Size="Size.Small"`),
  rendered as its own `MudItem xs="12"`, and shown **only when a filter is
  currently active** (search non-empty OR date range set OR `_filterStatus`
  set — check existing filter state fields and adapt).

## Guardrails
- Do NOT change any data-loading logic, service calls, or filter *behavior*
  (what gets searched/filtered) — layout/wrapper/conditional-clear only.
- Do NOT touch the status-chip row below the filter card (the
  `_statusDisplay` foreach) — it already correctly sits outside the card.
- Do NOT remove or add any filter fields.
- Do NOT touch any other page, any service, model, or DB/migration.
- Update the "Conformance status" table in `Docs/FILTER_STANDARDIZATION_PLAN.md`
  — add a row for `/warehouse/deliveries` (DeliveryList.razor) marked ✅ once
  fixed (it wasn't in the table before; add it).

## Verification
- `dotnet build` → 0 errors.
- Grep the file to confirm no `Variant.Outlined` remains on the search/date
  fields, no `md="6"`/`md="5"`/`md="1"` split remains, and the clear button is
  a labeled `MudButton` inside a conditional `@if`.
- No Playwright/browser verification — Deividas checks the UI manually.
- End with `./bump-version.sh patch`.

## Output
Write a report to `.opencode/reports/filter-standard-deliveries-<timestamp>.md`
with a before/after summary, guardrail confirmation, build result, commit
hash, version bump result.
