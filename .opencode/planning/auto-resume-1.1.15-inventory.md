# opencode-auto-resume@1.1.15 — source inventory

Task 2 + Task 3 of the Phase 0 evidence-instrumentation task. Read in full:
`.opencode/vendor/auto-resume-1.1.15-patched.js` (14,474 lines total; the
plugin's own logic is lines 12338-14474 — everything before that is bundled
dependencies (zod, effect, etc.) and was not re-read line by line, only
grepped for structure). All line numbers below refer to this vendor snapshot
unless stated otherwise.

## A. Config keys

Every key below is destructured from `options` in `AutoResumePlugin`'s top
few lines (12606-12633). All 25 requested keys ARE read — none are dead on
arrival. One extra key exists that was **not** on the requested list:
**`actionIntentPrompt`** (line 12624: `options?.actionIntentPrompt ??
continuePrompt`) — a separate prompt override for the "action intent" nudge,
defaulting to whatever `continuePrompt` resolves to.

| Key | Default (constant) | Read at | Controls |
|---|---|---|---|
| `chunkTimeoutMs` | 45000 | 12606; used 13872, 14389 | How long a **busy** top-level session can go with no activity before being treated as a stalled stream (stall-recovery). |
| `checkIntervalMs` | 5000 | 12607; used 13835, 13968 | Poll interval of the main periodic watchdog (`setInterval`). |
| `gracePeriodMs` | 3000 | 12608; used 13790, 13872 | Extra buffer added on top of `subagentWaitMs`/`chunkTimeoutMs` before declaring a real stall. |
| `maxRetries` | 3 | 12609; used at 13398, 13539, 13608, 13791, 13885, 13942, 14110, 14368 | Shared retry/attempt ceiling for nearly every distinct recovery and nudge mechanism (not just one timer). |
| `maxBackoffMs` | 8000 | 12610; used inside `backoffMs()` (12563) | Cap on exponential backoff delay between retries. |
| `baseBackoffMs` | 1000 | 12611; used inside `backoffMs()` | Starting delay for exponential backoff (doubles per attempt). |
| `subagentWaitMs` | 15000 (opencode.json overrides to **300000**) | 12612; used 13790, 13838, 14019, 14389 | How long a parent can sit in "lone busy after 2+ were busy" before subagent-orphan checks kick in. |
| `loopMaxContinues` | 3 | 12613; used in `isHallucinationLoop()` (12658) | How many continues inside `loopWindowMs` count as a hallucination loop (triggers abort+resume instead of another continue). |
| `loopWindowMs` | 600000 (10 min) | 12614; used in `recordContinue()` (12648) | Sliding window over which `loopMaxContinues` is counted. |
| `toolTextCheckDelayMs` | 3000 | 12615; used 12998, 14173, 14245 | Delay before checking for a hallucinated/tool-call-as-text response, and the watchdog delay after sending a continue prompt. |
| `maxRecoveryRetries` | 2 | 12616; used 12953, 12974 | Separate, smaller retry ceiling specific to the prompt-timeout watchdog (distinct from `maxRetries`) before escalating to abort+resume. |
| `minActivityGapMs` | 1000 | 12617; used 13621 | Minimum quiet time since last real activity required before a nudge is allowed to fire. |
| `warmupMs` | 15000 | 12618; used 14142, 14212 | Grace period after session creation during which the action-intent nudge is suppressed. |
| `debug` | false | 12619; gates `dbg()` (12634-12637) | Verbose `console.log` debug output. **Does not** gate `log("debug", ...)` calls, which always attempt `ctx.client.app.log` regardless of this flag (only deduplicated, at line 12663, not suppressed). |
| `resumeOnActionIntent` | true (`!== false`) | 12622; used 13550, 14137, 14209 | Whether the "ends with a colon" action-intent detector can fire a nudge at all. |
| `subagentNativeCompactionEnabled` | false | 12633; used 14029 | Whether a subagent nearing its context limit gets `session.summarize()` called on it automatically. **Dead in this deployment** — see G. |
| `contextSaturationThreshold` | 0.85 | 12632; used 14028, 14084 | Token-usage ratio that triggers either native compaction (subagent) or the magic-context wrapup command (top-level). |
| `busyStallStrategy` | "continue" (validated against "abort"/"off") | 12630-12631; used 13873, 13886 | Whether a detected stream-stall gets a plain continue, an abort+resume, or is ignored entirely. **"abort"/"off" are dead in this deployment** — see G. |
| `silentDeadStreamMinTokens` | 200 | 12629 (var name `DEFAULT_SILENT_DEAD_STREAM_MIN_TOKENS`); used 14059 | Minimum output-token count before a text-less finished turn counts as a "silent dead stream" worth recovering. |
| `streamingFailureErrorNames` | `[ProviderError, APIError, StreamError, ConnectionError, TimeoutError]` | 12620; used inside `isStreamingFailure()` (12468), called at 14045, 14316 | Which assistant-error `.name` values count as a streaming failure eligible for auto-recovery. |
| `streamingFailureMessagePatterns` | 4 substrings/regexes (12361-12366) | 12621; used same as above | Fallback message-substring match when `.name` doesn't match. |
| `continuePrompt` | `"continue"` | 12623; the default fallback prompt at the majority of call sites | The baseline nudge text. |
| `doneWithoutWorkPrompt` | `DONE_WITHOUT_WORK_PROMPT` (12450) | 12627; used 13521-13522 (source "done-claim"), 13534-13538 (source "done-claim-no-emoji") | Sent when the model claims done but todos are open or no completion signal was found. |
| `doneWithoutDetailsPrompt` | `DONE_WITHOUT_DETAILS_PROMPT` (12451) | 12628; used 13541-13545 (source "done-claim-no-todos") | Sent when the model claims done, no todos are open, but gave no detailed report. |
| `toolTextRecoveryPrompt` | `TOOL_TEXT_RECOVERY_PROMPT` (12370) | 12625; used in the ternary at 13492 (source "text") | Sent when a raw tool call was printed as plain text instead of executed. |
| `thinkingToolRecoveryPrompt` | `THINKING_TOOL_RECOVERY_PROMPT` (12371) | 12626; same ternary (source "reasoning") | Same detection, but the tool call leaked into a `reasoning` part instead. |

## B. Injection sites (verbatim prompt text, trigger, gating)

Every place the plugin sends text into a session via `ctx.client.session.prompt()`
(the single call site is inside `sendContinuePrompt()`, line 12912; everything
below funnels through it or through `tryAbortAndResume()` → `sendContinuePrompt`).

**`TOOL_LOOP_RECOVERY_PROMPT`** (source labels `"tool-loop"` and
`"tool-text-loop"`), verbatim:
> "I notice you've been calling the same tool multiple times in a row without making progress. Please step back and reassess your approach. Consider: 1) Are you stuck in a loop? 2) Do you need different information first? 3) Should you try a different tool or break the task into smaller steps? Take a moment to think about what's blocking you and propose a different strategy."

Triggers: inside `checkForToolCallAsText()`, when a live `tool_use` part (source
`"tool-loop"`, line ~13453) or a hallucinated tool-call-as-text (source
`"tool-text-loop"`, line ~13479) is classified as a loop by `trackToolCall()`,
capped at `w.toolLoopAttempts < 2`. **Also** sent from a second, independent
site: the `"tool.execute.before"` hook's live-loop detector (14417-14453),
which runs on **every tool call for every session with no isSubagent check at
all** — it aborts the session directly (`ctx.client.session.abort`) and then
sends this same prompt, entirely outside `checkForToolCallAsText`'s gating.

**`continuePrompt`** (default `"continue"`), source `"tool-use"`: sent when a
live `tool_use` part is seen but not classified as a loop (priority 1, inside
`checkForToolCallAsText`).

**`toolTextRecoveryPrompt`** / **`thinkingToolRecoveryPrompt`**, sources
`"text"` / `"reasoning"`: sent when `containsToolCallAsText()` matches a plain
`text` or `reasoning` part respectively (priority 0, highest).

**`continuePrompt`**, source `"todo-completed-continue"`: sent when
`containsReadyToContinuePattern()` matches and all todos are already closed,
gated on `w.todoCheckAttempts >= 2` (priority 1).

**`doneWithoutWorkPrompt`**, source `"done-claim"`: sent when
`containsReadyToContinuePattern()` matches **and** `containsDoneClaimPattern()`
also matches, with open (or no) todos (priority 1).

**`continuePrompt`**, source `"ready-to-continue"`: same
`containsReadyToContinuePattern()` branch, but no done-claim pattern matched
(priority 1).

**`doneWithoutWorkPrompt`**, source `"done-claim-no-emoji"`: `!bestCandidate`
and `containsDoneClaimPattern()` matches, and open todos exist (priority 1).

**`doneWithoutDetailsPrompt`**, source `"done-claim-no-todos"`: same, but no
open todos and `w.doneClaimNoTodosAttempts < maxRetries` (priority 1).

**`actionIntentPrompt`** (defaults to `continuePrompt`'s value), source
`"action-intent"`: `containsActionIntent()` matches (a message ending in a
line longer than 5 and shorter than 500 chars ending with `:`), gated on
`resumeOnActionIntent` (priority 2, lowest). Fired from **three** separate
sites: inside `checkForToolCallAsText` itself (13558-13568); a standalone
500ms `setTimeout` inside the `session.status`→`"idle"` branch, gated on
`w.isSubagentAtCreation` (14140-14167); and an **identical, duplicated**
500ms `setTimeout` inside the separate `case "session.idle"` handler, which
is **NOT** gated on `w.isSubagentAtCreation` at all (14210-14237) — see the
contradiction noted in section F/G below.

**`buildOpenTodosReminder(todos)`** (dynamically built, not a fixed string;
lists each open todo by content/status), source `"idle-with-open-todos-reminder"`:
sent from four distinct sites — inside `checkForToolCallAsText` when
`!bestCandidate` and open todos exist and `busyCount() === 0` (13592-13598,
priority 2); the periodic timer's own idle-todo-nudge loop, labeled
`"Idle with open todos (periodic)"` (13959-13963, gated `if (w.isSubagent)
continue` at 13925); and two sites inside the `session.status`→`"idle"`
handler's `if (!w.isSubagent)` block, labeled `"Idle with open todos"`
(14119-14121) and `"Idle with open todos (celebration false positive)"`
(14113-14117, when a 🎉 was detected but todos remain open).

**`continuePrompt`**, unlabeled reasons `"Streaming failure on idle"`
(14041-14056) and `"Silent dead stream (${finish})"` (14057-14080): both
inside the `if (!w.isSubagent)` top-level-only recovery block.

**`continuePrompt`** (or abort+resume if `busyStallStrategy === "abort"`),
reason `"Stream stall"`: main periodic timer, a **busy** session idle past
`chunkTimeoutMs + gracePeriodMs`, `numBusy <= 1`. **Not** gated on isSubagent
at all — the idle-injection patch's own header explicitly says this path is
deliberately left untouched.

**`continuePrompt`**, reason `"Abort+Resume on ..."`: orphan-watch escalation
for a lone-busy parent whose subagent appears idle/crashed. Not gated on
isSubagent (applies to whichever session is currently the lone busy one).

`taskCompleteTool`'s own soft-block string ("You have N unfinished task(s)...")
is **not** a `session.prompt()` injection — it's the tool's own return value,
shown to the model as that tool call's result.

## C. Timers

10 `setTimeout`/`setInterval` call sites total (confirmed by grep count).

| # | Line(s) | Kind | Delay | Cleared by | Notes |
|---|---|---|---|---|---|
| 1 | 12950-12998 | `setTimeout` | `toolTextCheckDelayMs` | **Never** — handle not stored | Prompt-timeout watchdog inside `sendContinuePrompt`; fires regardless of whether the session still exists; guarded by `if (w)`-style checks and try/catch, so a dangling fire is a harmless no-op/log line at worst. |
| 2 | 13770-13968 | `setInterval` (`timer`) | `checkIntervalMs` | **Never** (`.unref()`'d only, line 13969-13970) | Main periodic watchdog; runs for the process lifetime. |
| 3 | 13971-13975 | `setInterval` (`discoveryTimer`) | `SESSION_DISCOVERY_INTERVAL_MS` (60000) | **Never** (`.unref()`'d only) | Periodic `session.list()` re-discovery. |
| 4 | 13976 | `setTimeout` | 5000 | N/A (one-shot) | Initial discovery kick. |
| 5 | 14140-14167 | `setTimeout` | 500 | N/A (one-shot) | Action-intent check, gated `isSubagentAtCreation`. |
| 6 | 14171-14173 | `setTimeout` (`w.toolTextTimer`) | `toolTextCheckDelayMs` | 9 sites (see below) | Schedules `checkForToolCallAsText`, gated `isSubagentAtCreation`. |
| 7 | 14210-14237 | `setTimeout` | 500 | N/A (one-shot) | **Duplicate** of #5, inside `case "session.idle"`, **not** gated. |
| 8 | 14243-14245 | `setTimeout` (`w.toolTextTimer`) | `toolTextCheckDelayMs` | same 9 sites | **Duplicate** of #6, **not** gated. |
| 9 | 13678 | `setTimeout` via `await new Promise` | `ABORT_CONTINUE_DELAY_MS` (2000) | N/A (awaited) | Delay between abort and follow-up continue, inside `tryAbortAndResume`. |
| 10 | 14443 | same pattern | `ABORT_CONTINUE_DELAY_MS` | N/A (awaited) | Same delay, inside the live tool-loop IIFE in `tool.execute.before`. |

`w.toolTextTimer` is the **only** timer handle ever stored on the per-session
state object, and the only one ever `clearTimeout`'d — 9 call sites: 12946,
13290, 13322, 13582, 14005, 14170, 14242, 14259, 14377. Every other timer
(the watchdog, both 500ms one-shots, and both `setInterval`s) has no stored
handle and cannot be individually cancelled.

If a timer fires after its session is gone: `sessions.get(sid)` / the
closed-over `w` reference is either `undefined` (guarded with `if (w)`) or a
stale-but-still-valid JS object; a resulting `ctx.client.session.prompt()`
call against a dead session ID fails and is caught (12924-12939), logging a
warning, never throwing uncaught. `sessions` entries are only actively
deleted in `cleanupIdleSessions()` (12799-12832, gated by `IDLE_CLEANUP_MS` =
10 min and `MAX_IDLE_SESSIONS` = 50) — otherwise stale entries simply persist
in the Map indefinitely.

## D. Global state (`sessions` Map iteration)

`sessions` (a single `Map`, line 12638) is the only piece of global,
cross-session state. Every iteration site and what it can write:

- `busyCount()` (12753-12760): read-only count of `status==="busy" &&
  !userCancelled` across **every** tracked session — no session-tree
  (parent/subagent) scoping at all.
- `getLoneBusySession()` (12761-12771): same unscoped iteration, returns the
  single busy session when the count is exactly 1.
- `cleanupIdleSessions()` (12799-12832): two full-Map passes; writes
  `sessions.delete(sid)`.
- Main periodic timer, two `for (const [sid, w] of sessions)` loops
  (13775, 13900): reads/writes `status`, `orphanWatchStartAt`, `gaveUp`,
  `aborting`, `lastSubagentCheckAt`, `todoNudgeAttempts`, etc., for every
  tracked session on every tick.
- **`case "session.error"`, `MessageAbortedError` branch (14304-14313)**:
  `for (const [wSid, w] of sessions) { if (!w.pluginAbortInFlight) {
  w.userCancelled = true; w.status = "idle"; resetIdleFlags(w); } }` — this
  marks **every single tracked session in the entire Map** as user-cancelled
  whenever **any one** session receives a `MessageAbortedError`, gated only
  by that session's own `pluginAbortInFlight` (used to distinguish the
  plugin's own abort calls from a genuine user ESC). `EventSessionError`'s
  own `sessionID` field is optional per the SDK types (see Task 1), so this
  may be a structural necessity rather than an oversight — but as written, a
  user pressing ESC in one session silences auto-resume in every other
  tracked session (including unrelated concurrent top-level tasks) until each
  is re-armed by its own next `chat.message` (14411-14415).
- **`case "session.status"`, `"idle"` sub-branch (14013-14021)** — the
  `getLoneBusySession()`-driven heuristic: when `busyCount()` transitions
  from >1 to exactly 1, the plugin assumes the session that JUST went idle
  (`w`, the current event's own session) was a subagent, and unconditionally
  sets `w.isSubagent = true` on it — purely from a **global busy-count
  coincidence**, with no check that the remaining lone-busy session
  (`lone.w`) is actually this session's parent. Two independent, unrelated
  session trees running concurrently (e.g. two separate top-level tasks each
  with their own subagents) could produce the same 2-busy→1-busy transition
  by coincidence and cause a wrong session to be mislabeled `isSubagent`.
  This is the exact contamination risk the idle-injection patch's own header
  names as the reason it introduced the separate, immutable
  `isSubagentAtCreation` field instead of trusting this mutable flag for its
  own gating — but (per section E) `task_complete`'s own handler still reads
  the **mutable** `w.isSubagent`, not `w.isSubagentAtCreation`, so it remains
  exposed to this same contamination.

## E. `task_complete`

Registration (14360-14384, 14400-14402):
```
tool: { task_complete: taskCompleteTool }
```
- **description** (shown to the model): "Signal that all work is complete and
  stop automatic continuation prompts. Call this ONLY after finishing
  everything requested."
- **args**: `{}` — no parameters.
- **handler**: looks up `sessions.get(ctx.sessionID)`. If `w` exists and
  `!w.isSubagent` (the **mutable** flag — see D above) and open todos remain
  and `w.taskCompleteOverrides < maxRetries`: increments
  `taskCompleteOverrides` and returns (as the tool's own result string, not a
  session prompt) "You have N unfinished task(s). Please complete all
  remaining work before signaling completion." — a soft block; the tool call
  itself still "succeeds" from the framework's point of view. Otherwise (this
  is the unconditional path for a session flagged `isSubagent`, or a
  top-level session with no open todos, or one that has already exhausted
  its overrides): sets `w.toolTextRecovered = true`, `w.completionSignaled =
  true`, clears `w.toolTextTimer` if present, logs, and returns "Task
  completion acknowledged. No further continuation will be sent."
- **What wakes the parent**: nothing in `task_complete` itself. It only ever
  touches the calling session's own `w` entry. A finished subagent's parent is
  woken purely by the orphan-watch/lone-busy machinery in section D, entirely
  independent of whether `task_complete` was ever called.

## F. The patches

`git log --oneline -- .opencode/patches/` shows **three** patches applied
historically, matching the task's background description, though only **two**
remain active in `.opencode/patches/` today (both confirmed present as files
and both confirmed applied in the vendor snapshot via 4 total
`PATCHED (NordicBeesERP` markers):

1. **`fix-auto-resume-subagent-task-complete.js`** (commit `43eb5de`,
   2026-09-06) — targeted `opencode-auto-resume@1.1.3` loaded from
   `.opencode/node_modules/opencode-auto-resume` (a local npm dependency, per
   the older `.opencode/package.json`). **Dropped** in commit `c5e2dcd`
   (2026-09-12, "harness: drop dead subagent-task-complete patch") because
   opencode.json's plugin pin moved to the versioned spec
   `"opencode-auto-resume@1.1.15"`, which opencode's own loader resolves via
   the **global** `~/.cache/opencode/packages/` cache instead — the
   `.opencode/node_modules` copy this patch targeted became dead, unused
   leftover, and 1.1.15 had already fixed the same underlying gate upstream
   in its own way (see E above: the unconditional-set path for `isSubagent`
   sessions). Not present in the current vendor snapshot; no marker for it.

2. **`fix-auto-resume-doneclaim-false-positive.js`** (commit `fa0e2fa`,
   2026-09-12) — 1 marker at line 12453. Confirmed change, read directly
   from the patched code: `containsDoneClaimPattern()` now returns `false`
   outright for any message longer than 400 trimmed characters
   (`DONE_CLAIM_MAX_LEN`), **before** running any of the `DONE_CLAIM_PATTERNS`
   regexes against the last 5 lines. Without this gate, four loose/unanchored
   patterns added between 1.1.3 and 1.1.15 (`\bdone\s+with...`,
   `\bfinished\s+...`, `\b(?:all|everything)\s+(?:is\s+)?...`,
   `\bnothing\s+(?:else\s+)?(?:left|remaining|to do)`) could match a bare
   phrase anywhere inside a long, legitimate, substantive final report.

3. **`fix-auto-resume-idle-injection-topsession.js`** (commit `4f00cc6`,
   2026-09-12) — 3 markers, lines 12725, 14126, 14190. Confirmed changes,
   read directly from the patched code: added a new, **immutable**,
   creation-time-only field `w.isSubagentAtCreation` (set once at
   `session.created`, line 14192, and never touched again — unlike the
   mutable `w.isSubagent`, which the busy-count heuristic in section D can
   flip after the fact); and gated the `checkForToolCallAsText` scheduling +
   its accompanying action-intent check, **inside the `session.status` →
   `"idle"` branch only** (line 14125: `if (w.isSubagentAtCreation &&
   ...)`), so these nudges only reach sessions that were subagents from
   birth. The patch's own header (read in full above) states its intent
   plainly: gate "every session.idle transition." **Verified discrepancy**:
   the vendor file has a **second, structurally distinct** switch case,
   `case "session.idle":` (a different `Event.type` string from
   `"session.status"` with a status payload of type `"idle"` — see Task 1's
   Event-union list, both are real, separate event types), at lines
   14201-14249, which reaches the exact same `checkForToolCallAsText`
   scheduling (14243-14245) and the exact same action-intent
   `setTimeout` (14210-14237) **without any `isSubagentAtCreation` check at
   all** — gated only on `resumeOnActionIntent`/`toolTextRecovered`/
   `completionSignaled`/`toolTextAttempts`. I could not determine, by static
   reading alone, whether opencode's runtime actually emits both
   `session.status{type:"idle"}` and `session.idle` for the same transition
   (making this ungated path a live, unpatched hole) or only one of them in
   practice (making the other case effectively unreachable dead code). This
   is exactly the kind of fact the trace recorder (Task 4) and its
   `autoresume_injection_suspected` counter (Task 6) are built to answer —
   flagged as an open question in the handoff report, not resolved here.

## G. Dead code

1. **`subagentNativeCompactionEnabled`-gated native compaction** (line
   14029-14034, `ctx.client.session.summarize({path:{id:sid}})`): default
   `false`, and opencode.json's `opencode-auto-resume` options object (which
   sets `chunkTimeoutMs`, `gracePeriodMs`, `maxRetries`, `subagentWaitMs`
   only) does not set it. Dead in this deployment.
2. **Magic-context wrapup command** (lines 14081-14100,
   `ctx.client.session.command({body:{command: CTX_WRAPUP_TRIGGER, ...}})`):
   gated on `isMagicContextInstalled()`, which inspects the live config's
   plugin list for anything containing `"magic-context"` — opencode.json's
   `plugin` array contains only `opencode-auto-resume@1.1.15`. Dead in this
   deployment (would activate automatically if a magic-context plugin were
   ever added, with no code change needed).
3. **`busyStallStrategy === "abort"` / `"off"`** branches (13873, 13886):
   opencode.json does not set `busyStallStrategy`, so it defaults to
   `"continue"` and these two branches never execute. Dead in this
   deployment.
4. **`statusType === "interrupted"`** branch (14000-14009) and
   **`case "session.interrupted":`** (14250-14265) — not config-gated, so
   this doesn't strictly fit "dead under config we don't set," but it is
   dead for a related reason worth recording here anyway: per Task 1's
   reading of the currently-installed `@opencode-ai/sdk` types, there is no
   `"interrupted"` member of `SessionStatus` and no `session.interrupted`
   `Event` type at all in the current type surface. Whether the live wire
   protocol still emits something these branches would catch is unverified
   — flagged as an open question, not asserted as dead with certainty.

## H. Duplicate tool registration

**Answer: last-registered wins, silently — no error, no warning, no drop.**
Both registrations are actually kept, but only the most recent one is ever
visible to the model (tool list) or reachable by a tool call.

Evidence: `strings -a` extraction from the installed binary
(`/Users/deividasru/.opencode/bin/opencode`, confirmed `1.18.30` via
`opencode --version`, Mach-O 64-bit arm64), following the same technique the
circuit-breaker plugin's own header documents using successfully for the same
binary. No local opencode source checkout exists anywhere on this machine
(checked `~/Projects`, `~/dev`, `~/src`, `~/code`, common opencode-source
locations) — the binary is the only available ground truth.

The relevant service is literally named `"@opencode/v2/ToolRegistry"` in the
compiled output (a `class c1 extends j_.Service()("@opencode/v2/ToolRegistry")`
declaration is present verbatim in the strings dump). Its `register` effect
(paraphrasing minified variable names as found):

```
register: for each [name, toolDef] entry being registered:
  A.set(name, [...(A.get(name) ?? []), { token, registration: { identity: {}, tool: toolDef } }])
  // (a finalizer removes this specific {token, ...} entry from the array on
  // plugin/registration teardown, filtering by `token !== W`)

materialize(): 
  R = new Map(builtin_tools.entries())
  for (const [name, arr] of A) {
    const last = arr.at(-1)?.registration
    if (last) R.set(name, last)
  }
  return {
    definitions: [...tool schemas built from R, shown to the model...],
    settle(call): {
      const reg = R.get(call.name)
      if (reg) return dispatch(call, reg.identity)
      return { result: { type: "error", value: `Unknown tool: ${call.name}` } }
    }
  }
```

So: registering a tool name that's already registered **appends** to an
internal array for that name rather than rejecting, overwriting in place, or
erroring — but `materialize()`, which builds **both** the tool list handed to
the model **and** the dispatch table used to resolve an actual tool call
(`settle`), always takes `.at(-1)` — the most-recently-appended entry. Earlier
registrations for the same name are still present in the array (so they can
be individually torn down via their own `token`/finalizer without disturbing
others) but are completely invisible to the model and unreachable by any call
while shadowed.

**Practical implication for a future gradual migration** (informational only,
not a design decision made here): if a replacement plugin also registers
`task_complete`, whichever plugin's registration is processed **last** during
plugin load silently wins — no crash, no warning surfaces anywhere in this
code path. I did not verify what determines load/registration order (e.g.
whether it strictly follows opencode.json's `plugin` array order) — that
would need a small, separate live experiment (proposed, not run): register a
second dummy `tool: { task_complete: ... }` from a throwaway plugin listed
before vs. after `opencode-auto-resume` in opencode.json, and observe via a
completed `message.part.updated` tool part's `output` which implementation's
text actually came back.

`"Unknown tool: "`, `"Tool has no execute handler: "`, and `"Invalid tool
input: "` (the three rejection-reason prefixes circuit-breaker.ts's header
already documented finding) are all still present verbatim in this binary,
confirming circuit-breaker's own precedent technique still applies to the
current install: `grep -c "Tool has no execute handler"` → 2 occurrences,
`grep -c "Invalid tool input"` → 2 occurrences (once each in what appear to be
two structurally similar but separate call-dispatch code paths within the
bundle — not further disambiguated here, out of scope for this task).
