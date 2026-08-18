# Gamybos savikainos skaičiavimas → perkėlimas į NordicBeesERP

Šis dokumentas skirtas naujai sesijai (pvz. Claude Code su NordicBeesERP repo).
Jame surinkta visa informacija iš MB Lakštena Excel failo
`gamybos_savikainos_skaciavimas.xlsx` (produktas: **LIDL LT medus, stiklainis 0,9 kg**,
duomenų data 2026-02-19) — kaip šiuo metu skaičiuojama savikaina, kokie laukai/parametrai
naudojami, kur faile yra klaidų, ir koks siūlomas DB modelis šiam funkcionalumui perkelti į ERP.

---

## 1. Kontekstas

- Verslas: MB Lakštena (NordicBees) — medaus supirkimas, perdirbimas, pakavimas, eksportas.
- Failas yra vieno Excel lapo ("Sheet1") skaičiuoklė vienam konkrečiam produktui
  (šiuo atveju LIDL LT 0,9 kg medaus stiklainis). Kiekvienam naujam produktui/klientui
  šiuo metu greičiausiai kuriama nauja failo kopija arba lapas — tai ir yra priežastis,
  kodėl norima perkelti į ERP: kad skaičiavimai būtų saugomi DB, o ne išsibarstę po failus.
- **Svarbi pastaba (klaida originaliame faile):** laukas "Taros talpa, kg" (C15) buvo
  užpildytas kaip **0,5 kg**, nors produktas yra 0,9 kg. Tai bendrovės pačios pripažinta
  klaida faile. Visuose žemiau pateiktuose paskaičiavimuose naudojama **teisinga reikšmė
  0,9 kg** (ne originalus 0,5).

---

## 2. Excel failo struktūra (originalūs langeliai, Sheet1)

Failas suskirstytas į blokus, sudėliotus B–T stulpeliuose. Žemiau — kiekvieno bloko
langeliai, formulės ir reikšmės, kaip yra faile (su pažymėta 0,5→0,9 kg pataisa).

### 2.1 Žaliava — medus (B4:E10)

| Langelis | Laukas | Reikšmė |
|---|---|---|
| B5 | Medaus 1 kg kainos skačiavimas | — |
| C6:E6 | antraštės: Koks medus / % blende / kaina eur/kg | — |
| B7:E7 | Medus 1 | LT, dalis mišinyje = 1 (100%), kaina 3,20 €/kg |
| B8:E8 | Medus 2 | UKR, dalis 0%, kaina 42,60 €/kg |
| B9:E9 | Medus 3 | CN, dalis 0%, kaina 1,72 €/kg |
| C10 | Naudojamo medaus 1 kg kaina | `=(E7*D7)+(E8*D8)+(E9*D9)` → **3,20 €/kg** |

Formulė leidžia mišinį iš iki 3 medaus rūšių/kilmių su procentine dalimi — svarbu ERP
modelyje leisti **N eilučių** (ne fiksuotai 3), nes ateityje mišinių gali būti daugiau.

### 2.2 Tara / pirminė pakuotė (B12:C24)

| Langelis | Laukas | Reikšmė |
|---|---|---|
| C14 | Kokia tara? | skaidrus stiklas, 0,3 l, 500 g |
| C15 | Taros talpa, kg | **0,5 (faile) → turi būti 0,9** |
| C16 | Taros diametras/plotis, mm | 96 |
| C17 | Taros aukštis, mm | 133 |
| C18 | Taros svoris, kg | 0,188 |
| C19 | Taros kaina (tara+dangtelis), €/vnt | 0,21 |
| C21 | Kiek ir kokios etiketės | Šoninė, viršutinė |
| C22 | Etiketės kaina, €/vnt | 0,08 |
| C24 | Medaus paruošimo/išpylimo (pirminio pakavimo) kaina, €/vnt | 0,27 |

**Pastaba:** faile taip pat yra atskira "Stiklo taros savikainos skaičiuoklė" (N4:T13) —
pagalbinė lentelė su keliais stiklainio tipais (0,7 kg ir 0,9 kg), kur kiekvienam
apskaičiuojama "tikra savikaina" pagal vnt./padėklas, transporto kainą ir dangtelio kainą
(pvz. 0,9 kg stiklainiui ten išeina 0,1885 €/vnt.). **Ši lentelė NĖRA susieta formule** su
C19 — C19 yra įvestas rankiniu būdu ir realiai neatitinka skaičiuoklės rezultato. Tai reiškia,
kad faile yra du nesusieti šaltiniai tos pačios taros kainai — ERP modelyje šitą reikia
sujungti į vieną tiesos šaltinį (žr. 5 skyrių, `packaging_components`).

### 2.3 Antrinė pakuotė (B27:C36)

| Langelis | Laukas | Reikšmė |
|---|---|---|
| C28 | Pakuotės tipas | Pakai po 8 vnt. |
| C29 | Pakuotėje vnt. | 8 |
| C30–C32 | Pakuotės ilgis/plotis/aukštis, mm | 296 / 200 / 135 |
| C33 | Pakuotės svoris, kg | `=C29*(C18+C15)` |
| C34 | Pakuotės kaina, €/vnt | `=1.1/C29` → 0,1375 |
| C36 | Grupinio pakavimo darbo kaina, €/vnt | 0,02 |

### 2.4 Pakavimas ant padėklo (B38:C56)

| Langelis | Laukas | Reikšmė |
|---|---|---|
| C39–C41 | Padėklo ilgis/plotis/aukštis, mm | 1200 / 800 / 145 |
| C42 | Padėklo svoris (tara), kg | 25 |
| C43 | Sukrautos paletės max aukštis, mm | 1800 |
| C44 | Sukrautos paletės max svoris, kg | 800 |
| C45 | Ar reikalingi tarpsluoksniai (1/0) | 1 |
| C46 | Tarpsluoksnio svoris, kg | 0,7 |
| C47 | Tarpsluoksnio kaina, €/vnt | 0,48 |
| C49 | Pakavimo ant padėklo darbo kaina, €/PAL | 16 |
| C50 | Padėklo kaina, €/vnt | 10 |
| C51 | Strečo kaina, €/PAL | 3 |
| C52 | Tarpsluoksnių kaina, €/PAL | `=C66*C47` |
| C56 | Transporto kaina, €/PAL | 90 |

**"KIEK TELPA ANT PALETĖS" skaičiuoklė (B59:C68)** — tai ir yra "receptas", apie kurį
kalbėjai:

| Langelis | Laukas | Formulė | Reikšmė |
|---|---|---|---|
| C60 | Vnt. pakų viename sluoksnyje | `=INT(C39/C30)*INT(C40/C31)` | 16 |
| C61 | Galimų sluoksnių skaičius (pagal aukštį) | `=INT(C43/C32)` | 13 |
| C62 | Max pakų vnt. pagal aukštį | `=C60*C61` | 208 |
| C63 | Max pakų vnt. pagal svorį | `=INT((C44-C42)/C33)` | 140 |
| C64 | Maksimalus pakų vnt. ant padėklo | `=MIN(C62:C63)` | 140 |
| C65 | Sluoksnių skaičius ant padėklo (faktinis, rankinis) | 11 | 11 |
| C66 | Reikalingų tarpsluoksnių vnt. | `=C65` | 11 |
| C67 | Stiklainių/pakų vnt. skaičius ant padėklo | `=C64/C29*8` (iš esmės pakai×8) | 1056 |
| C68 | Padėklo svoris | `=C67/C29*C33` | 726,5 |

Tai automatinis pakavimo optimizavimo skaičiavimas: pagal dėžutės/padėklo matmenis ir
svorio limitus apskaičiuoja, kiek vienetų realiai telpa — **tai turi būti pilnai perkelta
kaip logika (ne hardcoded reikšmė), nes tai keisis kiekvienam naujam produktui/pakuotei.**

### 2.5 Savikainos suvestinė (H4:J13)

| Langelis | Laukas | Formulė | Reikšmė (su 0,9 kg) |
|---|---|---|---|
| I5 | Medaus kaina, €/vnt | `=C10*C15` | 2,880000 |
| I6 | Taros kaina, €/vnt | `=C19` | 0,210000 |
| I7 | Etikečių kaina, €/vnt | `=C22` | 0,080000 |
| I8 | Pirminio pakavimo kaina, €/vnt | `=C24` | 0,270000 |
| I9 | Antrinio pakavimo kaina (darbas+pakuotė), €/vnt | `=C34+C36` | 0,157500 |
| I10 | Pakavimas ant padėklo (darbas+medžiagos), €/vnt | `=D49+D50+D51+D52` | 0,032462 |
| I11 | Transportas, €/vnt | `=D56` | 0,085227 |
| **I12** | **VISO EXW kaina** | `=SUM(I5:I10)` | **3,629962** |
| **I13** | **VISO DAP kaina** | `=SUM(I5:I11)` | **3,715189** |

Kur D49:D56 — kiekvienos padėklo/transporto pozicijos kaina padalinta iš C67 (vnt./padėklas):
`D49=C49/C67`, `D50=C50/C67`, `D51=C51/C67`, `D52=C52/C67`, `D56=C56/C67`.

### 2.6 Pardavimo kaina (H16:I19)

| Langelis | Laukas | Formulė | Reikšmė |
|---|---|---|---|
| I17 | Uždarbio % | ranka įvesta | 0,15 (15%) |
| I18 | DAP pardavimo kaina | `=I13*(1+I17)` | 4,272467 |
| I19 | EXW pardavimo kaina | `=I12*(1+I17)` | 4,174456 |

### 2.7 Kiti blokai faile (žemesnis prioritetas ERP perkėlimui)

- **PAJAMŲ PLANAVIMAS** (H20:K24) — metinis planuojamas kiekis (27 600 kg / 55 200 vnt.),
  planuojama apyvarta ir pelnas, plius atskiras stulpelis su faktiniais/ataskaitiniais
  skaičiais (K23=67 999 €, K24=6 181 €). *Anksčiau nuspręsta, kad tai ne šio ERP modulio
  dalis (naudotojas paprašė šito neįtraukti į savikainos ataskaitą) — palikta čia tik
  užfiksavimui, jei prireiks atskiro pardavimų planavimo modulio.*
- **Konkurentai** (N16:Q17) — rankinė lentelė su konkurentų lentynos kainomis, skirta
  orientaciniam palyginimui, ne savikainos skaičiavimui.
- **Kartono taros savikainos skaičiuoklė** (N11:T13) — analogiška stiklo taros
  skaičiuoklei, bet dėžutėms.

---

## 3. Dabartinės savikainos skaidymas (galutinis rezultatas, su 0,9 kg pataisa)

| # | Sudedamoji dalis | €/vnt. |
|---|---|---|
| 1 | Žaliava – medus (3,20 €/kg × 0,9 kg) | 2,880000 |
| 2 | Pirminė pakuotė (stiklainis+dangtelis, etiketės, išpylimo darbas) | 0,560000 |
| 3 | Antrinė pakuotė (dėžutė + grupavimo darbas) | 0,157500 |
| 4 | Pakavimas ant padėklo (darbas + medžiagos) | 0,032462 |
| | **VISO EXW savikaina** | **3,629962** |
| 5 | Transportas | 0,085227 |
| | **VISO DAP savikaina** | **3,715189** |
| | EXW pardavimo kaina (+15%) | 4,174456 |
| | DAP pardavimo kaina (+15%) | 4,272467 |

Žaliava (medus) sudaro ~79% visos savikainos — didžiausias svertas kainos gerinimui.

---

## 4. Nustatytos problemos originaliame faile

1. **0,5 kg vs 0,9 kg** — "Taros talpa" laukas neatitiko produkto pavadinimo. Ištaisyta
   šiame dokumente ir ankstesnėse ataskaitose į 0,9 kg.
2. **Nesusieta stiklo taros skaičiuoklė** — pagalbinė lentelė (N4:T13) su detalizuotu
   taros savikainos skaičiavimu (transportas/vnt., dangtelis ir t.t.) egzistuoja atskirai
   ir nėra formule susieta su pagrindiniu C19 langeliu, kurį realiai naudoja suvestinė.
   Reikšmės skiriasi (0,9 kg stiklainiui skaičiuoklė duoda 0,1885 €, o C19=0,21 €).
3. **"1 kg kaina" stulpelis (J5:J13)** dalija €/vnt. iš C16 (taros diametras mm), o ne
   iš taros talpos kg — matematiškai tai nėra "€/kg", nepaisant antraštės. Šis stulpelis
   ERP modelyje neturėtų būti perkeltas be pataisymo arba turi būti aiškiai pervadintas.
4. Faile nėra atskiro **broko/nuostolių (%)** lauko — savikaina šiuo metu neįskaičiuoja
   galimo taros dužimo, medaus išpylimo nuostolių ar etikečių broko. Tai naujas laukas,
   kurio faile nebuvo, bet kurį verta pridėti ERP versijoje (žr. žemiau).

---

## 5. Siūlomas DB/ERP modelis

Tikslas: kiekvienas komponentas (tara, dėžutė, receptas, transportas) — pakartotinai
naudojamas katalogo įrašas, o kiekvienas skaičiavimas konkrečiam klientui/produktui —
išsaugoma "nuotrauka" (snapshot), kad būtų galima generuoti ataskaitas ir lyginti laike.

### 5.1 Katalogo lentelės (pakartotinai naudojamos, redaguojamos per UI)

**`honey_types`** — medaus rūšių/kilmių katalogas kainoms
- id, name, origin_country, price_per_kg, updated_at

**`packaging_components`** — visi fiziniai komponentai (stiklainis, dangtelis, etiketė,
dėžutė, padėklas, tarpsluoksnis, strečas)
- id, component_type (enum: jar/lid/label/box/pallet/interlayer/stretch_wrap),
  name, capacity_kg (nullable — tik jar/box), diameter_mm, height_mm, weight_kg,
  unit_price_eur, source_note

**`pallet_recipes`** — "receptas": kiek telpa ant padėklo konkrečiai
pakuotei (tai dabartinis "KIEK TELPA ANT PALETĖS" blokas, bet saugomas, ne perrašomas)
- id, name, box_id (FK → packaging_components), pallet_length_mm, pallet_width_mm,
  pallet_height_mm, pallet_weight_kg, max_stack_height_mm, max_stack_weight_kg,
  needs_interlayer (bool), interlayer_id (FK, nullable), layers_actual (rankinis
  patikslinimas, jei skiriasi nuo automatinio max)
- **Skaičiuojami laukai (logika servise, ne DB stulpeliai):** units_per_layer,
  max_layers_by_height, max_layers_by_weight, max_units_by_height, max_units_by_weight,
  max_units_final, units_per_pallet, pallet_total_weight

**`transport_rates`** — vežimo kainos
- id, destination (arba carrier + route), price_per_pallet_eur, valid_from, valid_to

**`waste_loss_rates`** *(naujas laukas, kurio faile nebuvo)*
- id, applies_to (enum: honey_fill/glass_breakage/label_defect/box_defect),
  loss_percent, note

### 5.2 Produkto ir skaičiavimo lentelės

**`products`**
- id, name (pvz. "LIDL LT medus 0,9 kg"), client_id (FK → business_partners arba
  atskira `customers` lentelė), net_weight_kg, jar_id (FK → packaging_components),
  label_id (FK), box_id (FK), pallet_recipe_id (FK), created_at

**`product_honey_mix`** — leidžia N medaus rūšių mišinį (ne fiksuotai 3, kaip Excel)
- id, product_id (FK), honey_type_id (FK), share_percent

**`cost_calculations`** — kiekvieno skaičiavimo "nuotrauka" konkrečiai datai/klientui
- id, product_id (FK), calculation_date, margin_percent,
  honey_cost_per_unit, primary_packaging_cost, secondary_packaging_cost,
  palletizing_cost, transport_cost, exw_cost_total, dap_cost_total,
  exw_sale_price, dap_sale_price, waste_adjustment_percent (jei naudota),
  created_by, notes

**`cost_calculation_lines`** — detali sudedamųjų dalių išklotinė (audit trail, kad
matytum tiksliai, kokios kainos buvo naudotos tą dieną, net jei katalogo kaina vėliau
pasikeis)
- id, cost_calculation_id (FK), component_type, component_name, unit_price_eur,
  quantity, line_total_eur

### 5.3 Ataskaitos generavimas

Ataskaita (analogiška anksčiau paruoštam Word dokumentui) generuojama iš
`cost_calculations` + `cost_calculation_lines` per pasirinktą `product_id` ir datą —
be poreikio iš naujo skaičiuoti rankiniu būdu. Galima:
- lyginti tą patį produktą skirtingoms datoms (kainų dinamika),
- lyginti skirtingus klientus/produktus vienoje ataskaitoje,
- eksportuoti į PDF/Word tiesiai iš ERP.

---

## 6. Ką reikia pasitikrinti prieš implementaciją (Claude Code sesijai)

1. Peržiūrėti esamą `nordic_bees_erp` schemą (`business_partners`, `honey_deliveries`)
   — ar `products`/`customers` sąvokos jau kaip nors egzistuoja, kad nebūtų dubliavimo.
   *(Šios sesijos metu DB MCP įrankis buvo nepasiekiamas — reikės patikrinti iš naujo.)*
2. Patvirtinti su naudotoju, ar `waste_loss_rates` (broko %) iš tikrųjų reikalingas, ar
   tai buvo tik pasiūlymas — faile tokio lauko nebuvo.
3. Nuspręsti, ar "Stiklo/kartono taros savikainos skaičiuoklė" (esama atskira pagalbinė
   lentelė) turėtų tapti pagrindiniu `packaging_components` kainos šaltiniu (t.y.
   automatiškai skaičiuoti tara kainą iš vnt./padėklas + transportas + dangtelis), ar
   likti rankinis įvedimas kaip dabar (C19).
4. Laikytis projekto standartų: `ExecuteSqlRawAsync` UPDATE/DELETE operacijoms (ne
   `FindAsync`+`SaveChanges`), servisai — ne UI tiesiogiai į DB, testai kiekvienam naujam
   DB-write metodui (žr. `nordicbees-standards.md`, `PATTERNS.md`, `FROZEN.md`).

---

*Šaltinis: MB Lakštena vidinė savikainos skaičiuoklė „gamybos_savikainos_skaciavimas.xlsx"
(produktas LIDL LT medus 0,9 kg, duomenų data 2026-02-19).*
