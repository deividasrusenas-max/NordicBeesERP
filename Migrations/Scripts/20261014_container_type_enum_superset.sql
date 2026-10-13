-- Apply manually (dev, nordic_bees_erp_test, then prod after verifying with: SHOW COLUMNS FROM containers LIKE 'container_type').
-- Works on MySQL 8.0 and MariaDB 11.8. Idempotent. Existing rows are not changed.
ALTER TABLE containers MODIFY container_type ENUM('BARREL','BUCKET','BUCKET_GROUP') NOT NULL;
ALTER TABLE delivery_lines MODIFY container_type ENUM('BARREL','BUCKET','BUCKET_GROUP') NOT NULL;

-- Record in EF history so `dotnet ef database update` does not re-run it (check the history table's column names first):
-- INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('20261014090000_ContainerTypeEnumAcceptsBucketGroup', '10.0.11');
