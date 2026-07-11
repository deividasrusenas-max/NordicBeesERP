---
name: verify-before-done
description: Mandatory verification discipline before reporting ANY task as done — for NordicBeesERP, "the build succeeded" is NEVER sufficient proof a feature works. Use this for every task that involves a button, form submission, dialog, or any UI-to-database write path (DeliveryCreate, DeliveryView, dialogs, print workflows, etc.), and for any Service method that persists data.
---

# Verify Before Done — NordicBeesERP

**Core rule: "the build has zero errors" is NOT the same as "the feature works."** This project has a specific, recurring failure mode — `FindAsync` + `SaveChangesAsync()` under global `NoTracking` compiles fine, produces zero build errors, zero exceptions at runtime, and silently persists 0 rows. A button can be fully wired, look correct in the UI, and still not write anything to the database. Compiling is the *minimum* bar, not the finish line.

## What "done" actually requires for UI/data-writing tasks

For any task that adds or changes a button, form, dialog, or any user action that is supposed to write to the database, before reporting done you must trace the FULL path and confirm each link, not just that the file compiles:

1. **UI element** — the button/form has an `OnClick`/`OnSubmit`/`OnValidSubmit` handler wired to a real method (not a stub, not a TODO).
2. **Handler method** — that method actually calls into a Service (not just setting local component state and stopping).
3. **Service method** — read the actual Service method body. Confirm it uses `ExecuteSqlRawAsync` (or a genuine `.Add()` + `SaveChangesAsync()` for a true INSERT) — NOT `FindAsync`/`.Find()`/`Where().FirstOrDefaultAsync()` followed by property mutation and `SaveChangesAsync()`. See the `dotnet-efcore-nordicbees` skill for the exact pattern.
4. **Feedback to user** — after the write, does the UI show a Snackbar/confirmation, refresh the displayed data, or navigate appropriately? A write that succeeds but leaves stale data on screen looks broken to the user even if the DB is correct.
5. **Round-trip check** — if you have DB access (via bash/mysql or the mysql tool), after tracing the code, actually query the affected table to see if a test write would plausibly hit real rows correctly (correct column names, correct types) — don't just assume the SQL string is correct because it looks right.

## Excuses that are NOT acceptable as a reason to skip verification

| Excuse | Why it doesn't hold |
|---|---|
| "The build passed with zero errors" | Build success only proves the code compiles, not that the write path works — this project's exact recurring bug (NoTracking) compiles fine while doing nothing. |
| "The code looks the same as the existing pattern" | Existing code in this project has been found to have the same bug before (see `dotnet-efcore-nordicbees` skill Rule 1) — pattern-matching to existing code isn't proof, it might propagate the same bug. |
| "It's just a small UI change" | Small UI changes are exactly where a missing/miswired event handler is easiest to introduce and easiest to miss without tracing. |
| "I already read the file, it looks correct" | Reading code and confirming it *reads* correctly is not confirming it *executes* correctly — trace the actual call chain, don't eyeball it. |
| "The reviewer will catch it at the end" | The `reviewer` agent only runs once, after ALL tasks — waiting means 14 tasks' worth of the same bug compounding before anyone notices. Catch it per-task instead. |

## What to actually report

Instead of just "✅ DONE — zero errors, committed", for any UI/data-writing task also state explicitly in your report:
- Which Service method(s) the new/changed UI action calls
- Confirmation that method uses the correct write pattern (`ExecuteSqlRawAsync` or genuine INSERT), quoting the relevant line
- If you could not fully trace or verify this (e.g. the caller only gave you the .razor file, not the Service it calls), say so explicitly and name what you'd need to check — don't silently assume it's fine.

## If Playwright/browser testing becomes available later

If this project later adds Playwright-based Blazor E2E tests (a real "click the button, check the DB" test, not just a compile check), prefer running those over manual code tracing wherever they exist. Until then, the manual call-chain trace above is the enforced minimum.
