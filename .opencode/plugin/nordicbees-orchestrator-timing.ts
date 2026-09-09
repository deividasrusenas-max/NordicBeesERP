import type { Plugin } from "@opencode-ai/plugin"
import { appendFileSync, mkdirSync, existsSync } from "fs"
import { join } from "path"

/**
 * NordicBeesERP orchestrator-level timing instrumentation.
 *
 * Problem this solves: .opencode/reports/task-stats.jsonl only records
 * subagent Task-tool start/end, so the 8-111s gaps BETWEEN delegations are
 * invisible — there is no way to tell which intermediate step (a
 * mempalace_search call, a git status/log check, a dotnet build
 * verification, skill-injection, or just the model composing its next
 * message) actually burns that time. This plugin records EVERY tool call
 * in the orchestrator's own top-level session (not subagent sessions,
 * which already get start/end granularity from task-stats.jsonl) with its
 * own duration plus the gap that preceded it.
 *
 * Purely deterministic (no LLM judge), disk-based JSONL under
 * .opencode/reports/ — same conventions as nordicbees-quality-monitor.ts,
 * which this file does NOT modify or share state with (separate plugin,
 * separate output file, additive only).
 *
 * SCOPE: only sessions with no parentID (the orchestrator's own top-level
 * session), learned via the "session.created" event — same proven pattern
 * nordicbees-circuit-breaker.ts already uses for the opposite scoping
 * problem. Subagent sessions (coder/fixer/reviewer) are deliberately
 * excluded here; their own tool-call timing isn't what's missing.
 *
 * "task" CALLS GET A "started" RECORD TOO (2026-09-09 fix): every other
 * tool writes exactly one line, on "after" — fine for calls lasting
 * milliseconds to a few seconds. A real fixer delegation once ran 8+
 * minutes and looped; this file recorded nothing about it at all, because
 * a call that never reaches "after" left no trace, which is exactly the
 * case this instrumentation exists to diagnose. task-stats.jsonl already
 * solves this correctly for subagent calls with its own started/completed
 * split, so "task" calls here get the same treatment: a {"status":
 * "started"} line at "before" (ts, session_id, call_id, tool,
 * args_summary, prompt_chars, skills_injected), then the existing line at
 * "after" gains {"status":"completed"}. Every other tool's "after" line
 * is completely unchanged — no "status" field appears on non-task lines,
 * so anything already parsing this file for those isn't affected. No
 * retroactive "interrupted" record is written for a "started" line that
 * never gets its "completed" match (unlike task-stats.jsonl's stale
 * sweep) — that's deliberately out of scope here; a reader can already
 * tell an in-flight-forever call apart from a normal one by the absence
 * of a matching call_id with status "completed".
 *
 * NOTE ON HOOK ARG SHAPE (same gotcha as nordicbees-quality-monitor.ts,
 * verified independently against the installed @opencode-ai/plugin type
 * defs, not just copied from that file's comment): "tool.execute.before"
 * has {tool,sessionID,callID} on the FIRST parameter and {args} on the
 * SECOND. "tool.execute.after" has {tool,sessionID,callID,args} all on
 * the FIRST parameter. getArgs()/getToolName() check both locations so
 * this keeps working even if that shape ever changes.
 *
 * HONEST LIMITATION: gap_before_ms cannot be split into "time spent in
 * nordicbees-skill-inject.ts's own hook" vs. "time the model spent
 * composing its next tool call" vs. anything else more granular — both
 * happen synchronously between the previous tool's "after" event and this
 * tool's "before" event, and this plugin has no way to observe or control
 * execution order relative to other plugins registered for the same
 * event. What IS observable, and exactly why prompt_chars/skills_injected
 * exist below: for "task" calls specifically, this plugin reads whatever
 * text is in the delegation's text field AT THE MOMENT its own
 * "tool.execute.before" hook fires. If nordicbees-skill-inject.ts's own
 * "tool.execute.before" hook already ran and mutated that text by the
 * time this one fires, prompt_chars reflects the POST-injection size and
 * skills_injected is true. If it hasn't run yet (or doesn't run this
 * call), prompt_chars reflects the PRE-injection size and skills_injected
 * is false. Which of these is actually true for this OpenCode install is
 * NOT assumed here — it is recorded per-call via skills_injected, so the
 * resulting file is self-documenting about which case it observed rather
 * than presenting a number without knowing what it measures.
 */

// Entries whose matching "after" event never arrives (an aborted call, a
// circuit-breaker abort — both real, observed occurrences in this harness)
// would otherwise leak in this map forever. Pruned on every new "before"
// call. Unlike nordicbees-quality-monitor.ts's STALE_MS sweep, this does
// NOT write a retroactive record — it only discards, since the point here
// is lightweight timing visibility, not crash/interruption accounting
// (task-stats.jsonl already owns that, for the much longer-running
// subagent-call case).
const STALE_MS = 15 * 60 * 1000

const SKILL_INJECTION_MARKER = "IMPORTANT: the following skill(s) are force-injected"

const topLevelSessions = new Set<string>()

type PendingCall = {
  tool: string
  startedAt: number
  argsSummary: string | undefined
  promptChars: number | undefined
  skillsInjected: boolean | undefined
}

// Keyed by `${sessionID}:${callID}`.
const pending = new Map<string, PendingCall>()

// Per-session timestamp of the last completed tool call, for computing
// the gap before the NEXT one. No entry yet = this session's first
// tracked call, so gap_before_ms is honestly null (no reliable baseline
// without also hooking session-creation timing, deliberately not added).
const lastEventEnd = new Map<string, number>()

function getArgs(input: any, second: any): Record<string, unknown> | undefined {
  return (input && input.args) || (second && second.args) || undefined
}

function getToolName(input: any, second: any): string | undefined {
  return (input && input.tool) || (second && second.tool) || undefined
}

/** Same dynamic field lookup nordicbees-skill-inject.ts itself uses to find
 * the delegation's text field — not hardcoded to "prompt", since that
 * plugin doesn't hardcode it either. */
function findTextField(args: Record<string, unknown> | undefined): string | undefined {
  if (!args) return undefined
  return ["prompt", "description", "task", "message"].find((f) => typeof args[f] === "string")
}

function summarizeArgs(tool: string, args: Record<string, unknown> | undefined): string | undefined {
  if (!args) return undefined
  if (tool === "bash" && typeof args.command === "string") {
    return (args.command as string).slice(0, 100)
  }
  if (tool === "task") {
    const subagent = args.subagent_type as string | undefined
    const description = args.description as string | undefined
    const summary = [subagent, description].filter(Boolean).join(": ")
    return summary.length > 0 ? summary : undefined
  }
  return undefined
}

function pruneStale(now: number) {
  for (const [key, entry] of pending) {
    if (now - entry.startedAt > STALE_MS) pending.delete(key)
  }
}

function appendRecord(logPath: string, reportsDir: string, record: Record<string, unknown>) {
  try {
    if (!existsSync(reportsDir)) mkdirSync(reportsDir, { recursive: true })
    appendFileSync(logPath, JSON.stringify(record) + "\n", "utf8")
  } catch {
    // Never let logging failure break the actual task — silently skip.
  }
}

export const NordicBeesOrchestratorTiming: Plugin = async ({ directory }) => {
  const reportsDir = join(directory, ".opencode", "reports")
  const logPath = join(reportsDir, "orchestrator-timing.jsonl")

  return {
    event: async ({ event }: any) => {
      if (event?.type !== "session.created") return
      const info = event.properties?.info
      if (info && !info.parentID) topLevelSessions.add(info.id)
    },

    "tool.execute.before": async (input: any, second: any) => {
      const sessionID = input?.sessionID as string | undefined
      const callID = input?.callID as string | undefined
      if (!sessionID || !callID) return
      if (!topLevelSessions.has(sessionID)) return

      const now = Date.now()
      pruneStale(now)

      const tool = getToolName(input, second) ?? "unknown"
      const args = getArgs(input, second)

      let promptChars: number | undefined
      let skillsInjected: boolean | undefined
      if (tool === "task") {
        const textField = findTextField(args)
        const promptText = textField ? (args?.[textField] as string) : ""
        promptChars = promptText.length
        skillsInjected = promptText.startsWith(SKILL_INJECTION_MARKER)
      }

      const argsSummary = summarizeArgs(tool, args)

      pending.set(`${sessionID}:${callID}`, {
        tool,
        startedAt: now,
        argsSummary,
        promptChars,
        skillsInjected,
      })

      // "task" calls only: a real fixer delegation once ran 8+ minutes and
      // looped, and orchestrator-timing.jsonl recorded nothing about it,
      // because this plugin only ever wrote a line on "after" — a call
      // that never completes left no trace, which is exactly the case
      // this file exists to diagnose. task-stats.jsonl already handles
      // this correctly for subagent calls via its own "started" record;
      // mirrored here for "task" only, not every tool, so non-task lines
      // already written to this file keep their exact current shape (no
      // "status" field appearing where it never did before).
      if (tool === "task") {
        const startedRecord: Record<string, unknown> = {
          status: "started",
          ts: new Date().toISOString(),
          session_id: sessionID,
          call_id: callID,
          tool,
        }
        if (argsSummary !== undefined) startedRecord.args_summary = argsSummary
        if (promptChars !== undefined) startedRecord.prompt_chars = promptChars
        if (skillsInjected !== undefined) startedRecord.skills_injected = skillsInjected
        appendRecord(logPath, reportsDir, startedRecord)
      }
    },

    "tool.execute.after": async (input: any, second: any) => {
      const sessionID = input?.sessionID as string | undefined
      const callID = input?.callID as string | undefined
      if (!sessionID || !callID) return
      if (!topLevelSessions.has(sessionID)) return

      const key = `${sessionID}:${callID}`
      const entry = pending.get(key)
      pending.delete(key)
      if (!entry) return // pruned as stale, or no matching "before" was ever recorded

      const now = Date.now()
      const prevEnd = lastEventEnd.get(sessionID)
      const gapBeforeMs = prevEnd !== undefined ? entry.startedAt - prevEnd : null
      lastEventEnd.set(sessionID, now)

      const record: Record<string, unknown> = {
        ts: new Date().toISOString(),
        session_id: sessionID,
        call_id: callID,
        tool: entry.tool,
        gap_before_ms: gapBeforeMs,
        duration_ms: now - entry.startedAt,
      }
      // "status" only added for "task" (which now also gets a "started"
      // record above) — every other tool's line keeps its exact existing
      // shape, so anything already parsing this file for non-task lines
      // doesn't see a new field appear.
      if (entry.tool === "task") record.status = "completed"
      if (entry.argsSummary !== undefined) record.args_summary = entry.argsSummary
      if (entry.promptChars !== undefined) record.prompt_chars = entry.promptChars
      if (entry.skillsInjected !== undefined) record.skills_injected = entry.skillsInjected

      appendRecord(logPath, reportsDir, record)
    },
  }
}
