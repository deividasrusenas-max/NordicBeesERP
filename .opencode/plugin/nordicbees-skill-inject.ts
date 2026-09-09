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
  // Requires .razor co-occurrence — a bare component-name mention used to
  // fire this with zero UI work involved (real incident, Docs/HARNESS_STATUS.md
  // §13: a pure-migration task's build-note aside "(Pre-existing MudBlazor
  // MUD0002 analyzer WARNINGS...)" injected the full mudblazor skill).
  { pattern: /(MudBlazor|MudStack|MudGrid|MudPaper|MudTable|MudDialog)[\s\S]*\.razor\b|\.razor\b[\s\S]*(MudBlazor|MudStack|MudGrid|MudPaper|MudTable|MudDialog)/i, skills: ["mudblazor"] },
  { pattern: /\bService\.cs\b|Migrations\/|DbContext|ExecuteSqlRawAsync|NordicBeesErpContext/i, skills: ["dotnet-efcore-nordicbees"] },
  { pattern: /\bVAT\b|PVM|i\.SAF|isaf/i, skills: ["lithuanian-vat-isaf"] },
  // Bare \bPDF\b used to fire on any incidental mention of the word (e.g.
  // "attach as PDF" in unrelated prose) — now requires generation context,
  // or one of the already-specific API names/known document types.
  { pattern: /QuestPDF|IDocument\b|GeneratePdf|generat\w*[^\n]{0,40}\bPDF\b|\bPDF\b[^\n]{0,40}generat\w*|kokyb.s p.ym.jimas|certificate\b|CMR\b/i, skills: ["questpdf-nordicbees"] },
  // Dropped \bform\b/\bbutton\b/\bdialog\b/write.*database/database.*write —
  // fired on generic prose anywhere in the text. Requires a real identifier:
  // a UI save-action handler, or a service-layer DB write call.
  { pattern: /OnClick|OnValidSubmit|MudButton|SaveChangesAsync|ExecuteSqlRawAsync|INSERT INTO|UPDATE\s+\w+\s+SET/i, skills: ["verify-before-done"] },
  { pattern: /\bE2E\b|end-to-end|browser test|playwright|verify in browser|real browser/i, skills: ["playwright-e2e-nordicbees"] },
  // slow|performance|optimi[sz]e now require query/DB context nearby — used
  // to fire on any performance mention (UI layout, build speed, etc).
  { pattern: /N\+1|quer(y|ies)[^\n]{0,40}speed|speed[^\n]{0,40}quer(y|ies)|\b(slow|performance|optimi[sz]e)\b[^\n]{0,40}\b(quer(y|ies)|SQL|database|DbContext|index)\b|\b(quer(y|ies)|SQL|database|DbContext|index)\b[^\n]{0,40}\b(slow|performance|optimi[sz]e)\b/i, skills: ["efcore-performance-nordicbees"] },
  // Dropped "all fields"/"every field"/"entity model"/"new column"/
  // "new field"/"model.*propert" — generic phrasing unrelated to actual
  // CRUD-surface parity. Kept only real path/identifier signals.
  { pattern: /Create\.razor|Edit\.razor|\bCRUD\b/i, skills: ["crud-completeness"] },
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

      // Idempotency guard: if this hook already injected skills into this
      // delegation text (e.g. tool.execute.before firing more than once for
      // the same "task" call), do NOT re-scan and re-inject — the injected
      // wrapper text and skill content themselves still match the same
      // trigger keywords (`.razor`, `Migrations/`, etc.), so without this
      // guard a second firing rebuilds the same skill Set and prepends a
      // second full copy on top of the first. Confirmed root cause of the
      // double-injection incident in Docs/HARNESS_STATUS.md §13. Same
      // pattern already used in nordicbees-reminder.ts's `firstText.text.
      // startsWith("[auto-reminder:")` guard.
      if (originalText.startsWith("IMPORTANT: the following skill(s) are force-injected")) return

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
