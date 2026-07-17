# NordicBeesERP — EF Core Migrations Architecture Fix

## STATUS: reconciliation complete, manual history cleanup pending

Update this line as work progresses:
- [x] Investigation (Path A vs B determination) — DONE, 2026-07-17, confirmed Path B
- [x] Reconciliation (fix model/DB drift, clean orphaned history) — DONE, 2026-07-17
- [ ] Normal `dotnet ef migrations add` workflow resumed — NOT STARTED (awaiting manual SQL from RECONCILE_MANUAL_STEPS.md)

Until the last box is checked, `.opencode/skills/dotnet-efcore-nordicbees`
Rule 3 forbids any agent from running `dotnet ef migrations add` — any
new schema change request must be reported to the user, not implemented,
until this file says reconciliation is done.

Context document for a dedicated session. Do NOT rush this — this touches
schema management for dev/staging/prod, treat it carefully.

## The problem

There is exactly ONE migration file:
`Migrations/20260602150000_InitialCreate.cs`. Every schema change since the
project started has been added as a new `migrationBuilder.Sql(...)` block
appended to this SAME file, instead of creating a new migration via
`dotnet ef migrations add`.

`__EFMigrationsHistory` already has `20260602150000_InitialCreate` marked
as **Applied** in dev, staging, and prod databases. This means:
`MigrateAsync()` / `dotnet ef database update` sees this migration as
already-run and **skips it entirely** — including any NEW `Sql()` blocks
someone appended to the file after the fact. Manual SQL execution against
the dev DB has been the workaround, but this is fragile and doesn't scale
(prod changes are manual/human-only per AGENTS.md anyway, but even dev/
staging now silently no-ops any new schema change added this way).

## The core question to answer FIRST, before deciding anything

**Does `Migrations/NordicBeesERPContextModelSnapshot.cs` currently match
the ACTUAL database schema exactly?**

This matters because normal EF Core migrations work by diffing the
current C# model against the last migration's snapshot, and generating
ONLY the delta. If the snapshot is accurate, switching to normal
incremental migrations might be as simple as running
`dotnet ef migrations add <NextChangeName>` for the next real change —
no special "baseline" step needed at all.

If the snapshot has drifted from the C# models (likely, since manually
appending `Sql()` blocks to InitialCreate.cs does NOT automatically keep
the snapshot in sync the way a real `dotnet ef migrations add` would),
then the first migration generated with the normal tool might be huge and
wrong — trying to (re)create columns/tables that already exist.

### How to check (do this first, read-only, no changes yet)

1. Read `Migrations/NordicBeesERPContextModelSnapshot.cs` in full.
2. For each table/column it describes, cross-check against the REAL DB
   schema via `SHOW CREATE TABLE <table>` on the documented dev DB
   connection (see `AGENTS.md` — 100.110.26.80, NOT localhost,
   `--skip-ssl` required).
3. Also try (in a throwaway branch, NOT on main): run
   `dotnet ef migrations add ProbeCheck --project NordicBeesERP.csproj`
   and inspect what it generates WITHOUT applying it. If the generated
   `Up()` method is empty or trivial, the snapshot is accurate and this
   is the easy path. If it tries to create/alter things that already
   exist, there's real drift to reconcile first. Delete the probe
   migration file afterward either way — it's just a diagnostic.

## The two possible paths, depending on the answer above

### Path A — snapshot is accurate (best case, likely simpler than expected)

Just start using `dotnet ef migrations add <DescriptiveName>` for every
future schema change, same as any normal EF Core project. Leave
`InitialCreate.cs` and its accumulated history exactly as-is (it's
already correctly marked Applied everywhere) — no baseline migration
needed. This is the standard, best-practice path — nothing exotic.

### Path B — real drift exists between snapshot and actual DB

Need to reconcile before switching. Standard approach for "baselining" an
existing database that wasn't fully created by migrations:

1. Ensure the C# model classes accurately reflect the real DB schema
   (fix any mismatches found in the verification step above — this may
   surface more instances of the same class of bug already found today,
   e.g. the `inspection_by`/`inspection_by_user_id` mismatch in
   `Delivery.cs`).
2. Generate a new migration that captures the model-to-snapshot delta:
   `dotnet ef migrations add ReconcileSnapshot`.
3. Inspect its `Up()` method carefully. For anything that would try to
   create/alter something that ALREADY exists in the real DB, either:
   - Remove that specific operation from the generated migration (since
     it's already true in the DB, just not correctly reflected in the
     snapshot), or
   - Wrap it defensively (check `information_schema.COLUMNS` first, per
     the existing MariaDB `ADD COLUMN IF NOT EXISTS` limitation already
     documented in AGENTS.md).
4. Manually mark this reconciliation migration as Applied in
   `__EFMigrationsHistory` on dev/staging (human-run SQL, not an agent
   action, per AGENTS.md's DDL-is-human-only rule) WITHOUT actually
   running its SQL (since the DB already has this state) — this just
   tells EF Core "this migration's changes are already true here."
5. From this point forward, use `dotnet ef migrations add` normally.

## Constraints that still apply (unchanged from the rest of the project)

- DDL is human-only. An agent may generate migration FILES (C# code) but
  must NEVER run `dotnet ef database update` or raw ALTER/CREATE/DROP
  against dev, staging, or prod. The human runs it manually and confirms
  with `DESCRIBE`/`SHOW CREATE TABLE`.
- MariaDB does not reliably support `ADD COLUMN IF NOT EXISTS` — any
  manually-adjusted migration SQL needs the `information_schema.COLUMNS`
  pre-check pattern instead, if idempotency is required.
- Prod (10.255.8.5) is never touched directly by this process — dev/
  staging first, prod is a separate manual step once fully confirmed.
- This is a genuinely risky category of change (schema management
  tooling itself) — go slower and verify more than usual, even by this
  project's already-cautious standards.

## Investigation findings (2026-07-17) — we are in Path B

Confirmed via independent verification (not just the investigating
agent's own report):

- 4 migration files exist (not 1): InitialCreate, ArtworkTables,
  ArtworkVersionEffectiveDates, DeliverySignatureColumns.
- `__EFMigrationsHistory` on dev only has the first 3 marked Applied —
  DeliverySignatureColumns was never marked Applied, yet all 3 of its
  columns (supplier_signature_svg, supplier_signed_at,
  supplier_signer_name) already exist in the real `deliveries` table —
  confirmed via DESCRIBE. Classic manual-SQL-applied-but-not-recorded
  drift.
- ADDITIONAL FINDING (not in the original report): `__EFMigrationsHistory`
  also contains an ORPHANED entry, `20260531103421_InitialSchema`, with
  no corresponding .cs file anywhere in the repo or its history at HEAD.
  `git log --all` shows this came from an earlier ad-hoc migration
  consolidation (commits dfc4086 → 492f8ee → c1d548b) where an old
  "InitialSchema" migration was replaced by the current "InitialCreate"
  one via direct file deletion rather than `dotnet ef migrations remove`,
  leaving a dead history row behind. Path B reconciliation should also
  clean this up (human-run `DELETE FROM __EFMigrationsHistory WHERE
  MigrationId = '20260531103421_InitialSchema';` on dev/staging, since it
  has no corresponding migration and represents dead bookkeeping).
- Real DB has 68 tables vs ~51 in the model snapshot — real, substantial
  drift (missing entities: orders/order_lines — deliberately removed per
  commit 75fe077 — plus label_templates, print_jobs, printers,
  non_conformances, and others never added to the EF model at all;
  naming mismatches: `units_of_measure` vs snapshot's `units`,
  `warehouse_stock` vs snapshot's `warehouse_stocks`).

## Suggested first message for the new session

"Investigate whether Migrations/NordicBeesERPContextModelSnapshot.cs
matches the real DB schema exactly (see Docs/EF_MIGRATIONS_TASK.md for
the full context and verification steps). Don't change anything yet —
just report which Path (A or B) we're actually in, with evidence."
