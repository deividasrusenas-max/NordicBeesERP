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

function cleanup(sessionID: string) {
  sessions.delete(sessionID)
  subagentSessions.delete(sessionID)
}

function logAbort(
  logPath: string,
  reportsDir: string,
  record: { sessionID: string; reason: string; history: CallRecord[]; totalToolCalls: number },
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
      if (part.type === "text" && part.state?.status === "completed") {
        const text: string = typeof part.text === "string" ? part.text : (part.state?.text ?? "")
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
      if (state.history.length > SAME_TOOL_STREAK_THRESHOLD) state.history.shift()

      const total = state.seenCallIDs.size
      const last = state.history
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
      const lastN = last.slice(-IDENTICAL_CALL_THRESHOLD)
      const identicalStreak = lastN.length === IDENTICAL_CALL_THRESHOLD &&
        lastN.every(h => h.tool === lastN[0].tool && h.argsHash === lastN[0].argsHash)

      let reason: string | null = null
      if (identicalStreak) reason = "identical-args-streak"
      else if (sameToolStreak) reason = "same-tool-streak"
      else if (total >= ABSOLUTE_CEILING) reason = "absolute-ceiling"

      if (reason) {
        logAbort(logPath, reportsDir, {
          sessionID: part.sessionID,
          reason,
          history: [...state.history],
          totalToolCalls: total,
        })
        await client.session.abort({ path: { id: part.sessionID } })
        cleanup(part.sessionID)
      }
    },
  }
}
