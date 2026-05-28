/*M!999999\- enable the sandbox mode */ 
-- MariaDB dump 10.19-12.1.2-MariaDB, for osx10.21 (arm64)
--
-- Host: 100.110.26.80    Database: nordic_bees_erp
-- ------------------------------------------------------
-- Server version	8.0.45-0ubuntu0.24.04.1

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*M!100616 SET @OLD_NOTE_VERBOSITY=@@NOTE_VERBOSITY, NOTE_VERBOSITY=0 */;

--
-- Table structure for table `AspNetRoleClaims`
--

DROP TABLE IF EXISTS `AspNetRoleClaims`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `AspNetRoleClaims` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `RoleId` varchar(255) NOT NULL,
  `ClaimType` longtext,
  `ClaimValue` longtext,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `AspNetRoleClaims`
--

LOCK TABLES `AspNetRoleClaims` WRITE;
/*!40000 ALTER TABLE `AspNetRoleClaims` DISABLE KEYS */;
set autocommit=0;
/*!40000 ALTER TABLE `AspNetRoleClaims` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `AspNetRoles`
--

DROP TABLE IF EXISTS `AspNetRoles`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `AspNetRoles` (
  `Id` varchar(255) NOT NULL,
  `Description` longtext,
  `AllowedModules` longtext,
  `Name` varchar(256) DEFAULT NULL,
  `NormalizedName` varchar(256) DEFAULT NULL,
  `ConcurrencyStamp` longtext,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `AspNetRoles`
--

LOCK TABLES `AspNetRoles` WRITE;
/*!40000 ALTER TABLE `AspNetRoles` DISABLE KEYS */;
set autocommit=0;
/*!40000 ALTER TABLE `AspNetRoles` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `AspNetUserClaims`
--

DROP TABLE IF EXISTS `AspNetUserClaims`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `AspNetUserClaims` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserId` varchar(255) NOT NULL,
  `ClaimType` longtext,
  `ClaimValue` longtext,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `AspNetUserClaims`
--

LOCK TABLES `AspNetUserClaims` WRITE;
/*!40000 ALTER TABLE `AspNetUserClaims` DISABLE KEYS */;
set autocommit=0;
/*!40000 ALTER TABLE `AspNetUserClaims` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `AspNetUserLogins`
--

DROP TABLE IF EXISTS `AspNetUserLogins`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `AspNetUserLogins` (
  `LoginProvider` varchar(255) NOT NULL,
  `ProviderKey` varchar(255) NOT NULL,
  `ProviderDisplayName` longtext,
  `UserId` varchar(255) NOT NULL,
  PRIMARY KEY (`LoginProvider`,`ProviderKey`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `AspNetUserLogins`
--

LOCK TABLES `AspNetUserLogins` WRITE;
/*!40000 ALTER TABLE `AspNetUserLogins` DISABLE KEYS */;
set autocommit=0;
/*!40000 ALTER TABLE `AspNetUserLogins` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `AspNetUserRoles`
--

DROP TABLE IF EXISTS `AspNetUserRoles`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `AspNetUserRoles` (
  `UserId` varchar(255) NOT NULL,
  `RoleId` varchar(255) NOT NULL,
  PRIMARY KEY (`UserId`,`RoleId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `AspNetUserRoles`
--

LOCK TABLES `AspNetUserRoles` WRITE;
/*!40000 ALTER TABLE `AspNetUserRoles` DISABLE KEYS */;
set autocommit=0;
/*!40000 ALTER TABLE `AspNetUserRoles` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `AspNetUserTokens`
--

DROP TABLE IF EXISTS `AspNetUserTokens`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `AspNetUserTokens` (
  `UserId` varchar(255) NOT NULL,
  `LoginProvider` varchar(255) NOT NULL,
  `Name` varchar(255) NOT NULL,
  `Value` longtext,
  PRIMARY KEY (`UserId`,`LoginProvider`,`Name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `AspNetUserTokens`
--

LOCK TABLES `AspNetUserTokens` WRITE;
/*!40000 ALTER TABLE `AspNetUserTokens` DISABLE KEYS */;
set autocommit=0;
/*!40000 ALTER TABLE `AspNetUserTokens` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `AspNetUsers`
--

DROP TABLE IF EXISTS `AspNetUsers`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
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
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `AspNetUsers`
--

LOCK TABLES `AspNetUsers` WRITE;
/*!40000 ALTER TABLE `AspNetUsers` DISABLE KEYS */;
set autocommit=0;
/*!40000 ALTER TABLE `AspNetUsers` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `__EFMigrationsHistory`
--

DROP TABLE IF EXISTS `__EFMigrationsHistory`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `__EFMigrationsHistory` (
  `MigrationId` varchar(150) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `ProductVersion` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  PRIMARY KEY (`MigrationId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `__EFMigrationsHistory`
--

LOCK TABLES `__EFMigrationsHistory` WRITE;
/*!40000 ALTER TABLE `__EFMigrationsHistory` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `__EFMigrationsHistory` VALUES
('20260222001656_AddEmailPropertyToEntities','7.0.0');
/*!40000 ALTER TABLE `__EFMigrationsHistory` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `business_partners`
--

DROP TABLE IF EXISTS `business_partners`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `business_partners` (
  `id` int NOT NULL AUTO_INCREMENT,
  `partner_type` enum('customer','supplier','both') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'customer',
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
  PRIMARY KEY (`id`),
  KEY `idx_partner_type` (`partner_type`),
  KEY `idx_name` (`name`),
  KEY `idx_vat_code` (`vat_code`),
  KEY `idx_country` (`country_code`)
) ENGINE=InnoDB AUTO_INCREMENT=8 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Klientai ir tiekėjai - unifikuota lentelė';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `business_partners`
--

LOCK TABLES `business_partners` WRITE;
/*!40000 ALTER TABLE `business_partners` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `business_partners` VALUES
(1,'customer','testas','12345','LT1213','adsasdasdas','dasdasd','12132','Lietuva','LT','213132131',NULL,'dasdads@dadas.com',NULL,'LT12131321313131313213213',30,'lt',21.00,NULL,NULL,NULL,NULL,'Geras klientas',1,'2026-02-20 07:08:12','2026-02-20 07:08:12'),
(2,'supplier','naujas tiekjas','1213','321321','zadas1ddasda312d1aas1d1','dsasdas','132321','Lietuva','LT','123123131',NULL,NULL,NULL,'LT12312321321',7,'lt',100.00,NULL,NULL,NULL,NULL,'z`x`zx`z',1,'2026-02-20 08:52:48','2026-02-20 08:52:48'),
(3,'customer','Ukininkas antanas','1321312','LT13131321321321','sadasdasdas','adasdasdas','12312','Lietuva','LT','1231321321',NULL,'dadas@asdasd.com',NULL,'LT32132313131323132132132',30,'lt',6.00,NULL,NULL,NULL,NULL,NULL,1,'2026-02-23 20:23:53','2026-02-23 20:23:53'),
(5,'supplier','Bartnik sadecki','1231321','PL01212121212','ulica dasdas. 65 , poland','dadasdas','PL12-456','Lietuva','LT','+48523223232',NULL,'info@testas.lt',NULL,'PL0132132132132123132121',7,'en',21.00,NULL,NULL,NULL,NULL,NULL,1,'2026-03-04 13:31:23','2026-03-04 13:31:23'),
(6,'supplier','Gintaras Cibas',NULL,'LT1321313231','Mokyklos g. 55','Ažuožeriai','LT-12356','Lietuva','LT','+37065236589',NULL,'cibas@testas.lt',NULL,'LT11321321313132113',7,'lt',6.00,NULL,NULL,NULL,NULL,NULL,1,'2026-03-04 21:01:13','2026-03-04 21:01:13'),
(7,'supplier','Mantas Raišys','312321321323','LT123123123123','Saltojos 10','Sodeliai','LT42457','Lietuva','LT','3123-01293',NULL,'mnasdaws@asddasd.com',NULL,'LT31231312312321312123',7,'lt',21.00,NULL,NULL,NULL,NULL,NULL,1,'2026-03-10 06:58:41','2026-03-10 06:58:41');
/*!40000 ALTER TABLE `business_partners` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `companies`
--

DROP TABLE IF EXISTS `companies`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `companies` (
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
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `companies`
--

LOCK TABLES `companies` WRITE;
/*!40000 ALTER TABLE `companies` DISABLE KEYS */;
set autocommit=0;
/*!40000 ALTER TABLE `companies` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `company_settings`
--

DROP TABLE IF EXISTS `company_settings`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `company_settings` (
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
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `company_settings`
--

LOCK TABLES `company_settings` WRITE;
/*!40000 ALTER TABLE `company_settings` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `company_settings` VALUES
(1,'MB Lakštena','302905315','LT100013406816',' dsasdsa P. Širvio g. 3, Juodupė, LT-42457','AB Artea Bankas','LT217189900060467854','CBSBLT26','','dasda','dasasd',NULL,'2026-03-09 14:03:48');
/*!40000 ALTER TABLE `company_settings` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `containers`
--

DROP TABLE IF EXISTS `containers`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `containers` (
  `id` int NOT NULL AUTO_INCREMENT,
  `container_code` varchar(50) NOT NULL,
  `container_type` enum('BARREL','BUCKET_GROUP') NOT NULL,
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
) ENGINE=InnoDB AUTO_INCREMENT=123 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `containers`
--

LOCK TABLES `containers` WRITE;
/*!40000 ALTER TABLE `containers` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `containers` VALUES
(17,'1','BARREL',2,9,1,1,1,3.000,1.000,2.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-03 19:16:50','2026-03-04 08:19:16'),
(18,'2','BARREL',2,9,1,1,1,3.000,1.000,2.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-03 19:16:50','2026-03-04 08:19:16'),
(19,'3','BARREL',2,9,1,1,1,3.000,1.000,2.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-03 19:16:50','2026-03-04 08:19:16'),
(20,'8','BARREL',2,13,1,1,1,306.000,16.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-03 19:24:49','2026-03-04 08:19:16'),
(21,'9','BARREL',2,13,1,1,1,306.000,16.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-03 19:24:49','2026-03-04 08:19:16'),
(22,'10','BARREL',2,13,1,1,1,306.000,16.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-03 19:24:49','2026-03-04 08:19:16'),
(23,'11','BARREL',2,13,1,1,1,306.000,16.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-03 19:24:49','2026-03-04 08:19:16'),
(24,'655','BARREL',2,14,1,1,4,306.000,16.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-03 19:46:30','2026-03-04 08:19:16'),
(25,'656','BARREL',2,14,1,1,4,306.000,16.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-03 19:46:30','2026-03-04 08:19:16'),
(26,'657','BARREL',2,14,1,1,4,306.000,16.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-03 19:46:30','2026-03-04 08:19:16'),
(27,'658','BARREL',2,14,1,1,4,306.000,16.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-03 19:46:30','2026-03-04 08:19:16'),
(28,'659','BARREL',2,14,1,1,4,306.000,16.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-03 19:46:30','2026-03-04 08:19:16'),
(29,'CZ20','BARREL',5,17,1,NULL,3,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-04 17:35:54','2026-03-09 13:53:20'),
(30,'CZ21','BARREL',5,17,1,NULL,3,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-04 17:35:54','2026-03-09 13:53:20'),
(31,'CZ22','BARREL',5,17,1,NULL,3,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-04 17:35:54','2026-03-09 13:53:20'),
(32,'CZ23','BARREL',5,17,1,NULL,3,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-04 17:35:54','2026-03-09 13:53:20'),
(33,'CZ24','BARREL',5,17,1,NULL,3,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-04 17:35:54','2026-03-09 13:53:20'),
(34,'CZ25','BARREL',5,17,1,NULL,3,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-04 17:35:54','2026-03-09 13:53:20'),
(35,'CZ26','BARREL',5,17,1,NULL,3,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-04 17:35:54','2026-03-09 13:53:20'),
(36,'CZ27','BARREL',5,17,1,NULL,3,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-04 17:35:54','2026-03-09 13:53:20'),
(37,'CZ28','BARREL',5,17,1,NULL,3,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-04 17:35:54','2026-03-09 13:53:20'),
(38,'CZ29','BARREL',5,17,1,NULL,3,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-04 17:35:54','2026-03-09 13:53:20'),
(39,'KB-20260305-001','BUCKET_GROUP',5,18,1,NULL,3,20.000,0.400,19.600,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-04 20:54:36','2026-03-09 13:53:20'),
(40,'2601','BARREL',6,19,2,NULL,5,312.000,19.000,293.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-04 21:06:54','2026-03-09 13:53:20'),
(41,'2602','BARREL',6,19,2,NULL,5,310.000,19.000,291.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-04 21:06:54','2026-03-09 13:53:20'),
(42,'KB-20260309-001','BUCKET_GROUP',6,20,1,NULL,NULL,2.000,1.600,0.400,4,4,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-08 21:22:42','2026-03-09 13:53:20'),
(43,'k12','BUCKET_GROUP',6,21,1,NULL,2,16.000,0.400,15.600,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-08 21:47:17','2026-03-09 13:53:20'),
(44,'25','BUCKET_GROUP',6,22,1,NULL,NULL,20.000,0.050,19.950,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-09 11:16:14','2026-03-09 13:53:20'),
(45,'26','BUCKET_GROUP',6,22,1,NULL,NULL,20.000,0.050,19.950,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-09 11:16:14','2026-03-09 13:53:20'),
(46,'2603','BARREL',6,23,1,NULL,3,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-09 13:40:29','2026-03-09 13:40:29'),
(47,'2604','BARREL',6,23,1,NULL,3,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-09 13:40:29','2026-03-09 13:40:29'),
(48,'2605','BARREL',6,23,1,NULL,3,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-09 13:40:29','2026-03-09 13:40:29'),
(49,'2606','BARREL',6,23,1,NULL,3,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-09 13:40:29','2026-03-09 13:40:29'),
(50,'2607','BARREL',6,23,1,NULL,3,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-09 13:40:29','2026-03-09 13:40:29'),
(51,'2608','BARREL',6,24,1,NULL,NULL,300.000,19.000,281.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-09 13:44:50','2026-03-09 13:44:50'),
(52,'2609','BARREL',6,24,1,NULL,NULL,290.000,19.000,271.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-09 13:44:50','2026-03-09 13:44:50'),
(53,'2610','BARREL',2,25,1,NULL,4,300.000,19.000,281.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-09 13:45:28','2026-03-10 07:05:22'),
(54,'2611','BARREL',2,25,1,NULL,4,300.000,19.000,281.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-09 13:45:28','2026-03-10 07:05:22'),
(55,'2612','BARREL',2,29,1,NULL,NULL,300.000,19.000,281.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-09 13:45:28','2026-03-10 07:05:23'),
(56,'27','BUCKET_GROUP',6,26,1,NULL,9,15.000,0.400,14.600,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-09 13:46:59','2026-03-09 20:35:04'),
(57,'28','BUCKET_GROUP',6,26,1,NULL,9,15.000,0.400,14.600,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-09 13:46:59','2026-03-09 20:35:04'),
(58,'29','BUCKET_GROUP',6,26,1,NULL,9,15.000,0.400,14.600,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-09 13:46:59','2026-03-09 20:35:04'),
(59,'30','BUCKET_GROUP',6,26,1,NULL,9,15.000,0.400,14.600,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-09 13:46:59','2026-03-09 20:35:04'),
(60,'31','BUCKET_GROUP',6,26,1,NULL,2,15.000,0.400,14.600,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-09 13:46:59','2026-03-09 20:34:18'),
(61,'2613','BARREL',7,27,2,NULL,7,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:00:22','2026-03-10 07:04:22'),
(62,'2614','BARREL',7,27,2,NULL,7,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:00:22','2026-03-10 07:04:22'),
(63,'2615','BARREL',7,27,2,NULL,7,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:00:22','2026-03-10 07:04:22'),
(64,'2616','BARREL',7,27,2,NULL,7,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:00:22','2026-03-10 07:04:22'),
(65,'2617','BARREL',7,27,2,NULL,7,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:00:22','2026-03-10 07:04:22'),
(66,'2618','BARREL',7,27,2,NULL,7,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:00:22','2026-03-10 07:04:22'),
(67,'2619','BARREL',7,27,2,NULL,7,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:00:22','2026-03-10 07:04:22'),
(68,'2620','BARREL',7,27,2,NULL,7,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:00:22','2026-03-10 07:04:22'),
(69,'2621','BARREL',7,27,2,NULL,7,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:00:22','2026-03-10 07:04:22'),
(70,'2622','BARREL',7,27,2,NULL,7,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:00:22','2026-03-10 07:04:22'),
(71,'2623','BARREL',7,27,2,NULL,7,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:00:22','2026-03-10 07:04:22'),
(72,'2624','BARREL',7,27,2,NULL,7,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:00:22','2026-03-10 07:04:22'),
(73,'2625','BARREL',7,27,2,NULL,7,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:00:22','2026-03-10 07:04:22'),
(74,'2626','BARREL',7,27,2,NULL,7,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:00:22','2026-03-10 07:04:22'),
(75,'2627','BARREL',7,27,2,NULL,7,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:00:22','2026-03-10 07:04:22'),
(76,'2628','BARREL',7,27,2,NULL,7,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:00:22','2026-03-10 07:04:22'),
(77,'2629','BARREL',7,27,2,NULL,7,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:00:22','2026-03-10 07:04:22'),
(78,'2630','BARREL',7,27,2,NULL,7,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:00:22','2026-03-10 07:04:22'),
(79,'2631','BARREL',7,27,2,NULL,7,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:00:22','2026-03-10 07:04:22'),
(80,'2632','BARREL',7,27,2,NULL,7,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:00:22','2026-03-10 07:04:22'),
(81,'2632','BARREL',7,27,2,NULL,7,309.000,19.000,290.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:00:22','2026-03-10 07:04:22'),
(82,'2633','BARREL',7,28,1,NULL,5,300.000,19.000,281.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:03:18','2026-03-10 07:03:32'),
(83,'2634','BARREL',7,28,1,NULL,5,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:03:18','2026-03-10 07:03:18'),
(84,'2635','BARREL',7,28,1,NULL,5,305.000,16.000,289.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:03:18','2026-03-10 07:03:18'),
(85,'2636','BARREL',7,28,1,NULL,5,305.000,21.000,284.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:03:18','2026-03-10 07:03:18'),
(86,'32','BUCKET_GROUP',7,30,1,NULL,5,29.000,0.400,28.600,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:09:25','2026-03-10 07:09:25'),
(87,'33','BUCKET_GROUP',7,30,1,NULL,5,15.000,0.400,14.600,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:09:25','2026-03-10 07:09:25'),
(88,'34','BUCKET_GROUP',7,30,1,NULL,5,15.000,0.400,14.600,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:09:25','2026-03-10 07:09:25'),
(89,'35','BUCKET_GROUP',7,30,1,NULL,5,15.000,0.400,14.600,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:09:25','2026-03-10 07:09:25'),
(90,'36','BUCKET_GROUP',7,30,1,NULL,5,15.000,0.400,14.600,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:09:25','2026-03-10 07:09:25'),
(91,'37','BUCKET_GROUP',7,30,1,NULL,5,15.000,0.400,14.600,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:09:25','2026-03-10 07:09:25'),
(92,'38','BUCKET_GROUP',7,30,1,NULL,5,15.000,0.400,14.600,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:09:25','2026-03-10 07:09:25'),
(93,'39','BUCKET_GROUP',7,30,1,NULL,5,15.000,0.400,14.600,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 07:09:25','2026-03-10 07:09:25'),
(94,'2637','BARREL',5,31,1,NULL,2,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 20:50:35','2026-03-10 20:50:35'),
(95,'2638','BARREL',5,31,1,NULL,2,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 20:50:35','2026-03-10 20:50:35'),
(96,'2639','BARREL',5,31,1,NULL,2,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 20:50:35','2026-03-10 20:50:35'),
(97,'40','BUCKET_GROUP',5,32,1,NULL,NULL,6.000,0.050,5.950,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 20:51:18','2026-03-10 20:51:18'),
(98,'41','BUCKET_GROUP',5,32,1,NULL,NULL,6.000,0.050,5.950,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 20:51:18','2026-03-10 20:51:18'),
(99,'42','BUCKET_GROUP',5,32,1,NULL,NULL,6.000,0.050,5.950,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 20:51:18','2026-03-10 20:51:18'),
(100,'2640','BARREL',5,33,1,NULL,5,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 22:29:09','2026-03-10 22:29:09'),
(101,'2641','BARREL',5,33,1,NULL,5,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 22:29:09','2026-03-10 22:29:09'),
(102,'2642','BARREL',5,33,1,NULL,5,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 22:29:09','2026-03-10 22:29:09'),
(103,'2643','BARREL',5,33,1,NULL,5,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 22:29:09','2026-03-10 22:29:09'),
(104,'2644','BARREL',5,33,1,NULL,5,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 22:29:09','2026-03-10 22:29:09'),
(105,'2645','BARREL',5,33,1,NULL,5,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 22:29:09','2026-03-10 22:29:09'),
(106,'2646','BARREL',5,33,1,NULL,5,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 22:29:09','2026-03-10 22:29:09'),
(107,'2647','BARREL',5,33,1,NULL,5,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 22:29:09','2026-03-10 22:29:09'),
(108,'2648','BARREL',5,33,1,NULL,5,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 22:29:09','2026-03-10 22:29:09'),
(109,'2649','BARREL',5,33,1,NULL,5,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 22:29:09','2026-03-10 22:29:09'),
(110,'2650','BARREL',5,33,1,NULL,5,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 22:29:09','2026-03-10 22:29:09'),
(111,'2651','BARREL',5,33,1,NULL,5,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 22:29:09','2026-03-10 22:29:09'),
(112,'2652','BARREL',5,33,1,NULL,5,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 22:29:09','2026-03-10 22:29:09'),
(113,'2653','BARREL',5,33,1,NULL,5,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 22:29:09','2026-03-10 22:29:09'),
(114,'2654','BARREL',5,33,1,NULL,5,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 22:29:09','2026-03-10 22:29:09'),
(115,'2655','BARREL',5,33,1,NULL,5,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 22:29:09','2026-03-10 22:29:09'),
(116,'2656','BARREL',5,33,1,NULL,5,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 22:29:09','2026-03-10 22:29:09'),
(117,'2657','BARREL',5,33,1,NULL,5,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 22:29:09','2026-03-10 22:29:09'),
(118,'2658','BARREL',5,33,1,NULL,5,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 22:29:09','2026-03-10 22:29:09'),
(119,'2659','BARREL',5,33,1,NULL,5,305.000,19.000,286.000,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-10 22:29:09','2026-03-10 22:29:09'),
(120,'43','BUCKET_GROUP',7,34,1,NULL,NULL,25.000,0.150,24.850,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-14 16:45:47','2026-03-14 16:45:47'),
(121,'44','BUCKET_GROUP',7,34,1,NULL,NULL,15.000,0.150,14.850,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-14 16:45:47','2026-03-14 16:45:47'),
(122,'45','BUCKET_GROUP',7,34,1,NULL,NULL,15.000,0.150,14.850,1,1,'IN_STOCK',NULL,NULL,NULL,NULL,NULL,NULL,'2026-03-14 16:45:47','2026-03-14 16:45:47');
/*!40000 ALTER TABLE `containers` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `deliveries`
--

DROP TABLE IF EXISTS `deliveries`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `deliveries` (
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
  PRIMARY KEY (`id`),
  KEY `supplier_id` (`supplier_id`),
  KEY `warehouse_id` (`warehouse_id`),
  CONSTRAINT `deliveries_ibfk_1` FOREIGN KEY (`supplier_id`) REFERENCES `business_partners` (`id`),
  CONSTRAINT `deliveries_ibfk_2` FOREIGN KEY (`warehouse_id`) REFERENCES `warehouses` (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=53 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `deliveries`
--

LOCK TABLES `deliveries` WRITE;
/*!40000 ALTER TABLE `deliveries` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `deliveries` VALUES
(28,'asda','2026-03-03',2,1,'RECEIVED',6.000,0.00,0.00,0,0,NULL,'2026-03-03 19:16:50','2026-03-03 19:16:51',NULL,0),
(32,'asd','2026-03-03',2,1,'RECEIVED',1160.000,0.00,0.00,0,0,NULL,'2026-03-03 19:24:49','2026-03-03 19:24:49',NULL,0),
(33,'bcv','2026-03-03',2,1,'RECEIVED',1450.000,0.00,0.00,0,0,NULL,'2026-03-03 19:46:29','2026-03-03 19:46:30',NULL,0),
(36,'PR-2026-001','2026-03-04',5,1,'RECEIVED',2900.000,0.00,0.00,10,0,NULL,'2026-03-04 17:35:54','2026-03-04 17:36:47',1,1),
(37,'PR-2026-002','2026-03-05',5,1,'RECEIVED',19.600,0.00,0.00,0,0,NULL,'2026-03-04 20:54:36','2026-03-04 20:54:36',3,0),
(38,'PR-2026-003','2026-03-05',6,1,'RECEIVED',584.000,0.00,0.00,0,0,NULL,'2026-03-04 21:06:54','2026-03-04 21:06:54',1,0),
(39,'PR-BD2603-004','2026-03-09',6,1,'PRICED',0.400,12.80,0.00,0,0,NULL,'2026-03-08 21:22:42','2026-03-10 19:45:52',2,0),
(40,'PR-MD2603-005','2026-03-09',6,1,'RECEIVED',15.600,0.00,0.00,0,0,NULL,'2026-03-08 21:47:17','2026-03-08 21:47:17',1,0),
(41,'PR-PR2603-006','2026-03-09',6,1,'RECEIVED',39.900,0.00,0.00,0,0,NULL,'2026-03-09 11:16:14','2026-03-09 11:16:14',4,0),
(42,'PR-MD2603-007','2026-03-09',6,1,'RECEIVED',1430.000,0.00,0.00,5,0,NULL,'2026-03-09 13:40:29','2026-03-09 13:40:45',1,1),
(43,'PR-MD2603-007','2026-03-09',6,1,'RECEIVED',552.000,0.00,0.00,2,0,NULL,'2026-03-09 13:44:50','2026-03-09 13:52:46',1,1),
(44,'PR-MD2603-007','2026-03-09',2,1,'RECEIVED',843.000,0.00,0.00,0,0,NULL,'2026-03-09 13:45:28','2026-03-09 13:45:28',1,0),
(45,'PR-MD2603-007','2026-03-09',6,1,'RECEIVED',73.000,0.00,0.00,0,0,NULL,'2026-03-09 13:46:58','2026-03-09 13:46:59',1,0),
(46,'PR-MD2603-007','2026-03-10',7,1,'PRICED',6090.000,7612.50,0.00,13,0,NULL,'2026-03-10 07:00:21','2026-03-10 19:26:41',1,1),
(47,'PR-MD2603-007','2026-03-10',7,1,'PAID',1140.000,2280.00,0.00,0,0,NULL,'2026-03-10 07:03:17','2026-03-10 20:18:07',1,0),
(48,'PR-MD2603-007','2026-03-10',7,1,'RECEIVED',130.800,313.92,0.00,0,0,NULL,'2026-03-10 07:09:24','2026-03-10 21:27:02',1,0),
(49,'PR-MD2603-007','2026-03-11',5,1,'RECEIVED',858.000,1973.40,0.00,0,0,NULL,'2026-03-10 20:50:35','2026-03-10 21:52:10',1,0),
(50,'PR-BD2603-007','2026-03-11',5,1,'PAID',17.850,803.25,0.00,0,0,NULL,'2026-03-10 20:51:18','2026-03-10 21:51:19',2,0),
(51,'PR-MD2603-001','2026-03-11',5,1,'RECEIVED',5720.000,0.00,0.00,0,0,NULL,'2026-03-10 22:29:09','2026-03-14 13:27:43',1,0),
(52,'PR-BD2603-001','2026-03-14',7,1,'RECEIVED',54.550,0.00,0.00,0,0,NULL,'2026-03-14 16:45:47','2026-03-14 16:45:48',2,0);
/*!40000 ALTER TABLE `deliveries` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `delivery_lines`
--

DROP TABLE IF EXISTS `delivery_lines`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `delivery_lines` (
  `id` int NOT NULL AUTO_INCREMENT,
  `delivery_id` int NOT NULL,
  `product_id` int DEFAULT NULL,
  `honey_type_id` int DEFAULT NULL,
  `container_type` enum('BARREL','BUCKET_GROUP') NOT NULL,
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
) ENGINE=InnoDB AUTO_INCREMENT=35 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `delivery_lines`
--

LOCK TABLES `delivery_lines` WRITE;
/*!40000 ALTER TABLE `delivery_lines` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `delivery_lines` VALUES
(9,28,1,1,'BARREL',3,0.000,0.000,6.000,NULL,NULL,NULL,'2026-03-03 19:16:50','2026-03-03 19:16:50',NULL),
(13,32,1,1,'BARREL',4,0.000,0.000,1160.000,NULL,NULL,NULL,'2026-03-03 19:24:49','2026-03-03 19:24:49',NULL),
(14,33,1,4,'BARREL',5,0.000,0.000,1450.000,NULL,NULL,NULL,'2026-03-03 19:46:30','2026-03-03 19:46:30',NULL),
(17,36,NULL,3,'BARREL',10,0.000,0.000,2900.000,NULL,NULL,NULL,'2026-03-04 17:35:54','2026-03-04 17:35:54',NULL),
(18,37,NULL,3,'BUCKET_GROUP',1,0.000,0.000,19.600,1.5000,29.40,NULL,'2026-03-04 20:54:36','2026-03-08 22:41:43',NULL),
(19,38,NULL,5,'BARREL',2,0.000,0.000,584.000,NULL,NULL,NULL,'2026-03-04 21:06:54','2026-03-04 21:06:54',NULL),
(20,39,NULL,NULL,'BUCKET_GROUP',4,0.000,0.000,0.400,32.0000,12.80,NULL,'2026-03-08 21:22:42','2026-03-10 19:45:52',NULL),
(21,40,NULL,2,'BUCKET_GROUP',1,0.000,0.000,15.600,NULL,NULL,NULL,'2026-03-08 21:47:17','2026-03-08 21:47:17',NULL),
(22,41,NULL,NULL,'BUCKET_GROUP',2,0.000,0.000,39.900,NULL,NULL,NULL,'2026-03-09 11:16:14','2026-03-09 11:16:14',NULL),
(23,42,NULL,3,'BARREL',5,0.000,0.000,1430.000,2.0000,2860.00,NULL,'2026-03-09 13:40:29','2026-03-09 13:51:20',NULL),
(24,43,NULL,NULL,'BARREL',2,0.000,0.000,552.000,NULL,NULL,NULL,'2026-03-09 13:44:50','2026-03-09 13:44:50',NULL),
(25,44,NULL,4,'BARREL',2,0.000,0.000,562.000,5.0000,4215.00,NULL,'2026-03-09 13:45:28','2026-03-10 07:05:23',NULL),
(26,45,NULL,NULL,'BUCKET_GROUP',5,0.000,0.000,73.000,NULL,NULL,NULL,'2026-03-09 13:46:58','2026-03-09 13:46:58',NULL),
(27,46,NULL,7,'BARREL',21,0.000,0.000,6090.000,1.2500,7612.50,NULL,'2026-03-10 07:00:21','2026-03-10 19:20:12',NULL),
(28,47,NULL,5,'BARREL',4,0.000,0.000,1140.000,2.0000,2280.00,NULL,'2026-03-10 07:03:18','2026-03-10 19:14:28',NULL),
(29,44,NULL,NULL,'BARREL',1,0.000,0.000,281.000,5.0000,NULL,NULL,'2026-03-10 07:05:23','2026-03-10 07:05:23',NULL),
(30,48,NULL,5,'BUCKET_GROUP',8,0.000,0.000,130.800,2.4000,313.92,NULL,'2026-03-10 07:09:25','2026-03-10 21:27:02',NULL),
(31,49,NULL,2,'BARREL',3,0.000,0.000,858.000,2.3000,1973.40,NULL,'2026-03-10 20:50:35','2026-03-10 21:52:10',NULL),
(32,50,NULL,NULL,'BUCKET_GROUP',3,0.000,0.000,17.850,45.0000,803.25,NULL,'2026-03-10 20:51:18','2026-03-10 21:51:10',NULL),
(33,51,NULL,5,'BARREL',20,0.000,0.000,5720.000,NULL,NULL,NULL,'2026-03-10 22:29:09','2026-03-10 22:29:09',NULL),
(34,52,NULL,NULL,'BUCKET_GROUP',3,0.000,0.000,54.550,NULL,NULL,NULL,'2026-03-14 16:45:47','2026-03-14 16:45:47',NULL);
/*!40000 ALTER TABLE `delivery_lines` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `erp_users`
--

DROP TABLE IF EXISTS `erp_users`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `erp_users` (
  `id` int NOT NULL AUTO_INCREMENT,
  `email` varchar(256) COLLATE utf8mb4_unicode_ci NOT NULL,
  `password_hash` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL,
  `full_name` varchar(256) COLLATE utf8mb4_unicode_ci NOT NULL,
  `role` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'User',
  `is_active` tinyint(1) NOT NULL DEFAULT '1',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `email` (`email`)
) ENGINE=InnoDB AUTO_INCREMENT=3 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='ERP Users for cookie-based authentication';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `erp_users`
--

LOCK TABLES `erp_users` WRITE;
/*!40000 ALTER TABLE `erp_users` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `erp_users` VALUES
(2,'admin@nordicbees.lt','$2a$11$e9w7/RkppZgjHTGJoR1oH.hLlZwUO6zjy8o5uWPmfOmovFRtdICim','Administratorius','Admin',1,'2026-03-16 13:39:28');
/*!40000 ALTER TABLE `erp_users` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `expense_categories`
--

DROP TABLE IF EXISTS `expense_categories`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `expense_categories` (
  `id` int NOT NULL AUTO_INCREMENT,
  `code` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL,
  `name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `parent_id` int DEFAULT NULL,
  `is_active` tinyint(1) DEFAULT '1',
  PRIMARY KEY (`id`),
  UNIQUE KEY `code` (`code`),
  KEY `parent_id` (`parent_id`),
  CONSTRAINT `expense_categories_ibfk_1` FOREIGN KEY (`parent_id`) REFERENCES `expense_categories` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Išlaidų kategorijos';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `expense_categories`
--

LOCK TABLES `expense_categories` WRITE;
/*!40000 ALTER TABLE `expense_categories` DISABLE KEYS */;
set autocommit=0;
/*!40000 ALTER TABLE `expense_categories` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `expenses`
--

DROP TABLE IF EXISTS `expenses`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `expenses` (
  `id` int NOT NULL AUTO_INCREMENT,
  `expense_date` date NOT NULL,
  `category_id` int NOT NULL,
  `amount` decimal(10,2) NOT NULL,
  `vat_amount` decimal(10,2) DEFAULT '0.00',
  `description` text COLLATE utf8mb4_unicode_ci,
  `supplier_id` int DEFAULT NULL,
  `invoice_reference` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `notes` text COLLATE utf8mb4_unicode_ci,
  `created_by` int DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `created_by` (`created_by`),
  KEY `idx_expense_date` (`expense_date`),
  KEY `idx_category` (`category_id`),
  KEY `idx_supplier` (`supplier_id`),
  CONSTRAINT `expenses_ibfk_1` FOREIGN KEY (`category_id`) REFERENCES `expense_categories` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `expenses_ibfk_2` FOREIGN KEY (`supplier_id`) REFERENCES `business_partners` (`id`) ON DELETE SET NULL,
  CONSTRAINT `expenses_ibfk_3` FOREIGN KEY (`created_by`) REFERENCES `users` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Išlaidos';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `expenses`
--

LOCK TABLES `expenses` WRITE;
/*!40000 ALTER TABLE `expenses` DISABLE KEYS */;
set autocommit=0;
/*!40000 ALTER TABLE `expenses` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `honey_deliveries`
--

DROP TABLE IF EXISTS `honey_deliveries`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `honey_deliveries` (
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
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `honey_deliveries`
--

LOCK TABLES `honey_deliveries` WRITE;
/*!40000 ALTER TABLE `honey_deliveries` DISABLE KEYS */;
set autocommit=0;
/*!40000 ALTER TABLE `honey_deliveries` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `honey_types`
--

DROP TABLE IF EXISTS `honey_types`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `honey_types` (
  `id` int NOT NULL AUTO_INCREMENT,
  `code` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL,
  `name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `name_en` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `description` text COLLATE utf8mb4_unicode_ci,
  `is_active` tinyint(1) DEFAULT '1',
  `sort_order` int DEFAULT '0',
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `code` (`code`),
  KEY `idx_code` (`code`)
) ENGINE=InnoDB AUTO_INCREMENT=16 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Medaus rūšys (liepa, rapsas, ir t.t.)';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `honey_types`
--

LOCK TABLES `honey_types` WRITE;
/*!40000 ALTER TABLE `honey_types` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `honey_types` VALUES
(1,'SPO','Šviesus polyfloras','Bright polyflora',NULL,1,1,'2026-02-27 18:54:45','2026-02-27 16:57:01'),
(2,'TPO','Tamsus polyfloras',NULL,NULL,1,2,'2026-02-27 18:54:45','2026-02-27 18:54:45'),
(3,'C13','C13 polyfloras',NULL,NULL,1,3,'2026-02-27 18:54:45','2026-02-27 18:54:45'),
(4,'NMR','NMR polyfloras',NULL,NULL,1,4,'2026-02-27 18:54:45','2026-02-27 18:54:45'),
(5,'RAP','Rapsas','Rapeseed',NULL,1,5,'2026-02-27 18:54:45','2026-03-04 21:11:47'),
(6,'GRI','Grikiai',NULL,NULL,1,6,'2026-02-27 18:54:45','2026-02-27 18:54:45'),
(7,'VIR','Viržiai',NULL,NULL,1,7,'2026-02-27 18:54:45','2026-02-27 18:54:45'),
(8,'LIE','Liepos',NULL,NULL,1,8,'2026-02-27 18:54:45','2026-02-27 18:54:45'),
(9,'LIP','Lipčius',NULL,NULL,1,9,'2026-02-27 18:54:45','2026-02-27 18:54:45'),
(10,'AKA','Akacijos',NULL,NULL,1,10,'2026-02-27 18:54:45','2026-02-27 18:54:45');
/*!40000 ALTER TABLE `honey_types` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `invoice_lines`
--

DROP TABLE IF EXISTS `invoice_lines`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `invoice_lines` (
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
) ENGINE=InnoDB AUTO_INCREMENT=31 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Sąskaitų eilutės su LOT traceability';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `invoice_lines`
--

LOCK TABLES `invoice_lines` WRITE;
/*!40000 ALTER TABLE `invoice_lines` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `invoice_lines` VALUES
(1,3,1,1,'dasdas','dasda',1.000,'vnt',0.0100,21.00,0.01,0.00,0.01,NULL,NULL,NULL,'2026-02-23 05:18:34','2026-02-23 05:18:34'),
(2,4,1,1,'dasdas','dasda',1.000,'vnt',0.0200,21.00,0.02,0.00,0.02,NULL,NULL,NULL,'2026-02-23 05:34:34','2026-02-23 05:34:34'),
(3,5,1,1,'dasdas','dasda',1.000,'vnt',0.0400,6.00,0.04,0.00,0.04,NULL,NULL,NULL,'2026-02-26 16:47:29','2026-02-26 14:47:29'),
(4,5,2,1,'dasdas','dasda',1.000,'vnt',0.0000,21.00,0.00,0.00,0.00,NULL,NULL,NULL,'2026-02-26 16:47:29','2026-02-26 14:47:29'),
(5,5,3,1,'dasdas','dasda',1.000,'vnt',123.0000,21.00,123.00,25.83,148.83,NULL,NULL,NULL,'2026-02-26 16:47:29','2026-02-26 14:47:29'),
(6,6,1,1,'dasdas','dasda',1.000,'vnt',0.0400,6.00,0.04,0.00,0.04,NULL,NULL,NULL,'2026-02-26 14:52:54','2026-02-26 14:52:54'),
(7,6,2,1,'dasdas','dasda',1.000,'vnt',0.0000,21.00,0.00,0.00,0.00,NULL,NULL,NULL,'2026-02-26 14:52:54','2026-02-26 14:52:54'),
(8,6,3,1,'dasdas','dasda',1.000,'vnt',123.0000,21.00,123.00,25.83,148.83,NULL,NULL,NULL,'2026-02-26 14:52:54','2026-02-26 14:52:54'),
(9,7,1,1,'dasdas','dasda',1.000,'vnt',0.0400,6.00,0.04,0.00,0.04,NULL,NULL,NULL,'2026-03-09 14:03:04','2026-03-09 12:03:04'),
(10,7,2,1,'dasdas','dasda',1.000,'vnt',0.0000,21.00,0.00,0.00,0.00,NULL,NULL,NULL,'2026-03-09 14:03:04','2026-03-09 12:03:04'),
(11,7,3,1,'dasdas','dasda',1.000,'vnt',123.0000,21.00,123.00,25.83,148.83,NULL,NULL,NULL,'2026-03-09 14:03:04','2026-03-09 12:03:04'),
(12,8,1,1,'dasdas','dasda',1.000,'vnt',0.0400,6.00,0.04,0.00,0.04,NULL,NULL,NULL,'2026-02-26 14:52:56','2026-02-26 14:52:56'),
(13,8,2,1,'dasdas','dasda',1.000,'vnt',0.0000,21.00,0.00,0.00,0.00,NULL,NULL,NULL,'2026-02-26 14:52:56','2026-02-26 14:52:56'),
(14,8,3,1,'dasdas','dasda',1.000,'vnt',123.0000,21.00,123.00,25.83,148.83,NULL,NULL,NULL,'2026-02-26 14:52:56','2026-02-26 14:52:56'),
(15,9,1,1,'dasdas','dasda',1.000,'vnt',0.0400,6.00,0.04,0.00,0.04,NULL,NULL,NULL,'2026-02-26 14:52:56','2026-02-26 14:52:56'),
(16,9,2,1,'dasdas','dasda',1.000,'vnt',0.0000,21.00,0.00,0.00,0.00,NULL,NULL,NULL,'2026-02-26 14:52:56','2026-02-26 14:52:56'),
(17,9,3,1,'dasdas','dasda',1.000,'vnt',123.0000,21.00,123.00,25.83,148.83,NULL,NULL,NULL,'2026-02-26 14:52:56','2026-02-26 14:52:56'),
(18,10,1,1,'dasdas','dasda',1.000,'vnt',0.0400,6.00,0.04,0.00,0.04,NULL,NULL,NULL,'2026-02-26 14:58:05','2026-02-26 14:58:05'),
(19,10,2,1,'dasdas','dasda',1.000,'vnt',0.0000,21.00,0.00,0.00,0.00,NULL,NULL,NULL,'2026-02-26 14:58:05','2026-02-26 14:58:05'),
(20,10,3,1,'dasdas','dasda',1.000,'vnt',123.0000,21.00,123.00,25.83,148.83,NULL,NULL,NULL,'2026-02-26 14:58:05','2026-02-26 14:58:05'),
(22,11,1,1,'dasdas','PAts skanaiusias medus',1.000,'vnt',21445.0000,21.00,21445.00,4503.45,25948.45,NULL,NULL,NULL,'2026-02-26 17:14:18','2026-02-26 15:14:18'),
(24,11,2,1,'dasdas','Nevisai skanus medus su grikiaisąčęčęėęčėNevisai skanus medus su grikiaisąčęčęėęčėNevisai skanus medus su grikiaisąčęčęėęčėdasddasdasdsadasdsaddsadasdasdasd dasdsaasdads dasdsadsa',1.000,'vnt',0.0000,21.00,0.00,0.00,0.00,NULL,NULL,NULL,'2026-02-26 17:14:18','2026-02-26 15:14:18'),
(25,12,1,1,'dasdas','dasda',1.000,'vnt',0.0200,6.00,0.02,0.00,0.02,NULL,NULL,NULL,'2026-02-26 19:42:26','2026-02-26 17:42:26'),
(26,12,2,1,'dasdas','dasda',1.000,'vnt',25.0000,21.00,25.00,5.25,30.25,NULL,NULL,NULL,'2026-02-26 19:42:26','2026-02-26 17:42:26'),
(27,13,1,1,'dasdas','dasda',1.000,'vnt',0.0200,6.00,0.02,0.00,0.02,NULL,NULL,NULL,'2026-03-04 23:16:47','2026-03-04 21:16:48'),
(28,13,2,1,'dasdas','dasda',1.000,'vnt',25.0000,6.00,25.00,1.50,26.50,NULL,NULL,NULL,'2026-03-04 23:16:47','2026-03-04 21:16:48'),
(29,13,3,1,'dasdas','hgjhggjhgjhgjhgj',1.000,'vnt',0.0000,6.00,0.00,0.00,0.00,NULL,NULL,NULL,'2026-03-04 23:16:47','2026-03-04 21:16:48'),
(30,7,4,1,'dasdas','dasda',1.005,'vnt',0.0800,21.00,0.08,0.02,0.10,NULL,NULL,NULL,'2026-03-09 12:03:04','2026-03-09 12:03:04');
/*!40000 ALTER TABLE `invoice_lines` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `invoices`
--

DROP TABLE IF EXISTS `invoices`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `invoices` (
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
  `status` enum('draft','issued','paid','cancelled') COLLATE utf8mb4_unicode_ci DEFAULT 'draft',
  `notes` text COLLATE utf8mb4_unicode_ci,
  `created_by` int DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `payment_term_id` int DEFAULT NULL,
  `creator_id` int DEFAULT NULL,
  `due_date` date DEFAULT NULL,
  `PaymentTermId1` int DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `invoice_number` (`invoice_number`),
  KEY `created_by` (`created_by`),
  KEY `idx_invoice_number` (`invoice_number`),
  KEY `idx_invoice_date` (`invoice_date`),
  KEY `idx_customer` (`customer_id`),
  KEY `idx_status` (`status`),
  CONSTRAINT `invoices_ibfk_1` FOREIGN KEY (`customer_id`) REFERENCES `business_partners` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `invoices_ibfk_2` FOREIGN KEY (`created_by`) REFERENCES `users` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB AUTO_INCREMENT=14 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Pardavimo sąskaitos faktūros';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `invoices`
--

LOCK TABLES `invoices` WRITE;
/*!40000 ALTER TABLE `invoices` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `invoices` VALUES
(3,'LAK26001','2026-02-23',2,NULL,'2026-03-02',7,'lt','PVM SĄSKAITA FAKTŪRA',0,0.01,0.00,0.01,NULL,NULL,NULL,'draft',NULL,NULL,'2026-02-23 05:18:34','2026-02-23 05:18:34',NULL,NULL,NULL,NULL),
(4,'LAK26002','2026-02-23',1,NULL,'2026-03-02',7,'lt','PVM SĄSKAITA FAKTŪRA',0,0.02,0.00,0.02,NULL,NULL,NULL,'draft',NULL,NULL,'2026-02-23 05:34:34','2026-02-23 05:34:34',NULL,NULL,NULL,NULL),
(5,'ULAK26001','2026-02-24',3,NULL,'2026-03-17',21,'EN','6% PVM SĄSKAITA FAKTŪRA',0,123.04,25.83,148.87,NULL,NULL,NULL,'issued',NULL,NULL,'2026-02-23 20:24:12','2026-02-26 14:47:29',NULL,NULL,NULL,NULL),
(6,'ULAK26002','2026-02-26',3,NULL,'2026-03-19',21,'EN','6% PVM SĄSKAITA FAKTŪRA',0,123.04,25.83,148.87,NULL,NULL,NULL,'draft',NULL,NULL,'2026-02-26 14:52:54','2026-02-26 14:52:54',NULL,NULL,NULL,NULL),
(7,'ULAK26003','2026-02-26',3,NULL,'2026-03-19',21,'EN','6% PVM SĄSKAITA FAKTŪRA',0,123.12,25.85,148.97,NULL,NULL,NULL,'draft',NULL,NULL,'2026-02-26 14:52:55','2026-03-09 12:03:04',NULL,NULL,NULL,NULL),
(8,'ULAK26004','2026-02-26',3,NULL,'2026-03-19',21,'EN','6% PVM SĄSKAITA FAKTŪRA',0,123.04,25.83,148.87,NULL,NULL,NULL,'draft',NULL,NULL,'2026-02-26 14:52:56','2026-02-26 14:52:56',NULL,NULL,NULL,NULL),
(9,'ULAK26005','2026-02-26',3,NULL,'2026-03-19',21,'EN','6% PVM SĄSKAITA FAKTŪRA',0,123.04,25.83,148.87,NULL,NULL,NULL,'draft',NULL,NULL,'2026-02-26 14:52:56','2026-02-26 14:52:56',NULL,NULL,NULL,NULL),
(10,'ULAK26006','2026-02-26',3,NULL,'2026-03-19',21,'EN','6% PVM SĄSKAITA FAKTŪRA',0,123.04,25.83,148.87,NULL,NULL,NULL,'draft',NULL,NULL,'2026-02-26 14:58:05','2026-02-26 14:58:05',NULL,NULL,NULL,NULL),
(11,'ULAK26007','2026-02-26',3,NULL,'2026-03-19',21,'EN','6% PVM SĄSKAITA FAKTŪRA',0,21445.00,4503.45,25948.45,NULL,NULL,NULL,'draft',NULL,NULL,'2026-02-26 15:03:23','2026-02-26 15:14:18',NULL,NULL,NULL,NULL),
(12,'LAK26003','2026-02-26',1,NULL,'2026-03-12',14,'LT','6% PVM SĄSKAITA FAKTŪRA',0,25.02,5.25,30.27,NULL,NULL,NULL,'issued',NULL,NULL,'2026-02-26 15:17:27','2026-02-26 17:42:26',NULL,NULL,NULL,NULL),
(13,'ULAK26008','2026-02-28',1,NULL,'2026-03-14',14,'LT','6% PVM SĄSKAITA FAKTŪRA',0,25.02,1.50,26.52,NULL,NULL,NULL,'issued',NULL,NULL,'2026-02-28 08:40:10','2026-03-04 21:16:48',NULL,NULL,NULL,NULL);
/*!40000 ALTER TABLE `invoices` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `lots`
--

DROP TABLE IF EXISTS `lots`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `lots` (
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
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `lots`
--

LOCK TABLES `lots` WRITE;
/*!40000 ALTER TABLE `lots` DISABLE KEYS */;
set autocommit=0;
/*!40000 ALTER TABLE `lots` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `order_lines`
--

DROP TABLE IF EXISTS `order_lines`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `order_lines` (
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
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `order_lines`
--

LOCK TABLES `order_lines` WRITE;
/*!40000 ALTER TABLE `order_lines` DISABLE KEYS */;
set autocommit=0;
/*!40000 ALTER TABLE `order_lines` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `orders`
--

DROP TABLE IF EXISTS `orders`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `orders` (
  `id` int NOT NULL AUTO_INCREMENT,
  `order_number` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `order_date` date NOT NULL,
  `customer_id` int NOT NULL,
  `delivery_date` date DEFAULT NULL,
  `status` enum('draft','confirmed','in_production','shipped','delivered','cancelled') COLLATE utf8mb4_unicode_ci DEFAULT 'draft',
  `notes` text COLLATE utf8mb4_unicode_ci,
  `created_by` int DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `order_number` (`order_number`),
  KEY `created_by` (`created_by`),
  KEY `idx_order_number` (`order_number`),
  KEY `idx_customer` (`customer_id`),
  KEY `idx_status` (`status`),
  CONSTRAINT `orders_ibfk_1` FOREIGN KEY (`customer_id`) REFERENCES `business_partners` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `orders_ibfk_2` FOREIGN KEY (`created_by`) REFERENCES `users` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Užsakymai - future integration';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `orders`
--

LOCK TABLES `orders` WRITE;
/*!40000 ALTER TABLE `orders` DISABLE KEYS */;
set autocommit=0;
/*!40000 ALTER TABLE `orders` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `payments`
--

DROP TABLE IF EXISTS `payments`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `payments` (
  `id` int NOT NULL AUTO_INCREMENT,
  `payment_date` date NOT NULL,
  `invoice_id` int DEFAULT NULL,
  `customer_id` int NOT NULL,
  `amount` decimal(10,2) NOT NULL,
  `payment_method` enum('bank_transfer','cash','card','other') COLLATE utf8mb4_unicode_ci DEFAULT 'bank_transfer',
  `reference_number` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `notes` text COLLATE utf8mb4_unicode_ci,
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `idx_payment_date` (`payment_date`),
  KEY `idx_invoice` (`invoice_id`),
  KEY `idx_customer` (`customer_id`),
  CONSTRAINT `payments_ibfk_1` FOREIGN KEY (`invoice_id`) REFERENCES `invoices` (`id`) ON DELETE SET NULL,
  CONSTRAINT `payments_ibfk_2` FOREIGN KEY (`customer_id`) REFERENCES `business_partners` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Mokėjimai - banko integracijos paruošimas';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `payments`
--

LOCK TABLES `payments` WRITE;
/*!40000 ALTER TABLE `payments` DISABLE KEYS */;
set autocommit=0;
/*!40000 ALTER TABLE `payments` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `product_categories`
--

DROP TABLE IF EXISTS `product_categories`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `product_categories` (
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
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `product_categories`
--

LOCK TABLES `product_categories` WRITE;
/*!40000 ALTER TABLE `product_categories` DISABLE KEYS */;
set autocommit=0;
/*!40000 ALTER TABLE `product_categories` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `production_batch_ingredients`
--

DROP TABLE IF EXISTS `production_batch_ingredients`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `production_batch_ingredients` (
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
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `production_batch_ingredients`
--

LOCK TABLES `production_batch_ingredients` WRITE;
/*!40000 ALTER TABLE `production_batch_ingredients` DISABLE KEYS */;
set autocommit=0;
/*!40000 ALTER TABLE `production_batch_ingredients` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `production_batches`
--

DROP TABLE IF EXISTS `production_batches`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `production_batches` (
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
  `created_by` int DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `lot_number` (`lot_number`),
  KEY `warehouse_id` (`warehouse_id`),
  KEY `created_by` (`created_by`),
  KEY `idx_lot_number` (`lot_number`),
  KEY `idx_product` (`product_id`),
  KEY `idx_batch_date` (`batch_date`),
  KEY `idx_status` (`status`),
  CONSTRAINT `production_batches_ibfk_1` FOREIGN KEY (`product_id`) REFERENCES `products` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `production_batches_ibfk_2` FOREIGN KEY (`warehouse_id`) REFERENCES `warehouses` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `production_batches_ibfk_3` FOREIGN KEY (`created_by`) REFERENCES `users` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Gamybos partijos - LOT valdymas ir traceability';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `production_batches`
--

LOCK TABLES `production_batches` WRITE;
/*!40000 ALTER TABLE `production_batches` DISABLE KEYS */;
set autocommit=0;
/*!40000 ALTER TABLE `production_batches` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `products`
--

DROP TABLE IF EXISTS `products`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `products` (
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
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Produktų katalogas - žaliavos, pakuotės, gatavi produktai';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `products`
--

LOCK TABLES `products` WRITE;
/*!40000 ALTER TABLE `products` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `products` VALUES
(1,'dasdas','dasda',NULL,'FinishedGood',NULL,NULL,'vnt',0.00,0.00,0.00,0,0,0.00,NULL,NULL,1,'2026-02-19 21:45:26','2026-02-19 21:45:26');
/*!40000 ALTER TABLE `products` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `raw_material_types`
--

DROP TABLE IF EXISTS `raw_material_types`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `raw_material_types` (
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
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `raw_material_types`
--

LOCK TABLES `raw_material_types` WRITE;
/*!40000 ALTER TABLE `raw_material_types` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `raw_material_types` VALUES
(1,'Medus','MD',1,1,1,'2026-03-04 17:39:35','2026-03-04 17:39:35'),
(2,'Bičių duona','BD',0,1,2,'2026-03-04 17:39:35','2026-03-14 00:18:46'),
(3,'Pikis','PK',0,1,3,'2026-03-04 17:39:35','2026-03-14 00:18:46'),
(4,'Propolis','PR',0,1,4,'2026-03-04 17:39:35','2026-03-14 00:18:47'),
(5,'Vaškas','VS',0,1,5,'2026-03-04 17:39:35','2026-03-14 00:18:48'),
(6,'Bičių duonos žaliava',NULL,0,1,6,'2026-03-04 22:39:13','2026-03-14 00:18:50');
/*!40000 ALTER TABLE `raw_material_types` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `stock_movements`
--

DROP TABLE IF EXISTS `stock_movements`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `stock_movements` (
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
) ENGINE=InnoDB AUTO_INCREMENT=185 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `stock_movements`
--

LOCK TABLES `stock_movements` WRITE;
/*!40000 ALTER TABLE `stock_movements` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `stock_movements` VALUES
(1,NULL,NULL,2.0000,'IN','Delivery',28,NULL,'2026-03-03 19:16:50',17,NULL,NULL,1,NULL,NULL),
(2,NULL,NULL,2.0000,'IN','Delivery',28,NULL,'2026-03-03 19:16:50',18,NULL,NULL,1,NULL,NULL),
(3,NULL,NULL,2.0000,'IN','Delivery',28,NULL,'2026-03-03 19:16:50',19,NULL,NULL,1,NULL,NULL),
(4,NULL,NULL,290.0000,'IN','Delivery',32,NULL,'2026-03-03 19:24:49',20,NULL,NULL,1,NULL,NULL),
(5,NULL,NULL,290.0000,'IN','Delivery',32,NULL,'2026-03-03 19:24:49',21,NULL,NULL,1,NULL,NULL),
(6,NULL,NULL,290.0000,'IN','Delivery',32,NULL,'2026-03-03 19:24:49',22,NULL,NULL,1,NULL,NULL),
(7,NULL,NULL,290.0000,'IN','Delivery',32,NULL,'2026-03-03 19:24:49',23,NULL,NULL,1,NULL,NULL),
(8,NULL,NULL,290.0000,'IN','Delivery',33,NULL,'2026-03-03 19:46:30',24,NULL,NULL,1,NULL,NULL),
(9,NULL,NULL,290.0000,'IN','Delivery',33,NULL,'2026-03-03 19:46:30',25,NULL,NULL,1,NULL,NULL),
(10,NULL,NULL,290.0000,'IN','Delivery',33,NULL,'2026-03-03 19:46:30',26,NULL,NULL,1,NULL,NULL),
(11,NULL,NULL,290.0000,'IN','Delivery',33,NULL,'2026-03-03 19:46:30',27,NULL,NULL,1,NULL,NULL),
(12,NULL,NULL,290.0000,'IN','Delivery',33,NULL,'2026-03-03 19:46:30',28,NULL,NULL,1,NULL,NULL),
(13,NULL,NULL,290.0000,'IN','Delivery',36,NULL,'2026-03-04 17:35:54',29,NULL,NULL,1,NULL,NULL),
(14,NULL,NULL,290.0000,'IN','Delivery',36,NULL,'2026-03-04 17:35:54',30,NULL,NULL,1,NULL,NULL),
(15,NULL,NULL,290.0000,'IN','Delivery',36,NULL,'2026-03-04 17:35:54',31,NULL,NULL,1,NULL,NULL),
(16,NULL,NULL,290.0000,'IN','Delivery',36,NULL,'2026-03-04 17:35:54',32,NULL,NULL,1,NULL,NULL),
(17,NULL,NULL,290.0000,'IN','Delivery',36,NULL,'2026-03-04 17:35:54',33,NULL,NULL,1,NULL,NULL),
(18,NULL,NULL,290.0000,'IN','Delivery',36,NULL,'2026-03-04 17:35:54',34,NULL,NULL,1,NULL,NULL),
(19,NULL,NULL,290.0000,'IN','Delivery',36,NULL,'2026-03-04 17:35:54',35,NULL,NULL,1,NULL,NULL),
(20,NULL,NULL,290.0000,'IN','Delivery',36,NULL,'2026-03-04 17:35:54',36,NULL,NULL,1,NULL,NULL),
(21,NULL,NULL,290.0000,'IN','Delivery',36,NULL,'2026-03-04 17:35:54',37,NULL,NULL,1,NULL,NULL),
(22,NULL,NULL,290.0000,'IN','Delivery',36,NULL,'2026-03-04 17:35:54',38,NULL,NULL,1,NULL,NULL),
(23,NULL,NULL,19.6000,'IN','Delivery',37,NULL,'2026-03-04 20:54:36',39,NULL,NULL,1,NULL,NULL),
(24,NULL,NULL,293.0000,'IN','Delivery',38,NULL,'2026-03-04 21:06:54',40,NULL,NULL,1,NULL,NULL),
(25,NULL,NULL,291.0000,'IN','Delivery',38,NULL,'2026-03-04 21:06:54',41,NULL,NULL,1,NULL,NULL),
(26,NULL,NULL,0.0000,'OUT','Manual',NULL,NULL,'2026-03-08 18:26:09',32,NULL,1,NULL,NULL,'Netinkama'),
(27,NULL,NULL,0.4000,'IN','Delivery',39,NULL,'2026-03-08 21:22:42',42,NULL,NULL,1,NULL,NULL),
(28,NULL,NULL,15.6000,'IN','Delivery',40,NULL,'2026-03-08 21:47:17',43,NULL,NULL,1,NULL,NULL),
(29,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:36:10',29,NULL,1,2,NULL,'PK-2603-001'),
(30,NULL,NULL,2.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:36:56',17,NULL,1,2,NULL,'PK-2603-002'),
(31,NULL,NULL,2.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:36:56',18,NULL,1,2,NULL,'PK-2603-002'),
(32,NULL,NULL,2.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:36:56',19,NULL,1,2,NULL,'PK-2603-002'),
(33,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:36:56',20,NULL,1,2,NULL,'PK-2603-002'),
(34,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:36:56',21,NULL,1,2,NULL,'PK-2603-002'),
(35,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:36:56',22,NULL,1,2,NULL,'PK-2603-002'),
(36,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:36:56',23,NULL,1,2,NULL,'PK-2603-002'),
(37,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:36:56',24,NULL,1,2,NULL,'PK-2603-002'),
(38,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:36:56',25,NULL,1,2,NULL,'PK-2603-002'),
(39,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:36:56',26,NULL,1,2,NULL,'PK-2603-002'),
(40,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:36:56',27,NULL,1,2,NULL,'PK-2603-002'),
(41,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:36:56',28,NULL,1,2,NULL,'PK-2603-002'),
(42,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:36:56',29,NULL,1,2,NULL,'PK-2603-002'),
(43,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:36:56',30,NULL,1,2,NULL,'PK-2603-002'),
(44,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:36:56',31,NULL,1,2,NULL,'PK-2603-002'),
(45,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:36:56',32,NULL,1,2,NULL,'PK-2603-002'),
(46,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:36:56',33,NULL,1,2,NULL,'PK-2603-002'),
(47,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:36:56',34,NULL,1,2,NULL,'PK-2603-002'),
(48,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:36:56',35,NULL,1,2,NULL,'PK-2603-002'),
(49,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:36:56',36,NULL,1,2,NULL,'PK-2603-002'),
(50,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:36:56',37,NULL,1,2,NULL,'PK-2603-002'),
(51,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:36:56',38,NULL,1,2,NULL,'PK-2603-002'),
(52,NULL,NULL,19.6000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:36:56',39,NULL,1,2,NULL,'PK-2603-002'),
(53,NULL,NULL,293.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:36:56',40,NULL,1,2,NULL,'PK-2603-002'),
(54,NULL,NULL,291.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:36:56',41,NULL,1,2,NULL,'PK-2603-002'),
(55,NULL,NULL,0.4000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:36:56',42,NULL,1,2,NULL,'PK-2603-002'),
(56,NULL,NULL,15.6000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:36:56',43,NULL,1,2,NULL,'PK-2603-002'),
(57,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:37:30',29,NULL,1,2,NULL,'PK-2603-003'),
(58,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:37:30',30,NULL,1,2,NULL,'PK-2603-003'),
(59,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:37:30',31,NULL,1,2,NULL,'PK-2603-003'),
(60,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:37:30',32,NULL,1,2,NULL,'PK-2603-003'),
(61,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:37:30',33,NULL,1,2,NULL,'PK-2603-003'),
(62,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:37:30',34,NULL,1,2,NULL,'PK-2603-003'),
(63,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:37:30',35,NULL,1,2,NULL,'PK-2603-003'),
(64,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:37:30',36,NULL,1,2,NULL,'PK-2603-003'),
(65,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:37:30',37,NULL,1,2,NULL,'PK-2603-003'),
(66,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:37:30',38,NULL,1,2,NULL,'PK-2603-003'),
(67,NULL,NULL,19.6000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:37:30',39,NULL,1,2,NULL,'PK-2603-003'),
(68,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:38:45',29,NULL,1,2,NULL,'PK-2603-004'),
(69,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:38:45',30,NULL,1,2,NULL,'PK-2603-004'),
(70,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:38:45',31,NULL,1,2,NULL,'PK-2603-004'),
(71,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:38:45',32,NULL,1,2,NULL,'PK-2603-004'),
(72,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:38:45',33,NULL,1,2,NULL,'PK-2603-004'),
(73,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:38:45',34,NULL,1,2,NULL,'PK-2603-004'),
(74,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:38:45',35,NULL,1,2,NULL,'PK-2603-004'),
(75,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:38:45',36,NULL,1,2,NULL,'PK-2603-004'),
(76,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:38:45',37,NULL,1,2,NULL,'PK-2603-004'),
(77,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:38:45',38,NULL,1,2,NULL,'PK-2603-004'),
(78,NULL,NULL,19.6000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 06:38:45',39,NULL,1,2,NULL,'PK-2603-004'),
(79,NULL,NULL,293.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 07:23:51',40,NULL,1,2,NULL,'PK-2603-005'),
(80,NULL,NULL,291.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 07:23:51',41,NULL,1,2,NULL,'PK-2603-005'),
(81,NULL,NULL,293.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 07:31:37',40,NULL,1,2,NULL,'PK-2603-006'),
(82,NULL,NULL,291.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 07:31:37',41,NULL,1,2,NULL,'PK-2603-006'),
(83,NULL,NULL,293.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 07:43:34',40,NULL,1,2,NULL,'PK-2603-007'),
(84,NULL,NULL,291.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-09 07:43:34',41,NULL,1,2,NULL,'PK-2603-007'),
(85,NULL,NULL,19.9500,'IN','Delivery',41,NULL,'2026-03-09 11:16:14',44,NULL,NULL,1,NULL,NULL),
(86,NULL,NULL,19.9500,'IN','Delivery',41,NULL,'2026-03-09 11:16:14',45,NULL,NULL,1,NULL,NULL),
(87,NULL,NULL,286.0000,'IN','Delivery',42,NULL,'2026-03-09 13:40:29',46,NULL,NULL,1,NULL,NULL),
(88,NULL,NULL,286.0000,'IN','Delivery',42,NULL,'2026-03-09 13:40:29',47,NULL,NULL,1,NULL,NULL),
(89,NULL,NULL,286.0000,'IN','Delivery',42,NULL,'2026-03-09 13:40:29',48,NULL,NULL,1,NULL,NULL),
(90,NULL,NULL,286.0000,'IN','Delivery',42,NULL,'2026-03-09 13:40:29',49,NULL,NULL,1,NULL,NULL),
(91,NULL,NULL,286.0000,'IN','Delivery',42,NULL,'2026-03-09 13:40:29',50,NULL,NULL,1,NULL,NULL),
(92,NULL,NULL,281.0000,'IN','Delivery',43,NULL,'2026-03-09 13:44:50',51,NULL,NULL,1,NULL,NULL),
(93,NULL,NULL,271.0000,'IN','Delivery',43,NULL,'2026-03-09 13:44:50',52,NULL,NULL,1,NULL,NULL),
(94,NULL,NULL,281.0000,'IN','Delivery',44,NULL,'2026-03-09 13:45:28',53,NULL,NULL,1,NULL,NULL),
(95,NULL,NULL,281.0000,'IN','Delivery',44,NULL,'2026-03-09 13:45:28',54,NULL,NULL,1,NULL,NULL),
(96,NULL,NULL,281.0000,'IN','Delivery',44,NULL,'2026-03-09 13:45:28',55,NULL,NULL,1,NULL,NULL),
(97,NULL,NULL,14.6000,'IN','Delivery',45,NULL,'2026-03-09 13:46:59',56,NULL,NULL,1,NULL,NULL),
(98,NULL,NULL,14.6000,'IN','Delivery',45,NULL,'2026-03-09 13:46:59',57,NULL,NULL,1,NULL,NULL),
(99,NULL,NULL,14.6000,'IN','Delivery',45,NULL,'2026-03-09 13:46:59',58,NULL,NULL,1,NULL,NULL),
(100,NULL,NULL,14.6000,'IN','Delivery',45,NULL,'2026-03-09 13:46:59',59,NULL,NULL,1,NULL,NULL),
(101,NULL,NULL,14.6000,'IN','Delivery',45,NULL,'2026-03-09 13:46:59',60,NULL,NULL,1,NULL,NULL),
(102,NULL,NULL,290.0000,'IN','Delivery',46,NULL,'2026-03-10 07:00:22',61,NULL,NULL,1,NULL,NULL),
(103,NULL,NULL,290.0000,'IN','Delivery',46,NULL,'2026-03-10 07:00:22',62,NULL,NULL,1,NULL,NULL),
(104,NULL,NULL,290.0000,'IN','Delivery',46,NULL,'2026-03-10 07:00:22',63,NULL,NULL,1,NULL,NULL),
(105,NULL,NULL,290.0000,'IN','Delivery',46,NULL,'2026-03-10 07:00:22',64,NULL,NULL,1,NULL,NULL),
(106,NULL,NULL,290.0000,'IN','Delivery',46,NULL,'2026-03-10 07:00:22',65,NULL,NULL,1,NULL,NULL),
(107,NULL,NULL,290.0000,'IN','Delivery',46,NULL,'2026-03-10 07:00:22',66,NULL,NULL,1,NULL,NULL),
(108,NULL,NULL,290.0000,'IN','Delivery',46,NULL,'2026-03-10 07:00:22',67,NULL,NULL,1,NULL,NULL),
(109,NULL,NULL,290.0000,'IN','Delivery',46,NULL,'2026-03-10 07:00:22',68,NULL,NULL,1,NULL,NULL),
(110,NULL,NULL,290.0000,'IN','Delivery',46,NULL,'2026-03-10 07:00:22',69,NULL,NULL,1,NULL,NULL),
(111,NULL,NULL,290.0000,'IN','Delivery',46,NULL,'2026-03-10 07:00:22',70,NULL,NULL,1,NULL,NULL),
(112,NULL,NULL,290.0000,'IN','Delivery',46,NULL,'2026-03-10 07:00:22',71,NULL,NULL,1,NULL,NULL),
(113,NULL,NULL,290.0000,'IN','Delivery',46,NULL,'2026-03-10 07:00:22',72,NULL,NULL,1,NULL,NULL),
(114,NULL,NULL,290.0000,'IN','Delivery',46,NULL,'2026-03-10 07:00:22',73,NULL,NULL,1,NULL,NULL),
(115,NULL,NULL,290.0000,'IN','Delivery',46,NULL,'2026-03-10 07:00:22',74,NULL,NULL,1,NULL,NULL),
(116,NULL,NULL,290.0000,'IN','Delivery',46,NULL,'2026-03-10 07:00:22',75,NULL,NULL,1,NULL,NULL),
(117,NULL,NULL,290.0000,'IN','Delivery',46,NULL,'2026-03-10 07:00:22',76,NULL,NULL,1,NULL,NULL),
(118,NULL,NULL,290.0000,'IN','Delivery',46,NULL,'2026-03-10 07:00:22',77,NULL,NULL,1,NULL,NULL),
(119,NULL,NULL,290.0000,'IN','Delivery',46,NULL,'2026-03-10 07:00:22',78,NULL,NULL,1,NULL,NULL),
(120,NULL,NULL,290.0000,'IN','Delivery',46,NULL,'2026-03-10 07:00:22',79,NULL,NULL,1,NULL,NULL),
(121,NULL,NULL,290.0000,'IN','Delivery',46,NULL,'2026-03-10 07:00:22',80,NULL,NULL,1,NULL,NULL),
(122,NULL,NULL,290.0000,'IN','Delivery',46,NULL,'2026-03-10 07:00:22',81,NULL,NULL,1,NULL,NULL),
(123,NULL,NULL,281.0000,'IN','Delivery',47,NULL,'2026-03-10 07:03:18',82,NULL,NULL,1,NULL,NULL),
(124,NULL,NULL,286.0000,'IN','Delivery',47,NULL,'2026-03-10 07:03:18',83,NULL,NULL,1,NULL,NULL),
(125,NULL,NULL,289.0000,'IN','Delivery',47,NULL,'2026-03-10 07:03:18',84,NULL,NULL,1,NULL,NULL),
(126,NULL,NULL,284.0000,'IN','Delivery',47,NULL,'2026-03-10 07:03:18',85,NULL,NULL,1,NULL,NULL),
(127,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-10 07:04:22',61,NULL,1,2,NULL,'PK-2603-008'),
(128,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-10 07:04:22',62,NULL,1,2,NULL,'PK-2603-008'),
(129,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-10 07:04:22',63,NULL,1,2,NULL,'PK-2603-008'),
(130,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-10 07:04:22',64,NULL,1,2,NULL,'PK-2603-008'),
(131,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-10 07:04:22',65,NULL,1,2,NULL,'PK-2603-008'),
(132,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-10 07:04:22',66,NULL,1,2,NULL,'PK-2603-008'),
(133,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-10 07:04:22',67,NULL,1,2,NULL,'PK-2603-008'),
(134,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-10 07:04:22',68,NULL,1,2,NULL,'PK-2603-008'),
(135,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-10 07:04:22',69,NULL,1,2,NULL,'PK-2603-008'),
(136,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-10 07:04:22',70,NULL,1,2,NULL,'PK-2603-008'),
(137,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-10 07:04:22',71,NULL,1,2,NULL,'PK-2603-008'),
(138,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-10 07:04:22',72,NULL,1,2,NULL,'PK-2603-008'),
(139,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-10 07:04:22',73,NULL,1,2,NULL,'PK-2603-008'),
(140,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-10 07:04:22',74,NULL,1,2,NULL,'PK-2603-008'),
(141,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-10 07:04:22',75,NULL,1,2,NULL,'PK-2603-008'),
(142,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-10 07:04:22',76,NULL,1,2,NULL,'PK-2603-008'),
(143,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-10 07:04:22',77,NULL,1,2,NULL,'PK-2603-008'),
(144,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-10 07:04:22',78,NULL,1,2,NULL,'PK-2603-008'),
(145,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-10 07:04:22',79,NULL,1,2,NULL,'PK-2603-008'),
(146,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-10 07:04:22',80,NULL,1,2,NULL,'PK-2603-008'),
(147,NULL,NULL,290.0000,'TRANSFER','Transfer',NULL,NULL,'2026-03-10 07:04:22',81,NULL,1,2,NULL,'PK-2603-008'),
(148,NULL,NULL,28.6000,'IN','Delivery',48,NULL,'2026-03-10 07:09:25',86,NULL,NULL,1,NULL,NULL),
(149,NULL,NULL,14.6000,'IN','Delivery',48,NULL,'2026-03-10 07:09:25',87,NULL,NULL,1,NULL,NULL),
(150,NULL,NULL,14.6000,'IN','Delivery',48,NULL,'2026-03-10 07:09:25',88,NULL,NULL,1,NULL,NULL),
(151,NULL,NULL,14.6000,'IN','Delivery',48,NULL,'2026-03-10 07:09:25',89,NULL,NULL,1,NULL,NULL),
(152,NULL,NULL,14.6000,'IN','Delivery',48,NULL,'2026-03-10 07:09:25',90,NULL,NULL,1,NULL,NULL),
(153,NULL,NULL,14.6000,'IN','Delivery',48,NULL,'2026-03-10 07:09:25',91,NULL,NULL,1,NULL,NULL),
(154,NULL,NULL,14.6000,'IN','Delivery',48,NULL,'2026-03-10 07:09:25',92,NULL,NULL,1,NULL,NULL),
(155,NULL,NULL,14.6000,'IN','Delivery',48,NULL,'2026-03-10 07:09:25',93,NULL,NULL,1,NULL,NULL),
(156,NULL,NULL,286.0000,'IN','Delivery',49,NULL,'2026-03-10 20:50:35',94,NULL,NULL,1,NULL,NULL),
(157,NULL,NULL,286.0000,'IN','Delivery',49,NULL,'2026-03-10 20:50:35',95,NULL,NULL,1,NULL,NULL),
(158,NULL,NULL,286.0000,'IN','Delivery',49,NULL,'2026-03-10 20:50:35',96,NULL,NULL,1,NULL,NULL),
(159,NULL,NULL,5.9500,'IN','Delivery',50,NULL,'2026-03-10 20:51:18',97,NULL,NULL,1,NULL,NULL),
(160,NULL,NULL,5.9500,'IN','Delivery',50,NULL,'2026-03-10 20:51:18',98,NULL,NULL,1,NULL,NULL),
(161,NULL,NULL,5.9500,'IN','Delivery',50,NULL,'2026-03-10 20:51:18',99,NULL,NULL,1,NULL,NULL),
(162,NULL,NULL,286.0000,'IN','Delivery',51,NULL,'2026-03-10 22:29:09',100,NULL,NULL,1,NULL,NULL),
(163,NULL,NULL,286.0000,'IN','Delivery',51,NULL,'2026-03-10 22:29:09',101,NULL,NULL,1,NULL,NULL),
(164,NULL,NULL,286.0000,'IN','Delivery',51,NULL,'2026-03-10 22:29:09',102,NULL,NULL,1,NULL,NULL),
(165,NULL,NULL,286.0000,'IN','Delivery',51,NULL,'2026-03-10 22:29:09',103,NULL,NULL,1,NULL,NULL),
(166,NULL,NULL,286.0000,'IN','Delivery',51,NULL,'2026-03-10 22:29:09',104,NULL,NULL,1,NULL,NULL),
(167,NULL,NULL,286.0000,'IN','Delivery',51,NULL,'2026-03-10 22:29:09',105,NULL,NULL,1,NULL,NULL),
(168,NULL,NULL,286.0000,'IN','Delivery',51,NULL,'2026-03-10 22:29:09',106,NULL,NULL,1,NULL,NULL),
(169,NULL,NULL,286.0000,'IN','Delivery',51,NULL,'2026-03-10 22:29:09',107,NULL,NULL,1,NULL,NULL),
(170,NULL,NULL,286.0000,'IN','Delivery',51,NULL,'2026-03-10 22:29:09',108,NULL,NULL,1,NULL,NULL),
(171,NULL,NULL,286.0000,'IN','Delivery',51,NULL,'2026-03-10 22:29:09',109,NULL,NULL,1,NULL,NULL),
(172,NULL,NULL,286.0000,'IN','Delivery',51,NULL,'2026-03-10 22:29:09',110,NULL,NULL,1,NULL,NULL),
(173,NULL,NULL,286.0000,'IN','Delivery',51,NULL,'2026-03-10 22:29:09',111,NULL,NULL,1,NULL,NULL),
(174,NULL,NULL,286.0000,'IN','Delivery',51,NULL,'2026-03-10 22:29:09',112,NULL,NULL,1,NULL,NULL),
(175,NULL,NULL,286.0000,'IN','Delivery',51,NULL,'2026-03-10 22:29:09',113,NULL,NULL,1,NULL,NULL),
(176,NULL,NULL,286.0000,'IN','Delivery',51,NULL,'2026-03-10 22:29:09',114,NULL,NULL,1,NULL,NULL),
(177,NULL,NULL,286.0000,'IN','Delivery',51,NULL,'2026-03-10 22:29:09',115,NULL,NULL,1,NULL,NULL),
(178,NULL,NULL,286.0000,'IN','Delivery',51,NULL,'2026-03-10 22:29:09',116,NULL,NULL,1,NULL,NULL),
(179,NULL,NULL,286.0000,'IN','Delivery',51,NULL,'2026-03-10 22:29:09',117,NULL,NULL,1,NULL,NULL),
(180,NULL,NULL,286.0000,'IN','Delivery',51,NULL,'2026-03-10 22:29:09',118,NULL,NULL,1,NULL,NULL),
(181,NULL,NULL,286.0000,'IN','Delivery',51,NULL,'2026-03-10 22:29:09',119,NULL,NULL,1,NULL,NULL),
(182,NULL,NULL,24.8500,'IN','Delivery',52,NULL,'2026-03-14 16:45:48',120,NULL,NULL,1,NULL,NULL),
(183,NULL,NULL,14.8500,'IN','Delivery',52,NULL,'2026-03-14 16:45:48',121,NULL,NULL,1,NULL,NULL),
(184,NULL,NULL,14.8500,'IN','Delivery',52,NULL,'2026-03-14 16:45:48',122,NULL,NULL,1,NULL,NULL);
/*!40000 ALTER TABLE `stock_movements` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `supplier_payments`
--

DROP TABLE IF EXISTS `supplier_payments`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `supplier_payments` (
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
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `supplier_payments`
--

LOCK TABLES `supplier_payments` WRITE;
/*!40000 ALTER TABLE `supplier_payments` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `supplier_payments` VALUES
(1,36,5,3000.00,'2026-03-04','bank_transfer','Pirmas mokėjimas','2026-03-04 17:38:48'),
(3,38,6,460.00,'2026-03-05','bank_transfer','','2026-03-04 21:09:35'),
(4,38,6,25000.00,'2026-03-08','bank_transfer','pirmas pavedimas uz duona','2026-03-08 19:29:54'),
(6,44,2,6300.00,'2026-03-10','bank_transfer','','2026-03-09 20:36:51'),
(10,47,7,2080.00,'2026-03-10','bank_transfer','GAlutinis mokėjimas','2026-03-10 19:26:19'),
(11,48,7,200.00,'2026-03-10','bank_transfer','','2026-03-10 19:45:16'),
(12,39,6,500.00,'2026-03-10','bank_transfer','','2026-03-10 19:45:37'),
(13,46,7,7612.50,'2026-03-11','bank_transfer','pilnas mok4jimas','2026-03-10 20:08:20'),
(14,46,7,7612.50,'2026-03-11','bank_transfer','pilnas mok4jimas','2026-03-10 20:08:30'),
(15,46,7,7612.50,'2026-03-11','bank_transfer','pilnas mok4jimas','2026-03-10 20:08:31'),
(16,48,7,87.76,'2026-03-11','bank_transfer','','2026-03-10 20:17:48'),
(17,47,7,200.00,'2026-03-11','bank_transfer','','2026-03-10 20:18:07'),
(18,50,5,803.25,'2026-03-11','bank_transfer','aaa','2026-03-10 21:51:19'),
(19,51,5,200.00,'2026-03-14','bank_transfer','testas','2026-03-14 13:27:34');
/*!40000 ALTER TABLE `supplier_payments` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `units_of_measure`
--

DROP TABLE IF EXISTS `units_of_measure`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `units_of_measure` (
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
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `units_of_measure`
--

LOCK TABLES `units_of_measure` WRITE;
/*!40000 ALTER TABLE `units_of_measure` DISABLE KEYS */;
set autocommit=0;
/*!40000 ALTER TABLE `units_of_measure` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `users`
--

DROP TABLE IF EXISTS `users`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `users` (
  `id` int NOT NULL AUTO_INCREMENT,
  `username` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `password_hash` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `full_name` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `email` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `is_active` tinyint(1) NOT NULL DEFAULT '1',
  `created_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `username` (`username`),
  KEY `idx_username` (`username`),
  KEY `idx_email` (`email`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Sistemos vartotojai - autentifikacija';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `users`
--

LOCK TABLES `users` WRITE;
/*!40000 ALTER TABLE `users` DISABLE KEYS */;
set autocommit=0;
/*!40000 ALTER TABLE `users` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Temporary table structure for view `warehouse_stock`
--

DROP TABLE IF EXISTS `warehouse_stock`;
/*!50001 DROP VIEW IF EXISTS `warehouse_stock`*/;
SET @saved_cs_client     = @@character_set_client;
SET character_set_client = utf8mb4;
/*!50001 CREATE VIEW `warehouse_stock` AS SELECT
 1 AS `warehouse_id`,
  1 AS `warehouse_name`,
  1 AS `warehouse_type`,
  1 AS `product_id`,
  1 AS `product_name`,
  1 AS `current_stock` */;
SET character_set_client = @saved_cs_client;

--
-- Table structure for table `warehouse_stocks`
--

DROP TABLE IF EXISTS `warehouse_stocks`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `warehouse_stocks` (
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
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `warehouse_stocks`
--

LOCK TABLES `warehouse_stocks` WRITE;
/*!40000 ALTER TABLE `warehouse_stocks` DISABLE KEYS */;
set autocommit=0;
/*!40000 ALTER TABLE `warehouse_stocks` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `warehouse_types`
--

DROP TABLE IF EXISTS `warehouse_types`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `warehouse_types` (
  `id` int NOT NULL AUTO_INCREMENT,
  `code` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL,
  `name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` text COLLATE utf8mb4_unicode_ci,
  `is_active` tinyint(1) DEFAULT '1',
  PRIMARY KEY (`id`),
  UNIQUE KEY `code` (`code`)
) ENGINE=InnoDB AUTO_INCREMENT=5 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Sandėlių tipai';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `warehouse_types`
--

LOCK TABLES `warehouse_types` WRITE;
/*!40000 ALTER TABLE `warehouse_types` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `warehouse_types` VALUES
(1,'RAW','Žaliavų',NULL,1),
(2,'PROD','Gamybos',NULL,1),
(3,'MAT','Medžiagų',NULL,1),
(4,'FIN','Gatavos produkcijos',NULL,1);
/*!40000 ALTER TABLE `warehouse_types` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Table structure for table `warehouses`
--

DROP TABLE IF EXISTS `warehouses`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `warehouses` (
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
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `warehouses`
--

LOCK TABLES `warehouses` WRITE;
/*!40000 ALTER TABLE `warehouses` DISABLE KEYS */;
set autocommit=0;
INSERT INTO `warehouses` VALUES
(1,'JUO_ONU','Onuškio žaliavos sandėlis',1,'Onuškio g. 23','Juodupė','Lithuania','Onuškio žaliavų sandėlis ',1,'2026-02-22 19:20:59','2026-03-04 20:42:57','sandėlis@lakstena.lt','MAIN'),
(2,'JUO_GAM','Juodupės gamyba',2,'S. Neries g. 9','Juodupė','Lithuania','Juodupės gamybos sandėlis',1,'2026-03-04 20:43:34','2026-03-04 20:45:16','juodupe_gamyba@lakstena.lt','MAIN');
/*!40000 ALTER TABLE `warehouses` ENABLE KEYS */;
UNLOCK TABLES;
commit;

--
-- Final view structure for view `warehouse_stock`
--

/*!50001 DROP VIEW IF EXISTS `warehouse_stock`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb4 */;
/*!50001 SET character_set_results     = utf8mb4 */;
/*!50001 SET collation_connection      = utf8mb4_general_ci */;
/*!50001 CREATE ALGORITHM=UNDEFINED */
/*!50013 DEFINER=`erp_user`@`%` SQL SECURITY DEFINER */
/*!50001 VIEW `warehouse_stock` AS select `w`.`id` AS `warehouse_id`,`w`.`name` AS `warehouse_name`,`w`.`warehouse_type` AS `warehouse_type`,`p`.`id` AS `product_id`,`p`.`name` AS `product_name`,coalesce(sum((case when (`sm`.`movement_type` = 'IN') then `sm`.`quantity` else -(`sm`.`quantity`) end)),0) AS `current_stock` from ((`warehouses` `w` join `products` `p`) left join `stock_movements` `sm` on(((`sm`.`warehouse_id` = `w`.`id`) and (`sm`.`product_id` = `p`.`id`)))) group by `w`.`id`,`w`.`name`,`w`.`warehouse_type`,`p`.`id`,`p`.`name` */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*M!100616 SET NOTE_VERBOSITY=@OLD_NOTE_VERBOSITY */;

-- Dump completed on 2026-03-16 16:22:25
