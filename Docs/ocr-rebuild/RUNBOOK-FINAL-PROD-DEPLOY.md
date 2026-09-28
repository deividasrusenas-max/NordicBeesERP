# Galutinio Etapų 1–4 prodo deploy'o instrukcija (D-037)

Sudaryta: 2026-09-28 (D-048 Part C). Vienas dokumentas visam Etapų 1–4 kodo ir schemos
perkėlimui iš `main` (dabar prode `v0.17.91`) į `production` šaką. Vykdo **žmogus** —
šis dokumentas neturi jokio DDL, paleisto iš agento, ir agentas niekada nesijungia prie
prodo (`AGENTS.md`).

**Kaip skaityti šį dokumentą.** Kiekvienas žingsnis, kurio šis dokumentas negalėjo
patikrinti (nes prodas agentui nepasiekiamas jokiomis aplinkybėmis), pažymėtas
**⚠ SAVININKAS PATIKRINA** su konkrečiu klausimu. Visos DDL eilutės žemiau nukopijuotos
tiesiai iš `Migrations/*.cs` failų `Up()` metodo — ne sugalvotos — ir jų rezultatas
patikrintas prieš DEV MySQL (`100.110.26.80`, `nordic_bees_erp`) per
`information_schema` 2026-09-28.

---

## 1. Prerequisites

### 1.1 Staginge švari pradžia atlikta ir išmatuota

D-031 kriterijus 1 reikalauja, kad visi kieti vartai veiktų ir būtų patikrinti staging
patikra ant prodo klono. D-047 nustato, kad švari pradžia staginge yra **kitas žingsnis
po šios sesijos** (`Migrations/Scripts/clean-start-expenses.sql`, Part A + Part B masinis
įkėlimas) — **⚠ SAVININKAS PATIKRINA:** ar staginge švari pradžia jau įvykdyta, visi
originalūs PDF pakartotinai įkelti per `/expenses/bulk-upload`, ir Etapo 3 S5 / Etapo 4
kriterijaus 3 matavimas (40 dev + 20 hold-out) atliktas su tais duomenimis. Šis dokumentas
neturi būti vykdomas anksčiau.

### 1.2 D-031 kriterijų būsena

| # | Kriterijus | Būsena prieš šį deploy'ą |
|---|---|---|
| 1 | Visi kieti vartai veikia (dublikatas, EN 16931 aritmetika, datos, tiekėjas, PVM kodas, IBAN, PVM tarifas) — testai + staging patikra | **⚠ SAVININKAS PATVIRTINA** po §1.1 |
| 2 | Tiekėjas randamas be spėjimų | Kodas `main` (Etapas 2, D-044) |
| 3 | Ekstrakcija ant realaus korpuso (40 dev + 20 hold-out) | **⚠ SAVININKAS PATVIRTINA** — priklauso nuo §1.1 |
| 4 | Golden-file regresija | **⚠ SAVININKAS PATVIRTINA** — priklauso nuo §1.1 |
| 5 | Prodo auditas (60 sąskaitų, 0 tylių klaidų) | Prasideda **po** šio deploy'o — žr. §9 |
| 6 | Peržiūros eilė gyva (≤ 5 d.d.) | C2 (`IReviewQueueAgingService`) kodas `main`, veiks nuo go-live |

Kriterijai 3–4 turi būti **TAIP** prieš tęsiant su §2 toliau — šis dokumentas nenustato,
kada tai bus, tik kad be jų deploy'as nevykdomas (D-031 principas: kokybė prieš greitį).

### 1.3 Buhalterio patvirtinimai

- **⚠ SAVININKAS/BUHALTERIS PATVIRTINA:** PVM tarifai, įskaitant Estijos datą (kada ir
  koks tarifas galioja EE tiekėjams — `VatRateValidator`/`VAT_RATE_NOT_ALLOWED` D-040
  slenksčiai naudoja šį sąrašą).
- **⚠ SAVININKAS/BUHALTERIS PATVIRTINA:** `ZERO_VAT_NO_BASIS` teisinio pagrindo
  formuluotės, kurios laikomos priimtinomis (0 % PVM be nurodyto pagrindo).

### 1.4 D-047 punkto 2 pagrindinių duomenų taisymai — pakartoti prode

D-047 punktas 1 (2026-09-27) šiuos taisymus atliko **tik staginge**, prieš pakartotinį
įkėlimą. Tas pats turi būti pakartota **prode**, prodo švarios pradžios metu (§8), **PRIEŠ**
paleidžiant `clean-start-expenses.sql` Section 3 (DELETE). Sąraše esantys ID yra staging
partnerių ID — **⚠ SAVININKAS PATIKRINA:** ar tie patys skaitiniai ID egzistuoja ir prode
su tais pačiais duomenimis (staging buvo prodo klonas D-029, bet nuo tada galėjo nutolti);
jei ne, atitinkami įrašai prode identifikuojami pagal tą patį kriterijų (dubliuotas
pavadinimas / PVM kodas / šalis), ne pagal ID:

1. **11 šiukšlinių dubliuotų partnerių ištrinti:** ID 381, 377, 386, 396, 79, 92, 89, 78,
   333, 85, 378.
2. **`bank_account` išvalytas:** partneriai 326, 328.
3. **Asmens kodas pašalintas iš `company_code`:** partneris 185.
4. **Šalis LT priskirta:** partneriai 371, 406. **Šalis EE priskirta:** partneris 374.
5. **PVM kodai pašalinti iš `company_code`:** partneriai 373, 375, 376, 379, 380, 406, 419.

Šie taisymai yra rankiniai `UPDATE`/`DELETE` sakiniai ant `business_partners` — ne DDL, bet
vis tiek **žmogaus veiksmas prieš tikrą duomenų bazę**, ne agento.

---

## 2. Schemos pakeitimai nuo v0.17.91

**Patikrinta 2026-09-28 prieš DEV `nordic_bees_erp` (`100.110.26.80`) per
`information_schema`:** `__EFMigrationsHistory` DEV bazėje po `20260910095703_...` turi
tiksliai šiuos tris papildomus įrašus (jokių kitų): `20260926131956_...`,
`20260926234735_...`, `20260927011841_...`. Visų `ProductVersion` reikšmė DEV bazėje —
`8.0.0` (patikrinta paskutinių trijų eilučių užklausa); tas pats turi būti naudojamas
naujuose prodo `__EFMigrationsHistory` įrašuose.

**⚠ SAVININKAS PATIKRINA PRIEŠ PRADEDANT §2:** ar prodo `__EFMigrationsHistory` jau turi
įrašą `20260915120000_AddFilesTableAndExpenseInvoiceFileId`. D-030 (2026-09-25) parodo, kad
šios migracijos DDL (`files` lentelė + `expense_invoices.file_id` stulpelis) **jau
pritaikytas prode rankiniu būdu** (`reapply-files.sql` + `ALTER TABLE ... ADD COLUMN
file_id`), o schemos diff prodas↔staging tada rodė 0 skirtumų. Bet D-030 **nemini**
`__EFMigrationsHistory` eilutės įterpimo (skirtingai nei `20260708120000_DeliverySignatureColumns`,
kur D-029 aiškiai aprašo rankinį įrašą). Jei istorijos eilutės nėra, `dotnet ef database
update` bandys pritaikyti šią migraciją iš naujo prieš jau egzistuojančią lentelę/stulpelį
ir nukris su „lentelė/stulpelis jau yra" klaida — tokiu atveju eilutė įterpiama rankiniu
būdu PRIEŠ paleidžiant §2.2 toliau, ta pati forma kaip žemiau (2.1 skiltis), su
`MigrationId = '20260915120000_AddFilesTableAndExpenseInvoiceFileId'`.

Patvirtinimo komanda savininkui prieš pradedant (prieš prodo DB, ne DEV):

```sql
SELECT MigrationId FROM __EFMigrationsHistory WHERE MigrationId LIKE '2026091%' OR MigrationId LIKE '2026092%' ORDER BY MigrationId;
```

Jei sąraše trūksta `20260915120000_...`, savininkas įterpia:

```sql
INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES
  ('20260915120000_AddFilesTableAndExpenseInvoiceFileId', '8.0.0');
```

### 2.1 `20260926131956_AlterExpenseInvoiceLineUnitPricePrecision` (D-039)

DDL (iš `Migrations/20260926131956_AlterExpenseInvoiceLineUnitPricePrecision.cs` `Up()`,
MariaDB sintaksė identiška MySQL šiai operacijai):

```sql
ALTER TABLE expense_invoice_lines
  MODIFY COLUMN unit_price DECIMAL(18,6) NULL;
```

Patikrinta DEV: `expense_invoice_lines.unit_price` = `decimal(18,6)`, `NULL` leidžiama. Jokių
duomenų reikšmės nekeičiamos — tik plotesnė tikslumo riba (kuro kainos turi 3 skaičius po
kablelio, medžiagų — 4; senoji `decimal(12,2)` juos apkarpydavo).

```sql
INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES
  ('20260926131956_AlterExpenseInvoiceLineUnitPricePrecision', '8.0.0');
```

### 2.2 `20260926234735_AddSupplierBankAccounts` (Etapas 2 S4, D-044 Q2/Q5/Q11)

```sql
CREATE TABLE supplier_bank_accounts (
  id                INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  partner_id        INT NOT NULL,
  iban              VARCHAR(34) NOT NULL,
  source            VARCHAR(20) NOT NULL,
  source_invoice_id INT NULL,
  is_active         TINYINT(1) NOT NULL,
  created_at        DATETIME(6) NOT NULL,
  created_by        VARCHAR(100) NULL,
  KEY idx_iban (iban),
  UNIQUE KEY uq_partner_iban (partner_id, iban)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
```

**Be užsienio raktų** (D-044 Q11 — sąmoningas sprendimas, žr. `PLAN-ETAPAS2.md` §1.4).
Patikrinta DEV: lentelė turi 8 stulpelius, atitinka aukščiau.

```sql
INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES
  ('20260926234735_AddSupplierBankAccounts', '8.0.0');
```

### 2.3 `20260927011841_AddSupplierAliases` (Etapas 2 S5, D-044 Q4/Q11)

```sql
CREATE TABLE supplier_alias_events (
  id         INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  alias_id   INT NOT NULL,
  invoice_id INT NULL,
  event      VARCHAR(20) NOT NULL,
  actor      VARCHAR(100) NULL,
  details    LONGTEXT NULL,
  created_at DATETIME(6) NOT NULL,
  KEY idx_alias_events_alias (alias_id),
  KEY idx_alias_events_invoice (invoice_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE supplier_aliases (
  id            INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  partner_id    INT NOT NULL,
  alias_key     VARCHAR(255) NOT NULL,
  raw_example   VARCHAR(255) NOT NULL,
  state         VARCHAR(12) NOT NULL,
  confirmations INT NOT NULL,
  frozen_reason VARCHAR(255) NULL,
  created_at    DATETIME(6) NOT NULL,
  updated_at    DATETIME(6) NOT NULL,
  KEY idx_alias_key (alias_key),
  UNIQUE KEY uq_alias_partner (alias_key, partner_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
```

**Be užsienio raktų** (tas pats D-044 Q11). Patikrinta DEV: `supplier_alias_events` — 7
stulpeliai, `supplier_aliases` — 9 stulpeliai, abu atitinka aukščiau.

```sql
INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES
  ('20260927011841_AddSupplierAliases', '8.0.0');
```

### 2.4 Rekomenduojama tvarka

`dotnet ef database update` (paleistas prieš prodo connection string, žmogaus, ne agento)
turėtų pritaikyti 2.1–2.3 automatiškai, JEIGU §2 pradžios patikrinimas (`20260915120000`
istorijos eilutė) atliktas pirma. Alternatyva — rankinis DDL aukščiau, jei `dotnet ef` dėl
bet kokios priežasties nenaudojamas prode (pvz. jei prodo aplinka neturi .NET SDK,
tik Docker image).

### 2.5 Etapo 4 audito lentelė (`expense_audit_samples`) — NĖRA sukurta

`PLAN-ETAPAS4.md` §3/§5/OQ-5: ši lentelė yra tik **pasiūlyta**, jokia DDL niekada
nesuprojektuota (jokio stulpelių sąrašo, jokios migracijos). **NEGALIMA** įtraukti į šį
deploy'ą, nes jos tiesiog nėra kaip realaus artefakto — tik nuoroda `Migrations/` faile
neegzistuoja. Kai savininkas norės pradėti ketvirtinius auditus (§9), tiksli `CREATE TABLE`
turi būti parašyta atskira užklausa, po to, kai OQ-1 (kur saugomi žymėti CSV/JSON) bus
atsakytas — lentelė neturi asmens duomenų (tik ID ir apskaičiuoti boolean), todėl gali
gyventi įprastoje schemoje.

---

## 3. IBAN backfill produkcijai

**Pataisyta (D-048 Part C, po nepriklausomos peržiūros):** ankstesnė šio dokumento versija
klaidingai teigė, kad backfill įrankio nėra. **Jis egzistuoja, jau patikrintas ir jau
paleistas staginge/DEV** —
`Migrations/Scripts/20260927_backfill_supplier_bank_accounts.sql` (commit `55f27bc`, Etapas 2
S4a, D-044 Q2/Q5): grynas, aplinkai nepriklausomas SQL (rekursinis CTE, veikia MySQL 8.0 ir
MariaDB ≥ 10.2), pilnai reimplementuojantis `IbanValidator` taisykles (šalies ilgio lentelė +
ISO 13616 mod-97 kontrolinė suma) tiesiogiai SQL, be jokio kodo iškvietimo. Kiekviena
`business_partners.bank_account` reikšmė, praeinanti šias taisykles, tampa eilute
`supplier_bank_accounts` su `source = 'MIGRATED'`; netenkinančios reikšmės lieka tik senajame
stulpelyje. Idempotentiškas (`NOT EXISTS` apsauga — saugu paleisti pakartotinai).

**Jau patikrintas:** `Tests/NordicBeesERP.Tests/ExpenseKnownIbanTests.cs:409`
(`BackfillSql_AgreesWithIbanValidator_OnEverySample_AndIsIdempotent`) įrodo SQL logika sutampa
su `IbanValidator` ~20 kraštinių atvejų. Jau paleistas DEV: 57 iš 61 reikšmių tapo `MIGRATED`
(`.opencode/reports/etapas2-s4-20260927-0416.md`). Tiksli žingsnis po žingsnio procedūra jau
parašyta `STAGING-CHECKS-ETAPAS2.md` §1.5 (nukopijuoti failą į `lakstena-dev`, paleisti dry-run
SELECT dalį, palyginti su tikėtinu kiekiu, tada INSERT dalį, tada patikros užklausos) — prode
naudoti TĄ PAČIĄ procedūrą, pakeičiant `nordic_bees_staging` → `nordic_bees_erp`.

**⚠ SAVININKAS PATIKRINA:** `STAGING-CHECKS-ETAPAS2.md` pati įspėja — rekursinis CTE
backfill'as patikrintas tik DEV MySQL 8.0.46, **NE MariaDB** (prodas ir staging yra MariaDB
11.8). Jei staginge (kuris naudoja tą pačią MariaDB variklio versiją kaip prodas) dry-run
SELECT dalis sėkmingai įvykdyta be sintaksės klaidos prieš pradedant šį deploy'ą — tai jau ir
yra MariaDB suderinamumo įrodymas prodo daliai; jei staginge dar nepaleista, paleisti pirma
ten, ne tiesiai prode. Be šio žingsnio `SUPPLIER_NEW_IBAN` (D-044 Q5) veiks nuo nulio prode net
tiems tiekėjams, kurie jau turi žinomą IBAN pagrindiniuose duomenyse.

---

## 4. Atsarginės kopijos

Prieš bet kurį iš §2/§3/§8 žingsnių:

```bash
mariadb-dump -h <prodo-hostas> -u erp_user -p'<slaptažodis>' --single-transaction --routines --no-create-db nordic_bees_erp > ~/backup/prod-before-etapai1-4-deploy-<data>.sql
gzip ~/backup/prod-before-etapai1-4-deploy-<data>.sql
```

`--no-create-db` (jokio `CREATE DATABASE`/`USE`) — tas pats principas kaip D-029 staging
atnaujinime, kad dump'as niekada netyčia neperrašytų kitos bazės pavadinimo.

Blobų atsarginė kopija (prieš §8 DELETE, ne prieš §2 schemos pakeitimus — schema
nekeičia jokio blob'o):

```bash
tar czf ~/backup/prod-blobs-before-clean-start-<data>.tgz -C /var/lib/nordicbees/prod blobs
```

(`/var/lib/nordicbees/prod` yra hosto kelias, prijungtas prie konteinerio kaip
`/var/lib/nordicbees/data` — `.github/workflows/deploy.yml` `-v` eilutė, `Services/Storage/FileStore.cs`
`FileStorageOptions.Root` numatytoji reikšmė ir blob'ų kelias `<Root>/blobs/<sha[0:2]>/<sha[2:4]>/<sha>`.)

---

## 5. Kodo push į `production`

```bash
./bump-version.sh
git push origin main:production
```

Stebėti: GitHub Actions `.github/workflows/deploy.yml`, darbas `build-and-deploy` —
`github.ref_name = production` šaka nustato `tag=latest` ir paleidžia `nordicbees_prod`
konteinerį su `ASPNETCORE_ENVIRONMENT=Production`, `Database=nordic_bees_erp`. **Pastaba
I-13 (SERVER-STATE.md):** staging ir prodas naudoja **tą patį** `erp_user`/
`secrets.MYSQL_PASSWORD` — atskiro stagingo vartotojo (I-13 darbas) šiuo metu nėra, tai
nepriklauso nuo šio deploy'o, bet reiškia, kad staging kredencialai šiuo metu taip pat
atidaro prodo bazę.

---

## 6. Patikros po deploy'o

```bash
docker ps --filter name=nordicbees_prod
docker logs nordicbees_prod 2>&1 | grep -E 'Kritinė klaida|Pending EF migrations|Migration warning|Unhandled exception|fail:'
```

Laukiama: jokio `Pending EF migrations` įspėjimo (jei yra — §2 nebaigtas arba
`__EFMigrationsHistory` neatitinka). UI poraštėje versijos numeris atitinka
`bump-version.sh` sukurtą. Vienas dūmų testas: `/expenses` → įkelti vieną žinomą
skaitmeninį PDF per įprastą dialogą (ne masinį įkėlimą — tai atskiras dūmų testas §8),
patikrinti, kad sąskaita sukuriama su teisingu statusu.

---

## 7. Prodo švari pradžia

**Tvarka (griežtai ši seka, ne kita):**

1. §1.4 pagrindinių duomenų taisymai prode (rankiniu būdu, prieš toliau).
2. `Migrations/Scripts/clean-start-expenses.sql` Section 1 (DRY RUN) paleista prieš
   `nordic_bees_erp` — patikrinti, kad visi stop-count'ai (mokėjimai, apmokėtos sąskaitos,
   susieti el. pašto importai) yra 0. Jei ne — **STOP**, netęsti, kol priežastis aiški.

   **Schemos nuokrypa (patikrinta 2026-09-28, ištaisyta šiame skripte):** DEV turi
   `supplier_approvals` lentelę (nesukurta jokios EF Core migracijos ar Modelio šiame
   kode — tai DEV-only likutis, ne laukianti migracija); prodas ir stagingas — kurie yra
   prodo klonai — jos neturi. Iki šio fix'o skriptas ją kietai referencino keturiose
   vietose (preview užklausoje ir Section 3 DELETE'uose), todėl DRY RUN staginge nukrito
   su `ERROR 1146 Table 'nordic_bees_erp_staging.supplier_approvals' doesn't exist` —
   tas pats būtų nutikę ir prode. Skriptas dabar patikrina lentelės buvimą per
   `information_schema.TABLES` ir sąlygiškai įtraukia/praleidžia predikatą per
   PREPARE/EXECUTE dinaminę SQL, tad Section 1 ir Section 3 veikia nepakeisti tiek DEV
   (su lentele), tiek stagingo/prodo (be jos) schemose — jokio papildomo veiksmo šiame
   žingsnyje nebereikia.
3. Ta pati atsarginė kopija kaip §4, jei dar nepadaryta šiai konkrečiai dienai.
4. `clean-start-expenses.sql` Section 3 (DELETE) paleista prieš `nordic_bees_erp` — **žmogaus
   veiksmas**, `sudo mariadb nordic_bees_erp < clean-start-expenses.sql` atitinkama sekcija,
   niekada agento.
5. Section 4 post-checks — visi skaičiai 0, bent vienas bitininkas su `honey_deliveries`
   išliko (spot-check tos pačios formos kaip skriptas pats atlieka).
6. Blob'ų valymas — companion bash fragmentas skripto antraštėje, parametrizuotas
   `/var/lib/nordicbees/prod` blob šaknimi (§4).
7. Pakartotinis įkėlimas per `/expenses/bulk-upload` (Part B, `[Authorize(Roles="Admin")]`)
   — visi originalūs PDF, po vieną Azure iškvietimą vienu metu, žmogaus peržiūra vyksta
   per statusus po įkėlimo, ne prieš.

---

## 8. D-031 kriterijaus 5 audito pradžia

`PLAN-ETAPAS4.md` §3 (kriterijus 5) ir §6 žingsnis 5: pirmasis ketvirtinis 15–20 sąskaitų
imties auditas paleidžiamas **ne anksčiau kaip po vieno pilno ketvirčio** nuo prodo
go-live — turi būti pakankamai auto-priimtų prodo sąskaitų, iš kurių imti pavyzdį. Auditas
tikrina 60 atsitiktinių auto-priimtų sąskaitų prieš originalų PDF, siekiant „< 5 % su 95 %
pasikliovimu" (rule-of-three riba, tyrimo 7 skyrius). Prieš pirmą kartą paleidžiant §3
užklausą reikalinga §2.5 lentelė (`expense_audit_samples`) — kol jos nėra, rezultatas
neįrašomas niekur patvariai, tik ad-hoc patikrinamas.

---

## 9. Atstatymo (rollback) žingsniai

**Kodas (§5).** Grąžinti `production` šaką į ankstesnį commit'ą ir stumti iš naujo:

```bash
git push origin <ankstesnis-sha>:production --force
```

(force push į `production` — žmogaus sprendimas, ne agento; CI perkurs konteinerį su
ankstesniu image tag'u per tą patį `deploy.yml` kelią.) Tai atstato KODĄ, bet **NE
schemą** — jei §2 jau pritaikyta, senas kodas veiks su nauja schema (visi §2 pakeitimai
yra papildantys — nauja lentelė arba platesnis stulpelio tipas — senas kodas juos tiesiog
ignoruoja, nelaužo).

**Schema (§2).** Sąžiningai: automatinio, išbandyto `Down()` kelio prode **NĖRA** —
`dotnet ef migrations` turi `Down()` metodus (matyti kiekviename faile aukščiau), bet jie
niekada nepaleisti prieš realius duomenis šioje sesijoje. Realus atstatymas =
**atstatymas iš §4 atsarginės kopijos**, ne `Down()` paleidimas prode. Išimtis:
§2.1 (`unit_price` decimal(18,6) → decimal(12,2)) yra saugu grąžinti `Down()` būdu, jei
jokia reikšmė dar neviršija dviejų skaičių po kablelio tikslumo (patikrinti prieš
paleidžiant) — §2.2/§2.3 (naujos lentelės) `Down()` yra tiesiog `DROP TABLE`, saugu tik
jei lentelėse dar nėra realių duomenų (t. y. tik prieš pirmą kartą naudojant naują
funkcionalumą po deploy'o).

**Švari pradžia (§7).** **NĖRA atstatymo** po Section 3 DELETE paleidimo, išskyrus
atstatymą iš §4 DB atsarginės kopijos IR §4 blob'ų atsarginės kopijos kartu (dalinis
atstatymas — tik DB arba tik blob'ai — sukurtų nesutampančią būseną). Būtent todėl §7
žingsnis 3 reikalauja šviežios kopijos prieš pat DELETE, ne pasikliauti §4 bendra kopija,
jei tarp jų praėjo laiko ir kažkas pasikeitė.

---

## Santrauka savininkui — ką reikia nuspręsti/patikrinti prieš vykdant

1. §1.1/§1.2: staginge švari pradžia + kriterijai 3–4 atlikti?
2. §1.3: PVM tarifai (su EE data) ir `ZERO_VAT_NO_BASIS` formuluotės patvirtintos su buhalteriu?
3. §1.4: ar staging partnerių ID (381, 377, 386…) egzistuoja prode su tais pačiais duomenimis?
4. §2 pradžia: ar prode jau yra `__EFMigrationsHistory` eilutė `20260915120000_...`?
5. §3: IBAN backfill skriptas jau yra ir patikrintas (`20260927_backfill_supplier_bank_accounts.sql`) —
   paleisti tą pačią `STAGING-CHECKS-ETAPAS2.md` §1.5 procedūrą prieš `nordic_bees_erp`.
6. §9: schema rollback realiai = atstatymas iš kopijos, ne `Down()` prode — priimtina?
