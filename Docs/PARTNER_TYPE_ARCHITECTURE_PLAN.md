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

**Kitas žingsnis:** jei planas tinka, rašau investigation prompt'ą 1-am
etapui (migracija + backfill scriptas), NE dar kodo pakeitimų — kad
OpenCode surastų VISAS vietas, kur šiuo metu naudojamas `PartnerType`,
prieš darant schema keitimą.
