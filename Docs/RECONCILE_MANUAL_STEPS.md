---
# Manual Steps for EF Migrations Reconciliation

**Generated:** 2026-07-17  
**Context:** Path B reconciliation — see `Docs/EF_MIGRATIONS_TASK.md`

---

## ⚠️ IMPORTANT: Verify Staging Schema First

**Before running any SQL on staging:** The dev DB (100.110.26.80) was used as the reference for model reconciliation. Staging/prod may have diverged. **You must verify that the 6 tables modeled here (`label_templates`, `print_jobs`, `printers`, `non_conformances`, `units_of_measure`, `warehouse_stocks`) have identical schemas on staging before applying the same `__EFMigrationsHistory` updates there.**

Run `SHOW CREATE TABLE <table>` on staging for each and compare against the dev schemas documented in the migration file `Migrations/20260717201656_ReconcileSnapshot.cs`.

---

## SQL to Run on DEV Database (100.110.26.80)

### 1. Remove orphaned migration history entry

This entry has no corresponding .cs file in the repo (it came from an old ad-hoc migration consolidation that deleted the file without using `dotnet ef migrations remove`).

```sql
DELETE FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260531103421_InitialSchema';
```

### 2. Mark DeliverySignatureColumns as Applied

This migration's columns (`supplier_signature_svg`, `supplier_signed_at`, `supplier_signer_name`) were already manually applied to the `deliveries` table but the history row was never inserted.

```sql
INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260708120000_DeliverySignatureColumns', '8.0.0');
```

### 3. Mark ReconcileSnapshot as Applied

This migration captures the model-to-snapshot delta for the 6 tables now correctly modeled. **DO NOT RUN THE MIGRATION'S SQL** — the tables/columns already exist in the dev DB. This INSERT only tells EF Core "these changes are already true here."

```sql
INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260717201656_ReconcileSnapshot', '8.0.0');
```

---

## What the ReconcileSnapshot Migration Contains (For Reference)

The generated migration `Migrations/20260717201656_ReconcileSnapshot.cs` includes operations that **would** create/alter objects that **already exist** in the dev DB. These operations MUST NOT be executed — the INSERT above is the only action needed.

### Operations targeting already-existing objects (DO NOT RUN):

| Operation | Target | Status in Dev DB |
|-----------|--------|------------------|
| `DropTable("units")` | `units` table | ✅ Does not exist (correct — real table is `units_of_measure`) |
| `DropColumn("expiration_date", "warehouse_stocks")` | `warehouse_stocks.expiration_date` | ✅ Does not exist (correct) |
| `DropColumn("honey_batch_id", "warehouse_stocks")` | `warehouse_stocks.honey_batch_id` | ✅ Does not exist (correct) |
| `AddColumn` (10 columns) | `deliveries` table | ⚠️ **ALL 10 ALREADY EXIST** — manually applied previously |
| `AlterColumn("default_vat_rate", "company_settings")` | `company_settings.default_vat_rate` | ⚠️ **WRONG** — dev has `decimal(5,2)`, migration wants `decimal(65,30)` |
| `AddColumn("no_email", "business_partners")` | `business_partners.no_email` | ⚠️ **ALREADY EXISTS** |
| `CreateTable("label_templates")` | `label_templates` | ⚠️ **ALREADY EXISTS** |
| `CreateTable("non_conformances")` | `non_conformances` | ⚠️ **ALREADY EXISTS** |
| `CreateTable("print_jobs")` | `print_jobs` | ⚠️ **ALREADY EXISTS** |
| `CreateTable("units_of_measure")` | `units_of_measure` | ⚠️ **ALREADY EXISTS** |

### Why these appear in the migration

The model snapshot (`Migrations/NordicBeesERPContextModelSnapshot.cs`) had drifted significantly from the real DB. The new C# models now correctly match the real DB, but the snapshot still reflects the old (incorrect) state. EF Core's diff generates operations to "fix" the DB to match the new model — but since the DB was already manually updated to match, those operations are redundant or wrong.

---

## After Running the SQL

1. Verify the history table:

```sql
SELECT * FROM `__EFMigrationsHistory` ORDER BY `MigrationId`;
```

Expected result (new rows at bottom):
```
20260602150000_InitialCreate           8.0.0
20260701192157_ArtworkTables           8.0.0
20260702120000_ArtworkVersionEffectiveDates  8.0.0
20260708120000_DeliverySignatureColumns  8.0.0   ← NEW
20260717201656_ReconcileSnapshot        8.0.0   ← NEW
```

2. Run the application and verify no migration-related errors on startup.

3. **Then and only then** — normal `dotnet ef migrations add <Name>` workflow can resume for future schema changes.

---

## Staging / Prod

Repeat the same 3 SQL statements on staging **after** verifying the 6 table schemas match dev. Prod is a separate manual process per AGENTS.md.
---
