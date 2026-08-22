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
 */
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
      const injected =
        `\n\n<automated-verification source="dotnet-build-hook">\n` +
        `${status} — exit code ${buildResult.exitCode}\n` +
        `--- stdout (tail) ---\n${buildResult.stdout}\n` +
        `--- stderr (tail) ---\n${buildResult.stderr}\n` +
        `This result was captured automatically by running \`dotnet build\`` +
        ` directly — it is NOT self-reported by the ${subagent} agent.` +
        ` Trust this over any claim of success/failure in the agent's own text.\n` +
        `</automated-verification>`

      // Append to the tool output the orchestrator sees, regardless of
      // what the subagent itself said.
      if (output && typeof output === "object") {
        ;(output as any).output = ((output as any).output ?? "") + injected
      }
    },
  }
}
