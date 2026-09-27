-- Etapas 2 S4 (D-044 Q2/Q5): known IBANs backfill. business_partners.bank_account values that pass the IBAN rules
-- (whitespace stripped, upper-cased, only A-Z0-9, country length table of Services/Validation/IbanValidator.cs,
-- ISO 13616 mod-97 = 1) become supplier_bank_accounts rows with source = 'MIGRATED'. Values that do not pass stay only in the
-- legacy column. Idempotent (skips rows that exist). Portable: recursive CTE (MySQL 8.0 and MariaDB >= 10.2).
--
-- The explicit COLLATE keeps it independent of the server's default collation (the new table's iban column takes the
-- database default, business_partners is utf8mb4_unicode_ci).
--
-- Run the SELECT first (dry run: which partners would get a row); then the INSERT. The /*SCOPE*/ token is where the tests
-- restrict the statement to their own partners; leave it as is.

-- ---- dry run
WITH RECURSIVE
src AS (
  SELECT id, UPPER(REGEXP_REPLACE(bank_account, '[[:space:]]', '')) AS n
  FROM business_partners
  WHERE bank_account IS NOT NULL AND TRIM(bank_account) <> '' /*SCOPE*/
),
shaped AS (
  SELECT id, n FROM src
  WHERE n REGEXP '^[A-Z][A-Z][0-9A-Z]+$'
    AND CHAR_LENGTH(n) BETWEEN 5 AND 34
    AND CASE WHEN LEFT(n, 2) IN ('LT','LV','EE','DE','PL','RO','FI','SE','NL','BE','DK','AT','CZ','SK','FR','IT','ES','GB','UA')
             THEN CHAR_LENGTH(n) = CASE LEFT(n, 2) WHEN 'LT' THEN 20 WHEN 'LV' THEN 21 WHEN 'EE' THEN 20 WHEN 'DE' THEN 22 WHEN 'PL' THEN 28 WHEN 'RO' THEN 24 WHEN 'FI' THEN 18 WHEN 'SE' THEN 24 WHEN 'NL' THEN 18 WHEN 'BE' THEN 16 WHEN 'DK' THEN 18 WHEN 'AT' THEN 20 WHEN 'CZ' THEN 24 WHEN 'SK' THEN 24 WHEN 'FR' THEN 27 WHEN 'IT' THEN 27 WHEN 'ES' THEN 24 WHEN 'GB' THEN 22 WHEN 'UA' THEN 29 END
             ELSE CHAR_LENGTH(n) BETWEEN 15 AND 34 END
),
digits AS (
  SELECT id, n, REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(CONCAT(SUBSTR(n,5),SUBSTR(n,1,4)),'A','10'),'B','11'),'C','12'),'D','13'),'E','14'),'F','15'),'G','16'),'H','17'),'I','18'),'J','19'),'K','20'),'L','21'),'M','22'),'N','23'),'O','24'),'P','25'),'Q','26'),'R','27'),'S','28'),'T','29'),'U','30'),'V','31'),'W','32'),'X','33'),'Y','34'),'Z','35') AS d FROM shaped
),
chain AS (
  SELECT id, n, d, 0 AS pos, 0 AS r FROM digits
  UNION ALL
  SELECT id, n, d, pos + 7, MOD(CAST(CONCAT(r, SUBSTR(d, pos + 1, 7)) AS UNSIGNED), 97)
  FROM chain WHERE pos < CHAR_LENGTH(d)
)
SELECT id AS partner_id, n AS iban FROM chain WHERE pos >= CHAR_LENGTH(d) AND r = 1 ORDER BY id;

-- ---- backfill
INSERT INTO supplier_bank_accounts (partner_id, iban, source, source_invoice_id, is_active, created_at, created_by)
WITH RECURSIVE
src AS (
  SELECT id, UPPER(REGEXP_REPLACE(bank_account, '[[:space:]]', '')) AS n
  FROM business_partners
  WHERE bank_account IS NOT NULL AND TRIM(bank_account) <> '' /*SCOPE*/
),
shaped AS (
  SELECT id, n FROM src
  WHERE n REGEXP '^[A-Z][A-Z][0-9A-Z]+$'
    AND CHAR_LENGTH(n) BETWEEN 5 AND 34
    AND CASE WHEN LEFT(n, 2) IN ('LT','LV','EE','DE','PL','RO','FI','SE','NL','BE','DK','AT','CZ','SK','FR','IT','ES','GB','UA')
             THEN CHAR_LENGTH(n) = CASE LEFT(n, 2) WHEN 'LT' THEN 20 WHEN 'LV' THEN 21 WHEN 'EE' THEN 20 WHEN 'DE' THEN 22 WHEN 'PL' THEN 28 WHEN 'RO' THEN 24 WHEN 'FI' THEN 18 WHEN 'SE' THEN 24 WHEN 'NL' THEN 18 WHEN 'BE' THEN 16 WHEN 'DK' THEN 18 WHEN 'AT' THEN 20 WHEN 'CZ' THEN 24 WHEN 'SK' THEN 24 WHEN 'FR' THEN 27 WHEN 'IT' THEN 27 WHEN 'ES' THEN 24 WHEN 'GB' THEN 22 WHEN 'UA' THEN 29 END
             ELSE CHAR_LENGTH(n) BETWEEN 15 AND 34 END
),
digits AS (
  SELECT id, n, REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(CONCAT(SUBSTR(n,5),SUBSTR(n,1,4)),'A','10'),'B','11'),'C','12'),'D','13'),'E','14'),'F','15'),'G','16'),'H','17'),'I','18'),'J','19'),'K','20'),'L','21'),'M','22'),'N','23'),'O','24'),'P','25'),'Q','26'),'R','27'),'S','28'),'T','29'),'U','30'),'V','31'),'W','32'),'X','33'),'Y','34'),'Z','35') AS d FROM shaped
),
chain AS (
  SELECT id, n, d, 0 AS pos, 0 AS r FROM digits
  UNION ALL
  SELECT id, n, d, pos + 7, MOD(CAST(CONCAT(r, SUBSTR(d, pos + 1, 7)) AS UNSIGNED), 97)
  FROM chain WHERE pos < CHAR_LENGTH(d)
)
SELECT DISTINCT c.id, c.n, 'MIGRATED', NULL, 1, NOW(6), NULL
FROM chain c
WHERE c.pos >= CHAR_LENGTH(c.d) AND c.r = 1
  AND NOT EXISTS (SELECT 1 FROM supplier_bank_accounts s WHERE s.partner_id = c.id AND s.iban = c.n COLLATE utf8mb4_unicode_ci);
