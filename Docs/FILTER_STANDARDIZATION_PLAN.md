# Filter Bar Design Standard

**Reference implementation:** `/invoices/sales` (`Components/Pages/Invoices.razor`) — screenshot-verified 2026-09-03.
**Shared wrapper component:** `Components/Shared/StandardListFilterBar.razor`

This document defines the one and only correct look/layout for list-page filter
bars. It replaces the 2026-08-18 audit document (deleted below — that table is
now stale and partly contradicts decisions made since, e.g. the write-off
module redesign removed status chips it had recommended adding).

---

## The standard, exactly

### 1. Page header
```razor
<MudText Typo="Typo.h5" Class="mb-3">@* Page title *@</MudText>
```
Primary action button (if the page has one, e.g. "Sukurti sąskaitą") goes on
its own row right after the title, left-aligned, `Variant.Filled`, `Class="mb-4"`.

### 2. Filter card — MUST use `<StandardListFilterBar>`
Never use a bare `MudPaper`/`MudGrid` for the filter card — always wrap in the
shared component so every page gets the identical background/rounding/padding:

```razor
<StandardListFilterBar>
    <MudGrid>
        <MudItem xs="12" sm="6" md="4">
            <MudTextField Value="@_filterSearch"
                          ValueChanged="@(async (string val) => await UpdateFilterSearchAsync(val))"
                          Label="Paieška ..." <!-- describe exactly what's searched -->
                          Variant="Variant.Text"
                          Immediate="true"
                          FullWidth="@true"
                          Clearable="true"
                          AdornmentIcon="@Icons.Material.Filled.Search"
                          Adornment="Adornment.Start" />
        </MudItem>
        <MudItem xs="12" sm="6" md="4"> <!-- only if the entity has a date axis -->
            <MudDateRangePicker DateRange="@_dateRange"
                                 DateRangeChanged="@(async (DateRange dr) => await UpdateDateRangeAsync(dr))"
                                 Label="Data (nuo / iki)"
                                 Variant="Variant.Text"
                                 FullWidth="@true"
                                 Clearable="true" Editable="true" DateFormat="yyyy-MM-dd" />
        </MudItem>
        @if (/* any filter is currently active */)
        {
            <MudItem xs="12">
                <MudButton StartIcon="@Icons.Material.Filled.FilterAltOff"
                           Variant="Variant.Text"
                           Color="Color.Secondary"
                           Size="Size.Small"
                           OnClick="@(async () => await ClearFiltersAsync())">
                    Išvalyti filtrus
                </MudButton>
            </MudItem>
        }
    </MudGrid>
</StandardListFilterBar>
```

Rules:
- Search field and date field are each `xs="12" sm="6" md="4"`. Do not use
  `md="8"` or any other split for these two fields.
- If a page has more than 2 filter fields (e.g. an extra dropdown), keep every
  field inside the same `<StandardListFilterBar><MudGrid>`, sized so widths
  are proportionate and everything wraps cleanly at `sm`/`md` — but the
  search box and date range keep their `sm=6 md=4` sizing regardless of what
  else is on the row.
- **"Išvalyti filtrus" is conditional, never permanently visible.** Show it
  only when at least one filter (search, date, dropdown, status/tab) is
  currently non-default. It always renders as `MudItem xs="12"` so it drops
  to its own line.
- Inputs always use `Variant="Variant.Text"` (the underlined style seen in the
  reference), never `Variant.Outlined`, inside the filter card.

### 3. Quick-filter tabs / status chips — OUTSIDE the card
If a page has quick-filter tabs (e.g. "Visos / Neapmokėtos / Vėluojančios /
Šis mėnuo") and/or status chips, they render **below** `StandardListFilterBar`,
in their own row, never inside the card:

```razor
<div class="d-flex flex-wrap gap-2 mb-3 align-center">
    <!-- MudChip per tab/status, Variant.Filled when selected else Variant.Outlined -->
</div>
```
Only add this row if the page's entity actually has a meaningful status/tab
axis to filter on. Don't add chips for statuses nothing in the code ever sets
(see write-off module investigation — RESERVED/IN_PRODUCTION/SOLD/RETURNED are
dead statuses; no chip row was added back for the write-off pages for this
reason).

---

## What NOT to do
- No bare `MudPaper Style="background:#f8fafc..."` filter card — use
  `<StandardListFilterBar>` every time, everywhere. Hardcoded background
  hex colors on filter cards are a violation of this standard even if the
  visual result looks similar.
- No `md="8"` (or other custom split) for the search field.
- No always-visible "Išvalyti filtrus" — must be conditional.
- No extra dropdown filters (warehouse, honey-type, etc.) unless the page
  genuinely needs them — prefer one unified search box over multiple
  dropdowns where reasonable (established during the write-off redesign).

---

## Conformance status (2026-09-03)

| Page | Status |
|---|---|
| `/invoices/sales`, `/invoices/purchases` (Invoices.razor) | ✅ Reference — already conforms |
| `/warehouse/stock` (StockOverview.razor) | ✅ Conforms — `StandardListFilterBar`, fields `xs=12 sm=6 md=4`, conditional `Išvalyti filtrus` (2026-09-03) |
| `/warehouse/deliveries` (DeliveryList.razor) | ✅ Conforms — `StandardListFilterBar`, search/date `xs=12 sm=6 md=4`, `Variant.Text`, labeled conditional `Išvalyti filtrus` (2026-09-03) |
| `/warehouse/write-off` (WriteOff.razor) | ✅ Conforms — `StandardListFilterBar`, search/date `xs=12 sm=6 md=4`, conditional `Išvalyti filtrus` (2026-09-03) |
| `/warehouse/write-off-history` (WriteOffHistory.razor) | ✅ Conforms — `StandardListFilterBar`, search/date `xs=12 sm=6 md=4`, conditional `Išvalyti filtrus` (2026-09-03) |
| All other list pages | Not yet audited against this version of the standard — audit and fix opportunistically when touching a page, or in a dedicated future pass |

Rollout of this standard to the remainder of the app is intentionally
incremental — fix a page's filter bar whenever it's being worked on for
another reason, rather than a risky big-bang pass across ~20 files at once.
