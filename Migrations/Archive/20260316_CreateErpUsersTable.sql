-- Create erp_users table for cookie-based authentication
-- =====================================================
-- NORDIC BEES ERP - Authentication Table
-- =====================================================

CREATE TABLE IF NOT EXISTS `erp_users` (
    `id` int NOT NULL AUTO_INCREMENT,
    `email` varchar(256) NOT NULL,
    `password_hash` varchar(500) NOT NULL,
    `full_name` varchar(256) NOT NULL,
    `role` varchar(50) NOT NULL DEFAULT 'User',
    `is_active` tinyint(1) NOT NULL DEFAULT 1,
    `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`),
    UNIQUE KEY `email` (`email`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='ERP Users for cookie-based authentication';

-- Insert default admin user
-- Email: admin@nordicbees.lt
-- Password: pakeisk_mane_123 (SHA256 hash will be generated)
-- TODO: Replace with actual hashed password before deployment
INSERT INTO `erp_users` (`email`, `password_hash`, `full_name`, `role`, `is_active`)
SELECT 'admin@nordicbees.lt', 'placeholder_hash', 'Administrator', 'Admin', 1
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM `erp_users` WHERE `email` = 'admin@nordicbees.lt'
);