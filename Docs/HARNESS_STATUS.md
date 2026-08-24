# NordicBeesERP Agent Harness — Status & Continuity Doc
(Written 2026-08-22, end of a long working session. Purpose: let a NEW
session pick up exactly where this one left off, without re-discovering
everything from scratch or performing worse than this session did.)

---

## 0. READ THIS FIRST — current state in one paragraph

The harness was consolidated from a messy 4-tool-era setup (Kilo Code +
Cline + OpenCode + agent-guardrails, scattered across `.kilo/`,
`.clinerules/`, `.agent-guardrails/`) into a single, git-tracked
`.opencode/` structure. It is now **v1: stable and in active use**, and
has been through TWO further rounds of incident-driven hardening since
(§11, §12). A full backup was PLANNED via git tag/branch but **still not
confirmed executed** as of this writing (see §7 — check this FIRST on
resume). We are CURRENTLY in a **deliberate pause**: collecting baseline
performance statistics on the OLD harness (coder+fixer as separate
subagents) BEFORE building a NEW harness architecture (merging
coder+fixer into the orchestrator's own session, Codex/Claude-Code
style, §8). **Do not start the new-harness rewrite until the user
explicitly says the baseline collection period is over.**

**Current confirmed-working GPU/context state (2026-08-24, see §12 for
the full story)**: `coder` runs at `--ctx-size 262144` (256K) with
`--cache-type-k q4_0 --cache-type-v q4_0` — this is a real, working
config (confirmed via `llama-server`'s own prefill logs showing normal
~1500-1600 tok/s processing), NOT just the 128K/q8_0 value from
2026-08-23 which has since been superseded. **A critical, high-impact
bug was found and fixed today**: `opencode.json`'s declared
`coder.limit.context` was STILL hardcoded at the stale `65536` value
this whole time, completely independent of whatever the real
`llama-server` ctx-size actually was — meaning OpenCode had been
triggering `Compaction` against the WRONG, much smaller limit
ALL DAY YESTERDAY AND TODAY, regardless of any server-side context
increase. Fixed to `262144` today (§12) — this may be the single highest-
impact fix in the whole session, since it was silently capping every
session's effective context far below what the hardware could actually
handle.

**Also as of today**: extensive further research into eval-driven
harness development has been done and written to a SEPARATE file,
`Docs/QUALITY_PLAN.md` (§12 links it) — read that file too when
resuming, it has detailed, first-party methodology from Anthropic's own
engineering blog plus current academic literature, and is NOT
duplicated in full here.

**Recommendation for whoever resumes**: this particular chat session
(where most of §11-§13 happened) has grown extremely long, and every
further message in it re-sends the entire day's history as context —
genuinely expensive. For focused, mechanical harness/plugin EDITING work
going forward (e.g. implementing the `n_toolcalls` tracking gap in
§12), use **Claude Code** (terminal or desktop app) in a fresh, narrow
session instead of continuing here — this was explicitly discussed and
agreed with the user today (§12). Reserve long chat sessions like this
one for research, planning, and multi-step diagnosis, not repetitive
plugin/prompt editing.

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

---

## 11. Day 2 (2026-08-23) — what happened on top of the above

**BUGLOG.md retrofitted with Error class/Status** (§6's plan executed):
all 11 historical entries + 2 new harness-bug entries now tagged. Key
finding from retrofit: **5 error classes are currently `Status: N/A`
(unguarded)** — these are the highest-priority candidates for an actual
guardrail next: `status-transition-incomplete-allowlist`,
`schema-drift-unverified-column-mapping`,
`blazor-forceload-fullreload-auth-redirect`,
`layout-lifecycle-unguarded-db-call`,
`mudblazor-autocomplete-tab-value-commit`.

**Orchestrator attempted direct `write`/`edit` on source files TWICE**
today (`InvoiceEdit.razor`, both times), despite an explicit prompt rule
saying not to. Both times the `opencode.json` permission config
correctly DENIED the attempt (confirms the config-level block works),
but real time (2+ minutes each) was wasted reasoning toward/attempting
something guaranteed to fail before falling back to delegating to
`coder`. Strengthened `orchestrator.md`'s wording at the exact
analysis→implementation transition point after the first incident — the
SECOND incident happened anyway (likely because that session had
already loaded the OLD prompt text into its context before the fix was
committed — prompt files load at session start, not live). Investigated
whether OpenCode's `tools: {edit: false}` agent-level field could fully
remove the temptation (not just deny at call time) — **rejected**: that
field is all-or-nothing per agent, but orchestrator has LEGITIMATE
narrow edit access (`Docs/BUGLOG.md`, `.opencode/reports/`,
`.opencode/planning/`, `.agent-guardrails/evidence/`) that would break
too. The current path-scoped `permission` config remains the only clean
mechanism for this mixed legitimate/illegitimate case. Conclusion: this
recurring waste is an ADDITIONAL argument for the §8 architecture change
(if orchestrator legitimately gets edit permission via the merge, the
"attempt gets denied" failure class disappears structurally rather than
needing prevention) — noted as supporting evidence, not a decision to
act on yet.

**Mandatory upfront task decomposition added to `orchestrator.md`**: the
user wants to paste ONE large multi-file task description (not manually
pre-split into "Prompt 1/2, 2/2") and have the orchestrator decompose it
automatically — both per-file AND per-distinct-part-within-a-file (>3
distinct changes in one file triggers a mandatory pre-split into
multiple rounds), via a `todowrite` planning pass BEFORE any Task tool
call. Real incident: a 7-file task was correctly split one-todo-per-file,
but the most complex single file (5 distinct sub-changes) was delegated
to `coder` as ONE call that ran 45 minutes and hit ~90K prompt_chars
(exceeding the 65536-token context). Not yet tested against a fresh
multi-file prompt — verify this actually produces fine-grained todos
next time a big task is given.

**`AGENTS.md` found stale, fixed**: its "Final work report" section
still pointed agents at the OLD, deleted `.agent-reports/` path (from
before the harness consolidation) instead of the current
`.opencode/reports/`. An orchestrator session correctly caught the
inconsistency (AGENTS.md vs actual opencode.json permissions) but
proposed the WRONG fix direction (re-add `.agent-reports/` to the
whitelist, resurrecting the old structure) — corrected AGENTS.md instead
to match the new, intentional structure. Lesson: when a subagent/
orchestrator reports "config X and doc Y disagree", always check WHICH
one is the stale one before applying its suggested fix — don't assume
the newer-looking config is wrong just because an inconsistency was
found.

**`llama-swap` `coder` model crashed and was recovered** — root cause
was this morning's manual KV-cache increase (made outside the git-
tracked harness, directly in `/home/asus/AI/llama-swap-config.yaml` on
`local-llm`) pushing VRAM past a safe margin, causing
`llama-server` to exit prematurely on load (`journalctl` showed clean
operation until 11:50, then repeated "upstream command exited
prematurely" for the `coder` model specifically). Recovery: reverted to
a KNOWN-SAFE value, then carefully re-tested at **`--ctx-size 131072`
(128K)`, CONFIRMED WORKING** — `curl http://localhost:9292/v1/models`
shows `coder` reaching `"status":"loaded"` cleanly, and
`nvidia-smi` shows coder (GPU0+1) using ~39GB/49GB (17312+21676 MiB) —
a safe margin, not maxed out. **This 128K value is the new confirmed-
working baseline for `coder`'s ctx-size** — do not casually increase
further without the same careful backup+direct-launch+nvidia-smi-watch
protocol used to find this value. The 4-GPU/180-190K estimate in §4 was
NOT re-validated with real hardware today (still just arithmetic) —
128K/2-GPU is the only number with real empirical confirmation as of
this writing.

**KV cache quantization research (NOT yet acted on, queued for after
the current task finishes)**: TurboQuant is confirmed a dead end for
this project — its mainline PR (#21089) is now CLOSED (rejected, not
just pending), and a very recent (6-days-old as of 2026-08-23)
`ggml-org/llama.cpp` discussion (#23470) reports that even where tested,
TurboQuant "neither rescued the collapsing model" for a sensitive case.
HOWEVER, the same discussion has genuinely useful, MODEL-SPECIFIC data:
switching KV cache from the current `q8_0` to `q4_0` was tested on
`Qwen3.8-27B` (this project's exact `coder` model) with only **3/500**
answers changed, and `Qwen3.6-27B` (same family) at **2/500** — both
near-zero degradation, versus some other model families collapsing
badly at `q4_0` (e.g. Qwen2.5-7B: 375/500, worse than random). Since
`q4_0` KV roughly halves memory versus the current `q8_0`, this could
plausibly let `coder` reach ~256K context (the user's original target)
on the SAME 2-GPU allocation, or free real VRAM margin at the current
128K. **User explicitly decided: do not touch this today, revisit after
the current OpenCode task finishes**, using the same careful protocol
that found the 128K value (backup config first, direct `llama-server`
launch to see real errors before trusting `llama-swap`'s opaque "exited
prematurely", watch `nvidia-smi` during load, confirm via
`curl .../v1/models` reaching `"loaded"` before declaring success).

**Open item carried forward, still not done**: the git tag/branch
backup from §7 is still unconfirmed — check `git tag -l` and
`git branch -a` before assuming `harness-v1-2026-08-22` exists.

---

## 12. Day 3 (2026-08-24) — a very long research-and-hardening session

### Two more scope-misrouting loop incidents, both fixed with a universal pattern

1. A `reviewer` REJECTED finding (duplicate `@code` blocks in
   `CreditNoteEdit.razor`) was routed by `orchestrator` DIRECTLY to
   `fixer` with instructions to "refactor"/"consolidate" — this
   violates an EXISTING rule (REJECTED must go back through `coder`,
   never straight to `fixer`). `fixer`'s own steps have no refactoring
   step, so it looped `git status`/`git log` 10 times with no valid next
   action, never refusing.
2. Separately, after a task's real work was already fully complete
   (build passed, commit made, version bumped, guardrails passed),
   `orchestrator` asked `fixer` to "generate and save the final
   deployment report" as a file — file-report-writing is
   `orchestrator`'s own job per `AGENTS.md`, not `fixer`'s. `fixer`
   looped `git log --oneline -5` 12+ times, same unchanging info every
   time, because "write a report file" was never one of its 10 defined
   steps.

**Root fix, deliberately made MINIMAL and ADDITIVE** (the user
explicitly warned against another destabilizing rewrite like the one
that caused problems previously — see §10): added a short, explicit
"GENERAL FALLBACK" block to `coder.md`, `fixer.md`, `verifier.md` (each
already had the SPECIFIC incident-driven rules; this generalizes the
principle: "if a task doesn't match my defined scope, say so plainly and
STOP — never loop re-verifying facts you already have hoping a path
appears"). `reviewer.md`/`visual-qa.md`/`design-review.md` were checked
and already had adequate structural protection (mandatory verdict
requirement, single-shot design) — left unchanged. `orchestrator.md`
got a NEW pre-delegation scope check: before every delegation, confirm
the specific ask actually matches the target agent's defined job —
catches exactly the two incidents above at the SOURCE, before wasting a
round-trip.

### A genuinely serious, different-in-kind incident: hallucinated garbled text in production error messages

From a prior session's "zero-price/zero-total" validation task,
`coder` had written syntactically-valid but semantically GARBLED,
non-language text into actual `_errorMessage` string literals in
`InvoiceCreate.razor` — e.g. `"Eilutę '{...}' tunga nukulė i negambēk
kiyu."` — not real Lithuanian, not English, pure hallucinated word-salad.
`reviewer` APPROVED this diff anyway (it was already committed and
LIVE, would show this garbled text to real users on validation failure)
— this is objectively, trivially checkable (unlike prior subjective
review misses) and reviewer still missed it, which is more concerning
than the earlier fabricated-citation incident.

**Immediate action**: a separate fix task was started by the user
(outside this harness-focused conversation) to correct the actual
garbled strings — check `git log` for a subsequent commit addressing
`InvoiceCreate.razor`'s `ValidateInvoice()` error messages; if none
exists yet, this is still an ACTIVE, LIVE bug in production-facing text
and should be prioritized.

**Harness fix**: added an explicit check to `reviewer.md`'s Mode A
checklist — item (e): any new/changed user-facing string literal must
be coherent, real language text; REJECTED immediately if not, "no
exceptions, this is not a judgment call" (deliberately worded to NOT be
a soft/subjective check like the duplicate-logic one, since this
specific failure mode is objectively detectable and should never again
slip past a review).

**Open hypothesis, NOT confirmed**: this incident happened the SAME DAY
the `coder` model's KV cache was switched to `q4_0` quantization (see
below). Benchmarks found earlier showed near-zero degradation for this
exact model family (`Qwen3.8-27B`, 3/500 answers changed) — but that
benchmark was for factual QA-style correctness, not necessarily for
generating clean natural-language string content under complex coding-
task conditions. **If garbled/hallucinated text appears again in a
future session, treat this as real evidence and revert `coder`'s KV
cache from `q4_0` back to `q8_0`**, accepting the smaller context in
exchange for correctness — this has NOT recurred yet as of this
writing, so it remains an open, unconfirmed hypothesis, not a decided
action.

### The GPU/context saga, continued and mostly resolved

Starting state this morning: `coder` running at the 128K/q8_0 value
confirmed working in §11, but with KV cache manually increased further
at some point without being carefully validated.

1. **`llama-swap` crashed** — `coder` failed to load with
   `"upstream command exited prematurely"`, confirmed via
   `journalctl -u llama-swap`: clean operation until ~11:50, then
   repeated failures specifically for `coder`. Root cause: this
   morning's further manual KV-cache increase (made directly in
   `/home/asus/AI/llama-swap-config.yaml`, OUTSIDE git, per usual for
   this kind of GPU tuning) pushed VRAM past a safe margin.
2. **Recovery attempted at 128K again** — confirmed working via
   `curl http://localhost:9292/v1/models` reaching `"status":"loaded"`.
3. **User then deliberately tried 256K (`--ctx-size 262144`)** — this
   loaded but logged a real warning during compute-buffer allocation:
   `cudaMalloc failed: out of memory` on GPU1, followed by an automatic
   fallback (`retrying without pipeline parallelism`) that DID succeed.
   `nvidia-smi` showed GPU1 at `23318MiB / 24576MiB` — only ~1.2GB free,
   uncomfortably tight margin, not a confident safe state.
4. **User independently also switched `--cache-type-k`/`-v` to `q4_0`**
   (halves KV cache memory vs the prior `q8_0`) to give real headroom at
   256K. **This combination (256K + q4_0) is CONFIRMED WORKING** — a
   real `coder` invocation processed a genuine prompt with normal
   prefill throughput (~1500-1600 tokens/sec, matching earlier healthy
   baselines) and no further OOM warnings reported.
5. **THE CRITICAL FIND**: `opencode.json`'s own
   `provider.llama-swap.models.coder.limit.context` field was STILL
   `65536` this entire time — a value OpenCode itself uses to decide
   when to trigger `Compaction`, completely independent of whatever the
   real `llama-server` could actually handle. This means from
   2026-08-23 through most of 2026-08-24, EVERY context increase made
   on the server side was silently ineffective from OpenCode's own
   perspective — sessions were still being compacted against the
   stale, much-smaller 65536 ceiling regardless. **Fixed today**: set to
   `262144` to match the real current server config.
6. **Also enabled**: `"compaction": {"auto": true, "prune": true,
   "reserved": 10000}` at the top level of `opencode.json` (previously
   `prune` was absent/false — OpenCode defaults to `prune: false`).
   Per OpenCode's own architecture (confirmed via official docs/source
   description), `prune` performs cheap, non-LLM tool-output hiding
   (timestamp-based, not physical deletion) BEFORE falling back to the
   expensive, lossy LLM-summarization `Compaction` — should reduce how
   often the expensive/signal-destroying summarization path triggers at
   all.

**Current state, confirmed working as of this writing**: `coder` at
256K context, `q4_0` KV cache, `opencode.json` declaring the matching
`262144` limit, `prune: true` enabled. **NOT yet re-tested across a full
multi-file task** to confirm compaction frequency actually dropped in
practice — this is the natural next thing to observe when resuming.

**Still NOT done, explicitly deferred by the user earlier**: no
decision yet on the 4-6 GPU expansion plan from §4/§8 (merging
coder+fixer, giving the merged role 4 GPUs) — that remains paused behind
the baseline-stats collection period, unaffected by today's q4_0/256K
work (which was a tuning improvement to the EXISTING 2-GPU `coder`
allocation, not the bigger architectural merge).

### The `.agent-reports/` vs `.opencode/reports/` mystery — solved, and it was Claude's own fault

A recurring, several-times-repeated confusion this session: agents kept
citing a stale `.agent-reports/` path (the pre-consolidation location,
deleted back on 2026-08-22) instead of `.opencode/reports/`. After
`AGENTS.md` was already confirmed fixed (§11), this KEPT recurring.
Root cause, finally identified: **the actual TASK PROMPTS being pasted
to the orchestrator were themselves written by Claude (this assistant,
in earlier turns of this same session)**, and those prompt-writing turns
kept including a stale `"REPORTING: ... .agent-reports/..."` line out
of habit, predating the consolidation. This was NOT a harness bug at
all — every `.opencode/` config/prompt file was already correct. Fixed
going forward: this assistant now uses `.opencode/reports/` when writing
any future task-prompt "REPORTING:" section. If a NEW session sees this
confusion resurface, check whether it's coming from a freshly-written
task prompt (a Claude mistake, fixable by just writing it correctly next
time) rather than assuming it's a harness config issue again.

### Deep research phase — eval-driven harness development

Extensive research was done (see the full detail in the separate file
`Docs/QUALITY_PLAN.md`, NOT duplicated here) into how to move beyond
purely reactive prompt-patching. Headline findings, condensed:

- **Anthropic's own Claude Code team had a real, documented incident**
  (public postmortem, April 2026): shipped a system-prompt change that
  passed their internal eval suite, found a 3% regression later with a
  BROADER eval set, had to revert, and committed to running "a broad
  suite of per-model evals for every system prompt change" going
  forward. This directly validates (and tempers expectations about) the
  eval-suite direction — even Anthropic's own team gets this wrong
  sometimes; it's an ongoing discipline, not a one-time fix.
- **Anthropic's own official methodology post** ("Demystifying evals for
  AI agents", Jan 2026) provides detailed, directly-actionable guidance:
  exact terminology (task/trial/grader/transcript/outcome), three
  grader types (code-based/model-based/human, prefer code-based where
  possible), capability-vs-regression eval distinction, and an 8-step
  practical roadmap starting from just 20-50 tasks drawn from REAL past
  failures — this project's `Docs/BUGLOG.md` (~15 entries) is EXACTLY
  this raw material already.
- Confirmed this project's own reactive-then-systematize pattern
  (BUGLOG → Error class/Status → escalation) matches Claude Code's own
  documented evolution: "Claude Code started with fast iteration...
  Later, we added evals — first for narrow areas... then more complex
  behaviors" — i.e. today's frustration ("we keep patching reactively")
  is a NORMAL, expected stage, not a sign of doing it wrong; the plan
  (`QUALITY_PLAN.md`) is the documented next stage.
- Existing tooling found, not yet adopted: `promptfoo` (has direct
  OpenCode SDK support), `Judge Reliability Harness` (arxiv 2603.05399,
  directly relevant to testing whether `reviewer` reliably catches
  known-bad cases like garbled text or fabricated findings).
- Academic taxonomy found, not yet applied: `AgentAtlas` (arxiv
  2605.20530) formalizes a six-state agent control-decision model
  (Act/Ask/Refuse/Stop/Confirm/Recover) — a more rigorous framework than
  today's ad-hoc "GENERAL FALLBACK" prose; worth revisiting the
  fallback rules through this lens later.

**`Docs/QUALITY_PLAN.md` created today** with the full 3-phase plan
(proactive BUGLOG risk audit → build mechanical checks for the highest-
priority gaps → eventual eval suite) plus a dedicated §7 documenting
concrete gaps in the existing `nordicbees-quality-monitor.ts` stats
plugin, found by comparing it against Anthropic's own tracked-metrics
example:

1. **`n_toolcalls` is completely missing** — the plugin only observes
   `task`-tool start/end, with zero visibility into how many internal
   `read`/`edit`/`grep` calls happened inside a single `coder`/`fixer`
   delegation. This is EXACTLY the signal that would have flagged
   today's (and yesterday's) repeated-file-read loop incidents
   automatically and in real time, rather than only via manual
   transcript reading or after-the-fact interruption detection.
2. **`prompt_chars` is a rough character-count proxy, not real tokens**
   — worth checking whether the OpenCode plugin hook exposes the raw
   API `usage: {prompt_tokens, completion_tokens}` object, and using
   that instead if so.
3. **Prefill vs. decode time are not separated** — `llama-server`'s own
   logs already report prefill throughput precisely; cross-referencing
   this with the plugin's wall-clock duration would let a future session
   directly test whether `Compaction` events cause expensive
   prefix-cache-busting re-prefill (a real, research-backed concern
   found today — LMCache paper reported prefix-cache-hit-rate collapsing
   from ~85% to ~45% under context truncation in one measured case) with
   real numbers, not just inference from papers.

None of these three are implemented yet — recommended path (per the
user's own agreement today) is to do this focused editing work via
**Claude Code**, not a continuation of this long chat session.

### Statistics collection status

`task-stats.jsonl` (see §5) continues accumulating correctly —
`duration_sec` and the disk-based interruption detection are both
confirmed working from yesterday's fixes. **`guardrail_score` extraction
was fixed yesterday but its correctness was never re-confirmed with a
fresh post-fix `fixer` call** — this is still an open item, same as
noted in §9, not yet resolved today either. Check the tail of
`task-stats.jsonl` for a recent `fixer` entry with a non-null
`guardrail_score` to confirm.

---

## 2026-08-24 (later) — opencode-auto-resume disabled; root cause of
   today's instability found: unpinned plugin version

After 4 distinct `fixer` loop incidents today (see BUGLOG.md), found that
`~/.cache/opencode/packages/opencode-auto-resume@latest` is NEWER than
`.opencode/node_modules/opencode-auto-resume` (installed 2026-07-17).
OpenCode's own plugin loader fetches npm plugins fresh via the `@latest`
tag on startup when no version is pinned in `opencode.json` (the config
had `"opencode-auto-resume"`, no `@version`). This means the plugin's
behavior could have silently changed between yesterday (stable) and
today (4 loop types) due to an upstream npm publish, with ZERO changes
made in this repo — explains "nothing we changed, but it broke" cleanly.

**Action taken**: `opencode.json` `"plugin": []` (disabled). Old config
preserved under key `"_plugin_disabled_2026-08-24"` for easy restore.

**If re-enabling later**: MUST pin an exact version
(`"opencode-auto-resume@1.1.3"` or whatever is current/tested), never
leave it unpinned — this applies to ANY future plugin added to this
harness, not just this one. An unpinned `latest`-tag dependency in a
production-adjacent harness is itself a standing risk, independent of
this specific plugin's quality.

**Also noted**: `Docs/HARNESS_STATUS.md` §13/§13.1 (baseline-week
sequencing + harness-core extraction plan, written earlier today) have
been silently reset/lost from this file at least twice today, likely by
a concurrent process touching `Docs/` during the loop incidents. Content
still exists in this chat's history if needed to reconstruct; not
re-added here to avoid a third conflict during an already unstable day.
