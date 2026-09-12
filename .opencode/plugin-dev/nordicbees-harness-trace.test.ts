import { describe, test, expect } from "bun:test"
import { readFileSync } from "fs"
import {
  sha256Prefix,
  stableStringify,
  hashArgs,
  matchInjection,
  shouldRotate,
  rotatedFileName,
  toLine,
  eventToRecord,
  buildMessageCreatedRecord,
  buildInjectionRecord,
  buildToolExecuteBeforeRecord,
  buildToolExecuteAfterRecord,
  buildHeartbeatRecord,
  buildRecorderErrorRecord,
  MAX_ROTATE_BYTES,
  HASH_PREFIX_LEN,
} from "./nordicbees-harness-trace"
import {
  stripToSingleExport,
  buildLiveCopy,
  PLUGIN_EXPORT_NAME,
  GENERATED_HEADER,
  devSourcePath,
  liveCopyPath,
} from "./build-plugin"

// Real, verbatim prompt strings from .opencode/vendor/auto-resume-1.1.15-patched.js,
// as catalogued in .opencode/planning/auto-resume-1.1.15-inventory.md section B.
// Kept here as full literals (not just the distinctive substrings the recorder
// itself matches on) so these tests exercise the matcher against the REAL
// strings the plugin actually sends, not a hand-picked fragment.
const TOOL_LOOP_RECOVERY_PROMPT =
  "I notice you've been calling the same tool multiple times in a row without making progress. " +
  "Please step back and reassess your approach. Consider: " +
  "1) Are you stuck in a loop? 2) Do you need different information first? " +
  "3) Should you try a different tool or break the task into smaller steps? " +
  "Take a moment to think about what's blocking you and propose a different strategy."
const TOOL_TEXT_RECOVERY_PROMPT =
  "Your last message contained a raw tool call printed as text instead of being executed. " +
  "Please use the proper tool calling mechanism to execute it."
const THINKING_TOOL_RECOVERY_PROMPT =
  "I noticed you have a tool call generated in your thinking/reasoning. " +
  "Please execute it using the proper tool calling mechanism instead of keeping it in reasoning."
const DONE_WITHOUT_WORK_PROMPT =
  "I need you to verify more carefully that you have actually completed all the required tasks. " +
  "Your response indicated you're done, but no work was detected. Please check your todo list " +
  "and complete any remaining work."
const DONE_WITHOUT_DETAILS_PROMPT =
  "Your last response claimed the task is complete but contained no work description. This is not acceptable. " +
  "You MUST respond now with a full, detailed report of everything you did: " +
  "for each file you modified, state the full path and the exact changes; " +
  "list every command you ran to verify and its result; state the final outcome. " +
  "Do NOT reply with 'done', 'task completed', or any short acknowledgment — " +
  "your ONLY acceptable response right now is this detailed report. Write it now."
const SUBAGENT_RECOVERY_PROMPT =
  "It looks like you may have stalled or timed out. Please retry the last operation or continue with the task."
function buildOpenTodosReminder(open: { status: string; content: string }[]): string {
  const list = open.map((t, i) => `${i + 1}. [${t.status}] ${t.content}`).join("\n")
  const plural = open.length > 1 ? "s" : ""
  const taskWord = open.length > 1 ? "tasks" : "task"
  const thisWord = open.length > 1 ? "these" : "this"
  return `You have ${open.length} unfinished task${plural}:\n${list}\n\nPlease continue working on ${thisWord} ${taskWord}.`
}

const SENTINEL = "SENTINEL_SECRET_TEXT_do_not_leak_9f3ac1"

function assertNoSentinel(record: unknown, label: string) {
  const line = JSON.stringify(record)
  expect(line.includes(SENTINEL)).toBe(false)
}

describe("sha256Prefix / stableStringify / hashArgs", () => {
  test("returns exactly HASH_PREFIX_LEN hex chars by default", () => {
    const h = sha256Prefix("hello")
    expect(h).toHaveLength(HASH_PREFIX_LEN)
    expect(/^[0-9a-f]+$/.test(h)).toBe(true)
  })

  test("is stable for the same input", () => {
    expect(sha256Prefix("abc")).toBe(sha256Prefix("abc"))
  })

  test("differs for different input", () => {
    expect(sha256Prefix("abc")).not.toBe(sha256Prefix("abd"))
  })

  test("stableStringify is key-order independent", () => {
    const a = { z: 1, a: { y: 2, x: 3 } }
    const b = { a: { x: 3, y: 2 }, z: 1 }
    expect(stableStringify(a)).toBe(stableStringify(b))
  })

  test("hashArgs is stable across key order (real tool-call-args shape)", () => {
    const h1 = hashArgs({ path: "/a/b.cs", limit: 100 })
    const h2 = hashArgs({ limit: 100, path: "/a/b.cs" })
    expect(h1).toBe(h2)
    expect(h1).toHaveLength(HASH_PREFIX_LEN)
  })

  test("hashArgs never contains the raw argument text", () => {
    const h = hashArgs({ command: SENTINEL })
    expect(h.includes(SENTINEL)).toBe(false)
  })

  test("stableStringify handles null/undefined/arrays without throwing", () => {
    expect(() => stableStringify(null)).not.toThrow()
    expect(() => stableStringify(undefined)).not.toThrow()
    expect(() => stableStringify([1, "a", null, { b: 2 }])).not.toThrow()
  })

  test("stableStringify falls back gracefully on a circular structure", () => {
    const circular: Record<string, unknown> = { a: 1 }
    circular.self = circular
    expect(() => stableStringify(circular)).not.toThrow()
  })
})

describe("matchInjection — positive cases against real vendor prompt strings", () => {
  test("tool-loop", () => {
    const m = matchInjection(TOOL_LOOP_RECOVERY_PROMPT)
    expect(m?.id).toBe("tool-loop")
    expect(m?.promptHash).toHaveLength(HASH_PREFIX_LEN)
  })

  test("tool-text-recovery", () => {
    expect(matchInjection(TOOL_TEXT_RECOVERY_PROMPT)?.id).toBe("tool-text-recovery")
  })

  test("thinking-tool-recovery", () => {
    expect(matchInjection(THINKING_TOOL_RECOVERY_PROMPT)?.id).toBe("thinking-tool-recovery")
  })

  test("done-without-work", () => {
    expect(matchInjection(DONE_WITHOUT_WORK_PROMPT)?.id).toBe("done-without-work")
  })

  test("done-without-details", () => {
    expect(matchInjection(DONE_WITHOUT_DETAILS_PROMPT)?.id).toBe("done-without-details")
  })

  test("subagent-recovery", () => {
    expect(matchInjection(SUBAGENT_RECOVERY_PROMPT)?.id).toBe("subagent-recovery")
  })

  test("todo-reminder — singular template", () => {
    const text = buildOpenTodosReminder([{ status: "pending", content: "Fix the thing" }])
    expect(matchInjection(text)?.id).toBe("todo-reminder")
  })

  test("todo-reminder — plural template with different todo content still matches", () => {
    const text = buildOpenTodosReminder([
      { status: "pending", content: "Task A" },
      { status: "in_progress", content: "Task B" },
    ])
    expect(matchInjection(text)?.id).toBe("todo-reminder")
  })

  test("continue-generic — exact match, no surrounding whitespace", () => {
    expect(matchInjection("continue")?.id).toBe("continue-generic")
  })

  test("continue-generic — matches after trimming surrounding whitespace/newlines", () => {
    expect(matchInjection("  continue\n")?.id).toBe("continue-generic")
  })

  test("promptHash reflects the actual matched text, not a fixed constant", () => {
    const a = matchInjection(TOOL_LOOP_RECOVERY_PROMPT)
    const b = matchInjection(TOOL_LOOP_RECOVERY_PROMPT + " extra trailing template text")
    expect(a?.id).toBe("tool-loop")
    expect(b?.id).toBe("tool-loop")
    expect(a?.promptHash).not.toBe(b?.promptHash)
  })
})

describe("matchInjection — negative cases (must NOT match)", () => {
  test("empty string", () => {
    expect(matchInjection("")).toBeNull()
  })

  test("a long legitimate report that merely uses the word 'continue' in a sentence", () => {
    const report =
      "Summary: I fixed the CreditNoteService rounding bug in Services/CreditNoteService.cs. " +
      "Ran the full test suite (dotnet test), all 412 tests pass. I will continue monitoring " +
      "the staging environment tomorrow, but there is nothing further to do here tonight. " +
      "Committed as a3f9c21."
    expect(matchInjection(report)).toBeNull()
  })

  test("a real done-claim-false-positive-shaped report (BUGLOG incident this recorder must not re-flag)", () => {
    // Mirrors the real incident behind fix-auto-resume-doneclaim-false-positive.js:
    // a long, legitimate, substantive report ending in wording that LOOKS like a
    // done-claim to auto-resume's own loose patterns, but is not one of auto-resume's
    // OWN injected prompts, and must not be flagged as an injection by this recorder.
    const report =
      "Completed T5: patched the idle-injection gate, added isSubagentAtCreation, verified via " +
      "opencode run --print-logs. Nothing left open from T5. All BUGLOG entries updated."
    expect(matchInjection(report)).toBeNull()
  })

  test("mentions 'stalled' without the full distinctive phrase", () => {
    expect(matchInjection("The deployment stalled for a few minutes but recovered on its own.")).toBeNull()
  })

  test("mentions todos and 'continue' together but isn't the actual template", () => {
    expect(
      matchInjection("I have a few open todos and will continue working on them after lunch."),
    ).toBeNull()
  })

  test("a message containing 'continue' as a substring of a longer word does not trigger continue-generic", () => {
    expect(matchInjection("discontinued")).toBeNull()
  })
})

describe("shouldRotate — boundary", () => {
  test("exactly at the cap does not rotate (strictly greater-than)", () => {
    expect(shouldRotate(MAX_ROTATE_BYTES)).toBe(false)
  })

  test("one byte over the cap rotates", () => {
    expect(shouldRotate(MAX_ROTATE_BYTES + 1)).toBe(true)
  })

  test("zero bytes never rotates", () => {
    expect(shouldRotate(0)).toBe(false)
  })

  test("respects a custom maxBytes", () => {
    expect(shouldRotate(101, 100)).toBe(true)
    expect(shouldRotate(100, 100)).toBe(false)
  })
})

describe("rotatedFileName", () => {
  test("no-suffix form", () => {
    expect(rotatedFileName("2026-09-12")).toBe("harness-trace.2026-09-12.jsonl")
  })

  test("with a collision suffix", () => {
    expect(rotatedFileName("2026-09-12", 2)).toBe("harness-trace.2026-09-12-2.jsonl")
  })
})

describe("eventToRecord — every Event type from the installed SDK, plus malformed input", () => {
  const NOW = "2026-09-12T20:00:00.000Z"
  const MONO = 12345

  test("session.created with a parentID -> isSubagentAtCreation true", () => {
    const r = eventToRecord(
      { type: "session.created", properties: { info: { id: "ses_child", parentID: "ses_parent" } } },
      NOW,
      MONO,
    )
    expect(r).toMatchObject({ type: "session_created", sessionID: "ses_child", parentID: "ses_parent", isSubagentAtCreation: true })
  })

  test("session.created with no parentID -> isSubagentAtCreation false, parentID null", () => {
    const r = eventToRecord({ type: "session.created", properties: { info: { id: "ses_top" } } }, NOW, MONO)
    expect(r).toMatchObject({ type: "session_created", sessionID: "ses_top", parentID: null, isSubagentAtCreation: false })
  })

  test("session.created missing info.id -> null", () => {
    expect(eventToRecord({ type: "session.created", properties: { info: {} } }, NOW, MONO)).toBeNull()
  })

  test("session.deleted", () => {
    const r = eventToRecord({ type: "session.deleted", properties: { info: { id: "ses_x" } } }, NOW, MONO)
    expect(r).toMatchObject({ type: "session_deleted", sessionID: "ses_x" })
  })

  test("session.status idle", () => {
    const r = eventToRecord({ type: "session.status", properties: { sessionID: "ses_x", status: { type: "idle" } } }, NOW, MONO)
    expect(r).toMatchObject({ type: "session_status", sessionID: "ses_x", status: "idle" })
  })

  test("session.status busy", () => {
    const r = eventToRecord({ type: "session.status", properties: { sessionID: "ses_x", status: { type: "busy" } } }, NOW, MONO)
    expect(r).toMatchObject({ type: "session_status", status: "busy" })
  })

  test("session.status retry carries attempt/next but never the provider's free-text message", () => {
    const r = eventToRecord(
      {
        type: "session.status",
        properties: { sessionID: "ses_x", status: { type: "retry", attempt: 2, message: SENTINEL, next: 5000 } },
      },
      NOW,
      MONO,
    )
    expect(r).toMatchObject({ type: "session_status", status: "retry", attempt: 2, next: 5000 })
    assertNoSentinel(r, "session.status retry")
  })

  test("session.idle (the separate event type, not session.status)", () => {
    const r = eventToRecord({ type: "session.idle", properties: { sessionID: "ses_x" } }, NOW, MONO)
    expect(r).toMatchObject({ type: "session_idle_event", sessionID: "ses_x" })
  })

  test("session.compacted", () => {
    const r = eventToRecord({ type: "session.compacted", properties: { sessionID: "ses_x" } }, NOW, MONO)
    expect(r).toMatchObject({ type: "session_compacted", sessionID: "ses_x" })
  })

  test("session.error with sessionID and error name", () => {
    const r = eventToRecord(
      { type: "session.error", properties: { sessionID: "ses_x", error: { name: "MessageAbortedError", data: { message: SENTINEL } } } },
      NOW,
      MONO,
    )
    expect(r).toMatchObject({ type: "session_error", sessionID: "ses_x", errorName: "MessageAbortedError" })
    assertNoSentinel(r, "session.error")
  })

  test("session.error with no sessionID at all (a real, typed possibility per the SDK)", () => {
    const r = eventToRecord({ type: "session.error", properties: { error: { name: "UnknownError" } } }, NOW, MONO)
    expect(r).toMatchObject({ type: "session_error", errorName: "UnknownError" })
    expect((r as Record<string, unknown>).sessionID).toBeUndefined()
  })

  test("session.error includes numeric statusCode when present", () => {
    const r = eventToRecord(
      { type: "session.error", properties: { sessionID: "ses_x", error: { name: "APIError", data: { statusCode: 429, message: SENTINEL } } } },
      NOW,
      MONO,
    )
    expect(r).toMatchObject({ statusCode: 429 })
    assertNoSentinel(r, "session.error APIError")
  })

  test("message.updated, assistant role, full token shape, no free-text finish leak", () => {
    const r = eventToRecord(
      {
        type: "message.updated",
        properties: {
          info: {
            sessionID: "ses_x",
            role: "assistant",
            error: { name: "ProviderAuthError" },
            tokens: { input: 100, output: 50, reasoning: 10, cache: { read: 5, write: 1 } },
            finish: "stop",
          },
        },
      },
      NOW,
      MONO,
    )
    expect(r).toMatchObject({
      type: "message_updated",
      sessionID: "ses_x",
      hasError: true,
      errorName: "ProviderAuthError",
      tokensInput: 100,
      tokensOutput: 50,
      tokensReasoning: 10,
      tokensCacheRead: 5,
      tokensCacheWrite: 1,
      finish: "stop",
    })
  })

  test("message.updated, user role -> null (only assistant messages tracked)", () => {
    expect(
      eventToRecord({ type: "message.updated", properties: { info: { sessionID: "ses_x", role: "user" } } }, NOW, MONO),
    ).toBeNull()
  })

  test("message.part.updated, tool part completed, includes duration and output length but never output text", () => {
    const r = eventToRecord(
      {
        type: "message.part.updated",
        properties: {
          part: {
            type: "tool",
            sessionID: "ses_x",
            messageID: "msg_1",
            callID: "call_1",
            tool: "bash",
            state: { status: "completed", output: SENTINEL, time: { start: 1000, end: 1500 } },
          },
        },
      },
      NOW,
      MONO,
    )
    expect(r).toMatchObject({ type: "tool_part", sessionID: "ses_x", tool: "bash", status: "completed", durationMs: 500, outputLength: SENTINEL.length })
    assertNoSentinel(r, "tool_part completed")
  })

  test("message.part.updated, tool part error, includes error length but never error text", () => {
    const r = eventToRecord(
      {
        type: "message.part.updated",
        properties: {
          part: { type: "tool", sessionID: "ses_x", messageID: "msg_1", callID: "call_1", tool: "bash", state: { status: "error", error: SENTINEL } },
        },
      },
      NOW,
      MONO,
    )
    expect(r).toMatchObject({ type: "tool_part", status: "error", errorLength: SENTINEL.length })
    assertNoSentinel(r, "tool_part error")
  })

  test("message.part.updated, tool part pending -> no durationMs", () => {
    const r = eventToRecord(
      {
        type: "message.part.updated",
        properties: { part: { type: "tool", sessionID: "ses_x", messageID: "msg_1", callID: "c1", tool: "read", state: { status: "pending" } } },
      },
      NOW,
      MONO,
    )
    expect(r).toMatchObject({ type: "tool_part", status: "pending" })
    expect((r as Record<string, unknown>).durationMs).toBeUndefined()
  })

  test("message.part.updated, text part, length/flag only, never the text itself", () => {
    const r = eventToRecord(
      {
        type: "message.part.updated",
        properties: { part: { type: "text", sessionID: "ses_x", messageID: "msg_1", text: SENTINEL, time: { start: 1, end: 2 } } },
      },
      NOW,
      MONO,
    )
    expect(r).toMatchObject({ type: "text_part", sessionID: "ses_x", textLength: SENTINEL.length, hasTimeEnd: true })
    assertNoSentinel(r, "text_part")
  })

  test("message.part.updated, compaction part", () => {
    const r = eventToRecord(
      { type: "message.part.updated", properties: { part: { type: "compaction", sessionID: "ses_x", messageID: "msg_1", auto: true } } },
      NOW,
      MONO,
    )
    expect(r).toMatchObject({ type: "compaction_part", sessionID: "ses_x", auto: true })
  })

  test("message.part.updated, an untracked part type (e.g. reasoning) -> null", () => {
    expect(
      eventToRecord(
        { type: "message.part.updated", properties: { part: { type: "reasoning", sessionID: "ses_x", messageID: "msg_1", text: "..." } } },
        NOW,
        MONO,
      ),
    ).toBeNull()
  })

  test("every other real Event type not tracked by this recorder maps to null", () => {
    const untracked = [
      "server.instance.disposed",
      "installation.updated",
      "installation.update-available",
      "lsp.client.diagnostics",
      "lsp.updated",
      "message.removed",
      "message.part.removed",
      "permission.updated",
      "permission.replied",
      "file.edited",
      "todo.updated",
      "command.executed",
      "session.updated",
      "session.diff",
      "file.watcher.updated",
      "vcs.branch.updated",
      "tui.prompt.append",
      "tui.command.execute",
      "tui.toast.show",
      "pty.created",
      "pty.updated",
      "pty.exited",
      "pty.deleted",
      "server.connected",
    ]
    for (const type of untracked) {
      expect(eventToRecord({ type, properties: {} }, NOW, MONO)).toBeNull()
    }
  })

  test("malformed events never throw and map to null", () => {
    expect(eventToRecord(null, NOW, MONO)).toBeNull()
    expect(eventToRecord(undefined, NOW, MONO)).toBeNull()
    expect(eventToRecord("not an event", NOW, MONO)).toBeNull()
    expect(eventToRecord(42, NOW, MONO)).toBeNull()
    expect(eventToRecord({}, NOW, MONO)).toBeNull()
    expect(eventToRecord({ type: 123 }, NOW, MONO)).toBeNull()
    expect(eventToRecord({ type: "session.created" }, NOW, MONO)).toBeNull()
    expect(eventToRecord({ type: "session.created", properties: null }, NOW, MONO)).toBeNull()
    expect(eventToRecord({ type: "message.part.updated", properties: {} }, NOW, MONO)).toBeNull()
    expect(eventToRecord({ type: "message.part.updated", properties: { part: null } }, NOW, MONO)).toBeNull()
    expect(eventToRecord({ type: "message.part.updated", properties: { part: { type: "tool" } } }, NOW, MONO)).toBeNull()
    expect(eventToRecord({ type: "unknown.made.up.event", properties: {} }, NOW, MONO)).toBeNull()
  })
})

describe("record builders — shape and the no-raw-text guarantee", () => {
  const NOW = "2026-09-12T20:00:00.000Z"
  const MONO = 42

  test("buildMessageCreatedRecord never includes message text, only counts", () => {
    const r = buildMessageCreatedRecord({
      sessionID: "ses_x",
      agent: "orchestrator",
      role: "user",
      partCount: 1,
      textLength: SENTINEL.length,
      nowIso: NOW,
      mono: MONO,
    })
    expect(r).toMatchObject({ type: "message_created", sessionID: "ses_x", agent: "orchestrator", role: "user", partCount: 1, textLength: SENTINEL.length })
    assertNoSentinel(r, "message_created")
  })

  test("buildInjectionRecord carries only the signature id, a hash, and isSubagentAtCreation", () => {
    const r = buildInjectionRecord({
      sessionID: "ses_x",
      signatureId: "tool-loop",
      promptHash: sha256Prefix(SENTINEL),
      isSubagentAtCreation: true,
      nowIso: NOW,
      mono: MONO,
    })
    expect(r).toMatchObject({ type: "autoresume_injection_suspected", sessionID: "ses_x", source: "tool-loop", isSubagentAtCreation: true })
    assertNoSentinel(r, "autoresume_injection_suspected")
  })

  test("buildToolExecuteBeforeRecord hashes args, never stores them raw", () => {
    const r = buildToolExecuteBeforeRecord({
      sessionID: "ses_x",
      callID: "call_1",
      tool: "bash",
      args: { command: SENTINEL },
      nowIso: NOW,
      mono: MONO,
    })
    expect(r).toMatchObject({ type: "tool_execute_before", sessionID: "ses_x", callID: "call_1", tool: "bash" })
    expect((r as Record<string, unknown>).argsHash).toHaveLength(HASH_PREFIX_LEN)
    assertNoSentinel(r, "tool_execute_before")
  })

  test("buildToolExecuteAfterRecord with a known duration", () => {
    const r = buildToolExecuteAfterRecord({ sessionID: "ses_x", callID: "call_1", tool: "bash", durationMs: 250, nowIso: NOW, mono: MONO })
    expect(r).toMatchObject({ type: "tool_execute_after", durationMs: 250 })
  })

  test("buildToolExecuteAfterRecord with an unmatched callID reports durationMs: null", () => {
    const r = buildToolExecuteAfterRecord({ sessionID: "ses_x", callID: "call_orphan", tool: "bash", durationMs: null, nowIso: NOW, mono: MONO })
    expect((r as Record<string, unknown>).durationMs).toBeNull()
  })

  test("buildHeartbeatRecord carries only a count", () => {
    const r = buildHeartbeatRecord(7, NOW, MONO)
    expect(r).toMatchObject({ type: "heartbeat", sessionsTracked: 7 })
  })

  test("buildRecorderErrorRecord never leaks more than a truncated error message", () => {
    const longMsg = SENTINEL.repeat(20)
    const r = buildRecorderErrorRecord(new Error(longMsg), NOW, MONO)
    expect(r).toMatchObject({ type: "recorder_error", errorName: "Error" })
    expect((r as Record<string, unknown>).errorMessage).toHaveLength(300)
  })

  test("buildRecorderErrorRecord handles a non-Error thrown value", () => {
    const r = buildRecorderErrorRecord("just a string", NOW, MONO)
    expect(r).toMatchObject({ type: "recorder_error", errorName: "UnknownError", errorMessage: "just a string" })
  })

  test("toLine produces one JSON line with no trailing newline", () => {
    const line = toLine({ a: 1 })
    expect(line).toBe('{"a":1}')
    expect(line.includes("\n")).toBe(false)
  })
})

describe("end-to-end privacy guarantee: no serialised record from any builder ever contains the sentinel", () => {
  test("sweeping every builder with sentinel-laden input", () => {
    const NOW = "2026-09-12T20:00:00.000Z"
    const records = [
      eventToRecord({ type: "session.status", properties: { sessionID: "s", status: { type: "retry", message: SENTINEL, attempt: 1 } } }, NOW, 1),
      eventToRecord({ type: "session.error", properties: { sessionID: "s", error: { name: "APIError", data: { message: SENTINEL } } } }, NOW, 1),
      eventToRecord(
        { type: "message.part.updated", properties: { part: { type: "text", sessionID: "s", messageID: "m", text: SENTINEL } } },
        NOW,
        1,
      ),
      eventToRecord(
        {
          type: "message.part.updated",
          properties: { part: { type: "tool", sessionID: "s", messageID: "m", callID: "c", tool: "bash", state: { status: "completed", output: SENTINEL, time: { start: 0, end: 1 } } } },
        },
        NOW,
        1,
      ),
      buildMessageCreatedRecord({ sessionID: "s", role: "user", partCount: 1, textLength: SENTINEL.length, nowIso: NOW, mono: 1 }),
      buildToolExecuteBeforeRecord({ sessionID: "s", callID: "c", tool: "bash", args: { x: SENTINEL }, nowIso: NOW, mono: 1 }),
      buildInjectionRecord({ sessionID: "s", signatureId: "continue-generic", promptHash: sha256Prefix(SENTINEL), isSubagentAtCreation: null, nowIso: NOW, mono: 1 }),
    ]
    for (const r of records) {
      expect(r).not.toBeNull()
      assertNoSentinel(r, "sweep")
    }
  })
})

describe("build-plugin: stripToSingleExport", () => {
  test("strips 'export ' from a non-designated const, keeps the designated one", () => {
    const src = `export const A = 1\nexport const Keep = 2\n`
    const out = stripToSingleExport(src, "Keep")
    expect(out).toBe(`const A = 1\nexport const Keep = 2\n`)
  })

  test("strips function/type/interface/class/let/var declarations the same way", () => {
    const src = [
      "export function f() {}",
      "export type T = {}",
      "export interface I {}",
      "export class C {}",
      "export let l = 1",
      "export var v = 1",
    ].join("\n")
    const out = stripToSingleExport(src, "Keep")
    expect(out).toBe(["function f() {}", "type T = {}", "interface I {}", "class C {}", "let l = 1", "var v = 1"].join("\n"))
  })

  test("removes an 'export default' line entirely", () => {
    const src = `export const Keep = 1\nexport default Keep\n`
    const out = stripToSingleExport(src, "Keep")
    expect(out).toBe(`export const Keep = 1\n`)
  })

  test("leaves non-export lines, comments, and multi-line bodies untouched", () => {
    const src = [
      "// a comment mentioning export const nothing here",
      "import type { X } from 'y'",
      "export function build(input: {",
      "  a: number",
      "}): number {",
      "  return input.a",
      "}",
    ].join("\n")
    const out = stripToSingleExport(src, "Keep")
    expect(out).toBe(
      [
        "// a comment mentioning export const nothing here",
        "import type { X } from 'y'",
        "function build(input: {",
        "  a: number",
        "}): number {",
        "  return input.a",
        "}",
      ].join("\n"),
    )
  })

  test("only matches the designated export by exact identifier, not by prefix", () => {
    // "KeepExtra" must NOT be treated as the designated export "Keep".
    const src = `export const KeepExtra = 1\nexport const Keep = 2\n`
    const out = stripToSingleExport(src, "Keep")
    expect(out).toBe(`const KeepExtra = 1\nexport const Keep = 2\n`)
  })

  test("is idempotent: running it again on its own output changes nothing further", () => {
    const src = `export const A = 1\nexport const Keep = 2\nexport default Keep\n`
    const once = stripToSingleExport(src, "Keep")
    const twice = stripToSingleExport(once, "Keep")
    expect(twice).toBe(once)
  })
})

describe("build-plugin: buildLiveCopy determinism", () => {
  test("is deterministic: the same input always produces byte-identical output", () => {
    const src = `export const A = 1\nexport const ${PLUGIN_EXPORT_NAME} = 2\nexport default ${PLUGIN_EXPORT_NAME}\n`
    const a = buildLiveCopy(src, PLUGIN_EXPORT_NAME)
    const b = buildLiveCopy(src, PLUGIN_EXPORT_NAME)
    expect(a).toBe(b)
  })

  test("output starts with the fixed, timestamp-free generated-file header", () => {
    const src = `export const ${PLUGIN_EXPORT_NAME} = 1\n`
    const out = buildLiveCopy(src, PLUGIN_EXPORT_NAME)
    expect(out.startsWith(GENERATED_HEADER)).toBe(true)
    expect(GENERATED_HEADER).not.toMatch(/\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/) // no embedded ISO timestamp
  })

  test("keeps only the designated Plugin export in the generated output", () => {
    const src = `export const Helper = 1\nexport const ${PLUGIN_EXPORT_NAME} = 2\nexport default ${PLUGIN_EXPORT_NAME}\n`
    const out = buildLiveCopy(src, PLUGIN_EXPORT_NAME)
    const exportLines = out.split("\n").filter((l) => l.startsWith("export "))
    expect(exportLines).toEqual([`export const ${PLUGIN_EXPORT_NAME} = 2`])
  })
})

describe("drift detection: the live .opencode/plugin/ copy must match the generator's current output", () => {
  test("regenerating from the current dev source byte-for-byte matches the current live copy", () => {
    const devSource = readFileSync(devSourcePath(), "utf8")
    const liveSource = readFileSync(liveCopyPath(), "utf8")
    const expected = buildLiveCopy(devSource, PLUGIN_EXPORT_NAME)
    if (expected !== liveSource) {
      throw new Error(
        "The live plugin copy (.opencode/plugin/nordicbees-harness-trace.ts) does not match " +
          "what `bun .opencode/plugin-dev/build-plugin.ts` would produce from the current dev " +
          "source right now. Either the dev source was edited without regenerating, or the live " +
          "copy was edited directly. Run: bun .opencode/plugin-dev/build-plugin.ts",
      )
    }
    expect(expected).toBe(liveSource)
  })
})
