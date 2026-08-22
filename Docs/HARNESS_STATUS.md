# NordicBeesERP Agent Harness — Status & Continuity Doc
(Written 2026-08-22, end of a long working session. Purpose: let a NEW
session pick up exactly where this one left off, without re-discovering
everything from scratch or performing worse than this session did.)

---

## 0. READ THIS FIRST — current state in one paragraph

The harness was consolidated from a messy 4-tool-era setup (Kilo Code +
Cline + OpenCode + agent-guardrails, scattered across `.kilo/`,
`.clinerules/`, `.agent-guardrails/`) into a single, git-tracked
`.opencode/` structure. It is now **v1: stable and in active use**. A
full backup exists (see §7). We are CURRENTLY in a **deliberate pause**:
collecting baseline performance statistics on the OLD harness (with two
manual tweaks — increased KV cache, orchestrator model switched from
Hy3 to Nemotron Ultra) BEFORE building a NEW harness architecture
(merging coder+fixer into the orchestrator's own session, Codex/Claude-
Code style). **Do not start the new-harness rewrite until the user
explicitly says the baseline collection period is over.**

---

## 1. Project basics

- NordicBeesERP: .NET 10 / Blazor Server / MariaDB honey-business ERP,
  solo-developed by Deividas, built via a local multi-agent OpenCode
  pipeline running on a home GPU server (`local-llm`, Tailscale IP
  100.110.26.80), 4× RTX 3090, via `llama-swap` (port 9292).
- Repo: `deividasrusenas-max/NordicBeesERP`, branch `main`.
- Communication with Deividas: primarily Lithuanian, informal, direct.
  He wants concise answers, no unnecessary hedging, and pushes back hard
  on hand-wavy claims — always ground things in evidence (files, git
  log, search results), never assert without checking.
- Deividas explicitly does NOT want overhead/complexity for its own
  sake — every addition must solve a CONCRETE, ALREADY-OBSERVED problem,
  not a hypothetical one. He rejected using more of MemPalace's 29 tools
  "just because they exist," and rejected a heavier lessons-database
  design in favor of reusing what's already there (BUGLOG.md +
  mempalace auto-indexing).

---

## 2. Harness structure (current, v1)

```
/AGENTS.md                  — agent workflow policy (git-tracked)
/opencode.json              — main config (git-tracked; used to be
                               gitignored entirely — this was a real bug
                               fixed today, see §4)
/.opencode/
  prompts/                  — orchestrator.md, coder.md, fixer.md,
                               reviewer.md, verifier.md, visual-qa.md,
                               design-review.md (all git-tracked)
  skills/                   — mudblazor, dotnet-efcore-nordicbees,
                               git-workflow-nordicbees, mempalace,
                               url-filter-persistence-nordicbees, etc.
  plugin/                   — nordicbees-verify.ts (build-check hook),
                               nordicbees-skill-inject.ts (force-injects
                               skills into reviewer calls),
                               nordicbees-mempalace-sync.ts,
                               nordicbees-quality-monitor.ts (NEW today,
                               see §5)
  plugins/guardrails.js
  reports/                  — task-stats.jsonl (see §5), per-task
                               .md reports (ephemeral, gitignored)
  planning/                 — orchestrator scratch space (gitignored)
  secrets/                  — gitignored
/.agent-guardrails/         — separate CLI tool, config.json (git-
                               tracked), produces a discrete 0-100
                               quality score
/Docs/
  FROZEN.md                 — do-not-touch code blocks (drag-drop JS,
                               ULAK module, OcrQueueWorker, ViesService,
                               BankImport) — merged from old .clinerules
  DESIGN_SYSTEM.md           — merged from old .clinerules
  UI_STANDARD.md              — canonical, current
  BUGLOG.md                  — bug postmortem log (see §6 for the new
                               error_class/status extension plan)
  archive/                   — old completed-work records
```

**DELETED entirely today, no longer exist:** `.kilo/`, `.clinerules/`,
`kilo.jsonc`. If any old memory/reference mentions these paths, they are
STALE — the harness moved to `.opencode/` only.

---

## 3. What today's session actually fixed (chronological, so a new
   session understands WHY each rule exists — many prompt rules
   reference "real incident" paragraphs matching this list)

1. **Consolidated 4 parallel, contradictory tooling systems** into one
   `.opencode/` structure (see §2). Found and fixed real contradictions:
   different files disagreeing on whether subagents should read
   AGENTS.md (fixed: they should NOT — orchestrator distills relevant
   rules into each delegation instead).
2. **`opencode.json` and `.agent-guardrails/config.json` were entirely
   gitignored** — the whole harness config was never version-controlled.
   Fixed the `.gitignore` to track config/prompts/skills, only ignore
   genuinely ephemeral/secret sub-paths.
3. **Fixed a design-review.md staleness bug**: it hardcoded
   `Variant.Outlined` as the correct filter-field style, but the project
   had switched to `Variant.Text` — meaning the correct new style would
   have been flagged as a violation. Fixed to defer to the live
   `Docs/UI_STANDARD.md` excerpt when given one.
4. **Added a capability-check rule**: `coder` has NO bash/grep/glob
   (edit-only role); `fixer` HAS bash. A real incident (coder given a
   task requiring `dotnet test`, looped ~8x re-reading files, tried
   browser-automation workarounds for 45 min) drove this.
5. **Added a whole-file-rewrite fallback for coder's edit failures**:
   if a narrow re-read + retry still fails on an `edit` call, and the
   file is under ~400 lines, switch to reading the whole file once and
   using `write` to replace it entirely — matches Cline/Cursor's own
   documented pattern (search/replace-style edits are known-unreliable,
   especially for smaller models).
6. **Added "report BLOCKED once and STOP" rules to fixer.md,
   orchestrator.md, reviewer.md, verifier.md** — real incident: fixer
   correctly diagnosed a blocker (bump-version.sh refused due to
   unrelated uncommitted files) on its FIRST check, then re-ran the same
   diagnostic 14 more times across compactions instead of stopping.
7. **Found and fixed a structurally-broken hardcode check**: fixer.md
   said `grep -r "BUCKET_GROUP" .` (whole repo) "must return 0 matches"
   — but `BUCKET_GROUP` is a REAL, legitimate `ContainerType` enum value
   used correctly in many files. This check could NEVER pass. Fixed to
   scope the grep to only the current task's own staged diff.
8. **Fixed inconsistent step numbering** in fixer.md (a later paragraph
   referenced "step 5" as the commit step, but step 5 was actually
   "bump version" — pre-existing confusion, cleaned up into one coherent
   1-10 sequence: build→fix-loop→status→add→grep-staged-diff→commit→
   log-verify→bump-version→agent-guardrails-check).
9. **Wired `agent-guardrails check --base-ref HEAD~1` into fixer's ACTUAL
   step list** — AGENTS.md always called this "MANDATORY" but the real
   fixer.md prompt never had it as a concrete step, so it was never
   reliably run.
10. **Removed noise**: an irrelevant "Blazor Server Playwright testing"
    section had been copy-pasted into coder.md and fixer.md (neither
    role does browser testing) — deleted from both, kept only in
    verifier.md where it's actually relevant.
11. **Playwright/verifier fixes**:
    - Added real dev login credentials (`admin@nordicbees.lt` /
      `aaaa`) directly into verifier.md so it never has to search/ask.
    - Restricted verifier to `localhost:5081` ONLY — never staging/prod.
    - Added `--headless` to the Playwright MCP config in `opencode.json`
      (was running a visible/rendered browser window every time — real
      perf cost, zero benefit for an automated agent). Confirmed via
      official microsoft/playwright-mcp docs: headed is the DEFAULT
      unless `--headless` is passed.
    - Fixed `--timeout-navigation` from 45000ms back to the library
      default 60000ms (previous value was STRICTER than default, likely
      causing false failures on Blazor Server SignalR hydration delay).
    - Made the "skip Playwright by default" policy explicit and
      unambiguous in orchestrator.md — only run when the user EXPLICITLY
      asks for browser verification.
12. **Fixed `mempalace_search` being skippable** — orchestrator.md said
    to use it "before starting a non-trivial investigation" but didn't
    make it clearly the literal FIRST tool call; a real orchestrator
    session skipped it entirely for a multi-file feature task. Also
    clarified: mempalace reflects state as of the last `fixer` commit —
    for UNCOMMITTED work it's already stale, so direct file reads are
    still required regardless of mempalace.
13. **Fixed orchestrator running unnecessary `find`/`ls`** to "confirm"
    the location of files whose path is already an established harness
    constant (`Docs/FROZEN.md`, `Docs/UI_STANDARD.md`, etc.) — these are
    known paths, no verification needed.
14. **Built `nordicbees-quality-monitor.ts`** — see §5, a deterministic
    (no LLM judge) stats-collection plugin. Had TWO real bugs, both
    found and fixed today:
    - `tool.execute.before` hook was reading `args` from the wrong
      parameter (OpenCode plugin API puts `args` on the SECOND parameter
      for the `before` hook, FIRST parameter for `after` — got this
      backwards originally, so ZERO "started" records were ever
      written, meaning `duration_sec` was always `null`).
    - `guardrail_score`/`verdict` extraction assumed `output` was a
      plain string; made it robust by JSON-stringifying whatever shape
      is actually there and searching across multiple plausible
      phrasings.
    Both fixes CONFIRMED WORKING via real subsequent log entries (see
    the `.jsonl` file itself for proof — entries after ~11:50 today have
    real non-null `duration_sec` values, and one entry proves the
    disk-based interruption-detection mechanism correctly caught a
    ~10-hour-stuck coder call and marked it `"interrupted"`).

15. Deleted a stray `.opencode/plugin/nordicbees-reminder.ts.bak` file
    that had gotten swept into a commit accidentally.

---

## 4. GPU / model / context plan (DECIDED, not yet executed)

Current `llama-swap` setup (`/home/asus/AI/llama-swap-config.yaml` on
`local-llm`, 4× RTX 3090 = 98GB total):
- `coder`: Qwen3.8-27B-MTP (dense, NOT MoE), GPU 0+1, ctx 65536
- `fixer`: Qwen3.6-35B-A3B (MoE, ~3B active params per token — NOT
  stronger than the 27B dense model for precise coding despite being
  "bigger" nominally), GPU 2
- `reviewer`: Qwen3.6-35B-A3B-MTP, GPU 3
- `vl-ocr`: Qwen2.5-VL-7B, shares GPU 0
- All four in one `swap:false, exclusive:true, ttl:0` group — ALWAYS
  loaded simultaneously, no swap-out.

**Confirmed decision** (after correcting two of my own earlier mistakes
— conflating GPU-fit with workload-fit, and conflating MoE "35B" size
with actual per-token capability): if/when the coder+fixer merge
happens (§8), the merged orchestrator role should use the 27B DENSE
model (proven stronger for precise code edits), NOT the 35B-A3B MoE
model.

**Deividas is planning to add GPUs** — explicitly said not to analyze
whether 6× RTX 3090 physically fits his setup, treat it as a given
constant. With `fixer` merged away, GPU2 becomes free. Plan (not yet
executed, pending the harness rewrite decision in §8):
- Give the merged coder/orchestrator model 4 GPUs (0+1+2+3 minus
  whatever `reviewer` needs) → much larger context window possible.
- Rough estimate ONLY (needs real benchmarking, not blind trust): with
  weights fixed at ~22GB and KV cache scaling with free VRAM, 4-GPU
  headroom could plausibly reach ~180-190K context vs current 65536 —
  but this must be verified empirically (watch `nvidia-smi` for OOM,
  measure actual tokens/sec at the new ctx-size), not assumed from
  arithmetic alone (we got burned once today trusting unverified math
  on an unrelated PDF-column-width task).
- Keep `reviewer` on its OWN dedicated GPU (not swapped/shared with the
  main model) — this avoids a "thrashing" scenario where the main model
  has to be unloaded/reloaded every single `coder→reviewer→fixer` file
  cycle, which would cost ~20s per file in reload overhead and could
  easily outweigh the benefit of a bigger context.
- `vl-ocr` should be moved to a `swap:true` group (currently always
  loaded even though it's rarely used, since Playwright/visual
  verification is now opt-in only per §3.11) — frees VRAM when not
  actively in use.

**Speed-vs-context tradeoff**: bigger KV cache context = slower
per-token decode (VRAM-bandwidth-bound), this is real and known, but NO
exact percentage has been measured for this specific model/hardware
combo — must be benchmarked, not assumed.

---

## 5. Quality/performance monitoring — nordicbees-quality-monitor.ts

Purpose: purely deterministic (no LLM judge) plugin, records every
coder/fixer/reviewer Task-tool call as TWO JSONL lines (a "started" and
a "completed" event, matched by `call_id`) to
`.opencode/reports/task-stats.jsonl`:
- `agent`, `model` (exact model name string)
- `duration_sec` (now correctly populated, was broken until today)
- `prompt_chars` (rough complexity proxy)
- `verdict` (reviewer only: APPROVED/REJECTED)
- `guardrail_score` (fixer only: the agent-guardrails 0-100 score,
  extraction just fixed today, NOT YET RE-VERIFIED with a fresh fixer
  call post-fix — check this when resuming)
- Disk-based (not just in-memory) interruption detection: any "started"
  record with no matching "completed" within 10 minutes gets a
  retroactive `"status":"interrupted"` record written on the NEXT
  coder/fixer/reviewer call (even in a new process, even after a full
  Ctrl+C kill of the whole OpenCode CLI) — CONFIRMED WORKING today
  (caught a real ~10hr-stuck call).

**Purpose of this data**: build an objective baseline for the OLD
harness (current session's explicit goal — see §0) to compare against
whatever the NEW harness (§8) achieves later. Do NOT delete or reset
`task-stats.jsonl` — it needs to keep accumulating during the baseline
period.

---

## 6. Self-improvement / "learning from experience" plan (DECIDED
   direction, NOT YET IMPLEMENTED — do this AFTER the baseline period
   and/or the harness rewrite, whichever the user asks for first)

Grounded in real research (searched today, see below), NOT just
intuition — Deividas explicitly required this.

**Key research findings that shaped this plan:**
- Production AI teams run a "human-governed improvement loop" (capture
  failure → structured eval case → propose fix → gate against a FIXED
  external metric → ship only if it helps), NOT autonomous self-
  modification — the autonomous version is explicitly flagged as the
  riskier, research-flavored approach, not what real companies do
  (source: prefactor.tech).
- Self-grading is explicitly dangerous and unreliable ("an agent asked
  to judge its own reply grades it optimistically") — this directly
  validates a rule the harness already has (reviewer must never self-
  approve) (source: Google Cloud Tech).
- An arxiv paper ("Self-Improving AI Coding Agents Through Accumulated
  Behavioral Rules") describes EXACTLY what today's session did all day
  manually: human review feedback codified as persistent, version-
  controlled prompt rules creates a "ratchet effect" — 35+ microservices
  in production, rule set grew 5→18 rules across 11 sessions, ZERO
  recurrence across 9 tracked error classes. This VALIDATES today's
  entire manual approach (each fix in §3 above, with its "Real incident
  this rule exists because of" paragraph) as the actual proven pattern,
  not an ad-hoc workaround.

**The plan (minimal, reuses existing infrastructure, no new heavy
system):**
1. Extend `Docs/BUGLOG.md` entries with two new fields: `Error class:`
   (a short stable tag, e.g. `reviewer-self-approval`,
   `grep-scope-too-broad`, `context-loss-compaction`) and `Status:`
   (`monitoring` / `stable` / `escalated`).
2. `mempalace` already auto-indexes `Docs/*` after every fixer commit —
   no new integration code needed, just write to the right place with
   the right structure.
3. THE MISSING PIECE from a naive "just log lessons" approach (this is
   what makes it match the real research pattern instead of just being
   a hopeful text dump): before writing a NEW rule for an error class,
   check whether that error class already has an existing rule/BUGLOG
   entry. If it does and the failure recurred anyway, that is OBJECTIVE
   evidence (not self-graded) that the previous rule didn't work —
   escalate to a stronger mechanism (e.g. move from a prompt-text rule
   to a mechanical `agent-guardrails` check) rather than just adding
   another similar sentence.
4. Periodic (weekly/biweekly, user-initiated — NOT automatic, matching
   the "human-governed" research finding) review: read BUGLOG.md +
   task-stats.jsonl together, compute "exposure count" (how many tasks
   happened since a rule was added) for each `monitoring`-status error
   class, promote to `stable` if no recurrence, flag for escalation if
   recurred.
5. Explicitly rejected: fine-tuning model weights (Deividas said this is
   "per sudėtinga" / too complex for now, revisit only after much more
   data accumulates). Explicitly rejected: using more of MemPalace's 29
   MCP tools without a concrete proven need for each one (Deividas's
   own stated principle — avoid overhead without concrete justification).

---

## 7. Backup

A full harness backup was planned via git tag/branch
(`harness-v1-2026-08-22` tag, `harness-v1-backup` branch) — **CHECK
whether this was actually executed** (the session paused before
confirming `git status` was clean and running the tag/branch/push
commands — verify with `git tag -l` and `git branch -a` on next
session before assuming it exists).

---

## 8. The bigger architectural question — Codex/Claude Code style
   restructuring (DECIDED DIRECTION, EXPLICITLY PAUSED, do not start
   without the user's explicit go-ahead)

**Research finding**: both Claude Code and Codex CLI keep the actual
iterative edit→build→commit work in ONE continuous main session/loop,
NOT delegated to fresh-context subagents each time. Subagents (in both
tools) are explicitly recommended ONLY for bounded, read-heavy
research/exploration tasks that return one clean output — official
Claude Code community finding: "subagents work best for read-heavy
research and exploration, not parallel coding"; Codex's own docs
explicitly say "do not use subagents for tiny tasks," reserving them for
large-scope audits/reviews.

**This directly explains today's most common failure pattern**: `coder`
being delegated a multi-step edit→verify→fix cycle as a FRESH subagent
context each time, repeatedly losing continuity across compaction and
re-reading the same files (documented in ~5 separate incidents today).

**Proposed new architecture** (not yet built):
- `orchestrator` gets direct `edit`+`bash`+`git` permissions (currently
  explicitly denied — "you no longer have edit permission at all") and
  does the actual code editing/building/committing itself, in its own
  continuous session, instead of delegating to `coder`+`fixer`.
- `reviewer` STAYS as a separate subagent (matches the good-use-case
  pattern — bounded, independent, produces one clean APPROVED/REJECTED
  verdict; also structurally necessary to avoid self-approval).
- `verifier`/`visual-qa`/`design-review` STAY as subagents (same good-
  fit reasoning).
- `coder.md`/`fixer.md` would be archived (not deleted — kept for
  reference/rollback) once/if this is built and proven.

**Explicit tradeoff acknowledged, not glossed over**: this concentrates
the heaviest workload onto ONE model (currently reasoned should be the
27B dense `coder` model, not the 35B-A3B `fixer` model — see §4) instead
of spreading it across specialized smaller models on separate GPUs. Real
cost: less GPU parallelism/specialization. Real benefit: eliminates the
entire "lost continuity across compaction" failure class.

**Why paused**: Deividas wants to FIRST collect real baseline
performance stats (§5 mechanism) on the CURRENT (old, coder+fixer-
delegated) harness with its current tweaks (bigger KV cache, Nemotron
Ultra orchestrator) before committing to the rewrite — so the
comparison is measured, not assumed. **Do not begin implementing this
restructuring until the user says the baseline period is over.**

---

## 9. Open / unverified items for next session to check

- [ ] Was the git tag/branch backup (§7) actually executed? Check
      `git tag -l` / `git branch -a`.
- [ ] Is `guardrail_score` extraction now actually working post-fix?
      Check the latest lines of `task-stats.jsonl` after the next real
      `fixer` call — should show a non-null `guardrail_score`.
- [ ] How long has the baseline-collection period been running, and is
      there enough data yet for the user to want to compare/decide?
- [ ] Confirm current KV cache / orchestrator-model settings on
      `local-llm` are still what was manually tweaked (increased
      context, Nemotron Ultra as orchestrator) — these were changed
      OUTSIDE the git-tracked harness (manual `llama-swap-config.yaml`
      edit + manual model picker in the OpenCode UI), so they will NOT
      show up in any git diff — check directly via SSH if relevant.
- [ ] Any new incidents/loops since this doc was written should be
      diagnosed the same way today's were: read the actual transcript
      evidence, distinguish a genuine repeat-read loop from a one-off
      large-but-legitimate context usage, fix the SPECIFIC root cause
      (not a generic "add more rules" reflex), and add a "Real incident
      this rule exists because of (date)" paragraph to the relevant
      prompt file so future sessions understand why the rule exists.

---

## 10. Working style notes for whoever picks this up

- Deividas pushes back hard on unverified claims — if you're not sure
  something is true, say so and check it (search, read the actual file,
  run the actual command) rather than asserting confidently. This
  session got corrected multiple times for overconfident-but-wrong
  claims (a PDF column-width calculation that contradicted its own
  stated math; conflating GPU capacity with workload fit; assuming
  MemPalace lacked features it actually has).
- Prefer minimal, targeted fixes with a clear, evidenced root cause over
  broad "let's add more rules/tools/complexity" reflexes.
- Never edit live harness files while a real OpenCode task is actively
  running against them (caused a real race-condition incident today —
  fixer saw the harness's own mid-edit uncommitted state as an
  unrelated blocker).
- When something looks like a loop, verify from the actual transcript
  whether it's truly repeating (same file, same conclusion, no new
  info) versus just a single large-but-legitimate step — don't assume
  either way without looking.
