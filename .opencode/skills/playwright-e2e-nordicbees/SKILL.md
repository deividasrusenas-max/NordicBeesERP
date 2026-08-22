---
name: playwright-e2e-nordicbees
description: Real browser-based end-to-end verification for NordicBeesERP using the Playwright MCP tool. Use this whenever a task adds or changes a button, form, dialog, or any UI-to-database write path, and you need to prove the feature actually works — not just that the code compiles and reads correctly. This is the objective, automated companion to the verify-before-done skill's manual call-chain tracing.
---

# Playwright E2E Verification — NordicBeesERP

**Core principle: a real browser click that produces a real DB row is the only fully objective proof a feature works.** Manual code tracing (per the `verify-before-done` skill) is good and necessary, but it can still miss a wiring mistake (e.g. an `OnClick` handler that's present but never actually bound, a Blazor render-mode issue, a JS interop failure). This skill closes that gap.

## When to use this vs. manual tracing only

- **Always do the manual call-chain trace first** (per `verify-before-done`) — it's fast and catches most issues.
- **Additionally run a real Playwright check** when: the task is a NEW page/dialog/wizard step, the task touches an existing page's save/submit flow, or the caller explicitly asks for E2E verification.
- Skip Playwright for: pure model/service changes with no UI surface, read-only display changes, or when the dev server isn't running (see below).

## Prerequisites — check before attempting

1. Is the dev server actually running? This project runs via `dotnet run` or a configured launch profile — check for a listening port (commonly 5000/5001/7xxx range for Kestrel) before trying to navigate. If it's not running, report that E2E verification requires the dev server to be started first — do NOT start it yourself as a side effect unless explicitly told to (starting a long-running process from an automated task has its own risks — orphaned processes, port conflicts).
2. Confirm the base URL (check `Properties/launchSettings.json` or `appsettings.Development.json` for the actual configured port) — don't guess a default port.

## If the UI shows an error (Snackbar, failed save, etc.) — STOP before editing code

A Snackbar error message is a SYMPTOM, not a diagnosis. Before touching any
code:

1. Check the `dotnet run` server console output for the actual exception —
   the real error (SQL error, EF Core translation failure, null reference,
   FK violation, etc.) is almost always printed there in full, with a stack
   trace, while the Snackbar text is often a generic wrapper
   (`$"Klaida: {ex.Message}"`) that hides the real cause. This project's
   real bugs (schema mismatches, EF query translation errors, connection
   scope issues) have consistently been diagnosable from the server console
   in under 5 minutes — never diagnosable from the browser alone.
2. If the console log doesn't make the cause obvious, run a direct
   `nordicbees-db` query on the relevant table(s) to compare expected vs
   actual state — this is usually faster and more precise than more
   browser interaction.
3. Only after you can name the actual root cause (a specific line, a
   specific mismatched column/type, a specific null value) should you
   consider a code change. If after checking console + DB you still cannot
   identify a concrete root cause, STOP and report exactly what you
   checked and what you found — do not start making speculative edits
   hoping one of them fixes it.

## Hard stop condition — do not loop indefinitely

If after 3 total attempts (browser interaction + log/DB check cycles) the
root cause is still not identified, STOP. Report to the orchestrator:
exactly what was tried, the exact console/DB output seen each time, and
that you could not identify a root cause — do not keep attempting
different speculative code edits past this point. A clear "I couldn't
find it, here's everything I checked" is always more useful than
open-ended trial-and-error edits, which have previously produced hours of
unproductive, messy changes on this project.

## Basic verification pattern

1. Navigate to the relevant page: `browser_navigate` to the target URL.
2. Take a snapshot (`browser_snapshot`) to see the accessibility tree — this is how you find the actual element refs to interact with, not by guessing CSS selectors.
3. Interact: fill fields (`browser_fill_form` / `browser_type`), click the button (`browser_click`) using the ref from the snapshot.
4. Wait for the expected result — a Snackbar confirmation, a navigation, or the record appearing in a list (`browser_wait_for` or another snapshot).
5. **Cross-check with the database** — after the UI action completes, use the `nordicbees-db` MCP tool to query the actual table and confirm the expected row/values exist. This is the step that actually proves the round-trip, not just that the UI "looked like" it worked.

## Project-specific notes

- **Auth**: this app uses cookie-based ERP auth (`erp_users` table) — you may need to log in first via the login page before reaching most warehouse/delivery pages. Check for a test/seed user credential in `appsettings.Development.json` or ask rather than guessing credentials.
- **MudBlazor components**: MudBlazor renders custom elements with ARIA roles — `browser_snapshot`'s accessibility tree should still expose them correctly (buttons, textboxes, comboboxes), but dialogs may render in a portal/overlay — if a dialog's fields don't appear in the snapshot immediately after opening it, wait briefly and re-snapshot.
- **Cleanup**: if your test run creates real data (a delivery, a container, a non-conformance record), note this in your report — don't silently leave test data in the dev database without flagging it, since dev DB is shared.
- **Never** run E2E verification against anything other than the local dev server / dev DB. Never point Playwright at a staging or production URL from an automated task.

## Report format

Include in your report:
- Which page/flow was tested and the exact steps taken
- The DB query used to confirm persistence, and its actual result
- Any test data created that wasn't cleaned up
- If the dev server wasn't running or auth blocked you, say so explicitly rather than skipping silently
