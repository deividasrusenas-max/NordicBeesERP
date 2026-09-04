import type { Plugin } from "@opencode-ai/plugin"
import { appendFileSync, mkdirSync, existsSync, readFileSync } from "fs"
import { join } from "path"

/**
 * NordicBeesERP quality/performance monitoring hook.
 *
 * Purely deterministic (no LLM judge, no external service) — records
 * coder/fixer/reviewer Task-tool calls to
 * .opencode/reports/task-stats.jsonl, one line per lifecycle event:
 *   {"status":"started", ...}   written immediately when a call begins
 *   {"status":"completed", ...} written when it returns normally
 *   {"status":"interrupted", ...} written retroactively (see below) if a
 *                                  "started" record is never matched by a
 *                                  "completed" one (Ctrl+C, crash, killed
 *                                  process, stuck subagent, etc.)
 *
 * WHY disk-based, not just in-memory: if the whole OpenCode process is
 * killed (Ctrl+C on the CLI itself, not just a stuck subagent), ALL
 * in-memory state dies with it — no plugin code can run afterward to
 * "clean up". Writing the "started" record to disk immediately, and
 * sweeping for orphaned ones on every future call (even in a brand new
 * process/session), is the only way to eventually detect and record an
 * interruption instead of the call simply vanishing from the stats.
 *
 * This is EVENTUAL detection, not real-time: an interrupted call is only
 * marked as such the next time ANY coder/fixer/reviewer call starts
 * (could be seconds later, could be the next day). duration_sec for an
 * interrupted record is an approximation (time until detected), not the
 * true moment of interruption, which is unknowable.
 *
 * NOTE ON HOOK ARG SHAPE (bug fixed 2026-08-22): the OpenCode plugin API
 * puts the tool's call arguments in different places depending on the
 * hook: for "tool.execute.before" they live on the SECOND parameter
 * (conventionally named `output` in the official examples, confusingly,
 * since at "before" time nothing has executed yet — it really means
 * "the pre-execution tool-call info"). For "tool.execute.after" they are
 * on the FIRST parameter (`input.args`), alongside `input.tool`. Getting
 * this backwards means `args` is silently `undefined` and every early
 * return fires before any record is ever written — exactly what happened
 * originally, producing zero "started" records for hours. getArgs() below
 * checks both locations so this plugin keeps working even if OpenCode
 * ever standardizes the two hooks to match one shape.
 */

const STALE_MS = 10 * 60 * 1000 // 10 minutes with no "completed" match = assume interrupted

// Fast in-memory path for the common case (before/after in the same
// process). Sequential-only workflow (per orchestrator.md) means a
// simple FIFO queue per subagent type is sufficient here.
//
// nToolCalls: running count of ALL tool calls (read, edit, grep, task,
// bash, ...) that fire while this task call is in-flight — i.e. between
// its "tool.execute.before" and matching "tool.execute.after". Counted
// per task call_id, not globally. This is pure tracking only: there is
// no circuit breaker / auto-abort based on it.
const pendingStarts: Record<string, { callId: string; startedAt: number; nToolCalls: number }[]> = {}

/** Increments the in-flight tool-call counter for every active task call (if any). */
function countInFlightToolCall() {
  for (const queue of Object.values(pendingStarts)) {
    for (const entry of queue) {
      entry.nToolCalls += 1
    }
  }
}

const taskId = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`

const MODEL_NAMES: Record<string, string> = {
  coder: "Qwen3.8-27B (Coder, via llama-swap)",
  fixer: "Qwen3.6-35B-A3B (Fixer, via llama-swap)",
  reviewer: "Qwen3.6-35B-A3B (Reviewer, via llama-swap)",
}

/** Reads tool-call args from whichever of the two hook parameters actually has them. */
function getArgs(input: any, second: any): Record<string, unknown> | undefined {
  return (input && input.args) || (second && second.args) || undefined
}

function getToolName(input: any, second: any): string | undefined {
  return (input && input.tool) || (second && second.tool) || undefined
}

function extractVerdict(text: string): string | null {
  if (/\bAPPROVED\b/.test(text)) return "APPROVED"
  if (/\bREJECTED\b/.test(text)) return "REJECTED"
  return null
}

function extractGuardrailScore(text: string): number | null {
  // Tries several phrasings a model might use to report the
  // agent-guardrails score, since the exact wording isn't guaranteed:
  // "Score: 95/100", "score 95 / 100", "95 out of 100", "guardrail
  // score of 95/100", etc.
  const patterns = [
    /score\s*:?\s*(\d{1,3})\s*\/\s*100/i,
    /(\d{1,3})\s*\/\s*100/i,
    /(\d{1,3})\s+out\s+of\s+100/i,
  ]
  for (const re of patterns) {
    const m = text.match(re)
    if (m) {
      const n = parseInt(m[1], 10)
      if (Number.isFinite(n) && n >= 0 && n <= 100) return n
    }
  }
  return null
}

/**
 * Turns whatever shape a hook parameter's "output" value has (a plain
 * string, a nested {output: "..."} object, an array of content blocks,
 * or anything else) into a single searchable string. This is
 * deliberately crude (JSON.stringify + fallback) rather than assuming
 * one exact shape, because the precise structure isn't guaranteed and
 * guessing wrong here previously caused every extraction to silently
 * return nothing (verdict/guardrail_score always null) for hours.
 */
function toSearchableText(value: unknown): string {
  if (typeof value === "string") return value
  if (value === null || value === undefined) return ""
  try {
    return JSON.stringify(value)
  } catch {
    return String(value)
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

/**
 * Scans the tail of the log for "started" records with no matching
 * "completed" record, older than STALE_MS, and writes an "interrupted"
 * completion for each. Cheap: only reads/parses the last ~500 lines.
 */
function sweepStale(logPath: string, reportsDir: string) {
  let lines: string[]
  try {
    if (!existsSync(logPath)) return
    const content = readFileSync(logPath, "utf8")
    lines = content.split("\n").filter(Boolean).slice(-500)
  } catch {
    return
  }

  const completedCallIds = new Set<string>()
  const startedByCallId = new Map<string, Record<string, unknown>>()

  for (const line of lines) {
    let rec: Record<string, unknown>
    try {
      rec = JSON.parse(line)
    } catch {
      continue
    }
    const callId = rec.call_id as string | undefined
    if (!callId) continue
    if (rec.status === "completed" || rec.status === "interrupted") {
      completedCallIds.add(callId)
    } else if (rec.status === "started") {
      startedByCallId.set(callId, rec)
    }
  }

  const now = Date.now()
  for (const [callId, startedRec] of startedByCallId) {
    if (completedCallIds.has(callId)) continue
    const startedTs = Date.parse(startedRec.ts as string)
    if (!Number.isFinite(startedTs)) continue
    if (now - startedTs < STALE_MS) continue // still plausibly in-flight, don't flag yet

    // Per-tool-call events are not persisted to disk (only started/
    // completed/interrupted records are), so for a call whose process is
    // still alive the in-memory counter is the only source; if the
    // process died, that value is unknowable and we default to 0.
    const nToolCalls = pendingStarts[startedRec.agent as string]
      ?.find((e) => e.callId === callId)?.nToolCalls ?? 0

    appendRecord(logPath, reportsDir, {
      ts: new Date().toISOString(),
      task_id: startedRec.task_id,
      call_id: callId,
      agent: startedRec.agent,
      model: startedRec.model,
      status: "interrupted",
      duration_sec: Math.round((now - startedTs) / 100) / 10,
      n_toolcalls: nToolCalls,
      note: "no matching 'completed' record found within the stale window — likely Ctrl+C, crash, or a stuck subagent that never returned",
    })

    // Drop the entry so it is not counted again by a future sweep.
    const queue = pendingStarts[startedRec.agent as string]
    if (queue) {
      const idx = queue.findIndex((e) => e.callId === callId)
      if (idx !== -1) queue.splice(idx, 1)
    }
  }
}

export const NordicBeesQualityMonitor: Plugin = async ({ directory }) => {
  const reportsDir = join(directory, ".opencode", "reports")
  const logPath = join(reportsDir, "task-stats.jsonl")

  return {
    "tool.execute.before": async (input: any, second: any) => {
      const tool = getToolName(input, second)
      const args = getArgs(input, second)
      const subagent = args?.subagent_type as string | undefined

      let callId: string | undefined
      if (tool === "task" && subagent && MODEL_NAMES[subagent]) {
        // Best-effort cleanup of any orphaned calls from a previous
        // (possibly killed) process, before recording this new one.
        sweepStale(logPath, reportsDir)

        callId = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
        const startedAt = Date.now()
        ;(pendingStarts[subagent] ??= []).push({ callId, startedAt, nToolCalls: 0 })
      }

      // Count EVERY tool call (task or otherwise) that happens while at
      // least one task call is in-flight — the task's own start event is
      // counted toward its own total because the entry was just pushed.
      countInFlightToolCall()

      if (tool !== "task") return
      if (!subagent || !MODEL_NAMES[subagent]) return

      const promptText = (args?.prompt as string | undefined) ?? ""

      appendRecord(logPath, reportsDir, {
        ts: new Date().toISOString(),
        task_id: taskId,
        call_id: callId,
        agent: subagent,
        model: MODEL_NAMES[subagent],
        status: "started",
        prompt_chars: promptText.length,
      })
    },

    "tool.execute.after": async (input: any, second: any) => {
      const tool = getToolName(input, second)
      if (tool !== "task") return
      const args = getArgs(input, second)
      const subagent = args?.subagent_type as string | undefined
      if (!subagent || !MODEL_NAMES[subagent]) return

      const started = pendingStarts[subagent]?.shift()
      const durationSec = started ? Math.round((Date.now() - started.startedAt) / 100) / 10 : null
      const nToolCalls = started?.nToolCalls ?? 0

      // Search across every plausible location for the tool's textual
      // output, since the exact shape/parameter isn't guaranteed (see
      // toSearchableText's doc comment).
      const outputText = [
        toSearchableText(second?.output),
        toSearchableText(input?.output),
        toSearchableText(second),
      ].join(" ")

      const record: Record<string, unknown> = {
        ts: new Date().toISOString(),
        task_id: taskId,
        call_id: started?.callId ?? null,
        agent: subagent,
        model: MODEL_NAMES[subagent],
        status: "completed",
        duration_sec: durationSec,
        n_toolcalls: nToolCalls,
      }

      if (subagent === "reviewer") {
        record.verdict = extractVerdict(outputText)
      }
      if (subagent === "fixer") {
        record.guardrail_score = extractGuardrailScore(outputText)
      }

      appendRecord(logPath, reportsDir, record)
    },
  }
}
