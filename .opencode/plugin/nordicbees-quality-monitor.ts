import type { Plugin } from "@opencode-ai/plugin"
import { appendFileSync, mkdirSync, existsSync } from "fs"
import { join } from "path"

/**
 * NordicBeesERP quality/performance monitoring hook.
 *
 * Purely deterministic (no LLM judge, no external service) — records one
 * JSON line per coder/fixer/reviewer Task-tool call to
 * .opencode/reports/task-stats.jsonl:
 *   - which agent + which underlying model
 *   - how long the call took
 *   - how large the delegation prompt was (rough complexity proxy)
 *   - reviewer verdict (APPROVED/REJECTED), if a reviewer call
 *   - agent-guardrails numeric score, if a fixer call reports one
 *
 * This exists to build up a real, automatically-collected history for
 * later trend analysis (per-agent speed over time, reviewer reject rate,
 * guardrail score trend) without any manual logging by the user.
 */

// Correlates tool.execute.before -> tool.execute.after per subagent type.
// Sequential-only workflow (per orchestrator.md) means a simple FIFO
// queue per subagent type is sufficient — no concurrent calls to the same
// subagent are expected.
const pendingStarts: Record<string, number[]> = {}

// One task_id per plugin lifetime (~ one orchestrator session). Not a
// perfect grouping of "one user request" but a reasonable approximation
// without relying on undocumented session-id fields.
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

export const NordicBeesQualityMonitor: Plugin = async ({ directory }) => {
  const reportsDir = join(directory, ".opencode", "reports")
  const logPath = join(reportsDir, "task-stats.jsonl")

  return {
    "tool.execute.before": async (input) => {
      if (input.tool !== "task") return
      const subagent = (input as any).args?.subagent_type as string | undefined
      if (!subagent || !MODEL_NAMES[subagent]) return
      ;(pendingStarts[subagent] ??= []).push(Date.now())
    },

    "tool.execute.after": async (input, output) => {
      if (input.tool !== "task") return
      const subagent = (input as any).args?.subagent_type as string | undefined
      if (!subagent || !MODEL_NAMES[subagent]) return

      const startedAt = pendingStarts[subagent]?.shift()
      const durationSec = startedAt ? Math.round((Date.now() - startedAt) / 100) / 10 : null

      const promptText = ((input as any).args?.prompt as string | undefined) ?? ""
      const outputText = (output && typeof output === "object" ? (output as any).output : "") ?? ""

      const record: Record<string, unknown> = {
        ts: new Date().toISOString(),
        task_id: taskId,
        agent: subagent,
        model: MODEL_NAMES[subagent],
        duration_sec: durationSec,
        prompt_chars: promptText.length,
      }

      if (subagent === "reviewer") {
        record.verdict = extractVerdict(outputText)
      }
      if (subagent === "fixer") {
        record.guardrail_score = extractGuardrailScore(outputText)
      }

      try {
        if (!existsSync(reportsDir)) mkdirSync(reportsDir, { recursive: true })
        appendFileSync(logPath, JSON.stringify(record) + "\n", "utf8")
      } catch {
        // Never let logging failure break the actual task — silently skip.
      }
    },
  }
}
