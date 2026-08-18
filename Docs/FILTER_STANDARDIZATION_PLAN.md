# Filter UI/UX Standardization Plan

**Reference standard:** Sales Invoices page — `Components/Pages/Invoices.razor` (`/invoices/sales`).
**Audit date:** 2026-08-18
**Scope:** AUDIT ONLY — no code changes made. This document is the deliverable.

---

## Summary

### Is the filter bar a shared component? (STEP 1)

**No.** The Sales Invoices filter bar is **inline markup duplicated per page**, not a reusable Razor component:

- **Search box** (`MudTextField` "Paieška pagal Nr. arba klientą") — inline markup, lines `57-66` of `Invoices.razor`.
- **Date range** (`MudDateRangePicker`) — inline markup, lines `67-74`.
- **Status chips** (`MudChip` + `ToggleStatusAsync`, driven by `_selectedStatuses` / `_availableStatuses`) — inline markup, lines `91-113`.
- **Quick-filter tabs** (`MudTabs` with "Visos / Nesumokėtos / Vėluojančios / Šis mėnuo") — inline markup, lines `116-123`.
- **Clear-filters action** (`ClearFiltersAsync` / "Išvalyti filtrus") — inline `MudButton`, lines `75-86` + `93-100`.

The **only** shared piece is `Helpers/FilterUrlBuilder.cs` — a helper that serializes/parses the URL query string (`q`, `from`, `to`, `status`, `tab`, `type`, `id`). It is used by 16 pages (see "Has URL persistence" column below). This helper was already extracted in a prior effort to kill the 6-page URL-duplication incident.

### Recommendation: extract now vs. keep copy-per-page

**Do NOT extract a monolithic `<FilterBar>` component right now.** Rationale / trade-off:

- **Effort:** A single component that reproduces the full Invoices bar would need many parameters/templates — type tabs, status-chip set, quick-filter tab set, date-range, and a clear button — because the *semantics* differ per page (Invoices has type tabs + status chips + 4 quick tabs; other pages have subsets or different fields). That is a high-abstraction-cost, high-risk refactor touching ~16 files for purely visual consistency gain.
- **Risk:** Big-bang extraction risks regressions in the interactive filter callbacks and the `FilterUrlBuilder` URL contract that each page already relies on.
- **What is already solved:** The riskiest part (URL serialization, the thing that actually caused the prior duplication bug) is already centralized in `FilterUrlBuilder`. Remaining duplication is low-risk presentational markup.

**Recommended hybrid instead:**

1. Extract **two small, focused, opt-in components** that are *identical everywhere* and have no per-page semantics:
   - `<FilterSearchBox>` (label + `MudTextField` + search icon + `Clearable`) — same on every page.
   - `<StatusChipGroup>` (label "Būsena:" + `MudChip` set + toggle callback + "Rodyti visas" reset) — takes `availableStatuses` + an `OnToggle(status)` callback as parameters.
   - Optionally `<ClearFiltersButton>` if a shared clear affordance is wanted.
2. Keep **date range** and **quick-filter tabs** inline, because their field set and meaning genuinely vary per page (e.g. a product list needs no date range; Invoices tabs mean "overdue/this-month" while Orders tabs would mean something else).
3. Apply the *consistent visual style* (the `MudPaper` wrapper with `background:#f8fafc; border-radius:8px`, the `MudGrid` column layout, chip colors via `StatusDisplayHelper`) page-by-page during the standardization work, reusing `FilterUrlBuilder` for persistence.

This gives visual consistency and removes the truly identical markup, while avoiding a risky monolith. Effort is moderate, risk is low because each page is changed independently and keeps its own filter state/URL contract.

---

## Methodology note

- "Has search / date / status chips / tabs / clear / URL" columns are derived from automated grep markers across each `.razor` file (presence of `MudTextField`+search adornment, `MudDateRangePicker`/`DateRange`, interactive status-filter logic `_selectedStatuses`/`ToggleStatus`/`StatusCsv`, `MudTabs`/`MudTabPanel`, clear affordance `ClearFilters`/`Išvalyti`/`FilterAltOff`, and `FilterUrlBuilder` usage respectively).
- Where a marker could be a row-display badge rather than an interactive filter (e.g. a `MudChip` used only to color a status in a table row), it is flagged in **Notes** as "needs closer review."
- "Recommended fields to add" lists only **real, currently displayed/queryable columns** from each page's `MudTh` headers (captured during the audit) — no invented fields.
- Rows are ordered **least compliant first** (fewest of the 6 features present) so the highest-value pages to standardize are at the top.

---

## List-page audit table

Legend: present / absent / partial-or-needs-closer-review

| # | Page / Route | Has search | Has date range | Has status chips | Has tabs | Has clear-filters | Has URL persistence | Recommended fields to add | Notes |
|---|--------------|:--:|:--:|:--:|:--:|:--:|:--:|------------------------|-------|
| 1 | `/warehouse/transfers` (TransferHistory) | no | no | no | no | no | no | search (Nr / is-i partner), date range (Data) | No filters at all. Columns: Nr, Data, Is, I, Vnt, Netto. Add search + date range + URL persistence. |
| 2 | `/warehouse/write-off-history` (WriteOffHistory) | no | no | no | no | no | no | search (Tiekejas / Ruis), date range (Data), warehouse filter | No filters. Columns: ID, Tipas, Tiekejas, Ruis, Netto, Sandelis, Pastabos, Data. |
| 3 | `/inventory` (Inventory) | no | no | no | no | no | no | search (Kodas / Pavadinimas), expiry-date filter (Galioti iki), batch/LOT filter | No filters. Columns: Kodas, Pavadinimas, Kiekis, Likutis, Atnaujinta, LOT, Galioti iki. |
| 4 | `/admin/users` (Users) | no | no | no | no | no | no | search (El. pastas / Vardas), role + active chips | Small list, no filters. Columns: El. pastas, Vardas, Role, Aktyvus. |
| 5 | `/warehouses` (Warehouses) | no | no | no | no | no | no | search (Kodas / Pavadinimas), active chip | No filters. Columns: Kodas, Pavadinimas, Tipas, Adresas, Miestas, Salis, Aktyvus. |
| 6 | `/warehouse/deliveries/{Id:int}` (DeliveryView) | no | no | no | no | no | yes | search (Preke), date range, supplier filter | URL persistence present (FilterUrlBuilder) but no on-page filters. Columns: Preke, Tipas, Kiekis, Netto, Brutto, Tara, Statusas. Verify whether filters make sense on a per-delivery detail view. |
| 7 | `/warehouse/supplier-debts` (SupplierDebts) | no | no | no | partial | no | no | search (Tiekejas), date range, tabs (apmoketa / nesumoketa) | Has MudTabs (verify if quick-filter tabs vs section tabs). Columns: Tiekejas, Pristatymai, Netto, Suma, Sumoketa, Skola. |
| 8 | `/warehouse/deliveries` (DeliveryList) | no | no | no | no | yes | yes | search (Numeris / Tiekejas), status chips (Busena), date range | Has clear + URL persistence but no actual filter controls. Columns: Data, Numeris, Tiekejas, Netto, Suma, Sumoketa, Skola, Busena, Saskaita. |
| 9 | `/settings/honey-types` (HoneyTypes) | yes | no | no | no | no | no | active chip (Aktyvus) | Search present; no status/active chip, no URL persistence. Columns: Kodas, Pavadinimas, EN, Aktyvus. |
| 10 | `/bank-import` (BankImport) | yes | no | no | no | no | no | date range (import Data), status chips (Statusas) | Import workflow page; search present. Special semantics — confirm filter scope makes sense before standardizing. |
| 11 | `/products` (Products) | yes | no | no | no | no | yes | active chip (Aktyvus), type/category chip (Tipas) | Search + URL present; no status chip filter, no clear, no tabs, no date (acceptable — products have no date axis). Columns: Kodas, Tipas, Pavadinimas, EAN, Vienetas, Savikaina, Pardavimo kaina, Aktyvus. |
| 12 | `/customers` (Customers) | yes | no | no | no | no | yes | active chip (Aktyvus) | Search + URL present; no status/active chip, no clear. Columns: Pavadinimas, Kodas, PVM kodas, PVM%, Mokejimo terminas, Aktyvus. |
| 13 | Embedded "Saskaitu mokejimu sarasas" (InvoicePaymentList — no @page, embedded component) | yes | yes | partial (MudSelect not chips) | no | no | yes | convert status MudSelect to status chips to match standard | Uses MudSelect for payment status (unpaid/partial/paid) instead of chips; uses MudDatePicker from/to. Columns: Nr, Klientas, Data, Terminas, Suma, Sumoketa, Like, Busena. Being embedded (no route), URL persistence wiring may differ — needs closer review. |
| 14 | `/warehouse/write-off` (WriteOff) | yes | no | no | no | yes | yes | status chips (Statusas), date range, supplier filter (Tiekejas) | Search + clear + URL present. Columns: ID, Tipas, Tiekejas, Ruis, Netto, Statusas. |
| 15 | `/warehouse/stock` (StockOverview) | yes | no | no | no | yes | yes | status chips (Statusas), warehouse filter (Sandelis), date range (Data) | Search + clear + URL present. Columns: ID, Tipas, Tiekejas, Ruis, Brutto, Tara, Netto, Statusas, Sandelis, Data. |
| 16 | `/expense-payments` (ExpensePayments) | yes | yes | no | no | yes | yes | status chips (payment method/status), tabs (e.g. by method) | Search + date + clear + URL present; no status chips, no tabs. Columns: Data, Tiekejas, Saskaita Nr, Suma, Metodas, Reference, Pastabos. |
| 17 | `/payments/history` (PaymentHistory) | yes | yes | no | no | yes | yes | status chips (Budas / paid status), tabs (veluojancios / visos) | Server-side sorted MudTable. Search + date + clear + URL present; no status chips/tabs. Columns: Saskaitos Nr, Apmokejimo data, Velavimas, Suma, Klientas, Budas. Needs closer review of filter field semantics. |
| 18 | `/suppliers` (Suppliers) | yes | no | no | partial | no | yes | active chip (Aktyvus), clear-filters button | Search + tabs (verify if quick-filter tabs) + URL present; no interactive status chip, no clear. Columns: Vardas, Asmens kodas, PVM%, Mokejimo term, Aktyvus. |
| 19 | `/credit-notes` (CreditNotes) | yes | yes | yes | no | yes | yes | quick-filter tabs (e.g. nesumoketos / sis menuo) | Search + date + status chips + clear + URL present; missing only tabs. Columns: Nr, Klientas, Data, Suma, Busena. |
| 20 | `/orders` (Orders/Index) | yes | yes | yes | no | yes | yes | quick-filter tabs (e.g. nesupakuotos / sis menuo) | Search + date + status chips + clear + URL present; missing only tabs. Columns: Nr, Klientas, Data, Paletai, Busena. |
| 21 | `/warehouse/delivery-pricing` (DeliveryPricing) | yes | yes | yes | no | yes | yes | quick-filter tabs (e.g. nesumoketa / sumoketa) | Search + date + status chips + clear + URL present; missing only tabs. Columns: Nr, Data, Tiekejas, Netto, Statusas, Suma, Sumoketa, Skola. |
| 22 | `/expense-invoices` (ExpenseInvoices) | yes | yes | yes | partial | yes | yes | (already near-complete) verify tabs are quick-filter tabs | Search + date + status chips + clear + URL present; tabs present (verify semantics). Columns: Nr, Tiekejas, Data, Terminas, Suma, Like, Statusas, Problemos. |
| 23 | `/invoices/sales` (Invoices) — REFERENCE | yes | yes | yes | yes | yes | yes | — | Full reference implementation. Type tabs (LAK/ULAK) + status chips + 4 quick tabs + search + date + clear, all URL-persisted via FilterUrlBuilder. |

---

## Suggested rollout order (highest value first)

1. **Tier A (0-1 features, no URL persistence):** TransferHistory, WriteOffHistory, Inventory, Users, Warehouses, DeliveryView, SupplierDebts — add at least search + URL persistence; add date/status where the entity has those axes.
2. **Tier B (2-3 features):** DeliveryList, HoneyTypes, BankImport, Products, Customers, InvoicePaymentList, WriteOff, StockOverview — add the missing status-chip / clear / active-chip pieces; convert InvoicePaymentList's MudSelect to chips.
3. **Tier C (4-5 features):** ExpensePayments, PaymentHistory, Suppliers, CreditNotes, Orders/Index, DeliveryPricing, ExpenseInvoices — mostly add quick-filter tabs and/or status chips to reach parity with the reference.
4. **Reference:** Invoices — unchanged; use as the visual/interaction template.

## Cross-cutting recommendations

- Adopt `Helpers/FilterUrlBuilder.cs` as the single persistence mechanism for **every** list page (Tier A/B pages currently lack it).
- Introduce `<FilterSearchBox>` and `<StatusChipGroup>` shared components (see Summary) to stop the identical-markup duplication; keep date-range and tabs inline per page.
- Standardize the wrapper style: `MudPaper` with `background:#f8fafc; border-radius:8px` + `MudGrid` column layout + status colors via `StatusDisplayHelper.GetColor/GetLabel` (extend `StatusDisplayHelper` for non-invoice entities' status enums as needed).
- Only add a feature where the entity genuinely has that axis (e.g. do **not** add a date range to Products/Users/Warehouses which have no date column; do add active/role chips since they have an "Aktyvus"/"Role" column).
