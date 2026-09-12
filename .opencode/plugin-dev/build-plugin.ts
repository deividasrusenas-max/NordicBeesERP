#!/usr/bin/env bun
/**
 * NordicBeesERP harness-trace plugin builder.
 *
 * Problem this solves: .opencode/plugin-dev/nordicbees-harness-trace.ts (the
 * full, test-covered source) and .opencode/plugin/nordicbees-harness-trace.ts
 * (the copy opencode actually loads) drifted — the live copy needed its
 * exports reduced to a single `Plugin` binding (opencode's loader fails with
 * `error="Plugin export is not a function"` otherwise; see that source
 * file's own header for how this was discovered), and that reduction was
 * done once, by hand, with a sed script and a manually-written comment. That
 * is exactly the kind of fix that silently rots: the tests exercise the dev
 * copy, opencode executes the live copy, and nothing forces them to match
 * after the next edit to the dev copy.
 *
 * This file is the structural fix: a deterministic, pure transformation from
 * dev-source text to live-copy text, plus a thin CLI wrapper that reads one
 * file and writes the other. The pure transformation (`stripToSingleExport`,
 * `buildLiveCopy`) is exported and covered by a drift-detection test in
 * nordicbees-harness-trace.test.ts, which asserts the CURRENT on-disk live
 * copy is byte-identical to what this generator produces from the CURRENT
 * on-disk dev source right now — so an edit to either file that isn't
 * followed by running this generator fails that test immediately, instead
 * of silently shipping untested code.
 *
 * Determinism: `buildLiveCopy` is a pure string transformation with no
 * timestamps, random IDs, or other non-reproducible content in its output —
 * the same dev-source text always produces byte-identical output. This is
 * required for the drift-detection test to be meaningful at all (a
 * generator that embeds "generated at <now>" would never match a
 * previously-generated file, making the test permanently red for the wrong
 * reason).
 *
 * Usage: bun .opencode/plugin-dev/build-plugin.ts
 *   (or: npx -y bun .opencode/plugin-dev/build-plugin.ts, if bun is not on PATH)
 * Reads .opencode/plugin-dev/nordicbees-harness-trace.ts, writes
 * .opencode/plugin/nordicbees-harness-trace.ts. Run it after every edit to
 * the dev source, before committing.
 */

import { readFileSync, writeFileSync } from "fs"
import { join } from "path"

/** The single export the live copy is allowed to keep. */
export const PLUGIN_EXPORT_NAME = "NordicBeesHarnessTrace"

/**
 * Fixed, timestamp-free banner prepended to every generated live copy. Kept
 * as its own exported constant (rather than inlined in `buildLiveCopy`) so
 * the drift-detection test can assert on its exact content too, not just on
 * round-trip equality.
 */
export const GENERATED_HEADER = `// ============================================================================
// GENERATED FILE — DO NOT EDIT DIRECTLY.
//
// This is a machine-generated copy of the plugin-dev source, with every
// export except the plugin binding itself stripped — opencode's plugin
// loader requires the loaded file's ONLY export to be the Plugin function
// (see that source file's own header for how this was discovered: a real
// smoke test failed with error="Plugin export is not a function" the first
// time this file additionally exported its pure-logic helpers).
//
// Source of truth: .opencode/plugin-dev/nordicbees-harness-trace.ts
// Regenerate with: bun .opencode/plugin-dev/build-plugin.ts
//   (or: npx -y bun .opencode/plugin-dev/build-plugin.ts, if bun is not on PATH)
//
// A test in .opencode/plugin-dev/nordicbees-harness-trace.test.ts asserts
// this file is byte-identical to the generator's current output. Editing
// this file directly, or editing the dev source without regenerating, will
// make that test fail until this file is regenerated again.
// ============================================================================

`

/**
 * Strips `export ` from every top-level `export const|function|type|let|var|
 * class|interface NAME` declaration EXCEPT the one named `keepExportName`,
 * and removes any `export default ...` line entirely. Every other line
 * (comments, imports, non-exported code, the bodies of multi-line
 * declarations) passes through unchanged.
 *
 * Line-based and deliberately simple, matching this codebase's actual
 * export style (every export is a single-line `export <keyword> NAME`
 * opener, even for multi-line function signatures/object literals) rather
 * than a full TypeScript parse — a full parse would handle export styles
 * this file doesn't use, at the cost of a dependency this repo's plugins
 * don't otherwise need.
 */
export function stripToSingleExport(source: string, keepExportName: string): string {
  const exportDeclRe = /^export\s+(const|function|type|let|var|class|interface)\s+([A-Za-z_$][A-Za-z0-9_$]*)\b/
  const exportDefaultRe = /^export\s+default\b/
  const lines = source.split("\n")
  const out: string[] = []
  for (const line of lines) {
    if (exportDefaultRe.test(line)) {
      // Drop the line entirely — a stray bare identifier statement left
      // behind by only removing "export default " would be pointless.
      continue
    }
    const m = line.match(exportDeclRe)
    if (m && m[2] !== keepExportName) {
      out.push(line.slice("export ".length))
      continue
    }
    out.push(line)
  }
  return out.join("\n")
}

/** Full dev-source -> live-copy transformation: banner + stripped exports. */
export function buildLiveCopy(devSource: string, keepExportName: string = PLUGIN_EXPORT_NAME): string {
  return GENERATED_HEADER + stripToSingleExport(devSource, keepExportName)
}

const DEV_SOURCE_FILENAME = "nordicbees-harness-trace.ts"

export function devSourcePath(): string {
  return join(import.meta.dir, DEV_SOURCE_FILENAME)
}

export function liveCopyPath(): string {
  return join(import.meta.dir, "..", "plugin", DEV_SOURCE_FILENAME)
}

if (import.meta.main) {
  const devPath = devSourcePath()
  const livePath = liveCopyPath()
  const devSource = readFileSync(devPath, "utf8")
  const output = buildLiveCopy(devSource)
  let previous: string | null = null
  try {
    previous = readFileSync(livePath, "utf8")
  } catch {
    // No existing live copy — that's fine, this is the first generation.
  }
  writeFileSync(livePath, output, "utf8")
  const changed = previous !== output
  console.log(`Read:  ${devPath} (${devSource.length} bytes)`)
  console.log(`Wrote: ${livePath} (${output.length} bytes)${changed ? "" : " — unchanged"}`)
}
