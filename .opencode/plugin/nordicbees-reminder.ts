import type { Plugin } from "@opencode-ai/plugin"

/**
 * NordicBeesERP standard-reminder auto-injection.
 *
 * Problem this solves: the user has to retype the same boilerplate
 * reminders in every message (use skills explicitly, use
 * planning-with-files, follow the honesty rule, use the DB MCP not raw
 * mysql, git add specific files not -A, etc.) even though all of this is
 * already written into plan.md/debug.md/code.md. This plugin prepends a
 * short standard reminder to every message sent to the orchestrator
 * agent, so the user can just type the task itself.
 *
 * NOTE: this is intentionally SHORT — it's a nudge/reinforcement, not a
 * duplicate of the full system prompt (which is already loaded via
 * plan.md). Keep it brief so it doesn't bloat every single message.
 */

const REMINDER = `[auto-reminder: follow your standard workflow — use planning-with-files ` +
  `and keep task_plan.md current; delegate to coder/fixer/reviewer with ` +
  `explicit skill names, never rely on auto-trigger; fixer uses the ` +
  `nordicbees-db MCP tool, never raw mysql CLI, and never applies schema ` +
  `changes itself; git add exact files only, never -A; one plain bash ` +
  `command per call, no &&/heredoc; follow the MANDATORY HONESTY RULE — no ` +
  `fabricated completion claims; always mark todo items complete via the ` +
  `todo-tracking mechanism (not just stating "done" in prose) before ` +
  `declaring a task finished.]\n\n`

export const NordicBeesReminder: Plugin = async () => {
  return {
    "chat.message": async (input: any, output: any) => {
      // Only prepend for messages going to the orchestrator agent, and
      // only for the user's own turn (not assistant/tool messages).
      if (input?.agent && input.agent !== "orchestrator") return
      if (output?.message?.role && output.message.role !== "user") return

      const parts = output?.parts
      if (!Array.isArray(parts) || parts.length === 0) return

      const firstText = parts.find((p: any) => p?.type === "text")
      if (firstText && typeof firstText.text === "string") {
        // Avoid double-prepending if this message was already processed
        // (e.g. on retry) or already contains the reminder.
        if (!firstText.text.startsWith("[auto-reminder:")) {
          firstText.text = REMINDER + firstText.text
        }
      }
    },
  }
}
