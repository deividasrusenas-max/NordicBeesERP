-- Migration: Fix Container NetWeight calculation
-- Date: 2026-03-04
-- Description: Update existing containers where NetWeight = 0 but GrossWeight > 0
-- This fixes containers created before NetWeight calculation was implemented in ContainerService

-- Update NetWeight for containers where it's 0 but GrossWeight > 0
UPDATE containers 
SET net_weight = gross_weight - tare_weight,
    updated_at = NOW()
WHERE net_weight = 0 
  AND gross_weight > 0 
  AND tare_weight > 0;

-- Verify the update
SELECT COUNT(*) as containers_fixed 
FROM containers 
WHERE net_weight = 0 
  AND gross_weight > 0 
  AND tare_weight > 0;