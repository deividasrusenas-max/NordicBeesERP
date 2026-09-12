# Phase 0 handoff — opencode-auto-resume rewrite, evidence instrumentation

Everything in this document is from this session. No design or specification
for a replacement plugin was written — that is explicitly out of scope for
Phase 0, and nothing below should be read as a proposal.

## What was built, and where

| File | Purpose |
|---|---|
| `.opencode/planning/plugin-api-surface.md` | Task 1: exact hook/event/type surface of the installed `@opencode-ai/plugin` + `@opencode-ai/sdk`, cross-checked against the two working reference plugins. |
| `.opencode/planning/auto-resume-1.1.15-inventory.md` | Tasks 2+3: full read-through of the vendor snapshot's own logic (config keys, injection sites with verbatim prompts, timers, global state, `task_complete`, the patches, dead code) plus the duplicate-tool-registration finding. |
| `.opencode/plugin-dev/nordicbees-harness-trace.ts` | The recorder's full source — every pure function (hashing, injection matching, rotation, event→record mapping) exported for testing. This is the file to edit going forward. |
| `.opencode/plugin-dev/nordicbees-harness-trace.test.ts` | 63 tests, `bun test` (run via `npx -y bun test`, since `bun` is not on this machine's PATH — see "Practical notes" below), all passing. |
| `.opencode/plugin-dev/analyze-trace.ts` | The analysis script: `bun .opencode/plugin-dev/analyze-trace.ts [path]`. |
| `.opencode/plugin/nordicbees-harness-trace.ts` | The **live** copy actually loaded by opencode. Deliberately exports **only** the plugin itself — see "What surprised me" below for why. |
| `opencode.json` | Added `"debug": true` to `opencode-auto-resume`'s options object. Nothing else in that object changed. |
| `.opencode/reports/.gitignore` | New: `harness-trace*.jsonl` — traces are data, never committed. (This is currently redundant with the root `.gitignore`'s existing blanket `.opencode/reports/` rule, kept anyway per this task's own instruction and Hard Rule 4's specific carve-out for this path.) |
| `.opencode/reports/harness-trace.jsonl` | Not committed (gitignored). Currently contains the 67-line trace from this session's own smoke test — real seed data, left in place rather than deleted. |

Commits: `e374a58` (Tasks 1-3 planning docs) and `68fe400` (Tasks 4-7:
plugin-dev, the live plugin copy, the config change, the new `.gitignore`).

## Task 3 answer: duplicate tool registration

**Last-registered wins, silently — no error, no warning, no drop, and the
earlier registration is not deleted, just shadowed.** Confirmed by `strings`
extraction from the installed `opencode` binary (`1.18.30`, Mach-O arm64),
same technique nordicbees-circuit-breaker.ts's own header documents using
for the same binary. Found the literal `ToolRegistry` service
(`"@opencode/v2/ToolRegistry"` appears verbatim in the binary) whose
`register` effect appends `{token, registration}` entries to a `name ->
array` map, and whose `materialize()` — which builds **both** the tool list
shown to the model and the dispatch table used for actual calls — always
takes `arr.at(-1)`, the most recently registered entry, for each name. Full
quoted (paraphrased-variable-name) code is in
`auto-resume-1.1.15-inventory.md` section H.

**Practical implication, not yet verified live**: a future replacement
plugin that also registers `task_complete` would silently win or lose
depending on plugin load order, which I did not determine (does it strictly
follow `opencode.json`'s `plugin` array order? unknown). A small, cheap,
**not yet run** experiment is proposed in that section if you want to settle
this before deciding whether a migration can be gradual (dual-register both
plugins' `task_complete` briefly) or must be atomic (disable
`opencode-auto-resume` and enable the replacement in the same commit).

## Smoke test result (verbatim)

Four attempts were needed, all logged truthfully here rather than only
reporting the last success:

1. **First attempt failed to load.** `opencode run --print-logs --log-level
   DEBUG` logged: `error="Plugin export is not a function"` for
   `.opencode/plugin/nordicbees-harness-trace.ts`. Root cause: that copy had
   the same many-named-exports-plus-default-export shape as the
   `plugin-dev` source (needed for its test suite). Both working reference
   plugins (`nordicbees-reminder.ts`, `nordicbees-circuit-breaker.ts`)
   export exactly one `Plugin`-typed binding and nothing else. Per this
   task's own instructions, I deleted the broken file immediately, confirmed
   (from the same run's log) that the rest of the harness — including
   `opencode-auto-resume` itself — continued loading and running normally
   right after that single error, then applied a fix.
2. **Fix, attempt 1 of 2 allowed:** regenerated the `.opencode/plugin/` copy
   with only `NordicBeesHarnessTrace` exported (no helpers, no default
   export). Re-ran the smoke test — the "Plugin export is not a function"
   error was gone, confirming the fix. This run and the next one, however,
   both **terminated prematurely** (no `EXIT_CODE=` line, process gone from
   `ps`, no `timeout 280`/125 exit code either) partway through a second
   orchestrator turn, before reaching an actual subagent delegation — an
   environment/background-task quirk in this sandboxed session, not a
   plugin bug (see "Open questions" below).
3. **Third attempt**, same shape, also cut short mid-turn.
4. **Fourth attempt succeeded end to end**, using a more direct prompt
   ("do not run bash or write todos yourself, immediately delegate...") and
   the Bash tool's own `run_in_background`/`timeout` parameters instead of a
   manual `&`. Full result:
   - `opencode run --print-logs --log-level DEBUG --agent orchestrator` exited
     0.
   - No `"failed to load plugin"` line anywhere in the log.
   - `.opencode/reports/harness-trace.jsonl` was created and grew to 67
     lines.
   - It contains a `session_created` line for the subagent with a non-empty
     `parentID`:
     `{"ts":"2026-09-12T18:42:45.256Z","mono":58312,"type":"session_created","sessionID":"ses_f6912267affe6RntVNCn1ES8i9","parentID":"ses_f691309fcffe7CRH5TWIQ7nafA","isSubagentAtCreation":true}`
   - It contains a full, real `task_complete` call from the reviewer
     subagent (`tool_part` pending→running→completed plus matching
     `tool_execute_before`/`tool_execute_after`, `durationMs:3`).
   - Zero `recorder_error` lines across the whole trace.
   - Zero `heartbeat` lines — the run (a few minutes) was shorter than the
     5-minute heartbeat interval, which the task's own wording anticipates
     ("if the run lasts long enough").
   - The reviewer's actual verbatim answer, relayed by the orchestrator:
     > APPROVED-READONLY — the file never calls `session.abort` or
     > `session.prompt` (the only occurrence of that string, line 162, is
     > inside a comment describing the vendor auto-resume plugin's
     > behavior) and its sole side effect is appending trace lines to
     > `.opencode/reports/harness-trace.jsonl`, with every hook body wrapped
     > in try/catch so it can only observe/log session events.
   - `bun .opencode/plugin-dev/analyze-trace.ts .opencode/reports/harness-trace.jsonl`
     (run via `npx -y bun`) ran cleanly against this real trace, no crash,
     and its output was internally consistent with the raw trace (e.g.
     "Delegations: total=1, with task_complete=1, WITHOUT=0" matched the
     real `task_complete` call found by direct `grep`).

This satisfies every condition Task 7 asked for. The two allowed
fix-and-retry cycles were both used (attempts 2 and 3 above, both against
the export-shape fix, which the eventual success confirms was the right
fix) before attempt 4 succeeded by working around the separate
background-task-termination issue rather than the plugin itself.

## Open questions and claims I could not verify

1. **Does opencode's runtime actually emit both `session.status
   {type:"idle"}` and the separate `session.idle` event for the same
   transition, or only one?** This is the single most important open
   question from Task 2 (inventory.md section F). The idle-injection
   patch's own header says it gates "every session.idle transition," but
   only patched the `session.status` handler's idle branch — a second,
   structurally separate `case "session.idle":` handler in the same vendor
   file reaches the exact same nudge-scheduling code completely ungated.
   Static reading cannot resolve whether this is a live hole or dead code.
   **This is exactly what the recorder's `session_status` vs
   `session_idle_event` record types (recorded as genuinely distinct) and
   the `autoresume_injection_suspected` counter with its
   "landed on a top-level session" flag are built to answer** — run
   `analyze-trace.ts` against a real multi-day trace and check whether that
   count is ever nonzero, and whether `session_idle_event` records ever
   appear for a top-level session without an immediately-preceding
   `session_status{status:"idle"}` for the same session.
2. **Plugin/tool registration order.** Confirmed last-registered-wins
   (Task 3), but not confirmed what determines the order plugins register
   in — whether it's strictly `opencode.json`'s `plugin` array order. Small
   experiment proposed in the inventory report, not run.
3. **Version mismatch**: `@opencode-ai/plugin`'s own `package.json` reports
   `1.17.18`; the installed `opencode` binary reports `1.18.30` (same
   number the user's task description names). I used the `.d.ts` files as
   the best available static contract but did not reconcile this gap — the
   live wire protocol could have moved slightly past what the types
   describe.
4. **The vendor snapshot's `"interrupted"` handling** (`statusType ===
   "interrupted"` and `case "session.interrupted":`) has no corresponding
   member in the currently-installed SDK's `Event` union or `SessionStatus`
   type at all. I couldn't determine whether this is genuinely dead code
   (the installed opencode version no longer emits it) or whether the wire
   protocol still sends something these branches would catch despite the
   published types omitting it.
5. **Background-task premature termination** (see smoke test attempts 2-3
   above): two `opencode run` background processes disappeared mid-turn
   with no exit code recorded, at inconsistent durations (~2m14s and
   ~4m00s), neither matching the `timeout 280` I'd set. I don't know
   whether this is specific to this Claude Code session's sandboxing, a
   general property of long-running background bash tasks here, or
   something else. It resolved when I stopped manually backgrounding with
   `&` and used the Bash tool's own `run_in_background`/explicit `timeout`
   parameters instead — worth knowing if you script further multi-turn
   `opencode run` invocations from within a similar harness.
6. **`bun` is not installed globally on this machine** (`which bun` finds
   nothing). All `bun test`/`bun run` commands in this session were run via
   `npx -y bun ...`, which works (downloads bun 1.4.2 as an npm package) but
   is slower to start each time. You may want `bun` on PATH directly before
   running the analysis command repeatedly.

## What surprised me / contradicts the task's own description

- **The idle-injection patch does not appear to fully close the hole it was
  written for** (open question 1 above) — this was the most consequential
  finding. It doesn't mean the patch is broken, only that a second, ungated
  code path exists that static reading can't rule out.
- **`session.error`'s `MessageAbortedError` handler marks every single
  tracked session in the plugin's entire global Map as `userCancelled`,**
  not just the session the abort actually belongs to (inventory.md section
  D). This is a second, independent instance of the same "global Map
  iteration used as a proxy for one specific session" pattern that also
  produces the `isSubagent`-flip contamination risk the idle-injection
  patch's own header names — but this second instance is not something
  either existing patch touches at all.
- **`task_complete`'s own open-todos gate reads the *mutable* `w.isSubagent`
  flag, not the *immutable* `w.isSubagentAtCreation`** the idle-injection
  patch introduced specifically to avoid this class of bug — so
  `task_complete` remains exposed to the exact contamination the patch was
  written to prevent, just in a different code path than the one the patch
  fixed.
- **The task description said "three patches"; only two remain as files**
  in `.opencode/patches/` today. Reconciled: a third
  (`fix-auto-resume-subagent-task-complete.js`) was applied 2026-09-06 then
  deliberately dropped 2026-09-12 (commit `c5e2dcd`) once the 1.1.3→1.1.15
  version bump moved the live plugin out from under it entirely (opencode's
  loader resolves the versioned `plugin` spec via the global package cache,
  not the stale local `.opencode/node_modules` copy that patch targeted).
  Not a discrepancy in the end, just worth knowing the history doesn't
  match today's file count at a glance.
- **A plugin file with any export other than a single `Plugin`-typed
  binding fails to load, silently skipping (not crashing) the rest of the
  harness** — confirmed the hard way (see smoke test attempt 1). Worth
  knowing before writing the eventual replacement's own file, which will
  presumably want the same testability-via-exports structure this
  recorder's `plugin-dev` copy has, and will need the same two-copy
  discipline (a fully-exported dev copy plus a stripped-down single-export
  deployed copy) unless a build step is introduced to strip exports
  automatically.

## Command to run in a few days

```
npx -y bun .opencode/plugin-dev/analyze-trace.ts .opencode/reports/harness-trace.jsonl
```

(or `bun .opencode/plugin-dev/analyze-trace.ts .opencode/reports/harness-trace.jsonl`
directly, if `bun` gets installed on PATH by then). No arguments needed
beyond the path — it prints per-day and total distributions for every metric
Task 6 specified. Give it real multi-day usage before trusting the
`chunkTimeoutMs`/`subagentWaitMs`-relevant percentiles; the only data in the
file right now is this session's own few-minute smoke test.
