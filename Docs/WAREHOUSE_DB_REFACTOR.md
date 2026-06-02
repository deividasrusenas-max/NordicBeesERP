# NordicBeesERP — Sandėlio modulio DB refaktorizacija
**Versija:** 1.0
**Data:** 2026-06-02
**Tikslas:** Pilna sandėlio modulio DB schema — BRC8 atitikimas, LOT genealogy, gamyba, atsekamumas

---

## Kontekstas — kodėl refaktorizacija būtina

Prieš implementuojant ženklinimo sistemą, automatinį svėrimą ir LOT tracking reikia sutvarkyti DB pagrindą. Priešingu atveju kiekvienas naujas modulis reikš dar vieną refaktorizaciją.

**Verslo realybė:**
- Medus iš skirtingų tiekėjų (ūkininkų + įmonių) maišomas gamybos metu
- Vienas production LOT = viena medaus rūšis iš N tiekėjų
- Viena žaliavos statinė gali būti panaudota dalinai (likutis grąžinamas į sandėlį)
- Žaliavos: medus (BARREL/BUCKET), bičių duona, žiedadulkės, pikis, propolis, vaškas
- Gatava produkcija: buteliukai, stiklainiai, pakuotės — mažmeninė ir didmeninė

---

## Esamos DB problemos

### Kritinės (blokiruoja naujus modulius)

**1. Dvi priėmimo lentelės — `honey_deliveries` ir `deliveries`**
- `honey_deliveries` — senasis modelis, tik medui, agregatas (ne atskiri konteineriai)
- `deliveries` — naujasis modelis, visiems produktams, su konteineriais
- Abi egzistuoja lygiagrečiai, nėra aiškaus ryšio
- `production_batch_ingredients.honey_delivery_id` → rodo į `honey_deliveries`, ne `deliveries`
- **Sprendimas:** `honey_deliveries` → pereinama prie `deliveries` + `containers`

**2. `production_batch_ingredients` rodo į `honey_deliveries`**
- Senas ryšys: `production_batch_ingredients.honey_delivery_id → honey_deliveries.id`
- Tai reiškia gamybos modulis negali naudoti naujų `containers` įrašų
- **Sprendimas:** pakeisti ryšį → `container_id → containers.id`

**3. `lots` lentelė neišbaigta**
- Turi tik `lot_number`, `lot_type`, `customer_id`, `invoice_id`
- Nėra ryšio su `containers` (žaliavomis)
- Nėra ryšio su `production_batches`
- Nėra `honey_type_id`, `total_weight`, `status`, `expiry_date`
- **Sprendimas:** pilnas redesign

**4. `containers.quantity` vs `remaining_weight`**
- `quantity = 1` visada — neturi prasmės
- Daliniam sunaudojimui reikia `remaining_weight`, ne `remaining_quantity`
- **Sprendimas:** pridėti `remaining_weight`, `quantity` palikti backward compat

**5. Dvi sandėlio atsargų lentelės — `warehouse_stock` ir `warehouse_stocks`**
- `warehouse_stock` — VIEW arba denormalizuota lentelė be PK
- `warehouse_stocks` — normalizuota su `lot_number` kaip string (ne FK)
- Nėra aiškaus sąsajos su `containers` ar `lots`
- **Sprendimas:** `warehouse_stocks` → pilnai perrašyti su FK į `lots`

**6. `stock_movements` — neišbaigtas**
- `movement_type: IN/OUT/TRANSFER/ADJUSTMENT` — per abstraktus
- `reference_type: varchar(50)` — polymorphic be FK
- Nėra `weight_kg` lauko (tik `quantity`)
- Nėra `lot_id` FK (yra kolona bet nėra FK constraint)
- **Sprendimas:** pridėti `event_type` enum su konkrečiais CTE tipais

**7. `products` — nėra žaliavų tipo**
- Visi produktai `product_type: FinishedGood` arba `Packaging`
- Nėra `RawMaterial` tipo
- `warehouse_managed = 0` visiems — žaliavos nesekamos per products
- Žaliavos sekamos per `honey_deliveries` ir `containers` — atskira logika
- **Sprendimas:** žaliavos lieka `containers` logikoje, gatava produkcija per `products`

---

### Mažesnės problemos

- `stock_movements.warehouse_id` ir `from_warehouse_id` / `to_warehouse_id` — du skirtingi laukai tam pačiam tikslui
- `production_batches.lot_number` — string, ne FK į `lots`
- `lots.invoice_id` — sena logika, lot pririštas prie sąskaitos, ne atvirkščiai
- `honey_types` ir `raw_material_types` — dvi atskiros klasifikacijos lentelės kurių ryšys neapibrėžtas

---

## Verslo procesų CTE žemėlapis

Sandėlio modulis organizuojamas pagal **Critical Tracking Events** (BRC8 Clause 3.9):

```
CTE-1: RECEIVE        Žaliava priimama į sandėlį
CTE-2: INSPECT        Kokybės tikrinimas (OK/NOK/CONDITIONAL)
CTE-3: STORE          Konteineris sandėlyje (implicit — po RECEIVE+INSPECT)
CTE-4: RESERVE        Rezervuojama gamybai ar pardavimui
CTE-5: CONSUME        Žaliava sunaudojama gamyboje (visa arba dalinai)
CTE-6: PRODUCE        Gamybos rezultatas — production LOT sukurtas
CTE-7: PACK           Fasavimas į mažmeninius pakuotes (iš production LOT)
CTE-8: SHIP           Išvežimas klientui
CTE-9: WRITE_OFF      Nurašymas (gedimas, praradimas, kokybės problema)
CTE-10: TRANSFER      Perkėlimas tarp sandėlių
CTE-11: QUARANTINE    Karantinas dėl kokybės problemos
CTE-12: RELEASE       Karantino atlaisvinimas
```

Kiekvienas CTE = vienas `stock_events` įrašas (žr. naują schemą).

---

## Žaliavų ir produkcijos ryšys

```
ŽALIAVOS (Raw Materials)
    Priimama:   deliveries → delivery_lines → containers
    Sandėlyje:  containers.status = IN_STOCK
    Gamyboje:   containers.status = IN_PRODUCTION
    Sunaudota:  containers.status = CONSUMED / PARTIALLY_CONSUMED

GAMYBA (Production)
    production_orders → production_batches
    production_batch_ingredients (N containers → 1 batch)
    Rezultatas: production_lots (finished bulk honey)

FASAVIMAS (Packing)
    packing_orders → packing_lines
    Iš production_lot → į finished_goods_stock (per products + lots)

PARDAVIMAS (Sales)
    invoices → invoice_lines (su lot_id FK)
    Iš finished_goods_stock → klientui
```

---

## Nauja DB schema

### Išlaikomos lentelės (be pakeitimų)
- `warehouses` ✓
- `warehouse_types` ✓
- `honey_types` ✓
- `raw_material_types` ✓
- `products` ✓ (tik FinishedGood + Packaging)
- `business_partners` ✓ (+ nauji BRC8 laukai iš LABELING_PLAN)
- `erp_users` ✓
- `invoices` ✓
- `invoice_lines` — pridėti `lot_id FK`

### Pašalinamos lentelės
- `honey_deliveries` → pakeičiama `deliveries` + `containers`
- `warehouse_stock` (VIEW/denorm) → pakeičiama `v_warehouse_stock` VIEW
- `production_batch_ingredients` → pakeičiama `production_batch_inputs`

### Keičiamos lentelės
Visos sandėlio modulio lentelės — žr. žemiau.

---

### `deliveries` — priėmimo antraštė
```sql
CREATE TABLE deliveries (
    id                        INT AUTO_INCREMENT PRIMARY KEY,
    delivery_number           VARCHAR(50) NOT NULL,
    UNIQUE KEY uk_delivery_number (delivery_number),
    delivery_date             DATE NOT NULL,
    supplier_id               INT NOT NULL,
    warehouse_id              INT NOT NULL,
    raw_material_type_id      INT NULL,              -- medus / bičių duona / kt.
    origin_country            VARCHAR(100) NULL DEFAULT 'Lietuva',
    supplier_batch_number     VARCHAR(100) NULL,     -- komercinio tiekėjo LOT nr.
    -- Svėrimo statusas
    weighing_status           ENUM('NOT_STARTED','IN_PROGRESS','COMPLETED') NOT NULL DEFAULT 'NOT_STARTED',
    weighing_station_id       INT NULL,
    weighing_started_at       DATETIME NULL,
    weighing_completed_at     DATETIME NULL,
    -- BRC8 3.5: priėmimo tikrinimas
    inspection_result         ENUM('OK','NOK','CONDITIONAL') NULL,
    inspection_notes          TEXT NULL,
    inspection_by_user_id     INT NULL,
    -- Finansai
    total_net_weight          DECIMAL(10,3) NOT NULL DEFAULT 0,
    total_amount              DECIMAL(10,2) NOT NULL DEFAULT 0,
    paid_amount               DECIMAL(10,2) NOT NULL DEFAULT 0,
    need_return_barrels       TINYINT(1) NOT NULL DEFAULT 0,
    barrels_owed              INT NOT NULL DEFAULT 0,
    barrels_returned          INT NOT NULL DEFAULT 0,
    invoice_id                INT NULL,
    invoice_number            VARCHAR(50) NULL,
    -- Statusas
    status                    ENUM('RECEIVED','PRICED','PARTIAL_PAID','PAID','ACCEPTED','CLOSED')
                              NOT NULL DEFAULT 'RECEIVED',
    -- Audit
    created_by_user_id        INT NULL,
    notes                     TEXT NULL,
    created_at                TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at                TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (supplier_id)             REFERENCES business_partners(id),
    FOREIGN KEY (warehouse_id)            REFERENCES warehouses(id),
    FOREIGN KEY (raw_material_type_id)    REFERENCES raw_material_types(id),
    FOREIGN KEY (inspection_by_user_id)   REFERENCES erp_users(id),
    FOREIGN KEY (created_by_user_id)      REFERENCES erp_users(id)
);
```

### `delivery_lines` — priėmimo eilutės (agregatas per medaus tipą)
```sql
CREATE TABLE delivery_lines (
    id                  INT AUTO_INCREMENT PRIMARY KEY,
    delivery_id         INT NOT NULL,
    honey_type_id       INT NULL,                    -- medaus rūšis (NULL ne medui)
    container_type      ENUM('BARREL','BUCKET') NOT NULL,
    container_count     INT NOT NULL DEFAULT 0,      -- atnaujinama automatiškai
    total_gross_weight  DECIMAL(10,3) NOT NULL DEFAULT 0,
    total_tare_weight   DECIMAL(10,3) NOT NULL DEFAULT 0,
    total_net_weight    DECIMAL(10,3) NOT NULL DEFAULT 0,
    unit_price          DECIMAL(10,4) NULL,
    line_total          DECIMAL(10,2) NULL,
    notes               TEXT NULL,
    created_at          TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at          TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (delivery_id)  REFERENCES deliveries(id),
    FOREIGN KEY (honey_type_id) REFERENCES honey_types(id)
);
```

### `containers` — kiekvienas fizinis konteineris
```sql
CREATE TABLE containers (
    id                      INT AUTO_INCREMENT PRIMARY KEY,
    container_code          VARCHAR(50) NOT NULL,
    UNIQUE KEY uk_container_code (container_code),
    container_type          ENUM('BARREL','BUCKET') NOT NULL,
    -- Ryšiai
    delivery_line_id        INT NULL,
    supplier_id             INT NOT NULL,
    warehouse_id            INT NOT NULL,
    honey_type_id           INT NULL,
    raw_material_type_id    INT NULL,
    -- Svoriai
    gross_weight            DECIMAL(10,3) NOT NULL DEFAULT 0,
    tare_weight             DECIMAL(10,3) NOT NULL DEFAULT 0,
    net_weight              DECIMAL(10,3) NOT NULL DEFAULT 0,
    remaining_weight        DECIMAL(10,3) NOT NULL DEFAULT 0,  -- lieka po dalinio naudojimo
    -- Statusas
    status                  ENUM(
                                'RECEIVED',        -- priimtas, dar netikrintas
                                'IN_STOCK',        -- sandėlyje, laisvas
                                'RESERVED',        -- rezervuotas gamybai/pardavimui
                                'IN_PRODUCTION',   -- šiuo metu naudojamas gamyboje
                                'PARTIALLY_CONSUMED', -- dalis panaudota, likutis sandėlyje
                                'CONSUMED',        -- visiškai sunaudotas
                                'QUARANTINE',      -- karantinas
                                'SOLD',            -- tiesiogiai parduotas (didmena)
                                'RETURNED',        -- grąžintas tiekėjui
                                'WRITTEN_OFF'      -- nurašytas
                            ) NOT NULL DEFAULT 'RECEIVED',
    -- LOT ryšys (žaliavos LOT — ne production LOT)
    raw_lot_id              INT NULL,              -- FK į raw_lots
    -- Svėrimas
    weighing_mode           ENUM('MANUAL','SCALE') NOT NULL DEFAULT 'MANUAL',
    received_by_user_id     INT NULL,
    -- Ženklinimas (BRC8)
    last_label_printed_at   DATETIME NULL,
    label_print_count       INT NOT NULL DEFAULT 0,
    -- Kokybė
    quality_params          JSON NULL,
    beehive_location        TEXT NULL,             -- iš honey_deliveries
    -- Audit
    notes                   TEXT NULL,
    created_at              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at              TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (delivery_line_id)       REFERENCES delivery_lines(id),
    FOREIGN KEY (supplier_id)            REFERENCES business_partners(id),
    FOREIGN KEY (warehouse_id)           REFERENCES warehouses(id),
    FOREIGN KEY (honey_type_id)          REFERENCES honey_types(id),
    FOREIGN KEY (raw_material_type_id)   REFERENCES raw_material_types(id),
    FOREIGN KEY (received_by_user_id)    REFERENCES erp_users(id)
);
```

**Pastaba:** `remaining_weight` = `net_weight` kai priimama. Mažėja kai dalis sunaudojama gamyboje.

### `raw_lots` — žaliavos LOT (priėmimo lygmuo)
```sql
-- Kiekvienas delivery = vienas raw_lot (arba keli jei skirtingos medaus rūšys)
CREATE TABLE raw_lots (
    id                  INT AUTO_INCREMENT PRIMARY KEY,
    lot_number          VARCHAR(50) NOT NULL,
    UNIQUE KEY uk_raw_lot_number (lot_number),
    -- Formatas: RAW-YYYYMMDD-{SUPPLIER_CODE}-{SEQ}
    delivery_id         INT NOT NULL,
    honey_type_id       INT NULL,
    raw_material_type_id INT NULL,
    supplier_id         INT NOT NULL,
    warehouse_id        INT NOT NULL,
    origin_country      VARCHAR(100) NULL,
    -- Svoriai (sumine iš containers)
    total_net_weight    DECIMAL(10,3) NOT NULL DEFAULT 0,
    remaining_weight    DECIMAL(10,3) NOT NULL DEFAULT 0,
    -- Galiojimas
    received_date       DATE NOT NULL,
    expiry_date         DATE NULL,               -- NULL = neterminuotas (medus ~2 metai)
    -- Statusas
    status              ENUM('ACTIVE','PARTIALLY_CONSUMED','CONSUMED','QUARANTINE','WRITTEN_OFF')
                        NOT NULL DEFAULT 'ACTIVE',
    -- Kokybė
    quality_params      JSON NULL,
    -- Audit
    created_by          INT NULL,
    notes               TEXT NULL,
    created_at          TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at          TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (delivery_id)           REFERENCES deliveries(id),
    FOREIGN KEY (honey_type_id)         REFERENCES honey_types(id),
    FOREIGN KEY (raw_material_type_id)  REFERENCES raw_material_types(id),
    FOREIGN KEY (supplier_id)           REFERENCES business_partners(id),
    FOREIGN KEY (warehouse_id)          REFERENCES warehouses(id),
    FOREIGN KEY (created_by)            REFERENCES erp_users(id)
);
```

### `production_orders` — gamybos užsakymas
```sql
CREATE TABLE production_orders (
    id                  INT AUTO_INCREMENT PRIMARY KEY,
    order_number        VARCHAR(50) NOT NULL UNIQUE,
    -- Formatas: PO-YYYYMMDD-{SEQ}
    honey_type_id       INT NOT NULL,            -- kokios rūšies medus gaminamas
    planned_quantity_kg DECIMAL(10,3) NOT NULL,  -- planuojamas kiekis kg
    warehouse_id        INT NOT NULL,            -- gamybos sandėlys
    status              ENUM('PLANNED','IN_PROGRESS','COMPLETED','CANCELLED')
                        NOT NULL DEFAULT 'PLANNED',
    planned_date        DATE NULL,
    started_at          DATETIME NULL,
    completed_at        DATETIME NULL,
    created_by          INT NULL,
    notes               TEXT NULL,
    created_at          TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at          TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (honey_type_id)  REFERENCES honey_types(id),
    FOREIGN KEY (warehouse_id)   REFERENCES warehouses(id),
    FOREIGN KEY (created_by)     REFERENCES erp_users(id)
);
```

### `production_batches` — gamybos partija (vienas maišymo ciklas)
```sql
CREATE TABLE production_batches (
    id                  INT AUTO_INCREMENT PRIMARY KEY,
    batch_number        VARCHAR(100) NOT NULL UNIQUE,
    -- Formatas: BATCH-YYYYMMDD-{SEQ}
    production_order_id INT NULL,                -- gali būti be užsakymo
    honey_type_id       INT NOT NULL,
    warehouse_id        INT NOT NULL,
    -- Svoriai
    planned_quantity_kg DECIMAL(10,3) NULL,
    actual_input_kg     DECIMAL(10,3) NOT NULL DEFAULT 0,   -- iš viso sunaudota žaliavos
    actual_output_kg    DECIMAL(10,3) NOT NULL DEFAULT 0,   -- gauta production LOT
    yield_pct           DECIMAL(5,2) NULL,                  -- output/input * 100
    -- Statusas
    status              ENUM('PLANNED','IN_PROGRESS','COMPLETED','CANCELLED')
                        NOT NULL DEFAULT 'PLANNED',
    -- Datos
    batch_date          DATE NOT NULL,
    started_at          DATETIME NULL,
    completed_at        DATETIME NULL,
    -- Kaina
    total_cost          DECIMAL(10,2) NULL,
    cost_per_kg         DECIMAL(10,4) NULL,
    -- Audit
    created_by          INT NULL,
    notes               TEXT NULL,
    created_at          TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at          TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (production_order_id) REFERENCES production_orders(id),
    FOREIGN KEY (honey_type_id)       REFERENCES honey_types(id),
    FOREIGN KEY (warehouse_id)        REFERENCES warehouses(id),
    FOREIGN KEY (created_by)          REFERENCES erp_users(id)
);
```

### `production_batch_inputs` — žaliavos į gamybą (pakeičia `production_batch_ingredients`)
```sql
-- Many-to-many: N containers → 1 production_batch
CREATE TABLE production_batch_inputs (
    id                  INT AUTO_INCREMENT PRIMARY KEY,
    batch_id            INT NOT NULL,
    container_id        INT NOT NULL,            -- konkreti statinė/kibiras
    raw_lot_id          INT NOT NULL,            -- žaliavos LOT
    -- Kiek sunaudota iš šio konteinerio
    weight_used_kg      DECIMAL(10,3) NOT NULL,  -- sunaudota kg
    weight_before_kg    DECIMAL(10,3) NOT NULL,  -- buvo prieš
    weight_after_kg     DECIMAL(10,3) NOT NULL,  -- liko po (0 = visa sunaudota)
    -- Kaina
    unit_cost           DECIMAL(10,4) NULL,
    total_cost          DECIMAL(10,2) NULL,
    -- Audit
    added_by            INT NULL,
    added_at            DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (batch_id)     REFERENCES production_batches(id),
    FOREIGN KEY (container_id) REFERENCES containers(id),
    FOREIGN KEY (raw_lot_id)   REFERENCES raw_lots(id),
    FOREIGN KEY (added_by)     REFERENCES erp_users(id)
);
```

### `production_lots` — gamybos LOT (rezultatas)
```sql
-- Vienas production_batch → vienas production_lot
CREATE TABLE production_lots (
    id                  INT AUTO_INCREMENT PRIMARY KEY,
    lot_number          VARCHAR(50) NOT NULL UNIQUE,
    -- Formatas: LOT-YYYYMMDD-{HONEY_TYPE_CODE}-{SEQ}
    batch_id            INT NOT NULL,
    honey_type_id       INT NOT NULL,
    warehouse_id        INT NOT NULL,
    -- Kiekiai
    total_weight_kg     DECIMAL(10,3) NOT NULL,
    remaining_weight_kg DECIMAL(10,3) NOT NULL,  -- mažėja kai fasavimas arba tiesioginis pardavimas
    -- Galiojimas (BRC8 3.9)
    production_date     DATE NOT NULL,
    expiry_date         DATE NULL,               -- medus: +24 mėnesiai
    -- Statusas
    status              ENUM('ACTIVE','PARTIALLY_USED','CONSUMED','SHIPPED','WRITTEN_OFF')
                        NOT NULL DEFAULT 'ACTIVE',
    -- Kaina
    cost_per_kg         DECIMAL(10,4) NULL,
    -- Audit
    created_by          INT NULL,
    notes               TEXT NULL,
    created_at          TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at          TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (batch_id)      REFERENCES production_batches(id),
    FOREIGN KEY (honey_type_id) REFERENCES honey_types(id),
    FOREIGN KEY (warehouse_id)  REFERENCES warehouses(id),
    FOREIGN KEY (created_by)    REFERENCES erp_users(id)
);
```

### `stock_events` — pilnas CTE audit trail (pakeičia `stock_movements`)
```sql
CREATE TABLE stock_events (
    id                  INT AUTO_INCREMENT PRIMARY KEY,
    -- CTE tipas
    event_type          ENUM(
                            'RECEIVE',         -- CTE-1: priėmimas
                            'INSPECT',         -- CTE-2: tikrinimas
                            'RESERVE',         -- CTE-4: rezervavimas
                            'UNRESERVE',       -- rezervavimo atšaukimas
                            'CONSUME',         -- CTE-5: sunaudojimas gamyboje
                            'PRODUCE',         -- CTE-6: gamybos rezultatas
                            'PACK',            -- CTE-7: fasavimas
                            'SHIP',            -- CTE-8: išvežimas
                            'WRITE_OFF',       -- CTE-9: nurašymas
                            'TRANSFER',        -- CTE-10: perkėlimas
                            'QUARANTINE',      -- CTE-11: karantinas
                            'RELEASE',         -- CTE-12: atlaisvinimas
                            'WEIGHT_CORRECTION', -- svorio korekcija
                            'RETURN'           -- grąžinimas tiekėjui
                        ) NOT NULL,
    -- Kas judėjo
    container_id        INT NULL,              -- žaliavos konteineriui
    raw_lot_id          INT NULL,              -- žaliavos LOT
    production_lot_id   INT NULL,              -- production LOT
    batch_id            INT NULL,              -- gamybos partija
    -- Kur
    from_warehouse_id   INT NULL,
    to_warehouse_id     INT NULL,
    -- Kiek
    weight_kg           DECIMAL(10,3) NULL,
    quantity_units      DECIMAL(10,3) NULL,    -- vnt (fasavimui)
    -- Referensai
    delivery_id         INT NULL,
    invoice_id          INT NULL,
    -- Audit (BRC8 3.3 — INSERT ONLY)
    operator_id         INT NULL,
    event_at            DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    notes               TEXT NULL,
    FOREIGN KEY (container_id)      REFERENCES containers(id),
    FOREIGN KEY (raw_lot_id)        REFERENCES raw_lots(id),
    FOREIGN KEY (production_lot_id) REFERENCES production_lots(id),
    FOREIGN KEY (batch_id)          REFERENCES production_batches(id),
    FOREIGN KEY (from_warehouse_id) REFERENCES warehouses(id),
    FOREIGN KEY (to_warehouse_id)   REFERENCES warehouses(id),
    FOREIGN KEY (delivery_id)       REFERENCES deliveries(id),
    FOREIGN KEY (invoice_id)        REFERENCES invoices(id),
    FOREIGN KEY (operator_id)       REFERENCES erp_users(id)
    -- INSERT ONLY — niekada UPDATE/DELETE (BRC8 3.3)
);
```

### `finished_goods_stock` — gatavos produkcijos atsargos
```sql
CREATE TABLE finished_goods_stock (
    id                  INT AUTO_INCREMENT PRIMARY KEY,
    product_id          INT NOT NULL,
    production_lot_id   INT NOT NULL,          -- iš kurio production_lot
    warehouse_id        INT NOT NULL,
    quantity            DECIMAL(10,3) NOT NULL DEFAULT 0,    -- vnt arba kg
    reserved_quantity   DECIMAL(10,3) NOT NULL DEFAULT 0,
    unit                VARCHAR(20) NOT NULL DEFAULT 'vnt',
    expiry_date         DATE NULL,
    created_at          TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at          TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (product_id)        REFERENCES products(id),
    FOREIGN KEY (production_lot_id) REFERENCES production_lots(id),
    FOREIGN KEY (warehouse_id)      REFERENCES warehouses(id)
);
```

### `invoice_lines` — pakeitimas
```sql
-- Pridėti production_lot_id FK (vietoj lot_number string)
ALTER TABLE invoice_lines
    ADD COLUMN production_lot_id INT NULL,
    ADD FOREIGN KEY (production_lot_id) REFERENCES production_lots(id);
-- lot_number palikti backward compat, bet naudoti production_lot_id
```

---

## Ženklinimo sistemos lentelės

Visos iš `LABELING_PLAN.md` — čia tik ryšiai su nauja schema:

```
printers                ← nepriklauso nuo sandėlio schemos
weighing_stations       ← FK → warehouses (nesikeičia)
print_jobs              ← FK → containers (nesikeičia)
container_label_events  ← FK → containers (nesikeičia)
container_weight_corrections ← FK → containers (nesikeičia)
label_templates         ← nepriklauso nuo sandėlio schemos
supplier_approvals      ← FK → business_partners (nesikeičia)
non_conformances        ← polymorphic → deliveries/containers (nesikeičia)
document_files          ← pridėti → production_lots ref_type
```

---

## LOT numerių formatai

```
Žaliavos LOT (raw_lots):
    RAW-20260601-JONAS-001
    RAW-{YYYYMMDD}-{SUPPLIER_CODE}-{SEQ}
    Vienas LOT per delivery per medaus rūšį

Gamybos LOT (production_lots):
    LOT-20260601-LIEPU-001
    LOT-{YYYYMMDD}-{HONEY_TYPE_CODE}-{SEQ}
    Vienas LOT per production_batch

Container kodai (containers):
    PR-MD2606-001/001
    {DELIVERY_NUMBER}/{SEQ:D3}
    Kaip apibrėžta LABELING_PLAN.md
```

---

## Mass balance (BRC8 3.9)

Mass balance skaičiuojamas iš `stock_events`:

```sql
-- Žaliavos raw_lot mass balance
SELECT
    rl.lot_number,
    rl.total_net_weight as received_kg,
    SUM(CASE WHEN se.event_type = 'CONSUME' THEN se.weight_kg ELSE 0 END) as consumed_kg,
    SUM(CASE WHEN se.event_type = 'WRITE_OFF' THEN se.weight_kg ELSE 0 END) as written_off_kg,
    rl.remaining_weight as remaining_kg
FROM raw_lots rl
LEFT JOIN stock_events se ON se.raw_lot_id = rl.id
WHERE rl.lot_number = ?
GROUP BY rl.id;

-- Production LOT mass balance
SELECT
    pl.lot_number,
    pl.total_weight_kg as produced_kg,
    SUM(CASE WHEN se.event_type = 'SHIP' THEN se.weight_kg ELSE 0 END) as shipped_kg,
    SUM(CASE WHEN se.event_type = 'WRITE_OFF' THEN se.weight_kg ELSE 0 END) as written_off_kg,
    pl.remaining_weight_kg as remaining_kg
FROM production_lots pl
LEFT JOIN stock_events se ON se.production_lot_id = pl.id
WHERE pl.lot_number = ?
GROUP BY pl.id;
```

---

## FEFO enforcement

```sql
-- Gamybai: imti seniausiai galiojančias žaliavas pirma
SELECT c.*, rl.expiry_date
FROM containers c
JOIN raw_lots rl ON c.raw_lot_id = rl.id
WHERE c.honey_type_id = ?
  AND c.status IN ('IN_STOCK', 'PARTIALLY_CONSUMED')
  AND c.warehouse_id = ?
ORDER BY rl.expiry_date ASC NULLS LAST, rl.received_date ASC;
```

---

## Atsekamumas — pilnas kelias (BRC8 3.9)

### Backward trace (nuo gatavos produkcijos iki lauko)
```
invoice_line.production_lot_id
    → production_lots.batch_id
    → production_batch_inputs.container_id
    → containers.delivery_line_id
    → delivery_lines.delivery_id
    → deliveries.supplier_id
    → business_partners (ūkininkas/įmonė)
```

### Forward trace (nuo ūkininko iki kliento)
```
business_partners (tiekėjas)
    → deliveries
    → containers
    → production_batch_inputs
    → production_batches
    → production_lots
    → finished_goods_stock
    → invoice_lines
    → invoices (klientas)
```

---

## `warehouse_stock` VIEW (pakeičia abi esamas lenteles)

```sql
CREATE OR REPLACE VIEW v_warehouse_stock AS
-- Žaliavos sandėlyje
SELECT
    w.id as warehouse_id,
    w.name as warehouse_name,
    'RAW' as stock_type,
    rmt.name as material_name,
    ht.name as honey_type,
    COUNT(c.id) as container_count,
    SUM(c.remaining_weight) as total_remaining_kg,
    MIN(rl.expiry_date) as earliest_expiry
FROM containers c
JOIN warehouses w ON c.warehouse_id = w.id
LEFT JOIN raw_material_types rmt ON c.raw_material_type_id = rmt.id
LEFT JOIN honey_types ht ON c.honey_type_id = ht.id
LEFT JOIN raw_lots rl ON c.raw_lot_id = rl.id
WHERE c.status IN ('IN_STOCK','PARTIALLY_CONSUMED','RESERVED')
GROUP BY w.id, rmt.id, ht.id

UNION ALL

-- Gatava produkcija sandėlyje
SELECT
    w.id as warehouse_id,
    w.name as warehouse_name,
    'FINISHED' as stock_type,
    p.name as material_name,
    ht.name as honey_type,
    NULL as container_count,
    SUM(fgs.quantity - fgs.reserved_quantity) as total_remaining_kg,
    MIN(fgs.expiry_date) as earliest_expiry
FROM finished_goods_stock fgs
JOIN warehouses w ON fgs.warehouse_id = w.id
JOIN products p ON fgs.product_id = p.id
JOIN production_lots pl ON fgs.production_lot_id = pl.id
JOIN honey_types ht ON pl.honey_type_id = ht.id
WHERE fgs.quantity > fgs.reserved_quantity
GROUP BY w.id, p.id, ht.id;
```

---

## Migracijos strategija

### Fazė 1 — Naujų lentelių sukūrimas (be duomenų praradimo)
1. Sukurti `raw_lots`, `production_lots`, `production_orders`
2. Sukurti `production_batch_inputs` (nepašalinti `production_batch_ingredients` dar)
3. Sukurti `stock_events` (nepašalinti `stock_movements` dar)
4. Sukurti `finished_goods_stock`
5. Modifikuoti `containers` — pridėti naujus laukus
6. Modifikuoti `deliveries` — pridėti naujus laukus
7. Modifikuoti `delivery_lines` — ištaisyti
8. Pridėti `invoice_lines.production_lot_id`

### Fazė 2 — Duomenų migracija (jei `honey_deliveries` turi duomenų)
- `honey_deliveries` → `deliveries` + `containers` + `raw_lots`
- `production_batch_ingredients` → `production_batch_inputs`
- `stock_movements` → `stock_events`

**Dabartinė situacija:** `containers = 0`, `deliveries = 0` — Fazė 2 iš esmės tuščia.

### Fazė 3 — Senų lentelių pašalinimas
- `honey_deliveries` → DROP (po migrацijos patikrinimo)
- `production_batch_ingredients` → DROP
- `stock_movements` → DROP (arba palikti kaip archive)
- `warehouse_stock` → DROP (pakeičia VIEW)
- `warehouse_stocks` → DROP (pakeičia VIEW)

---

## EF Core pakeitimai

### Nauji modeliai
```
Models/Warehouse/RawLot.cs
Models/Warehouse/ProductionOrder.cs
Models/Warehouse/ProductionBatch.cs          (perratytas)
Models/Warehouse/ProductionBatchInput.cs     (pakeičia ProductionBatchIngredient)
Models/Warehouse/ProductionLot.cs
Models/Warehouse/FinishedGoodsStock.cs
Models/Warehouse/StockEvent.cs               (pakeičia StockMovement)
```

### Pakeičiami modeliai
```
Models/WarehouseModule/Container.cs          — nauji laukai
Models/WarehouseModule/Delivery.cs           — nauji laukai
Models/WarehouseModule/DeliveryLine.cs       — ištaisyti
Models/WarehouseModule/ContainerEnums.cs     — BUCKET_GROUP→BUCKET, +PARTIALLY_CONSUMED
Models/InvoiceModels.cs (InvoiceLine)        — +production_lot_id
```

### Pašalinami modeliai
```
Models/Honey/HoneyBatch.cs
Models/Honey/HoneyBatchIngredient.cs
(HoneyDelivery — jei egzistuoja kaip modelis)
```

### `NordicBeesERPContext.cs`
- `stock_events` — INSERT ONLY enforcement (kaip `container_label_events`)
- Nauji `DbSet<>` registravimai
- Pašalinti seni `DbSet<>` (HoneyDeliveries, HoneyBatches ir kt.)

---

## Implementacijos tvarka

### Prieš ženklinimo sistemą (WAREHOUSE_DB_REFACTOR)
1. Naujų lentelių sukūrimas (Fazė 1)
2. EF Core modeliai + DbContext + snapshot
3. Servisų skeleton (interfaces + tuščios implementacijos)
4. `honey_deliveries` duomenų migracija jei reikia (Fazė 2)
5. Senų lentelių pašalinimas (Fazė 3)
6. Esamo kodo atnaujinimas — `DeliveryCreate`, `DeliveryView`, `StockOverview`

### Po refaktorizacijos — ženklinimo sistema
Visi LABELING_PLAN.md žingsniai lieka nepakitę — tik DB lentelės bus teisingos.

---

## Klausimai kuriuos reikia apsispręsti prieš implementuojant

| Klausimas | Svarba | Poveikis |
|---|---|---|
| Ar `honey_deliveries` turi duomenų kuriuos reikia migruoti? | 🔴 P0 | Migracijos sudėtingumas |
| Ar gamybos modulis (`production_batches`) šiuo metu naudojamas? | 🔴 P0 | Ar galima keisti be duomenų praradimo |
| Medaus galiojimo laikas — kiek mėnesių? | 🟠 Svarbu | `expiry_date` automatinis skaičiavimas |
| Ar fasavimas (PACK) yra atskiras žingsnis ar gamybos dalis? | 🟠 Svarbu | `production_lots` → `finished_goods_stock` flow |
| Ar tiesioginis didmeninis pardavimas eina per `production_lots` ar tiesiai iš `containers`? | 🟠 Svarbu | `invoice_lines` ryšys |

