#!/usr/bin/env node
/**
 * NordicBeesERP harness time accounting.
 *
 * The instrumentation already exists — nordicbees-orchestrator-timing.ts
 * records every orchestrator tool call with its own duration_ms plus the
 * gap_before_ms that preceded it, and nordicbees-harness-trace.ts records
 * compactions, injections and session status. What was missing is the
 * reader that turns those files into a per-day answer to one question:
 *
 *   where do the hours actually go?
 *
 * Splits each day into four buckets:
 *   delegation  — task-tool calls (subagent work; what task-stats.jsonl sees)
 *   tools       — the orchestrator's own bash/read/grep/edit calls
 *   thinking    — sum of gap_before_ms: model composing the next tool call,
 *                 plus skill-injection and any other in-between plugin work
 *                 (see that plugin's header for why this cannot be split
 *                 further from inside a plugin)
 *   idle/other  — span minus the above: session boundaries, human pauses,
 *                 and anything happening outside any tracked tool call
 *
 * Usage:
 *   node .opencode/plugin-dev/analyze-timing.mjs                 # last 14 days
 *   node .opencode/plugin-dev/analyze-timing.mjs --days 30
 *   node .opencode/plugin-dev/analyze-timing.mjs --day 2026-09-16
 *   node .opencode/plugin-dev/analyze-timing.mjs --gaps 20
 *
 * Read-only. Never writes to the reports directory.
 */

import { createReadStream, existsSync } from "fs"
import { createInterface } from "readline"
import { join, dirname } from "path"
import { fileURLToPath } from "url"

const ROOT = join(dirname(fileURLToPath(import.meta.url)), "..", "..")
const REPORTS = join(ROOT, ".opencode", "reports")
const TIMING = join(REPORTS, "orchestrator-timing.jsonl")
const TRACE = join(REPORTS, "harness-trace.jsonl")

// A single tool call longer than this is almost certainly an unclosed
// record, not real work. task-stats.jsonl's fixer max is 352224 s
// (98 hours) — any metric without this cap is fiction.
const MAX_SANE_MS = 60 * 60 * 1000 // 1 h
const MAX_SANE_GAP_MS = 60 * 60 * 1000

const args = process.argv.slice(2)
const flag = (name, fallback) => {
  const i = args.indexOf(`--${name}`)
  return i >= 0 && args[i + 1] ? args[i + 1] : fallback
}
const DAYS = Number(flag("days", 14))
const ONE_DAY = flag("day", null)
const GAPS_N = Number(flag("gaps", 0))

async function* lines(path) {
  if (!existsSync(path)) return
  const rl = createInterface({ input: createReadStream(path), crlfDelay: Infinity })
  for await (const line of rl) {
    if (!line.trim()) continue
    try {
      yield JSON.parse(line)
    } catch {
      /* skip malformed line */
    }
  }
}

const fmt = (ms) => {
  if (ms == null) return "     -"
  const m = ms / 60000
  return m >= 60 ? `${(m / 60).toFixed(1)}h`.padStart(6) : `${Math.round(m)}m`.padStart(6)
}
const pct = (part, whole) => (whole > 0 ? `${Math.round((part / whole) * 100)}%`.padStart(4) : "   -")

const days = new Map()
const dayOf = (d) => {
  if (!days.has(d)) {
    days.set(d, {
      first: null,
      last: null,
      delegationMs: 0,
      delegationN: 0,
      toolMs: 0,
      toolN: 0,
      thinkMs: 0,
      thinkN: 0,
      dropped: 0,
      byTool: new Map(),
      gapByNextTool: new Map(),
      compactions: 0,
      injections: new Map(),
      gaps: [],
    })
  }
  return days.get(d)
}

for await (const r of lines(TIMING)) {
  if (typeof r.ts !== "string") continue
  if (r.status === "started") continue // the paired "completed" line carries the numbers
  const d = dayOf(r.ts.slice(0, 10))
  const t = Date.parse(r.ts)
  if (d.first === null || t < d.first) d.first = t
  if (d.last === null || t > d.last) d.last = t

  const dur = typeof r.duration_ms === "number" ? r.duration_ms : null
  const gap = typeof r.gap_before_ms === "number" ? r.gap_before_ms : null

  if (dur !== null) {
    if (dur > MAX_SANE_MS) {
      d.dropped++
    } else if (r.tool === "task") {
      d.delegationMs += dur
      d.delegationN++
    } else {
      d.toolMs += dur
      d.toolN++
      d.byTool.set(r.tool, (d.byTool.get(r.tool) || 0) + dur)
    }
  }
  if (gap !== null && gap > 0 && gap <= MAX_SANE_GAP_MS) {
    d.thinkMs += gap
    d.thinkN++
    d.gapByNextTool.set(r.tool, (d.gapByNextTool.get(r.tool) || 0) + gap)
    d.gaps.push({ ts: r.ts, gap, tool: r.tool, summary: r.args_summary || "" })
  }
}

for await (const r of lines(TRACE)) {
  if (typeof r.ts !== "string") continue
  const key = r.ts.slice(0, 10)
  if (!days.has(key)) continue
  const d = days.get(key)
  if (r.type === "session_compacted") d.compactions++
  if (r.type === "autoresume_injection_suspected") {
    d.injections.set(r.source, (d.injections.get(r.source) || 0) + 1)
  }
}

const sorted = [...days.keys()].sort()
const shown = ONE_DAY ? sorted.filter((d) => d === ONE_DAY) : sorted.slice(-DAYS)

if (shown.length === 0) {
  console.log("No data. Checked:\n  " + TIMING + "\n  " + TRACE)
  process.exit(0)
}

console.log("")
console.log("day           span  delegation      tools     thinking   idle/other  cmp  del")
console.log("-".repeat(80))

let tSpan = 0
let tDel = 0
let tTool = 0
let tThink = 0
for (const key of shown) {
  const d = days.get(key)
  const span = d.last - d.first
  const other = Math.max(0, span - d.delegationMs - d.toolMs - d.thinkMs)
  tSpan += span
  tDel += d.delegationMs
  tTool += d.toolMs
  tThink += d.thinkMs
  console.log(
    `${key} ${fmt(span)} ${fmt(d.delegationMs)} ${pct(d.delegationMs, span)}` +
      ` ${fmt(d.toolMs)} ${pct(d.toolMs, span)}` +
      ` ${fmt(d.thinkMs)} ${pct(d.thinkMs, span)}` +
      ` ${fmt(other)} ${pct(other, span)}` +
      ` ${String(d.compactions).padStart(4)} ${String(d.delegationN).padStart(4)}`,
  )
}
console.log("-".repeat(80))
const tOther = Math.max(0, tSpan - tDel - tTool - tThink)
console.log(
  `total      ${fmt(tSpan)} ${fmt(tDel)} ${pct(tDel, tSpan)} ${fmt(tTool)} ${pct(tTool, tSpan)}` +
    ` ${fmt(tThink)} ${pct(tThink, tSpan)} ${fmt(tOther)} ${pct(tOther, tSpan)}`,
)

const droppedTotal = shown.reduce((s, k) => s + days.get(k).dropped, 0)
if (droppedTotal > 0) {
  console.log(`\n${droppedTotal} record(s) over the ${MAX_SANE_MS / 60000}min sanity cap excluded (unclosed calls).`)
}

if (ONE_DAY) {
  const d = days.get(ONE_DAY)
  const top = (map, n = 10) => [...map.entries()].sort((a, b) => b[1] - a[1]).slice(0, n)

  console.log(`\n-- ${ONE_DAY}: orchestrator's own tool time --`)
  for (const [tool, ms] of top(d.byTool)) console.log(`  ${fmt(ms)}  ${tool}`)

  console.log(`\n-- ${ONE_DAY}: thinking time, by which tool it preceded --`)
  for (const [tool, ms] of top(d.gapByNextTool)) console.log(`  ${fmt(ms)}  -> ${tool}`)

  if (d.injections.size > 0) {
    console.log(`\n-- ${ONE_DAY}: auto-resume injections --`)
    for (const [src, n] of top(d.injections)) console.log(`  ${String(n).padStart(4)}  ${src}`)
  }
  console.log(
    `\n  compactions: ${d.compactions}   delegations: ${d.delegationN}` +
      `   orchestrator tool calls: ${d.toolN}   tracked gaps: ${d.thinkN}`,
  )
}

if (GAPS_N > 0) {
  const all = shown.flatMap((k) => days.get(k).gaps)
  all.sort((a, b) => b.gap - a.gap)
  console.log(`\n-- ${GAPS_N} longest single gaps (model thinking between tool calls) --`)
  for (const g of all.slice(0, GAPS_N)) {
    console.log(
      `  ${(g.gap / 1000).toFixed(0).padStart(5)}s  ${g.ts.slice(0, 19)}  ${g.tool}  ${g.summary.slice(0, 60)}`,
    )
  }
}

console.log("")
