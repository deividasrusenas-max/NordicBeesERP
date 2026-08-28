TASK: Two fixes to the Home dashboard. (1) KPI cards show 0 because CurrentValue is sourced only from today's snapshot row, which doesn't exist yet (worker runs once daily at 03:00) — fix to compute CurrentValue live. (2) Abandon MudChart for the KPI sparklines, weekly revenue chart, and debt-aging ring — the approved mockup renders these as hand-crafted raw SVG paths and a CSS conic-gradient, NOT any chart library, so MudChart can never visually match it. Port the exact math below verbatim.

═══════════════════════════════════════════
PART 1 — Fix KPI CurrentValue (data bug, screenshot shows 0 kg / 0 kg / 0 / 0 €)
═══════════════════════════════════════════
Root cause confirmed by reading PaymentService.cs: `GetDashboardTrendAsync()` does
`trend.CurrentValue = todaySnap != null ? selector(todaySnap) : 0m;` — this requires
today's row to already exist in dashboard_daily_snapshots, which it won't on any day
before the 03:00 worker has run. Home.razor's OnInitializedAsync ALREADY computes the
correct live values (_barrelNetWeight, _bucketNetWeight, _unpricedDeliveries, _totalDebt
from ContainerService/DeliveryService) but they are no longer bound to the KPI cards.

FIX: Change `IPaymentService.GetDashboardTrendAsync` signature to accept the four live
current values as parameters:
  Task<DashboardTrendResult> GetDashboardTrendAsync(decimal currentBarrelsKg, decimal currentBucketsKg, int currentUnpricedDeliveries, decimal currentSupplierDebtTotal)
Inside the method: use the PASSED-IN live value as `trend.CurrentValue` for each KPI
(never read CurrentValue from todaySnap). Snapshots are used ONLY to build:
  (a) Value7DaysAgo / DeltaAbsolute / DeltaPercent (compare live current vs. the snapshot
      from ~7 days ago, if it exists)
  (b) the historical Series for the sparkline (snapshot rows from the last 14 days,
      PLUS append a synthetic final point using today's live value + today's date, so the
      sparkline always ends at the true current value even before the worker has run for
      today)
If snapshots.Count < 2 (brand new feature, no real history yet): DeltaAbsolute/DeltaPercent
stay null ("—"), and Series can either be empty OR just the single live point — decide
based on what renders better with the Part 2 sparkline code below (a single point should
render as a flat line, not crash).
Update the call site in Home.razor's OnInitializedAsync to pass
`_barrelNetWeight, _bucketNetWeight, _unpricedDeliveries, _totalDebt` (already computed
a few lines earlier in the same method) into `GetDashboardTrendAsync(...)`.
Update TestPaymentService test stub to match the new signature (it was likely stubbed
against the old signature in the v0.16.0/v0.17.0 work).

═══════════════════════════════════════════
PART 2 — Replace MudChart with literal ported SVG/CSS (pixel-match the mockup)
═══════════════════════════════════════════
The mockup's actual rendering (extracted directly from its source — this is not a
description, it is the literal algorithm, port it as closely as C#/Razor allows):

### 2a. KPI sparkline (4 cards: Statinės, Kibirai, Neįkainotos, Skolos)
Mockup uses a smooth cubic-bezier SVG path built from N data points, viewBox "0 0 240 46":
```js
// JS source (port to a C# helper, e.g. in a static ChartMathHelper class):
curve(vals, w, h, max) {
  const pts = vals.map((v, i) => [i * (w / (vals.length - 1)), h - (v / max) * (h - 12)]);
  let d = `M${pts[0][0]} ${pts[0][1].toFixed(1)}`;
  for (let i = 1; i < pts.length; i++) {
    const p = pts[i - 1], c = pts[i], mx = (p[0] + c[0]) / 2;
    d += ` C${mx.toFixed(1)} ${p[1].toFixed(1)} ${mx.toFixed(1)} ${c[1].toFixed(1)} ${c[0].toFixed(1)} ${c[1].toFixed(1)}`;
  }
  return { d, pts };
}
```
Port this exactly as a C# static method `BuildSmoothPath(decimal[] values, double w, double h, decimal max)` returning the `d` attribute string (use `CultureInfo.InvariantCulture` for all `.ToString("0.0")` formatting so decimal points, not commas, are emitted). `max` = the max value in the series (or the max across the 14-day series, with a small headroom multiplier like 1.1 so the line doesn't touch the top edge — mockup uses hardcoded `max` per chart type, use `values.Max() * 1.1m` as a sane default, minimum 1 to avoid divide-by-zero when all values are 0).
Render per KPI card:
```html
<svg viewBox="0 0 240 46" preserveAspectRatio="none" style="width:100%;height:46px;display:block">
  <path d="{areaPath}" fill="{kpiColor}" opacity="0.09"></path>
  <path d="{linePath}" fill="none" stroke="{kpiColor}" stroke-width="1.8" stroke-linecap="round"></path>
</svg>
```
Where `linePath` = `BuildSmoothPath(...)`, and `areaPath` = `linePath + " L{w} {h} L0 {h} Z"` (closes the shape down to the bottom for the fill). Per-card `kpiColor`: Statinės sandėlyje → `#c47a10` (amber/accent), Kibirai sandėlyje → `#2f7d6b` (teal/success), Neįkainotos → `#d9a13f` (sand), Skolos tiekėjams → `#b3402f` (rose/danger) — these are the exact mockup hex values, use them directly instead of the CSS var() indirection that MudChart couldn't resolve.
If a card's Series has fewer than 2 points, render a flat horizontal line at the vertical midpoint instead of calling BuildSmoothPath (avoid division-by-zero / degenerate path).

### 2b. Weekly revenue bars ("Laukiamos įplaukos")
NOT a line/area chart — the mockup renders this as simple colored bar rectangles, one per week bucket, color-coded by urgency: the first bucket (overdue/"Pradelsta") is rose `#b3402f`, the immediate next week is amber `#c47a10`, and all further-future weeks are sand `#d9a13f`. Build this as plain HTML/CSS flexbox bars (no SVG needed, no chart library):
```html
<div style="display:flex; align-items:flex-end; gap:8px; height:200px; padding-top:20px">
  @foreach (var week in weeklyBars) {
    <div style="flex:1; display:flex; flex-direction:column; align-items:center; gap:6px">
      <div style="font-family:'Geist Mono',monospace; font-size:11px; color:var(--lakstena-text-secondary)">@week.FormattedAmount</div>
      <div style="width:100%; max-width:32px; height:@(week.BarHeightPx)px; background:@week.Color; border-radius:4px 4px 0 0"></div>
      <div style="font-size:10px; color:var(--lakstena-text-secondary); white-space:nowrap">@week.Label</div>
    </div>
  }
</div>
```
`BarHeightPx` = `(week.Amount / maxAmountAcrossWeeks) * 160` (160px max bar height, leaving room for the value label above). First bar = overdue amount from `_agingReport` (rose), map it in alongside the existing `_cashFlowForecast` weeks (amber for the nearest week, sand for the rest) — reuse the exact same data already fetched (`_agingReport.TotalOverdue` for the overdue bar, `_cashFlowForecast` for the rest), just change the rendering, not the data source.

### 2c. Debt-aging donut ("Įsiskolinimų senėjimas")
Pure CSS conic-gradient — no SVG, no chart library at all:
```js
// JS source (port the math, not the JS itself):
const stops = buckets.map(b => {
  const from = (acc / total) * 100; acc += b.value;
  return `${b.color} ${from}% ${(acc/total)*100}%`;
});
agingRing = `conic-gradient(from -90deg, ${stops.join(', ')})`;
```
Port to C#: compute cumulative percentage stops across the 4 aging buckets (0-30, 31-60, 61-90, 90+) using their `TotalAmount` values from `_agingReport`, build the `conic-gradient(from -90deg, color1 0% x1%, color2 x1% x2%, ...)` string, and apply it as an inline `background` style on a circular div:
```html
<div style="position:relative; width:200px; height:200px; margin:0 auto">
  <div style="width:100%; height:100%; border-radius:50%; background:@agingRingCss"></div>
  <div style="position:absolute; inset:22px; border-radius:50%; background:#fff; display:flex; flex-direction:column; align-items:center; justify-content:center">
    <span style="font-size:11px; color:var(--lakstena-text-secondary)">Bendra skola</span>
    <span style="font-family:'Geist Mono',monospace; font-weight:700; font-size:22px">@FormatAmountHelper.Trim(_agingReport.TotalOverdue, 2) €</span>
  </div>
</div>
```
Bucket colors (exact mockup hex, green→red severity): 0-30 → `#2f7d6b`, 31-60 → `#d9a13f`, 61-90 → `#c47a10`, 90+ → `#b3402f`. If `_agingReport.TotalOverdue` is 0 (no debt at all), render a flat neutral ring (`background: rgba(27,25,23,.08)`) instead of a conic-gradient with a divide-by-zero.
Keep the existing 0-30/31-60/61-90/90+ legend row below the ring unchanged.

CRITICAL CONSTRAINTS:
1. Remove the MudChart usage (and the now-unused `ChartSeries`/`ChartOptions`/`ChartPalette`/`@using MudBlazor.Charts` if nothing else on the page needs it — check the Warehouse-composition stacked bar first, it may still legitimately use MudChart; only remove what Part 2 replaces) for the 4 KPI sparklines, the weekly revenue chart, and the aging donut specifically. Do NOT touch the "Sandėlio sudėtis" stacked bar unless it's also broken (it wasn't mentioned as broken).
2. All new SVG path / gradient string building happens in C# `@code` block or a small static helper class — use `CultureInfo.InvariantCulture` everywhere a decimal is formatted into a string that goes into SVG/CSS syntax (commas instead of periods will break the path/gradient silently).
3. Use the exact hex colors given above — these are the real mockup values, not approximations.
4. Guard every division (max value = 0, total = 0, single data point) so the dashboard never throws on empty/sparse data — this is a fresh feature with little history yet.
5. dotnet build must report 0 errors. No Playwright — Deividas checks the UI manually.
6. VERSION BUMP: this is a bugfix + visual-fidelity patch, not a new feature. Use `./bump-version.sh` (default patch bump) — do NOT manually set a minor/major version this time.
7. Write the complete work report — every file touched, confirmation of the version bump commit hash — to .opencode/reports/dashboard-chart-fidelity-fix-<timestamp>.md.
