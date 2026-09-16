#!/usr/bin/env node
/**
 * NordicBeesERP harness time accounting.
 *
 * The instrumentation already existed — nordicbees-orchestrator-timing.ts
 * records every orchestrator tool call with its own duration_ms plus the
 * gap_before_ms that preceded it, and nordicbees-harness-trace.ts records
 * compactions, injections, user messages and session status. What was
 * missing is the reader that turns those files into a per-day answer to
 * one question: where do the hours actually go?
 *
 * Each day's wall-clock span is split into:
 *   delegation  — task-tool calls (subagent work; what task-stats.jsonl sees)
 *   tools       — the orchestrator's own bash/read/grep/edit calls
 *   model       — gaps with no user message and no compaction inside them:
 *                 the model composing its next tool call. The real cost.
 *   compact     — gaps containing at least one session_compacted event
 *   waiting     — gaps containing a user message: the agent had finished and
 *                 was waiting for the human. NOT a harness cost.
 *   idle/other  — span minus the above: session boundaries and anything
 *                 outside a tracked tool call
 *
 * The model/compact/waiting split is the correction to the first version of
 * this script, which lumped all three into one "thinking" bucket and so
 * counted human lunch breaks as orchestrator overhead. A gap is attributed
 * by what harness-trace.jsonl shows happening inside its [start, end)
 * window for the same session; compaction wins over nothing, a user message
 * wins over everything (if the human was typing, the wall clock was theirs).
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

// ---------------------------------------------------------------------------
// Pass 1 — harness-trace: per-session event timestamps used to classify gaps
// ---------------------------------------------------------------------------

const userMsgs = new Map() // sessionID -> sorted number[]
const compactEvents = new Map() // sessionID -> sorted number[]
const dayCompactions = new Map()
const dayInjections = new Map()

const pushTo = (map, key, value) => {
  const arr = map.get(key)
  if (arr) arr.push(value)
  else map.set(key, [value])
}

for await (const r of lines(TRACE)) {
  if (typeof r.ts !== "string") continue
  const t = Date.parse(r.ts)
  if (Number.isNaN(t)) continue
  const day = r.ts.slice(0, 10)

  if (r.type === "session_compacted" || r.type === "compaction_part") {
    if (r.sessionID) pushTo(compactEvents, r.sessionID, t)
    if (r.type === "session_compacted") dayCompactions.set(day, (dayCompactions.get(day) || 0) + 1)
  } else if (r.type === "message_created" && r.role === "user") {
    if (r.sessionID) pushTo(userMsgs, r.sessionID, t)
  } else if (r.type === "autoresume_injection_suspected") {
    if (!dayInjections.has(day)) dayInjections.set(day, new Map())
    const m = dayInjections.get(day)
    m.set(r.source, (m.get(r.source) || 0) + 1)
  }
}
for (const arr of userMsgs.values()) arr.sort((a, b) => a - b)
for (const arr of compactEvents.values()) arr.sort((a, b) => a - b)

/** True when `arr` (sorted) has any value in [lo, hi). */
function hasInRange(arr, lo, hi) {
  if (!arr || arr.length === 0) return false
  let l = 0
  let r = arr.length
  while (l < r) {
    const mid = (l + r) >> 1
    if (arr[mid] < lo) l = mid + 1
    else r = mid
  }
  return l < arr.length && arr[l] < hi
}

// An auto-resume injection arrives as a user message but is the harness
// talking to itself, not the human. Those gaps are NOT "waiting" — they are
// harness overhead. Injections are recorded separately in harness-trace, so
// a user message that coincides with one (within this tolerance) is treated
// as machine-generated.
const INJECTION_TOLERANCE_MS = 2000
const injectionTimes = new Map() // sessionID -> sorted number[]
for await (const r of lines(TRACE)) {
  if (r.type !== "autoresume_injection_suspected") continue
  if (typeof r.ts !== "string" || !r.sessionID) continue
  const t = Date.parse(r.ts)
  if (!Number.isNaN(t)) pushTo(injectionTimes, r.sessionID, t)
}
for (const arr of injectionTimes.values()) arr.sort((a, b) => a - b)

// ---------------------------------------------------------------------------
// Pass 2 — orchestrator-timing
// ---------------------------------------------------------------------------

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
      modelMs: 0,
      modelN: 0,
      compactMs: 0,
      compactN: 0,
      waitMs: 0,
      waitN: 0,
      dropped: 0,
      byTool: new Map(),
      modelByNextTool: new Map(),
      gaps: [],
    })
  }
  return days.get(d)
}

for await (const r of lines(TIMING)) {
  if (typeof r.ts !== "string") continue
  if (r.status === "started") continue // the paired "completed" line carries the numbers
  const day = r.ts.slice(0, 10)
  const d = dayOf(day)
  const t = Date.parse(r.ts)
  if (Number.isNaN(t)) continue
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

  if (gap === null || gap <= 0 || gap > MAX_SANE_GAP_MS) continue

  // Reconstruct the gap window: the call started at (ts - duration), and the
  // gap ran for gap_before_ms up to that point.
  const gapEnd = t - (dur ?? 0)
  const gapStart = gapEnd - gap
  const sid = r.session_id

  const sawCompaction = hasInRange(compactEvents.get(sid), gapStart, gapEnd)
  const rawUserMsg = hasInRange(userMsgs.get(sid), gapStart, gapEnd)
  const sawInjection = hasInRange(
    injectionTimes.get(sid),
    gapStart - INJECTION_TOLERANCE_MS,
    gapEnd + INJECTION_TOLERANCE_MS,
  )
  // A user message that is itself an auto-resume injection is the harness,
  // not the human — it does not make the gap "waiting".
  const sawHuman = rawUserMsg && !sawInjection

  let kind
  if (sawHuman) {
    kind = "wait"
    d.waitMs += gap
    d.waitN++
  } else if (sawCompaction) {
    kind = "compact"
    d.compactMs += gap
    d.compactN++
  } else {
    kind = "model"
    d.modelMs += gap
    d.modelN++
    d.modelByNextTool.set(r.tool, (d.modelByNextTool.get(r.tool) || 0) + gap)
  }

  d.gaps.push({ ts: r.ts, gap, tool: r.tool, kind, summary: r.args_summary || "" })
}

// ---------------------------------------------------------------------------
// Output
// ---------------------------------------------------------------------------

const sorted = [...days.keys()].sort()
const shown = ONE_DAY ? sorted.filter((d) => d === ONE_DAY) : sorted.slice(-DAYS)

if (shown.length === 0) {
  console.log("No data. Checked:\n  " + TIMING + "\n  " + TRACE)
  process.exit(0)
}

console.log("")
console.log("day           span  delegation      tools      model      compact     waiting     other  cmp  del")
console.log("-".repeat(104))

const tot = { span: 0, del: 0, tool: 0, model: 0, compact: 0, wait: 0 }
for (const key of shown) {
  const d = days.get(key)
  const span = d.last - d.first
  const other = Math.max(0, span - d.delegationMs - d.toolMs - d.modelMs - d.compactMs - d.waitMs)
  tot.span += span
  tot.del += d.delegationMs
  tot.tool += d.toolMs
  tot.model += d.modelMs
  tot.compact += d.compactMs
  tot.wait += d.waitMs
  console.log(
    `${key} ${fmt(span)} ${fmt(d.delegationMs)} ${pct(d.delegationMs, span)}` +
      ` ${fmt(d.toolMs)} ${pct(d.toolMs, span)}` +
      ` ${fmt(d.modelMs)} ${pct(d.modelMs, span)}` +
      ` ${fmt(d.compactMs)} ${pct(d.compactMs, span)}` +
      ` ${fmt(d.waitMs)} ${pct(d.waitMs, span)}` +
      ` ${fmt(other)} ${pct(other, span)}` +
      ` ${String(dayCompactions.get(key) || 0).padStart(4)} ${String(d.delegationN).padStart(4)}`,
  )
}
console.log("-".repeat(104))
const tOther = Math.max(0, tot.span - tot.del - tot.tool - tot.model - tot.compact - tot.wait)
console.log(
  `total      ${fmt(tot.span)} ${fmt(tot.del)} ${pct(tot.del, tot.span)}` +
    ` ${fmt(tot.tool)} ${pct(tot.tool, tot.span)}` +
    ` ${fmt(tot.model)} ${pct(tot.model, tot.span)}` +
    ` ${fmt(tot.compact)} ${pct(tot.compact, tot.span)}` +
    ` ${fmt(tot.wait)} ${pct(tot.wait, tot.span)}` +
    ` ${fmt(tOther)} ${pct(tOther, tot.span)}`,
)

const machine = tot.del + tot.tool + tot.model + tot.compact
if (machine > 0) {
  console.log(
    `\nExcluding human wait and idle, the harness itself spent ${fmt(machine).trim()}: ` +
      `${pct(tot.del, machine).trim()} delegation, ${pct(tot.tool, machine).trim()} tools, ` +
      `${pct(tot.model, machine).trim()} model, ${pct(tot.compact, machine).trim()} compaction.`,
  )
}

const droppedTotal = shown.reduce((s, k) => s + days.get(k).dropped, 0)
if (droppedTotal > 0) {
  console.log(`${droppedTotal} record(s) over the ${MAX_SANE_MS / 60000}min sanity cap excluded (unclosed calls).`)
}

if (ONE_DAY) {
  const d = days.get(ONE_DAY)
  const top = (map, n = 10) => [...map.entries()].sort((a, b) => b[1] - a[1]).slice(0, n)

  console.log(`\n-- ${ONE_DAY}: orchestrator's own tool time --`)
  for (const [tool, ms] of top(d.byTool)) console.log(`  ${fmt(ms)}  ${tool}`)

  console.log(`\n-- ${ONE_DAY}: model time, by which tool it preceded --`)
  for (const [tool, ms] of top(d.modelByNextTool)) console.log(`  ${fmt(ms)}  -> ${tool}`)

  const inj = dayInjections.get(ONE_DAY)
  if (inj && inj.size > 0) {
    console.log(`\n-- ${ONE_DAY}: auto-resume injections --`)
    for (const [src, n] of top(inj)) console.log(`  ${String(n).padStart(4)}  ${src}`)
  }
  console.log(
    `\n  compactions: ${dayCompactions.get(ONE_DAY) || 0}   delegations: ${d.delegationN}` +
      `   orchestrator tool calls: ${d.toolN}` +
      `   gaps: ${d.modelN} model / ${d.compactN} compaction / ${d.waitN} waiting`,
  )
}

if (GAPS_N > 0) {
  const all = shown.flatMap((k) => days.get(k).gaps)
  all.sort((a, b) => b.gap - a.gap)
  console.log(`\n-- ${GAPS_N} longest single gaps --`)
  for (const g of all.slice(0, GAPS_N)) {
    console.log(
      `  ${(g.gap / 1000).toFixed(0).padStart(5)}s  ${g.kind.padEnd(7)}  ${g.ts.slice(0, 19)}  ` +
        `${g.tool}  ${g.summary.slice(0, 50)}`,
    )
  }
}

console.log("")
