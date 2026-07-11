---
name: dotnet-efcore-nordicbees
description: Core EF Core / MariaDB / migration conventions for NordicBeesERP that must never be violated. Use this whenever writing or editing any Service class that reads/writes via NordicBeesErpContext, whenever touching Migrations/20260602150000_InitialCreate.cs, or whenever adding/changing a database column or enum value.
---

# NordicBeesERP — EF Core & Migration Rules (FROZEN)

These rules come from `.clinerules/FROZEN.md` and `.clinerules/nordicbees-standards.md`. They are not suggestions — violating them causes silent, hard-to-detect bugs (no build error, no exception, just 0 rows changed).

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

## Rule 3: Single migration file is the source of truth

`Migrations/20260602150000_InitialCreate.cs` is the only migration file in this project. Any new C# model or column must be reflected here in the same commit — never create a second migration file.

- All `CREATE TABLE` statements must use `CREATE TABLE IF NOT EXISTS`.
- If a table **already exists in a live database with real data** (check via `SHOW TABLES LIKE '...'` if you have DB access, or ask if you don't), you cannot just edit its `CREATE TABLE` statement — you must add a separate `ALTER TABLE` block, since the migration has already run there and won't re-execute.
- If a table does **not yet exist** in any live database, it's safe to edit the `CREATE TABLE` statement directly to reflect the final correct schema.

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
