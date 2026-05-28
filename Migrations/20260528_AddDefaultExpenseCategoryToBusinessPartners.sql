-- Migration: Add default_expense_category_id to business_partners table
-- Purpose: Link a default expense category to each business partner (supplier)
-- Date: 2026-05-28
-- Requires DBA execution: MCP DDL is blocked

ALTER TABLE business_partners
ADD COLUMN default_expense_category_id INT NULL
AFTER partner_type;

-- Optional: Add foreign key constraint (uncomment if needed)
-- ALTER TABLE business_partners
-- ADD CONSTRAINT fk_bp_default_expense_category
-- FOREIGN KEY (default_expense_category_id) REFERENCES expense_categories(id) ON DELETE SET NULL;