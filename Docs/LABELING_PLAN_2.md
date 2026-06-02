# NordicBeesERP — Sandėlio ženklinimo ir spausdinimo sistema
**Versija:** 3.0 (galutinė)
**Data:** 2026-06-02
**Autorius:** Deividas Rusenas

---

## Problema ir tikslas

**Problema:** Konteineriai patenka į sandėlį be identifikacijos. Svoris rašomas ant popieriaus, vėliau suvedamas į Excel. BRC8 auditorius negali atsekti konteinerio iki tiekėjo.

**Sprendimas:** Kiekvienas fizinis konteineris priėmimo metu gauna unikalų kodą ir lipduką. Viskas fiksuojama automatiškai — kas priėmė, kada, koks svoris, kas spausdino.

**Rezultatas:** BRC8 Clause 3.5, 3.7, 3.8, 3.9, 3.3, 6.3, 6.4 atitikimas sandėlio modulyje.

---

## BRC8 atitikimas — kas ERP atsakomybė

| Clause | Reikalavimas | Implementacija |
|---|---|---|
| 3.3 | Įrašų nekintamumas ir saugojimas | Niekada netrinami įrašai, `container_label_events` INSERT only |
| 3.5 | Tiekėjų patvirtinimas | `supplier_approvals`, perspėjimas priėmimo metu |
| 3.7 | Korekciniai veiksmai | `container_weight_corrections` su privaloma priežastimi |
| 3.8 | Neatitinkanti produkcija | `QUARANTINE` statusas, `non_conformances`, karantino lipdukas |
| 3.9 | Atsekamumas (FUNDAMENTAL) | `TraceabilitySearch` — backward P0, forward P3 |
| 6.3 | Kiekybinė kontrolė | Svorio tikslumas, kalibracija |
| 6.4 | Kalibravimas | `weighing_stations` kalibracijų datos, `CalibrationLog` |

**Kas lieka popieriniame procese (ne ERP):** HACCP planas, valymo žurnalai, kenkėjų kontrolė, darbuotojų mokymai, alergeno procedūra.

---

## Architektūra

### Fizinė darbo vieta
```
1 darbo vieta = 1 Pi + 1 Zebra ZM400 + 1 svarstyklės + 1 planšetė (5G)

Planšetė (5G hotspot) → Pi (WiFi) → Tailscale → ERP serveris
                                   → USB → Zebra
                                   → RS-232/USB → Svarstyklės (P2)
```

### Tinklo schema
```
ERP serveris (Tailscale 100.a.a.a)
    ↕ HTTP
Pi (Tailscale 100.b.b.b) — fiksuotas IP nepriklausomai nuo hotspot
    ↓ USB          ↓ Serial
  Zebra        Svarstyklės
```

Pi gauna internetą iš planšetės 5G hotspot. Tailscale suteikia fiksuotą IP.

### Spausdinimo srautas
```
Delivery sukurtas → print_jobs (PENDING) → LabelPrintWorker (kas 1s)
    → SemaphoreSlim(1) per printer_id
    → HttpPrinterGateway → POST /print → Pi → USB → Zebra → Lipdukas
    → container_label_events (PRINTED arba PRINT_FAILED)
```

---

## DB schema

### Naujos lentelės

#### `printers`
```sql
CREATE TABLE printers (
    id                  INT AUTO_INCREMENT PRIMARY KEY,
    name                VARCHAR(100) NOT NULL,
    location            VARCHAR(100) NOT NULL,
    endpoint_url        VARCHAR(200) NOT NULL,         -- http://100.x.x.x:5000 arba STUB
    connection_type     ENUM('HTTP','STUB') NOT NULL DEFAULT 'STUB',
    label_width_mm      DECIMAL(5,1) NOT NULL DEFAULT 108.0,
    label_height_mm     DECIMAL(5,1) NOT NULL DEFAULT 75.0,
    darkness            INT NOT NULL DEFAULT 25,
    dpi                 INT NOT NULL DEFAULT 200,
    last_test_print_at  DATETIME NULL,
    last_test_result    VARCHAR(50) NULL,
    is_active           TINYINT(1) NOT NULL DEFAULT 1,
    created_at          TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at          TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);
```

#### `weighing_stations`
```sql
CREATE TABLE weighing_stations (
    id                      INT AUTO_INCREMENT PRIMARY KEY,
    name                    VARCHAR(100) NOT NULL,
    warehouse_id            INT NOT NULL,
    printer_id              INT NOT NULL,
    pi_base_url             VARCHAR(200) NULL,          -- NULL = Pi dar nėra
    default_container_type  ENUM('BARREL','BUCKET') NULL,
    min_weight_kg           DECIMAL(5,3) NOT NULL DEFAULT 0.500,
    scale_protocol          ENUM('TOLEDO','METTLER','CAS','KERN','NONE') NOT NULL DEFAULT 'NONE',
    scale_regex             VARCHAR(200) NULL,
    -- BRC8 6.4
    last_calibration_date   DATE NULL,
    next_calibration_date   DATE NULL,
    calibration_cert_number VARCHAR(100) NULL,
    is_active               TINYINT(1) NOT NULL DEFAULT 1,
    created_at              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at              TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (warehouse_id) REFERENCES warehouses(id),
    FOREIGN KEY (printer_id)   REFERENCES printers(id)
);
```

#### `print_jobs`
```sql
CREATE TABLE print_jobs (
    id                  INT AUTO_INCREMENT PRIMARY KEY,
    printer_id          INT NOT NULL,
    station_id          INT NULL,
    container_id        INT NOT NULL,
    job_type            ENUM('RECEIPT_LABEL','QUARANTINE_LABEL','REPRINT') NOT NULL DEFAULT 'RECEIPT_LABEL',
    zpl_content         LONGTEXT NOT NULL,
    status              ENUM('PENDING','PROCESSING','DONE','FAILED','CANCELLED') NOT NULL DEFAULT 'PENDING',
    retry_count         INT NOT NULL DEFAULT 0,
    max_retries         INT NOT NULL DEFAULT 3,
    last_error          TEXT NULL,
    created_by_user_id  INT NULL,
    created_at          TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    processed_at        DATETIME NULL,
    done_at             DATETIME NULL,
    FOREIGN KEY (printer_id)   REFERENCES printers(id),
    FOREIGN KEY (station_id)   REFERENCES weighing_stations(id),
    FOREIGN KEY (container_id) REFERENCES containers(id)
);
```

#### `container_label_events` (BRC8 3.3 — INSERT ONLY, niekada UPDATE/DELETE)
```sql
CREATE TABLE container_label_events (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    container_id    INT NOT NULL,
    event_type      ENUM('PRINTED','REPRINTED','QUARANTINE_PRINTED','CANCELLED','PRINT_FAILED') NOT NULL,
    print_job_id    INT NULL,
    reason_code     ENUM('DAMAGED','LOST','MISPRINT','OTHER') NULL,  -- kai REPRINTED
    reason_text     VARCHAR(200) NULL,
    operator_id     INT NULL,    -- NULL tik kai PRINT_FAILED iš worker be user konteksto
    created_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (container_id) REFERENCES containers(id),
    FOREIGN KEY (print_job_id) REFERENCES print_jobs(id)
);
-- Pastaba: PRINT_FAILED evento operator_id imamas iš print_jobs.created_by_user_id worker'yje
```

#### `container_weight_corrections` (BRC8 3.7)
```sql
CREATE TABLE container_weight_corrections (
    id               INT AUTO_INCREMENT PRIMARY KEY,
    container_id     INT NOT NULL,
    old_gross_weight DECIMAL(10,3) NOT NULL,
    new_gross_weight DECIMAL(10,3) NOT NULL,
    old_tare_weight  DECIMAL(10,3) NOT NULL,
    new_tare_weight  DECIMAL(10,3) NOT NULL,
    old_net_weight   DECIMAL(10,3) NOT NULL,
    new_net_weight   DECIMAL(10,3) NOT NULL,
    reason           VARCHAR(200) NOT NULL,   -- privalomas visada
    corrected_by     INT NOT NULL,
    corrected_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (container_id) REFERENCES containers(id),
    FOREIGN KEY (corrected_by) REFERENCES erp_users(id)
);
```

#### `label_templates`
```sql
CREATE TABLE label_templates (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    name            VARCHAR(100) NOT NULL,
    template_type   ENUM('RECEIPT_BARREL','RECEIPT_BUCKET','QUARANTINE_BARREL','QUARANTINE_BUCKET','LOT_BARREL','LOT_BUCKET') NOT NULL,
    scriban_content LONGTEXT NOT NULL,
    label_width_mm  DECIMAL(5,1) NOT NULL DEFAULT 108.0,
    label_height_mm DECIMAL(5,1) NOT NULL DEFAULT 75.0,
    is_active       TINYINT(1) NOT NULL DEFAULT 1,
    is_default      TINYINT(1) NOT NULL DEFAULT 0,
    created_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);
```

#### `supplier_approvals` (BRC8 3.5)
```sql
CREATE TABLE supplier_approvals (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    supplier_id     INT NOT NULL,
    approved_by     INT NOT NULL,
    approval_date   DATE NOT NULL,
    expires_at      DATE NULL,                -- NULL = neterminuotas
    risk_level      ENUM('LOW','MEDIUM','HIGH') NOT NULL,
    approval_method ENUM('AUDIT','QUESTIONNAIRE','CERTIFICATION','OTHER') NOT NULL,
    cert_number     VARCHAR(100) NULL,
    notes           TEXT NULL,
    is_current      TINYINT(1) NOT NULL DEFAULT 1,  -- application logika: kai naujas → senas = 0
    created_at      DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (supplier_id) REFERENCES business_partners(id),
    FOREIGN KEY (approved_by) REFERENCES erp_users(id)
);
```

#### `non_conformances` (BRC8 3.8)
```sql
CREATE TABLE non_conformances (
    id                INT AUTO_INCREMENT PRIMARY KEY,
    ref_type          ENUM('DELIVERY','CONTAINER') NOT NULL,
    ref_id            INT NOT NULL,              -- polymorphic FK, nėra DB constraint
    detected_at       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    detected_by       INT NOT NULL,
    description       TEXT NOT NULL,
    severity          ENUM('MINOR','MAJOR','CRITICAL') NOT NULL,
    disposition       ENUM('PENDING','ACCEPTED','REJECTED','REWORKED','QUARANTINED') NOT NULL DEFAULT 'PENDING',
    disposition_by    INT NULL,
    disposition_at    DATETIME NULL,
    disposition_notes TEXT NULL,
    created_at        DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (detected_by) REFERENCES erp_users(id)
);
-- Pastaba: ref_id integralumas užtikrinamas application lygmenyje, ne DB
```

#### `document_files` (P4 struktūra — sukuriama P0 migracijoje, naudojama P4)
```sql
CREATE TABLE document_files (
    id                INT AUTO_INCREMENT PRIMARY KEY,
    ref_type          ENUM('DELIVERY','LOT','ORDER') NOT NULL,
    ref_id            INT NOT NULL,
    doc_type          ENUM('PACKING_LIST','CMR','QUALITY_CERT','RECEIPT_ACT') NOT NULL,
    file_path         VARCHAR(500) NOT NULL,
    original_filename VARCHAR(200) NOT NULL,
    file_size_bytes   INT NULL,
    generated_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    generated_by      INT NULL
);
```

---

### Pakeitimai esamose lentelėse

#### `containers`
```sql
-- 1. BUCKET_GROUP → BUCKET
ALTER TABLE containers
    MODIFY container_type ENUM('BARREL','BUCKET') NOT NULL;

-- 2. Pridėti naujus laukus
ALTER TABLE containers
    ADD COLUMN weighing_mode         ENUM('MANUAL','SCALE') NOT NULL DEFAULT 'MANUAL',
    ADD COLUMN received_by_user_id   INT NULL,
    ADD COLUMN last_label_printed_at DATETIME NULL,
    ADD COLUMN label_print_count     INT NOT NULL DEFAULT 0;

-- 3. Pridėti QUARANTINE statusą
ALTER TABLE containers
    MODIFY status ENUM(
        'RECEIVED','IN_STOCK','RESERVED','IN_PRODUCTION',
        'QUARANTINE','SOLD','RETURNED','WRITTEN_OFF'
    ) DEFAULT 'IN_STOCK';
```

**Pastaba:** `quantity` kolona paliekama — reikšmė visada `1` nuo šiol.

#### `deliveries`
```sql
ALTER TABLE deliveries
    ADD COLUMN weighing_status        ENUM('NOT_STARTED','IN_PROGRESS','COMPLETED') NOT NULL DEFAULT 'NOT_STARTED',
    ADD COLUMN weighing_station_id    INT NULL,
    ADD COLUMN weighing_started_at    DATETIME NULL,
    ADD COLUMN weighing_completed_at  DATETIME NULL,
    ADD COLUMN created_by_user_id     INT NULL,
    ADD COLUMN origin_country         VARCHAR(100) NULL,        -- BRC8 3.9: kilmės šalis delivery lygyje
    ADD COLUMN inspection_result      ENUM('OK','NOK','CONDITIONAL') NULL,  -- BRC8 3.5
    ADD COLUMN inspection_notes       TEXT NULL,
    ADD COLUMN inspection_by_user_id  INT NULL,
    ADD UNIQUE KEY uk_delivery_number (delivery_number),
    ADD FOREIGN KEY (weighing_station_id)   REFERENCES weighing_stations(id),
    ADD FOREIGN KEY (created_by_user_id)    REFERENCES erp_users(id),
    ADD FOREIGN KEY (inspection_by_user_id) REFERENCES erp_users(id);
```

**Pastaba:** `origin_country` yra `deliveries` lygyje — ne `delivery_lines`. Vienas tiekėjas, viena kilmė per priėmimą. Default imamas iš `business_partners.default_origin_country`.

#### `delivery_lines`
```sql
ALTER TABLE delivery_lines
    MODIFY container_type ENUM('BARREL','BUCKET') NOT NULL;
    -- supplier_batch_number čia NEBŪTINAS — retai naudojamas komerciniams tiekėjams
    -- Jei reikės — P3 migracija
```

#### `business_partners`
```sql
ALTER TABLE business_partners
    ADD COLUMN is_approved           TINYINT(1) NOT NULL DEFAULT 0,
    ADD COLUMN approval_expires_at   DATE NULL,
    ADD COLUMN supplier_risk_level   ENUM('LOW','MEDIUM','HIGH') NULL,
    ADD COLUMN default_origin_country VARCHAR(100) NULL DEFAULT 'Lietuva';
-- Svarbu: pridėti ir į BusinessPartner.cs modelį ir į EF Core snapshot
```

---

## Container kodų sistema

```
Formatas: {DELIVERY_NUMBER}/{SEQ:D3}

PR-MD2606-001/001   ← pirma statinė
PR-MD2606-001/002   ← antra statinė
PR-BD2606-003/001   ← bičių duona, kitas delivery
```

Kodai generuojami **tik** transakcijoje, ne UI metu. UI rodo "Bus sugeneruotas" — tikras numeris po submit.

`delivery_number` turi `UNIQUE` constraint — race condition apsauga. Jei konfliktas — retry transakcijoje.

---

## Darbo vietos

Sandėlininkas pasirenka darbo vietą **kiekvienam naujam delivery** pradžioje. Pasirinkimas saugomas `ProtectedSessionStorage`. Iš stoties automatiškai užpildomas `warehouse_id`.

Pi setup (vienkartinis):
```bash
sudo apt install tailscale && tailscale up --authkey=<KEY>
# Užrašyti Tailscale IP → weighing_stations.pi_base_url
# Sukonfigūruoti wpa_supplicant planšetės SSID
sudo systemctl enable tailscaled printer-service
```

---

## Wizard — DeliveryCreate.razor (6 žingsniai)

### Žingsnis 1: Darbo vieta
Kiekvienam delivery iš naujo. Rodo stoties pavadinimą, sandėlį, Pi statusą (🟢/⚪).

### Žingsnis 2: Tiekėjas + data
- Tiekėjas: `MudAutocomplete` + "Sukurti naują" dialog
- Kilmės šalis: auto-užpildoma iš `business_partners.default_origin_country`, redaguojama
- Tiekėjo patvirtinimas (BRC8 3.5):
  - Nepatvirtintas → `Warning` alert, neblokuoja
  - Patvirtinimas pasibaigęs → `Error` alert, blokuoja (konfigūruojama `app_settings`)
- Sandėlys: readonly MudChip iš stoties

### Žingsnis 3: Žaliavos tipas
Identiškas dabartiniam.

### Žingsnis 4: Pakuotės tipas
BARREL / BUCKET. Ne-medaus žaliavoms — automatiškai BUCKET, žingsnis praleidžiamas.

### Žingsnis 5: Svėrimas

**Vienodi svoriai:**
```
Tara: [16 kg] [19 kg] [Kitas: ____]
Kiekis: [12]    Brutto: [305.0] kg
Netto vienai: 286.0 kg | Bendras: 3 432.0 kg
[Pridėti 12 konteinerių]
```

**Konvejeris (skirtingi svoriai):**
```
┌─────────────────────────────────────────┐
│  PR-MD2606-001 · Liepų · Onuškis        │
│  Tara: 19.0 kg | Pasverta: 7/12         │
├─────────────────────────────────────────┤
│    [    305.0    ] kg  (inputmode=decimal)│
│    Netto: 286.0 kg                       │
├─────────────────────────────────────────┤
│  [↩ Atšaukti paskutinį]  [Kitas →]       │
└─────────────────────────────────────────┘
```

### Žingsnis 6: Tikrinimas + patvirtinimas (BRC8 3.5, 3.8)
```
Priėmimo tikrinimas

Kilmės šalis: [Lietuva]  ← iš žingsnio 2, redaguojama

Tikrinimo rezultatas:
  [✅ Tinkama]  [⚠ Sąlyginai]  [❌ Netinkama]

Pastabos: [________________________________]

─────────────────────────────────────────
Suvestinė: 12 vnt. | 3 432.0 kg netto

[✓ Priimti pristatymą]  ← ConfirmDialog
```

**Jei NOK:**
- Containers → `QUARANTINE`
- Sukuriamas `non_conformances` įrašas
- Spausdinami karantino lipdukų (atskiras ZPL šablonas su "KARANTINAS" žyma)
- `MudAlert Error` su instrukcija

**Jei CONDITIONAL:**
- Privaloma pastaba
- Containers → `IN_STOCK`
- Sukuriamas `non_conformances` įrašas (informaciniam tikslui)

---

## Operatoriaus loginimas (BRC8 3.3)

`_currentUserId` = `await AuthService.GetUserIdAsync()` — vieną kartą `OnInitializedAsync`.

| Veiksmas | Lentelė | Laukas |
|---|---|---|
| Delivery sukūrimas | `deliveries` | `created_by_user_id` |
| Konteinerio priėmimas | `containers` | `received_by_user_id` |
| Priėmimo tikrinimas | `deliveries` | `inspection_by_user_id` |
| Lipdukas spausdinamas | `container_label_events` | `operator_id` |
| Lipdukas perspausdinamas | `container_label_events` | `operator_id` |
| Spausdinimas nepavyko | `container_label_events` | `operator_id` ← iš `print_jobs.created_by_user_id` |
| Svoris taisomas | `container_weight_corrections` | `corrected_by` |
| Sandėlio judėjimas | `stock_movements` | `created_by` (bug fix — buvo null) |
| Print job | `print_jobs` | `created_by_user_id` |
| Non-conformance | `non_conformances` | `detected_by` |

**Svarbu:** `LabelPrintWorker` yra background service — neturi user konteksto. Kai PRINT_FAILED, `operator_id` imamas iš `print_jobs.created_by_user_id`, ne iš worker thread.

---

## Svorio korekcija (BRC8 3.7)

`DeliveryView.razor` naudoja auto-save timer (500ms). **Svarbūs pakeitimai:**

1. Auto-save **intercept'inamas** prieš rašant — tikrinama `label_print_count > 0`
2. Jei `label_print_count > 0` → stabdomas auto-save → atidaromas `WeightCorrectionDialog`
3. Operatorius privalo įvesti priežastį → tik tada saugoma
4. Sukuriamas `container_weight_corrections` įrašas
5. Siūloma perspausdinti etiketę (ne privaloma — lipdukas su senu svoriu vis dar galioja kaip priėmimo dokumentas)

---

## Spausdinimo infrastruktūra

### Servisai
```csharp
ILabelPrintService
    PrintReceiptLabelAsync(int containerId, int stationId, int operatorId)
    PrintQuarantineLabelAsync(int containerId, int stationId, int operatorId)
    ReprintLabelAsync(int containerId, string reasonCode, string? reasonText, int operatorId)

ILabelTemplateService
    string RenderZpl(LabelTemplateType type, ContainerLabelData data)
    Task<byte[]> PreviewPngAsync(string zpl)  // Labelary API

IPrinterGateway
    Task<PrintResult> PrintAsync(string zpl, Printer printer)
    // HttpPrinterGateway: POST http://{pi_url}/print {"zpl":"..."}
    // StubGateway: ZPL į failą + Labelary PNG
```

### ZPL laukai (P0 — hardcoded, P1 — Scriban)
**Priėmimo lipdukas:**
- `container_code` + QR kodas
- Tiekėjas, žaliavos tipas, `origin_country`
- `net_weight` kg
- `delivery_date` ← **NE** `DateTime.Now` — BRC8 reikalavimas
- Sandėlys

**Karantino lipdukas:**
- Visos aukščiau esančios reikšmės
- Didelė **"KARANTINAS"** žyma raudona spalva
- `non_conformance.id` referensas

### ZPL techniniai parametrai
- Plotis: 108mm = 850 dots (200 DPI)
- Aukštis: nustatomas P1 su realia etikeete

### `LabelPrintWorker`
```csharp
// BackgroundService: kas 1s
// SemaphoreSlim(1) per printer_id — vienas job vienu metu
// PENDING → PROCESSING → DONE / FAILED
// Retry: max 3, exponential backoff (1s, 2s, 4s)
// Po galutinio FAILED:
//   1. print_jobs.status = FAILED
//   2. container_label_events INSERT (PRINT_FAILED, operator_id iš print_jobs.created_by_user_id)
```

---

## Tiekėjų patvirtinimas (BRC8 3.5)

### `SupplierApprovals.razor`
- Sąrašas su spalvomis: 🟢 Galioja / 🟡 Baigiasi per 30d / 🔴 Pasibaigęs / ⚪ Nepatvirtintas
- Naujo patvirtinimo pridėjimas: data, metodas, galiojimas, rizika, sertifikato nr.
- Kai naujas patvirtinimas → senas `is_current = 0` (application logika transakcijoje)
- Istorija — niekada neištrinama

### Integravimas
- Žingsnis 2 tikrina `business_partners.is_approved` ir `approval_expires_at`
- Blokavimo elgsena konfigūruojama `app_settings` lentele

---

## Non-conformance ir karantinas (BRC8 3.8)

### Karantino flow
```
Žingsnis 6: NOK
    → deliveries.inspection_result = 'NOK'
    → visi containers: status = 'QUARANTINE'
    → non_conformances INSERT (ref_type=DELIVERY, ref_id=delivery.id)
    → print_jobs INSERT (job_type=QUARANTINE_LABEL) kiekvienam container
    → MudAlert Error + instrukcija sandėlininkui
```

### `NonConformances.razor`
- Sąrašas su filtravimo galimybe
- Disposition valdymas: PENDING → ACCEPTED / REJECTED / QUARANTINED / REWORKED
- Pilnas audit trail: kas, kada, kokie veiksmai

### `QuarantineStock.razor`
- Visi containers su `status = QUARANTINE`
- Greitas vaizdas auditui

---

## Atsekamumas (BRC8 3.9 FUNDAMENTAL)

### P0 — dalinė trace (veikia dabar)

**Backward:** container kodas → delivery → tiekėjas → patvirtinimas → svoriai → lipdukų istorija

**Forward (dalinė):** tiekėjas / delivery → visi containers → kur jie yra dabar (sandėlyje / gamyboje / parduoti / nurašyti)

### P3 — pilna trace (po LOT modulio)
Forward trace papildoma: containers → LOT → gatava produkcija → pardavimų sąskaitos

### `TraceabilitySearch.razor`
```
[Container kodas arba delivery nr.] [🔍 Ieškoti]

Container: PR-MD2606-001/005
  Delivery: PR-MD2606-001 (2026-06-01, Jonas Petraitis)
  Tiekėjas: ✅ Patvirtintas iki 2027-01-01
  Žaliava: Medus liepų | Kilmė: Lietuva
  Svoris: 286.0 kg netto
  Tikrinimas: ✅ Tinkama (Petras Jonaitis, 2026-06-01 14:20)
  Etiketė: Išspausdinta 2026-06-01 14:23 (Petras Jonaitis)
  Statusas: IN_STOCK

[→ Visi šio delivery containers] [→ Visi šio tiekėjo containers]
```

---

## Kalibravimas (BRC8 6.4)

### `CalibrationLog.razor`
- Sąrašas stočių su kalibracijų datomis: 🟢/🟡/🔴
- Naujos kalibrацijos pridėjimas: data, sertif. nr., kas atliko
- Dashboard perspėjimas kai `next_calibration_date` arčiau nei 30d

---

## UI — planšetės stilius

### Principai
- `MaxWidth.Small` svėrimo puslapiuose
- `inputmode="decimal"` — skaitinė klaviatūra svorių laukams
- `xs="12"` visuose `MudGrid` — vienas stulpelis
- `min-height: 72px` visiems action mygtukams
- Spalvos iš esamos temos: Primary `#4f7cac`, Secondary `#7fb685`

### `wwwroot/css/warehouse.css`
```css
.tablet-action-btn {
    min-height: 72px !important;
    font-size: 1.2rem !important;
    border-radius: 12px !important;
}
.tablet-weight-display {
    font-size: 3rem;
    font-weight: 700;
    text-align: center;
    padding: 24px;
    border-radius: 16px;
    background: #f0f4ff;
    border: 2px solid #4f7cac;
}
.tablet-station-card {
    min-height: 100px;
    border-radius: 16px;
    cursor: pointer;
    transition: transform 0.1s;
}
.tablet-station-card:active { transform: scale(0.97); }
```

---

## Dialogai ir klaidos

### ConfirmDialog naudojimas

| Situacija | Tekstas | Spalva |
|---|---|---|
| Submit delivery | "{N} konteineriai, {kg} kg. Veiksmo atšaukti negalima." | Success |
| Atšaukti paskutinį | "Konteineris {code} bus pašalintas." | Warning |
| Baigti anksčiau | "Įvesta {x} iš {total}. Ar tikrai baigti?" | Warning |
| Keisti stotį | "Yra nepatvirtintų konteinerių. Jie bus prarasti." | Warning |
| Ištrinti spausdintuvą | "Neišspausdintos etiketės bus atšauktos." | Error |

### Snackbar pranešimai

| Situacija | Severity | Tekstas |
|---|---|---|
| Sėkmingas submit | Success | "Pristatymas {number} priimtas!" |
| Pridėta konteinerių | Success | "Pridėta {N} konteinerių" |
| Pi nepasiekiamas | Warning | "Spausdintuvas nepasiekiamas. Etiketės bus išspausdintos kai ryšys atsistatys." |
| Pasibaigęs patvirtinimas | Error | "Tiekėjas {name} neturi galiojančio patvirtinimo" |
| Kalibracija — perspėjimas | Warning | "Svarstyklės {station} — kalibracija baigiasi {date}" |
| Sėkmingas testas | Success | "Testo etiketė išspausdinta" |

### Specialūs dialogai

| Komponentas | Tikslas | Privalomi laukai |
|---|---|---|
| `ReprintReasonDialog` | Perspausdinimo priežastis | reason_code (dropdown) |
| `WeightCorrectionDialog` | Svorio korekcijos priežastis | reason (text) |
| `NonConformanceDialog` | Neatitikimo registravimas | description, severity |

---

## Esamo kodo pakeitimai — tiksliai

### C# modeliai
```
ContainerEnums.cs:
  ContainerType: BARREL, BUCKET           (BUCKET_GROUP pašalinta)
  ContainerStatus: + QUARANTINE

Container.cs:
  + WeighingMode, ReceivedByUserId
  + LastLabelPrintedAt, LabelPrintCount

Delivery.cs:
  + WeighingStatus, WeighingStationId
  + WeighingStartedAt, WeighingCompletedAt
  + CreatedByUserId, OriginCountry
  + InspectionResult, InspectionNotes, InspectionByUserId

DeliveryLine.cs:
  ContainerType: ENUM pakeistas į BARREL/BUCKET

BusinessPartner.cs:
  + IsApproved, ApprovalExpiresAt
  + SupplierRiskLevel, DefaultOriginCountry
  ← SVARBU: taip pat pataisyti EF Core snapshot
```

### `ContainerService.cs` + `IContainerService.cs`
Ištrinti: `GetLastContainerCodeAsync()`, `GetLastBucketCodeAsync()`

### `DeliveryService.cs`
- `GenerateDeliveryNumberAsync` → į `CreateDeliveryWithContainersAsync` transakciją
- `CreateDeliveryWithContainersAsync(delivery, lines, containers, operatorId)`
- Container kodai: `$"{delivery.DeliveryNumber}/{seq:D3}"`
- `StockMovement.CreatedBy = operatorId` (bug fix)

### `DeliveryCreate.razor`
- Žingsnis 1: stoties pasirinkimas (naujas)
- Žingsnis 2: tiekėjo patikrinimas + kilmės šalis iš default
- Žingsnis 6: tikrinimas + patvirtinimas (sujungta į vieną)
- Pašalinti: `_startId`, `_bucketStartId`, kodo generavimo logika
- `_warehouseId` → readonly iš stoties
- `"BUCKET_GROUP"` → `"BUCKET"` visur
- `tablet-action-btn` visiems mygtukams
- `inputmode="decimal"` skaičių laukams
- `MaxWidth.Small`
- Po submit → enqueue `print_jobs`

### `DeliveryView.razor`
- Spausdinimo statusas: inline `MudAlert`
- "Perspausdinti" mygtukas → `ReprintReasonDialog`
- Auto-save intercept: tikrinti `label_print_count > 0` → `WeightCorrectionDialog`

### `NordicBeesErpContext.cs`
- 11 naujų `DbSet<>`
- `ContainerLabelEvent` immutability:
```csharp
public override int SaveChanges() {
    var immutable = ChangeTracker.Entries<ContainerLabelEvent>()
        .Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted);
    if (immutable.Any())
        throw new InvalidOperationException("ContainerLabelEvent yra nekintamas (BRC8 3.3)");
    return base.SaveChanges();
}
```

### `Program.cs`
```csharp
builder.Services.AddHostedService<LabelPrintWorker>();
builder.Services.AddScoped<ILabelPrintService, LabelPrintService>();
builder.Services.AddScoped<ILabelTemplateService, ZplLabelTemplateService>();
builder.Services.AddScoped<IWeighingStationService, WeighingStationService>();
builder.Services.AddScoped<ISupplierApprovalService, SupplierApprovalService>();
builder.Services.AddScoped<INonConformanceService, NonConformanceService>();
builder.Services.AddScoped<IPrinterGateway, HttpPrinterGateway>();
```

### Grep + replace visame projekte
```
"BUCKET_GROUP" → "BUCKET"
ContainerType.BUCKET_GROUP → ContainerType.BUCKET
```

### `NavMenu.razor`
```
Sandėlis:
  + 🔍 Atsekamumas          /warehouse/traceability
  + ⚠  Neatitikimai          /warehouse/non-conformances
  + 🔒 Karantinas            /warehouse/quarantine
  + ✅ Tiekėjų patvirt.      /warehouse/supplier-approvals
Nustatymai:
  + 🖨  Spausdintuvai         /admin/printers
  + ⚖  Darbo vietos           /admin/weighing-stations
  + 📋 Spausdinimo žurnalas  /admin/print-jobs
  + 📐 Kalibravimas          /admin/calibration
```

---

## Nauji failai

```
Models/Printing/
    Printer.cs, WeighingStation.cs, PrintJob.cs
    ContainerLabelEvent.cs, ContainerWeightCorrection.cs
    LabelTemplate.cs, DocumentFile.cs
    PrintingEnums.cs, ContainerLabelData.cs

Models/Warehouse/
    SupplierApproval.cs, NonConformance.cs

Services/
    ILabelPrintService.cs + LabelPrintService.cs
    IPrinterGateway.cs + HttpPrinterGateway.cs + StubPrinterGateway.cs
    ILabelTemplateService.cs + ZplLabelTemplateService.cs
    LabelPrintWorker.cs
    IWeighingStationService.cs + WeighingStationService.cs
    ISupplierApprovalService.cs + SupplierApprovalService.cs
    INonConformanceService.cs + NonConformanceService.cs

Components/Pages/Warehouse/
    TraceabilitySearch.razor
    NonConformances.razor
    QuarantineStock.razor
    SupplierApprovals.razor

Components/Pages/Admin/
    PrinterSettings.razor
    WeighingStationSettings.razor
    PrintJobsLog.razor
    CalibrationLog.razor

Components/Dialogs/
    ReprintReasonDialog.razor
    WeightCorrectionDialog.razor
    NonConformanceDialog.razor
```

---

## Implementacijos fazės

### P0a — Core (1–2 savaitės, be hardware)
1. DB migracija — visos lentelės + pakeitimai
2. EF Core modeliai + DbContext (su ContainerLabelEvent immutability) + snapshot
3. `"BUCKET_GROUP"` → `"BUCKET"` grep + replace
4. `ContainerService` — išimti du metodus
5. `DeliveryService` — transakcija + container kodai + operatorId + bug fix
6. `IPrinterGateway` + `StubGateway` + Labelary PNG
7. `ILabelTemplateService` + hardcoded ZPL (108mm, delivery_date, receipt + quarantine)
8. `ILabelPrintService` (receipt + quarantine + reprint)
9. `LabelPrintWorker` (su PRINT_FAILED → operator_id iš print_jobs)
10. `Program.cs` registracija
11. `warehouse.css` + `App.razor` link
12. `DeliveryCreate.razor` — visi pakeitimai
13. `DeliveryView.razor` — spausdinimas + reprint + svorių korekcija intercept
14. `ReprintReasonDialog` + `WeightCorrectionDialog` + `NonConformanceDialog`

### P0b — BRC8 dokumentacija (1–2 savaitės)
15. `SupplierApprovals.razor`
16. `NonConformances.razor`
17. `QuarantineStock.razor`
18. `TraceabilitySearch.razor` (backward + dalinė forward)
19. `WeighingStationSettings.razor`
20. `PrinterSettings.razor` + testo spausdinimas
21. `PrintJobsLog.razor`
22. `CalibrationLog.razor`
23. `NavMenu.razor`

### P1 — Spausdintuvas rankose
24. Pi `printer_service.py` (Flask, `/print`, `/status`)
25. Pi Tailscale + wpa_supplicant
26. `HttpPrinterGateway`
27. ZPL kalibravimas su realia etikeete
28. `label_templates` DB + Scriban + admin preview

### P2 — Svarstyklės rankose
29. Pi `/scale` WebSocket
30. Real-time svoris UI

### P3 — LOT (po LOT modulio)
31. LOT etiketės
32. Pilna forward trace
33. Mass balance ataskaita

### P4 — Dokumentai
34. Packing list XLSX (ClosedXML)
35. CMR PDF (QuestPDF)
36. Kokybės sertifikatas PDF

---

## Sprendimai — visiškai užrakinti

| Klausimas | Sprendimas | Priežastis |
|---|---|---|
| Container kodas | `{delivery_nr}/{seq:D3}` transakcijoje | Race condition apsauga |
| BUCKET_GROUP | → BUCKET — kiekvienas atskiras | Kiekvienas kibiras unikalus |
| Delivery number | UNIQUE constraint + retry | Lygiagretumo apsauga |
| Pi internetas | Planšetės 5G hotspot + Tailscale | Fiksuotas IP |
| Spausdinimo eilė | `print_jobs` + `LabelPrintWorker` | Reliability + retry |
| Worker concurrency | `SemaphoreSlim(1)` per printer_id | Serial port negali lygiagrečiai |
| ZPL data | `delivery.delivery_date` | BRC8 3.3 — ne spausdinimo data |
| ZPL P0 | Hardcoded C# | Scriban tik kai spausdintuvas rankose |
| Stoties pasirinkimas | Kiekvienam delivery iš naujo | Nėra pasenusio state |
| Audit trail | `container_label_events` INSERT only | BRC8 3.3 |
| PRINT_FAILED operator | Iš `print_jobs.created_by_user_id` | Worker neturi user konteksto |
| Karantino lipdukas | Atskiras ZPL šablonas + QUARANTINE_LABEL job_type | Fizinis BRC8 reikalavimas |
| Auto-save intercept | Tikrinti `label_print_count > 0` | BRC8 3.7 korekcijų audit trail |
| `origin_country` | `deliveries` lygyje, default iš tiekėjo | UX — ne kiekvienas delivery_lines |
| `supplier_approvals.is_current` | Application logika transakcijoje | MariaDB nepalaiko partial unique |
| Įrašų saugojimas | Niekada netrinami | BRC8 3.3 retention |
| Kalibravimas | `weighing_stations` + `CalibrationLog` | BRC8 6.4 |
| Operatorius | `GetUserIdAsync()` vieną kartą wizard'e | Thread safety |
| `stock_movements.created_by` | Bug fix — `= operatorId` | BRC8 auditabilumas |
| Forward trace P0 | Dalinė — containers per tiekėją | Auditorius turi atsakymą |
| Wizard žingsniai | 6 (ne 7) — tikrinimas + patvirtinimas sujungta | UX planšetei |
| `BusinessPartner.cs` | Pridėti laukus + EF snapshot | EF Core drift apsauga |

