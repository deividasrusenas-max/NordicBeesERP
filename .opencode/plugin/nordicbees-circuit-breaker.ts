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
 */

const SAME_TOOL_STREAK_THRESHOLD = 8
const IDENTICAL_CALL_THRESHOLD = 3
const ABSOLUTE_CEILING = 1000

type CallRecord = { tool: string; argsHash: string; callID: string }
type SessionState = { history: CallRecord[]; seenCallIDs: Set<string> }
const sessions = new Map<string, SessionState>()

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
      if (part.type !== "tool" || part.state?.status !== "completed") return

      // Scoping guard: never track/abort a session we didn't register as
      // a subagent above — this is what keeps the orchestrator's own
      // top-level session permanently out of reach of this plugin.
      if (!subagentSessions.has(part.sessionID)) return

      const state = sessions.get(part.sessionID) ?? { history: [], seenCallIDs: new Set() }
      sessions.set(part.sessionID, state)
      if (state.seenCallIDs.has(part.callID)) return // dedupe repeated update events
      state.seenCallIDs.add(part.callID)

      const argsHash = JSON.stringify(part.state?.input ?? {})
      state.history.push({ tool: part.tool, argsHash, callID: part.callID })
      if (state.history.length > SAME_TOOL_STREAK_THRESHOLD) state.history.shift()

      const total = state.seenCallIDs.size
      const last = state.history
      const sameToolStreak = last.length === SAME_TOOL_STREAK_THRESHOLD &&
        last.every(h => h.tool === last[0].tool)
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
