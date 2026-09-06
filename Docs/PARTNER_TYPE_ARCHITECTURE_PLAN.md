# Partnerio tipų architektūros planas (PartnerType → rolės)

**Data:** 2026-09-04
**Statusas:** DRAFT — laukia Deivido patvirtinimo prieš BUILD etapą
**Susiję:** `.opencode/reports/partner-type-expense-category-audit-20260904-1630.md`

---

## 1. Kodėl reikia keisti

Dabartinis modelis: `BusinessPartner.PartnerType` — vienas enum'as su reikšmėmis
`Customer | Supplier | Both | ExpenseSupplier`. Auditas parodė realias
pasekmes:

- `Both` reiškia "partneris vienu metu ir klientas, ir tiekėjas" — bet tai
  priverstinai sutampa į VIENĄ reikšmę, todėl negalima natūraliai pridėti
  trečios rolės (pvz. partneris yra ir klientas, ir tiekėjas, IR išlaidų
  tiekėjas — enum'as to neišreiškia be dar vieno "visi trys" varianto).
- UI (Suppliers.razor tab'ai) IŠVIS nefiltruoja pagal `PartnerType` — naudoja
  euristiką (`NationalIdNumber`/`CompanyCode` užpildymą), kuri nesutampa su
  realiu duomenų modeliu. Tai reiškia du nepriklausomus, nesuderintus
  "tipo" šaltinius sistemoje.
- Create dialogai (`SupplierEditDialog` vs `CustomerCreateDialog`) turi
  skirtingus pasirinkimų rinkinius tam pačiam enum'ui.
- Farmerio/fizinio asmens identifikavimas remiasi vien tuo, ar užpildytas
  `NationalIdNumber` — nėra eksplicitinio lauko.

## 2. Kaip sprendžia geriausios ERP sistemos (tyrimas)

### SAP Business Partner (S/4HANA)
Nuo S/4HANA SAP panaikino atskirus Customer/Vendor master duomenis ir
perėjo prie vieno **Business Partner** objekto su **rolėmis**:
> "one partner, one master record, several roles"

Vienas BP įrašas gali turėti vienu metu kelias roles (Customer, Supplier,
Prospect, General Business Partner ir t.t.) — rolės PRIDEDAMOS, o ne
pasirenkamos iš exclusive sąrašo. Bendri duomenys (vardas, adresas,
banko sąskaita) saugomi vieną kartą; kiekviena rolė turi savo specifinius
laukus. Papildomai — **partnerio kategorija** (Person / Organization /
Group) yra atskiras, nekeičiamas po sukūrimo laukas, VISIŠKAI nepriklausomas
nuo rolių.

### Odoo (res.partner)
Odoo istoriškai turėjo `is_customer`/`is_vendor` boolean laukus, bet nuo
v13 pakeitė juos `customer_rank` / `supplier_rank` skaitiniais laukais:
- `rank = 0` → "dar nežinome" (paprastas kontaktas)
- `rank >= 1` → partneris veikia ta role
- Abu laukai nepriklausomi vienas nuo kito — partneris gali turėti
  `customer_rank=1 AND supplier_rank=1` tuo pačiu metu be jokio specialaus
  "Both" varianto.
- Atskirai yra `is_company` (boolean) — individas vs įmonė — visiškai
  atskirtas nuo pirkimo/pardavimo rolių.

### Bendra išvada
Abi sistemos nepriklausomai priėjo prie to paties sprendimo: **rolės kaip
nepriklausomos vėliavėlės, ne exclusive enum**, plius **atskiras
"individas/įmonė" laukas**, nesusietas su rolėmis.

## 3. Siūlomas modelis NordicBeesERP

Nekeičiam pagrindinės architektūros (vienas `business_partners` stalas —
tai jau atitinka "Party pattern", teisingas sprendimas). Keičiam TIK tipo
išraišką:

### 3.1 Nauji laukai (vietoj vieno `PartnerType` enum)

```csharp
public bool IsCustomer { get; set; }         // gali gauti sąskaitas (LAK)
public bool IsSupplier { get; set; }          // tiekia prekes/žaliavą
public bool IsExpenseSupplier { get; set; }   // naudojamas išlaidų sąskaitose
public bool IsIndividual { get; set; }        // fizinis asmuo / ūkininkas (vs įmonė)
```

- Bet kuris derinys galimas ir prasmingas be jokio "Both"-tipo dirbtinumo:
  partneris, kuris ir perka, ir parduoda mums, tiesiog turi
  `IsCustomer=true, IsSupplier=true`.
- `IsExpenseSupplier` tampa **nepriklausoma** nuo `IsSupplier` — dabar
  tiekėjas gali BŪTI IR prekių tiekėjas, IR naudojamas išlaidų
  sąskaitoms (dabar tai buvo neįmanoma be trečio enum'o varianto).
- `IsIndividual` pakeičia dabartinę `NationalIdNumber`-euristiką
  eksplicitiniu lauku — Ūkininkų tab'as filtruoja `IsIndividual == true`,
  ne "ar užpildytas laukas".

### 3.2 Migracijos / backfill logika (vienkartinė, human-applied SQL)

```
IsCustomer       = PartnerType IN ('customer', 'both')
IsSupplier       = PartnerType IN ('supplier', 'both')
IsExpenseSupplier= PartnerType = 'expense_supplier'
IsIndividual     = NationalIdNumber IS NOT NULL AND NationalIdNumber != ''
```

Senas `PartnerType` stulpelis **PALIEKAMAS** DB lygyje (nedrop'inam) kol
visas kodas perkeltas ir patvirtintas prod'e — laikinas dubliavimas,
saugesnis nei staigus laukų pašalinimas. Atsikratom jo atskiru, vėlesniu
migracijos etapu, kai viskas stabilu (mažiausiai vieną pilną gamybos
ciklą po perjungimo).

### 3.3 Verslo taisyklės, atsakančios į atviro klausimus iš audito

| Klausimas | Sprendimas |
|---|---|
| Ar partneris gali būti ir tiekėjas, ir pirkėjas vienu metu? | **TAIP, natūraliai** — `IsCustomer` ir `IsSupplier` nepriklausomi booleanai, ne exclusive pasirinkimas |
| Kaip identifikuoti ūkininką/fizinį asmenį? | Eksplicitinis `IsIndividual` checkbox'as dialoge, NE laukų užpildymo euristika. UI gali pasiūlyti default'ą pagal tai, ar įvestas asmens kodas, bet vartotojas gali koreguoti |
| Kuo ExpenseSupplier skiriasi nuo Supplier? | Tampa atskira vėliavėle `IsExpenseSupplier`, kompozuojama su `IsSupplier` — partneris gali turėti abi, tik vieną, ar nė vienos |
| Ar keisti senas sąskaitas pakeitus DefaultExpenseCategoryId? | **NE automatiškai** (SAP/Odoo šablonas — istoriniai duomenys nekeičiami tyliai). Vietoj to: rodyti pasiūlymą "N nepatvirtintų (draft) sąskaitų naudoja seną kategoriją — atnaujinti?" su aiškiu patvirtinimu, taikoma TIK juodraščiams, niekada patvirtintoms/apmokėtoms sąskaitoms |
| Ar riboti tipo keitimą po sukūrimo? | Rolių (IsCustomer/IsSupplier/IsExpenseSupplier) keitimas laisvas bet kada — jos tik prideda/atima matomumą sąrašuose, nekeičia istorinių įrašų. `IsIndividual` keitimas leidžiamas, bet rodomas patvirtinimo dialogas, jei partneris turi išrašytų sąskaitų (nes veikia PVM/asmens kodo logiką ateities dokumentuose) |

## 4. UI pokyčiai

### 4.1 Vieningas partnerio dialogas
Vietoj dviejų divergavusių dialogų (`SupplierEditDialog`,
`CustomerCreateDialog`) — **vienas bendras** `PartnerEditDialog.razor`,
naudojamas ir iš `/suppliers`, ir iš `/customers`, su:
- 3 checkbox'ai: `☐ Klientas` `☐ Tiekėjas` `☐ Išlaidų tiekėjas`
- 1 toggle/radio: `Įmonė / Fizinis asmuo (ūkininkas)` — keičia, kurie
  laukai rodomi (CompanyCode+VatCode vs NationalIdNumber)
- Likę laukai (adresas, PVM%, mokėjimo terminas, banko sąskaita,
  numatyta išlaidų grupė) — tie patys nepriklausomai nuo rolių derinio

### 4.2 Sąrašo tab'ai
- **Suppliers.razor**: tab'ai filtruoja `IsSupplier == true`, viduje
  papildomai skaidoma pagal `IsIndividual` (Ūkininkai/Įmonės), o "Visi"
  tab'as — visi `IsSupplier` partneriai nepriklausomai nuo
  `IsIndividual`.
- **Customers.razor**: filtras `IsCustomer == true` (be pakeitimų
  koncepte, tik lauko šaltinis keičiasi).
- Partneris su `IsCustomer=true AND IsSupplier=true` natūraliai matomas
  ABIEJUOSE sąrašuose — tai jau dabar veikia per `Both`, bet dabar be
  papildomos enum-mapping logikos.

## 5. Rollout etapai (kiekvienas — atskiras OpenCode BUILD task'as)

1. **Migracija**: pridėti 4 naujus bool stulpelius (EF Core migracija,
   `dotnet ef migrations add`), backfill SQL scriptas (žmogaus paleidžiamas
   rankiniu būdu pagal FROZEN.md DDL taisyklę). SENAS `PartnerType`
   nekeičiamas.
2. **Servisų sluoksnis**: `CustomerService`/`SupplierService` perjungiami
   skaityti iš naujų bool laukų (su fallback'u į seną `PartnerType`, jei
   naujas laukas dar nenustatytas — apsauga pereinamuoju laikotarpiu).
3. **UI — sąrašai**: Suppliers.razor/Customers.razor tab'ų filtrai
   perjungiami į naujus laukus.
4. **UI — dialogai**: sukuriamas vieningas `PartnerEditDialog.razor`,
   pakeičiantis abu senus dialogus.
5. **Expense flow**: `ExpenseService.AutoAssignSupplierAsync` perjungiamas
   į `IsExpenseSupplier`; pridedamas neprivalomas "atnaujinti juodraščius"
   veiksmas keičiant `DefaultExpenseCategoryId`.
6. **Cleanup** (atskiras, vėlesnis task'as, tik po stabilaus prod ciklo):
   pašalinti seną `PartnerType` stulpelį ir su juo susijusį kodą.

Kiekvienas etapas — savo investigation → build → verify ciklas, kaip
įprasta šiam projektui. Nesiūlau daryti viso to vienu BUILD task'u — per
daug rizikos vienu commit'u paliesti duomenų modelį + servisus + UI.

## 6. Rizikos / FROZEN.md pastabos

- DDL keitimai — tik žmogaus rankomis, per esamą incremental EF migracijų
  procesą.
- `PartnerType` DB stulpelio nešalinam pirmame etape — leidžia greitą
  rollback'ą, jei kas nors nepastebėta.
- `Reports/DebtReconciliation.razor` ir kitos vietos, tiesiogiai
  naudojančios `PartnerType`, turi būti surastos ir atnaujintos 2-3
  etapuose (bus dalis investigation prompt'o kiekvienam etapui).

---

## 7. Realiais PROD duomenimis patikrinti faktai (2026-09-04)

Ankstesnė šio plano versija rėmėsi vien kodo analize ir OpenCode audito
ataskaita. Po tiesioginio patikrinimo `lakstena-dev` PROD DB (ne dev
kopijoje — abi bazės rodo TĄ PATĮ realų vaizdą), paaiškėjo tikslesnis,
iš dalies kitoks vaizdas nei pirminė hipotezė:

### 7.1 `PartnerType` pasiskirstymas (PROD)
```
customer:          95
supplier:          93
expense_supplier:  19
```
`expense_supplier` REALIAI naudojamas — ankstesnė prielaida, kad šis
enum'o variantas "mirusi funkcija", buvo KLAIDINGA (remtasi vien dev
duomenimis, kurie tuo metu neturėjo šio tipo įrašų).

### 7.2 Klientas/Tiekėjas dublikatai — PATVIRTINTA, 7 poros
Tas pats žmogus įvestas du kartus (kaip `customer` IR kaip `supplier`),
identiškai ir dev, ir prod:

| Vardas | customer ID | supplier ID | Sąskaitos (customer/supplier pusėje) |
|---|---|---|---|
| Zita Rutkauskienė (tas pats VAT+įm.kodas) | 12 | 294 | 1 / 2 — **išskaidyta** |
| Tomas Balčiūnas | 78 | 326 | 1 / 1 — **išskaidyta** |
| AURIMAS BERNOTAS | 79 | 328 | 1 / 1 — **išskaidyta** |
| Vaidas Arbutavičius | 65 | 333 | 3 / 0 |
| Žilvinas Macijauskas | 85 | 185 | 0 / 4 |
| Regina Žilinskienė | 89 | 170 | 0 / 1 |
| LAIMUTIS ŽALALIS | 92 | 173 | 0 / 2 |

3 iš 7 porų turi **realiai išskaidytą** sąskaitų istoriją abiejose
pusėse — jei kas nors žiūri tik vieną iš dviejų įrašų, mato NEPILNĄ
šio žmogaus sąskaitų vaizdą. Tai jau egzistuojanti problema, nesusijusi
su būsimu modelio keitimu — pati savaime verta atskiro sprendimo.

### 7.3 `AssignSupplierDialog` filtras neatitinka realaus naudojimo
Dialogas rodo tik `PartnerType IN (ExpenseSupplier, Both)` — 19 įrašų.
Bet realūs `expense_invoices.supplier_id` priskyrimai PROD'e:
```
customer:          6   ← šitų šis dialogas NIEKADA nerodytų
supplier:          25  ← nei šitų
expense_supplier:  73
```
31 iš 104 esamų expense-sąskaitos priskyrimų veda į partnerius, kurių
tipas NĖRA `ExpenseSupplier`/`Both`. Tai reiškia, kad tie priskyrimai
buvo padaryti kitu keliu (per `AssignSupplierAsync`, kuris, kaip
patvirtinta skaitant `ExpenseService.cs`, IŠVIS netikrina
`PartnerType` prieš įrašydamas `supplier_id`), arba partnerio tipas
buvo pakeistas jau po priskyrimo. Bet kuriuo atveju — **`PartnerType`
šiandien jau NĖRA patikimas predictorius**, kas realiai naudojama kaip
expense tiekėjas. Tai tiesiogiai patvirtina role-flags sprendimo
pagrįstumą (7-8 skyriai), bet reikalauja pakeisti backfill taisyklę:

**Atnaujinta backfill taisyklė `IsExpenseSupplier`:**
```sql
IsExpenseSupplier = (PartnerType = 'expense_supplier')
                     OR (id IN (SELECT DISTINCT supplier_id FROM expense_invoices
                                 WHERE supplier_id IS NOT NULL))
```
Vien enum'o kopijavimo NEPAKAKTŲ — būtų prarasta 31 realiai naudojamo
tiekėjo matomumas naujame modelyje.

### 7.4 Peržiūrėta išvada
Ankstesnė (6 skyriaus) rollout strategija architektūriškai lieka
teisinga, bet reikalauja PAPILDOMO, ANKSTESNIO etapo:

**Naujas 0-as etapas (prieš schema keitimą):** žmogaus atliekama 7
klientas/tiekėjas dublikatų porų peržiūra (aukščiau lentelėje) —
kiekvienai porai nuspręsti: sujungti į vieną `Both`-tipo įrašą
(perkeliant FK iš vieno į kitą), ar palikti kaip du atskirus įrašus
sąmoningai (jei tai realiai du skirtingi santykiai, ne duplikatas).
Tai NEGALI būti automatizuota vien script'u, nes 3 poros turi realų
duomenų pasidalijimą tarp abiejų pusių.

---

**STATUSAS: 0 ETAPAS ĮVYKDYTAS (2026-09-04, PROD).** Visos 10 porų
sėkmingai sujungtos tiesiai PROD DB (dev buvo praleistas pagal aiškų
žmogaus sprendimą — "dev tik testiniai duomenys, negaištam laiko").

Vykdymo eiga:
1. OpenCode paruošė `.sql` draft'ą su visais 12 rastų FK/potencialių FK
   ryšių (dev schemos pagrindu) — kiekvienas blokas su numatytu
   `ROLLBACK`.
2. Prieš vykdant, Claude palygino draft'ą su tiesioginiu PROD SELECT
   patikrinimu — visi laukai/reikšmės sutapo, IŠSKYRUS tai, kad PROD
   NETURI `supplier_approvals` lentelės (ji buvo tik dev schemoje).
3. Pirmas vykdymo bandymas sustojo per `UPDATE supplier_approvals` (klaida
   `1146: table doesn't exist`) — MariaDB automatiškai atšaukė
   nebaigtą transakciją, jokių duomenų pažeidimų.
4. `supplier_approvals` eilutės pašalintos iš scripto (`sed` filtru), likusios
   10 lentelių patvirtintos kaip realiai egzistuojančios PROD schemoje.
5. Antras vykdymas praėjo be klaidų. Visi 10 "pralaimėjusių" įrašų —
   `is_active=0` su `[MERGED into id X]` pėdsaku `notes` lauke. Visi 7
   customer/supplier survivor'iai — `partner_type='both'`. 3
   supplier/supplier poros — tipas nepakeistas (teisingai). Visi backfill'ai
   (national_id_number, phone, default_expense_category_id) patvirtinti
   tiksliai atitinkantys.
6. Sąskaitų FK perkėlimas patvirtintas: 12→3 (1+2), 65→3 (nepakito), 326→2
   (1+1), 328→2 (1+1) — visi sutapo su prognozuotais skaičiais.

**Neišspręsta, sąmoningai palikta atskiram sprendimui:** `326`
(Tomas Balčiūnas) ir `328` (Aurimas Bernotas) vis dar dalinasi identišku
`national_id_number` (`302905315`) IR identišku adresu/banko lauku
("P. Širvio g. 3, Juodupė, Rokiškio raj." / "Paysera LT UAB") — tai rodo,
kad vienas iš šių dviejų įrašų buvo sukurtas nukopijavus kitą kaip
šabloną. Reikia atskirai surasti teisingus kiekvieno asmens duomenis.

**Kitas žingsnis:** 1 etapas (role-flags schema migracija) gali prasidėti,
kai žmogus nuspręs tęsti — pradedant investigation prompt'u pilnam
`PartnerType` naudojimo inventoriui kode (šis inventorius jau iš dalies
atliktas `.opencode/reports/partner-type-code-inventory-and-merge-brief-20260904-1700.md`,
reikia tik atnaujinti pagal PROD schemą, ne dev).

---

## 8. 1 ETAPAS ĮVYKDYTAS IR PATVIRTINTAS (2026-09-06, PROD)

### 8.1 Schema
4 nauji stulpeliai (`is_customer`, `is_supplier`, `is_expense_supplier`,
`is_individual`) pridėti prie `business_partners`:
- **DEV**: `dotnet ef database update` — sėkmingai, migracija
  `20260905201154_AddPartnerRoleFlags` užregistruota su `ProductVersion
  8.0.0` (dev anomalija, nesvarbi vykdymui).
- **PROD**: `dotnet ef` negalėjo pasiekti prod DB tiesiogiai iš Mac'o
  (`127.0.0.1` iš Mac'o reikštų patį Mac'ą, ne `lakstena-dev`) —
  pritaikyta rankiniu `ALTER TABLE` + rankinis `INSERT INTO
  __EFMigrationsHistory` su `ProductVersion 10.0.0` (atitinka realias kitas
  prod migracijas, patikrinta prieš vykdant).
- Migracijos failas išvalytas nuo EF automatiškai sugeneruoto
  `UpdateData` triukšmo (nesusijusių `artwork_brands`/`raw_material_types`
  timestamp pakeitimų) prieš taikant — liko tik 4 `AddColumn`/`DropColumn`.
- Senas `PartnerType` stulpelis NEpaliestas, kaip planuota.

### 8.2 Backfill
Paleista PROD per transakciją (numatytas `ROLLBACK`, patikrinta, tada
`COMMIT`):
- `IsCustomer`/`IsSupplier` — tiesioginis mapping iš `PartnerType`.
- `IsExpenseSupplier` — enum reikšmė ARBA realus naudojimas
  `expense_invoices.supplier_id` (išvengta 7.3 skyriuje rastos
  spragos — vien enum'o nepakaktų).
- `IsIndividual` — euristika pagal `national_id_number` buvimą.

**Rezultatas (PROD, po backfill):** 207 partneriai iš viso, jokio
praradimo/dubliavimo (89+49+32+26+6+4+1 = 207, sutampa su prieš
migraciją buvusiu 95+93+19). Visi 7 Phase 0 metu sujungti partneriai
sėkmingai gavo `is_customer=1 AND is_supplier=1`.

**Pastebėjimas kitam žingsniui:** Vaidas Arbutavičius (id 65) gavo
`is_individual=0`, nes nė vienoje iš dviejų (dabar sujungtų) įrašo pusių
niekada nebuvo užpildytas `national_id_number`. Jei jis realiai yra
fizinis asmuo/ūkininkas, reikės rankiniu būdu patikslinti Phase 2/3 UI
dialoguose.

### 8.3 Kitas žingsnis — 2 Etapas (servisų sluoksnis)
`CustomerService.cs`, `SupplierService.cs`, `ExpenseService.cs` dar
SKAITO tik iš seno `PartnerType` — nauji laukai kol kas tik egzistuoja
DB, bet joks kodas jų nenaudoja. Kitas investigation+build ciklas:
perjungti servisų sluoksnį skaityti iš naujų boolean laukų (su fallback'u
į seną `PartnerType`, kol UI dialogai dar jų nerašo), tada UI sąrašų
filtrus (3.2 skyrius), tada vieningą dialogą (4.1 skyrius).

---

## 9. 2 ETAPAS ĮVYKDYTAS IR PATVIRTINTAS (2026-09-06)

`CustomerService.cs`, `SupplierService.cs`, `InvoiceService.cs` (6 read
filtrų iš viso) perjungti skaityti iš naujų `Is*` laukų, su fallback'u
į seną `PartnerType` TIK kai visi trys role flag'ai vis dar `false`
(reikštų naują, dar nemigruotą įrašą). Commit'ai: `24b859e`, `7f5932d`,
`4f29e86` (v0.17.46→0.17.48). Kiekvienas failas patikrintas TIESIOGIAI
(ne tik per report'ą) — visi pakeitimai sutampa žodis į žodį.

**Tšskinis, sąmoningas elgesio pasikeitimas:** `SupplierService`
tiekėjų sąraše dabar naudoja `IsExpenseSupplier` kaip pagrindinį
(ne fallback) filtrą — tai išplės matomų kandidatų sąrašą, nes
7.3 skyriuje rastas 31 realiai naudojamas išlaidų tiekėjas dabar
taps matomas ten, kur anksčiau NEbuvo (senas enum filtras jų
nematytų). Tai numatytas, teisingas pagerėjimas, ne broken behavior.

**Nepaliesta (sąmoningai, Phase 3):**
- Joks `.razor` failas (`AssignSupplierDialog`, `ResolveSupplierDialog`,
  `Suppliers.razor`, `Customers.razor`, `TopHeader`) — vis dar skaito
  seną `PartnerType` tiesiogiai inline.
- `SaveSupplierAsync`/`SaveCustomerAsync` RAŠYMO logika — vis dar rašo
  tik `partner_type`, NE naujus `Is*` laukus. Nauji partneriai iki
  Phase 3 turės visus flag'us `false` ir pasikliaus fallback'u.
- `CustomerService.ParsePartnerType()` LT/EN string maišymas — lieka
  Phase 3 darbui, kai dialogai pereis prie checkbox'ų.

**Kitas žingsnis — 3 Etapas (UI):** sąrašų filtrai (Suppliers.razor/
Customers.razor tab'ai) + vieningas `PartnerEditDialog.razor` + rašymo
logikos atnaujinimas, kad nauji/redaguoti partneriai pradėtų realiai
išsaugoti naujus `Is*` laukus.
