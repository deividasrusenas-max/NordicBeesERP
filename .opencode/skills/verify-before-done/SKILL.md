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

## xUnit service-layer tests now exist — use them, don't just trace by hand

As of the 2026-07-19/20 FROZEN.md write-pattern audit, this project has a
real xUnit integration test project at `Tests/NordicBeesERP.Tests`,
running against a dedicated, isolated database (`nordic_bees_erp_test`,
same MariaDB/MySQL host as dev, never prod or staging). This supersedes
step 5's "round-trip check" as a manual, ad-hoc DB query — prefer a real
test instead wherever the Service method being verified doesn't already
have one.

- `Tests/NordicBeesERP.Tests/DbTestFixture.cs` provides
  `IClassFixture<DbTestFixture>` with an `IDbContextFactory<NordicBeesERPContext>`
  pointed at `nordic_bees_erp_test`, same `QueryTrackingBehavior.NoTracking`
  as production.
- `Tests/NordicBeesERP.Tests/SupplierServiceTests.cs` is the reference
  pattern: insert a minimal entity via `context.Add()+SaveChangesAsync()`,
  call the real Service method, re-read with a **brand-new** DbContext to
  prove the write actually reached the database (not just mutated an
  in-memory object), clean up the row afterward. For any Service method
  with a nullable string field flowing into `ExecuteSqlRawAsync`, also add
  a test that a null value persists as SQL NULL, not an empty string —
  see `UpdateBusinessPartnerAsync_NullEmail_PersistsAsSqlNullNotEmptyString`
  for the exact pattern (this caught a real regression:
  `?? ""` silently converting NULL to `''`).
- Per this project's own `.kilo/prompts/plan.md`/`code.md` rules: any task
  that creates or modifies a DB-write method MUST include writing or
  updating a corresponding test in the same delegation — not a
  deferrable follow-up. `reviewer` must confirm a test exists and
  actually exercises the changed path before approving.
- Run tests with:
  `TEST_DB_CONNECTION="Server=100.110.26.80;Port=3306;Database=nordic_bees_erp_test;Uid=erp_user;Pwd=NordicBees2024;SslMode=none;AllowPublicKeyRetrieval=True;" dotnet test`
  and report the actual pass/fail line (e.g. "Passed! - Failed: 0,
  Passed: 2, Skipped: 0, Total: 2") — a prose claim of "tests pass" is
  not acceptable without this real output.
- `bump-version.sh` runs `dotnet test` automatically as a release gate
  when `TEST_DB_CONNECTION` is set in the environment — a failing test
  blocks the release exactly like a failing build.

## If Playwright/browser testing becomes available later

If this project later adds Playwright-based Blazor E2E tests (testing
the full UI-to-database path through a real browser, not just the
Service layer), those complement — not replace — the xUnit Service-layer
tests above. For UI wiring specifically (does the button actually call
the Service method at all), the manual call-chain trace in steps 1-2
above is still the enforced minimum until Playwright tests exist.
