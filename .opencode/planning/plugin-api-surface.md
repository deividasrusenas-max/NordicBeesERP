# opencode plugin API surface — as installed on this machine

Task 1 of the Phase 0 evidence-instrumentation task. Every claim below is sourced
from a specific file path and line, or from the compiled `opencode` binary via
`strings`, both read during this session. Nothing here is guessed or recalled
from training data.

Sources read in full:
- `.opencode/node_modules/@opencode-ai/plugin/package.json` (version 1.17.18 —
  see "Version mismatch" note below)
- `.opencode/node_modules/@opencode-ai/plugin/dist/index.d.ts` (Hooks, Plugin,
  PluginInput types)
- `.opencode/node_modules/@opencode-ai/plugin/dist/tool.d.ts` (ToolContext,
  ToolDefinition, `tool()` helper)
- `.opencode/node_modules/@opencode-ai/sdk/dist/gen/types.gen.d.ts` (Event union,
  Session, Message, Part, ToolState — lines 1-530 read directly)
- `.opencode/plugin/nordicbees-circuit-breaker.ts` (confirmed-working API usage,
  house style)
- `.opencode/plugin/nordicbees-reminder.ts` (house style, `chat.message` usage)

**Version mismatch, flagged as an open question, not resolved here**: the
installed `@opencode-ai/plugin` package.json reports `"version": "1.17.18"`,
while `opencode --version` (the actual running binary) reports `1.18.30`, and
the user's task description also says v1.18.30. I did not reconcile this — the
`.d.ts` files are the best available static contract, but the live binary may
have moved slightly ahead of these types. Where the circuit-breaker plugin's own
header claims something was empirically confirmed against a live captured event
(not just the types), I've marked that below as higher-confidence.

## 1. Every hook key available (`Hooks` interface, index.d.ts:173-322)

| Hook key | Signature | Notes |
|---|---|---|
| `dispose` | `() => Promise<void>` | Called on plugin unload. **Neither reference plugin implements this — confirmed by grep, zero matches for "dispose" in either file.** auto-resume also does not implement it (confirmed in Task 2). |
| `event` | `(input: { event: Event }) => Promise<void>` | The main event-stream hook. Both reference plugins use this. |
| `config` | `(input: Config) => Promise<void>` | Called with the resolved config. auto-resume implements this (logs "config OK") but plugin.d.ts's `Config` type is basically the full SDK config minus `plugin`, so of limited use as a targeted hook. |
| `tool` | `{ [key: string]: ToolDefinition }` | A static map, not a function — this is how auto-resume registers `task_complete` (see Task 2E). |
| `auth` | `AuthHook` | OAuth/API-key provider auth flow registration. Not used by either reference plugin or auto-resume. |
| `provider` | `ProviderHook` | Custom model/provider registration. Not used by any plugin here. |
| `"chat.message"` | `(input: {sessionID, agent?, model?, messageID?, variant?}, output: {message: UserMessage, parts: Part[]}) => Promise<void>` | Fires when a new user message is received. Mutating `output.parts` mutates the actual message — confirmed working by nordicbees-reminder.ts (prepends reminder text to the first text part). auto-resume uses the `input` side only (to touch `lastActivityAt` / re-arm). |
| `"chat.params"` | `(input: {...}, output: {temperature, topP, topK, maxOutputTokens, options}) => Promise<void>` | Not used by any plugin here. |
| `"chat.headers"` | similar shape, `output: {headers}` | Not used. |
| `"permission.ask"` | `(input: Permission, output: {status}) => Promise<void>` | Not used. |
| `"command.execute.before"` | `(input: {command, sessionID, arguments}, output: {parts}) => Promise<void>` | auto-resume uses only the `input` side (increments `pendingCommands`). |
| `"tool.execute.before"` | `(input: {tool, sessionID, callID}, output: {args}) => Promise<void>` | auto-resume uses this for live tool-loop detection (reads `hookArgs?.args`); circuit-breaker does **not** use this hook at all — it relies entirely on the `event` hook's `message.part.updated` tool parts instead. |
| `"shell.env"` | `(input: {cwd, sessionID?, callID?}, output: {env}) => Promise<void>` | Not used. |
| `"tool.execute.after"` | `(input: {tool, sessionID, callID, args}, output: {title, output, metadata}) => Promise<void>` | auto-resume uses only the `input` side (decrements `pendingTools`). |
| `"experimental.chat.messages.transform"` | `(input: {}, output: {messages: {info, parts}[]}) => Promise<void>` | Not used by any plugin here. |
| `"experimental.chat.system.transform"` | `(input: {sessionID?, model}, output: {system: string[]}) => Promise<void>` | Not used. |
| `"experimental.provider.small_model"` | — | Not used. |
| `"experimental.session.compacting"` | `(input: {sessionID}, output: {context: string[], prompt?}) => Promise<void>` | **This is the only compaction-related hook in the type surface.** Not used by auto-resume (confirmed in Task 2 — auto-resume's own "compaction" handling is a config flag gate around `session.summarize()`, a client API call, not this hook). |
| `"experimental.compaction.autocontinue"` | `(input: {sessionID, agent, model, provider, message, overflow}, output: {enabled}) => Promise<void>` | Fires after compaction succeeds, before the synthetic "continue" turn opencode itself sends. Not used by any plugin here, but directly relevant background for the rewrite (out of scope to discuss further per this task's rules). |
| `"experimental.text.complete"` | `(input: {sessionID, messageID, partID}, output: {text}) => Promise<void>` | Not used. |
| `"tool.definition"` | `(input: {toolID}, output: {description, parameters}) => Promise<void>` | Not used. |

## 2. Every `Event` type string (types.gen.d.ts, full union at line 602)

Confirmed by reading the literal union and each member type:

`server.instance.disposed`, `installation.updated`, `installation.update-available`,
`lsp.client.diagnostics`, `lsp.updated`, `message.updated`, `message.removed`,
`message.part.updated`, `message.part.removed`, `permission.updated`,
`permission.replied`, `session.status`, `session.idle`, `session.compacted`,
`file.edited`, `todo.updated`, `command.executed`, `session.created`,
`session.updated`, `session.deleted`, `session.diff`, `session.error`,
`file.watcher.updated`, `vcs.branch.updated`, `tui.prompt.append`,
`tui.command.execute`, `tui.toast.show`, `pty.created`, `pty.updated`,
`pty.exited`, `pty.deleted`, `server.connected`.

**Important, verified negative finding**: there is **no** `session.interrupted`
event type in this Event union, and `SessionStatus` (types.gen.d.ts:396-405) has
only three members — `{type:"idle"}`, `{type:"retry", attempt, message, next}`,
`{type:"busy"}` — **no `"interrupted"` status type exists either.** This directly
contradicts what the vendor auto-resume snapshot's own code assumes (see the
inventory report, section G) — auto-resume has a `case "session.interrupted":`
switch branch and an `else if (statusType === "interrupted")` branch that,
per this type surface, can never fire against the currently-installed SDK.
I have not proven this is dead at runtime (the type surface could lag the
wire protocol), only that it's unreachable per the shipped `.d.ts` contract —
flagged as an open question in the handoff report.

**`session.compacted`** (`session.compacted`, types.gen.d.ts:419-424) is the
only compaction *event*; its properties are just `{sessionID}` — no detail on
what was compacted or by how much. This is the event the trace recorder (Task 4)
uses for compaction detection, since there is no dedicated `Event` variant name
containing "compaction" besides this one and the `CompactionPart` part type
below.

## 3. Exact field shape of event types carrying sessionID / parentID / message / part / tool state

All confirmed directly from types.gen.d.ts, lines 1-530 (see file for full
context; only the load-bearing fields are extracted here).

**sessionID carriers** (event `properties.sessionID` unless noted):
`EventMessageRemoved`, `EventMessagePartRemoved`, `EventSessionStatus`,
`EventSessionIdle`, `EventSessionCompacted`, `EventTodoUpdated`,
`EventCommandExecuted`, `EventSessionDiff`. `EventSessionError.properties.sessionID`
is **optional** (`sessionID?: string`) — a session-less error is possible and
must be handled (auto-resume's own `getSid()` helper already defends against
this generically).

**parentID carrier**: `Session.parentID?: string` (types.gen.d.ts:469), present
on `EventSessionCreated.properties.info` (types.gen.d.ts:493-498),
`EventSessionUpdated.properties.info`, and `EventSessionDeleted.properties.info`.
`parentID` is **optional/absent**, not an empty string, for a top-level session
— confirmed by reading the `Session` type: it's `parentID?: string`, no default.
Both reference plugins treat "has a non-empty string parentID" as "is a
subagent" (circuit-breaker.ts:355-356, and vendor auto-resume line ~14188:
`w.isSubagent = typeof parentID === "string" && parentID.length > 0`) —
consistent, corroborating evidence across two independently-written pieces of
code.

**Message carriers**: `UserMessage` (types.gen.d.ts:39-60) has `id, sessionID,
role:"user", time.created, summary?, agent, model, system?, tools?`.
`AssistantMessage` (98-127) has `id, sessionID, role:"assistant", time.created,
time.completed?, error?, parentID` (**this `parentID` is the assistant
message's own parent MESSAGE id, not the session's parent — a naming collision
worth being careful about when writing the recorder**), `modelID, providerID,
mode, path, summary?, cost, tokens.{input,output,reasoning,cache.{read,write}},
finish?`.

**Part carriers, tool state** (types.gen.d.ts:211-274, 345-353):
`ToolPart = {id, sessionID, messageID, type:"tool", callID, tool, state:
ToolState, metadata?}`. `ToolState` is a **tagged union on `status`**:
- `pending`: `{status:"pending", input, raw}` — `raw` is the only field
  guaranteed populated pre-execution (the raw, possibly-partial tool-call text).
- `running`: `{status:"running", input, title?, metadata?, time.start}`
- `completed`: `{status:"completed", input, output, title, metadata, time.{start,end,compacted?}, attachments?}`
- `error`: `{status:"error", input, error, metadata?, time.{start,end}}`

Circuit-breaker.ts's header (lines 26-29) states it empirically confirmed, via
a real captured event, that **`input` (not `raw`) is what's populated at
`"completed"` status** — this matches the type shape exactly (`raw` only exists
on the `pending` variant), so the type surface and the empirical claim agree.

`TextPart` (types.gen.d.ts:142-157) has **no `state` field at all** —
confirmed directly from the type. circuit-breaker.ts:415-423 independently
discovered this the hard way (its text-repeat-loop detector uses
`part.time?.end`, not `part.state?.status`, specifically because TextPart has
no `.state`) and flags the surrounding text-only-repeat-loop detector's own
condition as one it could not fully verify live. This is corroborating,
two-source confirmation that `TextPart` truly has no `state`.

`Part` itself (line 345) is a union of 11 shapes: `TextPart`, an inline
"subtask" part `{type:"subtask", prompt, description, agent}`, `ReasoningPart`,
`FilePart`, `ToolPart`, `StepStartPart`, `StepFinishPart`, `SnapshotPart`,
`PatchPart`, `AgentPart`, `RetryPart`, `CompactionPart`
(`{id, sessionID, messageID, type:"compaction", auto: boolean}` — the `auto`
flag distinguishes an automatic vs. user-triggered compaction; this is the
part-level compaction signal, complementary to the `session.compacted` event).

## 4. `ToolContext` / `tool()` (tool.d.ts, full file read)

`ToolContext = {sessionID, messageID, agent, directory, worktree, abort:
AbortSignal, metadata(input): void, ask(input): Promise<void>}`. The `tool()`
helper takes `{description, args: ZodRawShape, execute(args, context):
Promise<ToolResult>}` and returns the same shape (`ToolDefinition`). This is
what both the vendor auto-resume snapshot's `task_complete` registration and
any future replacement tool would use.

## Summary for the rewrite session (informational only — no design implied)

The two facts most load-bearing for whatever gets specified later: (1) `event`
is the only hook needed for pure observation — no `tool.execute.*` hook is
required to see tool activity, since `message.part.updated` on `ToolPart`
already carries `tool`, `callID`, and full `state`; and (2) there is exactly one
compaction *event* (`session.compacted`, session-level, no detail) and one
compaction *part* (`CompactionPart`, message-level, carries `auto`). Both are
used by the trace recorder in Task 4.
