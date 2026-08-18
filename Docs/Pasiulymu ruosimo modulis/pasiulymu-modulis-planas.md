# Pasiūlymų (Offers) generavimo modulis — NordicBeesERP

Planas naujai Claude Code / OpenCode sesijai. Tęsia anksčiau paruoštą
`gamybos_savikainos_skaciavimas` → DB modelio dokumentą (honey_types,
packaging_components, pallet_recipes, cost_calculations). Šis dokumentas
prideda: klientą, kelių pozicijų pasiūlymą, Raben pristatymo kainos
automatinį skaičiavimą, ir PDF generavimą.

---

## 1. Tikslas

Vartotojas (Deividas) nori vieno langelio (screen) kelyje:

1. Pasirenka arba sukuria klientą.
2. Pasirenka produktą (arba sukuria naują ad-hoc) — medaus rūšis/mišinys +
   tara + antrinė pakuotė + paletės receptas. Jei savikainos komponentų
   dar nėra kataloge — suveda vietoje.
3. Sistema paskaičiuoja gamybos savikainą (iš ankstesnio modulio).
4. Sistema automatiškai paskaičiuoja pristatymo kainą pagal Raben kainyną +
   einamojo mėnesio DAF (kuro priemoką), pagal svorį/paletes/kryptį.
5. Galima pridėti kelias pozicijas (skirtingi produktai/svoriai vienam
   pasiūlymui).
6. Pasiūlymas išsaugomas DB (su visų komponentų kainų "nuotrauka" tai
   dienai — audit trail, kaip ir cost_calculations).
7. Generuojamas gražus PDF (analogiškai kaip dabar generuojamos sąskaitos).

Svarbiausia architektūrinė mintis: **receptas (honey mix + packaging +
pallet) turi būti parametrizuotas pagal svorį**, ne hardcoded kiekvienam
naujam produktui — nes tas pats klientas gali užklausti to paties medaus
skirtingo dydžio stiklainiuose (0,5 kg / 0,9 kg / 1,3 kg ir t.t.), o receptas
turi automatiškai perskaičiuoti proporcijas.

---

## 2. Vartotojo srautas (UX)

```
[Naujas pasiūlymas]
  │
  ├─► 1. Klientas
  │      • Paieška esamų (business_partners / customers)
  │      • arba "Sukurti naują" inline (pavadinimas, šalis, PVM kodas,
  │        VIES/JAR patikra — jau yra integracijos, panaudoti)
  │
  ├─► 2. Pozicijos (1..N)
  │      Kiekvienai pozicijai:
  │      a) Produktas: pasirinkti esamą (products) arba "Naujas produktas"
  │         • Medaus mišinys (1..N rūšių, % dalys, turi sudėti 100%)
  │         • Tara (jar/box) — pasirinkti iš packaging_components arba
  │           sukurti naują komponentą inline
  │         • Paletės receptas — pasirinkti esamą arba sugeneruoti naują
  │           pagal matmenis (automatinis "kiek telpa" skaičiavimas)
  │      b) Kiekis / svoris
  │         • Jei reikia kito neto svorio nei receptas numato (pvz. esamas
  │           receptas 0,9 kg, reikia 0,5 kg) → sistema turi arba:
  │           (i) rasti/leisti pasirinkti kitą capacity_kg tarą tam pačiam
  │               jar tipui, arba
  │           (ii) proporcingai perskaičiuoti pripildymo/darbo sąnaudas,
  │               jei tik neto svoris keičiasi, o tara ta pati (netikslu —
  │               geriau reikalauti naujo packaging_components įrašo su
  │               teisinga capacity_kg, kad nekartotųsi Excel klaida
  │               0,5 vs 0,9 kg)
  │         • Užsakymo kiekis vienetais / paletėmis
  │      c) Marža % (numatytoji iš kliento profilio arba rankinė)
  │      d) Rodo paskaičiuotą EXW ir DAP kainą/vnt. gyvai
  │
  ├─► 3. Pristatymas
  │      • Paskirties šalis / pašto kodas
  │      • Paletės kiekis (suskaičiuojamas automatiškai iš pozicijų +
  │        pallet_recipe units_per_pallet)
  │      • Raben bazinis tarifas (pagal zoną/svorio/paleičių lentelę)
  │      • + einamojo mėnesio DAF % (automatiškai iš cache, žr. §4)
  │      • Galutinė transporto kaina — paskirstoma proporcingai pozicijoms
  │        pagal paletes arba svorį
  │
  ├─► 4. Peržiūra
  │      • Suvestinė lentelė: pozicija | kiekis | vnt. kaina | suma
  │      • EXW / DAP perjungiklis
  │      • Galiojimo terminas (valid_until, default +30d)
  │
  └─► 5. Išsaugoti + Generuoti PDF
         • Būsena: Draft → Sent → Accepted/Rejected/Expired
         • PDF: firmos rekvizitai, logotipas, pozicijų lentelė, sąlygos
           (EXW/DAP, mokėjimo terminas, galiojimas), panašus stilius kaip
           esamos sąskaitos
```

---

## 3. Duomenų modelis (naujos/išplėstos lentelės)

Prielaida: `honey_types`, `packaging_components`, `pallet_recipes`,
`product_honey_mix`, `cost_calculations`, `cost_calculation_lines` jau
suprojektuotos ankstesniame dokumente. Reikia patikrinti, ar `products` ir
klientų (`business_partners`?) lentelės realiai jau egzistuoja DB (žr. §6).

### 3.1 `offers` (pasiūlymo antraštė)
- id, offer_number (seka, pvz. PAS-2026-0042), client_id (FK),
  offer_date, valid_until, currency (default EUR),
  delivery_terms (enum: EXW/DAP/DDP...), status (enum: draft/sent/
  accepted/rejected/expired), created_by, notes, pdf_path

### 3.2 `offer_lines` (pozicijos)
- id, offer_id (FK), line_no, product_id (FK, nullable jei ad-hoc),
  description (jei ad-hoc arba perrašymas), net_weight_kg, quantity_units,
  quantity_pallets (skaičiuojama arba rankinė korekcija),
  cost_calculation_id (FK → snapshot iš cost modulio),
  unit_cost_exw, unit_cost_dap, margin_percent, unit_price_exw,
  unit_price_dap, line_total

### 3.3 `raben_rate_tables` (bazinis kainynas — periodinis importas)
- id, valid_from, valid_to, source_file (PDF/Excel pavadinimas,
  Raben "Priedo Nr.4 lentelė Nr.2 Vežėjo įkainiai"), imported_at,
  imported_by
- `raben_rate_lines`: rate_table_id (FK), zone/destination_country,
  weight_from_kg, weight_to_kg (arba pallet_from/pallet_to),
  price_eur

### 3.4 `raben_daf_history` (kuro priemoka — automatinis fetch)
- id, valid_month (pvz. 2026-07), daf_percent, fetched_at, source_url
- Unikalus (valid_month) — jei jau turim tą mėnesį, nefetchinam iš naujo
- Puslapis patvirtintas: `https://lietuva.raben-group.com/klientu-zona/
  kuro-priemokos-mokestis-daf` — rodo tekstą tipo
  "20XX M. [MĖNUO] MĖN. KURO PRIEMOKOS MOKESTIS (DAF) XX,X%"

### 3.5 `delivery_calculations` (pristatymo kainos snapshot pasiūlymui)
- id, offer_id (FK), destination_country, total_pallets, total_weight_kg,
  base_rate_eur (iš raben_rate_lines), daf_percent_used, daf_month_used,
  final_delivery_cost_eur, calculated_at

---

## 4. Raben DAF automatinio fetch architektūra

**Svarbu:** DAF% ir bazinis kainynas — du skirtingi mechanizmai.

- **DAF % (kuro priemoka):** vienas HTML puslapis, atnaujinamas kas mėnesį,
  lengva scrape'inti (regex/HTML parse ieškant "KURO PRIEMOKOS MOKESTIS
  (DAF)" + skaičius su %). Realizacija: background servisas
  (`IRabenDafFetchService`), kviečiamas:
  - kartą per parą (arba pirmą darbo dieną kiekvieną mėnesį) patikrina, ar
    `raben_daf_history` turi šio mėnesio įrašą; jei ne — fetch'ina ir
    įrašo.
  - jei fetch nepavyksta (svetainės struktūra pasikeitė, timeout) —
    naudoja **paskutinį žinomą %** ir pažymi pasiūlymą/kainą kaip
    "DAF neatnaujinta nuo [data]" — niekada tyliai nenaudoti 0% ar
    nulaužti.
  - Rezultatas turi būti matomas UI (nedidelis indikatorius pasiūlymo
    ekrane: "DAF: 25,2% (2026-07, atnaujinta automatiškai)").
- **Bazinis kainynas:** keičiasi rečiau (kelis kartus per metus), skelbiamas
  kaip PDF/Excel priedas. Tai **nėra** gero automatinio scrape kandidatas
  (dokumentų parsinimas nepatikimas). Siūloma: rankinis importas per UI
  (upload naujo Excel/CSV su zonomis+kainomis → `raben_rate_lines`), su
  `valid_from` data. Sistema visada naudoja naujausią galiojantį įrašą tai
  dienai.

**FROZEN.md atitikimas:** fetch servisas — atskiras `HttpClient`-based
servisas, jokių DB write per `FindAsync`; DAF įrašymas — arba
`context.Add()` (nauja eilutė) arba `ExecuteSqlRawAsync` (jei update).

---

## 5. Recepto pakartotinis naudojimas ir svorio skalė

Kad neatsikartotų Excel problema (kiekvienam produktui — nauja failo
kopija, atsijungę skaičiavimai):

- `pallet_recipes` saugo TIK matmenis/limitus + susijusius komponentus
  (box_id, interlayer_id) — patys "telpa" skaičiavimai (units_per_layer,
  max_units_final ir t.t.) **visada perskaičiuojami servise** iš esamų
  matmenų, niekada nesaugomi kaip hardcoded reikšmė DB. Tai leidžia
  keisti komponento matmenis (pvz. naują dėžės tipą) ir automatiškai
  gauti teisingą perskaičiavimą visiems, kas tą receptą naudoja.
- Naujam svoriui (0,5 kg vietoj 0,9 kg) — sistema turėtų:
  1. Patikrinti, ar jau yra `packaging_components` įrašas su ta
     `capacity_kg` tam pačiam stiklainio tipui;
  2. jei nėra — pasiūlyti sukurti naują komponentą (aiškiai reikalaujant
     `capacity_kg` lauko, kad nesikartotų 0,5/0,9 klaida);
  3. `product_honey_mix` % dalys nesikeičia nuo svorio — tik absoliutus kg
     kiekis linijoje perskaičiuojamas.

---

## 6. Ką reikia pasitikrinti prieš implementaciją

Šito Claude.ai sesijoje **nedarysiu pats** (neturiu leidimo skaityti DB/failų
šioje žinutėje) — žemiau OpenCode/Claude Code promptas, kurį gali paleisti,
kad surinktų info ir grąžintų ataskaitą (ne kodą, tik radinius):

```
UŽDUOTIS: Surinkti informaciją apie esamą NordicBeesERP struktūrą prieš
pradedant "Pasiūlymų generavimo" modulio implementaciją. NEDARYK jokių
pakeitimų kode ar DB — tik ataskaita.

Patikrink ir surašyk į markdown failą PROPOSAL_MODULE_DISCOVERY.md:

1. Ar DB jau turi lenteles klientams (business_partners, customers ar
   panašiai)? Nurodyk tikslų pavadinimą, stulpelius, ar yra VIES/JAR
   integracijos laukai.
2. Ar egzistuoja `products` arba panaši lentelė? Jei taip — struktūra.
3. Ar egzistuoja jau kokia nors "honey_types" / "packaging" / "recipe"
   struktūra iš ankstesnio cost-calculation darbo (ieškok migracijų ir
   Models/ katalogo)?
4. Kaip šiuo metu generuojamos sąskaitos (PDF)? Kokia biblioteka
   (QuestPDF?), koks servisas/klasė atsakinga, kur šablonas/layout
   saugomas — nurodyk failų kelius, kad naują pasiūlymo PDF generavimą
   būtų galima daryti analogiškai.
5. Ar yra esama sekų/numeravimo logika sąskaitoms (invoice_number), kurią
   galima pakartoti pasiūlymo numeriui (offer_number)?
6. Ar yra jau koks nors HTTP fetch / scraping servisas projekte (pvz. JARS
   API, VIES), kad būtų galima sekti tuo pačiu pattern'u kuriant Raben DAF
   fetch servisą?
7. Ar `__EFMigrationsHistory` / ReconcileSnapshot situacija (žinoma
   problema) paveiks naujų lentelių pridėjimą — ar naujos lentelės eis per
   naują atskirą migraciją, ar reikės rankinio DDL (kaip nustatyta
   FROZEN.md / esamoje praktikoje)?
8. Koks yra esamas "margin %" / kainodaros laukas kliento profilyje, jei
   toks yra (kad offer_lines numatytoji marža galėtų jį paveldėti)?

Rezultatą pateik tik kaip surinktos informacijos suvestinę su failų
keliais ir kodo fragmentais (citatomis), be pasiūlymų ar sprendimų —
sprendimus priims žmogus atskiroje sesijoje.

KRITINIAI APRIBOJIMAI:
1. Jokių DDL/migracijų nekurti šioje užduotyje.
2. Jokio kodo nerašyti — tik discovery/read-only.
3. Naudok tik SELECT užklausas per nordicbees-db MCP arba failų skaitymą.
```

---

## 7. Neatsakyti klausimai / sprendimai (patvirtinti su Deividu)

1. Ar transporto kaina pasiūlyme skaičiuojama **visam pasiūlymui vienąkart**
   (viena paskirtis), ar kiekvienai pozicijai atskirai (jei skirtingos
   paskirties šalys viename pasiūlyme — mažai tikėtina, bet reikia
   apsispręsti)?
2. Ar `waste_loss_rates` (broko %) iš ankstesnio dokumento įtraukiamas į šį
   modulį, ar lieka atskiras sprendimas?
3. Kai klientas "Accepted" pasiūlymą — ar reikia automatinio perėjimo į
   užsakymą (Orders modulis jau yra) ar tai atskiras rankinis žingsnis?
4. PDF kalba — visada lietuviškai, ar reikia daugiakalbio šablono
   (eksporto klientams anglų/kt. kalba)?
5. Ar reikalingas offer versioning (jei klientas prašo pakeitimų — nauja
   versija to paties offer_number su -v2, ar visiškai naujas įrašas)?

---

## 8. Implementacijos etapai (siūlomi)

1. **Discovery** — §6 promptas, rezultatų peržiūra.
2. **DB sluoksnis** — `offers`, `offer_lines`, `raben_rate_tables`,
   `raben_rate_lines`, `raben_daf_history`, `delivery_calculations`
   migracijos (naujoje, ne senoje InitialCreate migracijoje — žr. žinomą
   EF Core migracijų problemą).
3. **Raben DAF fetch servisas** + testas su realiu puslapiu (mock HTML
   fixture testams, kad testai neitų per tikrą internetą).
4. **Klientų/produkto pasirinkimo UI** (Blazor komponentai, panaudojant
   esamus `FilterUrlBuilder`, `StatusDisplayHelper` pattern'us).
5. **Pozicijų redagavimas + gyva kainos peržiūra** (reuse cost-calculation
   servisą iš ankstesnio modulio).
6. **Pristatymo skaičiavimo servisas** (bazinis tarifas + DAF).
7. **PDF generavimas** (reuse QuestPDF pattern iš sąskaitų).
8. **Statusų workflow** (draft/sent/accepted/...).
9. xUnit testai: cost recalculation su skirtingais svoriais, DAF fetch
   fallback logika, pallet-fitting skaičiavimas (jau yra reference
   reikšmės iš Excel, gerai tinka kaip test case'ai).
