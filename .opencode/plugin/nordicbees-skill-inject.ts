import type { Plugin } from "@opencode-ai/plugin"
import { readFileSync, existsSync } from "fs"
import { join } from "path"

/**
 * NordicBeesERP skill force-injection hook.
 *
 * Problem this solves: automatic skill activation (the model deciding on
 * its own to call the `skill` tool) is unreliable — this is a known,
 * confirmed OpenCode issue (obra/superpowers#439), not specific to our
 * local models. Rather than hoping the model remembers, this hook
 * deterministically injects the FULL content of the relevant skill
 * file(s) directly into the `coder`/`fixer` Task-tool delegation text,
 * based on simple keyword matching against the task description. The
 * subagent then has the skill content already in front of it — no
 * separate tool call needed, nothing to forget.
 */

const SKILLS_DIR = join(process.cwd(), ".opencode", "skills")

function loadSkill(name: string): string | null {
  const path = join(SKILLS_DIR, name, "SKILL.md")
  if (!existsSync(path)) return null
  try {
    return readFileSync(path, "utf-8")
  } catch {
    return null
  }
}

// keyword -> skill name(s) to force-inject when the keyword appears
// (case-insensitive) anywhere in the delegation text.
const RULES: { pattern: RegExp; skills: string[] }[] = [
  { pattern: /\.razor\b/i, skills: ["mudblazor"] },
  { pattern: /MudBlazor|MudStack|MudGrid|MudPaper|MudTable|MudDialog/i, skills: ["mudblazor"] },
  { pattern: /\bService\.cs\b|Migrations\/|DbContext|ExecuteSqlRawAsync|NordicBeesErpContext/i, skills: ["dotnet-efcore-nordicbees"] },
  { pattern: /\bVAT\b|PVM|i\.SAF|isaf/i, skills: ["lithuanian-vat-isaf"] },
  { pattern: /QuestPDF|IDocument\b|GeneratePdf|\bPDF\b|kokyb.s p.ym.jimas|certificate\b|CMR\b/i, skills: ["questpdf-nordicbees"] },
  { pattern: /\bbutton\b(?<!back-button)|MudButton|\bdialog\b|OnClick|OnValidSubmit|\bform\b|write.*database|database.*write/i, skills: ["verify-before-done"] },
  { pattern: /\bE2E\b|end-to-end|browser test|playwright|verify in browser|real browser/i, skills: ["playwright-e2e-nordicbees"] },
  { pattern: /slow|performance|optimi[sz]e|N\+1|query.*speed|speed.*query/i, skills: ["efcore-performance-nordicbees"] },
  { pattern: /Create\.razor|Edit\.razor|CRUD|all fields|every field|entity model|new column|new field|model.*propert/i, skills: ["crud-completeness"] },
]

// Always inject these for the given subagent, regardless of task content.
const ALWAYS_FOR_AGENT: Record<string, string[]> = {
  fixer: ["git-workflow-nordicbees"],
  reviewer: ["git-workflow-nordicbees", "llm-code-quality-gate"],
  coder: [],
}

export const NordicBeesSkillInjector: Plugin = async () => {
  return {
    "tool.execute.before": async (input, output) => {
      if (input.tool !== "task") return

      const args = (output as any)?.args
      if (!args) return

      const subagent = args.subagent_type as string | undefined
      if (subagent !== "coder" && subagent !== "fixer" && subagent !== "reviewer") return

      // The field holding the actual delegation text varies by OpenCode
      // version — check the common candidates.
      const textField = ["prompt", "description", "task", "message"].find(
        (f) => typeof args[f] === "string"
      )
      if (!textField) return

      const originalText: string = args[textField]

      const skillsToInject = new Set<string>(ALWAYS_FOR_AGENT[subagent] ?? [])
      for (const rule of RULES) {
        if (rule.pattern.test(originalText)) {
          for (const s of rule.skills) skillsToInject.add(s)
        }
      }

      if (skillsToInject.size === 0) return

      let injected = ""
      for (const skillName of skillsToInject) {
        const content = loadSkill(skillName)
        if (content) {
          injected += `\n\n<forced-skill-injection name="${skillName}">\n${content}\n</forced-skill-injection>\n`
        }
      }

      if (injected) {
        args[textField] =
          `IMPORTANT: the following skill(s) are force-injected because they ` +
          `are relevant to this task. Follow them — do not skip reading them ` +
          `just because you weren't explicitly told to call the skill tool.\n` +
          injected +
          `\n\n---\n\n${originalText}`
      }
    },
  }
}
