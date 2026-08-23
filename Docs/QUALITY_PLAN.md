# NordicBeesERP Harness — Quality Systematization Plan
(2026-08-24, written after the garbled-text incident revealed the limits
of pure reactive prompt-patching. Updated same day with detailed research
findings from Anthropic's official engineering blog and current academic
literature — see §5 and §6.)

## Why this plan exists

Today's session (and the prior day) fixed ~20 individual incidents by
adding prompt-text rules one at a time, after each was observed. This
IS a validated professional pattern (see the "Accumulated Behavioral
Rules" research cited in HARNESS_STATUS.md §6) — but it has a real
ceiling: an LLM reviewer/agent can miss a rule buried in a long prompt
even when the rule technically exists (the garbled-text incident:
`reviewer` had no explicit string-coherence check and would have missed
it even with one, since LLM judgment is probabilistic, not guaranteed).

This plan moves from "patch after the fact, forever" to a staged system
with three tiers of defense, and a proactive (not just reactive) review
cadence.

**IMPORTANT (added same day)**: this reactive-then-systematic progression
is not a workaround — it is EXACTLY the documented path Anthropic's own
Claude Code team followed. Their official post ("Demystifying evals for
AI agents", Jan 2026) states plainly: "Claude Code started with fast
iteration based on feedback from Anthropic employees and external
users. Later, we added evals — first for narrow areas like concision and
file edits, and then for more complex behaviors." Even Anthropic ships
regressions sometimes: a real April 2026 postmortem describes shipping a
Claude Code system-prompt change that passed their internal eval suite,
only to find a 3% quality drop later with a BROADER eval set — they
reverted and committed to "run a broad suite of per-model evals for
every system prompt change." The lesson: eval-driven development is the
right direction, but no eval suite is ever "done" — it must keep growing
as new failure modes are discovered, same as this project's BUGLOG.

## The three-tier defense model (already partially in place, now made explicit)

| Tier | Mechanism | Reliability | Cost |
|---|---|---|---|
| 1 | `dotnet build` / `agent-guardrails` / semgrep | Deterministic — cannot be "missed" | Cheap, fast, but only catches what's explicitly coded |
| 2 | `reviewer` (LLM judgment against explicit checklist) | Probabilistic — can miss things even when the rule exists | Medium cost, catches things too varied to hard-code |
| 3 | Prompt-text rules alone (no tooling backing) | Weakest — depends on the model weighting one sentence correctly in a long prompt | Cheapest to add, but proven insufficient for high-stakes cases (see BUGLOG `Status: escalated` entries) |

**Principle going forward**: for any NEW rule being added in response to an
incident, explicitly ask "which tier does this belong in?" BEFORE writing
it — don't default to Tier 3 (prompt text) just because it's the fastest
to write. High-stakes cases (production-facing text, DB writes, security,
anything a real user would see broken) should start at Tier 1 or 2, not
wait for a second occurrence to "earn" escalation.

---

## Phase 1 — Proactive BUGLOG risk audit (do this first, low effort)

Go through every current `Docs/BUGLOG.md` entry and classify:
- **Current tier** (1/2/3, per the table above — most are currently Tier 3)
- **Real-world stakes if it recurs unnoticed**: does it reach a real user
  (production UI text, data correctness) vs. purely a dev-loop
  inefficiency (an agent wasting time, no external-facing consequence)

Entries where stakes are HIGH and tier is currently 3 are the priority
list for Phase 2, regardless of whether they've recurred yet — don't
wait for `Status: escalated` to trigger this; that mechanism only fires
AFTER a second occurrence, which is too late for high-stakes cases.

Known candidates already visible from today's BUGLOG (2026-08-23 retrofit
+ new 2026-08-24 entries):
- `model-hallucinated-string-literal` (garbled text, 2026-08-24) —
  HIGH stakes (user-facing), currently Tier 3 (the reviewer instruction
  just added) — **top candidate for Phase 2**.
- The 5 `Status: N/A` (fully unguarded) classes from the 2026-08-23
  retrofit — review each for real-world stakes:
  `status-transition-incomplete-allowlist`,
  `schema-drift-unverified-column-mapping`,
  `blazor-forceload-fullreload-auth-redirect`,
  `layout-lifecycle-unguarded-db-call`,
  `mudblazor-autocomplete-tab-value-commit`.

**Deliverable**: a short table (in this file or a new one), one row per
BUGLOG error class, with Current Tier + Stakes + "Escalate now? Y/N".

---

## Phase 2 — Build the highest-priority mechanical checks

For each "Escalate now: Y" item from Phase 1, design the actual Tier-1
check. Two realistic mechanisms available in this project already:

1. **`agent-guardrails`** (`.agent-guardrails/config.json` +
   `nordicbees-rules.yaml`) — already wired into `fixer`'s step 10, runs
   on every task automatically. Good for: file-scope checks, protected-
   area checks, anything expressible as a static rule against the diff.
2. **`semgrep`** rules (already used for `nordicbees-stringcomparison-in-linq`,
   `nordicbees-dbnull-explicit-cast`) — good for pattern-matching C#/Razor
   syntax.

For the garbled-text case specifically, a concrete starting design
(needs real design work, not just this sketch): a check that scans
changed string literals in `.cs`/`.razor` diffs and flags any that don't
look like real Lithuanian/English — e.g., a basic dictionary/character-
n-gram sanity check, not full NLP. This does not need to be perfect —
even a coarse heuristic that catches obvious cases like the observed
incident is a real improvement over "hope the LLM reviewer notices."

**Deliverable**: one working mechanical check for the garbled-text case,
tested against the actual incident's known-bad string as a regression
case, wired into either `agent-guardrails` or a new semgrep rule.

---

## Phase 3 — Eval suite (the real long-term fix, separate larger project)

This is the professional-grade answer, now grounded in detailed,
first-party methodology (see §5) — genuinely substantial work, not
something to build in one sitting.

### Concrete starting scope (small, per Anthropic's own advice)

Anthropic's own guidance: *"20-50 simple tasks drawn from real failures
is a great start... Start with what you already test manually — your
bug tracker."* `Docs/BUGLOG.md` (~15 entries as of 2026-08-24) IS this
raw material already — each entry is a candidate eval task.

### Exact terminology to use (from Anthropic, "Demystifying evals for AI agents")

- **Task** — one test with defined input + success criteria.
- **Trial** — one attempt (run multiple since outputs vary between runs).
- **Grader** — logic scoring ONE aspect; a task can have multiple graders.
- **Transcript** — the full record of a trial (all tool calls, not just
  the final answer).
- **Outcome** — the final STATE (e.g. does `reviewer`'s response literally
  contain "REJECTED"), never just what the agent claims it did.
- **Eval suite** — a collection of tasks sharing a broad goal.

### Grader types (use the cheapest reliable one for each check)

| Type | Examples | When to use |
|---|---|---|
| Code-based | string match, regex, static analysis (semgrep), tool-call verification, transcript metrics (turns/tokens) | Prefer this whenever possible — fast, deterministic, reproducible |
| Model-based | rubric scoring, LLM-as-judge | When correctness genuinely needs judgment (matches this project's `llm-code-quality-gate` skill already) |
| Human | spot-check sampling | Calibrating the above two, not for routine runs |

### Capability evals vs. regression evals (important distinction)

- **Capability evals** ask "what can this agent do well" — should START
  at a LOW pass rate (target things the agent currently struggles with).
- **Regression evals** ask "does it still handle what it used to" —
  should stay near 100%; a drop signals real breakage.
- Once a capability eval consistently passes, it "graduates" into the
  regression suite. For this project: once we've fixed and verified a
  BUGLOG incident (e.g. garbled text), the fix itself becomes a
  regression-suite entry that must keep passing on every future prompt
  change.

### A concrete example task, adapted from Anthropic's own YAML template

```yaml
task:
  id: "garbled-text-detection_1"
  desc: "reviewer must REJECT a diff containing hallucinated non-language text"
  graders:
    - type: deterministic_tests
      check: "response contains literal string 'REJECTED'"
  tracked_metrics:
    - type: transcript
      metrics: [n_turns, n_toolcalls, n_total_tokens]
```

### Practical roadmap (Anthropic's Step 0-8, condensed)

0. Start with 20-50 tasks from real BUGLOG failures, not hundreds.
1. Convert what's already manually checked (BUGLOG) into test cases first.
2. Each task needs a REFERENCE SOLUTION (a known-correct output) — this
   proves the task is solvable and the grader is wired correctly. A
   0% pass rate across many trials usually means a BROKEN task/grader,
   not an incapable agent — always suspect the test first.
3. Build BALANCED sets — test both "should trigger" and "should NOT
   trigger" cases for every rule (a one-sided eval teaches one-sided
   behavior — e.g. only testing "reviewer rejects bad text" without also
   testing "reviewer approves genuinely fine text" risks an
   over-triggering reviewer that rejects everything).
4. Isolate each trial (clean git state, no leftover files) — shared
   state between trials causes false correlated failures.
5. Grade the OUTPUT, not the exact PATH taken — don't require an exact
   tool-call sequence; agents legitimately find different valid routes.
6. READ THE TRANSCRIPTS of failures — you can't trust a grader until
   you've confirmed by hand that its failures are genuine agent mistakes,
   not grader bugs.
7. Watch for saturation — a check at 100% pass rate stops giving signal;
   that's fine (it's now a regression guard), just don't mistake it for
   "nothing left to test."
8. Treat the suite as a living artifact — add a task every time a NEW
   incident is found, same rhythm as today's BUGLOG entries.

### Concrete existing tooling (don't build from scratch)

- **`promptfoo`** (open-source) has direct, documented support for
  OpenCode SDK evals specifically — a real starting point rather than
  building eval-running infrastructure from zero.
- **Judge Reliability Harness** (arxiv 2603.05399) — a dedicated
  open-source library specifically for testing whether an LLM judge
  (directly applicable to our `reviewer`) is reliable: "generates
  reliability tests that evaluate both binary judgment accuracy and
  ordinal grading performance." Directly usable to systematically test
  whether `reviewer` reliably catches known-bad cases (garbled text,
  fabricated findings) instead of hoping.

**Not scheduled yet** — revisit after Phase 1+2 are done and/or after the
baseline-stats collection period (per HARNESS_STATUS.md §0) concludes.

---

## §5 — Detailed methodology notes from Anthropic's own engineering post
(anthropic.com/engineering/demystifying-evals-for-ai-agents, Jan 2026)

Kept as a reference section since the source article is extremely dense
and directly actionable — condensed here rather than re-deriving later.

- **"Swiss Cheese Model"**: no single evaluation layer catches
  everything — automated evals + production monitoring + user feedback +
  manual transcript review + human studies each catch different failure
  classes. This validates having MULTIPLE mechanisms in this project
  (BUGLOG + `task-stats.jsonl` + future eval suite + periodic review)
  rather than expecting any one of them to be a complete solution.
- **pass@k vs pass^k**: pass@k = probability of at least one success in
  k attempts (rises with k — useful when one success is enough, e.g.
  finding a working fix). pass^k = probability ALL k trials succeed
  (falls with k — useful when consistency matters every time, e.g. a
  customer-facing agent). For this project, `reviewer`/`fixer`
  reliability is closer to a pass^k concern (we need it to catch
  problems EVERY time, not just sometimes).
- **Common eval-design mistakes observed even at Anthropic's scale**:
  rigid grading that penalizes valid near-matches (e.g. "96.12" vs
  "96.124991..."), ambiguous task specs that penalize an agent for a
  spec problem rather than its own mistake, and graders that can be
  "gamed" without genuinely solving the task. Before trusting a low eval
  score, always check whether the TASK/GRADER is broken before
  concluding the agent is bad.
- **Make graders resistant to being gamed** — don't let an agent pass by
  exploiting a loophole in the grading logic rather than solving the
  real problem (mirrors this project's `llm-code-quality-gate` skill's
  concern about fabricated confidence/plausible-sounding-but-wrong work).

---

## §6 — Broader academic landscape (surveyed 2026-08-24, for future reference)

- **`AgentAtlas`** (arxiv 2605.20530) — proposes a six-state
  control-decision taxonomy for agents: **Act / Ask / Refuse / Stop /
  Confirm / Recover**. This is a formal, peer-reviewed structure for
  EXACTLY what today's session added ad-hoc to every agent prompt (the
  "GENERAL FALLBACK — if this doesn't match my defined scope, refuse and
  stop" rules added to `coder.md`/`fixer.md`/`verifier.md`/
  `orchestrator.md` on 2026-08-24). Worth revisiting these rules through
  this taxonomy's lens later — it may reveal states we haven't explicitly
  covered (e.g. "Confirm" — asking for explicit user confirmation before
  a risky action — is only partially covered today via the
  explicit-permission-required action categories, not systematically
  applied to agent prompt design).
- **"Beyond pass@1: A Reliability Science Framework for Long-Horizon LLM
  Agents"** (arxiv 2603.29231) — applies classical reliability
  engineering methodology (citing Modarres & Kaminskiy, standard
  reliability-engineering textbook authors) to long-horizon LLM agents
  specifically — directly relevant to this project's long-running
  `coder`/`fixer` sessions (the ones that hit compaction repeatedly).
- **"A Survey of LLM Agent Evaluation"** (arxiv 2507.21504, published at
  KDD 2025, peer-reviewed) — organizes the field along two axes:
  evaluation OBJECTIVES (behavior/capability/reliability/safety) and
  evaluation PROCESS (interaction modes/datasets/metrics/tooling). Good
  structural reference if a more formal framework is needed later.
- **Field-wide trend** (per a curated GitHub survey of agent-eval
  papers): "Moving beyond coarse, end-to-end success metrics to more
  detailed, step-by-step analysis to diagnose failures" and "Increasing
  focus on measuring resource consumption (tokens, time, API calls)
  alongside performance" — both directly validate this project's
  `task-stats.jsonl` direction (tracking duration/calls, not just
  pass/fail) as aligned with current field practice, not an ad-hoc
  invention.

---

## §7 — Statistics-collection improvements identified (2026-08-24, not yet built)

Comparing this project's `nordicbees-quality-monitor.ts` against
Anthropic's own tracked-metrics example
(`n_turns, n_toolcalls, n_total_tokens` / `time_to_first_token,
output_tokens_per_sec, time_to_last_token`) surfaced three concrete gaps:

1. **`n_toolcalls` is completely missing.** The plugin currently only
   hooks the `task` tool boundary (start/end of a `coder`/`fixer`/
   `reviewer` delegation) — it has no visibility into how many internal
   `read`/`edit`/`grep` calls happened WITHIN that delegation. This is
   exactly the signal that would have flagged today's repeated-file-read
   loop incidents automatically (a session with 20 internal tool calls
   when 3 were expected is an anomaly, detectable without manually
   reading a transcript). Fix: since the plugin's `tool.execute.before`/
   `after` hooks fire for ALL tools, not just `task`, add a counter that
   increments for any tool call observed between a `task` start and end,
   keyed by the same `call_id`.
2. **`prompt_chars` is a rough proxy for tokens, not accurate.** The
   character-to-token ratio varies by content. OpenAI-compatible APIs
   (which `llama-swap` speaks) normally return exact
   `usage: {prompt_tokens, completion_tokens}` in each response — worth
   checking whether the OpenCode plugin hook exposes this raw API
   response anywhere, and using it instead of `.length` if so.
3. **Prefill vs. decode time are not separated.** `llama-server`'s own
   logs already report this split precisely (e.g. "prompt processing,
   n_tokens=8192... 1679.62 tokens per second" — this is prefill only,
   separate from decode/generation speed). Cross-referencing
   `llama-server`'s own logs with our plugin's wall-clock
   `duration_sec` would let us DIRECTLY test today's hypothesis (does
   `Compaction` cause expensive prefix-cache-busting re-prefill?) with
   real numbers instead of inference from research papers alone.

**Priority if implemented**: `n_toolcalls` first (highest value, real-time
loop detection instead of only after-the-fact interruption detection),
then exact tokens, then prefill/decode cross-referencing (most complex,
requires joining two separate log sources).

**Recommended execution path**: this kind of focused plugin/code editing
work is better suited to Claude Code (terminal or desktop app) than to
continuing in an already very long chat session — a fresh Claude Code
session for one focused task (e.g. "add `n_toolcalls` tracking to
`nordicbees-quality-monitor.ts`") avoids re-sending this entire day's
accumulated conversation history as context on every turn, which is a
real, avoidable token cost specific to how long this particular
conversation has already grown.
