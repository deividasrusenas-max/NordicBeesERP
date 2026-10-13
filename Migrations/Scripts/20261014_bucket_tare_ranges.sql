-- Apply manually: dev (nordic_bees_erp), test (nordic_bees_erp_test), then prod. Works on MySQL 8.0 and MariaDB 11.8.
-- NOT idempotent for the column/table (no IF [NOT] EXISTS by design): run once per database.
-- Verify afterwards: DESCRIBE bucket_tare_ranges; DESCRIBE containers; SELECT * FROM bucket_tare_ranges;

ALTER TABLE containers ADD tare_manual TINYINT(1) NOT NULL DEFAULT 0;

CREATE TABLE bucket_tare_ranges (
    id INT NOT NULL AUTO_INCREMENT,
    min_brutto_kg DECIMAL(6,2) NOT NULL,
    max_brutto_kg DECIMAL(6,2) NOT NULL,
    tare_kg DECIMAL(6,2) NOT NULL,
    sort_order INT NOT NULL,
    is_active TINYINT(1) NOT NULL DEFAULT 1,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    CONSTRAINT PK_bucket_tare_ranges PRIMARY KEY (id)
) CHARACTER SET utf8mb4;

INSERT INTO bucket_tare_ranges (min_brutto_kg, max_brutto_kg, tare_kg, sort_order, is_active)
SELECT s.min_brutto_kg, s.max_brutto_kg, s.tare_kg, s.sort_order, 1 FROM (
    SELECT 1.00 AS min_brutto_kg, 16.00 AS max_brutto_kg, 0.40 AS tare_kg, 1 AS sort_order
    UNION ALL SELECT 16.01, 40.00, 0.80, 2
    UNION ALL SELECT 40.01, 55.00, 1.10, 3) s
WHERE NOT EXISTS (SELECT 1 FROM bucket_tare_ranges);

-- EF history rows (check the column names of __EFMigrationsHistory first; same ProductVersion as earlier rows):
-- INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('20261014100000_AddBucketTareRanges', '10.0.11');

-- Down:
-- DROP TABLE bucket_tare_ranges;
-- ALTER TABLE containers DROP COLUMN tare_manual;
