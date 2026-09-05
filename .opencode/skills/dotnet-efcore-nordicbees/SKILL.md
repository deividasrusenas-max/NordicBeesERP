---
name: dotnet-efcore-nordicbees
description: Core EF Core / MariaDB / migration conventions for NordicBeesERP that must never be violated. Use this whenever writing or editing any Service class that reads/writes via NordicBeesErpContext, whenever touching Migrations/20260602150000_InitialCreate.cs, or whenever adding/changing a database column or enum value.
---

# NordicBeesERP — EF Core & Migration Rules

> Naming note: this skill is NOT the same concept as `Docs/FROZEN.md`
> (which lists specific do-not-touch code blocks like drag-and-drop JS,
> ULAK module, etc.). This skill covers DB-write/migration conventions
> that apply project-wide. The two have been confused before because both
> used the word "FROZEN" loosely in past documentation — they are
> different documents for different purposes.

These rules are not suggestions — violating them causes silent, hard-to-detect bugs (no build error, no exception, just 0 rows changed).

---

## Rule 1: Global NoTracking — never FindAsync + SaveChanges for writes

This project's `NordicBeesErpContext` uses `QueryTrackingBehavior.NoTracking` globally. This means:

```csharp
// WRONG — looks correct, silently does nothing:
var container = await _context.Containers.FindAsync(id); // detached entity!
container.Status = "WRITTEN_OFF";
await _context.SaveChangesAsync(); // 0 rows affected, NO exception thrown
```

```csharp
// CORRECT — use ExecuteSqlRawAsync with positional parameters for all writes:
await _context.Database.ExecuteSqlRawAsync(
    "UPDATE containers SET status = {0}, updated_at = NOW() WHERE id = {1}",
    "WRITTEN_OFF", id);
```

`SaveChangesAsync()` is still correct for genuine **INSERT-only** operations (adding a new tracked-free entity via `.Add()` then `SaveChanges()` works fine for inserts — the NoTracking issue only bites UPDATE/DELETE via a fetched-then-mutated entity). Example: `StockMovement`, `DeliveryLine` inserts are fine as-is.

Before touching any existing method in a Service class, check whether it uses `FindAsync`/`.Find()`/`Where().FirstOrDefaultAsync()` followed by property mutation and `SaveChangesAsync()` — if so, that's a bug even if it compiles and even if it was already there before your change. Flag it or fix it; don't assume existing code is correct just because it builds.

---

## Rule 2: Column names that don't exist in the model may still exist in the DB, and vice versa

This project's C# model classes are sometimes out of sync with the actual DB schema (columns added directly via migration SQL without a matching `[Column]` property, or a property added without the DB column existing yet). **Never assume a column exists — or doesn't — based on the C# model alone.** If you have DB access, check `SHOW COLUMNS FROM <table>` directly. If you don't, say so explicitly in your report rather than guessing.

---

## Rule 3: Migration history is under active reconciliation — see Docs/EF_MIGRATIONS_TASK.md

**This rule changed on 2026-07-17. The old "single migration file" rule is
OBSOLETE and must not be followed anymore** — it directly caused a real
schema-drift incident (see Docs/EF_MIGRATIONS_TASK.md for full details:
4 migration files already exist, `__EFMigrationsHistory` has an orphaned
entry, the model snapshot doesn't match the real DB in ~17 tables).

Until Docs/EF_MIGRATIONS_TASK.md's Path B reconciliation is complete
(check that file for current status before touching any migration):

- **NEVER append a new `migrationBuilder.Sql(...)` block to an
  ALREADY-COMMITTED migration file** (InitialCreate.cs or any other
  existing file), even if it looks like the established pattern in this
  codebase — this is exactly what caused the drift. If you see old code
  that did this, do not copy the pattern.
- **NEVER manually delete a migration file** to "clean up" or
  "consolidate" — this orphans `__EFMigrationsHistory` entries with no
  corresponding file, which is what happened before (see
  Docs/EF_MIGRATIONS_TASK.md's "20260531103421_InitialSchema" finding).
  Use `dotnet ef migrations remove` if a migration genuinely needs
  removing and was never applied anywhere.
- If a genuine new schema change is needed: STOP and tell the user this
  touches migrations, which are currently under reconciliation — do not
  generate or run `dotnet ef migrations add` yourself until
  Docs/EF_MIGRATIONS_TASK.md says Path B is complete and normal migration
  workflow has resumed. Report the needed change instead of implementing
  it.
- All `CREATE TABLE` statements (once normal migrations resume) must use
  `CREATE TABLE IF NOT EXISTS` as a defensive measure, but this is a
  secondary safeguard, not a substitute for correctly tracked migration
  history.

---

## Rule 4: MariaDB enum changes need widen → update → narrow, never a direct MODIFY

Changing an existing enum column (e.g. adding a new allowed value, or renaming one) on a table that may already contain rows with the old value(s) must follow this exact 3-step sequence, each as its own `migrationBuilder.Sql(...)` call:

```sql
-- 1. Widen: add the new value alongside the old one(s)
ALTER TABLE containers MODIFY status ENUM('OLD_VAL','NEW_VAL', ...) NOT NULL;

-- 2. Migrate data
UPDATE containers SET status = 'NEW_VAL' WHERE status = 'OLD_VAL';

-- 3. Narrow: remove the old value now that no rows use it
ALTER TABLE containers MODIFY status ENUM('NEW_VAL', ...) NOT NULL;
```

Skipping step 1 (going straight to a narrowed enum) causes `Data truncated for column` errors on any table with existing rows in the old value — this has happened in this project before. If you're only **adding** a new enum value (not removing/renaming an existing one), a single `MODIFY` that includes both old and new values is safe with no data migration needed.

---

## Rule 5: File paths — check the actual folder before assuming

Model files are not always where a spec document says they are. Known existing locations in this project:
- `Models/WarehouseModule/` — `Container.cs`, `ContainerEnums.cs`, `Delivery.cs`, `DeliveryLine.cs` (NOT `Models/Container.cs` or `Models/Delivery.cs` directly)
- `Models/Printing/` — printing/labeling module models

Always run `list`/`glob` on the target directory before creating a "new" file or assuming an "update" target's path — don't trust a plan document's stated path without checking, since these have been wrong before.

---

## Rule 6: Raw SQL SELECT expressions — match reader Get* call to actual DB return type

Bare boolean/comparison expressions in a raw-SQL `SELECT` list (`<=`, `>`, `=`, etc.) return **Int64**, not Int32 and not bool, from MySQL/MariaDB via MySqlConnector. Calling `reader.GetInt32()` on such a column throws `InvalidCastException` at runtime — no build error, and the C# property type you're assigning to (e.g. a `bool`) gives you false confidence that the read is safe.

```csharp
// WRONG — MySQL returns the comparison as Int64; GetInt32 throws InvalidCastException:
sql = "SELECT ..., (GREATEST(b.quantity - COALESCE(shipped_sum,0),0) <= 0) AS IsShipped ...";
bool isShipped = reader.GetInt32(reader.GetOrdinal("IsShipped")) != 0;

// CORRECT — CASE WHEN forces an Int32 literal:
sql = "SELECT ..., CASE WHEN GREATEST(b.quantity - COALESCE(shipped_sum,0),0) <= 0 THEN 1 ELSE 0 END AS IsShipped ...";
bool isShipped = reader.GetInt32(reader.GetOrdinal("IsShipped")) != 0;
```

- Always wrap computed boolean expressions in `CASE WHEN ... THEN 1 ELSE 0 END` to force Int32 (or use `GetInt64`/`GetBoolean` if the type is known).
- When writing new raw SQL readers, verify the actual return type by checking what the DB returns for the expression — don't assume based on the C# property type.

This is a reviewer-diligence + E2E-test guardrail — no mechanical semgrep rule exists yet (hard to detect statically without running the query). Source: BUGLOG 2026-08-25, error class `rawsql-reader-type-cast-mismatch` (`GetOrderPalletsAsync`, commit `c5599ed` v0.11.279).
