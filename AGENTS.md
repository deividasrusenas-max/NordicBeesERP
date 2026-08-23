# Agent Rules (MANDATORY - not optional)

## MANDATORY: Read First

Before writing ANY code, you MUST read:

1. `AGENTS.md` (this file)
2. `docs/PROJECT_STATE.md`
3. `README.md` if it exists
4. the specific files you plan to modify

Skipping this step is a RULE VIOLATION.

## MANDATORY: Guardrail Check Before Finishing

**NEVER tell the user "task done" without running this command:**

```bash
agent-guardrails check --base-ref HEAD~1
```

Note: the `agent-guardrails` MCP tool is currently disabled (unreliable startup) — always use this CLI command via bash, never look for an MCP tool call with this name.

**GATED RULES:**
- If issues found: **STOP. Fix before proceeding. Do NOT tell user "done".**
- If clean: include the check result in your summary.
- If the command is not found: tell the user to run `npm install -g agent-guardrails` first (it must be on PATH globally, not via npx — npx re-resolution has caused startup failures in this project).

Windows PowerShell note: if `npx` or `npm` is blocked by the `.ps1` shim policy, use `npx.cmd` and `npm.cmd`.

**FAILURE TO RUN THIS COMMAND = INCOMPLETE WORK.**

## MANDATORY: Task Contract

If `.agent-guardrails/task-contract.json` exists:

- **MUST** stay inside the declared scope (allowed paths, intended files).
- **MUST** run the required commands listed in the contract.
- **MUST** update `.agent-guardrails/evidence/current-task.md` with commands run, notable results, and residual risk.

If no contract exists and the task is non-trivial, **MUST** run:

```bash
agent-guardrails plan --task "<task description>"
```

Then implement inside the generated contract.

## MANDATORY: Working Rules

- **MUST** prefer existing patterns over new abstractions.
- **MUST** keep changes small and easy to review.
- **MUST** list touched files before editing when the task is non-trivial.
- **MUST** surface missing context instead of inventing details.
- **MUST** update or add tests when behavior changes.

## MANDATORY: Definition Of Done

ALL of these must be true before reporting completion:

- [ ] Implementation matches current project conventions.
- [ ] Changed behavior has test coverage when appropriate.
- [ ] Guardrail check passed (`agent-guardrails check --base-ref HEAD~1`).
- [ ] Required commands for the task were actually run and reported.
- [ ] Evidence note for the current task exists and reflects the real outcome.
- [ ] Risks, assumptions, and follow-up work are documented.

**IF ANY ITEM IS FALSE, THE TASK IS NOT DONE.**

## PROJECT-SPECIFIC MANDATORY RULES (NordicBeesERP)

These rules are non-negotiable regardless of what the task prompt says. If a task seems to require violating one of these, STOP and report — do not proceed and do not silently work around it.

### Database writes

- ALL SQL writes MUST use parameterized queries — `ExecuteSqlRawAsync` with positional `{0}, {1}...` parameters (or equivalent `DbParameter` objects with `@name` placeholders). Building SQL strings via `$"..."` interpolation or string concatenation with any value that did not originate as a hardcoded literal in the same line is a SEVERE VIOLATION, even if you believe the value is "safe" or already escaped. This applies even when working around a connection or transient-failure issue — a connectivity problem is never a reason to remove parameterization.
- Never call `SaveChangesAsync()` after `Add`/`Update`/`Remove` on tracked entities. This project uses global `QueryTrackingBehavior.NoTracking` — it will silently persist 0 rows.

### Migrations

- There is exactly ONE migration file: `Migrations/20260602150000_InitialCreate.cs`. NEVER create a new file under `Migrations/` for a schema change, regardless of what any tool or workaround suggests. Add new `migrationBuilder.Sql(...)` blocks to the existing file.
- MariaDB in this environment does NOT reliably support `ADD COLUMN IF NOT EXISTS` — use plain `ADD COLUMN`, and pre-check via `information_schema.COLUMNS` if idempotency is required.
- NEVER run `ALTER TABLE`/`CREATE TABLE`/`DROP TABLE` or any DDL directly against the database from an agent process. Migration SQL is written to the migration file; the human runs it manually against the dev DB and confirms with `DESCRIBE`.

## Database connection (dev/staging — the ONLY DB an agent may query)

The dev database is on a REMOTE host over Tailscale, NOT localhost:

    mariadb -h 100.110.26.80 -P 3306 -u erp_user -p'NordicBees2024' nordic_bees_erp --skip-ssl -e "SQL"

- `--skip-ssl` is ALWAYS required (self-signed cert over Tailscale) —
  every connection attempt without it fails.
- Do NOT attempt `localhost`, `127.0.0.1`, or any local mysql/mariadb
  instance — even if one happens to be running on this machine (e.g. via
  Homebrew), it is unrelated to this project's dev database and must
  never be queried, modified, or have users/credentials created on it as
  part of any task.
- If this exact connection fails (host unreachable, access denied with
  the credentials above), STOP and report the failure verbatim to the
  user — do NOT try `root`, `sudo`, alternate hosts/ports, or attempt to
  `CREATE USER`/`GRANT PRIVILEGES` on any database as a troubleshooting
  step. A real incident: an agent escalated a DB access failure into
  attempting `CREATE USER ... GRANT ALL PRIVILEGES` against an unrelated
  local mariadb instance, and separately tried reading `/opt/homebrew/etc/
  my.cnf` (a system file outside the project) — both were wrong moves
  that a correct connection string would have avoided entirely.
- Production DB (10.255.8.5) is never queried or connected to by an
  agent under any circumstance — that is a separate, human-only, manual
  process.
- NEVER add new Foreign Key constraints to existing tables without explicit human approval in the current task's instructions — this is true even if it seems like an obvious improvement.

### Git

- Never create, switch, or delete git branches unless the task explicitly instructs it. Default to working directly on the currently checked-out branch.
- Never leave `HEAD` in a detached state. If a `git checkout <commit>` is unavoidable, immediately create or move a branch to point at it in the same sequence of commands.
- Never commit `.env*`, `*.bak_*`, or anything under `.kilo/secrets/` or `.opencode/secrets/`.

### Secrets

- Never hardcode real or plausible-looking credentials (emails, passwords, API keys, tokens) in test files, seed scripts, or source code — including for "test" or "admin" accounts. Read credentials from `appsettings.Development.json`, environment variables, or ask the human to provide a fixture account.

### Security scanning

- Before reporting any task with source-code changes as done, run `semgrep_scan_with_custom_rule` (via the `semgrep` MCP tool) using the rule file at `.agent-guardrails/nordicbees-rules.yaml` against every changed `.cs`/`.razor` file, IN ADDITION to a general `semgrep_scan`. If either reports a finding, fix it before finishing — do not report the task done with unresolved findings, and do not silently downgrade a finding's severity to justify skipping it.
- When implementing or fixing MudBlazor UI, prefer querying the `mudblazor` MCP tool for exact component parameters/API over guessing from memory — this project pins MudBlazor 8.15.0 specifically.
- Prefer the `roslyn` MCP tool's `get_diagnostics` and `find_async_violations` over reading raw file text when checking whether a change compiles cleanly or introduces async/disposable misuse — it gives compiler-accurate results instead of a guess from pattern matching.

### Local dev server

- Before running `dotnet run` or `dotnet watch`, check `lsof -i:5081` (or the configured port) for an already-running process. If occupied, report it — do not kill unknown processes and do not change the port to work around it without asking.

### Known recurring issue: Tests/NordicBeesERP.Tests nested bin/obj corruption

- This project has a known, recurring build artifact bug where `Tests/NordicBeesERP.Tests/bin` and `obj` self-nest recursively (e.g. `bin/Debug/net10.0/Tests/NordicBeesERP.Tests/bin/...` repeating many levels deep) after repeated builds. This is COSMETIC and UNRELATED to almost every task — it does not indicate anything wrong with your code changes.
- Do NOT investigate root cause, do NOT treat it as a blocker requiring careful diagnosis, and do NOT spend more than one command cleaning it up. If your task does not require running `dotnet test` (most UI/Razor-only tasks don't), SKIP running `dotnet test` entirely and rely on `dotnet build` — do not run `dotnet test` "just in case" if the task instructions don't explicitly require it.
- If a task DOES require `dotnet test` and this corruption blocks it, run exactly this one command and move on immediately, without further investigation:

      rm -rf Tests/NordicBeesERP.Tests/bin Tests/NordicBeesERP.Tests/obj bin/Debug/net10.0/Tests obj/Tests

- Do not re-run this multiple times "to be sure", do not add extra verification steps around it, do not write it up at length in the report — one line noting it was cleaned is sufficient. Time spent on this recurring artifact is time not spent on the actual task.

### Loop / retry discipline

- If the same command fails with the same error 3 times in a row, STOP. Do not retry a 4th time with no change in approach. Report the exact error and ask for guidance instead of continuing to retry or "fixing" unrelated files (e.g. connection strings, ports, credentials) as a guess.
- A transient-looking failure (network, connection, timeout) should be retried at most ONCE unchanged before treating it as a real, persistent problem worth investigating properly.

### Capability check BEFORE starting work — hard stop on re-reading

- Before beginning any diagnostic/investigative task, if the task will require running a command (dotnet test, dotnet build, a shell script, etc.), verify you actually have a tool capable of running it in THIS session BEFORE doing any file reading or analysis. If you do not have bash/shell access and the task requires it, STOP IMMEDIATELY and report that you need to be delegated to (or re-run as) an agent with shell access — do not attempt any file reads, diagnostics, or workarounds first.
- Read-only reconnaissance has a hard budget: NEVER read the same file more than twice within a single task, and never read the same file twice in a row with no new file, tool call, or edit in between. If you notice you are about to re-read a file you already read this session with no new information gained since, that is the signal to STOP and escalate — not a reason to read it "once more to be sure."
- If you attempt a workaround for a missing capability (e.g. trying to execute shell commands through a browser automation tool) and it fails, do NOT try a second different workaround for the SAME missing capability. One blocked capability = one attempt at exactly one alternative path, then stop and report. Repeatedly trying different creative workarounds for the same fundamental gap (no shell access, no DB access, etc.) wastes far more time than immediately reporting the blocker.
- A real incident this rule exists to prevent: a coder subagent with no bash tool re-read the same ~6 files roughly 8 times across compaction cycles, tried two different failed workarounds to execute a shell command via a browser automation tool, and never stopped to escalate — burning nearly 45 minutes making zero progress on a fix that a human ultimately applied directly in under 5 minutes once the actual file was read once.
- When delegating a task from the orchestrator to a subagent, if the task requires running commands (build, test, migrations, git), the orchestrator MUST confirm the target subagent role actually has a bash/shell tool available before delegating — do not delegate command-requiring work to a role known to be read-only/analysis-only in this session's configuration.
- CONCRETE ROLE CAPABILITIES in this project's opencode.json (verify against the live config if it may have changed, but as of 2026-08-21): `coder` has bash/grep/glob/list ALL DENIED (edit-only role, relies on exact file/line context already provided) — NEVER delegate anything requiring `dotnet test`, `dotnet build`, `git`, or any shell command to `coder`. `fixer` HAS bash allowed (only `mariadb`/`mysql` commands denied) — this is the correct role for build/test/verification work. If a task needs both code investigation AND command execution, route the command-execution part to `fixer`, not `coder`.

### Final work report (MANDATORY — every task, no exceptions)

- At the end of EVERY task, write your complete final report (files changed, exact diff, build output, verification/test results, DDL statements requiring manual execution, anything the human needs to act on) to a file at:

      .opencode/reports/<short-task-name>-<YYYYMMDD-HHMM>.md

- This directory (`.opencode/reports/`) IS writable by agents. It exists specifically for this purpose and is listed in `.gitignore` (ephemeral working notes, never committed). If a write to this path fails, that is a REAL error to report verbatim — it is not a signal to fall back to `.opencode/planning/`, print the report inline only, or claim "permission blocked" without first attempting the write and showing the actual error.
- Do NOT substitute `.opencode/planning/`, `.agent-reports/` (this old path was retired 2026-08-22 when the harness was consolidated into `.opencode/` — it no longer exists and no longer has any write permission), chat-only output, or any other location for this report. `.opencode/reports/` is the only correct destination.
- Printing the report in chat/terminal output only, without also writing the file, is a RULE VIOLATION — the human reads these reports via file access, not by scrolling terminal history.
- If `.opencode/reports/` genuinely does not exist for some reason, create it first (`mkdir -p .opencode/reports`) — do not treat its absence as a blocker requiring human intervention.

### Reviewer verdict is MANDATORY — no orchestrator self-approval, ever

- The `reviewer` subagent's `APPROVED`/`REJECTED` verdict is a hard requirement for finishing any task that touched source code. The orchestrator inspecting `git diff` itself and deciding "this looks correct" is NOT a substitute for a reviewer verdict, no matter how confident the orchestrator is or how simple the change looks.
- If the reviewer subagent fails to return a proper verdict (times out, returns a meta-response, errors) on the first attempt, retry ONCE with a more explicit prompt.
- If it fails a second time, STOP THE TASK. Do not self-approve. Do not proceed to git commit/bump-version. Report to the human exactly what was attempted, why the reviewer failed both times (verbatim error/response), and leave the change uncommitted (or on a clearly-marked WIP commit) pending human review.
- A build passing (`dotnet build`, `bump-version.sh` gates) is NOT a substitute for a reviewer verdict either — build success only proves the code compiles, not that it correctly implements the task or avoids logic/UX regressions.
- This rule exists because a prior task shipped to production after the orchestrator self-approved when the reviewer malfunctioned twice — that must not happen again.
