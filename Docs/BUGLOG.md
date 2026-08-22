# NordicBeesERP — Bug Log

Running log of confirmed bugs (not features) — what happened, root cause,
fix, and whether a guardrail (semgrep rule / skill update) was added to
prevent recurrence. Reviewed periodically to spot patterns worth turning
into systemic fixes.

## Format per entry
YYYY-MM-DD — [short title]

Symptom: what the user/agent observed
Root cause: the actual mechanism
Fix: what was changed
Guardrail added: semgrep rule id / skill update / none (and why not)
Category: EF-core | UI-form | infra | encoding | other
Error class: a short, STABLE, reusable tag identifying the failure
  MECHANISM, not this one incident (e.g. `grep-scope-too-broad`,
  `reviewer-self-approval`, `context-loss-compaction`,
  `mudblazor-tab-value-commit`). Reuse an existing tag verbatim if this
  incident is the same underlying mechanism recurring — that reuse is
  the entire point (see Status below). Only mint a new tag if the
  mechanism is genuinely new.
Status: monitoring (guardrail just added, no re-exposure yet) |
  stable (guardrail has survived at least one later exposure with no
  recurrence) | escalated (this error class recurred AFTER a guardrail
  was already added for it once — the prompt-text/skill-note guardrail
  was proven insufficient, so a stronger mechanical check, e.g. a
  semgrep rule or an `agent-guardrails` check, is needed instead of
  another similar sentence)

**Before writing a NEW entry, check whether its Error class already
exists in this log** (grep this file for the candidate tag). If it does
and the same mechanism just recurred, that is objective, non-self-graded
evidence the earlier guardrail didn't work — file the new entry with
`Status: escalated`, referencing the earlier entry's date, and prefer
escalating the existing guardrail (weaker→stronger mechanism) over
adding a near-duplicate note. Entries predating this field (above the
2026-08-23 entries below) don't have Error class/Status retrofitted —
treat them as `Category`-only history, not part of the recurrence check.


## Entries

### 2026-07-17 — Order status stuck on draft after packing
- **Symptom**: All lines packed, but order header status never advanced.
- **Root cause**: `MarkReadyForPickupCheckAsync` WHERE clause excluded 'draft' from the allowed source statuses.
- **Fix**: Added 'draft' to the status IN clause.
- **Guardrail added**: none (one-off business logic bug, not a generalizable pattern)
- **Category**: EF-core

### 2026-07-17 — Expiry date not saved when packing order line
- **Symptom**: MudDatePicker value not persisted after selecting a date and confirming.
- **Root cause**: `@bind-Value` on MudDatePicker inside a dialog didn't commit before Confirm() read the field.
- **Fix**: Switched to explicit Value + ValueChanged pattern.
- **Guardrail added**: added to `mudblazor` skill's known pitfalls
- **Category**: UI-form

### 2026-07-17 — EF Core translation failure on invoice search
- **Symptom**: Runtime exception "Translation of method 'string.Contains' failed".
- **Root cause**: `.Contains(x, StringComparison.OrdinalIgnoreCase)` used inside a LINQ query against NordicBeesErpContext — MariaDB provider can't translate it.
- **Fix**: Replaced with `EF.Functions.Like`.
- **Guardrail added**: semgrep rule `nordicbees-stringcomparison-in-linq`
- **Category**: EF-core

### 2026-07-17 — DBNull mapping error creating orders
- **Symptom**: "no store type mapping for properties of type 'DBNull'" on order creation.
- **Root cause**: `(object?)x ?? DBNull.Value` pattern boxes as System.DBNull, which the MariaDB provider can't map.
- **Fix**: Pass nullable values directly without the cast/fallback.
- **Guardrail added**: semgrep rule `nordicbees-dbnull-explicit-cast`
- **Category**: EF-core

### 2026-07-17 — FK constraint failure creating orders
- **Symptom**: Intermittent FK violation on order_lines insert.
- **Root cause**: LAST_INSERT_ID() read on a different physical connection than the INSERT, due to connection pooling without an explicitly held-open connection.
- **Fix**: Explicitly open and hold one connection for the whole method.
- **Guardrail added**: already covered by `dotnet-efcore-nordicbees` skill's connection-scope guidance (pre-existing rule, reinforced)
- **Category**: EF-core

### 2026-07-17 — Delivery.cs column/type mismatch
- **Symptom**: "Unknown column 'd.inspection_by'" loading invoice details with a linked delivery.
- **Root cause**: Model mapped `[Column("inspection_by")] string?` but DB actually has `inspection_by_user_id` (int, FK).
- **Fix**: Corrected the property name/type to match DB.
- **Guardrail added**: none (one-off schema drift, covered generically by `dotnet-efcore-nordicbees` Rule 2 — "never assume a column exists based on the model alone")
- **Category**: EF-core

### 2026-08-18 — Artwork "Upload first version" button redirects to /login on prod
- **Symptom**: Clicking "Ikelti pirma versija" on an artwork asset detail page instantly redirected to /login in production, before any file was selected. Dev was unaffected; the sibling "Ikelti nauja versija" button worked.
- **Root cause**: `UploadFirstVersion()` called `Navigation.NavigateTo(url, forceLoad: true)`, forcing a full browser reload (a fresh HTTP GET to the target URL) instead of an in-app (SPA) navigation. The staging/production server enforces authentication on full-page GETs (ASP.NET cookie `LoginPath="/login"`), so an unauthenticated full GET to the upload URL is answered with `302 -> /login?ReturnUrl=...` and the browser is redirected to the login page. This is NOT a reverse proxy — there is no nginx config on the host touching ports 8080/8081 (verified). The sibling button used SPA navigation (no forceLoad), which is why it worked.
- **Fix**: Removed `forceLoad: true` from `UploadFirstVersion()` so it performs SPA navigation like the sibling button, avoiding the full reload entirely. Committed as e2593dc, released in v0.11.169.
- **Verified**: FULL (as of 2026-08-18 13:16 UTC). The fix is confirmed present in the deployed v0.11.169 build (footer shows v0.11.169; code review of e2593dc confirms forceLoad removed) and SPA navigation is proven working on staging. With the separate circuit-crash bug fixed (see 2026-08-18 MainLayout entry) and staging on v0.11.170, a fresh asset was created on staging and "Ikelti pirma versija" was clicked via SPA nav -> landed on /artwork/upload/0 (the real upload page: "Upload New Version for ..." with a file-drop area), NOT /login. Console: 0 errors. The original crash that blocked this test (SignalR circuit terminated at the home route, 2026-08-18 12:36:02) was a different pre-existing bug; now resolved, the button works end-to-end.
- **Guardrail added**: none yet (one-off inconsistency — file-download endpoints intentionally keep forceLoad: true). Consider a repo convention that forceLoad:true is reserved for genuine file/PDF download endpoints only.
- **Category**: infra

### 2026-08-18 — Unhandled SignalR circuit exception killing authed pages (MainLayout)
- **Symptom**: On staging (v0.11.169) admin login succeeded, but the next navigation (e.g. to "/") threw "There was an unhandled exception on the current circuit, so this circuit will be terminated" and froze the UI. /login was unaffected.
- **Root cause**: MainLayout.OnInitializedAsync called CompanySettingsService.GetSettingsAsync() unguarded; that service throws InvalidOperationException when the company_settings row is missing or on a transient DB error. An exception in a layout's lifecycle terminates the Blazor circuit. /login was immune because Login.razor declares @layout EmptyLayout, so it never rendered MainLayout.
- **Fix**: Wrapped the GetSettingsAsync() call in MainLayout.OnInitializedAsync in try/catch (commit 3055a66, v0.11.170). A missing/erroring company_settings row now degrades gracefully instead of killing the circuit.
- **Verified**: FULL — on staging v0.11.170 the home route renders completely with 0 console errors/warnings; the crash is gone. The company_settings row was also confirmed present on staging, so the guard is now belt-and-suspenders.
- **Guardrail added**: none (one-off missing try/catch). Consider a convention: any DB call in a layout/OnInitializedAsync must be wrapped defensively.
- **Category**: infra

### 2026-08-21 — Invoice create says "pasirinkite klientą" despite selected client
- **Symptom**: On /invoices/create, picking a client by typing + Tab left the name visible but Save showed "Prašome pasirinkti klientą!" every time.
- **Root cause**: e54d5b1 swapped the client-selection dialog for an inline MudAutocomplete; with MudBlazor 8.15.0's default SelectValueOnTab=false, Tab leaves only display text while ValueChanged never fires, so _selectedCustomerId stayed null and ValidateInvoice failed its HasValue check.
- **Fix**: commit 1f4aaaf (v0.11.217) — @bind-Text tracking + SelectValueOnTab="true" + TryResolveSelectedCustomerFromText() exact-match fallback in ValidateInvoice. Follow-up commit dbc146e (v0.11.218) also auto-focuses/opens the client autocomplete on first render.
- **Guardrail added**: none yet (component-default gotcha, one-off). Candidate: note in mudblazor skill that MudAutocomplete value commitment requires SelectValueOnTab or explicit selection when used inline in forms.
- **Category**: UI-form

### 2026-08-21 — Build artifact self-nesting caused MSB3021 build failure
- **Symptom**: `dotnet build`/`dotnet test` failed with MSB3021 "path too long"; `Tests/NordicBeesERP.Tests/bin/Debug/net10.0/Tests/...` self-nested ~21 levels deep, growing by one level per build.
- **Root cause**: `NordicBeesERP.csproj` only excluded `Compile` items under `Tests/**` (`<Compile Remove="Tests/**" />`). The Web SDK's implicit item globs still picked up `Content`/`None`/`EmbeddedResource` files under `Tests/NordicBeesERP.Tests/bin` and `obj` (leftover JSON/metadata from a previous test build) and copied them into the main project's own output tree under a `Tests/` subfolder. Since the test project references the main project via `ProjectReference`, the next test build copied the main project's output (now containing a nested `Tests/` copy) back into its own bin — each build compounding one more level of nesting.
- **Fix**: Added `Content`/`None`/`EmbeddedResource` excludes for `Tests/**` alongside the existing `Compile` exclude in `NordicBeesERP.csproj` (commit 99710a3, v0.11.222), matching the mechanism above exactly. One-time `rm -rf` of the existing corrupted trees was also needed to clear the already-poisoned state.
- **Guardrail added**: csproj glob exclusion (commit 99710a3) prevents recurrence at the source. AGENTS.md also updated with a fast-path rule so agents don't waste time deep-diagnosing this class of issue in the future.
- **Category**: infra
