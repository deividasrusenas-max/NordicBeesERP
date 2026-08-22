---
name: llm-code-quality-gate
description: A quality gate for catching common mistakes and smells specific to LLM-authored code, run after substantial new or refactored code is written and before it's committed. Use this on any file `coder` just created or substantially changed, as a check `fixer` runs before the build/commit step — this is a different, narrower check than build correctness or spec compliance.
---

# LLM Code Quality Gate — NordicBeesERP

This entire codebase is written by LLM agents. LLMs have characteristic
failure patterns that are different from typical human mistakes — this
skill catches those specifically, as a check distinct from "does it
build" or "does it match the spec."

## What to check on any freshly-written/changed file

1. **Orphaned/dead code from a partial edit** — a leftover unused
   variable, an unreachable branch, a duplicated block that should have
   replaced (not supplemented) an earlier one. This is the single most
   common LLM-authoring failure seen in this project today (e.g. a
   duplicated `HeaderContent`/`RowTemplate` block with no matching
   opening tag, left behind from an incomplete edit).

2. **Placeholder/stub content mistaken for real implementation** — a
   method that returns a hardcoded value, an empty catch block, a
   `// TODO` that reads like it was meant to be resolved in this same
   task but wasn't. Distinguish a deliberate, acknowledged stub (fine, if
   stated) from an accidental one (not fine).

3. **Fabricated confidence in comments** — comments asserting something
   is correct/verified/compliant ("// BRC8 3.9 compliant", "// verified
   working") that the code itself doesn't actually demonstrate. A comment
   claiming compliance is not the same as the code satisfying it — check
   the code, not the comment's claim.

4. **Over-defensive or redundant code from uncertainty** — LLMs
   sometimes add unnecessary null checks, duplicate validation, or
   redundant try/catch around code that can't actually throw, as a
   hedge against uncertainty rather than a real requirement. This adds
   noise without adding safety. (Don't confuse this with genuinely
   necessary defensive code — e.g. the DBNull.Value pattern for
   nullable DB parameters is real and necessary, not this kind of noise.)

5. **Inconsistent naming/casing vs. the rest of the codebase** — an LLM
   working from a fresh context sometimes doesn't match established
   naming conventions (e.g. camelCase vs PascalCase, `Id` vs
   `ID`, `GetX` vs `FetchX`) used elsewhere in the same file or sibling
   files. Check against a real sibling file, not general C# convention.

6. **Copy-paste residue** — a block copied from a reference file
   (explicitly encouraged elsewhere, e.g. "follow the pattern of
   WeightCorrectionDialog.razor") that still contains the SOURCE file's
   variable names, comments, or field references instead of being fully
   adapted to the new context. Check for any leftover reference to the
   wrong entity/field name.

7. **Silent scope creep or scope narrowing** — code that does more than
   asked (touching files/logic outside what was requested) or less
   (implementing only part of a multi-part request without saying so).
   Compare what was actually asked against what was actually delivered.

## How to apply this

This is a fast, targeted pass — not a full re-read of the whole file.
Focus on the specific lines/sections that were just added or changed.
Report any finding with the exact line and a one-sentence explanation of
which category above it falls into. If nothing is found, say so plainly
— don't manufacture a finding to seem thorough.
