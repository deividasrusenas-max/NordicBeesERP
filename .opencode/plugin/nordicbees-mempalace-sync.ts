import type { Plugin } from "@opencode-ai/plugin"

/**
 * NordicBeesERP MemPalace sync hook.
 *
 * Problem this solves: MemPalace's memory of this project goes stale as
 * soon as new code is written — nobody remembers to manually re-run
 * `mempalace mine` after every change. This hook does it automatically.
 *
 * Why this is safe/cheap to run after every task: `mempalace mine` is
 * idempotent — it skips files it has already filed (content-based dedup,
 * confirmed via its own "Files skipped (already filed)" output), so
 * re-running it on the whole project after every single task only
 * actually processes the files that changed, not the whole codebase.
 *
 * Runs after `fixer` (not `coder`) because fixer is the one that commits
 * — by the time fixer's Task-tool call returns, the code is finalized,
 * not mid-edit.
 */
export const NordicBeesMempalaceSync: Plugin = async ({ $, directory }) => {
  return {
    "tool.execute.after": async (input, output) => {
      if (input.tool !== "task") return

      const subagent = (input as any).args?.subagent_type as string | undefined
      if (subagent !== "fixer") return

      try {
        await $`mempalace mine ${directory}`.cwd(directory).nothrow().quiet()
      } catch {
        // Non-fatal — MemPalace sync failing should never block the
        // actual development workflow. Silently skip; nothing to inject
        // back into the session for this one, since it's a background
        // housekeeping step, not something the orchestrator needs to
        // react to.
      }
    },
  }
}
