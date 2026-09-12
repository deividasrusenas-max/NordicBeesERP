#!/usr/bin/env bun
/**
 * NordicBeesERP harness-trace analyzer — Phase 0 evidence instrumentation.
 *
 * Reads the JSONL trace written by nordicbees-harness-trace.ts and prints a
 * plain-text report, per day and in total. This is the tool that turns
 * .opencode/reports/harness-trace.jsonl into the empirical basis for
 * whatever timeout/threshold values a later specification session chooses
 * for the auto-resume replacement — every number that will later inform a
 * timeout is printed as a full distribution (p50/p90/p99/p99.9/max), never
 * as a mean, per that task's own explicit requirement.
 *
 * Usage: bun .opencode/plugin-dev/analyze-trace.ts [path-to-jsonl]
 * Defaults to .opencode/reports/harness-trace.jsonl next to this script.
 *
 * No dependencies. Never throws on a missing/tiny/malformed file — prints a
 * clear "no data" message instead, since this needs to run safely against a
 * brand new, nearly-empty trace right after Task 7's smoke test.
 */

import { existsSync, readFileSync } from "fs"
import { join } from "path"

type Rec = Record<string, unknown> & { ts?: string; mono?: number; type?: string; sessionID?: string }

function parseArgsPath(): string {
  const arg = process.argv[2]
  if (arg) return arg
  // import.meta.dir is a Bun-specific global; this script only needs to run under `bun`.
  return join(import.meta.dir, "..", "reports", "harness-trace.jsonl")
}

function loadRecords(path: string): { records: Rec[]; malformedLines: number } {
  if (!existsSync(path)) {
    return { records: [], malformedLines: 0 }
  }
  const raw = readFileSync(path, "utf8")
  const lines = raw.split("\n").filter((l) => l.trim().length > 0)
  const records: Rec[] = []
  let malformedLines = 0
  for (const line of lines) {
    try {
      const parsed = JSON.parse(line)
      if (parsed && typeof parsed === "object") records.push(parsed as Rec)
      else malformedLines++
    } catch {
      malformedLines++
    }
  }
  return { records, malformedLines }
}

/** Nearest-rank percentile over an ASCENDING-sorted array. p in [0, 100]. */
function percentile(sortedAsc: number[], p: number): number | null {
  if (sortedAsc.length === 0) return null
  const idx = Math.min(sortedAsc.length - 1, Math.max(0, Math.floor((p / 100) * sortedAsc.length)))
  return sortedAsc[idx]
}

function distributionLine(label: string, values: number[]): string {
  if (values.length === 0) return `  ${label}: n=0 (no data)`
  const sorted = [...values].sort((a, b) => a - b)
  const p50 = percentile(sorted, 50)!
  const p90 = percentile(sorted, 90)!
  const p99 = percentile(sorted, 99)!
  const p999 = percentile(sorted, 99.9)!
  const max = sorted[sorted.length - 1]
  const fmt = (ms: number) => `${ms}ms (${(ms / 1000).toFixed(1)}s)`
  return `  ${label}: n=${values.length}  p50=${fmt(p50)}  p90=${fmt(p90)}  p99=${fmt(p99)}  p99.9=${fmt(p999)}  max=${fmt(max)}`
}

function dayKey(ts: string | undefined): string {
  if (typeof ts !== "string" || ts.length < 10) return "unknown-date"
  return ts.slice(0, 10)
}

type Group = { label: string; records: Rec[] }

function analyzeGroup(records: Rec[]): string[] {
  const lines: string[] = []

  // --- sessions created, top-level vs subagent ---
  const created = records.filter((r) => r.type === "session_created")
  const subCreated = created.filter((r) => r.isSubagentAtCreation === true)
  const topCreated = created.filter((r) => r.isSubagentAtCreation === false)
  lines.push(`Sessions created: total=${created.length}  top-level=${topCreated.length}  subagent=${subCreated.length}`)

  // --- delegations (subagent sessions) vs task_complete ---
  const subagentSessionIDs = new Set(subCreated.map((r) => r.sessionID).filter(Boolean) as string[])
  const toolAfterBySession = new Map<string, Rec[]>()
  for (const r of records) {
    if (r.type !== "tool_execute_after" || !r.sessionID) continue
    const arr = toolAfterBySession.get(r.sessionID) ?? []
    arr.push(r)
    toolAfterBySession.set(r.sessionID, arr)
  }
  let withTaskComplete = 0
  for (const sid of subagentSessionIDs) {
    const calls = toolAfterBySession.get(sid) ?? []
    if (calls.some((c) => c.tool === "task_complete")) withTaskComplete++
  }
  const withoutTaskComplete = subagentSessionIDs.size - withTaskComplete
  lines.push(
    `Delegations (subagent sessions): total=${subagentSessionIDs.size}  ` +
      `with task_complete=${withTaskComplete}  WITHOUT task_complete=${withoutTaskComplete}`,
  )

  // --- autoresume_injection_suspected, by source, and top-level leakage ---
  const injections = records.filter((r) => r.type === "autoresume_injection_suspected")
  const bySource = new Map<string, number>()
  let onTopLevel = 0
  for (const r of injections) {
    const src = typeof r.source === "string" ? r.source : "unknown"
    bySource.set(src, (bySource.get(src) ?? 0) + 1)
    if (r.isSubagentAtCreation === false) onTopLevel++
  }
  lines.push(`Injections suspected: total=${injections.length}`)
  for (const [src, count] of [...bySource.entries()].sort((a, b) => b[1] - a[1])) {
    lines.push(`    - ${src}: ${count}`)
  }
  const flag = onTopLevel > 0 ? "  <-- NONZERO: idle-injection patch may not be fully effective, see auto-resume-1.1.15-inventory.md section F" : ""
  lines.push(`  Injections landing on a TOP-LEVEL session: ${onTopLevel}${flag}`)

  // --- per-session event stream (all sessionID-bearing records, sorted by mono) ---
  const bySessionAll = new Map<string, Rec[]>()
  for (const r of records) {
    if (!r.sessionID || typeof r.mono !== "number") continue
    const arr = bySessionAll.get(r.sessionID) ?? []
    arr.push(r)
    bySessionAll.set(r.sessionID, arr)
  }
  for (const arr of bySessionAll.values()) arr.sort((a, b) => (a.mono as number) - (b.mono as number))

  // Inter-event gaps while a session is BUSY (session_status status:"busy"
  // until the next session_status status:"idle"/session_idle_event for that
  // same session) — the empirical basis for chunkTimeoutMs: this is exactly
  // the "session is busy but nothing has happened in a while" condition
  // chunkTimeoutMs is meant to bound.
  const streamingGaps: number[] = []
  // Longest fully silent gap per session, across its ENTIRE lifetime,
  // regardless of busy/idle status — the empirical basis for subagentWaitMs
  // (how long can a parent legitimately go quiet waiting on a subagent).
  const longestGapPerSession: number[] = []
  const toolCallsPerSession: number[] = []

  for (const [sid, arr] of bySessionAll.entries()) {
    let longestGap = 0
    let busy = false
    for (let i = 1; i < arr.length; i++) {
      const gap = (arr[i].mono as number) - (arr[i - 1].mono as number)
      if (gap > longestGap) longestGap = gap
      if (busy) streamingGaps.push(gap)
      const t = arr[i].type
      if (t === "session_status" && arr[i].status === "busy") busy = true
      else if (t === "session_status" && arr[i].status === "idle") busy = false
      else if (t === "session_idle_event") busy = false
    }
    if (arr.length >= 2) longestGapPerSession.push(longestGap)
    const toolCalls = arr.filter((r) => r.type === "tool_execute_before").length
    toolCallsPerSession.push(toolCalls)
  }

  lines.push(distributionLine("Inter-event gap while streaming/busy", streamingGaps))
  lines.push(distributionLine("Longest silent gap per session", longestGapPerSession))

  // --- tool calls per session ---
  if (toolCallsPerSession.length === 0) {
    lines.push(`  Tool calls per session: n=0 (no data)`)
  } else {
    const sorted = [...toolCallsPerSession].sort((a, b) => a - b)
    const p50 = percentile(sorted, 50)!
    const p95 = percentile(sorted, 95)!
    const max = sorted[sorted.length - 1]
    lines.push(`  Tool calls per session: n=${sorted.length}  p50=${p50}  p95=${p95}  max=${max}`)
  }

  return lines
}

function heartbeatGapReport(records: Rec[], thresholdMs: number): string[] {
  const heartbeats = records
    .filter((r) => r.type === "heartbeat" && typeof r.mono === "number")
    .sort((a, b) => (a.mono as number) - (b.mono as number))
  const lines: string[] = []
  if (heartbeats.length < 2) {
    lines.push(`  n=${heartbeats.length} heartbeat(s) recorded — not enough to compute gaps.`)
    return lines
  }
  let flagged = 0
  for (let i = 1; i < heartbeats.length; i++) {
    const gap = (heartbeats[i].mono as number) - (heartbeats[i - 1].mono as number)
    if (gap > thresholdMs) {
      flagged++
      lines.push(`  gap of ${(gap / 60000).toFixed(1)}min between ${heartbeats[i - 1].ts} and ${heartbeats[i].ts} (possible recorder downtime)`)
    }
  }
  if (flagged === 0) lines.push(`  none (${heartbeats.length} heartbeats checked, threshold ${thresholdMs / 60000}min)`)
  return lines
}

function main() {
  const path = parseArgsPath()
  const { records, malformedLines } = loadRecords(path)

  console.log(`NordicBeesERP harness-trace analysis`)
  console.log(`Source: ${path}`)
  console.log(`Records loaded: ${records.length}${malformedLines > 0 ? `  (${malformedLines} malformed lines skipped)` : ""}`)
  console.log("")

  if (records.length === 0) {
    console.log("No records to analyze. (This is expected for a brand-new or empty trace file.)")
    return
  }

  const byDay = new Map<string, Rec[]>()
  for (const r of records) {
    const key = dayKey(r.ts)
    const arr = byDay.get(key) ?? []
    arr.push(r)
    byDay.set(key, arr)
  }

  const days = [...byDay.keys()].sort()
  for (const day of days) {
    console.log(`=== ${day} ===`)
    for (const line of analyzeGroup(byDay.get(day)!)) console.log(line)
    console.log(`  Heartbeat gaps > 12min:`)
    for (const line of heartbeatGapReport(byDay.get(day)!, 12 * 60_000)) console.log(`  ${line}`)
    console.log("")
  }

  console.log(`=== TOTAL (${days.length} day${days.length === 1 ? "" : "s"}) ===`)
  for (const line of analyzeGroup(records)) console.log(line)
  console.log(`  Heartbeat gaps > 12min (recorder downtime), across the whole file:`)
  for (const line of heartbeatGapReport(records, 12 * 60_000)) console.log(`  ${line}`)
}

main()
