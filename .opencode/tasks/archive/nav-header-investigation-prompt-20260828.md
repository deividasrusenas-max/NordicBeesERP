TASK: Investigate data sources and existing patterns needed to (1) restructure NavMenu into grouped sections with a sidebar user block + season-progress bar, and (2) build a functional personalized top header (greeting, working global search, working date-range filter that changes dashboard KPIs). READ-ONLY — no code changes.

CONTEXT:
The mockup shows: sidebar top block "Lakštena ERP / MEDAUS APSKAITA" with a small logo/avatar; nav items grouped under uppercase section labels (FINANSAI, ANALITIKA, DARBAS, SISTEMA) each with badge counts; a "Sezono planas 82%" progress bar pinned at the sidebar bottom. Top header shows "Sveiki, [Name]" + "Sandėlio ir finansų apžvalga · atnaujinta HH:mm", a working search box, a period selector (30d/90d/12mėn), and a user avatar with initials + name. The current app has none of this — flat nav list, "Administratorius" static text, no search, no date filter.

STEPS:
1. Check how the logged-in user's display name is available today: inspect the auth/identity setup (AuthenticationStateProvider usage in Home.razor/MainLayout, User claims) to find what claim holds a real display name (vs. just role "Administratorius"). Report the exact claim type/property to use for "Sveiki, {Name}".
2. Search the entire codebase for "sezona" / "sezon" / "season" / "planas" (case-insensitive) in both C# and Razor files, AND check DB schema (SHOW TABLES / relevant table columns) for anything resembling a seasonal production target/plan. Report whether this is a REAL existing business concept with data, or purely a mockup invention with no backing data. If no data exists, do NOT propose inventing fake data — flag it as a decision point for the human (options: omit, hardcode a placeholder, or scope a real feature separately).
3. Search for existing search/autocomplete implementations already in the app (e.g. customer autocomplete on /orders/create, invoice search used elsewhere) to identify a reusable pattern/service method for building a global search — check what entities have search-friendly service methods already (SearchCustomersAsync, SearchInvoicesAsync, etc.) versus what would need new methods (e.g. searching deliveries, products by name).
4. Read PaymentService.GetDashboardTrendAsync(), GetCashFlowForecastAsync(weeks), and GetAgingReportAsync() signatures and bodies to determine: which of these could accept a period parameter (30/90/365 days) to change their output, and which are inherently "current snapshot, not time-windowed" (e.g. aging report is live AR, likely period-independent). Report exactly what a period selector would change vs. what it structurally cannot change.
5. Confirm the current badge-count sources already wired in NavMenu.razor + GetNavBadgeCountsAsync() (from the v0.16.0 dashboard rebuild) so the grouped-menu restructure can reuse them without re-deriving counts.
6. Check NavMenu.razor's current full structure (all nav items, in order) so the report can propose a grouping mapping (which existing items go under FINANSAI/ANALITIKA/DARBAS/SISTEMA vs. staying top-level like Sandėlis/Produkcija/Užsakymai/Tiekėjai/Išlaidos, matching the mockup's apparent split between ungrouped operational items and grouped office/analysis items).

OUTPUT:
Write findings to .opencode/reports/nav-header-investigation-<timestamp>.md with:
- User display name: claim/property found, or gap if none exists
- Season plan: found with real data / found but empty / does not exist at all — clear recommendation
- Search: reusable service methods found per entity type, gaps requiring new methods
- Period filter: which service methods can accept a period param, which cannot, and why
- Current NavMenu full item list + proposed section-grouping mapping matching the mockup
- Badge counts already available (reusable) vs. new ones the mockup implies but don't exist yet

CRITICAL CONSTRAINTS:
1. READ-ONLY — do not modify, create, or delete any files. Do not touch the database beyond SELECT/SHOW/DESCRIBE.
2. Do not run ./bump-version.sh — no code changes, nothing to commit.
3. Do NOT invent or assume data for "Sezono planas" — if no real data source exists, say so explicitly and stop there; do not silently plan to hardcode a fake percentage as if it were real.
4. Write the complete report to .opencode/reports/nav-header-investigation-<timestamp>.md before finishing.
