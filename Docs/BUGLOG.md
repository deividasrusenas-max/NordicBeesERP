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

### 2026-09-06 — Harness infra files (fixer.md, circuit-breaker.ts, this BUGLOG) silently reverted because they were left uncommitted
- **Symptom**: All same-day fixes applied earlier (three layered circuit-breaker guardrails in `.opencode/plugin/nordicbees-circuit-breaker.ts`, the `task_complete`-is-unconditional rewrite in `fixer.md`, and the BUGLOG entries documenting both) disappeared entirely between one test run and the next — files were back to their PRE-fix content, with no error or warning anywhere. Discovered only because a `coder` loop that should have been caught by the restored diversity-aware `same-tool-streak` check went completely uncaught, prompting a direct re-read of the plugin file that revealed it was the old version.
- **Root cause**: these files were edited directly (via filesystem access, outside the normal orchestrator/coder/fixer per-file workflow) and were NEVER committed to git — every fixer task's own "Ignore other uncommitted files" instruction explicitly named `.opencode/plugin/nordicbees-circuit-breaker.ts` and `Docs/BUGLOG.md` as files to leave alone, confirming they sat as uncommitted working-tree changes throughout. `orchestrator`'s own edit permissions (`opencode.json`) don't cover `.opencode/plugin/*` or `.opencode/prompts/*` at all — these paths are structurally outside what the automated harness ever commits on its own. Some git operation with tree-cleaning effect (a `git checkout`/`git stash`/`git reset`, run manually or as part of preparing a clean test) discarded them, since uncommitted changes have no persistence guarantee across any such operation.
- **Fix**: re-applied all three circuit-breaker fixes and the fixer/coder/reviewer.md `task_complete` rewrites in this same session. No code-level guardrail exists yet to prevent this from happening a third time — the only real fix is committing harness infrastructure files immediately after editing them, same as any other source file, rather than leaving them as long-lived uncommitted changes that every fixer task is instructed to studiously ignore.
- **Category**: infra (process gap, not a code or prompt bug)
- **Error class**: `uncommitted-harness-file-silently-reverted` (new tag)
- **Status**: N/A — no mechanical guardrail exists; this is a process discipline gap (commit harness edits promptly) rather than something semgrep/agent-guardrails can check. A periodic `git status` audit specifically for `.opencode/plugin/*` and `.opencode/prompts/*` sitting uncommitted for more than a session would be the cheapest mitigation if this recurs.

### 2026-09-06 — ROOT CAUSE FOUND: opencode-auto-resume's retry counter resets on every busy transition, letting auto-"continue" nag indefinitely when task_complete is never called
- **Symptom**: A fixer session that finished its real work correctly, wrote a prose "task complete" report WITHOUT calling `task_complete`, then got auto-continued and re-generated that same prose report roughly 30 times in a row, with zero tool calls between repetitions (see the sibling entries immediately below for the specific loop shapes this produced).
- **Root cause**: `opencode-auto-resume` (`.opencode/node_modules/opencode-auto-resume/dist/index.js`, read directly rather than guessed) registers a real `task_complete` tool that sets `w.completionSignaled = true` when called — this is the ONLY thing that stops the plugin's automatic "continue" nudging for a session. `fixer.md` only told the model to call this tool conditionally ("if a task_complete tool exists in your tool list"), so the model never called it. Because `completionSignaled` stayed false, once the session went idle for `chunkTimeoutMs + gracePeriodMs` (185s in this project's config), the plugin auto-sent `"continue"` — a plain injected message, not a tool call. **Critically**, the plugin's `session.status === "busy"` handler calls `resetSessionFlags(w)` on EVERY transition back to busy, resetting the resume-attempt counter to zero — so the configured `maxRetries: 3` never actually caps the total number of auto-continues over a session's lifetime, only consecutive ones without an intervening busy period. The model dutifully responds to every "continue" (going busy, then idle again), creating an effectively unbounded nag-respond cycle. The plugin's own "hallucination loop" safety net (3+ continues within a 10-minute window) detects this pattern but responds by aborting and sending a FRESH `"continue"` rather than truly giving up — so even that safety net perpetuates the cycle.
- **Fix**: `.opencode/prompts/fixer.md`, `coder.md`, `reviewer.md` — changed the `task_complete` instruction from conditional to unconditional and factual (confirmed by reading the plugin source that it always exists), with the mechanism explained so the model understands WHY the call matters mechanically.
- **Category**: infra (harness plugin behavior, not a project code bug)
- **Error class**: `post-completion-continue-loop` (same family, now with the actual upstream mechanism identified rather than just its downstream symptom)
- **Status**: escalated — this supersedes the two circuit-breaker-side entries below as the PRIMARY fix; those two (`post-terminal-continuation`, `text-only-repeat-loop`) remain valuable as downstream backstops for cases where a `task_complete` call is still somehow missed. Do not flip to `stable` until a real fixer session confirms `task_complete` is actually called and no auto-continue nagging occurs afterward. **2026-09-06 addendum**: this fix (along with the two circuit-breaker entries below) was ITSELF silently reverted once already because it sat as an uncommitted file — see the `uncommitted-harness-file-silently-reverted` entry above. Re-applied same day.

### 2026-09-06 — Fixer skipped mandatory step 10/GUARDRAIL_SCORE report after a "skip bump-version" instruction, then text-repeat-looped ~30 rounds with zero tool calls
- **Symptom**: A Phase 3 multi-round task told fixer "Do NOT run bump-version.sh this round." Fixer correctly ran build→add→commit→verified via `git show --stat HEAD` (1 file, correct diff), then never ran `fixer.md` step 10 (`agent-guardrails check`) or emitted the mandated `GUARDRAIL_SCORE=` report line. It then regenerated a byte-for-byte identical "Objective/Work State/Next Move: Report task as complete" block roughly 30 times in a row with ZERO tool calls between repetitions — pure text regeneration, never actually terminating.
- **Root cause**: the task instruction to skip step 9 (`bump-version.sh`) was over-generalized by the model into also skipping step 10 (`agent-guardrails check`) and the mandated `GUARDRAIL_SCORE=` report line, which are unconditional per `fixer.md`. Because the report never contained `GUARDRAIL_SCORE=`, `APPROVED`, or `REJECTED`, it produced no terminal marker — meaning NEITHER the `post-terminal-continuation` guardrail NOR the original same-tool-streak/identical-args-streak checks could catch this: all three fire on TOOL-CALL events, and this loop had no tool calls at all between its identical text-only turns.
- **Fix**: (a) `.opencode/prompts/fixer.md` — added an explicit note to the DONE state definition: skipping step 9 per a task's own multi-round instruction never implies skipping step 10; step 10 and the `GUARDRAIL_SCORE=` line are unconditional. (b) `.opencode/plugin/nordicbees-circuit-breaker.ts` — added a NEW, independent detector (`text-only-repeat-loop`) that compares each completed text part's normalized content against the previous one for the SAME session; 3 consecutive near-identical text completions with no intervening tool call now abort immediately, in the text handler itself, without waiting for (or requiring) any following tool call or terminal marker. Any real tool call resets this counter.
- **Category**: infra (harness/prompt bug + plugin gap)
- **Error class**: `plan-without-execution-gap` (reusing the existing 2026-08-24 tag — same underlying mechanism: the model correctly finishes/articulates its work but never crosses into the corresponding terminal tool call/marker)
- **Status**: escalated — second distinct root cause manifesting inside the `plan-without-execution-gap`/`post-completion-continue-loop` family in one day. Do not flip either new guardrail to `stable` until both have survived a real clean exposure window.

### 2026-09-06 — Circuit-breaker's same-tool-streak check false-positive-aborted nearly every normal fixer session (bash-only agent)
- **Symptom**: After the `post-terminal-continuation` guardrail was added, a fixer session under test was cancelled almost immediately. `.opencode/reports/circuit-breaker.jsonl` showed 5 of 7 recent aborts had `reason: "same-tool-streak"` with histories consisting of completely normal, distinct fixer steps (`dotnet build`, `git status`, `git add`, `git commit`, `git log`, `agent-guardrails check`) — not a loop at all.
- **Root cause**: `nordicbees-circuit-breaker.ts`'s `same-tool-streak` check compared only the tool NAME across the last 8 calls, never the arguments. `fixer.md` restricts the fixer role to a SINGLE tool (`bash`), so every one of fixer's normal 7-10 sequential steps reports `tool: "bash"` regardless of the actual command — a structural false-positive, not a tuning issue. `coder`/`reviewer` rarely hit this because they use multiple distinct tools, so an 8-call same-tool run is naturally rarer and more often genuinely suspicious for them.
- **Fix**: `.opencode/plugin/nordicbees-circuit-breaker.ts` — `same-tool-streak` now additionally requires the number of DISTINCT commands within the 8-call window to be ≤ 3 (`SAME_TOOL_STREAK_MAX_DISTINCT_ARGS`), computed from the bash `command` string with the `workdir` field stripped out (workdir presence/absence on an otherwise-identical command was itself creating spurious diversity that could mask a genuine stuck-checking loop). A healthy multi-step bash workflow has high command diversity even though every call shares tool name "bash"; a genuine loop repeats the same few commands. This same diversity check also correctly catches a same-day `coder` alternating-read loop (tool name "read" constant, only 2 distinct file paths alternating — well under the threshold).
- **Category**: infra (harness plugin bug)
- **Error class**: `circuit-breaker-single-tool-agent-false-positive` (new tag)
- **Status**: monitoring — first occurrence, fix is structural (diversity-aware, not another threshold tweak), but has not yet been observed under a real clean exposure window with the new logic.

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
- **Status**: escalated → monitoring (2026-09-06, see note below) — user re-ran the same task after the `fixer.md`
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
  **Mechanical guardrail added 2026-09-06**: `.opencode/plugin/nordicbees-circuit-breaker.ts`
  now auto-aborts a subagent session showing this exact repeated-call
  signature (same-tool-streak of 8, or identical-args-streak of 3) — see
  `.opencode/planning/circuit-breaker-and-semgrep-plan.md`. NOT flipping
  this Status to `stable` — wait for a real clean exposure window under
  the new enforcement first, same caution already applied to the
  `ef-linq-untranslatable-stringcomparison` entries above.

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
- **Status**: monitoring (unchanged word, now backed by a real mechanical
  guardrail as of 2026-09-06, see note below) — but see the note above: this Error class and
  `post-completion-continue-loop` are both instances of the same deeper
  gap (no Tier-1 mechanical stop-loop circuit-breaker exists yet), and
  should be re-evaluated together, not independently, once the
  `n_toolcalls`-based circuit-breaker (see `HARNESS_STATUS.md` §13
  Etapas 0) is built.
  **Mechanical guardrail added 2026-09-06**: `.opencode/plugin/nordicbees-circuit-breaker.ts`
  now auto-aborts a subagent session showing this exact repeated-call
  signature (same-tool-streak of 8, or identical-args-streak of 3) — see
  `.opencode/planning/circuit-breaker-and-semgrep-plan.md`. NOT flipping
  this Status to `stable` — wait for a real clean exposure window under
  the new enforcement first, same caution already applied to the
  `ef-linq-untranslatable-stringcomparison` entries above.

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
- **Status**: N/A → monitoring (2026-09-06) — no guardrail existed at the
  time of this entry; this was the highest-priority of the three 2026-08-24
  loop entries for the planned Tier-1 `n_toolcalls`/cycle-detection
  circuit-breaker, since it is structurally unreachable by any prompt-text
  fix aimed at the agent's own behavior.
  **Mechanical guardrail added 2026-09-06**: `.opencode/plugin/nordicbees-circuit-breaker.ts`
  now auto-aborts a subagent session showing this exact repeated-call
  signature (same-tool-streak of 8, or identical-args-streak of 3) — see
  `.opencode/planning/circuit-breaker-and-semgrep-plan.md`. NOT flipping
  this Status to `stable` — wait for a real clean exposure window under
  the new enforcement first, same caution already applied to the
  `ef-linq-untranslatable-stringcomparison` entries above.

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
- **Status**: N/A → monitoring (2026-09-06) — no guardrail existed at the
  time of this entry; this was the primary evidence base for BOTH planned
  fixes (Tier-1 `n_toolcalls` circuit-breaker AND the `fixer.md` structural
  rewrite — the latter separately already done, see `fixer.md`'s
  WORKING/BLOCKED/OUT_OF_SCOPE/DONE state machine).
  **Mechanical guardrail added 2026-09-06**: `.opencode/plugin/nordicbees-circuit-breaker.ts`
  now auto-aborts a subagent session showing this exact repeated-call
  signature (same-tool-streak of 8, or identical-args-streak of 3) — see
  `.opencode/planning/circuit-breaker-and-semgrep-plan.md`. NOT flipping
  this Status to `stable` — wait for a real clean exposure window under
  the new enforcement first, same caution already applied to the
  `ef-linq-untranslatable-stringcomparison` entries above.

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
- **Status**: escalated — SECOND incident in this class (2026-07-17 invoice search, recurrence 2026-08-25 order-page invoice picker). The semgrep rule exists and catches the pattern, but enforcement wiring (pre-commit/CI) was effectively unenforced at the time of recurrence. **Corrected 2026-09-06**: this entry previously said wiring was "missing until 2026-09-04," implying it worked from that date — it did not. The 2026-09-04 wiring (`.semgrep.yml` + the pre-commit hook) had two independent bugs (a path-resolution bug in the hook meaning it silently no-op'd on every commit, and a `.semgrep.yml` include-syntax bug that crashed semgrep outright) that meant it never actually ran successfully until both were found and fixed on 2026-09-06 (commit `dd7a919`). Real, functioning enforcement has existed for less than a day as of this correction — do not flip this entry to `stable` until a genuine clean exposure window has passed under the now-actually-working wiring.

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
- **Status**: escalated — second incident in this class (first 2026-07-17 order_lines, recurrence 2026-08-26 Artwork CreateAssetAsync). The prompt/skill-text guardrail (connection-scope guidance) demonstrably did not hold; propose mechanical check (semgrep/analyzer for LAST_INSERT_ID() outside held-open connection) wired into pre-commit/CI.

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
- **Guardrail added**: Docs/FROZEN.md §9 (commit 9cae797) — documentation-only guardrail: `forceLoad:true` reserved ONLY for genuine file/PDF download endpoints; code review must flag any `forceLoad:true` outside download endpoints.
- **Category**: infra
- **Error class**: `blazor-forceload-fullreload-auth-redirect`
- **Status**: monitoring

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
- **Guardrail added**: `.opencode/skills/mudblazor/SKILL.md` known-pitfalls bullet (commit aaad158) — MudAutocomplete inline forms must use `SelectValueOnTab="true"` + `@bind-Text` + `OnBlur` exact-match fallback (`TryResolveSelectedCustomerFromText()` pattern from commit 1f4aaaf).
- **Category**: UI-form
- **Error class**: `mudblazor-autocomplete-tab-value-commit`
- **Status**: monitoring

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
- **Status**: monitoring (unchanged word, now backed by a real mechanical guardrail as of 2026-09-06, see note below) — this is a prompt-text guardrail, the weaker kind (an agent could in principle still not weight/follow it under context pressure) — this Error class is a good candidate to watch closely for recurrence, and if it recurs, escalating to a mechanical circuit-breaker (e.g. the plugin auto-detecting N identical consecutive tool calls and injecting a hard stop) would be the appropriate escalation.
  **Mechanical guardrail added 2026-09-06**: `.opencode/plugin/nordicbees-circuit-breaker.ts`
  now auto-aborts a subagent session showing this exact repeated-call
  signature (same-tool-streak of 8, or identical-args-streak of 3) — see
  `.opencode/planning/circuit-breaker-and-semgrep-plan.md`. NOT flipping
  this Status to `stable` — wait for a real clean exposure window under
  the new enforcement first, same caution already applied to the
  `ef-linq-untranslatable-stringcomparison` entries above.

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
- **Guardrail added**: `.agent-guardrails/nordicbees-rules.yaml` rule `nordicbees-pdf-locale-string-hardcoded-outside-labels` (WARNING, commit e017b73) — semgrep flags QuestPDF `.Text("literal")` calls whose string contains a 3+ char alphabetic run (Lithuanian/English visible words), excluding currency codes, ISO date formats, and numeric formatting tokens.
- **Category**: other (PDF localization)
- **Error class**: `pdf-locale-string-hardcoded-outside-labels`
- **Status**: monitoring

### 2026-08-24 — Credit-note system rendered "-0" / "-0.00" for zero amounts
- **Symptom**: Credit-note pages and PDFs displayed `-0`/`-0.00` whenever a line amount or total was exactly zero (e.g. zero-total invoices, zero-price lines, zero credit quantity), instead of `0` / `0.00`.
- **Root cause**: The credit-note UI/PDF deliberately NEGATES monetary magnitudes for display (`FormatAmount(-(value))` in CreditNoteView/CreditNoteEdit, `PriceExclVat = -l.PriceExclVat` in CreditNoteCreate's client model, and `$"-{value:N2}"` string interpolation in GenerateCreditNotePdf). In C# `-0m` is a distinct negative-zero decimal that formats with a leading minus ("-0", "-0,00", "-0.00"). The negations had no zero guard, so exactly-zero magnitudes rendered with a spurious minus sign across the whole credit-note system (11 UI/PDF sites + a model-construction site).
- **Fix**: commits a5ae7c4, e5cf356, d241b9d (v0.11.250→v0.11.253) — DRY zero-guards: `FormatAmountHelper.FormatNegatedAmount(decimal)` (storage-positive → displayed-negative, zero renders "0") and `FormatAmountHelper.FormatSignedAmount(decimal)` (already-signed passthrough, zero renders "0") used at all 10 UI display sites; Create's client model normalizes `l.X == 0m ? 0m : -l.X` at construction; `PdfGeneratorService.FormatNegativeAmount(decimal)` renders "0.00" for zero and `-{N2 Invariant}` for non-zero at all 6 PDF sites. Non-zero output byte-identical everywhere.
- **Verified**: `dotnet build` 0 errors after every step; reviewer APPROVED each per-file diff (argument identity preserved, no double negation, scope exact), including the final combined Create+PDF pass.
- **Guardrail added**: none mechanical. The DRY helper extraction means any future credit amount display should route through `FormatNegatedAmount`/`FormatSignedAmount`/`PdfGeneratorService.FormatNegativeAmount`. Candidate mechanical check: a semgrep rule flagging string interpolations `$"-{...N2}"` or `FormatAmount(-(` patterns in the credit-note pages, or an architectural rule that credit-note display negation must go through the guarded helpers.
- **Category**: UI-form (display formatting / negative-zero)
- **Error class**: `negative-zero-display-format`
- **Status**: monitoring — the fix is a mechanical zero-guard at every site, so recurrence requires someone to add a NEW unguarded negation site (possible but unlikely); the helper-extraction pattern is the soft sponsor.

### 2026-08-25 — Orders partial-shipment confirm button stuck at (0) despite checked pallet checkbox
- **Symptom**: On `/orders/{id}` a pallet's checkbox in the "Siuntimas (dalimis)" section rendered visibly checked, but "Patvirtinti siuntą" showed `(0)` and stayed disabled — shipment could not be confirmed.
- **Root cause**: The `MudCheckBox` was bound as a GENERIC-VALUE checkbox: `T="int"` + `Value="@p.BatchId"` + `Checked="@_selectedBatchIds.Contains(p.BatchId)"` + `CheckedChanged="@((int v) => ToggleBatchSelection(p.BatchId))"`. `MudCheckBox` extends `MudBooleanInput<T>` whose checked-input is a **bool**, and `CheckedChanged` is `EventCallback<bool>`. With `T="int"` the checked state is derived through a value converter rather than a real bool, and the `(int v)` lambda doesn't deliver the new bool state — so `_selectedBatchIds` (backing HashSet) never updated. The checkbox's visual state and the backing selection diverged: visual showed checked, `_selectedBatchIds.Count` stayed 0, `CanConfirmShipment` stayed false.
- **Fix**: commit `2fa979c` (v0.11.276) — replaced with the app's established working pattern (PaymentRegisterDialog.razor:102): `T="bool"`, `Checked="_selectedBatchIds.Contains(p.BatchId)"`, `CheckedChanged="@((bool val) => ToggleBatchSelection(p.BatchId, val))"`, and `ToggleBatchSelection(int batchId, bool isChecked)` now explicitly Add-s when `isChecked` else Remove. `_selectedBatchIds` now updates on every toggle, so the `(N)` count, enabled state, and the ids passed to `CreateShipmentAsync` are all correct.
- **Guardrail added**: `.opencode/skills/mudblazor/SKILL.md` known-pitfalls bullet (commit aaad158) — checkbox row-selection must use `T="bool"` + `Checked` bool-lookup + `CheckedChanged="val => Handler(id, val)"`, never a generic-`T` + `Value` combo for row selection (pattern from PaymentRegisterDialog.razor:102 / commit 2fa979c). Skills-loaded prompt-text guardrail, now committed and verified present.
- **Category**: UI-form
- **Error class**: `mudblazor-checkbox-generic-value-binding`
- **Status**: monitoring — note a prior, DIFFERENT MudBlazor bind-state-sync bug exists (`mudblazor-bind-value-commit-timing`, 2026-07-17 MudDatePicker dialog `@bind-Value` timing); this entry is a distinct mechanism (checkbox generic-value binding) but same broad family. If a third MudBlazor bind/state-sync variant appears, an umbrella escalation is warranted.

### 2026-08-25 — Invoice picker on order page returned zero results (RECURRENCE)
- **Symptom**: Invoice assignment autocomplete on `/orders/{id}` (`SearchInvoicesAsync`) silently showed no results; exceptions were swallowed by the caller's catch-all.
- **Root cause**: Same mechanism as the 2026-07-17 entry — `Contains(searchTerm, StringComparison.OrdinalIgnoreCase)` inside a LINQ query (`InvoiceService.SearchInvoicesAsync`, lines 572-574), untranslatable by the MariaDB provider. The code shipped AFTER the semgrep rule `nordicbees-stringcomparison-in-linq` existed, meaning the rule was never run against this file (or misses this call-shape variant).
- **Fix**: commit `a509d03` (v0.11.263) — replaced both Contains calls with `EF.Functions.Like(col, pattern)`, removed banned `.Include(i => i.Customer)`, added `.AsNoTracking()`.
- **Guardrail added**: none new. Existing mechanical guardrail FAILED to prevent this recurrence.
- **Category**: EF-core
- **Error class**: `ef-linq-untranslatable-stringcomparison`
- **Status**: escalated — SECOND incident in this class (2026-07-17 invoice search, this one 2026-08-25 order-page invoice picker). Post-fix semgrep verification (2026-08-25) showed the `nordicbees-stringcomparison-in-linq` rule DOES still match this pattern family — it flagged two sibling sites in InvoiceService.cs immediately when run via CLI (`semgrep scan --config .agent-guardrails/nordicbees-rules.yaml`). Conclusion refined: the rule's pattern coverage is fine; what failed was ENFORCEMENT WIRING — nothing ran it against changed files before commit. Escalation proposal stands: wire the existing config into a pre-commit hook or CI step that runs `semgrep scan --config .agent-guardrails/nordicbees-rules.yaml` on every changed `.cs` file. Note also: the rule currently flags in-memory LINQ-to-Objects filtering as false positives (it cannot distinguish EF queryables from materialized lists) — consider adding a guard/annotation so real findings aren't drowned by noise. See the 2026-07-17 sibling entry above for a 2026-09-06 correction to the wiring timeline — it was not actually functional as early as originally recorded.

### 2026-08-25 — Order detail page renders every packed line "Nepakuota" and hides shipment section (InvalidCast on IsShipped)
- **Symptom**: On `/orders/{id}` a line that was packed (order_line_batches has a row, order status ready_for_pickup) still showed "● Nepakuota", "Partijos (kiekis)" showed "–", and the "Siuntimas (dalimis)" section did not render — making partial shipment impossible from the UI. The `catch { _pallets = new(); }` in Detail.razor `LoadOrderAsync` silently swallowed the underlying exception.
- **Root cause**: `GetOrderPalletsAsync` (rewritten for quantity-based partial shipment) computed the `IsShipped` SELECT column as `(GREATEST(b.quantity - COALESCE(shipped_sum,0),0) <= 0)` — a comparison/boolean expression. MySqlConnector returns a comparison expression column as a 64-bit integer (Int64), but the reader code called `reader.GetInt32(reader.GetOrdinal("IsShipped"))`, throwing `InvalidCastException` on the very first row while mapping batch id 1. The reader cast had previously been satisfied because the old SQL used `CASE WHEN osp.id IS NULL THEN 0 ELSE 1 END` (an int literal, Int32).
- **Fix**: commit `c5599ed` + bump `943cecf` (v0.11.279) — changed the single `IsShipped` SELECT expression to `CASE WHEN GREATEST(b.quantity - COALESCE(osp_agg.shipped_sum, 0), 0) <= 0 THEN 1 ELSE 0 END AS IsShipped`, forcing a 0/1 integer literal (Int32) that matches the `GetInt32` reader call.
- **Verified**: dotnet build 0 errors; reviewer APPROVED; post-fix the order detail page loads the packed batch and the "Siuntimas (dalimis)" section appears (Playwright-verified through the full 3-round 2+2+1 shipment scenario).
- **Guardrail added**: `.opencode/skills/dotnet-efcore-nordicbees/SKILL.md` Rule 6 (commit 34cf364) — documents that bare boolean/comparison expressions in raw-SQL SELECT lists return Int64 (not Int32/bool) from MySQL/MariaDB, that `reader.GetInt32()` on such a column throws `InvalidCastException`, and mandates wrapping in `CASE WHEN ... THEN 1 ELSE 0 END`. Reviewer-diligence + E2E-test guardrail (no mechanical semgrep rule — too hard to detect statically without running the query). The Playwright E2E round-trip remains the objective backstop.
- **Category**: EF-core
- **Error class**: `rawsql-reader-type-cast-mismatch`
- **Status**: monitoring — new mechanism tag. Note the sibling family (`ef-dbnull-explicit-cast`) also concerns reader cast mismatches but is specifically DBNull-related; this is the computed-expression-vs-GetInt* variant.

### 2026-08-25 — Customer autocomplete on /orders/create doesn't match diacritic names
- **Symptom**: typing plain Latin letters in the customer autocomplete on `/orders/create` returned no suggestions for customers whose names contain Lithuanian (ą č ę ė į š ų ū ž) or German (ä ö ü ß) characters — e.g. typing "u" didn't match "ü", "a" didn't match "ą".
- **Root cause**: `SearchCustomers` used `c.Name.ToLower().Contains(search)` — case-insensitive but NOT diacritic-insensitive, so base-Latin input could never match composed/diacritic names. No diacritic-folding helper existed anywhere in the codebase (`CompanyNameHelper.Normalize` only folds company-type abbreviations, not diacritics).
- **Fix**: commits 5e9b7da + 1bec821 (v0.11.280-281) — new shared `Helpers/DiacriticHelper.Fold()` (NormalizationForm.FormD → strip combining marks → FormC re-compose, explicit `ß→ss`, lowercased); `SearchCustomers` now folds both the query and the customer name. UI polish task also renamed the header delivery-date label "Pristatymo data" → "Išsiuntimo data" in the same round.
- **Guardrail added**: none mechanical. First occurrence of this mechanism. The shared helper is now a single reuse point, but nothing mechanical prevents a future inline `.ToLower().Contains(...)` search (in a NEW page, e.g. product search still uses it) from being written diacritic-insensitively again. Candidate: extend the existing `nordicbees-stringcomparison-in-linq` semgrep rule family with a pattern targeting plain `Contains` on name-like fields, or a skill note mandating `DiacriticHelper.Fold` for any user-facing text search.
- **Category**: encoding
- **Error class**: `search-not-diacritic-insensitive`
- **Status**: monitoring — new mechanism tag, no prior guardrail.

### 2026-08-25 — "Atgal į užsakymus" button on /orders/22 wraps to two lines
- **Symptom**: the header back button on the order detail page wrapped its label onto two lines with an awkward gap between lines.
- **Root cause**: MudButton's label defaults to `white-space: normal`; the flex header row (`justify-space-between`) squeezed the button to ~141px of content width while the label "← Atgal į užsakymus" needs ~139px of text plus flex layout — sitting exactly at the wrap threshold, so the label broke mid-text and the button inflated to 36.5px tall (Playwright-verified: scrollHeight 362 ≫ clientHeight 35, computed `white-space: normal`). `text-transform:none` was already present; `white-space: nowrap` was the missing piece, not text transformation.
- **Fix**: commit c270429 (v0.11.285) — added `white-space:nowrap;` to the back button's `Style`. Same commit also updated the per-line packed chip label "✓ Pakuota" → "✓ Supakuota" (status-display consistency; the order-status map labels "packing"→"Supakuota" and "ready_for_pickup"→"Paruošta" landed in the same task round).
- **Guardrail added**: none mechanical. Candidate soft note for the `mudblazor` skill: MudButton labels inside squeezed flex containers (e.g. header rows) need explicit `white-space:nowrap` — plain "set text-transform:none" is insufficient when the trigger is flex shrinking.
- **Category**: UI-form
- **Error class**: `mudblazor-button-label-wrap`
- **Status**: monitoring — new mechanism tag, no prior guardrail. Distinct from the other MudBlazor bind/state-sync tags (`mudblazor-bind-value-commit-timing`, `mudblazor-checkbox-generic-value-binding`); this is a pure CSS/layout failure mode.

### 2026-08-26 — Debt reconciliation PDF ("skolų suderinimo aktas") throws unhandled license exception
- **Symptom**: Generating the debt reconciliation PDF ("skolų suderinimo aktas") on `/reports/debt-reconciliation` threw an unhandled `System.Exception` from `QuestPDF.Drawing.DocumentGenerator.ValidateLicense()`; the PDF never downloaded.
- **Root cause**: `DebtReconciliationPdfService.GeneratePdfAsync` called `document.GeneratePdf()` without first setting `QuestPDF.Settings.License = LicenseType.Community`. Every other PDF service in the repo (`DeliveryReceiptPdfService.cs:31`, `PdfGeneratorService.cs:63/113/615/895`) sets the Community license per-method before generating; the new service omitted it.
- **Fix**: Added `QuestPDF.Settings.License = LicenseType.Community;` inside `GeneratePdfAsync`, immediately before `document.GeneratePdf()`.
- **Guardrail added**: Global `QuestPDF.Settings.License = LicenseType.Community;` initialization in `Program.cs` (line 134, commit 4fcdeff) — covers all current and future PDF services automatically, eliminating the per-method pattern that was inconsistently followed.
- **Category**: PDF generation / third-party license validation
- **Error class**: `questpdf-license-not-set`
- **Status**: monitoring

### 2026-08-26 — Fixer subagent falsely claimed it added a FK constraint on artwork_files (artwork label-types task)
- **Symptom**: The fixer subagent's text completion summary for the artwork label-types Settings task claimed it had added a "LabelTypeId foreign key on artwork_files". No such FK exists in the actual deliverable.
- **Root cause**: The agent's natural-language self-report described work that was never performed. Deividas did not trust the summary and independently verified the actual commit (`f813139`) and the draft SQL (`Migrations/Scripts/20260826_artwork_labeltypes.sql`): zero `FOREIGN KEY`/`ALTER TABLE` matches, `ArtworkFile.cs` retains only the `General = "Bendra"` sentinel constant, no FK columns anywhere. The committed code matched the original prompt's explicit "no FK" constraint exactly — only the prose summary was wrong.
- **Fix**: N/A — no code fix needed, the actual artifact was correct. Caught by mandatory mechanical verification (reading the real file + grepping the SQL) before the claim was trusted or acted on.
- **Guardrail added**: none new — this is exactly the scenario the existing "agent self-reported done must never be trusted" rule already covers (mandatory read of report/git/DB/source over trusting prose). Reinforces that the rule must extend to ANY prose claim about what was implemented, not just completion status or commit hashes.
- **Category**: agent-reliability
- **Error class**: `fixer-fabricated-implementation-claim`
- **Status**: monitoring — if a second fabrication variant appears (a false claim about what was implemented, distinct from this one), consider an umbrella escalation for "agent prose is not evidence" as a named guardrail category. (2026-09-06: this entry previously cross-referenced a sibling tag `fixer-fabricated-commit-hash` as "same failure family" — removed after an audit found that tag has no backing entry anywhere in this file or in `.opencode/reports/`; it was referenced but never actually documented. If that incident is ever reconstructed, log it as its own entry rather than restoring the dangling reference.)

### 2026-08-26 — Artwork multi-file/label-types migration SQL failed with ERROR 1067 (Invalid default value for 'created_at')
- **Symptom**: Running `Migrations/Scripts/20260826_artwork_multifile.sql` against the dev DB failed immediately with `ERROR 1067 (42000) at line 12: Invalid default value for 'created_at'`, before any table was created. The sibling script `20260826_artwork_labeltypes.sql` had the identical latent bug (caught before Deividas ran it).
- **Root cause**: Both migration scripts declared `created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP` — a fractional-seconds precision mismatch. In MariaDB/MySQL, when a `DATETIME`/`TIMESTAMP` column specifies a fractional precision (the `(6)`), the `CURRENT_TIMESTAMP` default function must be called with the SAME precision (`CURRENT_TIMESTAMP(6)`); a bare `CURRENT_TIMESTAMP` (implicit precision 0) does not match and MariaDB rejects it as an invalid default. Both scripts were agent-authored in separate tasks and both carried the same latent defect — this is a repeatable authoring mistake, not a one-off typo.
- **Fix**: Deividas caught the failure on the first script before any table existed (transaction never committed a partial state); Claude corrected both `.sql` files in place, changing `DEFAULT CURRENT_TIMESTAMP` → `DEFAULT CURRENT_TIMESTAMP(6)` on the `created_at` column in each. No DB was left in a broken state since the CREATE TABLE never succeeded.
- **Guardrail added**: none mechanical yet. Candidate: extend `.agent-guardrails/nordicbees-rules.yaml` (semgrep) with a rule flagging any SQL `DATETIME(n)` or `TIMESTAMP(n)` column (n > 0) whose `DEFAULT CURRENT_TIMESTAMP` clause omits the matching `(n)` precision — this is a purely syntactic, zero-false-positive check since the two must always match in MariaDB.
- **Category**: schema/migration
- **Error class**: `mariadb-datetime-precision-default-mismatch`
- **Status**: N/A — the Guardrail line states "none mechanical yet" meaning no guardrail was actually added; per BUGLOG.md format definition, N/A applies when no guardrail exists (nothing to monitor effectiveness of), and this error class is effectively UNGUARDED — a periodic review should treat this as a highest-priority candidate for adding an actual mechanical guardrail (semgrep rule for DATETIME(n) DEFAULT CURRENT_TIMESTAMP without matching precision).

### 2026-08-26 — Artwork new-asset creation returned id 0 (LAST_INSERT_ID on pooled connection) — RECURRENCE
- **Symptom**: Creating a new Artwork asset (AssetCreateDialog → ArtworkBrandPage.OpenCreateAsset) navigated to `/artwork/asset/0`. Because the detail page and the multi-file upload page key off the route id (`/artwork/asset/{id}`, `/artwork/upload/{id}`), the asset could not be opened or uploaded — the whole create→upload flow dead-ended at a non-existent asset 0. The backend `GetAssetDetailAsync(0)` correctly rejected id 0 (returns null), so the page showed an error rather than data — which masked the real cause: the id was never 0 in the DB, the RETURNED id was simply wrong.
- **Root cause**: RECURRENCE of `ef-connection-pool-race-last-insert-id` (first logged 2026-07-17 for the order_lines FK insert; that entry's Status was "monitoring" and explicitly noted the guardrail was prompt/skill-text only and "worth watching for recurrence more closely"). `ArtworkService.CreateAssetAsync` ran the INSERT via `ExecuteSqlRawAsync` and then read `SELECT LAST_INSERT_ID() as Value` via a SEPARATE `SqlQueryRaw<int>` call. `LAST_INSERT_ID()` is connection-scoped in MySQL/MariaDB; EF Core's connection pooling served the second statement on a different physical connection than the INSERT, so it returned 0. The create→navigate chain (AssetCreateDialog.SaveAsync closes `MudDialog.Close(DialogResult.Ok(assetId))` → ArtworkBrandPage reads `int assetId` → `NavigateTo($"/artwork/asset/{assetId}")`) faithfully propagated the 0. Confirmed via Playwright repro: 3/3 runs navigated to `/artwork/asset/0`; a control run on an existing asset (`/artwork/asset/2`) navigated correctly to `/artwork/upload/2`, isolating the fault to the creation step.
- **Fix**: commit `ca25a2a` — wrapped both the INSERT and the `LAST_INSERT_ID()` read in `await _context.Database.OpenConnectionAsync()` … `try { … } finally { await _context.Database.CloseConnectionAsync(); }` so they execute on the same MySQL session. SQL strings unchanged. `GetAssetDetailAsync` / `UploadVersionAsync` were NOT touched (their id=0 rejection is correct and out of scope); NOTE `UploadVersionAsync` still contains the same `LAST_INSERT_ID()` pattern at two sites and is a latent recurrence risk if ever exercised through a pooled-connection path.
- **Verified**: `dotnet build` 0 errors (automated build hook, exit 0); reviewer APPROVED (connection held open across both statements, SQL unchanged, only `CreateAssetAsync` touched, parameterized INSERT intact, finally safely closes). The Playwright POST-FIX re-run was NOT executed this session (user halted the browser-loop verification) — the fix is verified by build + review + the pre-fix repro that pinned the fault to creation; a post-fix browser re-run is recommended before closing.
- **Guardrail added**: none mechanical. This is the SECOND occurrence of `ef-connection-pool-race-last-insert-id` and the prompt/skill-text guardrail (`dotnet-efcore-nordicbees` connection-scope guidance) demonstrably did NOT prevent it. Escalate to a mechanical check: a semgrep / `agent-guardrails` rule (or `roslyn` analyzer) that flags any `LAST_INSERT_ID()` / `SCOPE_IDENTITY()` / `@@IDENTITY` read that is NOT enclosed in an explicitly held-open connection (`OpenConnectionAsync`/`CloseConnectionAsync`, or a `DbConnection` held open across BOTH the write and the id read). This is the same escalation pattern already recommended for the sibling `ef-linq-untranslatable-stringcomparison` entry — wire the existing semgrep config into pre-commit/CI so it cannot be skipped by an agent.
- **Category**: EF-core
- **Error class**: `ef-connection-pool-race-last-insert-id`
- **Status**: escalated — second incident in this class (first 2026-07-17 order_lines). The existing guardrail was prompt/skill-text only and did not hold; propose a mechanical check (semgrep/analyzer) for `LAST_INSERT_ID()` reads outside a held-open connection, wired into pre-commit/CI like the `nordicbees-stringcomparison-in-linq` escalation.

### 2026-08-27 — Unpaid invoices report test fixture failed: Column 'applied_invoice_id' cannot be null (RECURRENCE)
- **Symptom**: The new integration test for `UnpaidInvoicesService` (built as part of the "Neapmokėtų sąskaitų suvestinė" report task) failed on first run with `Column 'applied_invoice_id' cannot be null` while inserting a test `CreditNote` row via `context.Add()` + `SaveChangesAsync()`.
- **Root cause**: RECURRENCE of `schema-drift-unverified-column-mapping` — `CreditNote.AppliedInvoiceId` is still declared `int?` (nullable) in the model, but `credit_notes.applied_invoice_id` is `NOT NULL` in the real DB (same mismatch already logged twice on 2026-08-24 for the create and edit service paths). This time the mismatch surfaced in brand-new TEST code rather than a production call site: the test constructed a `CreditNote` directly without setting `AppliedInvoiceId`, because nothing in the model signals that the column is actually required.
- **Fix**: test updated to explicitly set `AppliedInvoiceId = invB.Id` on the inserted credit note. The underlying model/DB nullability mismatch itself was NOT fixed (out of scope for the reporting task) — the report service only reads `OriginalInvoiceId`, so the report feature itself is unaffected.
- **Guardrail added**: none new — this is the fourth appearance of the same mechanism with no mechanical guardrail yet in place.
- **Category**: EF-core
- **Error class**: `schema-drift-unverified-column-mapping`
- **Status**: escalated — fourth occurrence in this class (Delivery.cs 2026-07-17; `applied_invoice_id` create/edit path 2026-08-24; `original_invoice_id` 2026-08-24; this test-fixture occurrence 2026-08-27). Notably this recurrence hit NEW test code, not a repeat of the same production call site — proof the mismatch is silently waiting to bite ANY future code (test or production) that constructs a `CreditNote` without independently knowing this specific column is NOT NULL despite the model claiming nullable. The previously proposed mechanical check (semgrep/`agent-guardrails` rule, or a `DESCRIBE`-based nullability audit flagging any EF model property declared nullable whose live DB column is NOT NULL) still has not been implemented after three prior incidents — recommend prioritizing it now rather than filing a fifth near-duplicate note. Also worth a dedicated follow-up to reconcile `CreditNote.AppliedInvoiceId`'s nullability in the model against the actual column (making it non-nullable in C# would let the compiler catch this class of bug immediately).

### 2026-08-27 — DefaultVatRate decimal precision not annotated (EF infers decimal(65,30), DB column is decimal(5,2))
- **Symptom**: Four model classes declared `decimal DefaultVatRate` with no precision annotation. The EF model snapshot inferred `decimal(65,30)` for these properties, while the live DB columns `business_partners.default_vat_rate` and `company_settings.default_vat_rate` are `decimal(5,2)`. A future `dotnet ef migrations add` would emit `ALTER COLUMN ... decimal(65,30)`, an unintended, unreviewed, potentially destructive column-type change; also a latent silent-truncation risk if any value ever exceeded 2 decimals / 5 total digits.
- **Root cause**: Same risk class as the already-confirmed CompanySettings precision bug — a `decimal` property with no `[Precision]` or `[Column(TypeName)]` annotation. Under the Pomelo MySQL provider EF Core 8 infers `decimal(65,30)` (its provider default) when no precision is specified, diverging from the real `decimal(5,2)` column. Confirmed via read-only investigation (`.opencode/reports/todo-audit-investigation-20260827-1911.md`, Part 1).
- **Fix**: commit `a6e7d9c` (v0.14.9) — added `[Precision(5, 2)]` directly above the `DefaultVatRate` property in all four model files (and `using Microsoft.EntityFrameworkCore;` to each): `Models/CompanySettings.cs`, `Models/Models_Part1.cs`, `Models/Models_Part2.cs` (the `Supplier` class — its missing `[Table]`/non-existent `Suppliers` table mapping was left untouched as a separate known issue), and `Models/InvoiceModels.cs`. No other property, default value, or attribute changed; no migration generated or applied.
- **Verified**: `dotnet build` 0 errors (automated build hook, exit 0) on each edit and the final commit; reviewer APPROVED all four diffs verbatim (quoted the four property blocks); `git show a6e7d9c` confirms exactly the 4 files, 8 insertions. The live DB column was NOT altered (no DDL run).
- **Guardrail added**: `.agent-guardrails/nordicbees-rules.yaml` rule `nordicbees-ef-decimal-precision-annotation-missing` (ERROR, commit e017b73) — semgrep flags `decimal`/`decimal?` properties in `[Table]`-attributed EF entity classes that lack `[Precision(p,s)]` or `[Column(TypeName="decimal(p,s)")]` annotation.
- **Category**: EF-core
- **Error class**: `ef-decimal-precision-annotation-missing`
- **Status**: monitoring

### 2026-08-28 — Coder stuck calling mudblazor_get_api_reference repeatedly, self-diagnosed loop but never stopped
- **Symptom**: During the dashboard-rebuild task (MudChart integration), `coder` called `mudblazor_get_api_reference` five times in a row with near-identical arguments (`MudChartBase\`1.Series\`1`, then `+ChartSeries\`1`, `+Series\`1`, `.ChartSeries\`1`, `.Series\`1` — grasping at reflection-style type-name variants for the same API). Before EACH repeated call, the model's own text explicitly said: "I've been stuck in a loop calling the same MudBlazor API tool repeatedly with no progress — stopping that immediately. Let me check what state I'm actually in before doing anything else." — then immediately issued the next near-identical tool call anyway.
- **Root cause**: NOT YET INVESTIGATED IN DEPTH — logged for pattern-tracking per the recurrence-check rule. Distinct from the existing `plan-without-execution-gap` family: those entries show a model correctly ARTICULATING a next step (a plan, a completion claim) but failing to CROSS into the corresponding tool call. Here the model articulates the OPPOSITE of what it does — it states an intention to stop, then performs the exact same class of action again in the same turn/next turn. The self-diagnosis text is accurate (it correctly recognizes the loop) but has zero causal effect on the following action, suggesting the "stopping" sentence is generated independently of whatever process selects the next tool call, rather than the two being coupled.
- **Fix**: NOT YET APPLIED.
- **Guardrail added**: none yet.
- **Category**: infra
- **Error class**: `self-diagnosed-loop-no-behavioral-stop` (new tag — distinct from `plan-without-execution-gap`: that family fails to execute a correctly stated PLAN; this one fails to honor a correctly stated STOP, and is doubly dangerous since the self-report reads as if the problem is already handled)
- **Status**: N/A → monitoring (2026-09-06) — no guardrail existed at the time of this entry. First occurrence, and first documented loop incident on `coder` specifically (all four 2026-08-24 loop entries were `fixer`/orchestrator). Same underlying gap as the other loop entries: no Tier-1 mechanical circuit-breaker (`n_toolcalls`-based repeated-call detection) exists to catch this regardless of what the model's own text says. Reinforces that the planned Tier-1 circuit-breaker should trigger on repeated near-identical tool calls directly, not on any text-based self-report, since this incident shows the self-report cannot be trusted as a stopping signal even when accurate.
  **Mechanical guardrail added 2026-09-06**: `.opencode/plugin/nordicbees-circuit-breaker.ts`
  now auto-aborts a subagent session showing this exact repeated-call
  signature (same-tool-streak of 8, or identical-args-streak of 3) — see
  `.opencode/planning/circuit-breaker-and-semgrep-plan.md`. NOT flipping
  this Status to `stable` — wait for a real clean exposure window under
  the new enforcement first, same caution already applied to the
  `ef-linq-untranslatable-stringcomparison` entries above.

### 2026-08-28 — Home dashboard KPI cards show 0 because CurrentValue read only from today's snapshot
- **Symptom**: Home dashboard KPI cards (Statinės sandėlyje, Kibirai sandėlyje, Neįkainotos, Skolos tiekėjams) displayed 0 kg / 0 / 0 € even though live warehouse/supplier data existed — the values were blank/zero on any day before the 03:00 daily snapshot worker had run.
- **Root cause**: `PaymentService.GetDashboardTrendAsync` set `trend.CurrentValue = todaySnap != null ? selector(todaySnap) : 0m`, so it depended on a `dashboard_daily_snapshots` row for "today" that does not exist until the once-daily 03:00 worker populates it. Home.razor already computed the correct live values (`_barrelNetWeight`, `_bucketNetWeight`, `_unpricedDeliveries`, `_totalDebt`) but they were not bound into the trend.
- **Fix**: Changed `IPaymentService.GetDashboardTrendAsync` to accept the four live current values as parameters (`decimal currentBarrelsKg, decimal currentBucketsKg, int currentUnpricedDeliveries, decimal currentSupplierDebtTotal`). `trend.CurrentValue` is now the passed-in live value (never read from today's snapshot). Snapshots (last 14 days) are used ONLY for the 7-day delta (Value7DaysAgo/DeltaAbsolute/DeltaPercent) and the sparkline Series, with a synthetic final Series point appended using today's live value + today's date so the sparkline always ends at the true current value. If snapshots.Count < 2, delta is null and Series is the single live point. Home.razor's OnInitializedAsync now passes the already-computed live values. The fake `IPaymentService` in CreditNoteServiceTests.cs was updated to the new signature.
- **Guardrail added**: none — this is a logic fix; the live value is now the source of truth and snapshots are only historical context. Could add a comment in DashboardService documenting "live = current, snapshot = history" to prevent regression.
- **Category**: UI-form (data-binding)
- **Error class**: `kpi-currentvalue-from-missing-snapshot`
- **Status**: monitoring

### 2026-08-29 — .NET runtime interpreter bug: array.Contains(enumValue) with ReadOnlySpan<TEnum> inside EF parameter extraction
- **Symptom**: New `InvoiceService.GetMonthlySalesVolumeAsync()` query threw an exception during EF Core's LINQ parameter extraction. The query used a pattern like `statuses.Contains(invoice.Status)` (an array/list of `InvoiceStatus` enum values checked with `.Contains()`) inside the `Where()` predicate.
- **Root cause**: A .NET runtime interpreter bug when the JIT/interpreter evaluates `array.Contains(enumValue)` against a `ReadOnlySpan<InvoiceStatus>` internally created for the `.Contains()` call on an enum-typed collection — this is a genuine CLR/interpreter issue, not an EF Core or application logic bug. Confirmed via a temporary xUnit test run directly against the dev DB (removed after diagnosis) that isolated the failure to the `.Contains()` call itself, independent of the surrounding query.
- **Fix**: Replaced `statuses.Contains(invoice.Status)` with explicit `!=` comparisons chained (e.g. `invoice.Status != InvoiceStatus.Draft && invoice.Status != InvoiceStatus.Cancelled`) instead of an inclusion/exclusion list with `.Contains()`.
- **Guardrail added**: Docs/FROZEN.md §10 (commit 9cae797) — documentation-only guardrail: `enumArray.Contains(x.EnumProperty)` / `enumList.Contains(x.EnumProperty)` is PROHIBITED inside EF Core LINQ predicates; replace with explicit `!=` chains.
- **Category**: EF-core / runtime
- **Error class**: `enum-array-contains-readonlyspan-interpreter-bug`
- **Status**: monitoring
