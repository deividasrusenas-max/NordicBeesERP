#!/usr/bin/env node
/**
 * NordicBeesERP context-window usage from harness-trace.jsonl.
 *
 * Answers, from real sessions rather than from the config file:
 *   - how much context the orchestrator ACTUALLY reaches before compaction
 *   - whether compaction is window-bound (peak sits just under the limit)
 *     or triggered by something else (peak well below it)
 *   - how much of each turn's output is reasoning (the thinking-mode cost)
 *   - how fast context grows per assistant turn, i.e. how many turns a
 *     bigger window would actually buy
 *
 * The orchestrator is identified as a top-level session (session_created
 * with no parentID); subagent sessions are reported separately since they
 * live one delegation and never compact.
 *
 * Usage:
 *   node .opencode/plugin-dev/analyze-context.mjs
 *   node .opencode/plugin-dev/analyze-context.mjs --days 30
 *   node .opencode/plugin-dev/analyze-context.mjs --limit 131072
 *
 * --limit is the orchestrator's configured context (opencode.json
 * provider.llama-swap.models.orchestrator.limit.context) and is only used
 * to print how close the observed peaks come to it.
 *
 * Read-only.
 */

import { createReadStream, existsSync } from "fs"
import { createInterface } from "readline"
import { join, dirname } from "path"
import { fileURLToPath } from "url"

const ROOT = join(dirname(fileURLToPath(import.meta.url)), "..", "..")
const TRACE = join(ROOT, ".opencode", "reports", "harness-trace.jsonl")

const args = process.argv.slice(2)
const flag = (n, d) => {
  const i = args.indexOf(`--${n}`)
  return i >= 0 && args[i + 1] ? args[i + 1] : d
}
const DAYS = Number(flag("days", 14))
const LIMIT = Number(flag("limit", 131072))

async function* lines(path) {
  if (!existsSync(path)) return
  const rl = createInterface({ input: createReadStream(path), crlfDelay: Infinity })
  for await (const line of rl) {
    if (!line.trim()) continue
    try {
      yield JSON.parse(line)
    } catch {
      /* skip */
    }
  }
}

const k = (n) => (n == null ? "    -" : `${Math.round(n / 1000)}k`.padStart(6))
const num = (n) => String(n).padStart(6)
const quant = (arr, q) => {
  if (arr.length === 0) return null
  const s = [...arr].sort((a, b) => a - b)
  return s[Math.min(s.length - 1, Math.floor(s.length * q))]
}

const isSubagent = new Map() // sessionID -> bool
const days = new Map()
const dayOf = (d) => {
  if (!days.has(d)) {
    days.set(d, {
      orchInputs: [], // tokensInput per assistant turn, top-level sessions
      subInputs: [],
      reasoning: 0,
      output: 0,
      turns: 0,
      compactions: 0,
      atCompaction: [], // last observed tokensInput before each compaction
      sessions: new Set(),
    })
  }
  return days.get(d)
}

// Last seen tokensInput per session, so a compaction event can be attributed
// to the context size that triggered it.
const lastInput = new Map()
// Consecutive input deltas per session, to estimate growth per turn.
const growth = []
const prevInput = new Map()

for await (const r of lines(TRACE)) {
  if (typeof r.ts !== "string") continue
  const day = r.ts.slice(0, 10)

  if (r.type === "session_created") {
    if (r.sessionID) isSubagent.set(r.sessionID, !!r.isSubagentAtCreation)
    continue
  }

  if (r.type === "message_updated") {
    const sid = r.sessionID
    const input = typeof r.tokensInput === "number" ? r.tokensInput : null
    if (!sid || input === null || input === 0) continue
    const d = dayOf(day)
    const sub = isSubagent.get(sid)
    d.sessions.add(sid)
    d.turns++
    if (typeof r.tokensReasoning === "number") d.reasoning += r.tokensReasoning
    if (typeof r.tokensOutput === "number") d.output += r.tokensOutput

    if (sub === false) {
      d.orchInputs.push(input)
      const prev = prevInput.get(sid)
      // Only count forward growth; a drop means a compaction happened.
      if (prev !== undefined && input > prev) growth.push(input - prev)
      prevInput.set(sid, input)
    } else if (sub === true) {
      d.subInputs.push(input)
    }
    lastInput.set(sid, input)
    continue
  }

  if (r.type === "session_compacted") {
    const d = dayOf(day)
    d.compactions++
    const at = lastInput.get(r.sessionID)
    if (typeof at === "number") d.atCompaction.push(at)
    prevInput.delete(r.sessionID)
  }
}

const sorted = [...days.keys()].sort().slice(-DAYS)
if (sorted.length === 0) {
  console.log("No data in " + TRACE)
  process.exit(0)
}

console.log("")
console.log("Orchestrator context per assistant turn (tokensInput), top-level sessions only")
console.log("")
console.log("day          turns    med    p90    max   | at compaction: n    med    max | reasoning")
console.log("-".repeat(96))

const allOrch = []
const allAtCompaction = []
let totReasoning = 0
let totOutput = 0

for (const day of sorted) {
  const d = days.get(day)
  allOrch.push(...d.orchInputs)
  allAtCompaction.push(...d.atCompaction)
  totReasoning += d.reasoning
  totOutput += d.output
  const rShare = d.output > 0 ? `${Math.round((d.reasoning / d.output) * 100)}%` : "-"
  console.log(
    `${day} ${num(d.orchInputs.length)} ${k(quant(d.orchInputs, 0.5))} ${k(quant(d.orchInputs, 0.9))}` +
      ` ${k(d.orchInputs.length ? Math.max(...d.orchInputs) : null)}   |` +
      ` ${num(d.compactions)} ${k(quant(d.atCompaction, 0.5))} ${k(d.atCompaction.length ? Math.max(...d.atCompaction) : null)} |` +
      ` ${String(rShare).padStart(6)}`,
  )
}
console.log("-".repeat(96))

const peak = allOrch.length ? Math.max(...allOrch) : 0
const medCompact = quant(allAtCompaction, 0.5)
console.log("")
console.log(`Configured orchestrator context : ${LIMIT.toLocaleString()} tokens`)
console.log(`Observed peak input             : ${peak.toLocaleString()} (${Math.round((peak / LIMIT) * 100)}% of limit)`)
if (medCompact !== null) {
  console.log(
    `Median context at compaction    : ${medCompact.toLocaleString()} (${Math.round((medCompact / LIMIT) * 100)}% of limit)`,
  )
}
if (allOrch.length) {
  console.log(`Median turn context             : ${quant(allOrch, 0.5).toLocaleString()}`)
  console.log(`p90 turn context                : ${quant(allOrch, 0.9).toLocaleString()}`)
}
if (totOutput > 0) {
  console.log(
    `Reasoning share of output       : ${Math.round((totReasoning / totOutput) * 100)}%` +
      ` (${Math.round(totReasoning / 1000)}k reasoning / ${Math.round(totOutput / 1000)}k output tokens)`,
  )
}
if (growth.length) {
  const g = quant(growth, 0.5)
  console.log(`Median context growth per turn  : ${g.toLocaleString()} tokens`)
  if (medCompact !== null && g > 0) {
    console.log("")
    console.log("Turns gained by a bigger window (at the observed growth rate):")
    for (const size of [131072, 196608, 262144, 393216, 524288]) {
      const usable = size * (medCompact / LIMIT)
      console.log(`  ${String(Math.round(size / 1024) + "k").padStart(5)} context -> ~${Math.round(usable / g)} turns between compactions`)
    }
  }
}

console.log("")
console.log("Interpretation: if the peak sits just under the limit, compaction is")
console.log("window-bound and more context directly buys more turns. If the peak is")
console.log("well below the limit, something else is forcing compaction and a bigger")
console.log("window buys nothing.")
console.log("")
