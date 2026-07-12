using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET FOREIGN_KEY_CHECKS=0;");

            // ── Group 1: no FK dependencies ──────────────────────────────────

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `AspNetRoleClaims` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `RoleId` varchar(255) NOT NULL,
  `ClaimType` longtext,
  `ClaimValue` longtext,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `AspNetRoles` (
  `Id` varchar(255) NOT NULL,
  `Description` longtext,
  `AllowedModules` longtext,
  `Name` varchar(256) DEFAULT NULL,
  `NormalizedName` varchar(256) DEFAULT NULL,
  `ConcurrencyStamp` longtext,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `AspNetUserClaims` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserId` varchar(255) NOT NULL,
  `ClaimType` longtext,
  `ClaimValue` longtext,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `AspNetUserLogins` (
  `LoginProvider` varchar(255) NOT NULL,
  `ProviderKey` varchar(255) NOT NULL,
  `ProviderDisplayName` longtext,
  `UserId` varchar(255) NOT NULL,
  PRIMARY KEY (`LoginProvider`,`ProviderKey`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `AspNetUserRoles` (
  `UserId` varchar(255) NOT NULL,
  `RoleId` varchar(255) NOT NULL,
  PRIMARY KEY (`UserId`,`RoleId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `AspNetUserTokens` (
  `UserId` varchar(255) NOT NULL,
  `LoginProvider` varchar(255) NOT NULL,
  `Name` varchar(255) NOT NULL,
  `Value` longtext,
  PRIMARY KEY (`UserId`,`LoginProvider`,`Name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `AspNetUsers` (
  `Id` varchar(255) NOT NULL,
  `FullName` longtext NOT NULL,
  `IsActive` tinyint(1) NOT NULL DEFAULT '1',
  `CreatedAt` datetime(6) NOT NULL,
  `UserName` varchar(256) DEFAULT NULL,
  `NormalizedUserName` varchar(256) DEFAULT NULL,
  `Email` varchar(256) DEFAULT NULL,
  `NormalizedEmail` varchar(256) DEFAULT NULL,
  `EmailConfirmed` tinyint(1) NOT NULL,
  `PasswordHash` longtext,
  `SecurityStamp` longtext,
  `ConcurrencyStamp` longtext,
  `PhoneNumber` longtext,
  `PhoneNumberConfirmed` tinyint(1) NOT NULL,
  `TwoFactorEnabled` tinyint(1) NOT NULL,
  `LockoutEnd` datetime(6) DEFAULT NULL,
  `LockoutEnabled` tinyint(1) NOT NULL,
  `AccessFailedCount` int NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `app_settings` (
  `id` int NOT NULL AUTO_INCREMENT,
  `setting_key` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `setting_value` text COLLATE utf8mb4_unicode_ci,
  PRIMARY KEY (`id`),
  UNIQUE KEY `setting_key` (`setting_key`)
) ENGINE=InnoDB AUTO_INCREMENT=17 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `bank_imports` (
  `id` int NOT NULL AUTO_INCREMENT,
  `import_date` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `file_name` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `bank_type` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'other',
  `total_rows` int NOT NULL DEFAULT '0',
  `matched_rows` int NOT NULL DEFAULT '0',
  `unmatched_rows` int NOT NULL DEFAULT '0',
  `processed_rows` int NOT NULL DEFAULT '0',
  `total_amount` decimal(12,2) NOT NULL DEFAULT '0.00',
  `status` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'processing',
  `imported_by` int DEFAULT NULL,
  `created_by` int DEFAULT NULL,
  `file_hash` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `error_message` text COLLATE utf8mb4_unicode_ci,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` datetime DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=28 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `business_partners` (
  `id` int NOT NULL AUTO_INCREMENT,
  `partner_type` enum('customer','supplier','both','expense_supplier') COLLATE utf8mb4_unicode_ci DEFAULT 'supplier',
  `name` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `company_code` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `vat_code` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `address` text COLLATE utf8mb4_unicode_ci,
  `city` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `postal_code` varchar(20) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `country` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT 'Lithuania',
  `country_code` varchar(10) COLLATE utf8mb4_unicode_ci DEFAULT 'LT',
  `phone` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `contact_phone` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `email` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `invoice_email` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `bank_account` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `payment_term_days` int DEFAULT '7',
  `default_language` varchar(5) COLLATE utf8mb4_unicode_ci DEFAULT 'LT',
  `default_vat_rate` decimal(5,2) DEFAULT '21.00',
  `supplier_first_name` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `supplier_last_name` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `national_id_number` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `supplier_type` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `notes` text COLLATE utf8mb4_unicode_ci,
  `is_active` tinyint(1) DEFAULT '1',
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `vies_verified` tinyint(1) DEFAULT '0',
  `vies_verified_at` datetime DEFAULT NULL,
  `vies_name` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `default_expense_category_id` int DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `idx_partner_type` (`partner_type`),
  KEY `idx_name` (`name`),
  KEY `idx_vat_code` (`vat_code`),
  KEY `idx_country` (`country_code`)
) ENGINE=InnoDB AUTO_INCREMENT=400 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Klientai ir tiekėjai - unifikuota lentelė';
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `companies` (
  `id` int NOT NULL AUTO_INCREMENT,
  `name` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `company_code` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `vat_code` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `address` text COLLATE utf8mb4_unicode_ci NOT NULL,
  `city` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `postal_code` varchar(20) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `country` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT 'Lithuania',
  `country_code` varchar(10) COLLATE utf8mb4_unicode_ci DEFAULT 'LT',
  `bank_account` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `swift` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL,
  `bank_name` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `phone` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `email` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `website` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `is_active` tinyint(1) DEFAULT '1',
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Nordic Bees įmonės informacija';
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `company_settings` (
  `id` int NOT NULL AUTO_INCREMENT,
  `company_name` varchar(255) NOT NULL DEFAULT 'MB Lakštena',
  `company_code` varchar(50) DEFAULT '302905315',
  `vat_code` varchar(50) DEFAULT 'LT100013406816',
  `address` varchar(500) DEFAULT 'P. Širvio g. 3, Juodupė, LT-42457',
  `bank_name` varchar(255) DEFAULT 'AB Artea Bankas',
  `bank_iban` varchar(50) DEFAULT 'LT217189900060467854',
  `bank_swift` varchar(20) DEFAULT 'CBSBLT26',
  `bank_account` varchar(20) DEFAULT NULL,
  `email` varchar(255) DEFAULT NULL,
  `phone` varchar(50) DEFAULT NULL,
  `logo_path` varchar(500) DEFAULT NULL,
  `default_vat_rate` decimal(5,2) NOT NULL DEFAULT 21.00,
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `currencies` (
  `id` int NOT NULL AUTO_INCREMENT,
  `code` varchar(3) NOT NULL,
  `name` varchar(50) NOT NULL,
  `symbol` varchar(5) DEFAULT NULL,
  `is_active` tinyint(1) DEFAULT '1',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `erp_users` (
  `id` int NOT NULL AUTO_INCREMENT,
  `email` varchar(256) COLLATE utf8mb4_unicode_ci NOT NULL,
  `password_hash` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL,
  `full_name` varchar(256) COLLATE utf8mb4_unicode_ci NOT NULL,
  `role` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'User',
  `is_active` tinyint(1) NOT NULL DEFAULT '1',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `email` (`email`)
) ENGINE=InnoDB AUTO_INCREMENT=4 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='ERP Users for cookie-based authentication';
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `expense_cost_centers` (
  `id` int NOT NULL AUTO_INCREMENT,
  `name` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `code` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `is_active` tinyint(1) DEFAULT '1',
  PRIMARY KEY (`id`),
  UNIQUE KEY `code` (`code`)
) ENGINE=InnoDB AUTO_INCREMENT=14 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `expense_invoice_audit` (
  `id` int NOT NULL AUTO_INCREMENT,
  `invoice_id` int NOT NULL,
  `invoice_number` varchar(100) DEFAULT NULL,
  `action` varchar(50) NOT NULL,
  `action_details` text,
  `old_status` varchar(20) DEFAULT NULL,
  `new_status` varchar(20) DEFAULT NULL,
  `performed_by` varchar(100) DEFAULT NULL,
  `performed_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `idx_invoice_id` (`invoice_id`)
) ENGINE=InnoDB AUTO_INCREMENT=205 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `expense_invoices` (
  `id` int NOT NULL AUTO_INCREMENT,
  `supplier_id` int DEFAULT NULL,
  `invoice_type` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'STANDARD',
  `category_id` int DEFAULT NULL,
  `pending_supplier_name` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `pending_supplier_vat` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `pending_supplier_address` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `invoice_number` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `invoice_date` date NOT NULL,
  `due_date` date NOT NULL,
  `amount_excl_vat` decimal(12,2) NOT NULL DEFAULT '0.00',
  `vat_rate` decimal(5,2) DEFAULT '21.00',
  `vat_amount` decimal(12,2) DEFAULT '0.00',
  `amount_incl_vat` decimal(12,2) DEFAULT '0.00',
  `paid_amount` decimal(12,2) DEFAULT '0.00',
  `status` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PENDING',
  `ocr_status` enum('PENDING','PROCESSING','COMPLETED','FAILED','MANUAL') COLLATE utf8mb4_unicode_ci DEFAULT 'PENDING',
  `ocr_confidence` decimal(5,2) DEFAULT NULL,
  `ocr_flags` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ocr_raw_json` json DEFAULT NULL,
  `notes` text COLLATE utf8mb4_unicode_ci,
  `approved_by` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `approved_at` datetime DEFAULT NULL,
  `rejected_reason` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `source` enum('MANUAL','EMAIL','N8N') COLLATE utf8mb4_unicode_ci DEFAULT 'MANUAL',
  `original_filename` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `currency` varchar(3) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'EUR',
  `ocr_pipeline` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `original_file_path` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `supplier_vat_verified` tinyint(1) DEFAULT '0',
  `supplier_vat_verified_name` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `pending_supplier_company_code` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `pending_supplier_bank_account` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `pending_supplier_city` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `pending_supplier_postal_code` varchar(20) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `pending_supplier_country_code` varchar(10) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `idx_supplier_id` (`supplier_id`),
  KEY `idx_invoice_number` (`invoice_number`),
  KEY `idx_status` (`status`),
  KEY `idx_due_date` (`due_date`),
  KEY `idx_ocr_status` (`ocr_status`),
  KEY `idx_expense_invoices_type` (`invoice_type`)
) ENGINE=InnoDB AUTO_INCREMENT=129 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `expense_ocr_queue` (
  `id` int NOT NULL AUTO_INCREMENT,
  `invoice_id` int DEFAULT NULL,
  `file_content` longtext COLLATE utf8mb4_unicode_ci,
  `file_name` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL,
  `attempts` int DEFAULT '0',
  `max_attempts` int DEFAULT '3',
  `status` enum('WAITING','PROCESSING','COMPLETED','FAILED') COLLATE utf8mb4_unicode_ci DEFAULT 'WAITING',
  `error_message` text COLLATE utf8mb4_unicode_ci,
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  `processed_at` timestamp NULL DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `idx_status` (`status`),
  KEY `idx_created_at` (`created_at`)
) ENGINE=InnoDB AUTO_INCREMENT=6 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `honey_types` (
  `id` int NOT NULL AUTO_INCREMENT,
  `code` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL,
  `name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `name_en` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `description` text COLLATE utf8mb4_unicode_ci,
  `is_active` tinyint(1) DEFAULT '1',
  `sort_order` int DEFAULT '0',
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `color` varchar(7) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `code` (`code`),
  KEY `idx_code` (`code`)
) ENGINE=InnoDB AUTO_INCREMENT=16 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Medaus rūšys (liepa, rapsas, ir t.t.)';
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `payment_audit_log` (
  `id` int NOT NULL AUTO_INCREMENT,
  `payment_id` int DEFAULT NULL,
  `invoice_id` int DEFAULT NULL,
  `action` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `old_amount` decimal(10,2) DEFAULT NULL,
  `new_amount` decimal(10,2) DEFAULT NULL,
  `changed_by` int DEFAULT NULL,
  `changed_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `notes` text COLLATE utf8mb4_unicode_ci,
  PRIMARY KEY (`id`),
  KEY `idx_payment_id` (`payment_id`),
  KEY `idx_invoice_id` (`invoice_id`)
) ENGINE=InnoDB AUTO_INCREMENT=95 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `raw_material_types` (
  `id` int NOT NULL AUTO_INCREMENT,
  `name` varchar(100) NOT NULL,
  `code` varchar(5) DEFAULT NULL,
  `is_honey` tinyint(1) NOT NULL DEFAULT '0',
  `is_active` tinyint(1) NOT NULL DEFAULT '1',
  `sort_order` int NOT NULL DEFAULT '0',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=7 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `units_of_measure` (
  `id` int NOT NULL AUTO_INCREMENT,
  `code` varchar(10) COLLATE utf8mb4_unicode_ci NOT NULL,
  `name` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `name_en` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `unit_type` enum('weight','volume','piece','length','area') COLLATE utf8mb4_unicode_ci NOT NULL,
  `is_active` tinyint(1) DEFAULT '1',
  PRIMARY KEY (`id`),
  UNIQUE KEY `code` (`code`),
  KEY `idx_code` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Matavimo vienetai';
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `warehouse_types` (
  `id` int NOT NULL AUTO_INCREMENT,
  `code` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL,
  `name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` text COLLATE utf8mb4_unicode_ci,
  `is_active` tinyint(1) DEFAULT '1',
  PRIMARY KEY (`id`),
  UNIQUE KEY `code` (`code`)
) ENGINE=InnoDB AUTO_INCREMENT=5 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Sandėlių tipai';
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `printers` (
  `id` int NOT NULL AUTO_INCREMENT,
  `name` varchar(100) NOT NULL,
  `location` varchar(100) NOT NULL,
  `endpoint_url` varchar(200) NOT NULL,
  `connection_type` enum('HTTP','STUB') NOT NULL DEFAULT 'STUB',
  `label_width_mm` decimal(5,1) NOT NULL DEFAULT 108.0,
  `label_height_mm` decimal(5,1) NOT NULL DEFAULT 75.0,
  `darkness` int NOT NULL DEFAULT 25,
  `dpi` int NOT NULL DEFAULT 200,
  `last_test_print_at` datetime DEFAULT NULL,
  `last_test_result` varchar(50) DEFAULT NULL,
  `is_active` tinyint(1) NOT NULL DEFAULT 1,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            ");

            // ── Group 2: depends on Group 1 ──────────────────────────────────

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `bank_import_rows` (
  `id` int NOT NULL AUTO_INCREMENT,
  `import_id` int NOT NULL,
  `row_date` date NOT NULL,
  `payer_name` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `payer_account` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `amount` decimal(10,2) NOT NULL,
  `currency` varchar(3) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'EUR',
  `reference` varchar(200) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `description` text COLLATE utf8mb4_unicode_ci,
  `match_status` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'unmatched',
  `matched_invoice_id` int DEFAULT NULL,
  `payment_id` int DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `idx_import_id` (`import_id`),
  CONSTRAINT `fk_bir_import` FOREIGN KEY (`import_id`) REFERENCES `bank_imports` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=376 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `expense_categories` (
  `id` int NOT NULL AUTO_INCREMENT,
  `parent_id` int DEFAULT NULL,
  `name` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `code` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `is_active` tinyint(1) DEFAULT '1',
  `sort_order` int DEFAULT '0',
  PRIMARY KEY (`id`),
  UNIQUE KEY `code` (`code`),
  KEY `parent_id` (`parent_id`),
  CONSTRAINT `expense_categories_ibfk_1` FOREIGN KEY (`parent_id`) REFERENCES `expense_categories` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB AUTO_INCREMENT=41 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `expense_invoice_lines` (
  `id` int NOT NULL AUTO_INCREMENT,
  `invoice_id` int NOT NULL,
  `category_id` int DEFAULT NULL,
  `description` text COLLATE utf8mb4_unicode_ci NOT NULL,
  `amount_excl_vat` decimal(12,2) NOT NULL DEFAULT '0.00',
  `vat_rate` decimal(5,2) DEFAULT '21.00',
  `amount_incl_vat` decimal(12,2) DEFAULT '0.00',
  `sort_order` int DEFAULT '0',
  `quantity` decimal(10,3) DEFAULT NULL,
  `unit_price` decimal(12,2) DEFAULT NULL,
  `unit_of_measure` varchar(20) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `idx_invoice_id` (`invoice_id`),
  CONSTRAINT `expense_invoice_lines_ibfk_1` FOREIGN KEY (`invoice_id`) REFERENCES `expense_invoices` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=325 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `expense_payments` (
  `id` int NOT NULL AUTO_INCREMENT,
  `invoice_id` int NOT NULL,
  `payment_date` date NOT NULL,
  `amount` decimal(12,2) NOT NULL,
  `payment_method` enum('BANK','CASH','OTHER') COLLATE utf8mb4_unicode_ci DEFAULT 'BANK',
  `reference` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `notes` text COLLATE utf8mb4_unicode_ci,
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `idx_invoice_id` (`invoice_id`),
  KEY `idx_payment_date` (`payment_date`),
  CONSTRAINT `expense_payments_ibfk_1` FOREIGN KEY (`invoice_id`) REFERENCES `expense_invoices` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=43 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `email_invoice_imports` (
  `id` int NOT NULL AUTO_INCREMENT,
  `message_id` varchar(500) NOT NULL,
  `subject` varchar(500) DEFAULT NULL,
  `sender` varchar(255) DEFAULT NULL,
  `received_at` timestamp NULL DEFAULT NULL,
  `attachment_name` varchar(255) DEFAULT NULL,
  `status` enum('processing','imported','failed','skipped') NOT NULL DEFAULT 'processing',
  `expense_invoice_id` int DEFAULT NULL,
  `error_message` text,
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_message_id` (`message_id`),
  KEY `fk_email_import_invoice` (`expense_invoice_id`),
  CONSTRAINT `fk_email_import_invoice` FOREIGN KEY (`expense_invoice_id`) REFERENCES `expense_invoices` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `invoices` (
  `id` int NOT NULL AUTO_INCREMENT,
  `invoice_number` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `invoice_date` date NOT NULL,
  `customer_id` int NOT NULL,
  `currency_id` int DEFAULT NULL,
  `payment_due_date` date DEFAULT NULL,
  `payment_term_days` int DEFAULT '7',
  `language` varchar(5) COLLATE utf8mb4_unicode_ci DEFAULT 'LT',
  `invoice_type` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT 'PVM SĄSKAITA FAKTŪRA',
  `reverse_charge` tinyint(1) DEFAULT '0',
  `subtotal_excl_vat` decimal(10,2) DEFAULT '0.00',
  `total_vat` decimal(10,2) DEFAULT '0.00',
  `total_incl_vat` decimal(10,2) DEFAULT '0.00',
  `pdf_path` text COLLATE utf8mb4_unicode_ci,
  `issued_by` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `received_by` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `status` enum('draft','confirmed','paid','disputed') COLLATE utf8mb4_unicode_ci DEFAULT 'draft',
  `notes` text COLLATE utf8mb4_unicode_ci,
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `due_date` date DEFAULT NULL,
  `delivery_id` int DEFAULT NULL,
  `paid_amount` decimal(10,2) NOT NULL DEFAULT '0.00',
  `payment_status` enum('unpaid','partial','paid','overdue') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'unpaid',
  `last_payment_date` date DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `invoice_number` (`invoice_number`),
  KEY `idx_invoice_number` (`invoice_number`),
  KEY `idx_invoice_date` (`invoice_date`),
  KEY `idx_customer` (`customer_id`),
  KEY `idx_status` (`status`),
  CONSTRAINT `invoices_ibfk_1` FOREIGN KEY (`customer_id`) REFERENCES `business_partners` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB AUTO_INCREMENT=664 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Pardavimo sąskaitos faktūros';
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `orders` (
  `id` int NOT NULL AUTO_INCREMENT,
  `order_number` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `order_date` date NOT NULL,
  `customer_id` int NOT NULL,
  `delivery_date` date DEFAULT NULL,
  `status` enum('draft','confirmed','in_production','shipped','delivered','cancelled') COLLATE utf8mb4_unicode_ci DEFAULT 'draft',
  `notes` text COLLATE utf8mb4_unicode_ci,
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `order_number` (`order_number`),
  KEY `idx_order_number` (`order_number`),
  KEY `idx_customer` (`customer_id`),
  KEY `idx_status` (`status`),
  CONSTRAINT `orders_ibfk_1` FOREIGN KEY (`customer_id`) REFERENCES `business_partners` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Užsakymai - future integration';
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `product_categories` (
  `id` int NOT NULL AUTO_INCREMENT,
  `code` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL,
  `name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `parent_id` int DEFAULT NULL,
  `description` text COLLATE utf8mb4_unicode_ci,
  `is_active` tinyint(1) DEFAULT '1',
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `code` (`code`),
  KEY `parent_id` (`parent_id`),
  KEY `idx_code` (`code`),
  CONSTRAINT `product_categories_ibfk_1` FOREIGN KEY (`parent_id`) REFERENCES `product_categories` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Produktų kategorijos hierarchija';
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `warehouses` (
  `id` int NOT NULL AUTO_INCREMENT,
  `code` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL,
  `name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `warehouse_type_id` int DEFAULT NULL,
  `address` text COLLATE utf8mb4_unicode_ci,
  `city` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `country` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT 'Lithuania',
  `description` text COLLATE utf8mb4_unicode_ci,
  `is_active` tinyint(1) DEFAULT '1',
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `Email` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci DEFAULT NULL,
  `warehouse_type` enum('MAIN','PRODUCTION','SALES') COLLATE utf8mb4_unicode_ci DEFAULT 'MAIN',
  PRIMARY KEY (`id`),
  UNIQUE KEY `code` (`code`),
  KEY `warehouse_type_id` (`warehouse_type_id`),
  KEY `idx_code` (`code`),
  CONSTRAINT `warehouses_ibfk_1` FOREIGN KEY (`warehouse_type_id`) REFERENCES `warehouse_types` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB AUTO_INCREMENT=3 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Sandėliai ir jų lokacijos';
            ");

            // ── Group 3: depends on Groups 1-2 ───────────────────────────────

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `expense_budgets` (
  `id` int NOT NULL AUTO_INCREMENT,
  `category_id` int NOT NULL,
  `year` int NOT NULL,
  `month` int NOT NULL,
  `planned_amount` decimal(12,2) NOT NULL DEFAULT '0.00',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_category_year_month` (`category_id`,`year`,`month`),
  KEY `idx_year_month` (`year`,`month`),
  CONSTRAINT `expense_budgets_ibfk_1` FOREIGN KEY (`category_id`) REFERENCES `expense_categories` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `products` (
  `id` int NOT NULL AUTO_INCREMENT,
  `code` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `name` text COLLATE utf8mb4_unicode_ci NOT NULL,
  `ean_code` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `product_type` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `category_id` int DEFAULT NULL,
  `unit_id` int DEFAULT NULL,
  `unit` varchar(20) COLLATE utf8mb4_unicode_ci DEFAULT 'kg',
  `cost_price` decimal(10,2) DEFAULT '0.00',
  `sale_price` decimal(10,2) DEFAULT '0.00',
  `purchase_price` decimal(10,2) DEFAULT '0.00',
  `warehouse_managed` tinyint(1) DEFAULT '0',
  `track_lots` tinyint(1) DEFAULT '0',
  `min_stock_level` decimal(10,2) DEFAULT '0.00',
  `description` text COLLATE utf8mb4_unicode_ci,
  `notes` text COLLATE utf8mb4_unicode_ci,
  `is_active` tinyint(1) DEFAULT '1',
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `code` (`code`),
  KEY `unit_id` (`unit_id`),
  KEY `idx_code` (`code`),
  KEY `idx_product_type` (`product_type`),
  KEY `idx_category` (`category_id`),
  KEY `idx_warehouse_managed` (`warehouse_managed`),
  CONSTRAINT `products_ibfk_1` FOREIGN KEY (`category_id`) REFERENCES `product_categories` (`id`) ON DELETE SET NULL,
  CONSTRAINT `products_ibfk_2` FOREIGN KEY (`unit_id`) REFERENCES `units_of_measure` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB AUTO_INCREMENT=35 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Produktų katalogas - žaliavos, pakuotės, gatavi produktai';
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `credit_notes` (
  `id` int NOT NULL AUTO_INCREMENT,
  `credit_note_number` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `credit_date` date NOT NULL,
  `original_invoice_id` int NOT NULL,
  `applied_invoice_id` int NOT NULL,
  `customer_id` int NOT NULL,
  `currency_id` int DEFAULT NULL,
  `language` varchar(5) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'LT',
  `credit_note_type` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT 'standard',
  `reverse_charge` tinyint(1) NOT NULL DEFAULT '0',
  `subtotal_excl_vat` decimal(10,2) NOT NULL DEFAULT '0.00',
  `total_vat` decimal(10,2) NOT NULL DEFAULT '0.00',
  `total_incl_vat` decimal(10,2) NOT NULL DEFAULT '0.00',
  `status` enum('draft','issued','cancelled','printed','disputed') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'draft',
  `pdf_path` text COLLATE utf8mb4_unicode_ci,
  `notes` text COLLATE utf8mb4_unicode_ci,
  `issued_by` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `created_by` int DEFAULT NULL,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `credit_note_number` (`credit_note_number`),
  KEY `idx_credit_note_number` (`credit_note_number`),
  KEY `idx_original_invoice` (`original_invoice_id`),
  KEY `idx_applied_invoice` (`applied_invoice_id`),
  KEY `idx_customer` (`customer_id`),
  CONSTRAINT `fk_cn_applied` FOREIGN KEY (`applied_invoice_id`) REFERENCES `invoices` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_cn_customer` FOREIGN KEY (`customer_id`) REFERENCES `business_partners` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_cn_original` FOREIGN KEY (`original_invoice_id`) REFERENCES `invoices` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB AUTO_INCREMENT=23 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `lots` (
  `id` int NOT NULL AUTO_INCREMENT,
  `lot_number` varchar(50) NOT NULL,
  `lot_type` enum('PRODUCTION','DIRECT_SALE') NOT NULL,
  `created_date` date NOT NULL,
  `customer_id` int DEFAULT NULL,
  `invoice_id` int DEFAULT NULL,
  `notes` text,
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `lot_number` (`lot_number`),
  KEY `customer_id` (`customer_id`),
  KEY `invoice_id` (`invoice_id`),
  CONSTRAINT `lots_ibfk_1` FOREIGN KEY (`customer_id`) REFERENCES `business_partners` (`id`),
  CONSTRAINT `lots_ibfk_2` FOREIGN KEY (`invoice_id`) REFERENCES `invoices` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `payments` (
  `id` int NOT NULL AUTO_INCREMENT,
  `payment_date` date NOT NULL,
  `invoice_id` int DEFAULT NULL,
  `customer_id` int NOT NULL,
  `amount` decimal(10,2) NOT NULL,
  `payment_method` enum('bank_transfer','cash','card','other') COLLATE utf8mb4_unicode_ci DEFAULT 'bank_transfer',
  `reference_number` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `notes` text COLLATE utf8mb4_unicode_ci,
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  `bank_import_row_id` int DEFAULT NULL,
  `source` enum('manual','bank_import') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'manual',
  `created_by` int DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `bank_import_id` int DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `idx_payment_date` (`payment_date`),
  KEY `idx_invoice` (`invoice_id`),
  KEY `idx_customer` (`customer_id`),
  CONSTRAINT `payments_ibfk_1` FOREIGN KEY (`invoice_id`) REFERENCES `invoices` (`id`) ON DELETE SET NULL,
  CONSTRAINT `payments_ibfk_2` FOREIGN KEY (`customer_id`) REFERENCES `business_partners` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB AUTO_INCREMENT=2547 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Mokėjimai - banko integracijos paruošimas';
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `deliveries` (
  `id` int NOT NULL AUTO_INCREMENT,
  `delivery_number` varchar(50) DEFAULT NULL,
  `delivery_date` date NOT NULL,
  `supplier_id` int NOT NULL,
  `warehouse_id` int NOT NULL,
  `status` enum('RECEIVED','PRICED','PARTIAL_PAID','PAID','ACCEPTED','CLOSED') DEFAULT 'RECEIVED',
  `total_net_weight` decimal(10,3) DEFAULT '0.000',
  `total_amount` decimal(10,2) DEFAULT '0.00',
  `paid_amount` decimal(10,2) DEFAULT '0.00',
  `barrels_owed` int DEFAULT '0',
  `barrels_returned` int DEFAULT '0',
  `notes` text,
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `raw_material_type_id` int DEFAULT NULL,
  `need_return_barrels` tinyint(1) NOT NULL DEFAULT '0',
  `invoice_id` int DEFAULT NULL,
  `invoice_number` varchar(50) DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `supplier_id` (`supplier_id`),
  KEY `warehouse_id` (`warehouse_id`),
  CONSTRAINT `deliveries_ibfk_1` FOREIGN KEY (`supplier_id`) REFERENCES `business_partners` (`id`),
  CONSTRAINT `deliveries_ibfk_2` FOREIGN KEY (`warehouse_id`) REFERENCES `warehouses` (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=56 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
             ");

            // ── Labeling module tables (Group 3 extension) ────────────────────

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
  CONSTRAINT `print_jobs_ibfk_4` FOREIGN KEY (`created_by_user_id`) REFERENCES `erp_users` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='Etiketų spausdinimo darbai';
            ");

            // Audit fix: created_by_user_id must be NOT NULL (BRC8 3.3 — every print job has an operator)
            migrationBuilder.AlterColumn<int>(
                name: "created_by_user_id",
                table: "print_jobs",
                type: "int",
                nullable: false,
                oldNullable: true,
                defaultValue: 0,
                schema: "nordic_bees_erp");

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
  CONSTRAINT `container_label_events_ibfk_3` FOREIGN KEY (`operator_id`) REFERENCES `erp_users` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='Etiketų istorija - INSERT ONLY (BRC8 3.3)';
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `container_weight_corrections` (
  `id` int NOT NULL AUTO_INCREMENT,
  `container_id` int NOT NULL,
  `old_gross_weight` decimal(10,3) NOT NULL,
  `new_gross_weight` decimal(10,3) NOT NULL,
  `old_tare_weight` decimal(10,3) NOT NULL,
  `new_tare_weight` decimal(10,3) NOT NULL,
  `old_net_weight` decimal(10,3) NOT NULL,
  `new_net_weight` decimal(10,3) NOT NULL,
  `reason` varchar(200) NOT NULL,
  `corrected_by` int NOT NULL,
  `corrected_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `container_id` (`container_id`),
  KEY `corrected_by` (`corrected_by`),
  CONSTRAINT `container_weight_corrections_ibfk_1` FOREIGN KEY (`container_id`) REFERENCES `containers` (`id`),
  CONSTRAINT `container_weight_corrections_ibfk_2` FOREIGN KEY (`corrected_by`) REFERENCES `erp_users` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='Svorio korekcijos (BRC8 3.7) - bruto/tara/neto atskirai';
            ");

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

            // ── Group 4: depends on Groups 1-3 ───────────────────────────────

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
  CONSTRAINT `supplier_approvals_ibfk_1` FOREIGN KEY (`supplier_id`) REFERENCES `business_partners` (`id`),
  CONSTRAINT `supplier_approvals_ibfk_2` FOREIGN KEY (`approved_by`) REFERENCES `erp_users` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='Tiekėjų patvirtinimai';
            ");

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
  CONSTRAINT `non_conformances_ibfk_3` FOREIGN KEY (`discovered_by`) REFERENCES `erp_users` (`id`),
  CONSTRAINT `non_conformances_ibfk_4` FOREIGN KEY (`closed_by`) REFERENCES `erp_users` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='Neprikaitos';
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `expense_line_allocations` (
  `id` int NOT NULL AUTO_INCREMENT,
  `invoice_line_id` int NOT NULL,
  `category_id` int NOT NULL,
  `cost_center_id` int NOT NULL,
  `allocated_amount` decimal(12,2) DEFAULT '0.00',
  `allocated_percent` decimal(5,2) DEFAULT '0.00',
  PRIMARY KEY (`id`),
  KEY `idx_invoice_line_id` (`invoice_line_id`),
  KEY `idx_category_id` (`category_id`),
  KEY `idx_cost_center_id` (`cost_center_id`),
  CONSTRAINT `expense_line_allocations_ibfk_1` FOREIGN KEY (`invoice_line_id`) REFERENCES `expense_invoice_lines` (`id`) ON DELETE CASCADE,
  CONSTRAINT `expense_line_allocations_ibfk_2` FOREIGN KEY (`category_id`) REFERENCES `expense_categories` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `expense_line_allocations_ibfk_3` FOREIGN KEY (`cost_center_id`) REFERENCES `expense_cost_centers` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `credit_note_lines` (
  `id` int NOT NULL AUTO_INCREMENT,
  `credit_note_id` int NOT NULL,
  `invoice_line_id` int DEFAULT NULL,
  `line_number` int NOT NULL,
  `product_code` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `description` text COLLATE utf8mb4_unicode_ci NOT NULL,
  `quantity` decimal(10,3) NOT NULL,
  `unit` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'vnt',
  `price_excl_vat` decimal(10,4) NOT NULL,
  `vat_rate` decimal(5,2) NOT NULL,
  `line_subtotal` decimal(10,2) NOT NULL,
  `vat_amount` decimal(10,2) NOT NULL,
  `line_total` decimal(10,2) NOT NULL,
  `lot_number` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `idx_credit_note` (`credit_note_id`),
  CONSTRAINT `fk_cnl_note` FOREIGN KEY (`credit_note_id`) REFERENCES `credit_notes` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=21 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `delivery_lines` (
  `id` int NOT NULL AUTO_INCREMENT,
  `delivery_id` int NOT NULL,
  `product_id` int DEFAULT NULL,
  `honey_type_id` int DEFAULT NULL,
  `container_type` enum('BARREL','BUCKET') NOT NULL,
  `container_count` int NOT NULL DEFAULT '1',
  `total_gross_weight` decimal(10,3) NOT NULL DEFAULT '0.000',
  `total_tare_weight` decimal(10,3) NOT NULL DEFAULT '0.000',
  `total_net_weight` decimal(10,3) NOT NULL DEFAULT '0.000',
  `unit_price` decimal(10,4) DEFAULT NULL,
  `line_total` decimal(10,2) DEFAULT NULL,
  `notes` text,
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `container_id` int DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `delivery_id` (`delivery_id`),
  KEY `product_id` (`product_id`),
  KEY `honey_type_id` (`honey_type_id`),
  CONSTRAINT `delivery_lines_ibfk_1` FOREIGN KEY (`delivery_id`) REFERENCES `deliveries` (`id`) ON DELETE CASCADE,
  CONSTRAINT `delivery_lines_ibfk_3` FOREIGN KEY (`honey_type_id`) REFERENCES `honey_types` (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=38 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `honey_deliveries` (
  `id` int NOT NULL AUTO_INCREMENT,
  `delivery_date` datetime NOT NULL,
  `delivery_number` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `supplier_id` int NOT NULL,
  `product_id` int DEFAULT NULL,
  `honey_type_id` int DEFAULT NULL,
  `gross_weight` decimal(10,3) NOT NULL COMMENT 'Bruto svoris su tara',
  `tare_weight` decimal(10,3) NOT NULL COMMENT 'Taros svoris',
  `net_weight` decimal(10,3) NOT NULL COMMENT 'Neto svoris (medus)',
  `container_quantity` int NOT NULL COMMENT 'Statinių skaičius',
  `warehouse_id` int NOT NULL,
  `price_per_kg` decimal(10,2) DEFAULT NULL COMMENT 'Pirkimo kaina už kg',
  `total_cost` decimal(10,2) DEFAULT NULL COMMENT 'Bendra suma',
  `transport_cost` decimal(10,2) DEFAULT '0.00' COMMENT 'Transporto išlaidos',
  `is_soured` tinyint(1) DEFAULT '0' COMMENT 'Ar medus surūgęs',
  `quality_grade` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Kokybės įvertinimas',
  `beehive_location` text COLLATE utf8mb4_unicode_ci COMMENT 'Bityno vieta',
  `notes` text COLLATE utf8mb4_unicode_ci,
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `delivery_number` (`delivery_number`),
  KEY `product_id` (`product_id`),
  KEY `idx_delivery_date` (`delivery_date`),
  KEY `idx_supplier` (`supplier_id`),
  KEY `idx_warehouse` (`warehouse_id`),
  KEY `idx_honey_type` (`honey_type_id`),
  CONSTRAINT `honey_deliveries_ibfk_1` FOREIGN KEY (`supplier_id`) REFERENCES `business_partners` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `honey_deliveries_ibfk_2` FOREIGN KEY (`product_id`) REFERENCES `products` (`id`) ON DELETE SET NULL,
  CONSTRAINT `honey_deliveries_ibfk_3` FOREIGN KEY (`honey_type_id`) REFERENCES `honey_types` (`id`) ON DELETE SET NULL,
  CONSTRAINT `honey_deliveries_ibfk_4` FOREIGN KEY (`warehouse_id`) REFERENCES `warehouses` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Medaus supirkimas iš bitininkų - žaliavų gavimas';
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `invoice_lines` (
  `id` int NOT NULL AUTO_INCREMENT,
  `invoice_id` int NOT NULL,
  `line_number` int NOT NULL,
  `product_id` int DEFAULT NULL,
  `product_code` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `description` text COLLATE utf8mb4_unicode_ci NOT NULL,
  `quantity` decimal(10,3) NOT NULL,
  `unit` varchar(20) COLLATE utf8mb4_unicode_ci DEFAULT 'vnt',
  `price_excl_vat` decimal(10,4) NOT NULL,
  `vat_rate` decimal(5,2) NOT NULL,
  `line_subtotal` decimal(10,2) NOT NULL,
  `vat_amount` decimal(10,2) NOT NULL,
  `line_total` decimal(10,2) NOT NULL,
  `lot_number` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `warehouse_id` int DEFAULT NULL,
  `notes` text COLLATE utf8mb4_unicode_ci,
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `warehouse_id` (`warehouse_id`),
  KEY `idx_invoice` (`invoice_id`),
  KEY `idx_product` (`product_id`),
  KEY `idx_lot` (`lot_number`),
  CONSTRAINT `invoice_lines_ibfk_1` FOREIGN KEY (`invoice_id`) REFERENCES `invoices` (`id`) ON DELETE CASCADE,
  CONSTRAINT `invoice_lines_ibfk_2` FOREIGN KEY (`product_id`) REFERENCES `products` (`id`) ON DELETE SET NULL,
  CONSTRAINT `invoice_lines_ibfk_3` FOREIGN KEY (`warehouse_id`) REFERENCES `warehouses` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB AUTO_INCREMENT=1208 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Sąskaitų eilutės su LOT traceability';
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `order_lines` (
  `id` int NOT NULL AUTO_INCREMENT,
  `order_id` int NOT NULL,
  `line_number` int NOT NULL,
  `product_id` int NOT NULL,
  `quantity` decimal(10,3) NOT NULL,
  `price` decimal(10,4) DEFAULT NULL,
  `notes` text COLLATE utf8mb4_unicode_ci,
  PRIMARY KEY (`id`),
  KEY `idx_order` (`order_id`),
  KEY `idx_product` (`product_id`),
  CONSTRAINT `order_lines_ibfk_1` FOREIGN KEY (`order_id`) REFERENCES `orders` (`id`) ON DELETE CASCADE,
  CONSTRAINT `order_lines_ibfk_2` FOREIGN KEY (`product_id`) REFERENCES `products` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Užsakymų eilutės';
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `payment_allocations` (
  `id` int NOT NULL AUTO_INCREMENT,
  `payment_id` int NOT NULL,
  `invoice_id` int NOT NULL,
  `allocated_amount` decimal(10,2) NOT NULL,
  `allocated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_payment_invoice` (`payment_id`,`invoice_id`),
  KEY `idx_invoice_id` (`invoice_id`),
  CONSTRAINT `fk_pa_invoice` FOREIGN KEY (`invoice_id`) REFERENCES `invoices` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_pa_payment` FOREIGN KEY (`payment_id`) REFERENCES `payments` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=1009 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `production_batches` (
  `id` int NOT NULL AUTO_INCREMENT,
  `lot_number` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `batch_date` datetime NOT NULL,
  `product_id` int NOT NULL,
  `quantity_produced` decimal(10,3) NOT NULL,
  `warehouse_id` int NOT NULL,
  `status` enum('planned','in_progress','completed','cancelled') COLLATE utf8mb4_unicode_ci DEFAULT 'completed',
  `total_cost` decimal(10,2) DEFAULT NULL COMMENT 'Bendra gamybos savikaina',
  `cost_per_unit` decimal(10,4) DEFAULT NULL COMMENT 'Savikaina vnt.',
  `notes` text COLLATE utf8mb4_unicode_ci,
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `lot_number` (`lot_number`),
  KEY `warehouse_id` (`warehouse_id`),
  KEY `idx_lot_number` (`lot_number`),
  KEY `idx_product` (`product_id`),
  KEY `idx_batch_date` (`batch_date`),
  KEY `idx_status` (`status`),
  CONSTRAINT `production_batches_ibfk_1` FOREIGN KEY (`product_id`) REFERENCES `products` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `production_batches_ibfk_2` FOREIGN KEY (`warehouse_id`) REFERENCES `warehouses` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Gamybos partijos - LOT valdymas ir traceability';
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `stock_movements` (
  `id` int NOT NULL AUTO_INCREMENT,
  `warehouse_id` int DEFAULT NULL,
  `product_id` int DEFAULT NULL,
  `quantity` decimal(18,4) NOT NULL,
  `movement_type` enum('IN','OUT','TRANSFER','ADJUSTMENT') NOT NULL,
  `reference_type` varchar(50) DEFAULT NULL,
  `reference_id` int DEFAULT NULL,
  `description` text,
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  `container_id` int DEFAULT NULL,
  `created_by` int DEFAULT NULL,
  `from_warehouse_id` int DEFAULT NULL,
  `to_warehouse_id` int DEFAULT NULL,
  `lot_id` int DEFAULT NULL,
  `notes` text,
  PRIMARY KEY (`id`),
  KEY `warehouse_id` (`warehouse_id`),
  KEY `product_id` (`product_id`),
  CONSTRAINT `stock_movements_ibfk_1` FOREIGN KEY (`warehouse_id`) REFERENCES `warehouses` (`id`),
  CONSTRAINT `stock_movements_ibfk_2` FOREIGN KEY (`product_id`) REFERENCES `products` (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=192 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `supplier_payments` (
  `id` int NOT NULL AUTO_INCREMENT,
  `delivery_id` int NOT NULL,
  `supplier_id` int NOT NULL,
  `amount` decimal(10,2) NOT NULL,
  `payment_date` date NOT NULL,
  `payment_method` varchar(50) DEFAULT 'bank_transfer',
  `notes` text,
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `idx_payment_supplier` (`supplier_id`),
  KEY `idx_payment_delivery` (`delivery_id`),
  CONSTRAINT `supplier_payments_ibfk_1` FOREIGN KEY (`delivery_id`) REFERENCES `deliveries` (`id`),
  CONSTRAINT `supplier_payments_ibfk_2` FOREIGN KEY (`supplier_id`) REFERENCES `business_partners` (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=20 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `warehouse_stocks` (
  `id` int NOT NULL AUTO_INCREMENT,
  `warehouse_id` int NOT NULL,
  `product_id` int NOT NULL,
  `lot_number` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `quantity` decimal(10,3) NOT NULL DEFAULT '0.000',
  `reserved_quantity` decimal(10,3) NOT NULL DEFAULT '0.000',
  `available_quantity` decimal(10,3) GENERATED ALWAYS AS ((`quantity` - `reserved_quantity`)) STORED,
  `last_movement_date` timestamp NULL DEFAULT NULL,
  `updated_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_warehouse_product_lot` (`warehouse_id`,`product_id`,`lot_number`),
  KEY `idx_warehouse` (`warehouse_id`),
  KEY `idx_product` (`product_id`),
  KEY `idx_lot` (`lot_number`),
  CONSTRAINT `warehouse_stocks_ibfk_1` FOREIGN KEY (`warehouse_id`) REFERENCES `warehouses` (`id`) ON DELETE CASCADE,
  CONSTRAINT `warehouse_stocks_ibfk_2` FOREIGN KEY (`product_id`) REFERENCES `products` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Sandėlio likučiai pagal produktą ir LOT';
            ");

            // ── Group 5: depends on Groups 1-4 ───────────────────────────────

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
  CONSTRAINT `document_files_ibfk_1` FOREIGN KEY (`uploaded_by`) REFERENCES `erp_users` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='Dokumentų saugojimas';
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `containers` (
  `id` int NOT NULL AUTO_INCREMENT,
  `container_code` varchar(50) NOT NULL,
  `container_type` enum('BARREL','BUCKET') NOT NULL,
  `supplier_id` int NOT NULL,
  `delivery_line_id` int DEFAULT NULL,
  `warehouse_id` int NOT NULL,
  `product_id` int DEFAULT NULL,
  `honey_type_id` int DEFAULT NULL,
  `gross_weight` decimal(10,3) NOT NULL DEFAULT '0.000',
  `tare_weight` decimal(10,3) NOT NULL DEFAULT '0.000',
  `net_weight` decimal(10,3) NOT NULL DEFAULT '0.000',
  `quantity` int NOT NULL DEFAULT '1',
  `remaining_quantity` int NOT NULL DEFAULT '1',
  `status` enum('RECEIVED','IN_STOCK','RESERVED','IN_PRODUCTION','SOLD','RETURNED','WRITTEN_OFF') DEFAULT 'IN_STOCK',
  `reservation_customer_id` int DEFAULT NULL,
  `reservation_notes` text,
  `reservation_date` datetime DEFAULT NULL,
  `lot_id` int DEFAULT NULL,
  `notes` text,
  `quality_params` json DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `delivery_line_id` (`delivery_line_id`),
  KEY `product_id` (`product_id`),
  KEY `honey_type_id` (`honey_type_id`),
  KEY `reservation_customer_id` (`reservation_customer_id`),
  KEY `lot_id` (`lot_id`),
  KEY `idx_container_code` (`container_code`),
  KEY `idx_container_warehouse` (`warehouse_id`),
  KEY `idx_container_supplier` (`supplier_id`),
  KEY `idx_container_status` (`status`),
  CONSTRAINT `containers_ibfk_1` FOREIGN KEY (`supplier_id`) REFERENCES `business_partners` (`id`),
  CONSTRAINT `containers_ibfk_2` FOREIGN KEY (`delivery_line_id`) REFERENCES `delivery_lines` (`id`),
  CONSTRAINT `containers_ibfk_3` FOREIGN KEY (`warehouse_id`) REFERENCES `warehouses` (`id`),
  CONSTRAINT `containers_ibfk_5` FOREIGN KEY (`honey_type_id`) REFERENCES `honey_types` (`id`),
  CONSTRAINT `containers_ibfk_6` FOREIGN KEY (`reservation_customer_id`) REFERENCES `business_partners` (`id`),
  CONSTRAINT `containers_ibfk_7` FOREIGN KEY (`lot_id`) REFERENCES `lots` (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=130 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `production_batch_ingredients` (
  `id` int NOT NULL AUTO_INCREMENT,
  `batch_id` int NOT NULL,
  `ingredient_type` enum('honey_delivery','product','other') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'honey_delivery',
  `honey_delivery_id` int DEFAULT NULL,
  `product_id` int DEFAULT NULL,
  `quantity_used` decimal(10,3) NOT NULL,
  `unit_cost` decimal(10,4) DEFAULT NULL COMMENT 'Vieneto savikaina',
  `total_cost` decimal(10,2) DEFAULT NULL COMMENT 'Bendra ingrediento savikaina',
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `idx_batch` (`batch_id`),
  KEY `idx_honey_delivery` (`honey_delivery_id`),
  KEY `idx_product` (`product_id`),
  CONSTRAINT `production_batch_ingredients_ibfk_1` FOREIGN KEY (`batch_id`) REFERENCES `production_batches` (`id`) ON DELETE CASCADE,
  CONSTRAINT `production_batch_ingredients_ibfk_2` FOREIGN KEY (`honey_delivery_id`) REFERENCES `honey_deliveries` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `production_batch_ingredients_ibfk_3` FOREIGN KEY (`product_id`) REFERENCES `products` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Gamybos ingredientai - traceability iki žaliavų';
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `invoice_audit` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `invoice_id` int NOT NULL,
                    `invoice_number` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `action` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `action_details` text COLLATE utf8mb4_unicode_ci,
                    `old_status` varchar(30) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `new_status` varchar(30) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `performed_by` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `performed_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    KEY `idx_invoice_id` (`invoice_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `artwork_brands` (
  `id` int NOT NULL AUTO_INCREMENT,
  `name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `slug` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `is_active` tinyint(1) NOT NULL DEFAULT '1',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `name` (`name`),
  UNIQUE KEY `slug` (`slug`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `artwork_assets` (
  `id` int NOT NULL AUTO_INCREMENT,
  `brand_id` int NOT NULL,
  `name` varchar(200) COLLATE utf8mb4_unicode_ci NOT NULL,
  `asset_type` enum('label','brochure','box','sticker','other') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'label',
  `description` text COLLATE utf8mb4_unicode_ci,
  `predecessor_asset_id` int DEFAULT NULL,
  `status` enum('active','archived') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'active',
  `created_by` int NOT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_brand_asset_name` (`brand_id`,`name`),
  KEY `idx_status` (`status`),
  KEY `idx_predecessor` (`predecessor_asset_id`),
  CONSTRAINT `fk_asset_brand` FOREIGN KEY (`brand_id`) REFERENCES `artwork_brands` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_asset_predecessor` FOREIGN KEY (`predecessor_asset_id`) REFERENCES `artwork_assets` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `artwork_versions` (
  `id` int NOT NULL AUTO_INCREMENT,
  `asset_id` int NOT NULL,
  `version_number` int NOT NULL,
  `file_type` enum('print_ready','source') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'print_ready',
  `file_path` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL,
  `original_filename` varchar(300) COLLATE utf8mb4_unicode_ci NOT NULL,
  `file_size_bytes` bigint NOT NULL,
  `file_sha256` char(64) COLLATE utf8mb4_unicode_ci NOT NULL,
  `preview_path` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `thumbnail_path` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `page_count` int DEFAULT NULL,
  `change_description` text COLLATE utf8mb4_unicode_ci NOT NULL,
  `status` enum('pending','approved','rejected','superseded') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'pending',
  `uploaded_by` int NOT NULL,
  `uploaded_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `reviewed_by` int DEFAULT NULL,
  `reviewed_at` datetime DEFAULT NULL,
  `review_comment` text COLLATE utf8mb4_unicode_ci,
  `effective_from` date DEFAULT NULL,
  `effective_to` date DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_asset_version` (`asset_id`,`version_number`),
  KEY `idx_asset_status` (`asset_id`,`status`),
  KEY `idx_sha256` (`file_sha256`),
  CONSTRAINT `fk_ver_asset` FOREIGN KEY (`asset_id`) REFERENCES `artwork_assets` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `artwork_comments` (
  `id` int NOT NULL AUTO_INCREMENT,
  `version_id` int NOT NULL,
  `user_id` int NOT NULL,
  `body` text COLLATE utf8mb4_unicode_ci NOT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `idx_version_id` (`version_id`),
  CONSTRAINT `fk_comment_version` FOREIGN KEY (`version_id`) REFERENCES `artwork_versions` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `artwork_audit_log` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `entity_type` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `entity_id` int NOT NULL,
  `action` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `user_id` int NOT NULL,
  `details` json DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `idx_entity` (`entity_type`,`entity_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
        ALTER TABLE `containers`
            ADD COLUMN IF NOT EXISTS `current_weight_kg` decimal(10,3) DEFAULT NULL COMMENT 'BRC8 3.7: Svoris',
            ADD COLUMN IF NOT EXISTS `weight_verified_at` datetime DEFAULT NULL COMMENT 'BRC8 3.7: Svorio patvirtinimo laikas',
            ADD COLUMN IF NOT EXISTS `weight_verified_by` int DEFAULT NULL COMMENT 'BRC8 3.7: Svorio patvirtinimo operatorius',
            ADD COLUMN IF NOT EXISTS `label_print_count` int NOT NULL DEFAULT 0 COMMENT 'BRC8 3.3: Etiketų spausdinimo skaičius',
            ADD COLUMN IF NOT EXISTS `quarantine_reason` text DEFAULT NULL COMMENT 'BRC8 6.3: Karantino priežastis',
            ADD COLUMN IF NOT EXISTS `quarantine_expires` datetime DEFAULT NULL COMMENT 'BRC8 6.3: Karantino pabaiga',
            ADD COLUMN IF NOT EXISTS `nc_flagged` tinyint(1) NOT NULL DEFAULT 0 COMMENT 'BRC8 6.3: Neprikaitos žymė',
            ADD COLUMN IF NOT EXISTS `nc_resolved` tinyint(1) NOT NULL DEFAULT 0 COMMENT 'BRC8 6.3: Neprikaitos išspręsta',
            ADD KEY IF NOT EXISTS `idx_weight_verified_by` (`weight_verified_by`),
            ADD CONSTRAINT IF NOT EXISTS `containers_ibfk_4` FOREIGN KEY (`weight_verified_by`) REFERENCES `erp_users` (`id`);
    ");

            migrationBuilder.Sql(@"
        ALTER TABLE deliveries
            ADD COLUMN IF NOT EXISTS `quarantine_reason` text DEFAULT NULL COMMENT 'BRC8 6.3: Karantino priežastis',
            ADD COLUMN IF NOT EXISTS `quarantine_expires` datetime DEFAULT NULL COMMENT 'BRC8 6.3: Karantino pabaiga';
    ");

            migrationBuilder.Sql(@"
        ALTER TABLE delivery_lines
            ADD COLUMN IF NOT EXISTS `quarantine_reason` text DEFAULT NULL COMMENT 'BRC8 6.3: Karantino priežastis',
            ADD COLUMN IF NOT EXISTS `quarantine_expires` datetime DEFAULT NULL COMMENT 'BRC8 6.3: Karantino pabaiga';
    ");

            migrationBuilder.Sql(@"
        ALTER TABLE business_partners
            ADD COLUMN IF NOT EXISTS `approval_status` enum('PENDING','APPROVED','REJECTED','EXPIRED') NOT NULL DEFAULT 'PENDING' COMMENT 'Tiekėjo patvirtinimo statusas',
            ADD COLUMN IF NOT EXISTS `approval_date` date DEFAULT NULL COMMENT 'Tiekėjo patvirtinimo data',
            ADD COLUMN IF NOT EXISTS `approval_expires_at` date DEFAULT NULL COMMENT 'Tiekėjo patvirtinimo galiojimo pabaiga';
    ");

            migrationBuilder.Sql(@"
        ALTER TABLE deliveries
            ADD COLUMN IF NOT EXISTS inspection_result ENUM('OK','NOK','CONDITIONAL') NULL,
            ADD COLUMN IF NOT EXISTS inspection_notes TEXT NULL,
            ADD COLUMN IF NOT EXISTS inspection_by VARCHAR(200) NULL,
            ADD COLUMN IF NOT EXISTS inspection_at DATETIME NULL;
    ");

            migrationBuilder.Sql(@"
        ALTER TABLE deliveries
            ADD COLUMN IF NOT EXISTS receipt_pdf_path VARCHAR(500) NULL;
    ");

            migrationBuilder.Sql(@"
        ALTER TABLE deliveries
            ADD COLUMN IF NOT EXISTS origin_country VARCHAR(100) NULL;
    ");

            migrationBuilder.Sql(@"
        ALTER TABLE business_partners
            ADD COLUMN IF NOT EXISTS no_email TINYINT(1) NOT NULL DEFAULT 0;
    ");

            migrationBuilder.Sql(@"
        ALTER TABLE deliveries
            ADD COLUMN IF NOT EXISTS signed_by_type VARCHAR(20) NOT NULL DEFAULT 'SUPPLIER',
            ADD COLUMN IF NOT EXISTS receiver_name VARCHAR(200) NULL;
    ");

            // ── BUCKET → BUCKET conversion (BRC8 labeling module) ─────────────
            // containers/delivery_lines already exist with real data containing
            // 'BUCKET' rows. Must widen enum first, then UPDATE data, then
            // narrow enum — a direct UPDATE to 'BUCKET' before widening fails with
            // "Data truncated" since 'BUCKET' isn't yet a valid enum value.
            migrationBuilder.Sql(@"
        ALTER TABLE containers
            MODIFY container_type ENUM('BARREL','BUCKET') NOT NULL;
    ");

            migrationBuilder.Sql(@"
        ALTER TABLE delivery_lines
            MODIFY container_type ENUM('BARREL','BUCKET') NOT NULL;
    ");

            migrationBuilder.Sql(@"
        UPDATE containers SET container_type = 'BUCKET' WHERE container_type = 'BUCKET';
    ");

            migrationBuilder.Sql(@"
        UPDATE delivery_lines SET container_type = 'BUCKET' WHERE container_type = 'BUCKET';
    ");

            migrationBuilder.Sql(@"
        ALTER TABLE containers
            MODIFY container_type ENUM('BARREL','BUCKET') NOT NULL;
    ");

            migrationBuilder.Sql(@"
        ALTER TABLE delivery_lines
            MODIFY container_type ENUM('BARREL','BUCKET') NOT NULL;
    ");

            // ── Add QUARANTINE to containers.status (BRC8 6.3) ───────────────
            // Pure addition to the enum, no data conversion needed — safe on
            // tables that already exist with real data.
            migrationBuilder.Sql(@"
        ALTER TABLE containers
            MODIFY status ENUM('RECEIVED','IN_STOCK','RESERVED','IN_PRODUCTION','QUARANTINE','SOLD','RETURNED','WRITTEN_OFF') NULL DEFAULT 'IN_STOCK';
    ");

            // ── BRC8 3.9: User ID fields for traceability ─────────────────────

            migrationBuilder.Sql(@"
        ALTER TABLE deliveries
            ADD COLUMN inspection_by_user_id INT NULL,
            ADD INDEX `idx_deliveries_inspection_by_user_id` (`inspection_by_user_id`);
    ");

            migrationBuilder.Sql(@"
        ALTER TABLE deliveries
            ADD CONSTRAINT `fk_deliveries_inspection_by_user` FOREIGN KEY (`inspection_by_user_id`) REFERENCES `erp_users` (`id`);
    ");

            migrationBuilder.Sql(@"
        ALTER TABLE containers
            ADD COLUMN received_by_user_id INT NULL,
            ADD INDEX `idx_containers_received_by_user_id` (`received_by_user_id`);
    ");

            migrationBuilder.Sql(@"
        ALTER TABLE containers
            ADD CONSTRAINT `fk_containers_received_by_user` FOREIGN KEY (`received_by_user_id`) REFERENCES `erp_users` (`id`);
    ");

            // BRC8 3.9: delivery-level received_by_user_id (who accepted the delivery)
            migrationBuilder.Sql(@"
        ALTER TABLE deliveries
            ADD COLUMN received_by_user_id INT NULL,
            ADD INDEX `idx_deliveries_received_by_user_id` (`received_by_user_id`);
    ");

            migrationBuilder.Sql(@"
        ALTER TABLE deliveries
            ADD CONSTRAINT `fk_deliveries_received_by_user` FOREIGN KEY (`received_by_user_id`) REFERENCES `erp_users` (`id`);
    ");

            // ── BRC8 labeling module — missing containers columns ──────────────

            migrationBuilder.Sql(@"
        ALTER TABLE containers
            ADD COLUMN IF NOT EXISTS weighing_mode ENUM('MANUAL','SCALE') NOT NULL DEFAULT 'MANUAL',
            ADD COLUMN IF NOT EXISTS last_label_printed_at DATETIME NULL;
    ");

            // ── BRC8 labeling module — missing deliveries columns ──────────────

            migrationBuilder.Sql(@"
        ALTER TABLE deliveries
            ADD COLUMN IF NOT EXISTS weighing_status ENUM('NOT_STARTED','IN_PROGRESS','COMPLETED') NOT NULL DEFAULT 'NOT_STARTED',
            ADD COLUMN IF NOT EXISTS weighing_station_id INT NULL,
            ADD COLUMN IF NOT EXISTS weighing_started_at DATETIME NULL,
            ADD COLUMN IF NOT EXISTS weighing_completed_at DATETIME NULL,
            ADD COLUMN IF NOT EXISTS created_by_user_id INT NULL;
    ");

            migrationBuilder.Sql(@"
        ALTER TABLE deliveries
            ADD INDEX IF NOT EXISTS `idx_deliveries_weighing_station_id` (`weighing_station_id`),
            ADD INDEX IF NOT EXISTS `idx_deliveries_created_by_user_id` (`created_by_user_id`);
    ");

            migrationBuilder.Sql(@"
        ALTER TABLE deliveries
            ADD CONSTRAINT IF NOT EXISTS `fk_deliveries_weighing_station` FOREIGN KEY (`weighing_station_id`) REFERENCES `weighing_stations` (`id`),
            ADD CONSTRAINT IF NOT EXISTS `fk_deliveries_created_by_user` FOREIGN KEY (`created_by_user_id`) REFERENCES `erp_users` (`id`);
    ");

            // UNIQUE key on delivery_number (race condition protection)
            migrationBuilder.Sql(@"
        ALTER TABLE deliveries
            ADD UNIQUE KEY IF NOT EXISTS `uk_delivery_number` (`delivery_number`);
    ");

            // ── BRC8 labeling module — missing business_partners columns ───────

            migrationBuilder.Sql(@"
        ALTER TABLE business_partners
            ADD COLUMN IF NOT EXISTS approval_status VARCHAR(15) NULL,
            ADD COLUMN IF NOT EXISTS approval_date DATE NULL,
            ADD COLUMN IF NOT EXISTS approval_expires_at DATE NULL,
            ADD COLUMN IF NOT EXISTS supplier_risk_level ENUM('LOW','MEDIUM','HIGH') NULL,
            ADD COLUMN IF NOT EXISTS default_origin_country VARCHAR(100) NULL DEFAULT 'Lietuva';
    ");

            // ── BRC8 labeling module — fix container_label_events columns ──────
            // Spec requires reason_code, reason_text; operator_id should be NULLable

            migrationBuilder.Sql(@"
        ALTER TABLE container_label_events
            ADD COLUMN IF NOT EXISTS reason_code ENUM('DAMAGED','LOST','MISPRINT','OTHER') NULL,
            ADD COLUMN IF NOT EXISTS reason_text VARCHAR(200) NULL;
    ");

            migrationBuilder.Sql(@"
        ALTER TABLE container_label_events
            MODIFY operator_id INT NULL;
    ");

            // ── BRC8 labeling module — fix label_templates columns ────────────
            // Spec: template_type ENUM('RECEIPT_BARREL','RECEIPT_BUCKET','QUARANTINE_BARREL','QUARANTINE_BUCKET','LOT_BARREL','LOT_BUCKET')
            //       scriban_content LONGTEXT, label_width_mm, label_height_mm, is_default
            // Existing: template_type ENUM('ZPL','EPL','PLAIN_TEXT'), content, width_mm, height_mm, default_printer_id

            // Widen template_type enum first
            migrationBuilder.Sql(@"
        ALTER TABLE label_templates
            MODIFY template_type ENUM('ZPL','EPL','PLAIN_TEXT','RECEIPT_BARREL','RECEIPT_BUCKET','QUARANTINE_BARREL','QUARANTINE_BUCKET','LOT_BARREL','LOT_BUCKET') NOT NULL DEFAULT 'ZPL';
    ");

            migrationBuilder.Sql(@"
        ALTER TABLE label_templates
            ADD COLUMN IF NOT EXISTS scriban_content LONGTEXT NULL,
            ADD COLUMN IF NOT EXISTS label_width_mm DECIMAL(5,1) NOT NULL DEFAULT 108.0,
            ADD COLUMN IF NOT EXISTS label_height_mm DECIMAL(5,1) NOT NULL DEFAULT 75.0,
            ADD COLUMN IF NOT EXISTS is_default TINYINT(1) NOT NULL DEFAULT 0;
    ");

            // ── BRC8 labeling module — fix supplier_approvals columns ──────────
            // Spec: approval_date, expires_at (NULL = no expiry), risk_level, approval_method,
            //       cert_number, is_current
            // Existing: approved_at, valid_until (NOT NULL), document_path

            migrationBuilder.Sql(@"
        ALTER TABLE supplier_approvals
            ADD COLUMN IF NOT EXISTS risk_level ENUM('LOW','MEDIUM','HIGH') NOT NULL DEFAULT 'LOW',
            ADD COLUMN IF NOT EXISTS approval_method ENUM('AUDIT','QUESTIONNAIRE','CERTIFICATION','OTHER') NOT NULL DEFAULT 'OTHER',
            ADD COLUMN IF NOT EXISTS cert_number VARCHAR(100) NULL,
            ADD COLUMN IF NOT EXISTS is_current TINYINT(1) NOT NULL DEFAULT 1;
    ");

            // Rename approved_at → approval_date, valid_until → expires_at (make nullable)
            migrationBuilder.Sql(@"
        ALTER TABLE supplier_approvals
            CHANGE COLUMN approved_at approval_date DATE NOT NULL;
    ");

            migrationBuilder.Sql(@"
        ALTER TABLE supplier_approvals
            CHANGE COLUMN valid_until expires_at DATE NULL;
    ");

            // ── BRC8 labeling module — fix non_conformances columns ───────────
            // Spec: ref_type ENUM('DELIVERY','CONTAINER'), ref_id INT, detected_by, detected_at,
            //       severity ENUM('MINOR','MAJOR','CRITICAL'), disposition, disposition_by,
            //       disposition_at, disposition_notes
            // Existing: delivery_id, container_id, nc_type, discovered_by, discovered_at,
            //           status, corrective_action, closed_by, closed_at

            // Widen status → disposition enum
            migrationBuilder.Sql(@"
        ALTER TABLE non_conformances
            MODIFY status ENUM('OPEN','INVESTIGATING','RESOLVED','CLOSED','PENDING','ACCEPTED','REJECTED','REWORKED','QUARANTINED') NOT NULL DEFAULT 'OPEN';
    ");

            migrationBuilder.Sql(@"
        ALTER TABLE non_conformances
            ADD COLUMN IF NOT EXISTS ref_type ENUM('DELIVERY','CONTAINER') NULL,
            ADD COLUMN IF NOT EXISTS ref_id INT NULL,
            ADD COLUMN IF NOT EXISTS severity ENUM('MINOR','MAJOR','CRITICAL') NULL,
            ADD COLUMN IF NOT EXISTS disposition_notes TEXT NULL,
            ADD COLUMN IF NOT EXISTS detected_by INT NULL,
            ADD COLUMN IF NOT EXISTS detected_at DATETIME NULL,
            ADD COLUMN IF NOT EXISTS disposition_by INT NULL,
            ADD COLUMN IF NOT EXISTS disposition_at DATETIME NULL;
    ");

            // ── BRC8 labeling module — fix document_files columns ─────────────
            // Spec: ref_type ENUM('DELIVERY','LOT','ORDER'), ref_id, doc_type,
            //       original_filename, generated_at, generated_by
            // Existing: document_type, document_ref, file_size, content_type,
            //           uploaded_by, uploaded_at

            migrationBuilder.Sql(@"
        ALTER TABLE document_files
            ADD COLUMN IF NOT EXISTS ref_type ENUM('DELIVERY','LOT','ORDER') NULL,
            ADD COLUMN IF NOT EXISTS ref_id INT NULL,
            ADD COLUMN IF NOT EXISTS doc_type ENUM('PACKING_LIST','CMR','QUALITY_CERT','RECEIPT_ACT') NULL,
            ADD COLUMN IF NOT EXISTS original_filename VARCHAR(200) NULL,
            ADD COLUMN IF NOT EXISTS generated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
            ADD COLUMN IF NOT EXISTS generated_by INT NULL;
    ");

            migrationBuilder.Sql("SET FOREIGN_KEY_CHECKS=1;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS `AspNetRoleClaims`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `AspNetRoles`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `AspNetUserClaims`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `AspNetUserLogins`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `AspNetUserRoles`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `AspNetUserTokens`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `AspNetUsers`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `app_settings`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `bank_import_rows`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `bank_imports`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `business_partners`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `companies`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `company_settings`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `container_label_events`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `container_weight_corrections`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `containers`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `credit_note_lines`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `credit_notes`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `currencies`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `deliveries`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `delivery_lines`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `document_files`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `email_invoice_imports`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `erp_users`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `expense_budgets`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `expense_categories`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `expense_cost_centers`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `expense_invoice_audit`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `expense_invoice_lines`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `expense_invoices`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `expense_line_allocations`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `expense_ocr_queue`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `expense_payments`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `honey_deliveries`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `honey_types`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `invoice_lines`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `invoices`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `label_templates`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `lots`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `non_conformances`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `order_lines`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `orders`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `payment_allocations`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `payment_audit_log`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `payments`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `product_categories`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `production_batch_ingredients`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `production_batches`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `print_jobs`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `products`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `raw_material_types`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `stock_movements`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `supplier_approvals`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `supplier_payments`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `warehouse_stock`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `warehouse_stocks`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `warehouse_types`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `weighing_stations`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `warehouses`");
        }
    }
}
