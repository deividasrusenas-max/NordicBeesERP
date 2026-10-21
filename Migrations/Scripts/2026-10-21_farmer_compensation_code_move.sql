-- Audit note: old farmers may still carry the compensation VAT code in vat_code (the compensation_vat_code column was
-- added without a data move). NOT executed by the agent. First run the SELECT and eyeball every row.
START TRANSACTION;

SELECT id, name, vat_code, compensation_vat_code
FROM business_partners
WHERE is_individual = 1 AND vat_code REGEXP '^[0-9]{12,13}$';

-- Only 12-digit codes (the valid current format; 13 digits is the obsolete format and needs re-registration, handle by hand)
UPDATE business_partners
SET compensation_vat_code = vat_code, vat_code = NULL
WHERE is_individual = 1
  AND vat_code REGEXP '^[0-9]{12}$'
  AND (compensation_vat_code IS NULL OR compensation_vat_code = '');

SELECT id, name, vat_code, compensation_vat_code
FROM business_partners
WHERE is_individual = 1 AND compensation_vat_code IS NOT NULL
ORDER BY id DESC LIMIT 50;

ROLLBACK; -- change to COMMIT only after review; note old invoices keep their customer_vat_code snapshot
