# Task: Consolidate warehouse write-off/transfer pages into Stock page + restyle Sandėlis nav

## ⚠️ Branch check — do this FIRST, before touching any file

```
git branch --show-current
```
This MUST print `main`. If it prints anything else (especially `production`), STOP immediately
and report back without making any changes. Do not `git checkout` on your own initiative — just
report the branch name and stop. (Context: a prior task was accidentally run while on `production`
directly, causing 20 commits to diverge from `main` until manually reconciled today. Do not repeat
that.)

## Context (already investigated — do not re-derive, verify then implement)

Three warehouse pages are redundant and will be deleted, merging all their functionality into
`/warehouse/stock` (Components/Pages/Warehouse/StockOverview.razor):

- `Components/Pages/Warehouse/WriteOff.razor` (`/warehouse/write-off`) — DELETE
- `Components/Pages/Warehouse/WriteOffHistory.razor` (`/warehouse/write-off-history`) — DELETE
- `Components/Pages/Warehouse/TransferHistory.razor` (`/warehouse/transfers`) — DELETE

Confirmed facts from investigation (re-verify these still hold before changing anything — files
were re-read as of the current `main` HEAD, v0.17.18):
- `containers.notes` column exists (text, nullable) — confirmed via `DESCRIBE containers` on dev DB.
- `ContainerService.WriteOffAsync` (Services/ContainerService.cs) currently writes the write-off
  reason ONLY into `stock_movements.notes`, never into `containers.notes`. This is a bug: the
  "Priežastis" column in the old WriteOffHistory.razor reads `Container.Notes`, which is why it
  always shows "—". Must fix: the `UPDATE containers SET status = {0}, updated_at = NOW() WHERE id = {1}`
  raw SQL in `WriteOffAsync` must also set `notes = {2}` with the `reason` parameter.
- `IContainerService.GetFilteredAsync(int? warehouseId, int? honeyTypeId, int? supplierId, string? status, string? searchCode)`
  already supports `status = "WRITTEN_OFF"` filtering — no service signature changes needed there.
- `TransferService.GetTransferHistoryAsync()` returns `List<StockMovement>` where `MovementType == "TRANSFER"`,
  grouped in the UI by `Notes` (which holds the transfer number, e.g. `PK-2601-001`). No service
  changes needed. `TransferService.TransferContainersAsync` updates containers via
  `context.Entry(container).Property(x => x.WarehouseId).IsModified = true` + `SaveChangesAsync` —
  this is an intentional, working pattern (explicit attach + mark-modified), NOT the banned
  FindAsync+SaveChanges anti-pattern. Do not "fix" it to raw SQL — out of scope for this task.
- `StockOverview.razor`'s "Nurašyti" button currently does `Href="/warehouse/write-off"` — a plain
  navigation link that does NOT pass `_selectedContainers`. This must become a dialog instead (see below).
- `container_type` DB enum is only `('BARREL','BUCKET')`, but application code elsewhere checks for
  a `"BUCKET_GROUP"` string value (e.g. `StockOverview.razor`'s summary cards and
  `BuildContainerTypeGroups`). This is a pre-existing inconsistency unrelated to this task — do not
  attempt to fix it, just preserve existing behavior exactly when porting code between files.

## Part 1 — Investigation (do first, read-only, after the branch check)

1. Grep the ENTIRE repo (not just Components/) for any remaining references to the three routes
   being deleted: `/warehouse/write-off`, `/warehouse/write-off-history`, `/warehouse/transfers`.
   Check especially: `Components/Layout/NavMenu.razor`, `Components/Pages/Home.razor` (dashboard
   quick links), any Reports pages, any other Razor page with an `Href=` or `Navigation.NavigateTo`
   pointing at these routes. List every hit found before making changes.
2. Confirm `IContainerService.WriteOffAsync` is called ONLY from `WriteOff.razor` today (grep for
   `WriteOffAsync(`). If it's called elsewhere, note it — the new call site will be the new dialog
   in StockOverview.razor.
3. Re-read `Services/ITransferService.cs` and `Services/TransferService.cs` in full before porting
   the transfer-history UI code, to confirm signatures match what's described above.
4. Re-read `Components/Layout/NavMenu.razor` in full before editing — confirm the `_groupedSections`
   / `NavGroup` / `lb-group` rendering pattern (used for Finansai/Analitika/Gamyba) matches Part 5
   below.

Report anything that contradicts the "Confirmed facts" section above before proceeding to build —
if something doesn't match, stop and describe the discrepancy in the report instead of guessing.

## Part 2 — StockOverview.razor: consolidate 3 pages into 1

Target file: `Components/Pages/Warehouse/StockOverview.razor`

### 2a. Add a top-level view switcher (styled exactly like `/invoices/sales` LAK/ULAK/KLAK switcher)

Copy the `MudChipSet` pattern verbatim from `Components/Pages/Invoices.razor` (the `_invoiceType`
switcher near the top: `SelectionMode.SingleSelection`, `Style="text-transform:none;min-width:180px;"`,
`Color="@(_viewType == "X" ? Color.Primary : Color.Default)"`, `Variant="@(_viewType == "X" ? Variant.Filled : Variant.Outlined)"`).

New field: `private string _viewType = "STOCK";` with three chip values/labels:
- `"STOCK"` → "Likučiai"
- `"WRITTEN_OFF"` → "Nurašyta"
- `"TRANSFERS"` → "Perkėlimai"

Sync `_viewType` to the URL as a new query param (`view`), following the exact same
`FilterUrlBuilder.Build` + `OnParametersSetAsync` pattern already used for the other filters in
this file. Changing view resets `_selectedContainers` and reloads data for that view.

### 2b. `_viewType == "STOCK"` (default)

No behavior change to the existing hierarchy, filters, status chips, or summary cards — EXCEPT:

Replace the current "Nurašyti" button:
```razor
<MudButton ... Href="/warehouse/write-off">Nurašyti</MudButton>
```
with a button that opens a new dialog (same pattern as the existing Transfer/AssignHoneyType
dialogs in this file):

```razor
<MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small"
    StartIcon="@Icons.Material.Filled.DeleteForever"
    OnClick="OpenWriteOffDialog">Nurašyti</MudButton>
```

New `MudDialog` (`_writeOffDialogVisible`), content ported from `WriteOff.razor`'s step-3 panel:
- Title: `Nurašyti @_selectedContainers.Count vnt. (@_selectedContainers.Sum(c => c.NetWeight).ToString("N1") kg)`
- `MudSelect` "Priežastis" (Required) with items: Pažeista / Pasenusi / Netinkama / Užteršta / Kita
- `MudTextField` "Pastaba"
- Confirm button ("Patvirtinti nurašymą", `Color.Error`), disabled while `string.IsNullOrEmpty(reason)`
  or while processing. On confirm:
  ```csharp
  var ids = _selectedContainers.Select(c => c.Id).ToList();
  var reason = _writeOffReason + (!string.IsNullOrEmpty(_writeOffNotes) ? $": {_writeOffNotes}" : "");
  await ContainerService.WriteOffAsync(ids, reason, null);
  ```
  Then close dialog, `Snackbar.Add(...)`, `await LoadData()`, clear selection.
- Cancel button clears dialog state without calling the service.

### 2c. `_viewType == "WRITTEN_OFF"`

Port the content of `WriteOffHistory.razor` almost verbatim into a new `@if (_viewType == "WRITTEN_OFF")`
branch:
- Own filter fields (`_writeOffSearchText`, `_writeOffDateRange`) — read-only view, no status chips,
  no bulk action bar, no container selection.
- Summary cards ("Nurašyta vnt." / "Nurašyta kg").
- Flat `MudTable<Container>` (NOT the category hierarchy) with columns: ID, Tipas, Tiekėjas, Rūšis,
  Netto (kg), Sandėlis, Priežastis (`c.Notes`, now populated after the Part 4 bugfix), Data (`c.UpdatedAt`).
- Data load: `ContainerService.GetFilteredAsync(null, null, null, "WRITTEN_OFF", null)`.
- `MudAlert Severity="Severity.Info"` "Nurašytų konteinerių nėra" when empty.

### 2d. `_viewType == "TRANSFERS"`

Port the content of `TransferHistory.razor` almost verbatim into a new `@if (_viewType == "TRANSFERS")`
branch:
- Own filter fields (search + date range), same `TransferGroup`/`TransferContainerInfo` private
  classes, same grouping-by-`Notes`-as-transfer-number logic, same `MudTable` with `ChildRowContent`
  expand-on-row-click showing per-container breakdown.
- Data load: `TransferService.GetTransferHistoryAsync()` + `ContainerService.GetByIdsAsync(...)` to
  resolve container codes, exactly as in the original file.
- `MudAlert Severity="Severity.Info"` "Perkėlimų dar nėra" when empty.

### 2e. Filter bar

The existing `<StandardListFilterBar>` search/date fields at the top should only apply to
`_viewType == "STOCK"`. WRITTEN_OFF and TRANSFERS each render their own filter fields (ported from
their original pages) since they filter different underlying data — do not try to unify all three
into one shared filter state.

## Part 3 — Delete the 3 old pages

Delete these files entirely:
- `Components/Pages/Warehouse/WriteOff.razor`
- `Components/Pages/Warehouse/WriteOffHistory.razor`
- `Components/Pages/Warehouse/TransferHistory.razor`

Keep all `@inject` services in StockOverview.razor that the ported code needs (it already injects
`ITransferService`, `IDeliveryService`, etc.) — only remove an injected service if it becomes
provably unused after the merge.

## Part 4 — ContainerService bugfix

`Services/ContainerService.cs`, method `WriteOffAsync`:

```csharp
await context.Database.ExecuteSqlRawAsync(
    "UPDATE containers SET status = {0}, updated_at = NOW() WHERE id = {1}",
    "WRITTEN_OFF", container.Id);
```
becomes:
```csharp
await context.Database.ExecuteSqlRawAsync(
    "UPDATE containers SET status = {0}, notes = {1}, updated_at = NOW() WHERE id = {2}",
    "WRITTEN_OFF", reason, container.Id);
```
(Parameter order/count changes — update accordingly, don't just insert a token.)

## Part 5 — NavMenu.razor: restyle "Sandėlis" to match Finansai/Analitika/Gamyba

Currently "Sandėlis" is built as a plain `_topSections` entry (simple toggle row, no group label,
no icon, no count badge). Move it to the `_groupedSections` / `NavGroup` pattern used for
Finansai/Analitika/Gamyba (group label above + collapsible header with icon + item count).

- Remove the "Sandėlis" `NavSection` from `_topSections` construction.
- Add it as a `NavGroup` in `_groupedSections` instead, e.g.:
  ```csharp
  var warehouseItems = new List<NavItem> { new() { Href = "/warehouse/deliveries/new", Label = "Naujas pristatymas" } };
  if (!_isManager)
      warehouseItems.Add(new NavItem { Href = "/warehouse/deliveries", Label = "Pristatymai", Count = _badgeCounts.UnpricedDeliveries });
  if (_isAdmin || _isManager)
  {
      warehouseItems.Add(new NavItem { Href = "/warehouse/delivery-pricing", Label = "Kainos ir mokėjimai" });
      warehouseItems.Add(new NavItem { Href = "/warehouse/supplier-debts", Label = "Skolos tiekėjams" });
  }
  warehouseItems.Add(new NavItem { Href = "/warehouse/stock", Label = "Sandėlio likučiai" });

  var warehouseSection = new NavSection { Title = "Sandėlis", Items = warehouseItems, DefaultExpanded = false };
  warehouseSection.Expanded = warehouseSection.DefaultExpanded;
  _groupedSections.Add(new NavGroup { GroupLabel = "Sandėlis", Icon = "▤", Section = warehouseSection });
  ```
  (Pick an icon consistent in weight/style with the existing `€` / `◲` / `◑` — `▤` is a suggestion,
  not mandatory; use judgment if a better single-glyph icon fits the existing set.)
- **Remove** the three `NavItem`s pointing at the deleted routes (`/warehouse/write-off`,
  `/warehouse/write-off-history`, `/warehouse/transfers`) — only `/warehouse/deliveries/new`,
  `/warehouse/deliveries`, `/warehouse/delivery-pricing`, `/warehouse/supplier-debts`,
  `/warehouse/stock` remain.
- Insert this new `NavGroup` as the FIRST entry added to `_groupedSections` (i.e. before the
  Finansai block), so Sandėlis appears immediately above Finansai/Analitika/Gamyba.
- No CSS changes should be needed — `_groupedSections` already renders via the shared `lb-group` /
  `lb-group-label` / `lb-group-header` / `lb-group-icon` / `lb-group-count` markup block, so moving
  Sandėlis into that list is sufficient to get identical styling automatically. Do NOT duplicate or
  fork that markup block.
- Known side effect (expected, not a bug): Sandėlis will now render below Produkcija/Užsakymai/
  Tiekėjai ir klientai/Išlaidos instead of above them, because `_topSections` render before
  `_groupedSections` in the markup. This is intentional per the task — do not try to reorder the
  markup blocks to compensate.

## Critical checks before calling this done

1. `dotnet build` — 0 errors, 0 new warnings introduced by this change. (Note: the build currently
   has ~480 pre-existing MUD0002 analyzer warnings across the codebase, including one already in
   StockOverview.razor — unrelated to this task, do not attempt to fix pre-existing warnings, only
   ensure this change doesn't add new ones.)
2. Grep confirms zero remaining references anywhere in the repo to `/warehouse/write-off`,
   `/warehouse/write-off-history`, `/warehouse/transfers` (routes, Hrefs, NavigateTo calls, docs
   excluded).
3. Manually trace: selecting containers on Stock → clicking "Nurašyti" → dialog → confirm → the
   selected containers actually disappear from the "Likučiai" view and appear in "Nurašyta" view
   with the correct "Priežastis" text (this depends on Part 4's bugfix being applied correctly —
   verify the SQL parameter order is right, this is exactly the kind of thing that has caused
   `schema-drift-unverified-column-mapping` bugs before).
4. Manually trace: `_viewType` switching correctly resets `_selectedContainers` and does not leak
   STOCK-view filter state into the WRITTEN_OFF/TRANSFERS views or vice versa.
5. Confirm no duplicate `@page` route registration exists after deleting the old files (only
   `/warehouse/stock` should remain for this area).
6. Reviewer: this diff touches user-facing Lithuanian strings (button labels, dialog text, nav
   labels). Per project rule, reviewer must quote every changed/added Lithuanian string verbatim in
   its verdict before approving.
7. No Playwright verification needed — `dotnet build` (0 errors) + reviewer verdict is sufficient.
   Deividas will check functionality/visuals manually.

## End of task

Write the full work report to `.opencode/reports/` (NOT `.agent-reports/`). Then run
`./bump-version.sh patch`.
