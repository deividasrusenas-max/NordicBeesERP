-- ============================================================
-- Draft schema migration: multi-file (label-set) artwork support
-- ============================================================
-- Apply MANUALLY against the dev/staging DB. The agent does NOT run DDL.
--   mariadb -h 100.110.26.80 -P 3306 -u erp_user -p'NordicBees2024' nordic_bees_erp --skip-ssl
-- NOTE: MariaDB does NOT support ADD COLUMN IF NOT EXISTS — run this once only.
-- After applying, reconcile EF migrations (Docs/EF_MIGRATIONS_TASK.md Path B):
--   the C# model now includes artwork_files and artwork_versions.artwork_file_id,
--   but no EF migration class was generated (dotnet-ef unavailable / under reconciliation).
--   Apply this SQL, then run `dotnet ef database update` is NOT needed; just this script.

CREATE TABLE IF NOT EXISTS artwork_files (
    id INT AUTO_INCREMENT PRIMARY KEY,
    asset_id INT NOT NULL,
    label_type VARCHAR(100) NOT NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE UNIQUE INDEX uq_asset_label_type ON artwork_files (asset_id, label_type);

ALTER TABLE artwork_versions ADD COLUMN artwork_file_id INT NULL;
CREATE INDEX IX_artwork_versions_artwork_file_id ON artwork_versions (artwork_file_id);
-- NOTE: Foreign key constraints are intentionally omitted. Add them only with explicit human approval (project rule: no FK without approval).
