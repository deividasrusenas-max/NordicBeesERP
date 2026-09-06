#!/usr/bin/env node
// NordicBeesERP patch for opencode-auto-resume.
//
// Problem: opencode-auto-resume's task_complete tool only sets
// `w.completionSignaled = true` when `!w.isSubagent`. Every fixer/coder/
// reviewer/verifier session invoked via orchestrator's Task tool is
// ALWAYS isSubagent=true, so task_complete was a silent no-op for exactly
// the population that needs a working completion signal most -- a
// subagent correctly reports done, correctly calls task_complete, and
// the auto-continue nag cycle keeps firing anyway. Confirmed via a real
// fixer transcript (2026-09-06, Components/Layout/TopHeader.razor task):
// task_complete was called twice, correctly, and the session still
// continued ~5 more redundant rounds before naturally ending.
//
// This script re-applies a one-line fix (drop the `if (!w.isSubagent)`
// gate) directly to the installed node_modules copy, since node_modules
// is gitignored here and a bare in-place edit would be silently wiped by
// the next `npm install`. Run automatically via the "postinstall" npm
// script in this package.json -- do not remove that wiring.
//
// See Docs/BUGLOG.md, error class
// `opencode-auto-resume-subagent-task-complete-noop`, for the full
// incident writeup.

const fs = require("fs");
const path = require("path");

const TARGET = path.join(__dirname, "..", "node_modules", "opencode-auto-resume", "dist", "index.js");

const ORIGINAL_SNIPPET = `      if (w) {
        if (!w.isSubagent) {
          w.toolTextRecovered = true;
          w.completionSignaled = true;
          if (w.toolTextTimer) {
            clearTimeout(w.toolTextTimer);
            w.toolTextTimer = null;
          }
        }
        log("info", \`\${short(ctx2.sessionID)} - task_complete called, \${w.isSubagent ? "subagent" : "agent"} done\`);
      }`;

const PATCHED_SNIPPET = `      if (w) {
        // PATCHED (NordicBeesERP, applied by .opencode/patches/fix-auto-resume-subagent-task-complete.js):
        // upstream gated this behind isSubagent, making task_complete a
        // no-op for every fixer/coder/reviewer/verifier session. See
        // Docs/BUGLOG.md error class opencode-auto-resume-subagent-task-complete-noop.
        w.toolTextRecovered = true;
        w.completionSignaled = true;
        if (w.toolTextTimer) {
          clearTimeout(w.toolTextTimer);
          w.toolTextTimer = null;
        }
        log("info", \`\${short(ctx2.sessionID)} - task_complete called, \${w.isSubagent ? "subagent" : "agent"} done\`);
      }`;

const MARKER = "PATCHED (NordicBeesERP";

function main() {
  if (!fs.existsSync(TARGET)) {
    console.warn(`[fix-auto-resume-subagent-task-complete] target not found, skipping: ${TARGET}`);
    return;
  }

  const content = fs.readFileSync(TARGET, "utf8");

  if (content.includes(MARKER)) {
    console.log("[fix-auto-resume-subagent-task-complete] already patched, nothing to do.");
    return;
  }

  if (!content.includes(ORIGINAL_SNIPPET)) {
    console.warn(
      "[fix-auto-resume-subagent-task-complete] expected original snippet not found -- " +
      "the package version may have changed its internals. Skipping automatic patch; " +
      "re-check Docs/BUGLOG.md (opencode-auto-resume-subagent-task-complete-noop) and " +
      "patch manually if the redundant-continuation-after-task_complete symptom returns."
    );
    return;
  }

  const patched = content.replace(ORIGINAL_SNIPPET, PATCHED_SNIPPET);
  fs.writeFileSync(TARGET, patched, "utf8");
  console.log("[fix-auto-resume-subagent-task-complete] patch applied successfully.");
}

main();
