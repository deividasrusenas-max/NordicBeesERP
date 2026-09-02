# FOLLOW-UP BUILD TASK — Warehouse Stock Redesign: fix incomplete/incorrect work

The previous round on `/warehouse/stock` (see `.opencode/reports/warehouse-stock-redesign-*` and `.opencode/reports/stockoverview-hierarchy-code-20260902.md`) left real gaps, verified directly against the current source files by Deividas. Do NOT re-do work that is already correct (filter bar Variant.Text, select removal, icon removal — all confirmed done). Fix only the following.

## 1. CRITICAL — Migration policy violation, redo as a new migration

`Migrations/20260602150000_InitialCreate.cs` was edited in-place to add the `containers.container_code` UNIQUE index (in `Up()` and `Down()`). This is wrong: that migration is already applied to dev/staging/prod, so editing it has **no effect** on any existing database and violates FROZEN.md §8 (incremental migrations policy — one `dotnet ef migrations add` per schema change going forward).

**Fix:**
- Revert the edits made to `Migrations/20260602150000_InitialCreate.cs` (both `Up()` and `Down()`) back to their original content.
- Create a **new** EF Core migration (`dotnet ef migrations add AddContainerCodeUniqueIndex` or equivalent) that does the same thing: `DROP INDEX idx_container_code` + `ADD UNIQUE INDEX idx_container_code (container_code)` in `Up()`, reverse in `Down()`.
- Do NOT apply it (`dotnet ef database update` stays human-only — Deividas runs it himself after checking for existing duplicate codes).

## 2. Wire up the new TP container code generation (currently dead code)

`ContainerService.GenerateNextContainerCodeAsync()` exists and is correct, but nothing calls it. New containers are still created with manually-typed codes via the "Pradinis ID" field in `DeliveryCreate.razor`, so in practice nothing changed for the ID collision problem this was meant to fix.

**Fix in `Components/Pages/Warehouse/DeliveryCreate.razor`:**
- Remove the "Pradinis ID" `MudTextField` inputs (`_startId` for barrels, `_bucketStartId` for buckets) from the UI entirely — both the "uniform" and "different weights" flows.
- Replace `GenerateSequentialId(startId, offset)` calls with calls to `ContainerService.GenerateNextContainerCodeAsync()` — call it once per container being added (inside the same loop/wizard step), so each new container gets the next `TP{NNNNNN}` code. Since `GenerateNextContainerCodeAsync` reads MAX from the DB each call, this is safe for the "different weights" one-at-a-time wizard; for the "uniform" bulk-add loop, generate all codes in the loop sequentially (each call sees the previous iteration's write only if you persist between calls — since containers are only added to the in-memory `_pendingContainers` list until `SubmitDelivery`, coordinate this: either reserve a block of N sequential codes with a single query (`MAX + 1` through `MAX + N`) instead of N separate calls, or call `GenerateNextContainerCodeAsync` fresh per container and accept it reads the same MAX from DB each time — in that case ALSO check `_pendingContainers` in-memory for already-used TP codes this session, same pattern as the existing DB-duplicate check for `GetByCodeAsync`.
- Update `OnInitializedAsync()` — remove the `GetLastContainerCodeAsync`/`GetLastBucketCodeAsync` + manual `_startId`/`_bucketStartId` prefill logic; no longer needed.
- Display the generated code(s) to the user read-only in the summary/wizard step so they can see what was assigned (e.g. "Statinė TP000042").
- Existing `LAK`-prefixed containers stay untouched — this only changes how *new* containers get coded going forward.

## 3. Render the category hierarchy in the actual page markup — this is the main gap

`BuildStockHierarchy()` / `CategoryGroups` in `StockOverview.razor`'s `@code` block is fully implemented and correct, but the `<MudTable>` markup was never changed — the page still renders one flat row per container exactly as before. This was the primary ask from Deividas and is currently not delivered at all.

**Fix:** Replace the current flat `<MudTable T="Container" Items="@FilteredContainers" ...>` block with a rendering of `CategoryGroups` (the `StockGroup` hierarchy), using `MudExpansionPanels`:

- **Level 1** (`CategoryGroups`, e.g. Medus/Bičių duona/Vaškas/...): one `MudExpansionPanel` per group. Header shows `group.Label`, `group.Count` vnt., `group.NetWeight` kg — use `group.SummaryText` if convenient, or lay it out with `MudText`/flex row for better alignment (category name left, count+weight right). Collapsed by default.
- **Level 2** (only present when `group.IsHoneyLevel` children exist, i.e. inside "Medus"): nested `MudExpansionPanels` inside the Level 1 panel content, one per honey-type child group, same header pattern.
- **Level 3** (container-type children — Statinės/Kibirai): nested `MudExpansionPanels` again, one per `StockGroup` in `Children` whose own `Children` is empty and `Items` is populated.
- **Level 4** (leaf level — `group.Items`): render the *existing* flat `MudTable` markup (columns: ID, Tiekėjas, Rūšis, Brutto, Tara, Netto, Statusas, Sandėlis, Data) scoped to `group.Items` instead of `FilteredContainers`. Reuse the exact same `RowTemplate` — do not rewrite column logic. Multi-select (`_selectedContainers`) and the bulk action bar (Transfer/Assign/Write-off) must keep working across the whole page regardless of which leaf table a row was selected from — `_selectedContainers` is already a page-level field, so as long as each leaf `MudTable` binds to the same `@bind-SelectedItems="_selectedContainers"`, this should work without further changes; verify it does.
- Recursion: since the depth varies (honey categories go 4 levels deep, non-honey categories go 3), write a small recursive rendering approach — either a Razor `RenderFragment` helper method that takes a `StockGroup` and renders itself (checking `Children.Any()` vs `Items.Any()`), or a `@if`/`@foreach` structure per known depth. Prefer the recursive `RenderFragment` approach since it naturally handles both the 3-level and 4-level cases without duplicated markup.
- Auto-expand behavior: when `_search` is non-empty, auto-expand (`Expanded="true"` conditionally) any Level 1/2/3 panel that contains at least one matching item, so search results are immediately visible without manual clicking. When `_search` is empty, all panels default to collapsed.
- Empty groups must not render at all (already handled correctly in `BuildStockHierarchy` — just don't add any empty-state rendering for zero-count groups).

## 4. Visual polish — align summary cards with design tokens

The 4 summary cards (Statinės / Bendras netto / Kibirai / Bičių duona) still use ad-hoc background colors (`#e3f2fd`, `#e8f5e9`, `#fff3e0`, `#f3e5f5`) that don't come from `Docs/DESIGN_SYSTEM.md`. Update them to use the documented tokens (section bg `#f8fafc` or card bg `#ffffff` with the documented border/text colors) — read `Docs/DESIGN_SYSTEM.md` again before touching this, don't invent new ones. Keep the 4-card layout and content, just fix the colors to match the rest of the page.

## Constraints (FROZEN.md — unchanged)
- `.AsNoTracking()` reads, `ExecuteSqlRawAsync` writes (insert exception stands for new `Container` rows).
- `EF.Functions.Like()` only, never `Contains(StringComparison...)` in LINQ-to-entities.
- New migration only — do not apply it, do not touch the old `20260602150000_InitialCreate.cs` beyond reverting it back to original.
- No FK constraints without approval.

## Verification
- `dotnet build` must succeed with 0 errors.
- Reviewer must explicitly confirm, by reading the actual markup (not trusting @code alone): (a) `CategoryGroups` is rendered via `MudExpansionPanels` and the flat table is gone from the top-level view, (b) `GenerateNextContainerCodeAsync` is called from `DeliveryCreate.razor` and "Pradinis ID" fields are removed, (c) `20260602150000_InitialCreate.cs` is back to its original pre-edit state and a new migration file exists instead, (d) multi-select + bulk actions still work when rows come from different leaf tables.
- No Playwright — Deividas checks the UI manually.

## Report & versioning
- Write a full report to `.opencode/reports/warehouse-stock-fixup-<timestamp>.md` covering exactly what changed in each of the 4 sections above, and explicitly confirm the migration revert.
- Run `./bump-version.sh patch` at the end.
