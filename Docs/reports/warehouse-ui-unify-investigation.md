# Warehouse UI unification — Phase 0 investigation (read-only)

Date: 2026-10-23 · Branch: `main` (clean at start) · No code changed in this phase.

Reference ("etalonas"): `Components/Pages/Invoices.razor` (`/invoices`, `/invoices/sales`, `/invoices/purchases` are ONE page, type via `?type=`) and `Components/Pages/InvoiceView.razor` (`/invoices/{id}?returnUrl=`).
Targets: `Warehouse/DeliveryList.razor`, `Warehouse/DeliveryView.razor`, `Warehouse/DeliveryPricing.razor`, `Warehouse/DeliveryPricingDetail.razor`.

## 1. Shared components — what exists, where the logic lives

Only ONE shared list component exists: `Components/Shared/StandardListFilterBar.razor` (a `MudPaper` wrapper, `ChildContent` only). Everything else in the invoice list is inline in `Invoices.razor` and is copy-pasted (differently) in the warehouse lists:

| Piece | Invoices.razor | DeliveryList | DeliveryPricing |
|---|---|---|---|
| Filter card | `StandardListFilterBar` + search (`Variant.Text`) + `MudDateRangePicker` "Data (nuo / iki)" + "Išvalyti filtrus" | same bar, label "Laikotarpis", no `Editable`/`DateFormat` | plain `MudPaper Elevation=2`, label "Pristatymo data", `Variant` default |
| Quick chips | inline `MudChip` row: Visos / Neapmokėtos / Vėluojančios / Šis mėnuo (`_selectedTab`, `?tab=`) + multi-select status chips (`?status=`) | only status chips (single-select, DB status) | status chips ("Statusas:" label, `Checked=`) **and** `MudTabs` (5 tabs) |
| "Pasirinkta: N" bar | inline `MudPaper` bg `#e8f0fe`, "Atšaukti" + "Generuoti PDF" | none — a bare green `Išrašyti sąskaitas (N)` button | none |
| Pager | inline custom button pager (`PagerContent`, `ShouldShowPage`, `NavigateToPage`), `PageSize=50`, `?page=` | none (all rows) | none (all rows) |
| Row colour | `GetRowStyle`: overdue `#fef2f2`, due ≤7 d `#fffbeb` (inline `Style`, not a class) | `selected-row` class only | none (debt cell red text only) |
| Money | `FormatAmountHelper.Trim(x, 2) €` | – | `Trim(x, 2)` without € in cells |
| Row open | `@ondblclick` on each `MudTd` (native dblclick) | `OnRowClick` + hand-rolled 400 ms `IsDoubleClick` | same hand-rolled `IsDoubleClick`, but `Style="cursor:pointer"` on the table |

`IsDoubleClick` is duplicated in DeliveryList and DeliveryPricing. `Helpers/LtPager.cs` exists — not inspected for fit in phase 0 (to check in phase 1).

Phase 1 therefore does need new shared components (filter card, quick-chip row, selection bar, pager, row-style helper). Extracting must not change invoice markup/behaviour.

## 2. URL sync and returnUrl

Invoices: `[SupplyParameterFromQuery]` `q`, `from`, `to`, `status` (CSV), `tab`, `type`, `page`. `OnParametersSetAsync` copies every param into state and reloads; each UI change builds a URL with `FilterUrlBuilder.Build(...)` and calls `Navigation.NavigateTo` (`replace:true` for typing/paging, `false` for chips/dates). `ViewInvoice` builds the *current list URL incl. all filters* and `FilterUrlBuilder.BuildDetailUrl("/invoices/{id}", listUrl, BaseUri)` → `?returnUrl=<escaped>`. `InvoiceView.HandleBack` unescapes it, fallback `/invoices`.

DeliveryList: params `q`, `status` (single), `from`, `to`; no tab/page. `GoToDelivery` passes `Navigation.Uri` as returnUrl (works). `DeliveryView.HandleBack` uses `FilterUrlBuilder.ResolveReturnUrl(ReturnUrl, "/warehouse/deliveries")` (works).

DeliveryPricing: params `q`, `from`, `to` (strings), `status` (CSV), `tab`. **Gaps found:**
- `OpenPricingDetail` navigates to `/warehouse/deliveries/{id}/pricing` with NO returnUrl.
- `DeliveryPricingDetail` has no `ReturnUrl` parameter; "Atgal" is `Href="/warehouse/delivery-pricing"`.
- `OnParametersSetAsync` calls `LoadData()` (full reload of all deliveries + up to 5000 invoices) on EVERY URL change, i.e. on every keystroke in search.
- `from`/`to` parsing only ever sets values, never clears `_dateRange` when the URL has none (so "Išvalyti filtrus" works only because it navigates; removing a date via picker and getting a URL without `from` leaves the old range in state).

## 3. Search: SQL or in memory?

All three are in memory:
- Invoices: `InvoiceService.GetInvoicesAsync` loads ALL invoices of the type (the `take` parameter is accepted but never applied), attaches customers, then filters `searchTerm` with `string.Contains(..., OrdinalIgnoreCase)` in C# (lines ~137-144). The Like-based `SearchInvoicesAsync` exists but is a different method (per-customer).
- DeliveryList: `GetFilteredAsync(status, null, from, to)` does status+date in SQL; free text is a C# `Contains` over the loaded list (number, supplier name via a separate suppliers lookup list, invoice number).
- DeliveryPricing: `GetAllAsync()` loads everything; text/date/status/tab all in C#; text matches only `DeliveryNumber` and supplier name — **not** `InvoiceNumber`, which is why "ULAK26013" returns 0 although the "Sąskaita" column shows it.

Consequence for plan: the task rule says `EF.Functions.Like`, not `StringComparison`. Moving delivery search to SQL needs a join to `business_partners` (no navigation property; supplier names come from `CustomerService.GetSuppliersAsync()`). At ~45 rows in-memory is fine; I propose to keep in-memory filtering over the already loaded list for the warehouse lists (add InvoiceNumber) and NOT touch `InvoiceService` — unless you want SQL search. **Decision needed (Q2).**

## 4. Delivery statuses

Single source: `deliveries.status` (string) — `RECEIVED | PRICED | PARTIAL_PAID | PAID`, written ONLY by `DeliveryTotals.RefreshStatusAsync` (raw `UPDATE ... CASE`, `ExecuteSqlRawAsync`, parameterised) from the debt base (`DeliveryDebt.Base`: linked non-draft ULAK total incl. VAT, else `total_amount`) vs `SUM(supplier_payments)`.

- DeliveryList + DeliveryView + DeliveryPricingDetail card: read the DB column through three separate copies of the dictionary `RECEIVED→Priimtas(Default) / PRICED→Įkainotas(Info) / PARTIAL_PAID→Dalinai(Warning) / PAID→Apmokėtas(Success)` (the pricing-detail copy says "Dalinai apmokėtas").
- DeliveryPricing list: does NOT use the column; recomputes with `GetStatusBadge(d)` from `DebtBase`/`PaidAmount`: `DebtBase<=0 → Priimtas`, `Paid<=0 → Neapmokėtas(Error)`, `Paid>=Debt → Apmokėtas`, else `Dalinai`. Chips are filtered by that Lithuanian *label string*.
- The two are logically equal (`PRICED` ≙ `Neapmokėtas`), only name/colour differ → pure vocabulary problem (Phase 4), no DB change. A residual risk: the column is only refreshed when a service path calls `RefreshStatusAsync`; the live-computed list can differ if that is skipped (e.g. direct DB edits, or a debt-base change from invoice status changes). Not verified against data in phase 0 (no DB queried).
- No enum/helper exists for delivery status; `StatusDisplayHelper` is a generic dictionary lookup (`GetLabel/GetColor`), usable as the home for a shared map.

## 5. Delivery detail: how edits are saved

`DeliveryView` (`Admin,Warehouse`):
- Line honey type (`MudSelect`) → `ValueChanged` → `ContainerService.UpdateDeliveryLineHoneyTypeAsync` immediately (auto-save on change), then reload + `_reloadToken++`.
- Gross/tare weights: plain `<input type=text>` with `@onchange` (= on blur/Enter) → `SaveWeightAsync` → `DeliveryService.UpdateContainerWeightAsync`; only editable while `IN_STOCK` and not signed. Reload afterwards; rejected values revert.
- Barrels switch/owed/returned: `ValueChanged` → `UpdateBarrelsAsync` immediately.
- "Išsaugota" chip is a fixed-position overlay for 1.5 s. So direct editing IS persisted; the "product (prekė) / container counts" cells are read-only. **Open point:** barrels `MudNumericField` saves on every `ValueChanged` (each keystroke/step) — chatty but working; not changed.

`DeliveryPricingDetail` (`Admin,Manager`): unit price `MudNumericField` → `OnPriceChanged` → `PerKeyDebouncer` 800 ms → `DeliveryService.UpdatePricesAsync` (flushed in `DisposeAsync`). Payments go through dialogs → `ISupplierPaymentService`.
**Defect found (pre-existing, from commit f18f015):** `DeliveryPricingDetail.razor` lines 118 and 247 contain a corrupted attribute `Culture="@LtCulture.Instance"lt-LT"))"` (stray `lt-LT"))"` text). The solution still builds (0 errors) — Razor accepts it as an extra bogus attribute — but it is garbage. I will remove it when I touch that file in phase 5 (one-line, behaviour-neutral) and mention it in the report.

## 6. How InvoiceView decides which buttons show

Header action row (left aligned, `d-flex flex-wrap gap-2`): `Atgal` → `Redaguoti` → `Spausdinti` → [`Pristabdyti priminimus` if CanEmail && not paid] → [`Siųsti el. paštu` if CanEmail] → `Kopijuoti` → [`Credit Note` if not Draft] → [`Patvirtinti` if Draft] → [`Registruoti mokėjimą` if `PaymentStatus != "paid"`]. All `Variant.Outlined`/`Default` except the last two, which are `Variant.Filled Color.Success`. All `text-transform:none`. Header is a sticky full-bleed bar; content width `MaxWidth.ExtraLarge`. Number + status chip on line 1, "Klientas/Tiekėjas: name" on line 2.

Notes: (a) for a Draft invoice TWO filled buttons show (Patvirtinti + Registruoti mokėjimą) — the reference itself breaks "one primary"; (b) `Color.Success` on primary buttons contradicts `nordicbees-standards.md` §22 ("Color.Success pagrindiniams mygtukams" is forbidden). **Decision needed (Q3).** Visibility is driven by invoice `Status` (enum) and `PaymentStatus` string.

Warehouse today: DeliveryView header is `SpaceBetween` (title+chips left, "Gauti parašą"/"Atsisiųsti aktą" centre, `Atgal` right, text-only), `MaxWidth.Large` (≈1248 px). DeliveryPricingDetail: same `SpaceBetween` pattern, `MaxWidth.Large`; `Atgal` fixed href; `Išrašyti sąskaitą` shown if `_canIssueInvoice` (no invoice yet and netto > 0), else a text link "Sąskaita išrašyta: nr"; "+ Pridėti mokėjimą" is `Add` StartIcon **plus** literal "+ " in text (the double plus); button sits above the payments table, not in the header.

Other defects listed in the task: totals footer row misaligned — the footer markup (`colspan=4` + netto + empty + suma = 7 cells for 7 columns) looks correct in source, so the cause is NOT visible from code and must be reproduced in the browser in phase 5 (not confirmed); third summary card caption = delivery status label ("Priimtas") above the debt; debt shown twice (top card "Skola" + bottom card); side menu: `NavMenu` uses `NavLinkMatch.Prefix`, so `/warehouse/deliveries/{id}/pricing` matches the "Pristatymai" item (`/warehouse/deliveries`) and NOT "Kainos ir mokėjimai" (`/warehouse/delivery-pricing`) → fix needs a per-item match/`ActivePrefix` or explicit route (touches shared `NavMenu.razor`).

**Role mismatch for cross links:** DeliveryList/DeliveryView/Signature = `Admin,Warehouse`; DeliveryPricing/PricingDetail = `Admin,Manager`. The planned "Kainos ir mokėjimai →" (in DeliveryView) and "Pristatymas →" (in PricingDetail) must be shown only to roles that can open the target, otherwise a Warehouse-only user (or Manager-only user) lands on "not authorized". I will gate them with `AuthorizeView Roles=...`.

## 7. Checkboxes on the deliveries list

Yes, they have one use: `CreateInvoicesForSelected` (bulk ULAK invoice draft creation for eligible rows = no invoice, farmer supplier, amount>0), shown as a green `Išrašyti sąskaitas (N)` button above the filters when any selected row is eligible. No select-all, no "Pasirinkta: N" bar, selection survives filtering, `_selectedDeliveries` stores `Delivery` objects (reference equality — lost after `LoadData()` replaces the list, which is why `Clear()` is called after creating). Phase 2: keep the checkboxes, add the shared selection bar with `Išrašyti sąskaitas`. Note ULAK logic itself (FROZEN §4) is not touched — only the trigger's placement.

## 8. Other findings relevant to the plan

- **Phase 6 money helper:** `FormatAmountHelper.Trim` is used for money AND weights/quantities (1–3 decimals) in ~40 razor files and covered by `FormatAmountHelperTests`. Services/PDF code does NOT use it (PDF/ULAK output unaffected). Changing `Trim` itself would alter weights and break tests → plan: add a NEW `Money(decimal)` helper (`11 180,40 €`, `0,00 €`) and switch the invoice pages/warehouse pages call sites to it in the separate phase-6 commit; `Trim` stays. Space for thousands should be a non-breaking space (U+00A0) so amounts do not wrap — **confirm (Q4)**.
- `Delivery.PaidAmount` is a mapped column that is never written (derived from `supplier_payments` in every read path) — fine, noted in service comments.
- Dev server is currently running on :5081 (PID 56997) — I will not touch/restart it; builds use `dotnet build` only.
- `Docs/PROJECT_STATE.md` is an unfilled template (nothing to learn). `README.md` does not exist. `nordicbees-standards.md` / `PATTERNS.md` are not in the repo; read from the synced skill copy (`~/.claude/skills/synced/.../nordicbees-erp/`).
- `.gitignore` does not ignore `Docs/reports/` (checked) — reports will be committed.

## 9. Contradictions with the plan / blockers (reason to stop after phase 0)

1. **AGENTS.md reviewer gate.** AGENTS.md makes a `reviewer` subagent `APPROVED` verdict mandatory for any task touching source, and forbids committing/proceeding without it. This session has no `reviewer` agent (only generic agents). Without your decision I cannot honour both "commit every phase" and the gate. **(Q1)**
2. **AGENTS.md `agent-guardrails check --base-ref HEAD~1`** is installed (`/opt/homebrew/bin/agent-guardrails`) — will run per phase; semgrep MCP / custom rule file is an opencode MCP tool, not available here → reported as not run unless you want me to run `semgrep` CLI if installed.
3. **Primary button colour** (see §6, Q3).
4. Everything else in the plan is consistent with the code.

## 10. Baseline

`dotnet build`: 0 errors, 615 warnings (all pre-existing). `dotnet test` baseline: see the final report/status message (Atmintinė task #17 notes a pending `invoices.supply_date` DB migration on dev / `nordic_bees_erp_test` which may make the full suite red independent of this work).
