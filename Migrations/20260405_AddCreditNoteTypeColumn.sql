-- =====================================================
-- NORDIC BEES ERP - ADD CREDIT NOTE TYPE COLUMN
-- Date: 2026-04-05
-- =====================================================

-- Add credit_note_type column to credit_notes table
ALTER TABLE `credit_notes`
ADD COLUMN `credit_note_type` VARCHAR(50) DEFAULT 'standard' AFTER `language`;