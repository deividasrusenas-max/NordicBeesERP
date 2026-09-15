# Claude Code run — OCR module inventory (independent second pass)

Paste this whole file as the opening prompt in Claude Code. Use the strongest
model available. Run this **only after** the OpenCode run has finished, or before
it — never at the same time, since both operate in the same git working tree.

---

## Prompt

Your task body is the file `.opencode/tasks/latest.md` in this repository. Read it
in full and follow every instruction in it exactly — the preconditions, the
database hard rule, Part A (18 claims to verify), Part B (9 knowledge gaps), the
report structure, the completion checklist and the STOP conditions.

Three overrides apply to your run, and only these three:

**1. Report path.** Ignore the `.opencode/reports/` path given in the task body.
Write your report to:

```
Docs/ocr-rebuild/analysis/inventory-claudecode-<YYYYMMDD-HHMM>.md
```

Everything else about the report — its structure, the `file:line` requirement, the
5-line quote limit, the checklist — stays exactly as specified in the task body.

**2. Blind run.** Do not read anything under `.opencode/reports/` and do not read
any file under `Docs/ocr-rebuild/analysis/` other than this prompt. Another agent
is performing the same task independently, and the two results are compared. If
you read the other result, the comparison is worthless. If you accidentally see
part of it, say so plainly at the top of your report.

You may read `Docs/ocr-rebuild/PLAN.md` and `DECISIONS.md` for context, but treat
D-001 there as the same set of claims to be tested — not as established fact.

**3. Disagreement is the point.** You are not being asked to confirm a prior
analysis. A claim you refute with solid evidence is worth more than a claim you
confirm. If a claim is stated imprecisely — right symptom, wrong mechanism — say
`PARTIALLY CONFIRMED` and give the correct mechanism. If you find something more
severe than anything on the list, lead your summary with it.

Work through the task in order. Do not skip Part B because Part A took longer than
expected; the knowledge gaps are what the next three build tasks depend on.

---

## What is being measured (for reference, not part of the prompt)

This run has two purposes. The first is a maximum-quality assessment of the OCR
module. The second is a benchmark of the OpenCode harness against Claude Code on
an identical, objectively gradeable task.

Comparison criteria, applied to both reports:

| Criterion | How it is scored |
|---|---|
| Claim coverage | How many of A1–A18 got a real verdict vs were skipped or hand-waved |
| Evidence quality | Does every verdict carry a `file:line` that actually points at the right code |
| Correctness | Where the two disagree, the code is read manually and one is right |
| Gap coverage | How many of B1–B9 answered, and how completely (B1 and B8 are the heavy ones) |
| New findings | Issues found that no claim covered — the strongest quality signal |
| Discipline | Did it stay read-only, did it respect the SELECT-only rule, did it stop when it should |
| False confidence | Statements presented as fact that turn out wrong — weighted heavily against |

The last row matters most. A report that is 70% complete and honest about the
remaining 30% is more useful than one that is 95% complete with three confident
errors buried in it, because the errors propagate into the build tasks.

Result of the comparison goes in `Docs/ocr-rebuild/analysis/COMPARISON.md`.
