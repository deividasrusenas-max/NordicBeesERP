# NordicBeesERP — AI Agent Stack Architecture

This document explains the full multi-agent setup built for this project:
what exists, why, and where to find it. Written so a future session (yours
or mine) can understand the whole system without re-deriving it.

Config files: `opencode.json` (OpenCode, primary) and `kilo.jsonc` (Kilo
Code, kept in sync as a fallback). Both live at the project root.

---

## 1. Philosophy

The core lesson from building this: **don't trust an LLM's self-report —
verify with code, and don't trust prompt instructions alone — enforce with
config where possible.** Every piece below exists because a purely
prompt-based instruction failed at least once during setup.

---

## 2. Agents

Defined in `opencode.json` / `kilo.jsonc` under `"agent"`. Prompt bodies
live in `.kilo/prompts/*.md`.

| Agent | Model | Role | Prompt file |
|---|---|---|---|
| `orchestrator` | Qwen3 235B Thinking (OpenRouter) | Plans, delegates via Task tool, never writes code itself (`edit: deny`). Runs read-only verification (`git log`, DB checks) itself. | `.kilo/prompts/plan.md` |
| `coder` | Qwen3.6-27B (local, port 8086) | Writes/edits one file per task. No bash, no search tools — works only from exact paths given. | `.kilo/prompts/code.md` |
| `fixer` | Qwen3.6-35B-A3B (local, port 8087) | Runs the full build→fix→grep→bump→commit cycle. Has bash (with `mysql`/`mariadb` denied). | `.kilo/prompts/debug.md` |
| `reviewer` | Qwen3.6-35B-A3B (local, port 8088) | Read-only BRC8/spec compliance audits. No edit, no bash except `grep`/`find`. | `.kilo/prompts/reviewer.md` |

**Naming note:** agents are named `orchestrator`/`coder`/`fixer`/`reviewer`
— deliberately NOT `plan`/`code`/`debug`, because those names collide with
Kilo's and OpenCode's own built-in reserved agent modes (Kilo's built-in
`code`/`debug` silently stripped `edit`/`write` tools; OpenCode's built-in
`plan` is a read-only confirmation-gated mode). **Never rename these back**
without checking for collisions first.

**Model routing gotcha:** OpenRouter models must be referenced as
`openrouter/<model-id>` (with the `openrouter/` prefix) in the `agent.*.model`
field, not just `<model-id>` — otherwise it silently resolves to a
different (wrong) built-in catalog entry instead of your custom provider.

**Reasoning mode:** `coder`/`fixer` run with `--reasoning off` on their
llama-server (mechanical tasks, thinking mode just adds latency).
`reviewer` keeps thinking ON (needs judgment/nuance). `orchestrator` is a
frontier model via API, thinking controlled by its own settings.

---

## 3. Plugins

Live in `.opencode/plugin/*.ts`. Auto-loaded by OpenCode at startup (no
registration needed beyond the file existing there).

| File | Hook | What it does |
|---|---|---|
| `nordicbees-verify.ts` | `tool.execute.after` | After every Task-tool call to `coder`/`fixer`, runs a REAL `dotnet build` and injects the actual exit code/output into the session — regardless of what the subagent claims. This is the "deterministic completion gate." |
| `nordicbees-skill-inject.ts` | `tool.execute.before` | Before a Task-tool call to `coder`/`fixer` starts, force-injects the full content of relevant skill(s) into the delegation text, based on keyword matching (`.razor`→mudblazor, `Service.cs`→dotnet-efcore-nordicbees, etc.). Exists because automatic skill auto-triggering is unreliable (confirmed matches a known OpenCode/community issue, not model-specific). |
| `nordicbees-reminder.ts` | `chat.message` | Prepends a short standard-workflow reminder to every user message sent to `orchestrator`, so you don't have to retype "use planning-with-files / delegate skills explicitly / etc." every time. **Experimental** — this hook's exact field shape is still evolving upstream in OpenCode; if it silently doesn't fire, that's why. |

---

## 4. Skills

Live in `.opencode/skills/<name>/SKILL.md`. Auto-discovered by `name` +
`description` frontmatter, but auto-trigger is unreliable — see the
skill-injection plugin above, which force-injects the ones marked "always."

| Skill | Triggers on | Always injected for |
|---|---|---|
| `mudblazor` | `.razor` files, MudBlazor component names | — |
| `dotnet-efcore-nordicbees` | Service/migration/DbContext files | — |
| `git-workflow-nordicbees` | (commit conventions, hardcode-check rules) | `fixer` |
| `crud-completeness` | (field-parity checklist for CRUD code) | `coder` |
| `verify-before-done` | (call-chain tracing before claiming done) | `coder` |
| `questpdf-nordicbees` | PDF/QuestPDF/invoice/certificate keywords | — |
| `lithuanian-vat-isaf` | VAT/PVM/i.SAF keywords | — |
| `playwright-e2e-nordicbees` | E2E/browser-test/playwright keywords | — |
| `efcore-performance-nordicbees` | slow/performance/optimize/N+1 keywords | — |
| `llm-code-quality-gate` | (catches LLM-authoring-specific smells: orphaned code, fabricated-confidence comments, copy-paste residue) | `fixer` |
| `planning-with-files` (third-party, `OthmanAdi/planning-with-files`) | manual invocation | — |

`planning-with-files` writes persistent state to `.planning/<session>/` —
`task_plan.md`, `findings.md`, `progress.md` — so progress survives context
compaction. **Caveat found today:** if a fresh session re-runs its
`init-session.sh`, it can overwrite existing plan content back to the
blank template — treat `.planning/` as informative, not as the sole source
of truth; `git log` + real file/DB checks remain the ground truth.

---

## 5. MCP servers

Registered under `"mcp"` in the config files.

| Server | Type | Purpose |
|---|---|---|
| `nordicbees-db` | local, `node /Users/deividasru/mysql-mcp/server.js` | Scoped, single-connection MySQL/MariaDB access to `nordic_bees_erp` (dev, `100.110.26.80`). No parameter to specify a different host/schema/user — this is deliberate, so an agent can't invent or escalate to a different database. Raw `mysql -u *` CLI is denied globally for this reason. |
| `playwright` | local, `npx @playwright/mcp@latest` | Official Microsoft Playwright MCP — real browser automation for E2E verification. Paired with the `playwright-e2e-nordicbees` skill. |

---

## 6. Permission architecture highlights

- `coder`: `bash: deny` entirely (no legitimate need — build/commit is
  `fixer`'s job). Also denies `glob`/`grep`/`list`/`task`/`agent_manager`/
  `background_process`/etc. — kept to a minimal tool set deliberately
  (community-documented finding: smaller local models pick the *wrong*
  tool more often when given a large tool menu, even though structured
  output/grammar constraints keep the *format* valid).
- `fixer`: `bash: allow` but `mysql *`/`mariadb *` denied (must use the
  `nordicbees-db` MCP tool instead).
- `orchestrator`: `edit: deny` for everything except `.planning/**` (needed
  for the planning-with-files workflow to update its own state files).
- Hard rule (in skills, not just prompts): **no agent may ever apply a
  database schema change (ALTER/CREATE/DROP) automatically, even to dev.**
  A human applies it directly. Also: **never try alternate DB credentials
  or guess a schema name on a permission error** — stop and report.

---

## 7. Known upstream gotchas (not our bugs, but worth remembering)

- **Kilo/OpenCode built-in agent name collisions** — see naming note above.
- **Ripgrep download bug** — Kilo/OpenCode's built-in `grep`/`glob` tool
  tries to download its own ripgrep binary over the network and fails in
  offline/local-model setups (GitHub issue #11320). Worked around by
  denying `grep`/`glob` for `coder`/`fixer` and giving them only exact
  file paths from `orchestrator`.
- **Hardcoded bash syntax guard** — any bash command containing a newline,
  `&&`, `;`, `|`, backtick, `$(`, `<(`, or `>` is blocked regardless of
  allow rules. Never chain commands; use the `workdir` param instead of
  `cd X &&`.
- **EF Core migration "already applied" trap** — this project keeps
  editing the *same* migration ID's `Up()` method repeatedly instead of
  creating new migration files. Once a migration ID is marked applied in
  `__EFMigrationsHistory`, `dotnet ef database update` becomes a no-op for
  it — new `CREATE TABLE`/`ALTER` statements added to that file's `Up()`
  method do NOT get applied automatically. This caused a real incident
  (Task 2's 8 labeling tables existed in the migration file but not in the
  live DB for most of a day). Always verify with `SHOW TABLES`/
  `SHOW COLUMNS` directly — never trust migration-history status alone.
- **`AspNetUsers.Id` is `varchar(255)`, not `int`** — several new tables'
  FK columns (`print_jobs.created_by_user_id`,
  `container_label_events.operator_id`) were specified as `int`, causing
  FK type-mismatch errors. Check this project's actual user-table PK type
  before adding a new FK to it (there's also a separate `erp_users` table
  with an `int` PK — figure out which one a given audit field should
  reference).
- **`git add -A` is dangerous in an automated workflow** — it silently
  sweeps up ANY dirty file into a commit, including accidental corruption
  from an earlier, unrelated session. A real incident: `DeliveryCreate.razor`
  was accidentally truncated to one line by an earlier session, and a
  later `git add -A` committed that corruption under an unrelated commit
  message ("BUCKET_GROUP cleanup") — nobody noticed for hours. Always
  `git add <exact files>`, check `git status` first.
- **OpenCode TUI shows "unknown" for MCP/external tool calls** — this is a
  known, purely cosmetic display gap (OpenCode issue #1013), not a
  functional problem. The tool call and result still work.
- **`.kilo/worktrees/`** contains old, stale checkouts with pre-refactor
  code (e.g. still has `BUCKET_GROUP` references). Never treat matches
  found there as real — always exclude it from greps/checks on the main
  codebase.

---

## 8. What's portable to a different project vs. NordicBeesERP-specific

**Portable (reusable pattern, just adapt file paths/keywords):**
agent architecture (orchestrator/coder/fixer/reviewer split, naming
rationale, permission minimalism), the three plugin *mechanisms*
(build-verification gate, skill force-injection, message reminder),
`crud-completeness` and `verify-before-done` skills (generic to any
CRUD/data-writing codebase), the local model provider setup (tied to your
hardware, not this project).

**NordicBeesERP-specific (needs a new/adapted version per project):**
`dotnet-efcore-nordicbees`, `git-workflow-nordicbees`, `questpdf-nordicbees`,
`lithuanian-vat-isaf`, `mudblazor` (only if the next project is also
Blazor/MudBlazor), the `nordicbees-db` MCP registration, `plan.md`'s
Task 0-14 list and file map.
