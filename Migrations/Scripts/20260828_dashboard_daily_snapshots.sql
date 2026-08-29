-- ============================================================
-- Migration: dashboard_daily_snapshots table (EF migration 20260828070929_AddDashboardDailySnapshots)
-- ============================================================
-- Apply MANUALLY against staging (and later prod) DB. The agent does NOT run DDL.
-- This table already exists on the dev DB (100.110.26.80) — this script is for
-- staging/prod, which never got it because MigrateAsync() only auto-applies in
-- IsDevelopment() (FROZEN.md, v0.14.11 gating decision).
--
-- Usage (adjust host/user/password/db for the target environment):
--   mariadb -h <STAGING_DB_HOST> -P 3306 -u <user> -p'<password>' nordic_bees_erp --skip-ssl < 20260828_dashboard_daily_snapshots.sql
--
-- NOTE: MariaDB does not support ADD COLUMN IF NOT EXISTS, but CREATE TABLE IF NOT EXISTS
-- is safe to run more than once.

CREATE TABLE IF NOT EXISTS dashboard_daily_snapshots (
    id INT AUTO_INCREMENT PRIMARY KEY,
    snapshot_date DATE NOT NULL,
    barrels_count INT NOT NULL,
    barrels_kg DECIMAL(12,2) NOT NULL,
    buckets_count INT NOT NULL,
    buckets_kg DECIMAL(12,2) NOT NULL,
    unpriced_deliveries_count INT NOT NULL,
    supplier_debt_total DECIMAL(12,2) NOT NULL,
    supplier_debt_count INT NOT NULL,
    created_at DATETIME(6) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE UNIQUE INDEX IX_dashboard_daily_snapshots_snapshot_date ON dashboard_daily_snapshots (snapshot_date);

-- Mark this migration as applied in EF's history table, so a future `dotnet ef database
-- update` (if ever run manually against this DB) does not try to reapply it.
INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
VALUES ('20260828070929_AddDashboardDailySnapshots', '8.0.0')
ON DUPLICATE KEY UPDATE MigrationId = MigrationId;
