# NordicBeesERP — Sandėlio ženklinimo ir spausdinimo sistema
**Versija:** 1.0  
**Data:** 2026-06-01  
**Tikslas:** BRC8 atitinkanti kiekvieno sandėlio konteinerio identifikavimo sistema su automatiniu lipdukų spausdinimu

---

## Kontekstas ir tikslas

Kiekvienas fizinis konteineris (statinė, kibiras) patenkantis į sandėlį turi gauti unikalų identifikavimo lipduką priėmimo metu. Tai yra BRC8 reikalavimas ir sandėlio tvarkos pagrindas. Spausdinimas yra output — esmė yra atsekamumas.

**Dabartinė situacija:**
- Sandėlininkas rankiniu būdu rašo svorį ant popieriaus → vėliau suveda į Excel
- Konteineriai stovi be identifikacijos
- BRC8 auditorius negali atsekti konteinerio iki tiekėjo

**Po implementacijos:**
- Kiekvienas konteineris → unikalus kodas → lipdukas → pririštas prie tiekėjo ir priėmimo
- Svoris fiksuojamas svėrimo metu (rankiniu arba automatiniu būdu)
- Pilnas BRC8 audit trail kiekvienam ženklinimo veiksmui

---

## Sistemos ribos

### Šioje fazėje (P0-P1)
- Priėmimo lipdukų generavimas ir spausdinimas
- BARREL ir BUCKET — kiekvienas fizinis vienetas = atskiras įrašas su unikaliu kodu
- Vienodų ir skirtingų svorių svėrimo režimai
- BRC8 perspausdinimo audit trail
- Pi/Zebra spausdinimo infrastruktūra
- Planšetės touch UI

### Ne šioje fazėje
- LOT etiketės (išeinanti produkcija) — P3
- Packing list, CMR, kokybės sertifikatai — P4
- Real-time svarstyklių integracija — P2
- Scriban šablonų admin UI — po P1 (kai spausdintuvas rankose)

---

## Architektūra

### Srautas

```
Sandėlininkas → Pasirinkia stotį → Sukuria delivery →
Sveria konteinerius → Sistema generuoja kodus →
Išsaugoma DB → print_jobs enqueue →
LabelPrintWorker → Pi → USB → Zebra → Lipdukas
```

### Komponentų schema

```
DeliveryCreate.razor (wizard)
    └── WeighingStationSelect (žingsnis 1)
    └── Tiekėjas + data (žingsnis 2)
    └── Žaliavos tipas (žingsnis 3)
    └── Pakuotės tipas (žingsnis 4, tik medui)
    └── Svėrimas (žingsnis 5)
    └── Patvirtinimas + spausdinimas (žingsnis 6)

IDeliveryService.CreateDeliveryWithContainersAsync()
    └── Generuoja delivery_number (transakcijoje, UNIQUE)
    └── Generuoja container kodus: {delivery_number}/{seq:D3}
    └── Sukuria containers atomiškai
    └── Enqueue print_jobs

LabelPrintWorker (IHostedService)
    └── Kas 1s tikrina PENDING print_jobs
    └── SemaphoreSlim(1) per printer_id
    └── HttpPrinterGateway → Pi /print
    └── StubGateway → ZPL failas + Labelary PNG (dev)

Pi (Python Flask)
    └── POST /print → USB → Zebra ZM400
    └── GET /status → printer + scale būsena
    └── WS /scale → real-time svoris (P2)
```

---

## Tinklo architektūra

```
Planšetė (5G) ←→ WiFi Hotspot ←→ Pi
     ↓                                ↓
Tailscale VPN                   Tailscale VPN
     ↓                                ↓
ERP serveris ←————————————————→ Pi (fiksuotas 100.x.x.x)
```

**Principas:** Pi jungiasi prie planšetės 5G hotspot'o interneto. Tailscale suteikia Pi fiksuotą IP nepriklausomai nuo hotspot'o. ERP serveris kviečia Pi per Tailscale IP.

**Pi setup (vienkartinis):**
```bash
sudo apt install tailscale
tailscale up --authkey=<KEY>
# Užrašyti Tailscale IP → įvesti weighing_stations.pi_base_url
# Sukonfigūruoti wpa_supplicant planšetės SSID
sudo systemctl enable tailscaled printer-service
```

**Edge case — planšetė išjungta:** Pi neturi interneto → Tailscale neveikia → `print_jobs` lieka `PENDING` → kai planšetė grįžta → Pi reconnect → `LabelPrintWorker` automatiškai siunčia PENDING jobs.

---

## DB schema — pilna

### Naujos lentelės

#### `printers`
```sql
CREATE TABLE printers (
    id                  INT AUTO_INCREMENT PRIMARY KEY,
    name                VARCHAR(100) NOT NULL,
    location            VARCHAR(100) NOT NULL,
    endpoint_url        VARCHAR(200) NOT NULL,  -- http://100.x.x.x:5000 arba STUB
    connection_type     ENUM('HTTP','STUB') NOT NULL DEFAULT 'STUB',
    label_width_mm      DECIMAL(5,1) NOT NULL DEFAULT 108.0,
    label_height_mm     DECIMAL(5,1) NOT NULL DEFAULT 75.0,
    darkness            INT NOT NULL DEFAULT 25,
    dpi                 INT NOT NULL DEFAULT 200,
    is_active           TINYINT(1) NOT NULL DEFAULT 1,
    created_at          TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at          TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);
```

#### `weighing_stations`
```sql
CREATE TABLE weighing_stations (
    id                      INT AUTO_INCREMENT PRIMARY KEY,
    name                    VARCHAR(100) NOT NULL,      -- "Statinių priėmimas"
    warehouse_id            INT NOT NULL,
    printer_id              INT NOT NULL,
    pi_base_url             VARCHAR(200) NULL,          -- NULL = Pi dar nėra
    default_container_type  ENUM('BARREL','BUCKET') NULL,
    min_weight_kg           DECIMAL(5,3) NOT NULL DEFAULT 0.500,
    scale_protocol          ENUM('TOLEDO','METTLER','CAS','KERN','NONE') NOT NULL DEFAULT 'NONE',
    scale_regex             VARCHAR(200) NULL,
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
    id              INT AUTO_INCREMENT PRIMARY KEY,
    printer_id      INT NOT NULL,
    station_id      INT NULL,
    container_id    INT NOT NULL,
    job_type        ENUM('RECEIPT_LABEL','REPRINT') NOT NULL DEFAULT 'RECEIPT_LABEL',
    zpl_content     LONGTEXT NOT NULL,
    status          ENUM('PENDING','PROCESSING','DONE','FAILED','CANCELLED') NOT NULL DEFAULT 'PENDING',
    retry_count     INT NOT NULL DEFAULT 0,
    max_retries     INT NOT NULL DEFAULT 3,
    last_error      TEXT NULL,
    created_by_user_id INT NULL,
    created_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    processed_at    DATETIME NULL,
    done_at         DATETIME NULL,
    FOREIGN KEY (printer_id)   REFERENCES printers(id),
    FOREIGN KEY (station_id)   REFERENCES weighing_stations(id),
    FOREIGN KEY (container_id) REFERENCES containers(id)
);
```

#### `container_label_events` (BRC8 immutable audit log)
```sql
CREATE TABLE container_label_events (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    container_id    INT NOT NULL,
    event_type      ENUM('PRINTED','REPRINTED','CANCELLED') NOT NULL,
    print_job_id    INT NULL,
    reason_code     ENUM('DAMAGED','LOST','MISPRINT','OTHER') NULL,  -- privalomas kai REPRINTED
    reason_text     VARCHAR(200) NULL,
    operator_id     INT NULL,
    created_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    -- NIEKADA UPDATE, NIEKADA DELETE
    FOREIGN KEY (container_id) REFERENCES containers(id),
    FOREIGN KEY (print_job_id) REFERENCES print_jobs(id)
);
```

#### `label_templates`
```sql
CREATE TABLE label_templates (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    name            VARCHAR(100) NOT NULL,
    template_type   ENUM('RECEIPT_BARREL','RECEIPT_BUCKET','LOT_BARREL','LOT_BUCKET') NOT NULL,
    scriban_content LONGTEXT NOT NULL,
    label_width_mm  DECIMAL(5,1) NOT NULL DEFAULT 108.0,
    label_height_mm DECIMAL(5,1) NOT NULL DEFAULT 75.0,
    is_active       TINYINT(1) NOT NULL DEFAULT 1,
    is_default      TINYINT(1) NOT NULL DEFAULT 0,
    created_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);
```

#### `document_files` (struktūra ateičiai — P4)
```sql
CREATE TABLE document_files (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    ref_type        ENUM('DELIVERY','LOT','ORDER') NOT NULL,
    ref_id          INT NOT NULL,
    doc_type        ENUM('PACKING_LIST','CMR','QUALITY_CERT','RECEIPT_ACT') NOT NULL,
    file_path       VARCHAR(500) NOT NULL,
    original_filename VARCHAR(200) NOT NULL,
    file_size_bytes INT NULL,
    generated_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    generated_by    INT NULL
);
```

### Pakeitimai esamose lentelėse

#### `containers`
```sql
-- Pakeisti enum
ALTER TABLE containers
    MODIFY container_type ENUM('BARREL','BUCKET') NOT NULL;

-- Pridėti
ALTER TABLE containers
    ADD COLUMN weighing_mode        ENUM('MANUAL','SCALE') NOT NULL DEFAULT 'MANUAL',
    ADD COLUMN received_by_user_id  INT NULL,
    ADD COLUMN last_label_printed_at DATETIME NULL,
    ADD COLUMN label_print_count    INT NOT NULL DEFAULT 0;
```

**Pastaba:** `quantity` kolona paliekama (naudojama `remaining_quantity` logikoje). Reikšmė visada `1` kiekvienam konteineriui nuo šiol.

#### `deliveries`
```sql
ALTER TABLE deliveries
    ADD COLUMN weighing_status      ENUM('NOT_STARTED','IN_PROGRESS','COMPLETED') NOT NULL DEFAULT 'NOT_STARTED',
    ADD COLUMN weighing_station_id  INT NULL,
    ADD COLUMN weighing_started_at  DATETIME NULL,
    ADD COLUMN weighing_completed_at DATETIME NULL,
    ADD COLUMN created_by_user_id   INT NULL,
    ADD UNIQUE KEY uk_delivery_number (delivery_number),
    ADD FOREIGN KEY (weighing_station_id) REFERENCES weighing_stations(id),
    ADD FOREIGN KEY (created_by_user_id)  REFERENCES erp_users(id);
```

**Svarbu:** `UNIQUE` constraint ant `delivery_number` — apsauga nuo race condition kai du vartotojai vienu metu kuria delivery.

#### `delivery_lines`
```sql
-- Pakeisti enum
ALTER TABLE delivery_lines
    MODIFY container_type ENUM('BARREL','BUCKET') NOT NULL;
```

**Pastaba:** `total_gross_weight`, `total_tare_weight` kolonos DB egzistuoja bet `DeliveryLine.cs` modelyje nėra. Jos niekada nepildomos. Palikti kaip yra — nekeisti šioje fazėje.

---

## Container kodų sistema

### Formatas
```
{DELIVERY_NUMBER}/{SEQ:D3}

PR-MD2606-001/001   ← pirma statinė iš PR-MD2606-001
PR-MD2606-001/002   ← antra statinė
PR-BD2606-003/001   ← bičių duona iš kito delivery
```

**Separatorius `/`** — ne brūkšnelis, nes brūkšnelis jau naudojamas delivery numeryje.

### Generavimo logika
Kodai generuojami **tik** `CreateDeliveryWithContainersAsync` transakcijoje — ne UI metu:

```csharp
// Po delivery.Id gautas:
int seq = 1;
foreach (var container in containers)
{
    container.ContainerCode = $"{delivery.DeliveryNumber}/{seq:D3}";
    seq++;
}
```

**Race condition apsauga:** `UNIQUE` constraint ant `deliveries.delivery_number` + retry logika `GenerateDeliveryNumberAsync` jei konfliktas.

### Delivery numerio generavimas (nauja logika)
Numeris generuojamas **transakcijoje**, ne UI žingsnyje:

```csharp
// Transakcijos viduje prieš containers:
var number = await GenerateAndReserveDeliveryNumberAsync(materialCode, context);
delivery.DeliveryNumber = number;
await context.SaveChangesAsync(); // UNIQUE constraint čia saugoja
```

UI rodo `"Bus sugeneruotas"` — tik po sėkmingo submit parodo tikrą numerį.

---

## Darbo vietos (Weighing Stations)

### Koncepcija
**1 darbo vieta = 1 Pi + 1 Zebra + 1 svarstyklės + 1 planšetė**

Planšetė yra mobili — **viena planšetė, kelios darbo vietos**. Sandėlininkas pasirenka darbo vietą kiekvieno naujo delivery pradžioje.

### Stoties pasirinkimas
- Saugojama `ProtectedSessionStorage` (ne Local — gyvena tik naršyklės sesiją)
- Pasirinkimas galioja vienam delivery — kiekvienam naujam klausia iš naujo
- Iš stoties automatiškai užpildomas `warehouse_id` → delivery `warehouse_id` tampa **readonly**

### Pi online statuso tikrinimas
Prieš stoties pasirinkimą sistema tikrina `GET /status` kiekvieno Pi:
- `🟢 Spausdintuvas prisijungęs` — Pi pasiekiamas
- `⚪ Spausdintuvas neprijungtas` — Pi nepasiekiamas (lipdukai bus spausdinami kai ryšys atsistatys)
- `pi_base_url = NULL` — Pi dar nekonfigūruotas

Stotis **neblokuojama** jei Pi offline — tik informacinis ženklas. Spausdinimas vyks vėliau per eilę.

---

## Wizard flow — DeliveryCreate.razor

### Žingsnis 1: Darbo vieta
```
Pasirinkite darbo vietą

[🛢 Statinių priėmimas]      Onuškio žaliavos sandėlis  🟢
[🪣 Kibirų priėmimas]        Onuškio žaliavos sandėlis  🟢
[🌿 Bičių duona / Kita]      Juodupės gamyba             ⚪
```

Paspaudus → `station_id` → `ProtectedSessionStorage` → automatiškai `warehouse_id`.

### Žingsnis 2: Tiekėjas + data
- Tiekėjas: `MudAutocomplete` (kaip dabar) + "Sukurti naują" dialog
- Data: `MudDatePicker` (default šiandien)
- Sandėlys: **readonly** `MudChip` su tooltip "Nustatytas pagal darbo vietą"

### Žingsnis 3: Žaliavos tipas
Identiškas dabartiniam.

### Žingsnis 4: Pakuotės tipas (tik medui)
`BARREL` arba `BUCKET`. Ne-medaus žaliavoms — automatiškai `BUCKET`, žingsnis praleidžiamas.

### Žingsnis 5: Svėrimas

**Režimas A — Vienodi svoriai:**
```
Taros svoris: [16 kg] [19 kg] [Kitas: ____]
Kiekis:       [12   ]
Brutto:       [305.0] kg
─────────────────────────────────
Netto vienai: 286.0 kg
Bendras netto: 3 432.0 kg

[Pridėti 12 konteinerių]
```

**Režimas B — Skirtingi svoriai (konvejeris):**
```
┌─────────────────────────────────────────┐
│  PR-MD2606-001  ·  Liepų  ·  Onuškis   │
│  Tara: 19.0 kg                          │
├─────────────────────────────────────────┤
│                                         │
│    [    305.0    ] kg  ← inputmode=decimal │
│    Netto: 286.0 kg                      │
│                                         │
├─────────────────────────────────────────┤
│  Pasverta: 7/12    Σ 2 002.0 kg netto   │
│                                         │
│  [↩ Atšaukti paskutinį]  [Kitas →]      │
└─────────────────────────────────────────┘
```

### Žingsnis 6: Patvirtinimas
```
Suvestos pozicijos:
┌──────────────────────────────────────────┐
│ Kodas          │ Brutto │ Tara │ Netto   │
│ (bus sukurtas) │ 305.0  │ 19.0 │ 286.0  │
│ ...            │ ...    │ ...  │ ...    │
└──────────────────────────────────────────┘
Viso: 12 vnt. | 3 432.0 kg netto

[✓ Priimti pristatymą]
```

Po patvirtinimo → `ConfirmDialog` → submit → navigacija į `DeliveryView`.

---

## Spausdinimo infrastruktūra

### `ILabelPrintService`
```csharp
Task PrintReceiptLabelAsync(int containerId, int stationId, int operatorId);
Task ReprintLabelAsync(int containerId, string reasonCode, string? reasonText, int operatorId);
```

Abiejų metodų rezultatas:
1. Generuoja ZPL iš `ILabelTemplateService`
2. Įrašo `print_jobs` (status: PENDING)
3. Įrašo `container_label_events` (PRINTED arba REPRINTED)
4. Atnaujina `containers.label_print_count++`

### `ILabelTemplateService`
```csharp
string RenderZpl(LabelTemplateType type, ContainerLabelData data);
Task<byte[]> PreviewPngAsync(string zpl); // Labelary API
```

**P0 fazėje:** hardcoded ZPL konstanta C# kode.
**P1 fazėje:** `label_templates` DB įrašas + Scriban render.

### ZPL šablonas — techniniai parametrai
- Plotis: 108mm = 850 dots (200 DPI)
- Aukštis: nustatomas P1 kai spausdintuvas rankose
- QR kodas: `container_code` (pilnas, su `/`)
- Laukai: container_code, tiekėjas, žaliavos tipas, net_weight, data, sandėlys

### `LabelPrintWorker`
```csharp
public class LabelPrintWorker : BackgroundService
{
    // Kas 1s tikrina PENDING print_jobs
    // SemaphoreSlim(1) per printer_id — vienas job vienu metu
    // PENDING → PROCESSING → DONE / FAILED
    // Retry: max 3 kartai, exponential backoff
    // Po 3 nesėkmių → FAILED, operator alert
}
```

### `IPrinterGateway`
```csharp
interface IPrinterGateway {
    Task<PrintResult> PrintAsync(string zpl, Printer printer);
}

// HttpPrinterGateway: POST http://{pi_url}/print {"zpl":"..."}
// StubPrinterGateway: logina ZPL į failą + Labelary PNG preview
```

---

## Pi pusė

### `printer_service.py` (Python Flask)
```python
# Endpoints:
GET  /status  → { "printer": "ready|error|paper_out", "scale": "connected|disconnected" }
POST /print   → { "zpl": "^XA...^XZ" } → 200 OK | 500 error
WS   /scale   → real-time: { "weight": 1.234, "stable": true } (P2)

# Serial port: /dev/ttyUSB0 (USB) arba /dev/ttyS0 (RS-232)
# Baud rate: konfigūruojama pagal Zebra nustatymus
```

### Svarstyklių protokolai (P2)
`weighing_stations.scale_protocol` + `scale_regex` — konfigūruojama per admin UI.

Palaikomi protokolai: TOLEDO, METTLER, CAS, KERN, NONE (rankinis).

---

## Perspausdinimas — BRC8 reikalavimas

### `ReprintReasonDialog`
```
⚠️ Konteineris PR-MD2606-001/003 jau turėjo 1 etiketę.
BRC8 reikalauja nurodyti priežastį.

Priežastis: [Lipdukas sugadintas ▾]
             Lipdukas pamestas
             Spausdinimo klaida
             Kita → [Aprašymas: ___________]

[Atšaukti]  [Perspausdinti ⚠]
```

Mygtukas `Disabled` kol priežastis nepasirinkta.

### `container_label_events` immutability
EF Core konfigūracija `OnModelCreating`:
```csharp
modelBuilder.Entity<ContainerLabelEvent>(entity => {
    entity.ToTable("container_label_events");
    // Nėra Update/Delete operacijų
});
```

`SaveChanges` override tikrina — jei `ContainerLabelEvent` state == Modified/Deleted → throw `InvalidOperationException`.

---

## UI — planšetės stilius

### Principai
- Touch-first — visi action mygtukai min 72px aukščio
- `MaxWidth.Small` svėrimo puslapiuose
- `inputmode="decimal"` skaičių laukams — iškviečia skaitinę klaviatūrą
- `xs="12"` visuose `MudGrid` laukuose — ne du stulpeliai
- Didelis šriftas svoriui: `font-size: 1.8rem`
- Spalvos: esamos temos (`Primary = "#4f7cac"`, `Secondary = "#7fb685"`)

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
    letter-spacing: 2px;
}
.tablet-station-card {
    min-height: 100px;
    border-radius: 16px;
    cursor: pointer;
    transition: transform 0.1s;
}
.tablet-station-card:active { transform: scale(0.97); }
.tablet-counter {
    font-size: 1.5rem;
    font-weight: 600;
    color: #4f7cac;
}
```

---

## Dialogai ir klaidos — pilnas sąrašas

### Patvirtinimo dialogai (`ConfirmDialog`)

| Situacija | Pavadinimas | Tekstas | Spalva |
|---|---|---|---|
| Submit delivery | "Patvirtinti pristatymą?" | "{N} konteineriai, {kg} kg netto. Veiksmo atšaukti negalima." | Success |
| Atšaukti paskutinį | "Atšaukti paskutinį?" | "Konteineris {code} bus pašalintas." | Warning |
| Baigti anksčiau | "Baigti svėrimą?" | "Įvesta {x} iš {total}. Ar tikrai baigti?" | Warning |
| Keisti stotį | "Keisti darbo vietą?" | "Yra nepatvirtintų konteinerių. Jie bus prarasti." | Warning |
| Ištrinti spausdintuvą | "Ištrinti spausdintuvą?" | "Neišspausdintos etiketės bus atšauktos." | Error |

### Snackbar pranešimai

| Situacija | Severity | Tekstas |
|---|---|---|
| Sėkmingas submit | Success | "Pristatymas {number} priimtas!" |
| Pridėta konteinerių | Success | "Pridėta {N} konteinerių" |
| Pi nepasiekiamas | Warning | "Spausdintuvas nepasiekiamas. Etiketės bus išspausdintos kai ryšys atsistatys." |
| Testo etiketė | Success | "Testo etiketė išspausdinta" |
| Testo klaida | Error | "Nepavyko pasiekti spausdintuvo: {error}" |
| Dublikatas | Error | "Konteineris {code} jau registruotas sistemoje" |
| Išsaugojimo klaida | Error | "Nepavyko išsaugoti: {innerMessage}" |

### Inline alerts (`MudAlert`)

| Situacija | Severity | Vieta |
|---|---|---|
| Nėra aktyvių stočių | Warning | Žingsnis 1 |
| Pi offline | Warning | Stoties kortelė |
| Spausdinamas | Info | DeliveryView |
| Išspausdinta | Success | DeliveryView |
| Nepavyko spausdinti | Error | DeliveryView + [Bandyti dar kartą] |
| Nulis svoris | Warning | Inline po svorių lauku |

---

## Nauji puslapiai ir komponentai

### Sandėlio modulis
| Puslapis | URL | Tikslas |
|---|---|---|
| `DeliveryCreate.razor` | `/warehouse/deliveries/new` | Esamas + wizard žingsniai |
| `DeliveryView.razor` | `/warehouse/deliveries/{id}` | Esamas + spausdinimo statusas |

### Admin modulis
| Puslapis | URL | Tikslas |
|---|---|---|
| `PrinterSettings.razor` | `/admin/printers` | Spausdintuvų konfigūracija + testavimas |
| `WeighingStationSettings.razor` | `/admin/weighing-stations` | Darbo vietų konfigūracija |
| `PrintJobsLog.razor` | `/admin/print-jobs` | Eilės stebėjimas, reprint, statusai |

### Dialogo komponentai
| Komponentas | Tikslas |
|---|---|
| `ReprintReasonDialog.razor` | BRC8 perspausdinimo priežastis |

---

## Nauji C# failai — pilnas sąrašas

```
Models/Printing/Printer.cs
Models/Printing/WeighingStation.cs
Models/Printing/PrintJob.cs
Models/Printing/ContainerLabelEvent.cs
Models/Printing/LabelTemplate.cs
Models/Printing/DocumentFile.cs
Models/Printing/PrintingEnums.cs
Models/Printing/ContainerLabelData.cs        ← ZPL render DTO

Services/ILabelPrintService.cs
Services/LabelPrintService.cs
Services/IPrinterGateway.cs
Services/HttpPrinterGateway.cs
Services/StubPrinterGateway.cs               ← + Labelary PNG
Services/ILabelTemplateService.cs
Services/ZplLabelTemplateService.cs          ← hardcoded P0, Scriban P1
Services/LabelPrintWorker.cs                 ← IHostedService
Services/IWeighingStationService.cs
Services/WeighingStationService.cs
```

---

## Esamo kodo pakeitimai — tiksliai kas keičiasi

### `ContainerEnums.cs`
```csharp
// PRIEŠ:
public enum ContainerType { BARREL, BUCKET_GROUP }
// PO:
public enum ContainerType { BARREL, BUCKET }
```

### `ContainerService.cs` + `IContainerService.cs`
- **Ištrinti:** `GetLastContainerCodeAsync()`, `GetLastBucketCodeAsync()`
- Šie metodai nebereikalingi — kodai generuojami automatiškai

### `DeliveryService.cs`
- `GenerateDeliveryNumberAsync` → perkelti į `CreateDeliveryWithContainersAsync` transakcijos vidų
- `CreateDeliveryWithContainersAsync` → po delivery sukūrimo generuoti container kodus

### `DeliveryCreate.razor`
- Pridėti žingsnis 1 (stoties pasirinkimas)
- Išimti `_startId`, `_bucketStartId` kintamuosius
- Išimti `OnInitializedAsync` kodo generavimo logiką
- `_warehouseId` → readonly, iš stoties
- `_deliveryNumber` → rodomas tik po submit
- Visi `"BUCKET_GROUP"` → `"BUCKET"`
- Visi action mygtukai + `tablet-action-btn`
- `inputmode="decimal"` skaičių laukams
- `MaxWidth.Small`
- Po submit → enqueue print_jobs

### `DeliveryView.razor`
- Pridėti spausdinimo statuso sekciją (inline alert)
- Pridėti "Perspausdinti" mygtuką prie kiekvieno konteinerio
- `GetContainerTypeName("BUCKET_GROUP")` → palikti backward compat

### `NordicBeesErpContext.cs`
- Pridėti 6 naujus `DbSet<>`
- `OnModelCreating` — nauji entity konfigūravimai
- `ContainerLabelEvent` — immutability apsauga `SaveChanges` override

### `Program.cs`
```csharp
builder.Services.AddHostedService<LabelPrintWorker>();
builder.Services.AddScoped<ILabelPrintService, LabelPrintService>();
builder.Services.AddScoped<ILabelTemplateService, ZplLabelTemplateService>();
builder.Services.AddScoped<IWeighingStationService, WeighingStationService>();
builder.Services.AddScoped<IPrinterGateway, HttpPrinterGateway>(); // arba Stub pagal env
```

### `NavMenu.razor`
```
Nustatymai grupė:
  + 🖨 Spausdintuvai        /admin/printers
  + ⚖ Darbo vietos          /admin/weighing-stations
  + 📋 Spausdinimo žurnalas /admin/print-jobs
```

### `wwwroot/css/`
- Sukurti `warehouse.css`
- Pridėti `<link>` į `App.razor`

### Grep + replace visame projekte
```
"BUCKET_GROUP" → "BUCKET"  (C# kodas, Razor, test duomenys)
```

---

## Implementacijos fazės

### P0 — veikia be hardware (dabar)
1. DB migracija — visos naujos lentelės + pakeitimai
2. EF Core modeliai + DbContext + snapshot
3. `"BUCKET_GROUP"` → `"BUCKET"` grep + replace
4. `ContainerService` — išimti du metodus
5. `DeliveryService` — delivery number į transakciją + container kodai
6. `IPrinterGateway` + `StubGateway` (ZPL failas + Labelary PNG)
7. `ILabelTemplateService` + hardcoded ZPL (108mm, 200dpi)
8. `ILabelPrintService` + `LabelPrintService`
9. `LabelPrintWorker`
10. `Program.cs` registracija
11. `warehouse.css` + `App.razor` link
12. `DeliveryCreate.razor` — visi pakeitimai
13. `DeliveryView.razor` — spausdinimo statusas + reprint
14. `ReprintReasonDialog.razor`
15. `WeighingStationSettings.razor` (admin)
16. `PrinterSettings.razor` (admin) + testas
17. `PrintJobsLog.razor` (admin)
18. `NavMenu.razor` — nauji įrašai

### P1 — spausdintuvas rankose
19. Pi `printer_service.py` — Flask + `/print` + `/status`
20. Pi Tailscale setup + wpa_supplicant
21. `HttpPrinterGateway` — realus HTTP
22. ZPL šablono kalibravimas su realia etikeete
23. `label_templates` seed + Scriban + admin preview

### P2 — svarstyklės rankose
24. Pi `/scale` WebSocket
25. `IWeighingStationService` WebSocket klientas
26. `WeighingStation.razor` real-time svoris
27. Svarstyklių protokolo konfigūracija admin UI

### P3 — LOT etiketės (po LOT modulio)
28. `LabelTemplateType.LOT_BARREL`, `LOT_BUCKET`
29. `ILabelPrintService.PrintLotLabelsAsync(lotId)`

### P4 — dokumentai
30. `IDocumentGenerationService` impl
31. Packing list XLSX (ClosedXML)
32. CMR PDF (QuestPDF)
33. Kokybės sertifikatas PDF

---

## Operatoriaus loginimas — BRC8 reikalavimas

### Infrastruktūra
`ErpUser` modelis ir `IAuthService` jau egzistuoja. `GetUserIdAsync()` grąžina prisijungusio vartotojo `id`.

### Kaip operatorius perduodamas
`DeliveryCreate.razor` `OnInitializedAsync` vieną kartą gauna `_currentUserId`:
```csharp
_currentUserId = await AuthService.GetUserIdAsync();
```
Šis `int?` perduodamas į visus servisų metodus kurie atlieka sandėlio veiksmus.

### Kas fiksuojama

| Veiksmas | Kur saugoma | Laukas |
|---|---|---|
| Delivery sukūrimas | `deliveries` | `created_by_user_id` |
| Konteinerio priėmimas | `containers` | `received_by_user_id` |
| Lipdukas spausdinamas | `container_label_events` | `operator_id` |
| Lipdukas perspausdinamas | `container_label_events` | `operator_id` |
| Sandėlio judėjimas | `stock_movements` | `created_by` (jau yra, bet perduodamas `null` — taisoma) |
| Print job sukūrimas | `print_jobs` | `created_by_user_id` |

### DB pakeitimai

```sql
-- deliveries: pridėti
ADD COLUMN created_by_user_id INT NULL,
ADD FOREIGN KEY (created_by_user_id) REFERENCES erp_users(id);

-- print_jobs: pridėti
ADD COLUMN created_by_user_id INT NULL;

-- containers.received_by_user_id — jau planuota ✅
-- container_label_events.operator_id — jau planuota ✅
-- stock_movements.created_by — jau yra DB, taisomas null bug
```

### Servisų metodų parašai su operatorId

```csharp
// IDeliveryService
Task<int> CreateDeliveryWithContainersAsync(
    Delivery delivery, List<DeliveryLine> lines,
    List<Container> containers, int operatorId);

// ILabelPrintService
Task PrintReceiptLabelAsync(int containerId, int stationId, int operatorId);
Task ReprintLabelAsync(int containerId, string reasonCode, string? reasonText, int operatorId);
```

### Klaida esame kode — taisoma
`DeliveryService.CreateDeliveryWithContainersAsync` šiuo metu `StockMovement.CreatedBy = null`. Po pakeitimo:
```csharp
CreatedBy = operatorId
```

---

## Kritiniai sprendimai — užrakinti

| Klausimas | Sprendimas |
|---|---|
| Container kodas | `{delivery_number}/{seq:D3}` — generuojamas DB transakcijoje |
| BUCKET_GROUP | Pakeisti į BUCKET — kiekvienas kibiras atskiras įrašas |
| Delivery number race condition | UNIQUE constraint + retry transakcijoje |
| Pi komunikacija | HTTP REST per Tailscale |
| Pi internetas | Planšetės 5G hotspot + Tailscale ant Pi |
| Spausdinimo eilė | `print_jobs` DB + `LabelPrintWorker` IHostedService |
| Worker konkurencija | `SemaphoreSlim(1)` per printer_id |
| ZPL šablonas P0 | Hardcoded C# — Scriban tik po P1 |
| Stoties pasirinkimas | Kiekvienam delivery iš naujo, `ProtectedSessionStorage` |
| BRC8 audit | `container_label_events` — INSERT only, niekada UPDATE/DELETE |
| Perspausdinimas | Privaloma priežastis, `ReprintReasonDialog` |
| UI stilius | Touch-first, `warehouse.css`, `MaxWidth.Small` |
| `delivery_lines.total_gross_weight` | Palikti — nekeisti šioje fazėje |
| Operatoriaus loginimas | `GetUserIdAsync()` vieną kartą wizard'e → perduodamas į visus servisus |
| `stock_movements.created_by = null` | Bug — taisomas, perduodamas `operatorId` |

---

## Žinomi apribojimai ir ateities darbai

- ZPL šablonas bus perrašytas P1 kai spausdintuvas rankose — tai normalu
- Svarstyklių protokolas konfigūruojamas tik P2 — P0/P1 rankinis svoris
- Dokumentų generavimas (packing list, CMR) — P4
- Multi-printer routing (skirtingi LOT tipai → skirtingi spausdintuvai) — P3+
- `document_files` lentelė sukurta P0 migracijoje bet naudojama tik P4
