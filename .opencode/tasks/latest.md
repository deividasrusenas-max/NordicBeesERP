TASK: Fix two remaining bugs from the last session, then run a Playwright + vision-model visual-fidelity check against the real mockup reference before committing. Full exact reference markup/CSS/algorithms are at .opencode/planning/design-reference/dashboard-mockup.dc.html — READ IT FIRST, it is the literal source of truth (not a description).

═══════════════════════════════════════════
PART 1 — Fix NavMenu group label bug
═══════════════════════════════════════════
Current screenshot shows an empty "DARBAS" section header with nothing under it, and no
"GAMYBA" section. Per the reference file, the correct grouping is:
  FINANSAI → Pardavimai (+ its sub-items)
  ANALITIKA → Statistika (+ its sub-items)
  GAMYBA → Artwork (+ its sub-items)  ← this was mislabeled "DARBAS" last session, fix the label
  SISTEMA → Nustatymai (+ its sub-items)
Remove any stray empty group header ("DARBAS" or any other) that has no items under it —
render only groups that actually contain nav items. Do not add any new nav items or change
routes/role-gating — this is a label/grouping correction only.

═══════════════════════════════════════════
PART 2 — Fix KPI sparkline algorithm (wrong math used last session)
═══════════════════════════════════════════
The reference file's bottom comment block documents TWO different chart math functions —
last session's ChartMathHelper.BuildSmoothPath ported curve() (bezier), but the 4 KPI
sparklines actually use spark() (straight-line segments, per-series min/max normalized),
NOT curve(). curve() is only used for a different, larger 12-month chart that doesn't
exist in our app yet (out of scope, do not build it).
Correct algorithm for KPI sparklines (port exactly):
  spark(vals, w, h):
    min = min(vals), max = max(vals), sp = (max - min) || 1  // avoid div-by-zero
    pts[i] = [ i * (w/(n-1)), h - 4 - ((vals[i]-min)/sp) * (h-12) ]
    line = "M{p0x} {p0y}" then "L{pix} {piy}" for each subsequent point
    area = line + " L{w} {h} L0 {h} Z"
Add `ChartMathHelper.BuildSparkPath(decimal[] values, double w, double h)` returning
(string line, string area) using this exact formula (CultureInfo.InvariantCulture for all
numeric-to-string formatting). Replace the KPI cards' use of BuildSmoothPath with this new
BuildSparkPath. Leave BuildSmoothPath in place (unused for now) in case a future task adds
the 12-month purchases chart from the reference file — do not delete it.
If a KPI's Series has fewer than 2 points, render a flat horizontal line at the vertical
midpoint (same fallback as before, just using the corrected function family).

═══════════════════════════════════════════
PART 3 — Visual fidelity verification loop (NEW — Playwright + vision model)
═══════════════════════════════════════════
After Parts 1-2 build successfully (dotnet build 0 errors):
1. Start the app locally in the background (dotnet run, dev environment) if not already running.
2. Use the playwright MCP tool to navigate to the local Home page (/) at viewport 1600x1000,
   wait for full render (including the SVG sparklines and CSS conic-gradient donut), and
   take a screenshot. Save it to .opencode/planning/design-reference/actual-render-<timestamp>.png.
3. Invoke the `design-review` subagent (vision-capable, Qwen2.5-VL-7B, only 16384 ctx —
   do NOT have it read the full ~15KB reference .dc.html file, that will blow its context
   budget alongside the screenshot's vision tokens). Give it ONLY: (a) the screenshot path,
   (b) this short checklist inline in the prompt (not a file read):
   - Sidebar shows FINANSAI/ANALITIKA/GAMYBA/SISTEMA group labels, no empty groups, no "DARBAS"
   - KPI card sparkline colors: card 1 (Statinės) amber #c47a10, card 2 (Kibirai) teal #2f7d6b,
     card 3 (Neįkainotos) sand #d9a13f, card 4 (Skolos) rose #b3402f
   - Debt-aging donut is a circular conic-gradient ring (not a broken/missing shape), colors
     progress teal→sand→#c9761f→rose across the 4 buckets (exact proportions will differ from
     mockup since real data differs — that's fine, only the COLOR MAPPING per bucket matters)
   - Weekly revenue bars: first bar rose, rest not-rose (amber/sand family) — exact per-bar
     values will differ from mockup (real data), only the color-by-position pattern matters
   - Overall background is warm sand (#efece6 page, #fff cards), NOT default white/gray
     Material design, and cards have NO drop shadow (flat, 1px border only)
4. If design-review reports a genuine mismatch (wrong colors, broken/missing element, empty
   group headers still present), fix the specific issue and repeat steps 2-3 ONCE more
   (maximum 2 verification rounds total). If still mismatched after 2 rounds, STOP and report
   BLOCKED with the exact design-review findings — do not loop indefinitely (per BUGLOG's
   documented loop-incident history, a bounded retry with a clear stop condition is required).
5. Differences that are due to REAL DATA being sparse/different from the mockup's fictional
   demo data (e.g. only 1 bar has a nonzero value because there's little real cash-flow-forecast
   data yet, or the donut is almost entirely one color because real debt actually is
   concentrated in one aging bucket) are NOT bugs — do not attempt to fabricate or pad data to
   look more like the mockup. Only flag/fix actual styling, color-mapping, or structural
   mismatches.

CRITICAL CONSTRAINTS:
1. The reference file .opencode/planning/design-reference/dashboard-mockup.dc.html uses
   `{{ }}` template placeholders and `sc-for` directives that are NOT real HTML/Blazor syntax
   — it's a design tool's template format kept only for exact CSS values, colors, and the
   algorithm comment block at the bottom. Do not try to run or parse it as executable code.
2. Do not change any business logic, EF Core queries, or data sources in this task — Parts 1-2
   are presentation-only fixes; Part 3 is verification-only (no code changes from Part 3 itself
   unless a genuine mismatch is found and needs a small targeted fix).
3. dotnet build must report 0 errors before Part 3 begins.
4. VERSION BUMP: bugfix, use `./bump-version.sh` (default patch) — do NOT manually set version.
5. Write the complete work report — files touched, design-review verdict(s) from Part 3
   (include what it found on each round), confirmation of the version bump commit hash — to
   .opencode/reports/dashboard-navmenu-sparkline-fix-<timestamp>.md.
