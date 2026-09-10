import type { Plugin } from "@opencode-ai/plugin"
import { appendFileSync, mkdirSync, existsSync } from "fs"
import { join } from "path"

/**
 * NordicBeesERP loop circuit-breaker.
 *
 * Resolves 6 BUGLOG.md loop-family entries that had no mechanical guardrail:
 * post-completion-continue-loop, deadlock-constraint-conflict,
 * idle-no-input-loop, plan-without-execution-gap,
 * harness-blocked-state-not-terminated, self-diagnosed-loop-no-behavioral-stop.
 *
 * Mechanism proven working in .opencode/reports/abort-smoke.log (2026-09-05):
 * subscribe to message.part.updated tool parts, use part.sessionID directly,
 * call client.session.abort({ path: { id } }).
 *
 * Two implementation assumptions were empirically confirmed via a live
 * orchestrator->reviewer test run (2026-09-06, `opencode run --print-logs
 * --log-level DEBUG`, real session.created log line showed
 * parentID=<orchestrator session> on the reviewer subagent's own session)
 * before this file was finalized, using temporary debug logging since
 * removed:
 * - session.created for a given sessionID always precedes any
 *   message.part.updated for that same sessionID — zero dropped-
 *   registration cases across a real run with multiple reviewer turns and
 *   a compaction cycle.
 * - ToolPart.state's `input` field (not `raw`) is what's actually
 *   populated at `"completed"` status — confirmed against a real captured
 *   event, not assumed.
 *
 * Design and evidence: .opencode/planning/circuit-breaker-and-semgrep-plan.md
 *
 * IMPORTANT (2026-09-06): this file was accidentally reverted once already
 * because it was sitting as an UNCOMMITTED change that a `git checkout`/
 * `git stash`/`git reset` elsewhere wiped out. This file, and the prompt
 * files under .opencode/prompts/, are harness infrastructure that the
 * normal orchestrator/coder/fixer workflow never commits on its own
 * (orchestrator's own edit permissions don't cover .opencode/plugin/* or
 * .opencode/prompts/*) — COMMIT THIS FILE MANUALLY after any edit, or the
 * next git-tree-cleaning operation will silently discard the fix again.
 */

const SAME_TOOL_STREAK_THRESHOLD = 8
const IDENTICAL_CALL_THRESHOLD = 3
const ABSOLUTE_CEILING = 1000

// Cyclic multi-step pattern detector (BUGLOG:
// circuit-breaker-blind-to-multi-step-cycles, first occurrence 2026-09-10).
// Both checks above operate on a bounded CONSECUTIVE window requiring
// uniformity across the whole window (same tool name for 8 calls, or same
// tool+args for 3 calls) — a periodic cycle whose period is longer than 1
// call structurally cannot satisfy either: a real incident looped ~20
// times on the 5-step sequence skill(mempalace) -> roslyn_get_type_members
// -> read -> read -> read (byte-identical args every cycle, confirmed from
// the actual session DB), and circuit-breaker.jsonl has zero entries for
// it, confirmed via `grep roslyn_get_type_members` returning nothing.
//
// CYCLE_MAX_PERIOD (8) and CYCLE_REPEAT_THRESHOLD (5) were chosen against
// real data, not guessed: replayed the real incident's actual tool-call
// sequence (fires at call #45 of 111 — well before its true ~20-cycle
// length) and a real, legitimate, successful 518-tool-call fixer session
// (task-stats.jsonl's own largest recorded n_toolcalls, 748, counted
// in-flight calls across concurrent sessions; 518 is this one session's
// own actual tool-call count) against several repeat thresholds first.
// That legitimate session contains a real, coincidental 3-step cycle
// (`git branch -v` -> `git log --oneline -5` -> `git status`, byte-
// identical each time) that recurs 3-4 times in a row on its own, TWICE,
// before the agent moved on and completed the task successfully —
// meaning REPEAT_THRESHOLD=3 (matching IDENTICAL_CALL_THRESHOLD's own
// value) and even 4 both false-positive on real, legitimate work; 5 is
// the first threshold clean against this real session while still
// catching the real incident with more than half its length to spare.
// This is real evidence a naive low threshold is not acceptable here, the
// same concern the 748-tool-call task raised in the first place.
const CYCLE_MAX_PERIOD = 8
const CYCLE_REPEAT_THRESHOLD = 5
const HISTORY_MAX = CYCLE_MAX_PERIOD * CYCLE_REPEAT_THRESHOLD

/**
 * Looks for a period-P sequence of calls (2 <= P <= CYCLE_MAX_PERIOD) that
 * repeats CYCLE_REPEAT_THRESHOLD times back to back, exact tool+argsHash
 * match required for every position. Starts at period 2 deliberately —
 * period 1 repeated K times is exactly what identical-args-streak (K=3,
 * consecutive) already covers; this is additive, not a replacement.
 * Checks the SHORTEST period first so a genuine short cycle (e.g. the
 * real 5-step incident) is reported as period 5, not misidentified as a
 * coincidental longer period that happens to also satisfy the tail.
 */
function detectCyclicPattern(history: CallRecord[]): { period: number; repeats: number } | null {
  for (let period = 2; period <= CYCLE_MAX_PERIOD; period++) {
    const needed = period * CYCLE_REPEAT_THRESHOLD
    if (history.length < needed) continue
    const tail = history.slice(-needed)
    const block = tail.slice(0, period)
    let matches = true
    for (let rep = 1; rep < CYCLE_REPEAT_THRESHOLD && matches; rep++) {
      const segment = tail.slice(rep * period, (rep + 1) * period)
      for (let i = 0; i < period; i++) {
        if (segment[i].tool !== block[i].tool || segment[i].argsHash !== block[i].argsHash) {
          matches = false
          break
        }
      }
    }
    if (matches) return { period, repeats: CYCLE_REPEAT_THRESHOLD }
  }
  return null
}

// Malformed/rejected tool-call streak (BUGLOG: tool-call-json-argument-
// bleed-multicall, first occurrence 2026-09-09). Confirmed by reading the
// actual installed OpenCode binary's own tool-dispatch logic (v1.18.30,
// `strings` extraction — grepped for these exact literals and found them
// live in the binary, not assumed): these are the three message prefixes
// OpenCode itself uses when a tool call is rejected BEFORE the tool's own
// execute() ever runs -- unknown tool name, no execute handler registered,
// or the call's arguments failed schema/JSON decode (this last one is what
// a truncated/argument-bled tool call actually surfaces as: "Invalid tool
// input: " + the underlying JSON.parse/decode error message). A tool that
// DID start executing and then threw its own runtime error (a failing
// dotnet build, a nonzero bash exit) produces a different, tool-specific
// message and must never count toward this streak -- only these three
// prefixes mean "the call itself was rejected," which is never a
// legitimate outcome for any role, unlike the repetition-based detectors
// below which DO have legitimate cases (see SAME_TOOL_STREAK_MAX_DISTINCT_ARGS's
// own comment for a concrete one). This is why this detector, uniquely
// among all of this file's detectors, is NOT limited to subagentSessions --
// see its own comment further down in the event handler.
const MALFORMED_STREAK_THRESHOLD = 3
const MALFORMED_ERROR_MAX_LEN = 400
const MALFORMED_ERROR_PREFIXES = ["Unknown tool: ", "Tool has no execute handler: ", "Invalid tool input: "]

function isMalformedToolError(error: string): boolean {
  return MALFORMED_ERROR_PREFIXES.some((p) => error.startsWith(p))
}

// A pure text-only repeat loop (no tool calls at all between turns) is
// aborted after this many consecutive near-identical completed text
// parts. Real incident (2026-09-06): a fixer session correctly finished
// its actual work (build+commit+verify, confirmed via git show) in its
// first turn, but never reached fixer.md's own mandatory step 10
// (agent-guardrails check) or emitted the mandated GUARDRAIL_SCORE= line
// — so it never produced the terminal marker the other checks below key
// off of. It then regenerated a byte-for-byte identical "Objective/Work
// State/Next Move: Report task as complete" block roughly 30 times in a
// row with ZERO tool calls in between — a shape neither same-tool-streak
// nor identical-args-streak (both tool-call-based) nor the
// post-terminal-continuation check (which waits for a FOLLOWING tool
// call that never came) can detect, since none of them fire on
// text-only turns.
const TEXT_REPEAT_THRESHOLD = 3

// Max distinct argsHash values allowed within a same-tool-streak window
// for it to still count as "stuck repeating". Real incident (2026-09-06):
// fixer.md's normal, entirely legitimate workflow is bash-ONLY (build,
// git status, git add, git diff, git commit, git log, bump-version,
// guardrail check — 7-10 sequential steps, ALL tool=="bash"). The
// original same-tool-streak check compared only tool NAME, so this
// completely normal single-tool-agent workflow was indistinguishable
// from an actual stuck loop and got auto-aborted constantly (see
// circuit-breaker.jsonl entries with 8 different git/dotnet commands all
// flagged as "same-tool-streak"). A genuine loop repeats the SAME FEW
// commands over and over (low distinct-argsHash count); a healthy
// multi-step bash workflow has high distinct-argsHash count even though
// every call shares the tool name "bash". This threshold distinguishes
// the two. NOTE: this also correctly catches the 2026-09-06 `coder`
// alternating-read loop (Read fileA, Read UI_STANDARD.md, repeat) —
// tool name "read" stays constant but only 2 distinct paths alternate,
// well under this threshold, so same-tool-streak still fires for that
// shape once this diversity check is combined with the tool-name check.
const SAME_TOOL_STREAK_MAX_DISTINCT_ARGS = 3

// Matches fixer.md's mandated terminal report line (GUARDRAIL_SCORE=<N>
// or GUARDRAIL_SCORE=N/A) and reviewer.md's mandated APPROVED/REJECTED
// verdict. Either one means the subagent itself declared a terminal
// state (DONE/BLOCKED/OUT_OF_SCOPE, or a review verdict) and, per its own
// system prompt, should stop generating and make no further tool calls.
// Real incident this guards against (2026-09-06): a fixer session
// reported GUARDRAIL_SCORE= (DONE) at round ~39 of what should have been
// a single-round task, then kept being re-invoked ('continue if you have
// next steps') for 40+ more rounds across repeated auto-compactions,
// re-diagnosing and re-committing the same file, because nothing
// mechanically enforced fixer.md's own 'stop generating' terminal rule.
const TERMINAL_MARKER_RE = /GUARDRAIL_SCORE\s*=|\bAPPROVED\b|\bREJECTED\b/

type CallRecord = { tool: string; argsHash: string; callID: string }
type SessionState = {
  history: CallRecord[]
  seenCallIDs: Set<string>
  terminalReported: boolean
  lastTextKey: string | null
  textRepeatCount: number
}
const sessions = new Map<string, SessionState>()

function newSessionState(): SessionState {
  return { history: [], seenCallIDs: new Set(), terminalReported: false, lastTextKey: null, textRepeatCount: 0 }
}

// Normalizes a report's text for repeat-comparison: collapses whitespace
// and strips a few known-volatile tokens (compaction timing suffixes,
// trailing checkmarks) so near-identical re-generations of the same
// report still compare equal even if punctuation/whitespace drifts
// slightly between turns, matching what was actually observed in the
// 2026-09-06 incident transcript (repeated blocks were byte-identical
// except for a trailing ✅ appearing inconsistently).
function normalizeReportText(text: string): string {
  return text.replace(/\s+/g, " ").replace(/✅/g, "").trim()
}

// Populated ONLY from session.created events where parentID is present —
// i.e. only real Task-tool-spawned subagent sessions. The top-level
// orchestrator session (no parentID) is never added here and is therefore
// never tracked or abortable by this plugin, regardless of its own
// tool-call pattern.
const subagentSessions = new Set<string>()

// Malformed-tool-call tracker — a SEPARATE map, deliberately keyed by
// EVERY session this plugin observes (subagent AND top-level orchestrator
// sessions alike), unlike `sessions` above which only exists for sessions
// already in subagentSessions. See the detector's own comment in the event
// handler for why.
type MalformedTracker = { streak: number; recentErrors: string[]; seenPartIds: Set<string> }
const malformedTrackers = new Map<string, MalformedTracker>()

function cleanup(sessionID: string) {
  sessions.delete(sessionID)
  subagentSessions.delete(sessionID)
  malformedTrackers.delete(sessionID)
}

// Lightweight always-on diagnostic log (BUGLOG: circuit-breaker-blind-to-
// error-status-tool-calls, 2026-09-10). Added because answering "did this
// plugin even see these events" for the mudblazor incident took an 8-minute
// direct SQLite dive into opencode's own opencode.db, since nothing in this
// file records its own liveness independently of an actual abort. Writes
// ONE "observed" line per completed-or-errored tool-call part this plugin
// sees, for EVERY session (subagent or not, mirroring the malformed-tool-
// call detector's own unscoped reach) — so a future "why didn't this fire"
// question is answerable by reading a file instead of the database:
//   - zero lines for the incident's time window => this plugin (or the
//     whole process) never saw the events at all;
//   - lines with isSubagentTracked:false => the scoping guard excluded the
//     session (see subagentSessions above), not a detection failure;
//   - lines with status:"error" => this call was invisible to all three
//     repetition detectors below (the confirmed 2026-09-10 gap);
//   - a "checked" line (emitted later, once for every subagent-tracked
//     completed call, see below) with cyclicVerdict:null repeatedly is a
//     genuine "checked, found nothing" — as opposed to no "checked" line
//     ever following an "observed" one for the same part, which would mean
//     something between the two throws and is being silently swallowed
//     somewhere in this file's own completed-call handling (this doubles
//     as a live check for that possibility going forward, not just a
//     one-off answer to the mudblazor incident).
const DIAG_SEEN_MAX = 5000
const diagSeenPartIds = new Set<string>()
function diagLog(diagPath: string, reportsDir: string, record: Record<string, unknown>) {
  try {
    if (!existsSync(reportsDir)) mkdirSync(reportsDir, { recursive: true })
    appendFileSync(diagPath, JSON.stringify({ ts: new Date().toISOString(), ...record }) + "\n", "utf8")
  } catch {
    // Diagnostics must never affect real detection — same defensive pattern as logAbort below.
  }
}

function logAbort(
  logPath: string,
  reportsDir: string,
  record: {
    sessionID: string
    reason: string
    history: CallRecord[]
    totalToolCalls: number
    recentErrors?: string[]
    cyclePeriod?: number
  },
) {
  try {
    if (!existsSync(reportsDir)) mkdirSync(reportsDir, { recursive: true })
    appendFileSync(
      logPath,
      JSON.stringify({ ts: new Date().toISOString(), ...record }) + "\n",
      "utf8",
    )
  } catch {
    // Never let logging failure block the actual abort — same defensive
    // pattern as nordicbees-quality-monitor.ts's appendRecord.
  }
}

export const NordicBeesCircuitBreaker: Plugin = async ({ client, directory }) => {
  const reportsDir = join(directory, ".opencode", "reports")
  const logPath = join(reportsDir, "circuit-breaker.jsonl")
  const diagPath = join(reportsDir, "circuit-breaker-diag.jsonl")

  return {
    event: async ({ event }) => {
      // Registration: only sessions with a parentID are subagents.
      if (event.type === "session.created") {
        if (event.properties.info.parentID) {
          subagentSessions.add(event.properties.info.id)
        }
        return
      }
      // Cleanup path 1: session finished normally.
      if (event.type === "session.idle") {
        cleanup(event.properties.sessionID)
        return
      }
      // Cleanup path 2: session torn down some other way.
      if (event.type === "session.deleted") {
        cleanup(event.properties.info.id)
        return
      }

      if (event.type !== "message.part.updated") return
      const part = event.properties.part

      // "observed" diagnostic checkpoint — see diagLog's own comment above.
      // Deliberately unconditional: runs before the malformed-tracker logic,
      // before the subagentSessions scoping guard, before anything that
      // could return early or throw, so its presence or absence is the
      // ground truth for "did this plugin see this event at all."
      if (part.type === "tool" && (part.state?.status === "completed" || part.state?.status === "error")) {
        if (diagSeenPartIds.size > DIAG_SEEN_MAX) diagSeenPartIds.clear()
        if (!diagSeenPartIds.has(part.id)) {
          diagSeenPartIds.add(part.id)
          diagLog(diagPath, reportsDir, {
            stage: "observed",
            sessionID: part.sessionID,
            isSubagentTracked: subagentSessions.has(part.sessionID),
            tool: part.tool,
            status: part.state?.status,
          })
        }
      }

      // Malformed-tool-call detector — runs for EVERY session, including
      // the top-level orchestrator session, BEFORE the subagent-only
      // scoping guard below. Deliberate choice, not an oversight: the
      // 2026-09-09 argument-bleed incident this guards against happened on
      // the orchestrator's own top-level session (parentID=undefined,
      // confirmed via .local/share/opencode/log/opencode.log), and a
      // rejected/malformed tool call has no legitimate case for ANY role
      // the way "many calls to the same tool" does for the orchestrator's
      // own long bash reconnaissance sequences (that legitimate-repetition
      // concern is specific to the three repetition-based detectors below,
      // not to this one — see SAME_TOOL_STREAK_MAX_DISTINCT_ARGS's comment).
      // A malformed-call streak means the model's own tool-call-generation
      // is broken right now, at any level, so aborting the top-level
      // session here is the correct outcome, not an over-broad one.
      if (part.type === "tool" && part.state?.status === "completed") {
        const tracker = malformedTrackers.get(part.sessionID)
        if (tracker) tracker.streak = 0
      } else if (part.type === "text" && part.time?.end) {
        // Real TextPart completion signal (time.end set) — NOT
        // part.state?.status, which the pre-existing text-repeat-loop
        // detector below uses but TextPart has no `.state` field at all
        // per the actual SDK type (@opencode-ai/sdk types.gen.d.ts) —
        // flagging that as a separate, likely-real bug, not fixed here
        // (out of scope for this task; that detector's own condition
        // never evaluates true against a real TextPart as far as I can
        // tell from the type definitions, but I have not proven it live).
        const tracker = malformedTrackers.get(part.sessionID)
        if (tracker) tracker.streak = 0
      } else if (part.type === "tool" && part.state?.status === "error" && isMalformedToolError(part.state?.error ?? "")) {
        const tracker = malformedTrackers.get(part.sessionID) ?? { streak: 0, recentErrors: [], seenPartIds: new Set() }
        malformedTrackers.set(part.sessionID, tracker)
        if (!tracker.seenPartIds.has(part.id)) {
          tracker.seenPartIds.add(part.id)
          tracker.streak += 1
          const inputSnippet = JSON.stringify(part.state?.input ?? {}).slice(0, MALFORMED_ERROR_MAX_LEN)
          const errorSnippet = (part.state?.error ?? "").slice(0, MALFORMED_ERROR_MAX_LEN)
          tracker.recentErrors.push(`tool=${part.tool} error=${errorSnippet} input=${inputSnippet}`)
          if (tracker.recentErrors.length > MALFORMED_STREAK_THRESHOLD) tracker.recentErrors.shift()

          if (tracker.streak >= MALFORMED_STREAK_THRESHOLD) {
            logAbort(logPath, reportsDir, {
              sessionID: part.sessionID,
              reason: "malformed-tool-call-streak",
              history: [],
              totalToolCalls: tracker.streak,
              recentErrors: [...tracker.recentErrors],
            })
            await client.session.abort({ path: { id: part.sessionID } })
            cleanup(part.sessionID)
          }
        }
      }

      // Scoping guard: never track/abort a session we didn't register as
      // a subagent above — this is what keeps the orchestrator's own
      // top-level session permanently out of reach of this plugin.
      if (!subagentSessions.has(part.sessionID)) return

      // Terminal-marker detection: a completed TEXT part (the subagent's
      // own prose report, not a tool call) containing GUARDRAIL_SCORE= or
      // APPROVED/REJECTED means the subagent has declared itself done,
      // per fixer.md/reviewer.md's own terminal-state rules. Record this
      // on the session so that ANY further tool call after this point is
      // treated as a post-terminal continuation loop, regardless of
      // whether it repeats an identical call or varies each time.
      if (part.type === "text" && part.time?.end) {
        const text: string = part.text
        const state = sessions.get(part.sessionID) ?? newSessionState()
        sessions.set(part.sessionID, state)

        if (TERMINAL_MARKER_RE.test(text)) {
          state.terminalReported = true
        }

        // Text-only repeat loop: this does NOT wait for a following tool
        // call (there may never be one — that's exactly the failure shape
        // this exists for). Compare this turn's normalized text against
        // the previous turn's; if the SAME report is regenerated
        // TEXT_REPEAT_THRESHOLD times in a row with no tool call in
        // between (a tool call resets this counter below), abort right
        // here, in the text handler itself.
        const key = normalizeReportText(text)
        if (key.length > 0 && key === state.lastTextKey) {
          state.textRepeatCount += 1
        } else {
          state.textRepeatCount = 1
          state.lastTextKey = key
        }

        if (state.textRepeatCount >= TEXT_REPEAT_THRESHOLD) {
          logAbort(logPath, reportsDir, {
            sessionID: part.sessionID,
            reason: "text-only-repeat-loop",
            history: [...state.history],
            totalToolCalls: state.seenCallIDs.size,
          })
          client.session.abort({ path: { id: part.sessionID } }).finally(() => cleanup(part.sessionID))
        }
        return
      }

      if (part.type !== "tool" || part.state?.status !== "completed") return

      const state = sessions.get(part.sessionID) ?? newSessionState()
      sessions.set(part.sessionID, state)

      // Any real tool call breaks a text-only-repeat streak — the agent
      // is doing something again, not just re-narrating.
      state.textRepeatCount = 0
      state.lastTextKey = null

      // Post-terminal continuation: the subagent already reported DONE/
      // BLOCKED/OUT_OF_SCOPE (fixer) or APPROVED/REJECTED (reviewer) in a
      // prior turn, and is now making ANOTHER tool call anyway. Per its
      // own system prompt this should never happen — abort immediately,
      // before this becomes another multi-round re-diagnosis loop.
      if (state.terminalReported) {
        logAbort(logPath, reportsDir, {
          sessionID: part.sessionID,
          reason: "post-terminal-continuation",
          history: [...state.history],
          totalToolCalls: state.seenCallIDs.size,
        })
        client.session.abort({ path: { id: part.sessionID } }).finally(() => cleanup(part.sessionID))
        return
      }
      if (state.seenCallIDs.has(part.callID)) return // dedupe repeated update events
      state.seenCallIDs.add(part.callID)

      const argsHash = JSON.stringify(part.state?.input ?? {})
      state.history.push({ tool: part.tool, argsHash, callID: part.callID })
      // Capped at HISTORY_MAX (40), not SAME_TOOL_STREAK_THRESHOLD (8) —
      // the cyclic-pattern detector below needs up to CYCLE_MAX_PERIOD *
      // CYCLE_REPEAT_THRESHOLD calls of context. same-tool-streak below
      // explicitly slices the last 8 of this longer array rather than
      // relying on the array's own length, so it keeps working exactly as
      // before now that the array can hold more than 8 entries.
      if (state.history.length > HISTORY_MAX) state.history.shift()

      const total = state.seenCallIDs.size
      const full = state.history
      const last = full.slice(-SAME_TOOL_STREAK_THRESHOLD)
      // Diversity check ignores the `workdir` field on purpose: the same
      // logical bash command (e.g. "git status") sometimes carries an
      // explicit workdir and sometimes doesn't, purely depending on
      // whether cwd was already correct — that alone must not count as
      // two "different" commands, or a genuine stuck-checking-state loop
      // (repeatedly running only git status/git log, never progressing to
      // build/commit) can hide behind spurious workdir-presence diversity.
      // Real incident (2026-09-06): an 8-call session consisting only of
      // git status/git log repeated, with workdir present on some calls
      // and absent on others, computed as 5 "distinct" full argsHash
      // values — above the threshold — purely because of this noise. For
      // non-bash tools (e.g. "read"), there is no `command` field, so
      // this falls back to the full argsHash — which already varies
      // correctly by file path for a genuine alternating-file-read loop.
      const commandKey = (h: CallRecord): string => {
        try {
          const parsed = JSON.parse(h.argsHash)
          return typeof parsed?.command === "string" ? parsed.command : h.argsHash
        } catch {
          return h.argsHash
        }
      }
      const distinctArgsInWindow = new Set(last.map(commandKey)).size
      const sameToolStreak = last.length === SAME_TOOL_STREAK_THRESHOLD &&
        last.every(h => h.tool === last[0].tool) &&
        distinctArgsInWindow <= SAME_TOOL_STREAK_MAX_DISTINCT_ARGS
      const lastN = full.slice(-IDENTICAL_CALL_THRESHOLD)
      const identicalStreak = lastN.length === IDENTICAL_CALL_THRESHOLD &&
        lastN.every(h => h.tool === lastN[0].tool && h.argsHash === lastN[0].argsHash)
      const cyclic = detectCyclicPattern(full)

      let reason: string | null = null
      let cyclePeriod: number | undefined
      if (identicalStreak) reason = "identical-args-streak"
      else if (sameToolStreak) reason = "same-tool-streak"
      else if (cyclic) {
        reason = "cyclic-pattern-repeat"
        cyclePeriod = cyclic.period
      }
      else if (total >= ABSOLUTE_CEILING) reason = "absolute-ceiling"

      // "checked" diagnostic checkpoint — only reachable for a subagent-
      // tracked, status:"completed" call that made it all the way through
      // history-building and all three repetition checks. Pairs with the
      // unconditional "observed" line above: an "observed" line for this
      // exact part.id with no matching "checked" line means something in
      // between (history push, commandKey, detectCyclicPattern) threw and
      // was never caught — the exception-swallowing question from the
      // mudblazor investigation, made checkable from now on without a
      // repeat DB dive.
      diagLog(diagPath, reportsDir, {
        stage: "checked",
        sessionID: part.sessionID,
        historyLength: full.length,
        sameToolStreak,
        identicalStreak,
        cyclicVerdict: cyclic,
        reason,
      })

      if (reason) {
        logAbort(logPath, reportsDir, {
          sessionID: part.sessionID,
          reason,
          history: [...state.history],
          totalToolCalls: total,
          ...(cyclePeriod !== undefined ? { cyclePeriod } : {}),
        })
        await client.session.abort({ path: { id: part.sessionID } })
        cleanup(part.sessionID)
      }
    },
  }
}
