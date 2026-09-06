import type { Plugin } from "@opencode-ai/plugin"

/**
 * NordicBeesERP verification hook.
 *
 * Problem this solves: subagents (coder/fixer) sometimes claim "done" /
 * "all tasks completed" without the orchestrator ever independently
 * verifying it. This plugin removes that trust gap by running a REAL
 * `dotnet build` (deterministic code, not another LLM call) after every
 * Task-tool delegation to `coder` or `fixer`, and injecting the actual
 * exit code + output back into the session automatically. The
 * orchestrator sees ground truth on every turn, whether or not the
 * subagent's own text claims success.
 *
 * FIXER-ONLY CLAIM CHECKS (added after Docs/BUGLOG.md
 * `fixer-fabricated-implementation-claim`, 2026-08-26): a successful build
 * is orthogonal to two other fabrication axes actually observed in this
 * project — (a) fixer's report citing a commit hash/message that doesn't
 * match git reality, and (b) fixer's report claiming a specific mechanism
 * (e.g. "added a foreign key") that the actual committed diff doesn't
 * contain. These three checks (hash match, claimed-file presence, claimed-
 * mechanism presence) are additive to the build check, not a replacement,
 * and only apply to `fixer` (the only role that commits). They ANNOTATE
 * the tool output the same way the build check does — they never block or
 * abort anything; the orchestrator/human decides what a MISMATCH means.
 *
 * All three are silent no-ops when fixer's report doesn't cite anything
 * checkable (absence of a claim is not evidence of fabrication) — only an
 * actual cited-but-wrong claim produces a MISMATCH line. Regex extraction
 * and the negation-window logic below were validated against several real
 * historical .opencode/reports/*.md files and their actual git history
 * before being written (see .opencode/planning/verify-plugin-enhancement.md)
 * — in particular, the mechanism check requires the claim NOT be inside a
 * negation ("intentionally omitted", "not added", "no FK", etc.) window,
 * confirmed against a real report where "FK constraints intentionally
 * omitted" would otherwise have produced a false MISMATCH, and the
 * claimed-file check is scoped to an actual "Files changed"-style section
 * rather than the whole report body, confirmed against a real report where
 * an unscoped scan pulled in unrelated files mentioned elsewhere in the
 * text and would have produced 3 false MISMATCHes.
 *
 * Known, accepted limitation: these are lexical/structural checks, not a
 * semantic correctness review (that's `reviewer`'s job, and reviewer
 * already ran before fixer in the normal per-file workflow). The mechanism
 * keyword list is small and fixed — grow it only after a real incident
 * demonstrates a gap, same pattern BUGLOG.md's own error-class tags
 * already follow, not by preemptively guessing every fabrication phrasing.
 */

const HASH_RE = /\bcommit(?:s|ted|ted as)?\s*[:\-]?\s*`?([0-9a-f]{7,40})`?/gi
const FILES_SECTION_HEADER_RE = /^#{1,4}\s*.*files?\s+changed.*$/gim
const FILE_RE = /`([\w./-]+\.(?:cs|razor|sql|md|json|ts))`/g
const MECHANISM_PATTERNS: { name: string; re: RegExp }[] = [
  { name: "foreign key", re: /foreign key/gi },
  { name: "FK", re: /\bFK\b/g },
  { name: "ALTER TABLE", re: /ALTER TABLE/gi },
  { name: "CREATE INDEX", re: /CREATE INDEX/gi },
  { name: "unique constraint", re: /unique constraint/gi },
]
const NEGATION_WORDS_RE = /\b(not|n't|without|omit|omitted|no|zero|never|pending|intentionally)\b/i
const NEGATION_WINDOW = 40

/** Extracts commit hashes fixer's report claims, deduplicated. */
function extractClaimedHashes(text: string): string[] {
  return [...new Set([...text.matchAll(HASH_RE)].map((m) => m[1]))]
}

/**
 * Extracts file paths fixer's report claims to have changed, scoped to an
 * actual "Files changed"-style section (not the whole report body) — an
 * unscoped whole-document scan picks up files mentioned for unrelated
 * context elsewhere in the report, which is a confirmed real false-positive
 * source (see the doc comment above). Returns { files, scoped }: scoped is
 * false when no such section header was found, meaning this check has
 * nothing to compare and should be treated as a no-op, not "zero files."
 */
function extractClaimedFiles(text: string): { files: string[]; scoped: boolean } {
  const headers = [...text.matchAll(FILES_SECTION_HEADER_RE)]
  if (headers.length === 0) return { files: [], scoped: false }
  const files = new Set<string>()
  for (const h of headers) {
    const sectionStart = (h.index ?? 0) + h[0].length
    const rest = text.slice(sectionStart)
    const nextHeader = rest.match(/^#{1,4}\s+\S/m)
    const sectionText = nextHeader ? rest.slice(0, nextHeader.index) : rest
    for (const m of sectionText.matchAll(FILE_RE)) files.add(m[1])
  }
  return { files: [...files], scoped: true }
}

/**
 * Extracts mechanism keywords fixer's report POSITIVELY claims to have
 * implemented — excludes any match sitting inside a negation window (e.g.
 * "FK constraints intentionally omitted", "no FOREIGN KEY DDL lines").
 * Without this filter, a report honestly stating a mechanism was NOT added
 * would still trip a false MISMATCH when the real diff (correctly) doesn't
 * contain it either.
 */
function extractPositiveMechanismClaims(text: string): string[] {
  const found = new Set<string>()
  for (const { name, re } of MECHANISM_PATTERNS) {
    for (const m of text.matchAll(re)) {
      const start = Math.max(0, (m.index ?? 0) - NEGATION_WINDOW)
      const windowText = text.slice(start, (m.index ?? 0) + m[0].length + NEGATION_WINDOW)
      if (!NEGATION_WORDS_RE.test(windowText)) found.add(name)
    }
  }
  return [...found]
}

export const NordicBeesVerify: Plugin = async ({ $, directory }) => {
  return {
    "tool.execute.after": async (input, output) => {
      // Only fires after a Task-tool delegation (subagent call) completes
      if (input.tool !== "task") return

      const subagent = (input as any).args?.subagent_type as string | undefined
      if (subagent !== "coder" && subagent !== "fixer") return

      let buildResult: { exitCode: number; stdout: string; stderr: string }
      try {
        const result = await $`dotnet build`.cwd(directory).nothrow().quiet()
        buildResult = {
          exitCode: result.exitCode,
          stdout: (result.stdout?.toString() ?? "").slice(-4000), // last 4000 chars, avoid flooding context
          stderr: (result.stderr?.toString() ?? "").slice(-2000),
        }
      } catch (err: any) {
        buildResult = { exitCode: -1, stdout: "", stderr: String(err?.message ?? err) }
      }

      const status = buildResult.exitCode === 0 ? "BUILD OK (0 errors)" : "BUILD FAILED"
      let injected =
        `\n\n<automated-verification source="dotnet-build-hook">\n` +
        `${status} — exit code ${buildResult.exitCode}\n` +
        `--- stdout (tail) ---\n${buildResult.stdout}\n` +
        `--- stderr (tail) ---\n${buildResult.stderr}\n` +
        `This result was captured automatically by running \`dotnet build\`` +
        ` directly — it is NOT self-reported by the ${subagent} agent.` +
        ` Trust this over any claim of success/failure in the agent's own text.\n` +
        `</automated-verification>`

      if (subagent === "fixer") {
        const reportText =
          typeof (output as any)?.output === "string" ? (output as any).output : JSON.stringify(output ?? "")

        // Check A — commit hash match
        const claimedHashes = extractClaimedHashes(reportText)
        let hashCheck = "no commit hash cited in report — skipped"
        if (claimedHashes.length > 0) {
          try {
            const log = await $`git log -n 10 --format=%H`.cwd(directory).nothrow().quiet()
            const realHashes = (log.stdout?.toString() ?? "").split("\n").filter(Boolean)
            const unmatched = claimedHashes.filter((h) => !realHashes.some((real) => real.startsWith(h)))
            hashCheck =
              unmatched.length > 0
                ? `MISMATCH — report cites hash(es) [${unmatched.join(", ")}] not found in last 10 real commits`
                : `OK — all cited hash(es) [${claimedHashes.join(", ")}] found in real history`
          } catch (err: any) {
            hashCheck = `check failed to run: ${String(err?.message ?? err)}`
          }
        }

        // Check B1 — claimed file presence (scoped to a "Files changed" section)
        const { files: claimedFiles, scoped } = extractClaimedFiles(reportText)
        let fileCheck = scoped ? "no files listed in the Files-changed section — skipped" : "no Files-changed section found — skipped"
        if (scoped && claimedFiles.length > 0) {
          try {
            const stat = await $`git show --stat HEAD`.cwd(directory).nothrow().quiet()
            const statText = stat.stdout?.toString() ?? ""
            const missing = claimedFiles.filter((f) => !statText.includes(f))
            fileCheck =
              missing.length > 0
                ? `MISMATCH — report cites file(s) [${missing.join(", ")}] not in HEAD's diff`
                : `OK — all cited file(s) found in HEAD's diff`
          } catch (err: any) {
            fileCheck = `check failed to run: ${String(err?.message ?? err)}`
          }
        }

        // Check B2 — claimed mechanism keyword presence in HEAD's full diff
        const claimedMechanisms = extractPositiveMechanismClaims(reportText)
        let mechanismCheck = "no tracked mechanism keywords positively claimed — skipped"
        if (claimedMechanisms.length > 0) {
          try {
            const diff = await $`git show HEAD`.cwd(directory).nothrow().quiet()
            const diffText = diff.stdout?.toString() ?? ""
            const unbacked = claimedMechanisms.filter((name) => {
              const pattern = MECHANISM_PATTERNS.find((p) => p.name === name)?.re
              return pattern ? !new RegExp(pattern.source, pattern.flags.replace("g", "")).test(diffText) : false
            })
            mechanismCheck =
              unbacked.length > 0
                ? `MISMATCH — report claims [${unbacked.join(", ")}] but HEAD's diff contains no matching text`
                : `OK — claimed mechanism(s) [${claimedMechanisms.join(", ")}] found in HEAD's diff`
          } catch (err: any) {
            mechanismCheck = `check failed to run: ${String(err?.message ?? err)}`
          }
        }

        injected +=
          `\n\n<automated-verification source="fixer-claim-check">\n` +
          `Commit hash check: ${hashCheck}\n` +
          `Claimed-file check: ${fileCheck}\n` +
          `Claimed-mechanism check: ${mechanismCheck}\n` +
          `These are structural/lexical checks, not a correctness review — reviewer's ` +
          `own diff review already ran before fixer in the normal workflow. A MISMATCH ` +
          `here means fixer's own prose does not match git reality and should not be ` +
          `trusted without independent verification.\n` +
          `</automated-verification>`
      }

      // Append to the tool output the orchestrator sees, regardless of
      // what the subagent itself said.
      if (output && typeof output === "object") {
        ;(output as any).output = ((output as any).output ?? "") + injected
      }
    },
  }
}
