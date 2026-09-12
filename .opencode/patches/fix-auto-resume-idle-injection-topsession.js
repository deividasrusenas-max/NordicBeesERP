#!/usr/bin/env node
// NordicBeesERP patch for opencode-auto-resume@1.1.15.
//
// Same target location as fix-auto-resume-doneclaim-false-positive.js (commit
// fa0e2fa, same session): the LIVE plugin file is in the user's global
// opencode package cache, NOT .opencode/node_modules -- opencode.json's
// `"plugin": ["opencode-auto-resume@1.1.15", {...}]` array pins a versioned
// spec that opencode's own loader resolves via its cache, bypassing
// .opencode/node_modules entirely. See that patch's header for the full
// explanation of why the sibling fix-auto-resume-subagent-task-complete.js
// patch's target has been stale/dead since the 1.1.3 -> 1.1.15 version pin.
//
// Problem this patch fixes: opencode-auto-resume schedules
// checkForToolCallAsText() on every session.idle transition, for EVERY
// session, with no isSubagent distinction at all (unlike its sibling
// recovery block -- streaming-failure/silent-dead-stream/open-todos-reminder
// -- which is already gated `if (!w.isSubagent)`, i.e. already restricted to
// non-subagent/top-level sessions only). checkForToolCallAsText is what
// sends the "continue" nudge (source "tool-use"/"ready-to-continue") and the
// "contained no work description" reminder (source "done-claim-no-todos",
// prompt DONE_WITHOUT_DETAILS_PROMPT) -- both landed in a real interactive
// top-level session with no subagent involved, unwanted. This is a DIFFERENT
// bug from the doneclaim-false-positive one patched earlier the same
// session: that one was about containsDoneClaimPattern matching too
// loosely; this one is about checkForToolCallAsText running AT ALL against
// a session it was never meant to run against.
//
// Fix must NOT touch:
//   - the `tool: { task_complete: taskCompleteTool }` registration (a
//     separate plugin hook key, never in the event-dispatch path below --
//     confirmed untouched by grepping for "taskCompleteTool"/"tool:" after
//     patching, see verification section of the BUGLOG entry)
//   - the `if (!w.isSubagent)` recovery block (streaming-failure/silent-
//     dead-stream/open-todos-reminder) -- already top-level-only, explicitly
//     wanted, not part of this incident
//   - the periodic-timer orphan-watch / chunkTimeoutMs stall-recovery
//     (subagentWaitMs, busyStallStrategy) -- explicitly wanted (this is what
//     caught the crashed-subagent case in the 2026-09-09 BUGLOG entry);
//     removing it to fix a problem it didn't cause was explicitly rejected
//
// isSubagent reliability risk (investigated before writing this patch, per
// explicit request -- do not gate on w.isSubagent directly): w.isSubagent is
// set reliably from `parentID` at session.created, but a SEPARATE heuristic
// (elsewhere in this same file, "prevBusyCount > 1 && currentBusy === 1")
// can later flip w.isSubagent to true on a session that was never actually a
// subagent. That heuristic reads getLoneBusySession()/busyCount(), both of
// which iterate the plugin's *global* sessions Map with NO filtering by
// parentID or session family -- confirmed by reading both functions
// directly. A real top-level orchestrator session is structurally safe from
// *self*-contamination (it stays "busy" for the whole duration of its own
// Task-tool dispatches, per the pendingTools/hasInflightTools machinery
// elsewhere in this file, so it's structurally the last session in its own
// tree to go idle). But it is NOT safe from *cross-session* contamination:
// if any other session tracked by the same opencode server (a second
// tab/window, an unrelated orchestrator run) happens to still be busy at the
// exact moment this session finishes, the busy-count arithmetic can stamp
// w.isSubagent = true on it permanently, silently re-enabling the very
// nudges this patch is meant to suppress. Because of this, the guard below
// is NOT `w.isSubagent` -- it's a new field, `isSubagentAtCreation`, set
// once from parentID at session.created and never written anywhere else in
// the file, so the busy-count heuristic cannot touch it.
//
// This patch edits THREE sites (unlike the single-site sibling patches):
//   1. ensureWatch()'s initial watch object -- adds isSubagentAtCreation
//   2. the "session.created" event case -- captures it alongside isSubagent
//   3. the checkForToolCallAsText scheduling guard -- gates on it
// All three are verified present before ANY write happens (see main()) --
// this never applies a partial patch.
//
// See Docs/BUGLOG.md, error class
// `auto-resume-idle-injection-not-scoped-to-subagents`, for the full
// incident writeup.
//
// Durability note (same as the doneclaim-false-positive patch): the target
// lives outside this repo, in the user's global opencode package cache, so
// it is NOT reliably reapplied by `.opencode`'s own npm install/postinstall
// -- that only fires if someone runs `npm install` inside `.opencode/`,
// which does nothing to the global cache. The only true trigger for this
// file being rewritten is opencode itself re-fetching the plugin (fresh
// machine, cache clear, version bump past 1.1.15). After any opencode
// restart, confirm the patch is still live with:
//   grep -c "PATCHED (NordicBeesERP" ~/.cache/opencode/packages/opencode-auto-resume@1.1.15/node_modules/opencode-auto-resume/dist/index.js
// A count of 0 means the cache got rewritten; re-run this script:
//   node .opencode/patches/fix-auto-resume-idle-injection-topsession.js

const fs = require("fs");
const os = require("os");
const path = require("path");

const CACHE_ROOT = process.env.XDG_CACHE_HOME || path.join(os.homedir(), ".cache");
const TARGET = path.join(CACHE_ROOT, "opencode", "packages", "opencode-auto-resume@1.1.15", "node_modules", "opencode-auto-resume", "dist", "index.js");

const MARKER = "PATCHED (NordicBeesERP, applied by .opencode/patches/fix-auto-resume-idle-injection-topsession.js)";

const SITE_1_ORIGINAL = `        isSubagent: false,
        completionSignaled: false,`;
const SITE_1_PATCHED = `        isSubagent: false,
        // ${MARKER}: creation-time-only copy of isSubagent, immune to the
        // busy-count heuristic below that can later flip isSubagent itself.
        isSubagentAtCreation: false,
        completionSignaled: false,`;

const SITE_2_ORIGINAL = `        const parentID = createdProps?.parentID ?? createdProps?.session?.parentID;
        w.isSubagent = typeof parentID === "string" && parentID.length > 0;
        log("debug", \`New session: \${short(sid)} (\${sessions.size})\${w.isSubagent ? " [subagent]" : ""}\`);`;
const SITE_2_PATCHED = `        const parentID = createdProps?.parentID ?? createdProps?.session?.parentID;
        w.isSubagent = typeof parentID === "string" && parentID.length > 0;
        // ${MARKER}: captured once, here only -- never touched by the
        // later busy-count heuristic that can flip w.isSubagent itself.
        w.isSubagentAtCreation = w.isSubagent;
        log("debug", \`New session: \${short(sid)} (\${sessions.size})\${w.isSubagent ? " [subagent]" : ""}\`);`;

const SITE_3_ORIGINAL = `          if (!w.completionSignaled && !w.userCancelled && w.toolTextAttempts < maxRetries) {`;
const SITE_3_PATCHED = `          if (w.isSubagentAtCreation && !w.completionSignaled && !w.userCancelled && w.toolTextAttempts < maxRetries) {
            // ${MARKER}: checkForToolCallAsText (continue / done-claim /
            // done-claim-no-todos / tool-text-recovery / tool-loop / action-
            // intent nudges) is now subagent-only. Gated on isSubagentAtCreation,
            // not isSubagent, per the cross-session contamination risk documented
            // in this patch's header. Does NOT affect the sibling
            // if (!w.isSubagent) recovery block above (streaming-failure/
            // silent-dead-stream/open-todos-reminder, already top-level-only
            // and intentionally left alone), the periodic orphan-watch/
            // stall-recovery timers (also intentionally left alone), or the
            // task_complete tool registration (a separate hook, not in this
            // function at all).`;

function main() {
  if (!fs.existsSync(TARGET)) {
    console.warn(`[fix-auto-resume-idle-injection-topsession] target not found, skipping: ${TARGET}`);
    return;
  }

  const content = fs.readFileSync(TARGET, "utf8");

  if (content.includes(MARKER)) {
    console.log("[fix-auto-resume-idle-injection-topsession] already patched, nothing to do.");
    return;
  }

  const sites = [
    { name: "ensureWatch init", original: SITE_1_ORIGINAL, patched: SITE_1_PATCHED },
    { name: "session.created capture", original: SITE_2_ORIGINAL, patched: SITE_2_PATCHED },
    { name: "checkForToolCallAsText guard", original: SITE_3_ORIGINAL, patched: SITE_3_PATCHED }
  ];

  for (const site of sites) {
    if (!content.includes(site.original)) {
      console.warn(
        `[fix-auto-resume-idle-injection-topsession] expected snippet not found for site "${site.name}" -- ` +
        "the package version may have changed its internals. Skipping automatic patch entirely " +
        "(no partial patch applied); re-check Docs/BUGLOG.md " +
        "(auto-resume-idle-injection-not-scoped-to-subagents) and patch manually if the top-level " +
        "idle-injection symptom returns."
      );
      return;
    }
    const occurrences = content.split(site.original).length - 1;
    if (occurrences !== 1) {
      console.warn(
        `[fix-auto-resume-idle-injection-topsession] snippet for site "${site.name}" is not unique ` +
        `(${occurrences} occurrences) -- refusing to guess which one. Skipping automatic patch entirely.`
      );
      return;
    }
  }

  let patched = content;
  for (const site of sites) {
    patched = patched.replace(site.original, site.patched);
  }
  fs.writeFileSync(TARGET, patched, "utf8");
  console.log("[fix-auto-resume-idle-injection-topsession] patch applied successfully (3 sites).");
}

main();
