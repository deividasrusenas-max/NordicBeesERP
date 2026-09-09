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

      pending.set(`${sessionID}:${callID}`, {
        tool,
        startedAt: now,
        argsSummary: summarizeArgs(tool, args),
        promptChars,
        skillsInjected,
      })
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
      if (entry.argsSummary !== undefined) record.args_summary = entry.argsSummary
      if (entry.promptChars !== undefined) record.prompt_chars = entry.promptChars
      if (entry.skillsInjected !== undefined) record.skills_injected = entry.skillsInjected

      appendRecord(logPath, reportsDir, record)
    },
  }
}
