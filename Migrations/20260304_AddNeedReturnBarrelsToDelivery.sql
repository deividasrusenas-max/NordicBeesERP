ALTER TABLE deliveries 
ADD COLUMN need_return_barrels TINYINT(1) NOT NULL DEFAULT 0 COMMENT 'Ar bitininkui reikės grąžinti statines';