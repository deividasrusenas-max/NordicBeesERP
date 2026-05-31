-- Delete duplicate Sanitex UAB suppliers (IDs 364, 365)
-- Kept the original with higher ID, removing duplicates created during data import
DELETE FROM business_partners WHERE id IN (364, 365);