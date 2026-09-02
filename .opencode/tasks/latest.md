# Task: Investigate Warehouse Write-Off page status logic + minor UI fix

Type: INVESTIGATION (read-only, no code changes except item 1)

## Context
Page: `/warehouse/write-off` (Sandėlio nurašymai). We need to understand the full
status/type system before making further changes.

## Scope

### 1. UI fix (small, safe to do directly)
On the write-off page, remove the icons that are currently displayed next to the
"Type" (Tipas) column/field values. Keep the text label only, no icon.
Find the relevant Razor component (likely under `Pages/Warehouse/` or `Components/Warehouse/`,
search for "write-off" / "WriteOff" / "Nurašymas").

### 2. Investigate: Product/Lot statuses
Find every enum or constant set that defines stock/lot statuses (e.g. `IN_STOCK`,
and any others). For each status found, report:
- Exact name/value
- Where it's defined (file + enum/class name)
- Plain-language meaning (infer from usage/context, code comments, UI labels)
- Every place in the codebase where this status is **set/assigned** (which service,
  which method, triggered by what business action — e.g. delivery received, order
  packed, write-off created, manual edit, etc.)
- Every place where this status is **read/filtered on** (queries, UI filters, reports)

### 3. Investigate: Status filter "tags" on the write-off page
On `/warehouse/write-off`, there appears to be some kind of status filter UI
(chips/tags). Report:
- What filter options exist exactly (labels + underlying values)
- How the filter is wired to the query (LINQ/SQL) that loads the write-off list
- Is it MudBlazor MudChip / MudChipSet or something else — quote the exact markup

### 4. Investigate: Full write-off lifecycle
Trace the complete write-off flow end to end:
- What entity/table represents a "write-off" (nurašymas)
- What statuses/states a write-off record itself can be in (separate from
  product/lot status if applicable)
- What triggers a status change on the write-off record itself (create, confirm,
  cancel, etc.)
- How write-off relates to LOT/warehouse quantity — does creating a write-off
  reduce stock immediately or only on confirmation?
- Any relevant service class names, method names, file paths

## Output
Write a full report to `.opencode/reports/writeoff-status-investigation-<timestamp>.md`
covering sections 2, 3, 4 above with concrete file paths, code snippets (short,
relevant excerpts only), and a plain-language summary table of all statuses found
(status name → meaning → where set → where used).

Also confirm in the report that the icon removal from item 1 was done, with the
file path and a short diff/description of the change.

## Constraints
- Do NOT change any business logic, only the icon removal in item 1.
- Do NOT touch DB schema or run migrations.
- Follow FROZEN.md rules if any code is touched.
- Run `dotnet build` after the icon removal to confirm 0 errors.
- Do NOT include Playwright/browser verification steps.
- End with `./bump-version.sh patch` only if the icon fix was actually made.
