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
  another similar sentence) | N/A (no guardrail was added for this entry
  — see the entry's own Guardrail line for why; there is nothing to
  monitor the effectiveness of, and this error class is currently
  UNGUARDED — a periodic review should treat N/A entries as the
  highest-priority candidates for actually adding a guardrail, since
  recurrence here would be silent and unprotected)

**Before writing a NEW entry, check whether its Error class already
exists in this log** (grep this file for the candidate tag). If it does
and the same mechanism just recurred, that is objective, non-self-graded
evidence the earlier guardrail didn't work (or, for an N/A entry, that
the lack of a guardrail is now costing real time) — file the new entry
with `Status: escalated`, referencing the earlier entry's date, and
prefer escalating the existing guardrail (weaker→stronger mechanism, or
N/A→an actual guardrail) over adding a near-duplicate note.

**2026-08-23 retrofit note**: all entries below (including the ones
originally logged 2026-07-17 through 2026-08-21, before this field
existed) now have Error class/Status assigned retroactively, so the
recurrence check covers the full history, not just entries written after
this system existed. Each retrofitted tag was chosen by re-reading that
entry's actual Root cause and picking the underlying MECHANISM, not
re-labeling the symptom.


## Entries

### 2026-08-24 — Credit-note edit save fails: Column 'original_invoice_id' cannot be null
- **Symptom**: Saving an edited credit note via CreditNoteEdit.razor threw `Column 'original_invoice_id' cannot be null` (both Save and Save+PDF buttons).
- **Root cause**: RECURRENCE of `schema-drift-unverified-column-mapping` (first logged 2026-07-17 for Delivery.cs; sibling incident logged earlier on 2026-08-24 for `applied_invoice_id`). The edit page loaded `OriginalInvoiceId` but never stored it in a page field, so `BuildCreditNoteRequest()` built the update request WITHOUT setting `OriginalInvoiceId`; `UpdateCreditNoteAsync` line 571 then assigned `request.OriginalInvoiceId` (null) into the entity, and the raw UPDATE (line ~663, `original_invoice_id = {1}`) wrote NULL into the NOT NULL `credit_notes.original_invoice_id` column. The earlier applied_invoice_id entry explicitly flagged this same latent path in `UpdateCreditNoteAsync`; its `?? creditNote.AppliedInvoiceId` guard did not extend to the sibling `OriginalInvoiceId` column.
- **Fix**: commits 1557945 + f6cdce8 + 2a689e4 (v0.11.260-262) — (a) CreditNoteEdit preserves `OriginalInvoiceId` through the round-trip; (b) service line 571 now `request.OriginalInvoiceId ?? creditNote.OriginalInvoiceId` (defense-in-depth); (c) xUnit test `UpdateCreditNoteAsync_NullOriginalInvoiceId_PreservesExistingInvoiceId` drives the real service against `nordic_bees_erp_test` and round-trip-reads `original_invoice_id` preserved. No schema change.
- **Verified**: dotnet build 0 errors each round; `dotnet test` — `Passed: 20, Failed: 0`; reviewer APPROVED every diff.
- **Guardrail added**: none new mechanical — the `?? existing` service default now covers both `AppliedInvoiceId` AND `OriginalInvoiceId`, but the mechanism that silently wrote NULL was missed once for each column.
- **Category**: EF-core
- **Error class**: `schema-drift-unverified-column-mapping`
- **Status**: escalated — THIRD incident in this error class (Delivery.cs 2026-07-17, applied_invoice_id 2026-08-24, original_invoice_id this entry). Multiple prompt/skill sentences have not prevented it. Recommend a MECHANICAL check as proposed in the applied_invoice_id entry: a semgrep / `agent-guardrails` rule (or `DESCRIBE`-based nullability audit) that flags any DB-NOT-NULL column written from a `??`-able request field without a guaranteed non-null fallback, so it cannot be skipped by an agent.

### 2026-08-24 — Fixer stuck in Compaction→re-summarize loop after real completion
- **Symptom**: Task genuinely finished (v0.11.242, `FormatAmountHelper.cs`
  commit, tag pushed, `agent-guardrails` score 90/100), but the session
  then entered ~15-20 back-to-back cycles of Compaction → Fixer
  re-generating a near-identical "Objective/Important Details/Work
  State" summary → a `task_complete({})`-looking line → Compaction again.
  No further file changes occurred during the loop. User manually
  interrupted it after noticing "unrecognizable garbled output"
  accompanying the cycling.
- **Root cause**: the harness's `opencode-auto-resume` npm plugin
  (v1.1.3, `.opencode/node_modules/opencode-auto-resume`) documents a
  real, structured `task_complete` tool that, when genuinely invoked,
  stops the plugin's auto-"continue" behavior deterministically. Fixer's
  own prompt (`fixer.md`) never told the model this tool existed or that
  it needed to be called as a real structured tool call — only that a
  plain-text ✅ DONE/❌ BLOCKED report was expected. The `task_complete({})`
  line visible in the transcript is consistent with the model (Qwen3.6-
  35B-A3B) printing this as plain text/pseudo-tool-call rather than
  issuing a real tool invocation — a failure mode the SAME plugin's own
  README documents separately ("Tool calls as raw text"). Without a real
  tool call, the plugin never received the deterministic completion
  signal, so it kept treating the idle session as stalled/needing a
  "continue", and fixer's own compaction-recovery instructions ("verify
  your position before every step") caused it to re-generate the same
  state summary each time it was resumed.
- **Investigated and explicitly ruled out**: fixer's small context limit
  (65536, vs coder's 262144) was initially suspected as a contributing
  factor (more frequent compaction → more loop iterations) and a config
  bump to 262144 was drafted but NOT applied — user confirmed fixer's
  context is intentionally small due to single-GPU-card VRAM allocation
  in the 4x RTX 3090 setup, not an oversight. Do not revisit this as a
  "fix" without an explicit VRAM-budget conversation first.
- **Fix**: added a new mandatory section to `fixer.md` ("after writing
  your DONE/BLOCKED report, signal completion via the real tool")
  instructing the model to make a REAL structured `task_complete` tool
  call (if present in its tool list) as its final action, and explicitly
  telling it that typing the tool name as text is not equivalent to
  calling it.
- **Guardrail added**: none yet — this is a prompt-text mitigation only
  (Tier 3). Status below reflects this is unverified against a real
  re-exposure.
- **Category**: infra
- **Error class**: `post-completion-continue-loop`
- **Status**: escalated — user re-ran the same task after the `fixer.md`
  prompt fix was applied AND (later) after the fix itself was committed
  to git, and the Compaction→re-summarize loop recurred both times. This
  is objective evidence the Tier-3 prompt-text mitigation is
  insufficient for this local model under this failure mode — do not
  attempt a third prompt-text variant. Per the escalation rule at the
  top of this file, the next step must be a Tier-1 mechanical check:
  the `nordicbees-quality-monitor.ts` plugin detecting N (e.g. 3)
  consecutive Compaction→Fixer (or any agent) cycles with zero new
  tool calls between them, and forcing a hard BLOCKED stop
  deterministically — not dependent on the model reading/following any
  prompt sentence. This is now the same underlying mechanical gap
  identified separately in the `deadlock-constraint-conflict` entry
  below and in `harness-blocked-state-not-terminated` (2026-08-22) —
  three separate incidents now point to the same missing Tier-1
  circuit-breaker as the real fix, not another prompt edit.

### 2026-08-24 — Fixer deadlock: generates a plan violating its own constraint, never executes or escalates
- **Symptom**: A `fixer` run hit `./bump-version.sh patch` refusing to
  run because `.opencode/prompts/fixer.md` and `Docs/BUGLOG.md` were
  modified but uncommitted (a side effect of Claude's own direct file
  edits via filesystem MCP being left uncommitted). Fixer correctly
  identified the blocker, but instead of following the ALREADY-EXISTING
  "unrelated files block bump-version.sh -> report BLOCKED once and
  stop" rule (see `harness-blocked-state-not-terminated`, 2026-08-22),
  it formulated a NEW plan each cycle ("commit these two files with a
  generic message"), never executed that plan, and never reported
  BLOCKED -- ~8 identical cycles, each re-running `git status`,
  re-discovering the same two files, re-writing the same unexecuted
  "Next Move" plan.
- **Root cause**: a genuine decision deadlock, not a compaction
  artifact. This task's CRITICAL CONSTRAINTS block explicitly said the
  two files "must NOT be staged or committed" (correct — they weren't
  part of this task). The model then proposed committing them anyway
  (contradicting its own stated constraint), never executed the
  contradiction, and never fell back to the existing, directly-
  applicable "unrelated files -> BLOCKED, stop" rule — likely because
  that rule's wording doesn't explicitly address the case where the
  blocking files are ones a task-specific constraint forbids touching.
- **Fix**: strengthened the existing "unrelated files block
  bump-version.sh" rule in `fixer.md` to name this exact case: if the
  blocking files are ones you were told NOT to touch, report BLOCKED
  with the exact file names and STOP, same as any other unrelated-file
  blocker. Do NOT invent a workaround (committing anyway, `git stash`)
  unless a human explicitly instructs one.
- **Guardrail added**: none yet — prompt-text only (Tier 3). Given the
  sibling entry above (`post-completion-continue-loop`) already showed
  a prompt-text fix failing to hold for a related class of "agent
  should stop but doesn't" failure, this entry's prompt-text fix should
  be treated with the same skepticism until a real re-exposure confirms
  or denies it.
- **Category**: infra
- **Error class**: `deadlock-constraint-conflict`
- **Status**: monitoring — but see the note above: this Error class and
  `post-completion-continue-loop` are both instances of the same deeper
  gap (no Tier-1 mechanical stop-loop circuit-breaker exists yet), and
  should be re-evaluated together, not independently, once the
  `n_toolcalls`-based circuit-breaker (see `HARNESS_STATUS.md` §13
  Etapas 0) is built.

### 2026-08-24 — Orchestrator re-invokes agent cycle indefinitely on genuinely idle state (no new user input)
- **Symptom**: A third, distinct loop type in the same session. Unlike
  the two entries above, the agent's own response was CORRECT each
  cycle: "Active: (none) / Blocked: (none)", i.e. no work pending, no
  error, genuinely waiting for a new directive. Despite this, the
  Compaction→Fixer cycle continued indefinitely anyway — each
  iteration re-loading the same 4 skills (mudblazor, verify-before-done,
  url-filter-persistence, dotnet-efcore) as if starting a brand-new
  task, with no new user message and no new task content anywhere in
  the loop.
- **Root cause**: NOT a model/prompt problem — the agent answered
  correctly every time. This is a harness/loop-control gap: the
  orchestration loop (`opencode-auto-resume` and/or the core OpenCode
  session driver) has no "await new user input and pause" terminal
  state distinct from "keep iterating". A correct "nothing to do,
  waiting" report doesn't stop the loop, because the loop's continuation
  condition is apparently keyed to a completion SIGNAL (task_complete,
  discussed in the entry above) rather than to "is there a new,
  unprocessed user message queued". Skill re-injection firing every
  cycle confirms the harness is treating each empty iteration as a new
  task start, not recognizing it's re-entering the same idle state.
- **Why this is more dangerous than the other two**: the other two loop
  types eventually surface a signal an agent COULD act on (an unexecuted
  plan, a stuck completion report) that a stronger prompt rule might
  catch. This one never will, by design — the agent is behaving
  correctly every single cycle, so no prompt-text rule aimed at agent
  behavior can fix it. It can silently burn context/inference (every
  cycle is a real llama-swap call, ~5-7s + KV-cache cost) indefinitely
  with nothing resembling an error to trigger any existing rule.
- **Fix**: NOT YET APPLIED — no safe prompt-text mitigation exists for a
  harness/loop-control-level gap; this needs a real code-level fix in
  the orchestration driver (or `nordicbees-quality-monitor.ts`
  detecting "N consecutive cycles with identical skill-injection set and
  zero new user-message content" and forcing a hard pause) — deferred to
  a focused Claude Code session, not attempted live in this already-long
  chat session.
- **Guardrail added**: none yet.
- **Category**: infra
- **Error class**: `idle-no-input-loop`
- **Status**: N/A — no guardrail exists yet; this is the highest-priority
  of the three 2026-08-24 loop entries for the planned Tier-1
  `n_toolcalls`/cycle-detection circuit-breaker, since it is structurally
  unreachable by any prompt-text fix aimed at the agent's own behavior.

### 2026-08-24 — SYNTHESIS: four loop types same day share one root pattern (plan-without-execution gap)
- **Symptom**: A fourth loop (same day, after all three fixes above were
  applied/committed): all preconditions for `git commit` were already
  satisfied (file staged, BUCKET_GROUP check passed, no blocker), yet
  `fixer` never executed the `git commit` call itself — each cycle it
  re-ran `git status`/`git log` (already-confirmed facts), re-wrote an
  identical "Next Move: 1. Run git commit..." text block, and stopped
  short of the actual tool call. At least one cycle's output was a bare
  `(1):` with no content, consistent with generation being cut off at
  exactly the plan-to-action boundary.
- **Pattern across all four 2026-08-24 entries** (this one,
  `post-completion-continue-loop`, `deadlock-constraint-conflict`,
  `idle-no-input-loop`): three of the four (this entry,
  `post-completion-continue-loop`, `deadlock-constraint-conflict`) share
  the SAME underlying shape — the model correctly ARTICULATES the right
  next step in text (a completion claim, a commit plan, a "Next Move"
  block) but never CROSSES from describing the action to actually
  invoking the corresponding tool call. Only `idle-no-input-loop` is a
  pure harness-level issue independent of this pattern (there the model's
  text was already correct and final — nothing to "execute").
- **Root cause hypothesis** (plausible, not yet proven): `fixer.md` grew
  by pure incident-driven accretion over 2026-08-21 through 2026-08-24
  (7 separate "Real incident this rule exists because of" narratives,
  ~290 lines total) without ever being restructured. It now contains
  FIVE separate, overlapping "what to do when stuck/verifying"
  mechanisms (Scope Check, General Fallback, Position-Verify-Before-
  Every-Step, Blocked-Report-Once-and-Stop, and the newest
  task_complete-tool-call rule) layered on top of each other, including
  at least one direct tension: "verify your position before EVERY step"
  (re-check state) vs. "once BLOCKED, do NOT re-check" (stop checking) —
  the model must correctly classify its own state to know which of these
  applies, a meta-cognitive judgment call a smaller local model (Qwen3.6-
  35B-A3B) may not reliably make. The heavy verification/narration
  emphasis throughout the file may be consuming generation "budget" such
  that turns end after articulating the right next step but before
  emitting the actual tool call for it.
- **Explicitly NOT the fix attempted today**: no fifth prompt-text patch
  was added live in this session for this pattern — four incidents in
  one day is itself evidence that incremental prompt patching has
  stopped being a net positive for `fixer.md` specifically, and may now
  be part of the problem (longer prompt → more internal rules to weigh
  → more opportunities for exactly this kind of turn-ending-before-
  action gap).
- **Fix direction (deferred to a focused Claude Code session, NOT
  attempted live)**: restructure `fixer.md` rather than extend it —
  move all "Real incident..." narratives out of the prompt into
  `BUGLOG.md`-only (they already live there; the prompt only needs a
  short reference, not the full paragraph), and collapse the five
  overlapping stuck-state mechanisms into one single, unambiguous state
  sequence (e.g. a plain Act → Blocked → Report → Stop chain) so the
  model has fewer, clearer rules to weigh per turn instead of more,
  longer ones. Goal: a SHORTER file with fewer but clearer rules, not a
  longer one with another rule appended.
- **Guardrail added**: none — this entry is a synthesis/diagnosis, not a
  fix.
- **Category**: infra
- **Error class**: `plan-without-execution-gap` (new tag — covers this
  entry and, on reflection, describes the shared mechanism in
  `post-completion-continue-loop` and `deadlock-constraint-conflict`
  more precisely than either of their original tags; those two entries'
  tags are left as-is since they were already filed, but a future
  periodic BUGLOG review should treat all three as one family when
  evaluating whether the Tier-1 circuit-breaker and/or the `fixer.md`
  restructure actually resolved them)
- **Status**: N/A — no guardrail exists; this is now the primary
  evidence base for BOTH planned fixes (Tier-1 `n_toolcalls` circuit-
  breaker AND the `fixer.md` structural rewrite), and neither should be
  considered validated until re-tested against a real `fixer` task after
  both are in place.

### 2026-07-17 — Order status stuck on draft after packing
- **Symptom**: All lines packed, but order header status never advanced.
- **Root cause**: `MarkReadyForPickupCheckAsync` WHERE clause excluded 'draft' from the allowed source statuses.
- **Fix**: Added 'draft' to the status IN clause.
- **Guardrail added**: none (one-off business logic bug, not a generalizable pattern)
- **Category**: EF-core
- **Error class**: `status-transition-incomplete-allowlist`
- **Status**: N/A — no guardrail exists; a future status-transition method with a similarly incomplete allowed-source-status list would not be caught by anything today

### 2026-07-17 — Expiry date not saved when packing order line
- **Symptom**: MudDatePicker value not persisted after selecting a date and confirming.
- **Root cause**: `@bind-Value` on MudDatePicker inside a dialog didn't commit before Confirm() read the field.
- **Fix**: Switched to explicit Value + ValueChanged pattern.
- **Guardrail added**: added to `mudblazor` skill's known pitfalls
- **Category**: UI-form
- **Error class**: `mudblazor-bind-value-commit-timing`
- **Status**: monitoring — skill note added 2026-07-17, no confirmed re-exposure tracked yet

### 2026-07-17 — EF Core translation failure on invoice search
- **Symptom**: Runtime exception "Translation of method 'string.Contains' failed".
- **Root cause**: `.Contains(x, StringComparison.OrdinalIgnoreCase)` used inside a LINQ query against NordicBeesErpContext — MariaDB provider can't translate it.
- **Fix**: Replaced with `EF.Functions.Like`.
- **Guardrail added**: semgrep rule `nordicbees-stringcomparison-in-linq`
- **Category**: EF-core
- **Error class**: `ef-linq-untranslatable-stringcomparison`
- **Status**: monitoring — this is a mechanical (semgrep) guardrail, structurally stronger than a prompt reminder since it can't be skipped by an agent simply not reading a sentence; still marked monitoring rather than stable because no confirmed exposure count has been tracked yet, not because the guardrail itself is weak

### 2026-07-17 — DBNull mapping error creating orders
- **Symptom**: "no store type mapping for properties of type 'DBNull'" on order creation.
- **Root cause**: `(object?)x ?? DBNull.Value` pattern boxes as System.DBNull, which the MariaDB provider can't map.
- **Fix**: Pass nullable values directly without the cast/fallback.
- **Guardrail added**: semgrep rule `nordicbees-dbnull-explicit-cast`
- **Category**: EF-core
- **Error class**: `ef-dbnull-explicit-cast`
- **Status**: monitoring — mechanical semgrep guardrail, same reasoning as above

### 2026-07-17 — FK constraint failure creating orders
- **Symptom**: Intermittent FK violation on order_lines insert.
- **Root cause**: LAST_INSERT_ID() read on a different physical connection than the INSERT, due to connection pooling without an explicitly held-open connection.
- **Fix**: Explicitly open and hold one connection for the whole method.
- **Guardrail added**: already covered by `dotnet-efcore-nordicbees` skill's connection-scope guidance (pre-existing rule, reinforced)
- **Category**: EF-core
- **Error class**: `ef-connection-pool-race-last-insert-id`
- **Status**: monitoring — this is a prompt/skill-text guardrail (not mechanical), so it's the weaker kind — worth watching for recurrence more closely than the semgrep-backed entries above

### 2026-07-17 — Delivery.cs column/type mismatch
- **Symptom**: "Unknown column 'd.inspection_by'" loading invoice details with a linked delivery.
- **Root cause**: Model mapped `[Column("inspection_by")] string?` but DB actually has `inspection_by_user_id` (int, FK).
- **Fix**: Corrected the property name/type to match DB.
- **Guardrail added**: none (one-off schema drift, covered generically by `dotnet-efcore-nordicbees` Rule 2 — "never assume a column exists based on the model alone")
- **Category**: EF-core
- **Error class**: `schema-drift-unverified-column-mapping`
- **Status**: N/A — the generic skill rule is a soft mitigation (applies broadly, not specifically triggered by this pattern), not a dedicated guardrail for this exact mechanism; treat as effectively unguarded for recurrence-tracking purposes

### 2026-08-18 — Artwork "Upload first version" button redirects to /login on prod
- **Symptom**: Clicking "Ikelti pirma versija" on an artwork asset detail page instantly redirected to /login in production, before any file was selected. Dev was unaffected; the sibling "Ikelti nauja versija" button worked.
- **Root cause**: `UploadFirstVersion()` called `Navigation.NavigateTo(url, forceLoad: true)`, forcing a full browser reload (a fresh HTTP GET to the target URL) instead of an in-app (SPA) navigation. The staging/production server enforces authentication on full-page GETs (ASP.NET cookie `LoginPath="/login"`), so an unauthenticated full GET to the upload URL is answered with `302 -> /login?ReturnUrl=...` and the browser is redirected to the login page. This is NOT a reverse proxy — there is no nginx config on the host touching ports 8080/8081 (verified). The sibling button used SPA navigation (no forceLoad), which is why it worked.
- **Fix**: Removed `forceLoad: true` from `UploadFirstVersion()` so it performs SPA navigation like the sibling button, avoiding the full reload entirely. Committed as e2593dc, released in v0.11.169.
- **Verified**: FULL (as of 2026-08-18 13:16 UTC). The fix is confirmed present in the deployed v0.11.169 build (footer shows v0.11.169; code review of e2593dc confirms forceLoad removed) and SPA navigation is proven working on staging. With the separate circuit-crash bug fixed (see 2026-08-18 MainLayout entry) and staging on v0.11.170, a fresh asset was created on staging and "Ikelti pirma versija" was clicked via SPA nav -> landed on /artwork/upload/0 (the real upload page: "Upload New Version for ..." with a file-drop area), NOT /login. Console: 0 errors. The original crash that blocked this test (SignalR circuit terminated at the home route, 2026-08-18 12:36:02) was a different pre-existing bug; now resolved, the button works end-to-end.
- **Guardrail added**: none yet (one-off inconsistency — file-download endpoints intentionally keep forceLoad: true). Consider a repo convention that forceLoad:true is reserved for genuine file/PDF download endpoints only.
- **Category**: infra
- **Error class**: `blazor-forceload-fullreload-auth-redirect`
- **Status**: N/A — no guardrail exists; any future button copy-pasting `forceLoad: true` outside a genuine download endpoint would hit the same failure with nothing to catch it

### 2026-08-18 — Unhandled SignalR circuit exception killing authed pages (MainLayout)
- **Symptom**: On staging (v0.11.169) admin login succeeded, but the next navigation (e.g. to "/") threw "There was an unhandled exception on the current circuit, so this circuit will be terminated" and froze the UI. /login was unaffected.
- **Root cause**: MainLayout.OnInitializedAsync called CompanySettingsService.GetSettingsAsync() unguarded; that service throws InvalidOperationException when the company_settings row is missing or on a transient DB error. An exception in a layout's lifecycle terminates the Blazor circuit. /login was immune because Login.razor declares @layout EmptyLayout, so it never rendered MainLayout.
- **Fix**: Wrapped the GetSettingsAsync() call in MainLayout.OnInitializedAsync in try/catch (commit 3055a66, v0.11.170). A missing/erroring company_settings row now degrades gracefully instead of killing the circuit.
- **Verified**: FULL — on staging v0.11.170 the home route renders completely with 0 console errors/warnings; the crash is gone. The company_settings row was also confirmed present on staging, so the guard is now belt-and-suspenders.
- **Guardrail added**: none (one-off missing try/catch). Consider a convention: any DB call in a layout/OnInitializedAsync must be wrapped defensively.
- **Category**: infra
- **Error class**: `layout-lifecycle-unguarded-db-call`
- **Status**: N/A — no guardrail exists; any other layout/OnInitializedAsync method with an unguarded DB call would kill its circuit with nothing to catch it

### 2026-08-21 — Invoice create says "pasirinkite klientą" despite selected client
- **Symptom**: On /invoices/create, picking a client by typing + Tab left the name visible but Save showed "Prašome pasirinkti klientą!" every time.
- **Root cause**: e54d5b1 swapped the client-selection dialog for an inline MudAutocomplete; with MudBlazor 8.15.0's default SelectValueOnTab=false, Tab leaves only display text while ValueChanged never fires, so _selectedCustomerId stayed null and ValidateInvoice failed its HasValue check.
- **Fix**: commit 1f4aaaf (v0.11.217) — @bind-Text tracking + SelectValueOnTab="true" + TryResolveSelectedCustomerFromText() exact-match fallback in ValidateInvoice. Follow-up commit dbc146e (v0.11.218) also auto-focuses/opens the client autocomplete on first render.
- **Guardrail added**: none yet (component-default gotcha, one-off). Candidate: note in mudblazor skill that MudAutocomplete value commitment requires SelectValueOnTab or explicit selection when used inline in forms.
- **Category**: UI-form
- **Error class**: `mudblazor-autocomplete-tab-value-commit`
- **Status**: N/A — the candidate skill note mentioned in Guardrail added was never actually confirmed as written; treat as unguarded until verified present in the `mudblazor` skill's known pitfalls

### 2026-08-21 — Build artifact self-nesting caused MSB3021 build failure
- **Symptom**: `dotnet build`/`dotnet test` failed with MSB3021 "path too long"; `Tests/NordicBeesERP.Tests/bin/Debug/net10.0/Tests/...` self-nested ~21 levels deep, growing by one level per build.
- **Root cause**: `NordicBeesERP.csproj` only excluded `Compile` items under `Tests/**` (`<Compile Remove="Tests/**" />`). The Web SDK's implicit item globs still picked up `Content`/`None`/`EmbeddedResource` files under `Tests/NordicBeesERP.Tests/bin` and `obj` (leftover JSON/metadata from a previous test build) and copied them into the main project's own output tree under a `Tests/` subfolder. Since the test project references the main project via `ProjectReference`, the next test build copied the main project's output (now containing a nested `Tests/` copy) back into its own bin — each build compounding one more level of nesting.
- **Fix**: Added `Content`/`None`/`EmbeddedResource` excludes for `Tests/**` alongside the existing `Compile` exclude in `NordicBeesERP.csproj` (commit 99710a3, v0.11.222), matching the mechanism above exactly. One-time `rm -rf` of the existing corrupted trees was also needed to clear the already-poisoned state.
- **Guardrail added**: csproj glob exclusion (commit 99710a3) prevents recurrence at the source. AGENTS.md also updated with a fast-path rule so agents don't waste time deep-diagnosing this class of issue in the future.
- **Category**: infra
- **Error class**: `csproj-implicit-glob-nested-test-output`
- **Status**: monitoring — structurally this guardrail is stronger than a typical prompt rule (it eliminates the root cause at the build-config level, not just an instruction to avoid it), so recurrence should be effectively impossible unless the csproj exclusion is later reverted or the test project's output structure changes; still marked monitoring rather than stable since no confirmed post-fix exposure has been explicitly tracked

### 2026-08-22 — BUCKET_GROUP hardcode check was structurally broken (whole-repo grep)
- **Symptom**: `fixer` sessions repeatedly got stuck treating a permanently-true `grep` match as a blocker requiring investigation, across at least two separate tasks the same day, burning many minutes each time re-diagnosing the identical, unchanging state.
- **Root cause**: `fixer.md`'s hardcode-check step read `grep -r "BUCKET_GROUP" --include="*.cs" --include="*.razor" .` (whole repository) and required 0 matches to proceed — but `BUCKET_GROUP` is a real, legitimate `ContainerType` enum value used correctly throughout the warehouse module (Migrations, ContainerEnums.cs, Home.razor, several Delivery*.razor files). This check could never pass regardless of what was actually being committed, since it wasn't scoped to the current task's own change.
- **Fix**: Rescoped the check to `git diff --cached -- [task's own files] | grep "BUCKET_GROUP"` — only the staged diff of the current task, never the whole repo (commit on 2026-08-22, see `.opencode/prompts/fixer.md` and `orchestrator.md`).
- **Guardrail added**: the fix itself is the guardrail (the check is now scoped correctly at the source) — no separate mechanical enforcement beyond that, since this was a prompt-instruction bug, not something semgrep-checkable.
- **Category**: infra (harness/prompt bug, not application code)
- **Error class**: `harness-check-unscoped-repo-wide-grep`
- **Status**: monitoring — same reasoning as the csproj entry above: this was a structural fix to the check's scope, not a text reminder, so recurrence of THIS exact check failing is unlikely; however the underlying pattern (a hardcode/lint-style check accidentally scoped to the whole repo instead of the current diff) could recur if a similar check is added elsewhere in the harness without the same scoping discipline — worth re-checking any future grep-based check against this class

### 2026-08-22 — fixer re-diagnosed an unchanging blocker 14+ times across compactions
- **Symptom**: `fixer` correctly diagnosed on its FIRST check that `bump-version.sh` was blocked by an unrelated, out-of-scope uncommitted file (a harness prompt file being edited concurrently) — then re-ran the identical `git status` diagnostic at least 14 more times across several compaction cycles, never actually stopping to report the (unchanging) finding as final.
- **Root cause**: neither `fixer.md` nor `orchestrator.md` had an explicit "once you determine you are BLOCKED for a reason outside your control, report ONCE and STOP" rule — the model correctly re-verified state after each compaction (per the earlier compaction-safety rule) but had no instruction telling it that a repeatedly-identical diagnostic result should terminate the loop rather than trigger another identical check.
- **Fix**: added an explicit "MANDATORY: once BLOCKED, report ONCE and STOP — never re-diagnose an unchanging blocker" section to `fixer.md`, plus matching guidance in `orchestrator.md`'s error-handling section for how the orchestrator itself should react to this specific blocker (check `git status` itself, don't re-delegate to fixer to "check again").
- **Guardrail added**: prompt-text rule (not mechanical) — see Status note.
- **Category**: infra (harness/prompt bug)
- **Error class**: `harness-blocked-state-not-terminated`
- **Status**: monitoring — this is a prompt-text guardrail, the weaker kind (an agent could in principle still not weight/follow it under context pressure) — this Error class is a good candidate to watch closely for recurrence, and if it recurs, escalating to a mechanical circuit-breaker (e.g. the plugin auto-detecting N identical consecutive tool calls and injecting a hard stop) would be the appropriate escalation

### 2026-08-22 — nordicbees-quality-monitor.ts: hook argument shape assumed wrong parameter
- **Symptom**: `duration_sec` was `null` for every single recorded task-stats entry for hours after the plugin was first written — the "started" lifecycle event was silently never being written at all.
- **Root cause**: the OpenCode plugin API puts a tool call's `args` on the SECOND parameter for the `tool.execute.before` hook, but on the FIRST parameter for `tool.execute.after` — the plugin's `before` handler read `input.args` (first parameter), which is `undefined` for that hook, causing an early return before any record was ever written. Confirmed against official OpenCode plugin documentation examples after the fact, not guessed.
- **Fix**: added a `getArgs(input, second)` helper that checks both parameter positions, making the plugin resilient regardless of which hook is calling it.
- **Guardrail added**: none beyond the fix itself (this is infrastructure code, not a pattern checkable by semgrep/agent-guardrails against application code).
- **Category**: infra (harness plugin bug)
- **Error class**: `opencode-plugin-hook-arg-shape-mismatch`
- **Status**: monitoring — if a similar hook-shape assumption bug appears in a DIFFERENT plugin file later (e.g. a future plugin also gets the before/after parameter shape backwards), that would be a recurrence of this exact mechanism and should reuse this tag

### 2026-08-23 — unhandled service exception in InvoiceView.razor event handlers killed the Blazor circuit
- **Symptom**: clicking "Patvirtinti" on an invoice with lines but a 0.00 total crashed the entire Blazor Server circuit (page became unresponsive). `HandleConfirm()` called `InvoiceService.UpdateInvoiceStatusAsync` unguarded; the service threw `InvalidOperationException` ("Invoice has lines but total is zero...") which was caught nowhere, terminating the circuit.
- **Root cause**: same mechanism as the 2026-08-18 `layout-lifecycle-unguarded-db-call` entry — a Service method that can throw is called from a Blazor component without try/catch, and the unhandled exception kills the SignalR circuit. 2026-08-18 was a layout lifecycle call site (MainLayout.OnInitializedAsync); this recurrence is at user-action event handlers (HandleConfirm et al.) on InvoiceView.razor. Additionally the zero-total invoice itself came from a save-time validation gap compounded by `OpenRawMaterialDialog` (InvoiceEdit.razor:448-463) never setting `PriceExclVat` for ULAK lines, letting price-0 lines persist.
- **Fix**: (a) injected `ISnackbar` into InvoiceView.razor and wrapped `HandleConfirm` in try/catch showing `Snackbar.Add(ex.Message, Severity.Error)` with no rethrow; (b) audited and wrapped 7 more service-calling handlers (LoadInvoiceAsync, LoadCreditNotesAsync, LoadPaymentHistoryAsync, OpenEditPayment, DeletePayment, RegisterPayment, HandleCopy) in the same pattern; (c) localized the service guard message to Lithuanian; (d) added save-time zero-price-line + zero-total-with-lines validation to InvoiceEdit.razor, InvoiceCreate.razor and CreditNoteEdit.razor so the bad invoice never reaches the DB (commits 3099abd, 79215de, 3893208, 3e95f06, ab9340e, cc29f68; v0.11.237–239).
- **Verified**: dotnet build 0 errors each round; reviewer APPROVED each diff (handlers wrapped, validation precedes Update/Create calls, no DB-write convention change); read-only DB audit found 11 zero-total invoices and 14 zero-price lines (LAK26082 among them) — not mutated.
- **Guardrail added**: none beyond the code fix (per-and-page try/catch is the shallowest form; the durable guard is the save-time validation). Candidate mechanical check: a semgrep rule that flags `await <Service>.XxxAsync(...)` statements inside Blazor `@code` event handlers that are not enclosed in try/catch whose catch shows a Snackbar — see Status.
- **Category**: UI-form (Blazor circuit / error handling)
- **Error class**: `layout-lifecycle-unguarded-db-call`
- **Status**: escalated — this is a RECURRENCE of the 2026-08-18 mechanism (unguarded throwing service call inside a Blazor component terminating the circuit; the earlier entry's Status was N/A with only a suggested convention, never added as a rule). A second copy of a prompt/skill sentence is not sufficient; propose adding a mechanical check (semgrep rule for ungapped `await` service calls in .razor event handlers) so this cannot be skipped by an agent not reading a prompt sentence.

### 2026-08-24 — Credit note create fails: Column 'applied_invoice_id' cannot be null
- **Symptom**: Creating a credit note via the UI threw `MySqlException: Column 'applied_invoice_id' cannot be null` on `INSERT INTO credit_notes` (both the Save and Save+PDF buttons failed; `SaveChangesAsync` bubbled a `DbUpdateException`). Same error class would have hit any credit-note EDIT, since the edit page never sends AppliedInvoiceId.
- **Root cause**: RECURRENCE of `schema-drift-unverified-column-mapping` (first logged 2026-07-17, Delivery.cs): the model declares `CreditNote.AppliedInvoiceId` as `int?` (nullable), but the DB column `credit_notes.applied_invoice_id` is `int NOT NULL` (Migrations/20260602150000_InitialCreate.cs:663, with FK to invoices). The 2026-08-24 "Taikoma sąskaita" UI-cleanup commit 3e8a765 removed the create page's auto-fill of AppliedInvoiceId (which had previously ALWAYS satisfied the NOT NULL column by sending the original invoice id) without any vetting of the real DB column nullability — so `CreateCreditNoteAsync` line 143 wrote `request.AppliedInvoiceId` (now null) and the DB correctly rejected the NULL. The identical latent path existed in `UpdateCreditNoteAsync` line 507, which wrote `request.AppliedInvoiceId` (never set by the edit page) into the raw SQL UPDATE.
- **Fix**: commit 70d26a8 (v0.11.250) — service-layer defaults, NO schema change: `CreateCreditNoteAsync` now uses `AppliedInvoiceId = request.AppliedInvoiceId ?? request.OriginalInvoiceId` (restores the exact pre-cleanup behavior: applied = original invoice when unspecified); `UpdateCreditNoteAsync` now uses `creditNote.AppliedInvoiceId = request.AppliedInvoiceId ?? creditNote.AppliedInvoiceId` (preserves the stored value instead of nulling it). Centralized so UI create, save+PDF, and the HandleCopy flow are all covered.
- **Verified**: `dotnet build` 0 errors; new xUnit test `CreateCreditNoteAsync_NullAppliedInvoiceId_PersistsOriginalInvoiceId` drives the REAL service path against `nordic_bees_erp_test` and round-trip-reads `applied_invoice_id == original_invoice_id` via a brand-new DbContext — `Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1`. Reviewer verified fallback/preserve logic + real-write-path test and APPROVED.
- **Guardrail added**: none new here (the existing `dotnet-efcore-nordicbees` Rule 2 "never assume a column exists/or doesn't based on the model alone" was the intended soft mitigation and did NOT prevent this — see Status).
- **Category**: EF-core
- **Error class**: `schema-drift-unverified-column-mapping`
- **Status**: escalated — second incident in this error class (first: 2026-07-17 Delivery.cs, whose own Status explicitly noted Rule 2 was only a soft, effectively-unguarded mitigation). The 2026-08-24 UI cleanup removed the only code implicitly satisfying the NOT NULL constraint with no nullability audit, proving the existing generic skill sentence is insufficient. Reacting with another prompt/skill sentence is NOT sufficient; propose a MECHANICAL check: (a) a semgrep / `agent-guardrails` rule flagging any EF model property declared nullable whose migration or live DB column is NOT NULL (or at minimum: any property written into an INSERT/UPDATE without a guaranteed-non-null `??` fallback while the DB schema says NOT NULL), or (b) a `DESCRIBE <table>`-based nullability audit step run before any credit-note create/edit/service change. Either must be non-skippable by an agent.

### 2026-08-24 — English credit-note PDF showed the Lithuanian title and a wrong "Credit invoice:" label
- **Symptom**: A credit note generated in English (customer DefaultLanguage = EN) rendered the document title as "KREDITINĖ SĄSKAITA FAKTŪRA" instead of "CREDIT NOTE", and the referenced-invoice line read "Credit invoice: …" instead of the correct "Credited invoice:".
- **Root cause**: In `GenerateCreditNotePdf` (Services/PdfGeneratorService.cs) the title was a hardcoded Lithuanian literal rendered UNCONDITIONALLY (line 761), bypassing the `GetLocalizationLabels` EN/LT branching that every other string on the document uses; and the EN branch of the invoice-info line used the non-standard label "Credit invoice:". The two header dates used ISO `yyyy-MM-dd` already, but without an explicit culture (so the "international format" guarantee was implicit, not enforced).
- **Fix**: commit pending (v0.11.251) — added a `CreditNoteTitle` field to the `LocalizationLabels` record (EN `"CREDIT NOTE"`, LT `"KREDITINĖ SĄSKAITA FAKTŪRA"` preserved verbatim), rendered `labels.CreditNoteTitle` at the title site (mirroring how the invoice renders `labels.DocumentTitle`), renamed the EN label to `"Credited invoice:"`, and made both dates explicit `ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)` (ISO 8601 international date format, same style as the invoice at lines 287/366). Lithuanian output byte-identical.
- **Verified**: `dotnet build` 0 errors; reviewer traced the diff (scope, verbatim LT string, EN grammar/labels, ISO dates, both construction sites) and APPROVED.
- **Guardrail added**: none new. The established pattern is already that all PDF strings must come from `GetLocalizationLabels` — the title simply bypassed it. Candidate mechanical check: a semgrep rule flagging QuestPDF `.Text("<hardcoded string>")` calls that are not `labels.*`, or an agent-check that any visible PDF string in a language-branchable document routes through the labels record.
- **Category**: other (PDF localization)
- **Error class**: `pdf-locale-string-hardcoded-outside-labels`
- **Status**: monitoring — no dedicated mechanical guardrail yet; the existing labels-record pattern is the sponsor and was verified working everywhere else, so recurrence risk is low but not mechanically prevented.

### 2026-08-24 — Credit-note system rendered "-0" / "-0.00" for zero amounts
- **Symptom**: Credit-note pages and PDFs displayed `-0`/`-0.00` whenever a line amount or total was exactly zero (e.g. zero-total invoices, zero-price lines, zero credit quantity), instead of `0` / `0.00`.
- **Root cause**: The credit-note UI/PDF deliberately NEGATES monetary magnitudes for display (`FormatAmount(-(value))` in CreditNoteView/CreditNoteEdit, `PriceExclVat = -l.PriceExclVat` in CreditNoteCreate's client model, and `$"-{value:N2}"` string interpolation in GenerateCreditNotePdf). In C# `-0m` is a distinct negative-zero decimal that formats with a leading minus ("-0", "-0,00", "-0.00"). The negations had no zero guard, so exactly-zero magnitudes rendered with a spurious minus sign across the whole credit-note system (11 UI/PDF sites + a model-construction site).
- **Fix**: commits a5ae7c4, e5cf356, d241b9d (v0.11.250→v0.11.253) — DRY zero-guards: `FormatAmountHelper.FormatNegatedAmount(decimal)` (storage-positive → displayed-negative, zero renders "0") and `FormatAmountHelper.FormatSignedAmount(decimal)` (already-signed passthrough, zero renders "0") used at all 10 UI display sites; Create's client model normalizes `l.X == 0m ? 0m : -l.X` at construction; `PdfGeneratorService.FormatNegativeAmount(decimal)` renders "0.00" for zero and `-{N2 Invariant}` for non-zero at all 6 PDF sites. Non-zero output byte-identical everywhere.
- **Verified**: `dotnet build` 0 errors after every step; reviewer APPROVED each per-file diff (argument identity preserved, no double negation, scope exact), including the final combined Create+PDF pass.
- **Guardrail added**: none mechanical. The DRY helper extraction means any future credit amount display should route through `FormatNegatedAmount`/`FormatSignedAmount`/`PdfGeneratorService.FormatNegativeAmount`. Candidate mechanical check: a semgrep rule flagging string interpolations `$"-{...N2}"` or `FormatAmount(-(` patterns in the credit-note pages, or an architectural rule that credit-note display negation must go through the guarded helpers.
- **Category**: UI-form (display formatting / negative-zero)
- **Error class**: `negative-zero-display-format`
- **Status**: monitoring — the fix is a mechanical zero-guard at every site, so recurrence requires someone to add a NEW unguarded negation site (possible but unlikely); the helper-extraction pattern is the soft sponsor.

### 2026-08-25 — Invoice picker on order page returned zero results (RECURRENCE)
- **Symptom**: Invoice assignment autocomplete on `/orders/{id}` (`SearchInvoicesAsync`) silently showed no results; exceptions were swallowed by the caller's catch-all.
- **Root cause**: Same mechanism as the 2026-07-17 entry — `Contains(searchTerm, StringComparison.OrdinalIgnoreCase)` inside a LINQ query (`InvoiceService.SearchInvoicesAsync`, lines 572-574), untranslatable by the MariaDB provider. The code shipped AFTER the semgrep rule `nordicbees-stringcomparison-in-linq` existed, meaning the rule was never run against this file (or misses this call-shape variant).
- **Fix**: commit `a509d03` (v0.11.263) — replaced both Contains calls with `EF.Functions.Like(col, pattern)`, removed banned `.Include(i => i.Customer)`, added `.AsNoTracking()`.
- **Guardrail added**: none new. Existing mechanical guardrail FAILED to prevent this recurrence.
- **Category**: EF-core
- **Error class**: `ef-linq-untranslatable-stringcomparison`
- **Status**: escalated — SECOND incident in this class (2026-07-17 invoice search, this one 2026-08-25 order-page invoice picker). Post-fix semgrep verification (2026-08-25) showed the `nordicbees-stringcomparison-in-linq` rule DOES still match this pattern family — it flagged two sibling sites in InvoiceService.cs immediately when run via CLI (`semgrep scan --config .agent-guardrails/nordicbees-rules.yaml`). Conclusion refined: the rule's pattern coverage is fine; what failed was ENFORCEMENT WIRING — nothing ran it against changed files before commit. Escalation proposal stands: wire the existing config into a pre-commit hook or CI step that runs `semgrep scan --config .agent-guardrails/nordicbees-rules.yaml` on every changed `.cs` file. Note also: the rule currently flags in-memory LINQ-to-Objects filtering as false positives (it cannot distinguish EF queryables from materialized lists) — consider adding a guard/annotation so real findings aren't drowned by noise.
