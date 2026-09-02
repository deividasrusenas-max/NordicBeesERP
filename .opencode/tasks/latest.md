# Task: Redesign Warehouse Write-Off module (WriteOff.razor + WriteOffHistory.razor)

Type: BUILD (code changes to Razor pages only — no service/interface/model/DB changes)

## Context
This module is for writing off spoiled/unsuitable honey containers from stock.
Investigation report `writeoff-status-investigation-20260902-2241.md` established:
- Of the 7 `containers.status` enum values, only `IN_STOCK` and `WRITTEN_OFF` are
  ever actually set in production code. RECEIVED/RESERVED/IN_PRODUCTION/SOLD/RETURNED
  have UI chips but zero code paths that assign them.
- `ContainerService.GetFilteredAsync(warehouseId, honeyTypeId, supplierId, status, ...)`
  is shared with `StockOverview.razor` — its signature must NOT change.

Owner decision: simplify this module to reflect reality — it only ever deals with
IN_STOCK (candidates) and WRITTEN_OFF (already written off) containers. No status
picker is needed anywhere in this module.

## Required changes

### A. `Components/Pages/Warehouse/WriteOff.razor` (candidates / selection page)

1. **Remove the status filter chip row entirely** (the `_containerStatusDisplay`
   `MudChip` foreach block and `ToggleStatus`/`_status` wiring). This page should
   only ever load containers with `status == "IN_STOCK"` — pass that literal
   constant into `GetFilteredAsync` instead of a user-toggleable `_status`.
2. **Remove the warehouse dropdown filter and honey type dropdown filter** (the
   `MudSelect` for warehouse and honey type, and their bound fields/params sent
   to `GetFilteredAsync` — pass `null` for those two params since we're not
   filtering by them anymore).
3. **Rebuild the filter bar to match the standard pattern** used on
   `/invoices/sales` (see `Docs/FILTER_STANDARDIZATION_PLAN.md` and that page's
   markup for reference): page title on its own row, primary action button
   below it (left-aligned), filter fields with **no bordered card wrapper**.
   Filter bar contents:
   - Single search box covering: container code (`Code`), supplier name, honey
     type/rūšis (search across these fields — check how `_search` currently
     works client-side in `FilteredContainers` and keep/adapt that logic, just
     make sure honey-type text is included in what's matched)
   - Date range picker for laikotarpis (use whatever date field the containers
     list currently filters/sorts by for this page — check existing code
     first, likely delivery/created date)
4. **Remove the "Statusas" column from the container table.** Every row on this
   page is IN_STOCK by definition now, so a status column is redundant. Just
   drop the column, don't replace it with anything on this page.
5. Keep everything else: container selection checkboxes, the write-off reason
   step (Pažeista/Pasenusi/Netinkama/Užteršta/Kita + notes), confirm button,
   `ContainerService.WriteOffAsync` call — unchanged.

### B. `Components/Pages/Warehouse/WriteOffHistory.razor` (history page)

1. This page already hard-loads `status="WRITTEN_OFF"` — confirm there's no
   status filter/chip UI here to remove (per the investigation report there
   wasn't one, but re-check).
2. **Replace any "Statusas" display (badge/column showing "Nurašytas") with a
   proper "Priežastis" column** showing the write-off reason (the `Notes` field,
   which already stores the reason typed on the WriteOff page — see
   `WriteOffAsync` in `ContainerService.cs`). If a Notes/Priežastis column
   already exists, just make sure the redundant status badge is removed and
   Priežastis is clearly labeled and prominent.
3. Remove warehouse/honey type dropdown filters here too if present, for
   consistency with WriteOff.razor.
4. Apply the same standard borderless filter bar pattern as (A.3): search box
   (code/supplier/rūšis) + date range picker (use the write-off date —
   `UpdatedAt`, per the investigation report).

## Explicit guardrails — do NOT touch

- Do NOT change `ContainerService.GetFilteredAsync` / `IContainerService`
  signatures — `StockOverview.razor` depends on the exact current signature.
- Do NOT touch `StockOverview.razor` at all.
- Do NOT touch `Container.cs`, the `containers` table, any migration, or the
  DB enum. Status values stay as-is in the DB/model — we're only removing
  unused UI, not the underlying data model.
- Do NOT touch `ContainerService.WriteOffAsync`, `ContainerService.UpdateStatusAsync`,
  or any other service method logic.
- Do NOT remove the `RESERVED`/`IN_PRODUCTION`/`SOLD`/`RETURNED` handling from
  `StockOverview.razor` — that page is out of scope for this task.
- Follow FROZEN.md: `.AsNoTracking()` on reads, `ExecuteSqlRawAsync` for writes,
  no FK changes, DDL human-only (n/a here since no DDL).

## Verification
- `dotnet build` → must be 0 errors.
- Grep both files after the change to confirm `_containerStatusDisplay`,
  `ToggleStatus`, and the status chip markup are fully gone from WriteOff.razor
  (and from WriteOffHistory.razor if it had them).
- Confirm `StockOverview.razor` was NOT modified (`git diff --stat` should not
  list it).
- No Playwright/browser verification — Deividas checks the UI manually.
- End with `./bump-version.sh patch`.

## Output
Write a full report to `.opencode/reports/writeoff-module-redesign-<timestamp>.md`
listing: exact diffs/summary per file, confirmation of the guardrails above,
build result, commit hash, version bump result.
