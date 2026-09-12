#!/usr/bin/env node
// NordicBeesERP patch for opencode-auto-resume@1.1.15.
//
// IMPORTANT — this patch targets a DIFFERENT install location than
// fix-auto-resume-subagent-task-complete.js. That older patch was written
// against opencode-auto-resume@1.1.3, which the harness loaded from a
// locally npm-installed copy at .opencode/node_modules/opencode-auto-resume
// (see .opencode/package.json's own "opencode-auto-resume": "^1.1.3"
// dependency). Since then, opencode.json's own `"plugin"` array pins
// `"opencode-auto-resume@1.1.15"` as a *versioned plugin spec* -- opencode's
// own plugin loader resolves that by downloading/caching the package under
// the user's global opencode cache, NOT under .opencode/node_modules. The
// .opencode/node_modules copy (still 1.1.3, still gitignored, still targeted
// by the older patch) is therefore a dead, unused leftover as far as the
// live plugin is concerned -- confirmed by diffing the two: 1.1.3 has none
// of the loose DONE_CLAIM_PATTERNS that cause the bug below, and its
// task_complete handler still has the old `if (!w.isSubagent)` gate that
// the older patch strips (1.1.15 fixed that gate upstream in a different,
// unconditional-set way -- see BUGLOG "opencode-auto-resume-subagent-task-
// complete-noop" for the original incident and its own note that the fix
// had not yet "survived a real npm install/version bump" -- it didn't; the
// version bump moved the live file out from under that patch entirely).
//
// Problem this patch fixes: 1.1.3 -> 1.1.15 (upgraded 2026-09-10 specifically
// to fix task_complete, per opencode.json's plugin pin) also introduced four
// new, UNANCHORED substring patterns to DONE_CLAIM_PATTERNS:
//   /\bdone\s+with\s+(?:the\s+)?(?:task|work|implementation)/im
//   /\bfinished\s+(?:the\s+)?(?:task|work|implementation)/im
//   /\b(?:all|everything)\s+(?:is\s+)?(?:complete|done|finished)/im
//   /\bnothing\s+(?:else\s+)?(?:left|remaining|to do)/im
// and widened the lookback from the last 3 lines to the last 5. Unlike the
// original patterns (all anchored `^...$`, so they only match a line that
// IS just "done." / "task complete." etc.), these four match a bare phrase
// ANYWHERE inside a line, with no check on what else is in the message. A
// complete, correct final report ending "...Nothing left open from T5" was
// flagged as a contentless done-claim by `\bnothing\s+(?:else\s+)?left/im`
// alone -- forcing a full report rewrite, twice (once per retry attempt,
// since the same long report kept re-matching the same unconditional
// substring test on each backoff check).
//
// Two independent defects were identified; only the first is patched here:
//   (a) the substring match itself -- containsDoneClaimPattern will flag
//       ANY message, however long and substantive, if a loose phrase
//       appears anywhere in its last 5 lines. FIXED below: the function now
//       refuses to test the loose/anchored patterns at all unless the
//       message's total trimmed length is under a small threshold -- i.e.
//       unless the message could plausibly BE a bare "done" claim with
//       nothing else in it. A multi-paragraph report is long regardless of
//       which 5 lines it ends on, so this closes the false positive
//       directly, independent of anything about message framing or count.
//   (b) messages.slice(-3) in checkForToolCallAsText() takes the last 3 RAW
//       messages (any role) before filtering to assistant-only inside the
//       loop, instead of filtering-then-slicing. This means a non-assistant
//       message (e.g. the plugin's own injected corrective prompt, which
//       lands as a new message) can occupy a window slot without evicting
//       an already-examined assistant report, so the same report can be
//       re-tested across retries. NOT patched here: once (a) is fixed, a
//       long report can never match the loose patterns no matter how many
///      times or from which stale window it gets re-examined -- the "twice"
//       in the incident is fully explained by maxRetries > 1 retrying the
//       same (a)-broken check, not by (b) smuggling in different text. (b)
//       is a real latent correctness issue independent of this bug (it
//       could in principle matter for OTHER checks that read the same
//       window, e.g. containsToolCallAsText/containsReadyToContinuePattern)
//       but fixing it is not needed to close this incident, and is left out
//       to keep this patch to the minimal diff that fixes the reported
//       false positive. Revisit if a similar re-triggering symptom shows up
//       on a check other than containsDoneClaimPattern.
//
// See Docs/BUGLOG.md, error class
// `auto-resume-doneclaim-false-positive-on-real-report`, for the full
// incident writeup.
//
// Durability note (worse than the sibling task_complete patch): the target
// file lives in the user's GLOBAL opencode package cache, outside this repo
// and outside .opencode/node_modules, so it is NOT reliably reapplied by
// `.opencode`'s own `npm install`/postinstall the way the sibling patch's
// target is. This script is *also* wired into that same postinstall (best
// effort -- it will reapply whenever someone happens to run `npm install`
// inside `.opencode/`), but the only true trigger for the cache copy being
// rewritten is opencode itself re-fetching the plugin (fresh machine, cache
// clear, or a version bump past 1.1.15). If the "nothing left" / "all done"
// false-positive symptom returns after any of those, re-run this script
// manually: `node .opencode/patches/fix-auto-resume-doneclaim-false-positive.js`.

const fs = require("fs");
const os = require("os");
const path = require("path");

const CACHE_ROOT = process.env.XDG_CACHE_HOME || path.join(os.homedir(), ".cache");
const TARGET = path.join(CACHE_ROOT, "opencode", "packages", "opencode-auto-resume@1.1.15", "node_modules", "opencode-auto-resume", "dist", "index.js");

const ORIGINAL_SNIPPET = `function containsDoneClaimPattern(text) {
  const lines = text.split(\`
\`);
  const lastLines = lines.slice(-5).join(\`
\`);
  return DONE_CLAIM_PATTERNS.some((pat) => pat.test(lastLines));
}`;

const MARKER = "PATCHED (NordicBeesERP, applied by .opencode/patches/fix-auto-resume-doneclaim-false-positive.js)";

const PATCHED_SNIPPET = `function containsDoneClaimPattern(text) {
  // ${MARKER}:
  // a done-claim is only meaningful as a signal of hollow "done with no
  // work description" when the WHOLE message is short enough to plausibly
  // BE just that claim. Without this gate, the loose/unanchored patterns in
  // DONE_CLAIM_PATTERNS (e.g. "nothing left") match a phrase anywhere in the
  // last 5 lines of ANY message, however long and substantive. See
  // Docs/BUGLOG.md error class auto-resume-doneclaim-false-positive-on-real-report.
  const DONE_CLAIM_MAX_LEN = 400;
  if (text.trim().length > DONE_CLAIM_MAX_LEN) {
    return false;
  }
  const lines = text.split("\\n");
  const lastLines = lines.slice(-5).join("\\n");
  return DONE_CLAIM_PATTERNS.some((pat) => pat.test(lastLines));
}`;

function main() {
  if (!fs.existsSync(TARGET)) {
    console.warn(`[fix-auto-resume-doneclaim-false-positive] target not found, skipping: ${TARGET}`);
    return;
  }

  const content = fs.readFileSync(TARGET, "utf8");

  if (content.includes(MARKER)) {
    console.log("[fix-auto-resume-doneclaim-false-positive] already patched, nothing to do.");
    return;
  }

  if (!content.includes(ORIGINAL_SNIPPET)) {
    console.warn(
      "[fix-auto-resume-doneclaim-false-positive] expected original snippet not found -- " +
      "the package version may have changed its internals. Skipping automatic patch; " +
      "re-check Docs/BUGLOG.md (auto-resume-doneclaim-false-positive-on-real-report) and " +
      "patch manually if the done-claim false-positive symptom returns."
    );
    return;
  }

  const patched = content.replace(ORIGINAL_SNIPPET, PATCHED_SNIPPET);
  fs.writeFileSync(TARGET, patched, "utf8");
  console.log("[fix-auto-resume-doneclaim-false-positive] patch applied successfully.");
}

main();
