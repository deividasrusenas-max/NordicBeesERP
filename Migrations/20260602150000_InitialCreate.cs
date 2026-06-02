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
            // AspNet Core Identity tables
            migrationBuilder.Sql(@"
                CREATE TABLE `AspNetRoleClaims` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `RoleId` varchar(255) NOT NULL,
                    `ClaimType` longtext,
                    `ClaimValue` longtext,
                    PRIMARY KEY (`Id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `AspNetRoles` (
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
                CREATE TABLE `AspNetUserClaims` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `UserId` varchar(255) NOT NULL,
                    `ClaimType` longtext,
                    `ClaimValue` longtext,
                    PRIMARY KEY (`Id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `AspNetUserLogins` (
                    `LoginProvider` varchar(255) NOT NULL,
                    `ProviderKey` varchar(255) NOT NULL,
                    `ProviderDisplayName` longtext,
                    `UserId` varchar(255) NOT NULL,
                    PRIMARY KEY (`LoginProvider`, `ProviderKey`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `AspNetUserRoles` (
                    `UserId` varchar(255) NOT NULL,
                    `RoleId` varchar(255) NOT NULL,
                    PRIMARY KEY (`UserId`, `RoleId`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `AspNetUserTokens` (
                    `UserId` varchar(255) NOT NULL,
                    `LoginProvider` varchar(255) NOT NULL,
                    `Name` varchar(255) NOT NULL,
                    `Value` longtext,
                    PRIMARY KEY (`UserId`, `LoginProvider`, `Name`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `AspNetUsers` (
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
                CREATE TABLE `app_settings` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `setting_key` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `setting_value` text COLLATE utf8mb4_unicode_ci,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `setting_key` (`setting_key`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `bank_import_rows` (
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
                    KEY `idx_import_id` (`import_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `bank_imports` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `import_date` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `source_file` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `total_rows` int NOT NULL,
                    `processed_rows` int NOT NULL DEFAULT '0',
                    `success_count` int NOT NULL DEFAULT '0',
                    `error_count` int NOT NULL DEFAULT '0',
                    `status` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'pending',
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `processed_at` datetime DEFAULT NULL,
                    `error_message` text COLLATE utf8mb4_unicode_ci,
                    PRIMARY KEY (`id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `business_partners` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `partner_type` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `name` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `vat_code` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `company_code` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `address` text COLLATE utf8mb4_unicode_ci,
                    `city` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `postal_code` varchar(20) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `country_code` varchar(5) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `bank_account` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `bank_name` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `contact_name` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `contact_phone` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `contact_email` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `is_active` tinyint(1) NOT NULL DEFAULT '1',
                    `is_supplier` tinyint(1) NOT NULL DEFAULT '0',
                    `is_customer` tinyint(1) NOT NULL DEFAULT '0',
                    `notes` text COLLATE utf8mb4_unicode_ci,
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    KEY `idx_partner_type` (`partner_type`),
                    KEY `idx_vat_code` (`vat_code`),
                    KEY `idx_name` (`name`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `companies` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `name` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `code` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `vat_code` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `address` text COLLATE utf8mb4_unicode_ci,
                    `bank_name` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `bank_iban` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `is_active` tinyint(1) NOT NULL DEFAULT '1',
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `company_settings` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `company_id` int NOT NULL,
                    `company_name` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `company_code` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `vat_code` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `address` text COLLATE utf8mb4_unicode_ci,
                    `bank_name` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `bank_iban` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    KEY `idx_company_id` (`company_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `containers` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `type` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `capacity` decimal(10,2) NOT NULL,
                    `unit_of_measure` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'kg',
                    `is_active` tinyint(1) NOT NULL DEFAULT '1',
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `credit_note_lines` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `credit_note_id` int NOT NULL,
                    `invoice_id` int NOT NULL,
                    `product_id` int DEFAULT NULL,
                    `description` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `quantity` decimal(10,2) NOT NULL,
                    `unit_price` decimal(10,2) NOT NULL,
                    `vat_rate` decimal(5,2) NOT NULL DEFAULT '21.00',
                    `amount_excl_vat` decimal(10,2) NOT NULL,
                    `vat_amount` decimal(10,2) NOT NULL,
                    `amount_incl_vat` decimal(10,2) NOT NULL,
                    `sort_order` int NOT NULL,
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    KEY `idx_credit_note_id` (`credit_note_id`),
                    KEY `idx_invoice_id` (`invoice_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `credit_notes` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `invoice_id` int NOT NULL,
                    `customer_id` int NOT NULL,
                    `credit_note_number` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `issue_date` date NOT NULL,
                    `due_date` date DEFAULT NULL,
                    `reason` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `amount_excl_vat` decimal(10,2) NOT NULL,
                    `vat_amount` decimal(10,2) NOT NULL,
                    `amount_incl_vat` decimal(10,2) NOT NULL,
                    `status` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'DRAFT',
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `uk_credit_note_number` (`credit_note_number`),
                    KEY `idx_invoice_id` (`invoice_id`),
                    KEY `idx_customer_id` (`customer_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `currencies` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `code` varchar(3) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `symbol` varchar(10) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `is_active` tinyint(1) NOT NULL DEFAULT '1',
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `uk_code` (`code`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `deliveries` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `delivery_number` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `delivery_date` date NOT NULL,
                    `customer_id` int NOT NULL,
                    `supplier_id` int DEFAULT NULL,
                    `warehouse_id` int NOT NULL,
                    `driver_name` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `vehicle_number` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `status` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PENDING',
                    `notes` text COLLATE utf8mb4_unicode_ci,
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `uk_delivery_number` (`delivery_number`),
                    KEY `idx_customer_id` (`customer_id`),
                    KEY `idx_supplier_id` (`supplier_id`),
                    KEY `idx_warehouse_id` (`warehouse_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `delivery_lines` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `delivery_id` int NOT NULL,
                    `product_id` int NOT NULL,
                    `lot_id` int DEFAULT NULL,
                    `quantity` decimal(10,2) NOT NULL,
                    `unit_price` decimal(10,2) NOT NULL,
                    `amount_incl_vat` decimal(10,2) NOT NULL,
                    `sort_order` int NOT NULL,
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    KEY `idx_delivery_id` (`delivery_id`),
                    KEY `idx_product_id` (`product_id`),
                    KEY `idx_lot_id` (`lot_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `email_invoice_imports` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `email_subject` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `email_from` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `email_date` datetime NOT NULL,
                    `file_name` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `file_path` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `invoice_number` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `invoice_date` date DEFAULT NULL,
                    `amount_incl_vat` decimal(10,2) DEFAULT NULL,
                    `supplier_name` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `status` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PENDING',
                    `error_message` text COLLATE utf8mb4_unicode_ci,
                    `processed_at` datetime DEFAULT NULL,
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    KEY `idx_status` (`status`),
                    KEY `idx_invoice_number` (`invoice_number`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `erp_users` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `user_id` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `company_id` int DEFAULT NULL,
                    `role` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'USER',
                    `is_active` tinyint(1) NOT NULL DEFAULT '1',
                    `permissions` text COLLATE utf8mb4_unicode_ci,
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `uk_user_id` (`user_id`),
                    KEY `idx_company_id` (`company_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `expense_budgets` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `category_id` int NOT NULL,
                    `cost_center_id` int DEFAULT NULL,
                    `year` int NOT NULL,
                    `month` int NOT NULL,
                    `budget_amount` decimal(10,2) NOT NULL,
                    `actual_amount` decimal(10,2) NOT NULL DEFAULT '0.00',
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `uk_category_month_year` (`category_id`, `cost_center_id`, `year`, `month`),
                    KEY `idx_category_id` (`category_id`),
                    KEY `idx_cost_center_id` (`cost_center_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `expense_categories` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `code` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `is_active` tinyint(1) NOT NULL DEFAULT '1',
                    `sort_order` int NOT NULL DEFAULT '0',
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `uk_code` (`code`),
                    UNIQUE KEY `uk_name` (`name`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `expense_cost_centers` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `code` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `is_active` tinyint(1) NOT NULL DEFAULT '1',
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `uk_code` (`code`),
                    UNIQUE KEY `uk_name` (`name`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `expense_invoice_audit` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `invoice_id` int NOT NULL,
                    `invoice_number` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `action` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `action_details` text COLLATE utf8mb4_unicode_ci,
                    `old_status` varchar(30) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `new_status` varchar(30) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `performed_by` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `performed_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    KEY `idx_invoice_id` (`invoice_id`),
                    KEY `idx_performed_at` (`performed_at`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `expense_invoice_lines` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `invoice_id` int NOT NULL,
                    `category_id` int DEFAULT NULL,
                    `description` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `quantity` decimal(10,4) DEFAULT NULL,
                    `unit_price` decimal(10,2) NOT NULL,
                    `unit_of_measure` varchar(20) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `amount_excl_vat` decimal(10,2) NOT NULL,
                    `vat_rate` decimal(5,2) NOT NULL DEFAULT '0.00',
                    `amount_incl_vat` decimal(10,2) NOT NULL,
                    `sort_order` int NOT NULL,
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    KEY `idx_invoice_id` (`invoice_id`),
                    KEY `idx_category_id` (`category_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `expense_invoices` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `supplier_id` int DEFAULT NULL,
                    `invoice_type` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'STANDARD',
                    `pending_supplier_name` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `pending_supplier_vat` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `pending_supplier_address` text COLLATE utf8mb4_unicode_ci,
                    `pending_supplier_city` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `pending_supplier_postal_code` varchar(20) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `pending_supplier_country_code` varchar(5) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `pending_supplier_company_code` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `pending_supplier_bank_account` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `invoice_number` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `invoice_date` date NOT NULL,
                    `due_date` date DEFAULT NULL,
                    `amount_excl_vat` decimal(10,2) NOT NULL,
                    `vat_rate` decimal(5,2) NOT NULL DEFAULT '21.00',
                    `vat_amount` decimal(10,2) NOT NULL,
                    `amount_incl_vat` decimal(10,2) NOT NULL,
                    `paid_amount` decimal(10,2) NOT NULL DEFAULT '0.00',
                    `status` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PENDING',
                    `ocr_status` varchar(20) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `ocr_confidence` int DEFAULT NULL,
                    `ocr_flags` text COLLATE utf8mb4_unicode_ci,
                    `ocr_raw_json` text COLLATE utf8mb4_unicode_ci,
                    `notes` text COLLATE utf8mb4_unicode_ci,
                    `approved_by` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `approved_at` datetime DEFAULT NULL,
                    `rejected_reason` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `source` varchar(10) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'MANUAL',
                    `original_filename` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `ocr_pipeline` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `currency` varchar(3) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'EUR',
                    `original_file_path` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `supplier_vat_verified` tinyint(1) DEFAULT NULL,
                    `supplier_vat_verified_name` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `category_id` int DEFAULT NULL,
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `uk_invoice_number` (`invoice_number`),
                    KEY `idx_supplier_id` (`supplier_id`),
                    KEY `idx_status` (`status`),
                    KEY `idx_invoice_date` (`invoice_date`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `expense_line_allocations` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `invoice_line_id` int NOT NULL,
                    `cost_center_id` int DEFAULT NULL,
                    `project_id` int DEFAULT NULL,
                    `amount` decimal(10,2) NOT NULL,
                    `percentage` decimal(5,2) NOT NULL,
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    KEY `idx_invoice_line_id` (`invoice_line_id`),
                    KEY `idx_cost_center_id` (`cost_center_id`),
                    KEY `idx_project_id` (`project_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `expense_ocr_queue` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `file_path` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `file_name` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `file_size` int NOT NULL,
                    `file_type` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `status` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PENDING',
                    `error_message` text COLLATE utf8mb4_unicode_ci,
                    `processed_at` datetime DEFAULT NULL,
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    KEY `idx_status` (`status`),
                    KEY `idx_created_at` (`created_at`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `expense_payments` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `invoice_id` int NOT NULL,
                    `payment_date` date NOT NULL,
                    `amount` decimal(10,2) NOT NULL,
                    `payment_method` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'BANK',
                    `bank_reference` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `notes` text COLLATE utf8mb4_unicode_ci,
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    KEY `idx_invoice_id` (`invoice_id`),
                    KEY `idx_payment_date` (`payment_date`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `honey_deliveries` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `delivery_date` date NOT NULL,
                    `beekeeper_id` int NOT NULL,
                    `total_weight` decimal(10,2) NOT NULL,
                    `total_price` decimal(10,2) NOT NULL,
                    `payment_method` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'CASH',
                    `notes` text COLLATE utf8mb4_unicode_ci,
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    KEY `idx Beekeeper_id` (`beekeeper_id`),
                    KEY `idx_delivery_date` (`delivery_date`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `honey_types` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `code` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `description` text COLLATE utf8mb4_unicode_ci,
                    `is_active` tinyint(1) NOT NULL DEFAULT '1',
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `uk_code` (`code`),
                    UNIQUE KEY `uk_name` (`name`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `invoice_lines` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `invoice_id` int NOT NULL,
                    `product_id` int DEFAULT NULL,
                    `lot_id` int DEFAULT NULL,
                    `description` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `quantity` decimal(10,2) NOT NULL,
                    `unit_price` decimal(10,2) NOT NULL,
                    `vat_rate` decimal(5,2) NOT NULL DEFAULT '21.00',
                    `amount_excl_vat` decimal(10,2) NOT NULL,
                    `vat_amount` decimal(10,2) NOT NULL,
                    `amount_incl_vat` decimal(10,2) NOT NULL,
                    `sort_order` int NOT NULL,
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    KEY `idx_invoice_id` (`invoice_id`),
                    KEY `idx_product_id` (`product_id`),
                    KEY `idx_lot_id` (`lot_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `invoices` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `invoice_number` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `invoice_date` date NOT NULL,
                    `due_date` date DEFAULT NULL,
                    `customer_id` int NOT NULL,
                    `warehouse_id` int NOT NULL,
                    `amount_excl_vat` decimal(10,2) NOT NULL,
                    `vat_amount` decimal(10,2) NOT NULL,
                    `amount_incl_vat` decimal(10,2) NOT NULL,
                    `paid_amount` decimal(10,2) NOT NULL DEFAULT '0.00',
                    `status` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'DRAFT',
                    `notes` text COLLATE utf8mb4_unicode_ci,
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `uk_invoice_number` (`invoice_number`),
                    KEY `idx_customer_id` (`customer_id`),
                    KEY `idx_warehouse_id` (`warehouse_id`),
                    KEY `idx_status` (`status`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `lots` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `lot_number` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `product_id` int NOT NULL,
                    `production_date` date NOT NULL,
                    `expiry_date` date DEFAULT NULL,
                    `initial_quantity` decimal(10,2) NOT NULL,
                    `remaining_quantity` decimal(10,2) NOT NULL,
                    `storage_location` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `notes` text COLLATE utf8mb4_unicode_ci,
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `uk_lot_number` (`lot_number`),
                    KEY `idx_product_id` (`product_id`),
                    KEY `idx_expiry_date` (`expiry_date`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `order_lines` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `order_id` int NOT NULL,
                    `product_id` int NOT NULL,
                    `quantity` decimal(10,2) NOT NULL,
                    `unit_price` decimal(10,2) NOT NULL,
                    `vat_rate` decimal(5,2) NOT NULL DEFAULT '21.00',
                    `amount_excl_vat` decimal(10,2) NOT NULL,
                    `amount_incl_vat` decimal(10,2) NOT NULL,
                    `delivery_date` date DEFAULT NULL,
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    KEY `idx_order_id` (`order_id`),
                    KEY `idx_product_id` (`product_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `orders` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `order_number` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `order_date` date NOT NULL,
                    `customer_id` int NOT NULL,
                    `warehouse_id` int NOT NULL,
                    `status` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'DRAFT',
                    `notes` text COLLATE utf8mb4_unicode_ci,
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `uk_order_number` (`order_number`),
                    KEY `idx_customer_id` (`customer_id`),
                    KEY `idx_warehouse_id` (`warehouse_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `payment_allocations` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `payment_id` int NOT NULL,
                    `invoice_id` int NOT NULL,
                    `allocated_amount` decimal(10,2) NOT NULL,
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    KEY `idx_payment_id` (`payment_id`),
                    KEY `idx_invoice_id` (`invoice_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `payment_audit_log` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `payment_id` int NOT NULL,
                    `action` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `action_details` text COLLATE utf8mb4_unicode_ci,
                    `performed_by` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `performed_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    KEY `idx_payment_id` (`payment_id`),
                    KEY `idx_performed_at` (`performed_at`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `payments` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `payment_number` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `payment_date` date NOT NULL,
                    `customer_id` int NOT NULL,
                    `invoice_id` int NOT NULL,
                    `amount` decimal(10,2) NOT NULL,
                    `payment_method` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'BANK',
                    `bank_reference` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `notes` text COLLATE utf8mb4_unicode_ci,
                    `status` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'COMPLETED',
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `uk_payment_number` (`payment_number`),
                    KEY `idx_customer_id` (`customer_id`),
                    KEY `idx_invoice_id` (`invoice_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `product_categories` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `code` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `parent_id` int DEFAULT NULL,
                    `is_active` tinyint(1) NOT NULL DEFAULT '1',
                    `sort_order` int NOT NULL DEFAULT '0',
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `uk_code` (`code`),
                    UNIQUE KEY `uk_name` (`name`),
                    KEY `idx_parent_id` (`parent_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `production_batch_ingredients` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `batch_id` int NOT NULL,
                    `raw_material_id` int NOT NULL,
                    `quantity` decimal(10,2) NOT NULL,
                    `unit_of_measure` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'kg',
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    KEY `idx_batch_id` (`batch_id`),
                    KEY `idx_raw_material_id` (`raw_material_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `production_batches` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `batch_number` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `product_id` int NOT NULL,
                    `production_date` date NOT NULL,
                    `quantity` decimal(10,2) NOT NULL,
                    `unit_of_measure` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'kg',
                    `status` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PLANNED',
                    `notes` text COLLATE utf8mb4_unicode_ci,
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `uk_batch_number` (`batch_number`),
                    KEY `idx_product_id` (`product_id`),
                    KEY `idx_production_date` (`production_date`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `products` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `name` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `code` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `product_category_id` int DEFAULT NULL,
                    `unit_of_measure` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'kg',
                    `is_active` tinyint(1) NOT NULL DEFAULT '1',
                    `is_raw_material` tinyint(1) NOT NULL DEFAULT '0',
                    `is_finished_product` tinyint(1) NOT NULL DEFAULT '0',
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `uk_code` (`code`),
                    KEY `idx_category_id` (`product_category_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `raw_material_types` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `code` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `description` text COLLATE utf8mb4_unicode_ci,
                    `is_active` tinyint(1) NOT NULL DEFAULT '1',
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `uk_code` (`code`),
                    UNIQUE KEY `uk_name` (`name`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `stock_movements` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `product_id` int NOT NULL,
                    `lot_id` int DEFAULT NULL,
                    `warehouse_id` int NOT NULL,
                    `movement_type` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `quantity` decimal(10,2) NOT NULL,
                    `unit_price` decimal(10,2) NOT NULL DEFAULT '0.00',
                    `reference_id` int DEFAULT NULL,
                    `reference_type` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    KEY `idx_product_id` (`product_id`),
                    KEY `idx_lot_id` (`lot_id`),
                    KEY `idx_warehouse_id` (`warehouse_id`),
                    KEY `idx_movement_type` (`movement_type`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `supplier_payments` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `supplier_id` int NOT NULL,
                    `payment_date` date NOT NULL,
                    `amount` decimal(10,2) NOT NULL,
                    `payment_method` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'BANK',
                    `bank_reference` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `notes` text COLLATE utf8mb4_unicode_ci,
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    KEY `idx_supplier_id` (`supplier_id`),
                    KEY `idx_payment_date` (`payment_date`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `units_of_measure` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `code` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `symbol` varchar(20) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `is_active` tinyint(1) NOT NULL DEFAULT '1',
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `uk_code` (`code`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `warehouse_stock` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `product_id` int NOT NULL,
                    `lot_id` int DEFAULT NULL,
                    `warehouse_id` int NOT NULL,
                    `quantity` decimal(10,2) NOT NULL,
                    `unit_price` decimal(10,2) NOT NULL DEFAULT '0.00',
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    KEY `idx_product_id` (`product_id`),
                    KEY `idx_lot_id` (`lot_id`),
                    KEY `idx_warehouse_id` (`warehouse_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `warehouse_stocks` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `product_id` int NOT NULL,
                    `lot_id` int DEFAULT NULL,
                    `warehouse_id` int NOT NULL,
                    `quantity` decimal(10,2) NOT NULL,
                    `unit_price` decimal(10,2) NOT NULL DEFAULT '0.00',
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    KEY `idx_product_id` (`product_id`),
                    KEY `idx_lot_id` (`lot_id`),
                    KEY `idx_warehouse_id` (`warehouse_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `warehouse_types` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `code` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `description` text COLLATE utf8mb4_unicode_ci,
                    `is_active` tinyint(1) NOT NULL DEFAULT '1',
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `uk_code` (`code`),
                    UNIQUE KEY `uk_name` (`name`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE `warehouses` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `code` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
                    `warehouse_type_id` int NOT NULL,
                    `location` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
                    `capacity` decimal(10,2) NOT NULL DEFAULT '0.00',
                    `is_active` tinyint(1) NOT NULL DEFAULT '1',
                    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (`id`),
                    UNIQUE KEY `uk_code` (`code`),
                    KEY `idx_warehouse_type_id` (`warehouse_type_id`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            ");
        }

        /// <inheritdoc />
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
            migrationBuilder.Sql("DROP TABLE IF EXISTS `containers`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `credit_note_lines`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `credit_notes`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `currencies`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `deliveries`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `delivery_lines`");
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
            migrationBuilder.Sql("DROP TABLE IF EXISTS `lots`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `order_lines`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `orders`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `payment_allocations`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `payment_audit_log`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `payments`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `product_categories`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `production_batch_ingredients`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `production_batches`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `products`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `raw_material_types`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `stock_movements`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `supplier_payments`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `units_of_measure`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `warehouse_stock`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `warehouse_stocks`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `warehouse_types`");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `warehouses`");
        }
    }
}