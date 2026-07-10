# NordicBeesERP P0a Task 1: DB Migration Plan

## Goal
Implement idempotent DB migration adding 9 new tables and altering 4 existing tables per LABELING_PLAN_2.md §66.

## Critical Path

### 1. Modify Single Migration File
- **File:** `Migrations/20260602150000_InitialCreate.cs`
- **Why:** Project rules mandate all schema changes in this single file (no separate migrations)
- **Order of Operations:**
  ```sql
  /* New tables */
  CREATE TABLE IF NOT EXISTS printers (...);
  CREATE TABLE IF NOT EXISTS weighing_stations (...);
  CREATE TABLE IF NOT EXISTS print_jobs (...);
  CREATE TABLE IF NOT EXISTS container_label_events (...);
  CREATE TABLE IF NOT EXISTS container_weight_corrections (...);
  CREATE TABLE IF NOT EXISTS label_templates (...);
  CREATE TABLE IF NOT EXISTS supplier_approvals (...);
  CREATE TABLE IF NOT EXISTS non_conformances (...);
  CREATE TABLE IF NOT EXISTS document_files (...);

  /* Existing table alterations */
  ALTER TABLE containers ...;
  ALTER TABLE deliveries ...;
  ALTER TABLE delivery_lines ...;
  ALTER TABLE business_partners ...;
  ```
- **Validation:**
  ```bash
  grep -c "CREATE TABLE IF NOT EXISTS" Migrations/20260602150000_InitialCreate.cs | grep 9
  grep -c "ALTER TABLE" Migrations/20260602150000_InitialCreate.cs | grep 4
  ```

### 2. Foreign Key Dependencies
- Ensure referenced tables exist before FKs:
  1. `printers` → `weighing_stations`
  2. `weighing_stations` → `print_jobs`
  3. `containers` → `container_label_events`

### 3. Immutability Enforcement
- Add to `NordicBeesErpContext.cs`:
  ```csharp
  public override int SaveChanges() {
    var immutable = ChangeTracker.Entries<ContainerLabelEvent>()
      .Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted);
    if (immutable.Any())
      throw new InvalidOperationException("ContainerLabelEvent yra nekintamas (BRC8 3.3)");
    return base.SaveChanges();
  }
  ```

## Verification Steps
1. `dotnet build` → 0 errors
2. `grep -r "BUCKET_GROUP" --include="*.cs" --include="*.razor" .` → 0 matches
3. DB schema validation:
   ```bash
   mysql -u root nordic_bees_erp_STAGING -e "SHOW TABLES" | grep -E 'printers|container_label_events'
   ```

## Commit Protocol
```bash
dotnet build
git add Migrations/20260602150000_InitialCreate.cs
# bump patch in csproj
git commit -m "P0a: Migrations/20260602150000_InitialCreate.cs — added labeling tables"
```