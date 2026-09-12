// ============================================================================
// GENERATED FILE — DO NOT EDIT DIRECTLY.
//
// This is a machine-generated copy of the plugin-dev source, with every
// export except the plugin binding itself stripped — opencode's plugin
// loader requires the loaded file's ONLY export to be the Plugin function
// (see that source file's own header for how this was discovered: a real
// smoke test failed with error="Plugin export is not a function" the first
// time this file additionally exported its pure-logic helpers).
//
// Source of truth: .opencode/plugin-dev/nordicbees-harness-trace.ts
// Regenerate with: bun .opencode/plugin-dev/build-plugin.ts
//   (or: npx -y bun .opencode/plugin-dev/build-plugin.ts, if bun is not on PATH)
//
// A test in .opencode/plugin-dev/nordicbees-harness-trace.test.ts asserts
// this file is byte-identical to the generator's current output. Editing
// this file directly, or editing the dev source without regenerating, will
// make that test fail until this file is regenerated again.
// ============================================================================

import type { Plugin } from "@opencode-ai/plugin"
import { appendFileSync, existsSync, mkdirSync, renameSync, statSync } from "fs"
import { join } from "path"
import { createHash } from "crypto"

/**
 * NordicBeesERP harness trace recorder — Phase 0 evidence instrumentation.
 *
 * Problem this solves: opencode-auto-resume@1.1.15 (the only harness
 * component not under our control) was the source of six bugs across
 * 2026-09-08..12, and every one of them was invisible from reading its
 * source — each only surfaced from real session data (see
 * .opencode/planning/auto-resume-1.1.15-inventory.md for the full read-through
 * that WAS possible, and note how much of section D/F/G there is phrased as
 * "I could not determine this by static reading alone"). This plugin is the
 * measuring instrument for the eventual replacement: it records what the
 * harness actually does, in production, so a later session can write that
 * replacement's specification against real traces instead of guesswork, and
 * build its test suite from real recorded sequences.
 *
 * STRICTLY READ-ONLY. This plugin must never inject text, abort a session,
 * or modify any hook's `output` parameter. Its only side effect is appending
 * lines to .opencode/reports/harness-trace.jsonl. Every hook body below is
 * wrapped in try/catch specifically so a bug in this recorder can never take
 * down or alter real harness behavior — on a caught error it writes one
 * `recorder_error` line and keeps going.
 *
 * API facts this file relies on, and where they were confirmed (see
 * .opencode/planning/plugin-api-surface.md for the full derivation, and
 * .opencode/planning/auto-resume-1.1.15-inventory.md for the vendor-source
 * findings referenced below):
 *   - `event: async ({ event }) => {...}` is the correct Hooks shape
 *     (@opencode-ai/plugin dist/index.d.ts:175-177), same as both
 *     nordicbees-circuit-breaker.ts and the vendor auto-resume snapshot use.
 *   - `Session.parentID` (types.gen.d.ts:469) is optional/absent — not an
 *     empty string — for a top-level session. Both the circuit-breaker
 *     plugin (line 355-356) and the vendor auto-resume snapshot (its own
 *     `w.isSubagent = typeof parentID === "string" && parentID.length > 0`)
 *     independently treat "non-empty string parentID" as "is a subagent";
 *     this file does the same for `isSubagentAtCreation`.
 *   - `TextPart` (types.gen.d.ts:142-157) has no `.state` field at all —
 *     confirmed directly from the type, and independently rediscovered the
 *     hard way by circuit-breaker.ts (comment at its own lines 415-423).
 *     This file never reads `.state` off a text part.
 *   - `EventSessionError.properties.sessionID` is OPTIONAL (types.gen.d.ts:518-524)
 *     — a session-less error is a real, typed possibility, handled below.
 *   - There is no `session.interrupted` Event type and no `"interrupted"`
 *     SessionStatus member in the currently-installed SDK types at all
 *     (see plugin-api-surface.md, "Important, verified negative finding").
 *     The vendor auto-resume snapshot nonetheless branches on both — this
 *     file does NOT special-case "interrupted" for that reason; if the live
 *     wire protocol ever does emit something along those lines, it would
 *     still arrive as a `session.status` or `session.error` event and be
 *     captured generically by the handlers below.
 *   - `session.status` (event type, with `properties.status.type` one of
 *     "idle"/"retry"/"busy") and `session.idle` (a SEPARATE, structurally
 *     distinct event type, `properties: {sessionID}` only) are BOTH real,
 *     separate Event union members. This file records them as two distinct
 *     record types (`session_status` and `session_idle_event`) deliberately
 *     — auto-resume-1.1.15-inventory.md section F found that the
 *     idle-injection patch only gates the `session.status` → `"idle"`
 *     branch's nudge scheduling, while a second, separate `case
 *     "session.idle":` handler in the same vendor file schedules the exact
 *     same nudges WITHOUT that gate, and it was not possible to determine by
 *     static reading whether opencode's runtime emits both events for the
 *     same transition or only one. Recording them as genuinely separate
 *     event types (rather than collapsing both into one "went idle" record)
 *     is what will let a later analysis answer that question from real data.
 *
 * PRIVACY: this file must never write message text, tool call arguments, or
 * file contents into the trace — only lengths, counts, and SHA-256 prefixes
 * (first 12 hex chars). The one deliberate exception is the injection
 * detector below (see `matchInjection`), which still only ever emits a hash
 * of the matched text, never the text itself.
 *
 * STANDING WARNING (same as nordicbees-circuit-breaker.ts's own header):
 * files under .opencode/plugin/ are harness infrastructure that the normal
 * orchestrator/coder/fixer workflow never commits on its own (orchestrator's
 * edit permissions don't cover .opencode/plugin/*), and this repo has
 * already lost such a file once to an unrelated git-tree-cleaning operation.
 * This file lives in .opencode/plugin-dev/ during development specifically
 * so it is NOT yet live — once copied into .opencode/plugin/ (Task 7 of the
 * Phase 0 task this file was built for), COMMIT IT MANUALLY.
 */

// ---------------------------------------------------------------------------
// Pure logic — no I/O, no opencode runtime dependency. Exported so the test
// file (nordicbees-harness-trace.test.ts) can exercise every branch directly.
// ---------------------------------------------------------------------------

const HASH_PREFIX_LEN = 12
const MAX_ROTATE_BYTES = 100 * 1024 * 1024 // 100 MB
const HEARTBEAT_INTERVAL_MS = 5 * 60_000 // 5 minutes
const TOOL_START_MAP_MAX = 5000

/** SHA-256 of `input`, truncated to the first `len` hex characters. */
function sha256Prefix(input: string, len: number = HASH_PREFIX_LEN): string {
  return createHash("sha256").update(input, "utf8").digest("hex").slice(0, len)
}

/**
 * Stable (key-order-independent) JSON stringification, for hashing tool
 * arguments. Mirrors the approach the vendor auto-resume snapshot's own
 * `toolCallSignature()` uses (sort object keys, recurse), so a given call's
 * hash is stable regardless of argument-object key order.
 */
function stableStringify(value: unknown): string {
  const go = (v: unknown): string => {
    if (v === null) return "null"
    if (v === undefined) return "undefined"
    if (typeof v !== "object") {
      try {
        return JSON.stringify(v) ?? String(v)
      } catch {
        return String(v)
      }
    }
    if (Array.isArray(v)) return "[" + v.map(go).join(",") + "]"
    const obj = v as Record<string, unknown>
    return (
      "{" +
      Object.keys(obj)
        .sort()
        .map((k) => JSON.stringify(k) + ":" + go(obj[k]))
        .join(",") +
      "}"
    )
  }
  try {
    return go(value)
  } catch {
    return "unserializable"
  }
}

/** SHA-256 prefix of a stable-stringified value (e.g. tool call args). */
function hashArgs(value: unknown): string {
  return sha256Prefix(stableStringify(value))
}

/**
 * Known auto-resume injection prompts (verbatim, from
 * .opencode/planning/auto-resume-1.1.15-inventory.md section B), reduced to
 * a distinctive matcher per prompt.
 *
 * Design note (why substring for some, exact-match for others): every one of
 * these is sent as the ENTIRE text of a new user message (auto-resume's
 * `sendContinuePrompt` always calls `session.prompt({body:{parts:[{type:
 * "text", text}]}})` with nothing else in the message) — so in principle
 * exact-match-after-trim would be correct for all of them. But the task
 * this file was built for explicitly asks for substring matching "since the
 * plugin may template them" (true for the open-todos reminder, which
 * interpolates the live todo list) and to avoid a long legitimate report
 * that merely contains similar wording being flagged. The long, fixed
 * prompts (tool-loop, tool-text-recovery, done-without-work,
 * done-without-details, subagent-recovery) each get a long (40+ char),
 * highly distinctive substring lifted from their own constant text, which
 * a real report is extremely unlikely to contain verbatim. The two
 * generic, single-word prompts (`continuePrompt` default "continue" and
 * `actionIntentPrompt`, which defaults to the same value) are NOT matched
 * by substring at all — "continue" appearing inside a real message proves
 * nothing — they are matched only by exact equality of the FULL trimmed
 * message text, which is what auto-resume actually sends for them.
 *
 * This table assumes auto-resume is running with its DEFAULT prompt option
 * values, which is true in this deployment today (opencode.json's
 * `opencode-auto-resume` options only set chunkTimeoutMs, gracePeriodMs,
 * maxRetries, subagentWaitMs — no *Prompt key is overridden; confirmed by
 * reading opencode.json directly, see auto-resume-1.1.15-inventory.md
 * section A). If those defaults are ever overridden in opencode.json, this
 * table would need updating — it cannot discover overridden prompt text on
 * its own.
 */
type InjectionSignature = { id: string; kind: "substring" | "exact"; pattern: string }

const INJECTION_SIGNATURES: readonly InjectionSignature[] = [
  { id: "tool-loop", kind: "substring", pattern: "step back and reassess your approach" },
  { id: "tool-text-recovery", kind: "substring", pattern: "raw tool call printed as text instead of being executed" },
  { id: "thinking-tool-recovery", kind: "substring", pattern: "tool call generated in your thinking/reasoning" },
  { id: "done-without-work", kind: "substring", pattern: "but no work was detected" },
  {
    id: "done-without-details",
    kind: "substring",
    pattern: "your ONLY acceptable response right now is this detailed report",
  },
  { id: "todo-reminder", kind: "substring", pattern: "Please continue working on" },
  { id: "subagent-recovery", kind: "substring", pattern: "may have stalled or timed out" },
  { id: "continue-generic", kind: "exact", pattern: "continue" },
]

/**
 * Checks `text` against every known injection signature. Returns the first
 * match (signature id + a hash of the ACTUAL matched text, not the raw
 * text itself — see the file header's privacy note) or null.
 *
 * A long legitimate report that happens to contain a short generic word
 * ("continue") must NOT match — that's why "continue-generic" is `kind:
 * "exact"` against the FULL trimmed text, not a substring test.
 */
function matchInjection(text: string): { id: string; promptHash: string } | null {
  if (typeof text !== "string" || text.length === 0) return null
  const trimmed = text.trim()
  for (const sig of INJECTION_SIGNATURES) {
    if (sig.kind === "substring") {
      if (text.includes(sig.pattern)) {
        return { id: sig.id, promptHash: sha256Prefix(text) }
      }
    } else {
      if (trimmed === sig.pattern) {
        return { id: sig.id, promptHash: sha256Prefix(trimmed) }
      }
    }
  }
  return null
}

/** True when the file at `sizeBytes` should be rotated before the next append. */
function shouldRotate(sizeBytes: number, maxBytes: number = MAX_ROTATE_BYTES): boolean {
  return sizeBytes > maxBytes
}

/** Base filename for a rotated-out trace file, given an ISO date (YYYY-MM-DD). */
function rotatedFileName(isoDate: string, suffix: number = 0): string {
  return suffix === 0 ? `harness-trace.${isoDate}.jsonl` : `harness-trace.${isoDate}-${suffix}.jsonl`
}

type BaseFields = { ts: string; mono: number; type: string; sessionID?: string }

/** JSON-serializes one trace record as a single line (no trailing newline is added here). */
function toLine(record: Record<string, unknown>): string {
  return JSON.stringify(record)
}

function base(type: string, nowIso: string, mono: number, sessionID?: string): BaseFields {
  return sessionID ? { ts: nowIso, mono, type, sessionID } : { ts: nowIso, mono, type }
}

/**
 * Maps one opencode `Event` object to a trace record, or null to skip an
 * event type this recorder doesn't track. Pure — no I/O, no mutation of
 * `event`. Defensive against malformed/partial events: every field access
 * is optional-chained, and an event with an unrecognized or missing `type`
 * simply yields null rather than throwing.
 *
 * Event-type-to-record mapping (see plugin-api-surface.md section 2 for the
 * full Event union this switches over):
 *   session.created      -> session_created      (parentID, isSubagentAtCreation)
 *   session.updated       -> null (not tracked; no analysis in Task 6 needs it)
 *   session.deleted       -> session_deleted
 *   session.status        -> session_status       (idle/retry/busy; retry's own
 *                            `message` field is free text from the provider and
 *                            is deliberately NOT copied into the trace)
 *   session.idle          -> session_idle_event    (see header note: kept
 *                            separate from session_status on purpose)
 *   session.compacted     -> session_compacted
 *   session.error         -> session_error         (error NAME only, an
 *                            enum-like tag, never `.data.message`)
 *   message.updated       -> message_updated, assistant messages only (role,
 *                            token counts, hasError, error name, finish reason
 *                            truncated defensively)
 *   message.part.updated  -> tool_part | text_part, depending on part.type
 *   everything else       -> null
 */
function eventToRecord(event: unknown, nowIso: string, mono: number): Record<string, unknown> | null {
  if (!event || typeof event !== "object") return null
  const ev = event as { type?: unknown; properties?: unknown }
  const type = ev.type
  if (typeof type !== "string") return null
  const props = (ev.properties ?? {}) as Record<string, unknown>

  switch (type) {
    case "session.created": {
      const info = props.info as { id?: unknown; parentID?: unknown } | undefined
      const sessionID = typeof info?.id === "string" ? info.id : undefined
      if (!sessionID) return null
      const parentID = typeof info?.parentID === "string" && info.parentID.length > 0 ? info.parentID : null
      return {
        ...base("session_created", nowIso, mono, sessionID),
        parentID,
        isSubagentAtCreation: parentID !== null,
      }
    }
    case "session.deleted": {
      const info = props.info as { id?: unknown } | undefined
      const sessionID = typeof info?.id === "string" ? info.id : undefined
      if (!sessionID) return null
      return base("session_deleted", nowIso, mono, sessionID)
    }
    case "session.status": {
      const sessionID = typeof props.sessionID === "string" ? props.sessionID : undefined
      if (!sessionID) return null
      const status = props.status as { type?: unknown; attempt?: unknown; next?: unknown } | undefined
      const statusType = typeof status?.type === "string" ? status.type : "unknown"
      const record: Record<string, unknown> = { ...base("session_status", nowIso, mono, sessionID), status: statusType }
      if (statusType === "retry") {
        if (typeof status?.attempt === "number") record.attempt = status.attempt
        if (typeof status?.next === "number") record.next = status.next
      }
      return record
    }
    case "session.idle": {
      const sessionID = typeof props.sessionID === "string" ? props.sessionID : undefined
      if (!sessionID) return null
      return base("session_idle_event", nowIso, mono, sessionID)
    }
    case "session.compacted": {
      const sessionID = typeof props.sessionID === "string" ? props.sessionID : undefined
      if (!sessionID) return null
      return base("session_compacted", nowIso, mono, sessionID)
    }
    case "session.error": {
      const sessionID = typeof props.sessionID === "string" ? props.sessionID : undefined
      const error = props.error as { name?: unknown; data?: { statusCode?: unknown } } | undefined
      const errorName = typeof error?.name === "string" ? error.name : "Unknown"
      const record: Record<string, unknown> = { ...base("session_error", nowIso, mono, sessionID), errorName }
      const statusCode = error?.data?.statusCode
      if (typeof statusCode === "number") record.statusCode = statusCode
      return record
    }
    case "message.updated": {
      const info = props.info as
        | {
            sessionID?: unknown
            role?: unknown
            error?: { name?: unknown } | undefined
            tokens?: { input?: unknown; output?: unknown; reasoning?: unknown; cache?: { read?: unknown; write?: unknown } }
            finish?: unknown
          }
        | undefined
      if (!info || info.role !== "assistant") return null
      const sessionID = typeof info.sessionID === "string" ? info.sessionID : undefined
      if (!sessionID) return null
      const record: Record<string, unknown> = base("message_updated", nowIso, mono, sessionID)
      record.hasError = !!info.error
      if (info.error && typeof info.error.name === "string") record.errorName = info.error.name
      const t = info.tokens
      if (t) {
        record.tokensInput = typeof t.input === "number" ? t.input : 0
        record.tokensOutput = typeof t.output === "number" ? t.output : 0
        record.tokensReasoning = typeof t.reasoning === "number" ? t.reasoning : 0
        record.tokensCacheRead = typeof t.cache?.read === "number" ? t.cache.read : 0
        record.tokensCacheWrite = typeof t.cache?.write === "number" ? t.cache.write : 0
      }
      if (typeof info.finish === "string") record.finish = info.finish.slice(0, 40)
      return record
    }
    case "message.part.updated": {
      const part = props.part as
        | {
            type?: unknown
            sessionID?: unknown
            messageID?: unknown
            callID?: unknown
            tool?: unknown
            text?: unknown
            time?: { start?: unknown; end?: unknown }
            state?: {
              status?: unknown
              output?: unknown
              error?: unknown
              time?: { start?: unknown; end?: unknown }
            }
          }
        | undefined
      if (!part) return null
      const sessionID = typeof part.sessionID === "string" ? part.sessionID : undefined
      if (!sessionID) return null
      const messageID = typeof part.messageID === "string" ? part.messageID : undefined

      if (part.type === "tool") {
        const state = part.state
        const status = typeof state?.status === "string" ? state.status : "unknown"
        const record: Record<string, unknown> = {
          ...base("tool_part", nowIso, mono, sessionID),
          messageID,
          callID: typeof part.callID === "string" ? part.callID : undefined,
          tool: typeof part.tool === "string" ? part.tool : "unknown",
          status,
        }
        if (status === "completed") {
          const start = state?.time?.start
          const end = state?.time?.end
          if (typeof start === "number" && typeof end === "number") record.durationMs = end - start
          if (typeof state?.output === "string") record.outputLength = state.output.length
        } else if (status === "error") {
          if (typeof state?.error === "string") record.errorLength = state.error.length
        }
        return record
      }

      if (part.type === "text") {
        const textLength = typeof part.text === "string" ? part.text.length : 0
        return {
          ...base("text_part", nowIso, mono, sessionID),
          messageID,
          textLength,
          hasTimeEnd: part.time?.end != null,
        }
      }

      if (part.type === "compaction") {
        return {
          ...base("compaction_part", nowIso, mono, sessionID),
          messageID,
          auto: !!(part as { auto?: unknown }).auto,
        }
      }

      return null
    }
    default:
      return null
  }
}

/** Builds a `message_created` record from a `chat.message` hook invocation. */
function buildMessageCreatedRecord(input: {
  sessionID: string
  agent?: string
  role: string
  partCount: number
  textLength: number
  nowIso: string
  mono: number
}): Record<string, unknown> {
  return {
    ...base("message_created", input.nowIso, input.mono, input.sessionID),
    agent: input.agent ?? null,
    role: input.role,
    partCount: input.partCount,
    textLength: input.textLength,
  }
}

/** Builds an `autoresume_injection_suspected` record. */
function buildInjectionRecord(input: {
  sessionID: string
  signatureId: string
  promptHash: string
  isSubagentAtCreation: boolean | null
  nowIso: string
  mono: number
}): Record<string, unknown> {
  return {
    ...base("autoresume_injection_suspected", input.nowIso, input.mono, input.sessionID),
    source: input.signatureId,
    promptHash: input.promptHash,
    isSubagentAtCreation: input.isSubagentAtCreation,
  }
}

/** Builds a `tool_execute_before` record. */
function buildToolExecuteBeforeRecord(input: {
  sessionID: string
  callID: string
  tool: string
  args: unknown
  nowIso: string
  mono: number
}): Record<string, unknown> {
  return {
    ...base("tool_execute_before", input.nowIso, input.mono, input.sessionID),
    callID: input.callID,
    tool: input.tool,
    argsHash: hashArgs(input.args),
  }
}

/** Builds a `tool_execute_after` record. `durationMs` is null when no matching "before" was seen. */
function buildToolExecuteAfterRecord(input: {
  sessionID: string
  callID: string
  tool: string
  durationMs: number | null
  nowIso: string
  mono: number
}): Record<string, unknown> {
  return {
    ...base("tool_execute_after", input.nowIso, input.mono, input.sessionID),
    callID: input.callID,
    tool: input.tool,
    durationMs: input.durationMs,
  }
}

/** Builds a `heartbeat` record. */
function buildHeartbeatRecord(sessionsTracked: number, nowIso: string, mono: number): Record<string, unknown> {
  return { ...base("heartbeat", nowIso, mono), sessionsTracked }
}

/** Builds a `recorder_error` record — never thrown, always logged and swallowed. */
function buildRecorderErrorRecord(error: unknown, nowIso: string, mono: number): Record<string, unknown> {
  const name = error instanceof Error ? error.name : "UnknownError"
  const message = error instanceof Error ? error.message : String(error)
  return { ...base("recorder_error", nowIso, mono), errorName: name, errorMessage: message.slice(0, 300) }
}

// ---------------------------------------------------------------------------
// I/O layer
// ---------------------------------------------------------------------------

export const NordicBeesHarnessTrace: Plugin = async ({ directory }) => {
  const reportsDir = join(directory, ".opencode", "reports")
  const tracePath = join(reportsDir, "harness-trace.jsonl")
  const loadMono = performance.now()
  const mono = () => Math.round(performance.now() - loadMono)

  // Per-session isSubagentAtCreation, populated at session_created,
  // deleted at session_deleted. Read-only observation state — never
  // affects control flow in the real harness.
  const sessionSubagentFlag = new Map<string, boolean>()
  // callID -> {mono, sessionID} for tool.execute duration tracking.
  const toolStartTimes = new Map<string, { mono: number }>()

  function rotateIfNeeded() {
    try {
      if (!existsSync(tracePath)) return
      const size = statSync(tracePath).size
      if (!shouldRotate(size)) return
      const isoDate = new Date().toISOString().slice(0, 10)
      let suffix = 0
      let target = join(reportsDir, rotatedFileName(isoDate, suffix))
      while (existsSync(target)) {
        suffix += 1
        target = join(reportsDir, rotatedFileName(isoDate, suffix))
      }
      renameSync(tracePath, target)
    } catch {
      // Rotation failing must never block appending — worst case the file
      // grows past the cap until the next successful rotation attempt.
    }
  }

  function append(record: Record<string, unknown>) {
    try {
      if (!existsSync(reportsDir)) mkdirSync(reportsDir, { recursive: true })
      rotateIfNeeded()
      appendFileSync(tracePath, toLine(record) + "\n", "utf8")
    } catch {
      // This recorder must never throw out of a hook. There is nowhere
      // else to report an append failure — if the file itself can't be
      // written, a recorder_error line would fail the same way.
    }
  }

  function nowIso(): string {
    return new Date().toISOString()
  }

  const heartbeatTimer = setInterval(() => {
    try {
      append(buildHeartbeatRecord(sessionSubagentFlag.size, nowIso(), mono()))
    } catch (e) {
      append(buildRecorderErrorRecord(e, nowIso(), mono()))
    }
  }, HEARTBEAT_INTERVAL_MS)
  if (heartbeatTimer.unref) heartbeatTimer.unref()

  return {
    event: async ({ event }) => {
      try {
        // Track subagent flag / clean up state — read-only bookkeeping,
        // mirrors what the record itself says, never drives any decision.
        if (event?.type === "session.created") {
          const info = (event.properties as { info?: { id?: unknown; parentID?: unknown } })?.info
          const sessionID = typeof info?.id === "string" ? info.id : undefined
          if (sessionID) {
            const isSub = typeof info?.parentID === "string" && info.parentID.length > 0
            sessionSubagentFlag.set(sessionID, isSub)
          }
        } else if (event?.type === "session.deleted") {
          const info = (event.properties as { info?: { id?: unknown } })?.info
          const sessionID = typeof info?.id === "string" ? info.id : undefined
          if (sessionID) sessionSubagentFlag.delete(sessionID)
        }

        const record = eventToRecord(event, nowIso(), mono())
        if (record) append(record)
      } catch (e) {
        append(buildRecorderErrorRecord(e, nowIso(), mono()))
      }
    },

    "chat.message": async (input, output) => {
      try {
        const sessionID = input?.sessionID
        if (!sessionID || !output?.message) return
        const parts = Array.isArray(output.parts) ? output.parts : []
        let textLength = 0
        let combinedText = ""
        for (const p of parts) {
          if ((p as { type?: unknown }).type === "text") {
            const t = (p as { text?: unknown }).text
            if (typeof t === "string") {
              textLength += t.length
              combinedText += t + "\n"
            }
          }
        }
        const role = output.message.role ?? "unknown"
        append(
          buildMessageCreatedRecord({
            sessionID,
            agent: input.agent,
            role,
            partCount: parts.length,
            textLength,
            nowIso: nowIso(),
            mono: mono(),
          }),
        )

        if (role === "user") {
          const match = matchInjection(combinedText)
          if (match) {
            const isSub = sessionSubagentFlag.has(sessionID) ? sessionSubagentFlag.get(sessionID)! : null
            append(
              buildInjectionRecord({
                sessionID,
                signatureId: match.id,
                promptHash: match.promptHash,
                isSubagentAtCreation: isSub,
                nowIso: nowIso(),
                mono: mono(),
              }),
            )
          }
        }
      } catch (e) {
        append(buildRecorderErrorRecord(e, nowIso(), mono()))
      }
    },

    "tool.execute.before": async (input, output) => {
      try {
        const sessionID = input?.sessionID
        const callID = input?.callID
        if (!sessionID || !callID) return
        if (toolStartTimes.size > TOOL_START_MAP_MAX) toolStartTimes.clear()
        toolStartTimes.set(callID, { mono: mono() })
        append(
          buildToolExecuteBeforeRecord({
            sessionID,
            callID,
            tool: input.tool ?? "unknown",
            args: output?.args,
            nowIso: nowIso(),
            mono: mono(),
          }),
        )
      } catch (e) {
        append(buildRecorderErrorRecord(e, nowIso(), mono()))
      }
    },

    "tool.execute.after": async (input) => {
      try {
        const sessionID = input?.sessionID
        const callID = input?.callID
        if (!sessionID || !callID) return
        const started = toolStartTimes.get(callID)
        toolStartTimes.delete(callID)
        const durationMs = started ? mono() - started.mono : null
        append(
          buildToolExecuteAfterRecord({
            sessionID,
            callID,
            tool: input.tool ?? "unknown",
            durationMs,
            nowIso: nowIso(),
            mono: mono(),
          }),
        )
      } catch (e) {
        append(buildRecorderErrorRecord(e, nowIso(), mono()))
      }
    },
  }
}

