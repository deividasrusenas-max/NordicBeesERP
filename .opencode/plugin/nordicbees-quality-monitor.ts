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
 */

const STALE_MS = 10 * 60 * 1000 // 10 minutes with no "completed" match = assume interrupted

// Fast in-memory path for the common case (before/after in the same
// process). Sequential-only workflow (per orchestrator.md) means a
// simple FIFO queue per subagent type is sufficient here.
const pendingStarts: Record<string, { callId: string; startedAt: number }[]> = {}

const taskId = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`

const MODEL_NAMES: Record<string, string> = {
  coder: "Qwen3.8-27B (Coder, via llama-swap)",
  fixer: "Qwen3.6-35B-A3B (Fixer, via llama-swap)",
  reviewer: "Qwen3.6-35B-A3B (Reviewer, via llama-swap)",
}

function extractVerdict(text: string): string | null {
  if (/\bAPPROVED\b/.test(text)) return "APPROVED"
  if (/\bREJECTED\b/.test(text)) return "REJECTED"
  return null
}

function extractGuardrailScore(text: string): number | null {
  const m = text.match(/Score:\s*(\d{1,3})\s*\/\s*100/i)
  if (!m) return null
  const n = parseInt(m[1], 10)
  return Number.isFinite(n) ? n : null
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

    appendRecord(logPath, reportsDir, {
      ts: new Date().toISOString(),
      task_id: startedRec.task_id,
      call_id: callId,
      agent: startedRec.agent,
      model: startedRec.model,
      status: "interrupted",
      duration_sec: Math.round((now - startedTs) / 100) / 10,
      note: "no matching 'completed' record found within the stale window — likely Ctrl+C, crash, or a stuck subagent that never returned",
    })
  }
}

export const NordicBeesQualityMonitor: Plugin = async ({ directory }) => {
  const reportsDir = join(directory, ".opencode", "reports")
  const logPath = join(reportsDir, "task-stats.jsonl")

  return {
    "tool.execute.before": async (input) => {
      if (input.tool !== "task") return
      const subagent = (input as any).args?.subagent_type as string | undefined
      if (!subagent || !MODEL_NAMES[subagent]) return

      // Best-effort cleanup of any orphaned calls from a previous
      // (possibly killed) process, before recording this new one.
      sweepStale(logPath, reportsDir)

      const callId = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
      const startedAt = Date.now()
      ;(pendingStarts[subagent] ??= []).push({ callId, startedAt })

      const promptText = ((input as any).args?.prompt as string | undefined) ?? ""

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

    "tool.execute.after": async (input, output) => {
      if (input.tool !== "task") return
      const subagent = (input as any).args?.subagent_type as string | undefined
      if (!subagent || !MODEL_NAMES[subagent]) return

      const started = pendingStarts[subagent]?.shift()
      const durationSec = started ? Math.round((Date.now() - started.startedAt) / 100) / 10 : null

      const outputText = (output && typeof output === "object" ? (output as any).output : "") ?? ""

      const record: Record<string, unknown> = {
        ts: new Date().toISOString(),
        task_id: taskId,
        call_id: started?.callId ?? null,
        agent: subagent,
        model: MODEL_NAMES[subagent],
        status: "completed",
        duration_sec: durationSec,
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
