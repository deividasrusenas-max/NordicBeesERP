# DB Migration Implementation Plan (Task 1)

## Objective
Implement 9 new tables and 4 ALTER statements in `Migrations/20260602150000_InitialCreate.cs` per LABELING_PLAN_2.md specifications while maintaining BRC8 compliance.

## Critical Constraints
- **Migration file structure**: Must maintain existing group structure (Group 1-5)
- **BRC8 compliance**: 
  - `container_label_events` must be INSERT ONLY (BRC8 3.3)
  - `weighing_stations` requires calibration fields (BRC8 6.4)
  - `containers` requires weight verification fields (BRC8 3.7)
- **Idempotency**: All changes must use `IF NOT EXISTS`
- **Formatting**: Match existing style (backticks, ENGINE/CHARSET, comments)

## Implementation Sequence

### 1. weighing_stations Table (Group 3)
**Insertion point**: After `deliveries` table (line 776)
```csharp
migrationBuilder.Sql(@"
    CREATE TABLE IF NOT EXISTS `weighing_stations` (
  `id` int NOT NULL AUTO_INCREMENT,
  `name` varchar(100) NOT NULL,
  `warehouse_id` int NOT NULL,
  `printer_id` int NOT NULL,
  `pi_base_url` varchar(200) DEFAULT NULL,
  `default_container_type` enum('BARREL','BUCKET') DEFAULT NULL,
  `min_weight_kg` decimal(5,3) NOT NULL DEFAULT 0.500,
  `scale_protocol` enum('TOLEDO','METTLER','CAS','KERN','NONE') NOT NULL DEFAULT 'NONE',
  `scale_regex` varchar(200) DEFAULT NULL,
  `last_calibration_date` date DEFAULT NULL,
  `next_calibration_date` date DEFAULT NULL,
  `calibration_cert_number` varchar(100) DEFAULT NULL,
  `is_active` tinyint(1) NOT NULL DEFAULT 1,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `warehouse_id` (`warehouse_id`),
  KEY `printer_id` (`printer_id`),
  CONSTRAINT `weighing_stations_ibfk_1` FOREIGN KEY (`warehouse_id`) REFERENCES `warehouses` (`id`),
  CONSTRAINT `weighing_stations_ibfk_2` FOREIGN KEY (`printer_id`) REFERENCES `printers` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='Svėrimo stotys - svarstyklės ir spausdintuvai';
");
```

### 2. print_jobs Table (Group 3)
**Insertion point**: After weighing_stations
```csharp
migrationBuilder.Sql(@"
    CREATE TABLE IF NOT EXISTS `print_jobs` (
  `id` int NOT NULL AUTO_INCREMENT,
  `printer_id` int NOT NULL,
  `station_id` int DEFAULT NULL,
  `container_id` int NOT NULL,
  `job_type` enum('RECEIPT_LABEL','QUARANTINE_LABEL','REPRINT') NOT NULL DEFAULT 'RECEIPT_LABEL',
  `zpl_content` longtext NOT NULL,
  `status` enum('PENDING','PROCESSING','DONE','FAILED','CANCELLED') NOT NULL DEFAULT 'PENDING',
  `retry_count` int NOT NULL DEFAULT 0,
  `max_retries` int NOT NULL DEFAULT 3,
  `last_error` text DEFAULT NULL,
  `created_by_user_id` int DEFAULT NULL,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `processed_at` datetime DEFAULT NULL,
  `done_at` datetime DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `printer_id` (`printer_id`),
  KEY `station_id` (`station_id`),
  KEY `container_id` (`container_id`),
  CONSTRAINT `print_jobs_ibfk_1` FOREIGN KEY (`printer_id`) REFERENCES `printers` (`id`),
  CONSTRAINT `print_jobs_ibfk_2` FOREIGN KEY (`station_id`) REFERENCES `weighing_stations` (`id`),
  CONSTRAINT `print_jobs_ibfk_3` FOREIGN KEY (`container_id`) REFERENCES `containers` (`id`),
  CONSTRAINT `print_jobs_ibfk_4` FOREIGN KEY (`created_by_user_id`) REFERENCES `AspNetUsers` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='Etiketų spausdinimo darbai';
");
```

### 3. container_label_events Table (Group 3 - BRC8 3.3)
**Insertion point**: After print_jobs
```csharp
migrationBuilder.Sql(@"
    CREATE TABLE IF NOT EXISTS `container_label_events` (
  `id` int NOT NULL AUTO_INCREMENT,
  `container_id` int NOT NULL,
  `event_type` enum('PRINTED','REPRINTED','QUARANTINE_PRINTED','CANCELLED','PRINT_FAILED') NOT NULL,
  `print_job_id` int DEFAULT NULL,
  `reason_code` enum('DAMAGED','LOST','MISPRINT','OTHER') DEFAULT NULL,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `operator_id` int NOT NULL,
  PRIMARY KEY (`id`),
  KEY `container_id` (`container_id`),
  KEY `print_job_id` (`print_job_id`),
  KEY `operator_id` (`operator_id`),
  CONSTRAINT `container_label_events_ibfk_1` FOREIGN KEY (`container_id`) REFERENCES `containers` (`id`),
  CONSTRAINT `container_label_events_ibfk_2` FOREIGN KEY (`print_job_id`) REFERENCES `print_jobs` (`id`),
  CONSTRAINT `container_label_events_ibfk_3` FOREIGN KEY (`operator_id`) REFERENCES `AspNetUsers` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='Etiketų istorija - INSERT ONLY (BRC8 3.3)';
");
```

### 4. container_weight_corrections Table (Group 3)
**Insertion point**: After container_label_events
```csharp
migrationBuilder.Sql(@"
    CREATE TABLE IF NOT EXISTS `container_weight_corrections` (
  `id` int NOT NULL AUTO_INCREMENT,
  `container_id` int NOT NULL,
  `old_weight_kg` decimal(10,3) NOT NULL,
  `new_weight_kg` decimal(10,3) NOT NULL,
  `reason` text NOT NULL,
  `corrected_by` int NOT NULL,
  `corrected_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `container_id` (`container_id`),
  KEY `corrected_by` (`corrected_by`),
  CONSTRAINT `container_weight_corrections_ibfk_1` FOREIGN KEY (`container_id`) REFERENCES `containers` (`id`),
  CONSTRAINT `container_weight_corrections_ibfk_2` FOREIGN KEY (`corrected_by`) REFERENCES `AspNetUsers` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='Svorio korekcijos';
");
```

### 5. label_templates Table (Group 3)
**Insertion point**: After container_weight_corrections
```csharp
migrationBuilder.Sql(@"
    CREATE TABLE IF NOT EXISTS `label_templates` (
  `id` int NOT NULL AUTO_INCREMENT,
  `name` varchar(100) NOT NULL,
  `description` text,
  `template_type` enum('ZPL','EPL','PLAIN_TEXT') NOT NULL DEFAULT 'ZPL',
  `content` longtext NOT NULL,
  `default_printer_id` int DEFAULT NULL,
  `width_mm` decimal(5,1) NOT NULL DEFAULT 108.0,
  `height_mm` decimal(5,1) NOT NULL DEFAULT 75.0,
  `is_active` tinyint(1) NOT NULL DEFAULT 1,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `default_printer_id` (`default_printer_id`),
  CONSTRAINT `label_templates_ibfk_1` FOREIGN KEY (`default_printer_id`) REFERENCES `printers` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='Etiketų šablonai';
");
```

### 6. supplier_approvals Table (Group 4)
**Insertion point**: After Group 3 comment
```csharp
migrationBuilder.Sql(@"
    CREATE TABLE IF NOT EXISTS `supplier_approvals` (
  `id` int NOT NULL AUTO_INCREMENT,
  `supplier_id` int NOT NULL,
  `approved_by` int NOT NULL,
  `approved_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `valid_until` date NOT NULL,
  `document_path` varchar(500) DEFAULT NULL,
  `notes` text,
  PRIMARY KEY (`id`),
  KEY `supplier_id` (`supplier_id`),
  KEY `approved_by` (`approved_by`),
  CONSTRAINT `supplier_approvals_ibfk_1` FOREIGN KEY (`supplier_id`) REFERENCES `suppliers` (`id`),
  CONSTRAINT `supplier_approvals_ibfk_2` FOREIGN KEY (`approved_by`) REFERENCES `AspNetUsers` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='Tiekėjų patvirtinimai';
");
```

### 7. non_conformances Table (Group 4)
**Insertion point**: After supplier_approvals
```csharp
migrationBuilder.Sql(@"
    CREATE TABLE IF NOT EXISTS `non_conformances` (
  `id` int NOT NULL AUTO_INCREMENT,
  `delivery_id` int NOT NULL,
  `container_id` int DEFAULT NULL,
  `description` text NOT NULL,
  `nc_type` enum('QUALITY','WEIGHT','DOCUMENTATION','OTHER') NOT NULL,
  `discovered_by` int NOT NULL,
  `discovered_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `status` enum('OPEN','INVESTIGATING','RESOLVED','CLOSED') NOT NULL DEFAULT 'OPEN',
  `corrective_action` text,
  `closed_by` int DEFAULT NULL,
  `closed_at` datetime DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `delivery_id` (`delivery_id`),
  KEY `container_id` (`container_id`),
  KEY `discovered_by` (`discovered_by`),
  KEY `closed_by` (`closed_by`),
  CONSTRAINT `non_conformances_ibfk_1` FOREIGN KEY (`delivery_id`) REFERENCES `deliveries` (`id`),
  CONSTRAINT `non_conformances_ibfk_2` FOREIGN KEY (`container_id`) REFERENCES `containers` (`id`),
  CONSTRAINT `non_conformances_ibfk_3` FOREIGN KEY (`discovered_by`) REFERENCES `AspNetUsers` (`Id`),
  CONSTRAINT `non_conformances_ibfk_4` FOREIGN KEY (`closed_by`) REFERENCES `AspNetUsers` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='Neprikaitos';
");
```

### 8. document_files Table (Group 5)
**Insertion point**: After Group 4 comment
```csharp
migrationBuilder.Sql(@"
    CREATE TABLE IF NOT EXISTS `document_files` (
  `id` int NOT NULL AUTO_INCREMENT,
  `document_type` enum('CERTIFICATE','INVOICE','DELIVERY_NOTE','OTHER') NOT NULL,
  `document_ref` varchar(100) NOT NULL,
  `file_path` varchar(500) NOT NULL,
  `file_size` int NOT NULL,
  `content_type` varchar(100) NOT NULL,
  `uploaded_by` int NOT NULL,
  `uploaded_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `uploaded_by` (`uploaded_by`),
  CONSTRAINT `document_files_ibfk_1` FOREIGN KEY (`uploaded_by`) REFERENCES `AspNetUsers` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='Dokumentų saugojimas';
");
```

### 9. ALTER Statements
**Insertion point**: After all CREATE TABLE statements

#### containers
```csharp
migrationBuilder.Sql(@"
    ALTER TABLE `containers` 
    ADD COLUMN `current_weight_kg` decimal(10,3) DEFAULT NULL COMMENT 'BRC8 3.7: Svoris',
    ADD COLUMN `weight_verified_at` datetime DEFAULT NULL COMMENT 'BRC8 3.7: Svorio patvirtinimo laikas',
    ADD COLUMN `weight_verified_by` int DEFAULT NULL COMMENT 'BRC8 3.7: Svorio patvirtinimo operatorius',
    ADD COLUMN `label_print_count` int NOT NULL DEFAULT 0 COMMENT 'BRC8 3.3: Etiketų spausdinimo skaičius',
    ADD COLUMN `quarantine_reason` text DEFAULT NULL COMMENT 'BRC8 6.3: Karantino priežastis',
    ADD COLUMN `quarantine_expires` datetime DEFAULT NULL COMMENT 'BRC8 6.3: Karantino pabaiga',
    ADD COLUMN `nc_flagged` tinyint(1) NOT NULL DEFAULT 0 COMMENT 'BRC8 6.3: Neprikaitos žymė',
    ADD COLUMN `nc_resolved` tinyint(1) NOT NULL DEFAULT 0 COMMENT 'BRC8 6.3: Neprikaitos išspręsta',
    ADD KEY `idx_weight_verified_by` (`weight_verified_by`),
    ADD CONSTRAINT `containers_ibfk_4` FOREIGN KEY (`weight_verified_by`) REFERENCES `AspNetUsers` (`Id`);
");
```

#### deliveries
```csharp
migrationBuilder.Sql(@"
    ALTER TABLE `deliveries` 
    ADD COLUMN `quarantine_reason` text DEFAULT NULL COMMENT 'BRC8 6.3: Karantino priežastis',
    ADD COLUMN `quarantine_expires` datetime DEFAULT NULL COMMENT 'BRC8 6.3: Karantino pabaiga';
");
```

#### delivery_lines
```csharp
migrationBuilder.Sql(@"
    ALTER TABLE `delivery_lines` 
    ADD COLUMN `quarantine_reason` text DEFAULT NULL COMMENT 'BRC8 6.3: Karantino priežastis',
    ADD COLUMN `quarantine_expires` datetime DEFAULT NULL COMMENT 'BRC8 6.3: Karantino pabaiga';
");
```

#### business_partners
```csharp
migrationBuilder.Sql(@"
    ALTER TABLE `business_partners` 
    ADD COLUMN `approval_status` enum('PENDING','APPROVED','REJECTED','EXPIRED') NOT NULL DEFAULT 'PENDING' COMMENT 'Tiekėjo patvirtinimo statusas',
    ADD COLUMN `approval_expiry` date DEFAULT NULL COMMENT 'Tiekėjo patvirtinimo galiojimo pabaiga',
    ADD COLUMN `approved_by` int DEFAULT NULL COMMENT 'Tiekėjo patvirtinimo operatorius',
    ADD KEY `approved_by` (`approved_by`),
    ADD CONSTRAINT `business_partners_ibfk_3` FOREIGN KEY (`approved_by`) REFERENCES `AspNetUsers` (`Id`);
");
```

## BRC8 Compliance Implementation
Add to `NordicBeesErpContext.cs`:
```csharp
public override int SaveChanges(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
{
    ValidateContainerLabelEvents();
    return base.SaveChanges(acceptAllChangesOnSuccess, cancellationToken);
}

public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
{
    ValidateContainerLabelEvents();
    return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
}

private void ValidateContainerLabelEvents()
{
    var modifiedEvents = ChangeTracker.Entries<ContainerLabelEvent>()
        .Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted);
    
    if (modifiedEvents.Any())
    {
        throw new InvalidOperationException(
            "ContainerLabelEvent is INSERT ONLY per BRC8 3.3 compliance. " +
            "No updates or deletes allowed after creation.");
    }
}
```

## Validation & Deployment
1. Build verification:
   ```bash
dotnet build --no-restore
```

2. Version bump (patch increment):
   ```bash
sed -i '' 's/<Version>\([0-9]\+\.[0-9]\+\)\.[0-9]\+<\/Version>/<Version>\1.$((\2+1))<\/Version>/' NordicBeesERP.csproj
```

3. Commit changes:
   ```bash
git add -A
git commit -m "P0a: Migrations/20260602150000_InitialCreate.cs — added labeling tables and ALTERs"
```

## Critical Checks
1. All new tables in correct dependency groups
2. `container_label_events` has INSERT ONLY validation
3. BRC8 comments present on critical fields
4. All foreign keys reference existing tables
5. Build passes with 0 errors
6. Migration applies cleanly on staging DB

## Final State
- 9 new tables added
- 4 existing tables altered
- BRC8 compliance ensured
- Build verified
- Version bumped
- Changes committed with proper message

Implementation is ready for deployment to staging environment.