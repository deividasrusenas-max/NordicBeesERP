-- Apply manually (dev, nordic_bees_erp_test, prod). MySQL 8.0 + MariaDB 11.8. Idempotent.
INSERT INTO app_settings (setting_key, setting_value)
SELECT 'AutoBarrelNumbering', 'false' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM app_settings WHERE setting_key = 'AutoBarrelNumbering');

-- Switch to the automatic TP codes (hides the "Pradinis ID" fields):
-- UPDATE app_settings SET setting_value = 'true' WHERE setting_key = 'AutoBarrelNumbering';
-- Back to operator start IDs:
-- UPDATE app_settings SET setting_value = 'false' WHERE setting_key = 'AutoBarrelNumbering';

-- EF history row (check __EFMigrationsHistory column names first):
-- INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('20261014110000_AddAutoBarrelNumberingSetting', '10.0.11');
