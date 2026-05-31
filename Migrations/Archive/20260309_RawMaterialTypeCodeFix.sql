-- Atnaujinimo skriptas: raw_material_types lentelės code stulpelio pildymui
-- 2026-03-09

-- Užpildyti NULL codes pagal is_honey reikšmę:
-- is_honey = 1 (medus) -> code = 'MD'
-- is_honey = 0 (ne medus) -> code = 'ZM'

UPDATE raw_material_types 
SET code = 'MD' 
WHERE is_honey = 1 AND code IS NULL;

UPDATE raw_material_types 
SET code = 'ZM' 
WHERE is_honey = 0 AND code IS NULL;

-- Patikrinimo užklausa
SELECT id, name, code, is_honey, is_active 
FROM raw_material_types 
ORDER BY sort_order;