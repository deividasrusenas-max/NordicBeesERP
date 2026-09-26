# Sprendimų žurnalas

Tik pildomas. Sprendimai nešalinami — jei sprendimas atšaukiamas, prirašomas
naujas su nuoroda į senąjį.

---

## D-021 — Žalias Azure JSON saugomas DB stulpelyje `ocr_raw_json` (2026-09-16, įrašyta 2026-09-25)

**Pastaba.** Sprendimas priimtas sesijoje 04 ir ten pažymėtas kaip D-021, bet į šį
žurnalą neįrašytas. Atkurta iš `sessions/2026-09-16-04.md`; originalus pagrindimas
neužfiksuotas.

**Sprendimas.** Žalias Azure DI atsakymas rašomas į esamą `expense_invoices.ocr_raw_json`
stulpelį.

**Keičia D-004** (gzip diske, DB tik kelias). D-004 nurodytos rizikos (backup'ų dydis,
netyčinis stulpelio paėmimas užklausose) lieka galioti ir turi būti peržiūrėtos, jei
kiekis augs.

---

## D-022 — Etapų eiliškumas apverstas: vartai prieš ekstrakciją (2026-09-25)

**Kontekstas.** `analysis/RESEARCH-2026-09-25-reliability.md` ir prodo duomenų analizė
staginge (`analysis/PROD-DATA-FINDINGS-2026-09-25.md`).

**Sprendimas.** Deterministiniai vartai ir triukšmo mažinimas (Etapai 0–2) eina prieš
ekstrakcijos perrašymą (Etapas 3). **Keičia D-020 seką** 2–3 žingsniuose.

**Pagrindimas (patikslintas pagal prodo duomenis).** Didžiausia žala atsirado ne iš
atpažinimo:

- Dublikatų aptikimas **veikė** — visi 27 tikri dublikatai gavo `DUPLICATE_PENDING`.
  Gedimas: `DUPLICATE_PENDING` neturi karantino semantikos — eilutė skaičiuojama cash
  flow, eksporte ir sąrašuose kaip įsipareigojimas (28 679,74 €).
- 16 sąskaitų tyliai priskirta neteisingam tiekėjui (`FindSupplierIdAsync`, tuščias PVM
  kodas) — be jokios vėliavėlės.
- Sąskaitos su sulūžusia antraštės aritmetika (vienoje 465 374,45 € su neto 0,00)
  nesustabdomos, nes `AMOUNT_ARITHMETIC_MISMATCH` neįeina į statuso logiką.

Nė vienas iš jų nėra ekstrakcijos kokybės klausimas.

---

## D-023 — Kryptis: `prebuilt-layout` + sava `tables[]` serializacija (2026-09-25)

**Statusas: kryptis, ne galutinis sprendimas.**

**Kontekstas.** ASF0021438: `analyzeResult.tables[2]` turėjo teisingą lentelę su
`columnHeader`, o `documents[0].fields` paėmė „Suma su PVM" skiltį ir sulaužė skaičių
formatą.

**Kodėl ne galutinis.** D-016 sąlyga buvo ~10 sąskaitų žalių atsakymų; matyta viena.
D-016 lieka atviras, kol neperskaityta ~10. Taip pat prieštarauja D-019 („investuoti į
eilučių redagavimą, ne į ekstrakciją") — D-019 galioja, kol Etapo 3 pradžioje
nepriimtas galutinis sprendimas.

---

## D-024 — Statistinis pasitikėjimo kalibravimas atmetamas šiame etape (2026-09-25)

**Sprendimas.** Nedaroma: conformal prediction, apmokytas confidence modelis (CatBoost),
izotoninė regresija.

**Pagrindimas.** ~420 dok./metus per maža imtis; tyrimo 5 skyrius. Papildomai: ExtractConf
(vieno autoriaus workshop straipsnis, nepriklausomai nepakartotas) rodo AUC 0,896 vien
OCR požymiais prieš 0,928 su pilnu modeliu.

**Peržiūrėti**, jei tūris viršytų ~5 000 dok./metus.

---

## D-025 — `MISSING_DUE_DATE` — informacija, ne klaida; numatytas terminas žymimas (2026-09-25)

**Kontekstas.** Mokėjimo terminas nėra privalomas pagal Direktyvos 2006/112/EB 226 str.
Kode vėliavėlė statuso nekeičia, bet trūkstant termino jis **tyliai nustatomas
`invoice_date + 30`** ir patenka į cash flow kaip tikras.

**Sprendimas.** Vėliavėlė rodoma kaip informacija, ne kaip klaida. UI prie termino
aiškiai rodo, kad jis numatytas (+30 d.), o ne ištrauktas iš dokumento. Vėliavėlė
išlieka duomenyse kaip šio fakto žymė.

---

## D-026 — `ZERO_VAT` sprendžiamas formuluotės patikra pagal sandorio tipą (2026-09-25)

**Sprendimas.** Kai PVM 0 %, tikrinama, ar dokumente yra teisinio pagrindo formuluotė.
Yra — vėliavėlė užsidaro; nėra — tikra atitikties rizika.

**Svarbu — formuluotė priklauso nuo sandorio tipo**, ne viena eilutė:

- prekių tiekimas ES viduje (226 str. 11 p.) — nuoroda į neapmokestinimą
  (pvz. „steuerfreie innergemeinschaftliche Lieferung", „Art. 138");
- atvirkštinis apmokestinimas (226 str. 11a p.) — „Reverse charge" / nacionalinis
  atitikmuo;
- kitos išimtys — nuoroda į išimties pagrindą.

CJEU C-247/21 konkrečiai liečia trikampę prekybą; jo išvada, kad praleidimo negalima
ištaisyti atgaline data, netaikoma automatiškai visiems 0 % atvejams.

Formuluočių sąrašas kalboms LT/DE/LV/EE/PL/RO/UA — Q-009. Įgyvendinama Etape 3.
ULAK sąskaitoms netaikoma (`FROZEN.md` §4).

---

## D-027 — Dublikato vartas = karantinas, ne nauja aptikimo logika (2026-09-25)

**Kontekstas.** `CheckDuplicateAsync` (OCR kelias) ieško pagal numerį + sumą ±0,01,
nepriklausomai nuo tiekėjo, ir prode pagavo visus 27 tikrus dublikatus. Bet sąskaita vis
tiek sukuriama ir `DUPLICATE_PENDING` skaičiuojama visur.

**Sprendimas.**

1. `DUPLICATE_PENDING` ir `REJECTED` neįtraukiami į jokias sumas, cash flow ir eksportą,
   nebent filtras aiškiai prašo to statuso.
2. `DUPLICATE_PENDING` yra blokuojantis statusas — sąskaita toliau nejuda, kol neišspręsta.
3. Sprendimas „tai skirtinga sąskaita" ir „ištrinti" fiksuojami audito žurnale;
   „ištrinti" reiškia `REJECTED`, ne `DELETE` (apskaitos dokumentas).

**Žinoma spraga.** Numeris + suma duoda klaidingą teigiamą, kai numeris trumpas ir
suma 0,00: `277` (Rotada) sulygintas su `173` (Franko), abu „1" / 0,00 €. Dublikato
paieška neturi remtis sąskaita su suma 0,00.

---

## D-028 — Antraštės aritmetikos vartas perkeliamas į Etapą 0 (2026-09-25)

**Kontekstas.** `AddAmountConsistencyFlags` jau skaičiuoja `excl + vat = incl` ir deda
`AMOUNT_ARITHMETIC_MISMATCH` / `MISSING_MONEY_FIELD`, bet šios vėliavėlės neįeina į
statuso nustatymą. Prode `213` (465 374,45 €, neto 0,00), `167`, `168` praėjo.

**Sprendimas.** Abi vėliavėlės → `NEEDS_REVIEW`. Tai BR-CO-15 poaibis; pilnas EN 16931
modulis lieka Etape 1.

**Kodėl į Etapą 0.** Kaina — viena sąlyga statuso logikoje; nauda — sustabdo didžiausią
vieno įrašo klaidą prode.

---

## D-029 — Staging atnaujinamas kaip prodo klonas (2026-09-25)

**Kontekstas.** `__EFMigrationsHistory` staginge rodė visas migracijas, bet schema
skyrėsi nuo prodo (trūko `deliveries`, `expense_payments` stulpelių, ~12 artwork
indeksų, tarp jų unikalių; `invoice_audit` tipai skyrėsi). Istorijos eilutės buvo
įrašytos ranka be DDL. Istorija nėra schemos įrodymas.

**Sprendimas.** Staging atnaujinamas taip: backup → prodo `mariadb-dump` (patikrinus, kad
nėra `USE`/`CREATE DATABASE`) → į staging bazę → neišleisti pakeitimai ant viršaus →
schemos diff prieš prodą per `information_schema` (turi likti tik neišleisti pakeitimai).

**Pasekmė.** Staginge yra realūs prodo duomenys ir prodo `app_settings` (įskaitant Azure
DI raktą). Prieš startą tikrinama: eilės tuščios, išorinių siuntimų (SMTP, Telegram)
konfigūracija tuščia.

---

## D-030 — Prodo DB: skaitymas per MCP leidžiamas savininko nurodymu; DDL tik žmogus (2026-09-25)

**Kontekstas.** Savininkas šioje sesijoje aiškiai leido Claude pačiam atlikti prodo
pasiruošimą `files` / `file_id` schemai. Prodo MCP (`lcl-mysql-prod`, per SSH tunelį
`127.0.0.1:3307`) **pats blokuoja** `CREATE` ir `ALTER` — įrankis leidžia tik skaitymą.

**Kas padaryta 2026-09-25.**

1. Savininkas: pilnas prodo backup'as `lakstena-dev:~/backup/prod-before-files-ddl-2026-09-25.sql.gz`
   (dump completed 22:43:56, 64 lentelės).
2. Claude per MCP (tik `SELECT`): tapatybė (`lakstena-dev.self`, MariaDB 11.8.2,
   `nordic_bees_erp`, `erp_user@%`), pradinė būsena (nėra `files`, nėra `file_id`).
3. Savininkas: `reapply-files.sql` + `ALTER TABLE expense_invoices ADD COLUMN file_id
   bigint(20) DEFAULT NULL` — tas pats DDL, kuris pritaikytas staginge (D-029).
4. Claude per MCP: schemos diff prodas ↔ staging per `information_schema` — 0 stulpelių,
   0 indeksų skirtumų, po 65 lenteles; 247 sąskaitos nepakito.

**Sprendimas.**

- Prodo **skaitymas** per MCP agentui leidžiamas tik su aiškiu savininko leidimu tos
  sesijos metu. `AGENTS.md` taisyklė kodo agentams (OpenCode, Claude Code) **nekeičiama**:
  jie prodo neliečia jokiomis aplinkybėmis.
- Prodo **DDL ir rašymas** — visada žmogus. MCP blokavimas yra teisingas ir paliekamas.

**Atsitiktinis įrodymas I-13.** `erp_user` per prodo MCP mato `nordic_bees_erp_staging`
schemą `information_schema` — t.y. turi teises abiem bazėms.

**Atšaukimas, jei prireiktų:** `DROP TABLE files; ALTER TABLE expense_invoices DROP COLUMN
file_id;` — duomenų šis pakeitimas neliečia.

---

## D-031 — Modulio baigtumo kriterijai: kokybė prieš greitį (2026-09-26)

**Kontekstas.** Savininkas: „darom nuosekliai ir patikimai — modulis baigtas, kai veikia
idealiai". Siūlytas apimties apkarpymas (Etapai 3–4 atidėti) **atmestas**.

**Principas.** Nė viena klaida nepraeina tyliai: kiekviena sąskaita arba teisinga, arba
garsiai sustabdyta. „100 % atpažinta" su OCR nepasiekiama; nulis tylių klaidų —
pasiekiama ir patikrinama.

**Modulis baigtas, kai įvykdyti visi šeši:**

1. Visi kieti vartai veikia (dublikatas, EN 16931 aritmetika, datos, tiekėjas, PVM kodas,
   IBAN, PVM tarifas) — testai + staging patikra ant prodo klono.
2. Tiekėjas randamas be spėjimų (kaskada; dviprasmiška → „nerastas").
3. Ekstrakcija ant realaus korpuso (40 dev + 20 hold-out, ranka patikrinta): pinigų
   laukai hold-out aibėje — 0 tylių klaidų.
4. Golden-file regresija ant saugomo žalio JSON.
5. Prodo auditas: 60 atsitiktinių auto-priimtų sąskaitų prieš PDF — 0 tylių klaidų
   („< 5 % su 95 % pasikliovimu", tyrimo 7 sk.).
6. Peržiūros eilė gyva: nė viena sąskaita neišspręsta ilgiau nei **5 darbo dienas**
   (numatyta reikšmė; savininkas gali pakeisti).

**Tvarka.** Etapai 0 → 1 → 2 → 3 → 4 nuosekliai; kiekvienas uždaromas (užduotis →
agentas → Claude peržiūra → pilni testai → staging → prodas) prieš kitą. Lygiagrečiai
leidžiami tik izoliuoti darbai (tik nauji failai, be DB), jų peržiūra ir suliejimas —
nuosekliai.

---

## D-032 — Įvestis: tik skaitmeniniai PDF (2026-09-26)

**Sprendimas.** Modulis priima tik PDF su teksto sluoksniu. Paveikslėliai (JPG/PNG/…) ir
skenuoti PDF (be teksto sluoksnio) atmetami prieš Azure su aiškiu pranešimu.

**Pasekmės.**

- SHA-256 dedup (B5) tampa pilnas — paveikslėlių konvertavimo nedeterminizmo problema
  išnyksta kartu su paveikslėlių keliu.
- Etapo 3 (D-023) ekstrakcijos dizainas paprastėja: skenuotų dokumentų klasė (tyrimo
  2 sk. — didžiausias vaizdo/teksto skirtumas) iš apimties iškrenta.
- Skenuotų sąskaitų palaikymas — atskiras vėlesnis sprendimas, ne šio modulio baigtumo
  kriterijus.

---

## D-033 — Leidžiama keisti `BankImport.razor` kandidatų filtrą (FROZEN §7) (2026-09-26)

**Kontekstas.** `BankImport.razor:510–512` siūlo `DUPLICATE_PENDING` ir `REJECTED`
sąskaitas kaip banko mokėjimo atitikmenis — mokėjimas gali būti užskaitytas
dublikatui, o tikras originalas lieka neapmokėtas. Tai apeina D-027 karantiną.

**Sprendimas.** Savininkas leidžia vieną pakeitimą užšaldytame bloke: kandidatų
predikatas papildomas `DUPLICATE_PENDING` ir `REJECTED` išimtimis (aiškūs `!=`,
FROZEN §10). Kitos failo eilutės ir `BankImportService.cs` — neliečiami. Leidimas
galioja tik šiam pakeitimui (Etapas 0c, C3).

---

## D-034 — FROZEN §3 buvo netikslus; leidžiama naujas `OnFileDropped` ir klaidos antraštė (2026-09-26)

**Kontekstas.** `FROZEN.md` §3 teigė, kad `ExpenseUploadDialog.razor` turi veikiantį
`[JSInvokable] OnFileDropped`. Git istorija rodo, kad **šiame faile jo niekada nebuvo**
(nuo pradinio commit'o `9e6f82f`); vienintelė implementacija buvo `ArtworkUpload.razor`
(`6738014`, vėliau pašalinta). Drag & drop išlaidų dialoge niekada neveikė. Etapo 0c
užduotis (C9 „atkurti iš istorijos") rėmėsi šiuo netiksliu dokumentu.

**Sprendimas.** Leidžiama:

1. Parašyti **naują** `[JSInvokable] OnFileDropped(string fileName, long size, string mimeType)`
   pagal `dropzone.js` kontraktą (`getDropFileBase64("expense-drop-zone")`), per tą patį
   PDF-only kelią kaip `OnFileChanged`, naudojant esamą `DroppedFile` klasę.
   `dropzone.js`, `App.razor`, `OnAfterRenderAsync`, `DisposeAsync` — neliečiami.
2. Pakeisti klaidos fazės antraštę („OCR nepavyko" rodoma tik OCR klaidoms).
3. Pataisyti `FROZEN.md` §3, kad atspindėtų tikrą būseną.

**Pamoka.** FROZEN.md teiginiai apie kodą tikrinami prieš jų pagrindu rašant užduotį.

---

## D-035 — Redagavimo formoje antraštė autoritetinga; vienas išsaugojimas, viena transakcija (2026-09-26)

**Kontekstas.** `InvoiceDetailDialog.SaveAsync` kviečia `UpdateInvoiceAsync` (vartai
vertina vartotojo įvestas sumas), tada išsaugo eilutes ir `RecalculateInvoiceTotalsAsync`
**perrašo antraštės sumas eilučių sumomis**. Pasekmės: išsaugotos sumos skiriasi nuo
tų, kurias tikrino vartai; sąskaita **be eilučių išsaugoma su 0,00** visose trijose
sumose. Tai prieštarauja D-019 (antraštė autoritetinga, eilutės patariamosios) ir D-010
(persist — viena transakcija).

**Sprendimas.**

1. Redagavimo išsaugojimas — vienas serviso metodas, viena transakcija: antraštė +
   eilutės + vėliavėlės + statusas + auditas.
2. Antraštės sumos niekada neperrašomos iš eilučių. Nesutapimas → `AMOUNT_MISMATCH`
   vėliavėlė (D-019), ne tylus pakeitimas.
3. Vartai vertinami ant **galutinių** išsaugomų reikšmių.

**Kiti `RecalculateInvoiceTotalsAsync` kviečiamieji** — inventorizuojami; jei kuris
nors perrašo antraštę kitame kelyje, pranešama atskirai.

---

## D-036 — Biudžeto faktas: kategorijos šaltinių grandinė, neto (2026-09-26)

**Kontekstas.** `ExpenseBudgetDialog` faktas skaičiuojamas iš `[NotMapped]` eilučių — visada
0. Galimi kategorijos šaltiniai: paskirstymas (`expense_line_allocations`), eilutės
`category_id`, sąskaitos `category_id`.

**Sprendimas.** Kiekvienai eilutei: paskirstymas, jei yra → kitaip eilutės kategorija →
kitaip sąskaitos kategorija. Suma — **be PVM** (MB Lakštena yra PVM mokėtoja, PVM
atskaitomas, sąnaudos — neto). Karantino sąskaitos (`DUPLICATE_PENDING`, `REJECTED`)
neįskaičiuojamos. Eilutės be jokios kategorijos — atskira „Nepriskirta" suma, ne
išmetamos. Sąskaita be eilučių — antraštės neto su sąskaitos kategorija (arba
„Nepriskirta").

---

## D-037 — Deploy politika: kitas prodo deploy'us — tik baigus Etapus 1–4 (2026-09-26)

**Kontekstas.** Etapas 0 (+ 0c ir Etapo 1 validatoriai, dar neprijungti) po staging patikrų
2026-09-26 deploy'intas į prodą kaip **v0.17.91** (`main` `a1ad12a` → `production`). Staging
patikrų rezultatai — `STATE.md`.

**Sprendimas (savininkas).** Kitas prodo deploy'us vyksta tik tada, kai Etapai 1–4 **visiškai
baigti ir patikrinti staginge**. Daliniai Etapų 1–4 deploy'ai į prodą nedaromi.

**Pasekmės.**

- `main` kaupia Etapus 1–4; staging yra integracijos aplinka (kiekvieno etapo patikra ten).
- Prodas lieka v0.17.91, kol neįvykdyta aukščiau nurodyta sąlyga.
- Skubūs prodo pataisymai, jei kada prireiktų, — atskiras savininko sprendimas, ne šios
  politikos išimtis „savaime".
- Keičia D-031 „Tvarka" dalį, kiek ji liečia prodą: etapas uždaromas staginge; prodas — vienu
  deploy'umi po Etapo 4.

---

## D-038 — Etapo 1 sprendimai (atsakymai į PLAN-ETAPAS1 Q1–Q11) (2026-09-26)

**Kontekstas.** `PLAN-ETAPAS1.md` §9 — 11 klausimų savininkui. Atsakymai žemiau; numeriai
atitinka plano klausimus.

**Sprendimai.**

- **Q1 — tolerancija: sprendžiama pagal duomenis.** Savininkas paleidžia plano §7 staging
  užklausas; kol jų nėra, S4 nepradedamas. Numatytas pasiūlymas: BR-CO-15 — tiksliai
  (Schematron); BR-CO-10 skirtumai ≤ 0,05 € — informacija, didesni — peržiūra. Patvirtinama
  su skaičiais.
- **Q2 — PVM tarifų lentelė:** LT, LV, EE, DE, PL, RO. Buhalterė kiekvieną tarifą patikrina
  pagal EK TEDB (Taxes in Europe Database) **prieš** prijungiant tarifų vartus (S6). Nežinoma
  šalis → `VAT_RATE_UNCHECKED` (informacija).
- **Q3:** `LINE_AMOUNT_IMPLAUSIBLE` — informacija.
- **Q4:** `VAT_FORMAT_UNCHECKED` ir `VAT_RATE_UNCHECKED` — informacija.
- **Q5:** priskyrus tiekėją, dokumento `INVALID_IBAN` / `INVALID_VAT_FORMAT` — informacija
  (tapatybę jau nustatė žmogus). Neteisingas IBAN **neperkeliamas** į `SupplierCreateDialog` —
  laukas lieka tuščias, rodomas įspėjimas lietuviškai.
- **Q6:** tik aptikimas; Etape 1 reikšmės automatiškai niekada nekeičiamos.
- **Q7:** suderinimo žingsniai „šalinti eilutes su kiekiu > 1000" ir „šalinti pasikartojančius
  aprašymus" nebeištrina — tik pažymi (jokio tylaus duomenų pakeitimo).
- **Q8:** re-OCR sąskaitai su paskirstymais — įspėti, reikalauti patvirtinimo, pašalintus
  paskirstymus įrašyti audito eilutėje.
- **Q9:** D-016 sąlyga „~10 žalių atsakymų" galioja tik Etapui 3 (ekstrakcijai);
  deterministiniai vartai vykdomi pagal D-022. ~11 Azure kvietimų korpusas iš principo
  patvirtintas, bet korpuse yra asmens duomenų (bitininkų vardai, adresai) → saugomas **už
  git ribų**; jo reikalaujantys testai praleidžiami, kai korpuso nėra. Atskira užduotis prieš S7.
- **Q10:** eilės darbuotojas (`OcrQueueWorker`) — atidėta, kol modulis baigtas (Etapuose 1–4
  neliečiamas).
- **Q11:** našlaičiai dialogai (biudžetas, cash flow, tiekėjo istorija) — prijungiami prie
  išlaidų suvestinės („Suvestinė") kaip mygtukai; atskira nedidelė užduotis, ne Etapas 1.

---

## D-039 — unit_price tikslumas, Q5 po redagavimo, PVM tarifų galiojimo pradžia (2026-09-26)

**Kontekstas.** Etapo 1 S1–S2 raportas (`.opencode/reports/etapas1-s1-s2-20260926-1427.md`):
`expense_invoice_lines.unit_price` yra `decimal(12,2)` (0,2066 → 0,21); D-038 Q5 veikė tik
priskiriant tiekėją; `VatRateTable` pradžios datos nebuvo žinomos.

**Sprendimai.**

1. **`expense_invoice_lines.unit_price` → `decimal(18,6)`** (kuro kainos turi 3 skaitmenis po
   kablelio, medžiagų — 4). Agentas parašo modelio pakeitimą ir EF migraciją; DDL dev ir
   staging aplinkose vykdo savininkas; prode — per galutinį Etapų 1–4 deploy'ų (D-037). S4
   laukia šio pakeitimo.
2. **Q5 išplėstas:** kai sąskaita turi tiekėją (nesvarbu, kaip priskirtą), dokumento
   `INVALID_IBAN` / `INVALID_VAT_FORMAT` yra informacija **visuose** keliuose (sukūrimas,
   re-OCR, redagavimas, priskyrimas). Schemos keitimo nėra. Pagrindimas: OCR tiekėją priskiria
   tik tiksliai sutapus PVM kodui arba tiksliam pavadinimui.
3. **`VatRateTable` galiojimas prasideda 2025-01-01** visoms šalims; ankstesnės sąskaitų datos →
   `VAT_RATE_UNCHECKED` (informacija). Šalys: LT, LV, EE, DE, PL, RO, CZ, ES (staging duomenys:
   su tiekėju susietos sąskaitos — LT 83, LI 11, RO 3, CZ 2, PL 2, ES 1; pasitaikę tarifai —
   21, 0, 12, 23, 24, 8, 22). 11 „LI" partnerių — įtariamos pagrindinių duomenų klaidos;
   atviras klausimas, LI į lentelę neįtraukiama. Tarifai lieka NEPATVIRTINTI, kol buhalterė
   nepatikrina pagal EK TEDB.

---

## D-040 — EN 16931 vartų slenksčiai (2026-09-26)

**Kontekstas.** D-038 Q1 tolerancijas paliko spręsti pagal duomenis. `PLAN-ETAPAS1.md` §1.4–§1.5,
S4. Staging matavimas 2026-09-26 (savininkas, plano §7 užklausa).

**Sprendimai.**

1. **BR-CO-15** (antraštė: be PVM + PVM = su PVM) — **tiksliai**, kaip Schematron (`round()` iki
   centų, lygybė, jokios plokščios tolerancijos). Pažeidimas → `AMOUNT_ARITHMETIC_MISMATCH`
   (peržiūra). Staging matavimas 2026-09-26: tikslus 0,01 palyginimas papildomai pažymi **2 iš 248**
   sąskaitų (iki šiol tolerancija buvo 0,02).
2. **BR-CO-10** (eilučių sumų be PVM suma = antraštės suma be PVM), skirtumas po Schematron
   apvalinimo:
   - **> 0 ir ≤ 0,05 €** → nauja informacinė vėliavėlė `LINE_SUM_ROUNDING` („Eilučių suma skiriasi
     keliais centais"), statuso nekeičia;
   - **> 0,05 €** → `AMOUNT_MISMATCH` (peržiūra).
   Pakeičia abi senas patikras (OCR kelias ~0,05 ir redagavimo kelias 0,01). Staginge dar
   **neišmatuota** — patvirtinama S8 su realiais skaičiais; jei informacinė juosta pasirodys
   nenaudinga ar triukšminga, sprendimas peržiūrimas.
3. **Eilutės taisyklė** (`LINE_AMOUNT_IMPLAUSIBLE`, kiekis × kaina ≈ eilutės suma) — informacija
   (D-038 Q3).
4. **Etape 1 realiai veikia tik BR-CO-10 ir BR-CO-15** (plano §1.4). Sąskaita turi vieną sumą be PVM,
   ji paduodama ir kaip BT-106, ir kaip BT-109, nuolaidų / priemokų (BT-107/108) nėra, todėl
   **BR-CO-13 visada tenkinama**; mokėtinos sumos (BT-115) nėra, todėl **BR-CO-16 visada
   netaikoma**. Tai užrašyta, kad niekas nemanytų, jog veikia keturios taisyklės.

---

## D-041 — S7 apimtis pagal korpusą (2026-09-26)

**Kontekstas.** D-038 Q9 leido ~11 Azure kvietimų korpusą, saugomą **už git ribų**
(`~/NordicBeesERP-corpus/`, asmens duomenys). PLAN-ETAPAS1 §3.3: „ar `NUMBER_AMBIGUOUS` dažnai
suveikia teisingoms reikšmėms — spręsti pagal duomenis". Korpusas surinktas vienkartine
programa už repozitorijos ribų (ta pati `prebuilt-invoice`, `lt-LT`, `pages 1-2`, SDK 1.0.0
API versija kaip `ExpenseOcrService`; raktas tik iš aplinkos kintamųjų).

**Korpuso dydis.** 14 vietinių PDF: 11 skaitmeninių (11 Azure kvietimų), 3 skenuoti — atmesti
D-032 patikra prieš Azure. 11 atsakymų, 52 sąskaitų eilutės.

**Skaičiai** (`LocaleNumberCandidates`, kiekvieno skaitinio lauko atspausdintas `content` prieš
Azure tipizuotą reikšmę; M = sutampa, X = klaidingai perskaityta, A = dviprasmiška, NC = nėra ką tikrinti):

| Lauko tipas | M | X | A | NC |
|---|---|---|---|---|
| antraštės sumos (be PVM, PVM, su PVM) | 31 | 0 | 0 | 0 |
| kiekis | 37 | 0 | 0 | 0 |
| vieneto kaina | 25 | 3 | 1 | 0 |
| eilutės suma | 48 | 0 | 0 | 1 |

- 3 X — vienos sąskaitos kuro kainos su trimis skaitmenimis po kablelio: atspausdinta „0,115",
  „0,118", „0,006"; Azure grąžino 115, 118, 6. Griežtas kandidatas kiekvienu atveju vienintelis
  (0,115 / 0,118 / 0,006). Šios klasės PLAN-ETAPAS1 nenumatė (jame — tik kiekiai).
- 1 A — „11,990" (kandidatai 11,99 ir 11 990), Azure grąžino 11 990, kiekis 1, eilutės suma 11,99:
  **Azure pasirinkimas klaidingas**, eilutės aritmetika (1 × 11 990 ≠ 11,99) jam prieštarauja.
- 1 NC — eilutės sumos lauko tekstas „€" be reikšmės (nėra ką lyginti).
- Antraštės sumos ir kiekiai: 0 klaidų iš 68 patikrintų laukų. Kiekių su „1,000"-tipo dviprasmybe
  korpuse nėra (0 iš 37).

**Sprendimai.**

1. **`NUMBER_MISREAD`** (Azure reikšmė nėra griežtas kandidatas) — **peržiūra, visada**. Tai tikra
   klaida, ne spėjimas (3 iš 3 korpuso atvejų — tikros Azure klaidos).
2. **`NUMBER_AMBIGUOUS` — peržiūra, visada.** D-041 sąlyga švelninti (informacija, kai eilutės
   aritmetika sutampa su Azure pasirinkimu) taikoma tik jei korpusas rodo, kad vėliava dažnai
   suveikia **teisingoms** reikšmėms. Korpusas rodo priešingai: 1 dviprasmybė iš 118 patikrintų
   skaitinių laukų (0 iš 37 kiekių), ir ta viena — klaidinga reikšmė, kurios aritmetika **nepatvirtina**
   (todėl ir švelninta taisyklė duotų peržiūrą). Aritmetikos išimtis nerealizuojama. Ribotumas:
   11 sąskaitų — mažas pavyzdys; jei vėliau (staging) pasirodys daug teisingų „1,000" kiekių,
   sprendimas peržiūrimas su tais skaičiais.
3. **Reikšmės niekada nekeičiamos** (D-038 Q6): rodomi tik Azure reikšmė ir griežtas(-i)
   kandidatas(-ai) iš dokumento teksto.
4. **D-038 Q7 — pašalinimo žingsniai nebetrina, eilutės paliekamos ir pažymimos INFORMACIJA**
   (ne peržiūra): `LINE_LARGE_QUANTITY` („kiekis > 1000") ir `LINE_DUPLICATE_DESCRIPTION`
   (pasikartojantis aprašymas). Vėliavėlė dedama tik toje situacijoje, kurioje senasis žingsnis
   būtų trynęs (eilučių suma viršija antraštę > 0,05 € po nulinių eilučių pašalinimo). Pagrindimas
   skaičiais: (a) korpuse 3 eilutės su kiekiu > 1000 (1600, 1800, 11 000 — kuras) — visi trys kiekiai
   **teisingai** atspausdinti (Match), t. y. senasis žingsnis trintų teisingas eilutes; (b) toje
   situacijoje BR-CO-10 jau laiko sąskaitą peržiūroje (`AMOUNT_MISMATCH`), todėl papildomas
   peržiūros signalas nieko nepridėtų — informacija tik nurodo įtariamas eilutes.
   **Sąžiningai:** vienoje korpuso sąskaitoje (10 eilučių, 2 aprašymai po 3 kartus, eilučių suma
   2 385,61 prieš antraštę 2 060,33) senasis 3-ias žingsnis (pagal korpuso sumas, apskaičiuota
   ranka — `ProcessAsync` be Azure nevykdytas) pašalindavo 4 eilutes ir suma sutapdavo iki cento su
   antraštės — t. y. čia jis, atrodo, veikė teisingai. Pagal D-038 Q7 to nebedaroma:
   tokia sąskaita dabar lieka peržiūroje (`AMOUNT_MISMATCH`) su informacine vėliavėle. Tai
   sąmoninga kaina „jokio tylaus duomenų keitimo" principui.
5. **Kaip vėliava išvaloma.** OCR keliuose (sukūrimas, re-OCR) `NUMBER_*` perskaičiuojamos iš
   galutinių reikšmių: žmogus pakeitęs reikšmę įkėlimo lange — vėliava dingsta tam laukui.
   Redagavimo kelyje jos **perkeliamos nepakeistos** (neperskaičiuojamos, nes atspausdinto teksto
   redagavimo forma neturi); sąskaitos peržiūrą užbaigia **PATVIRTINTI**, o vėliavėlė lieka kaip įrašas.
   Re-OCR perskaičiuoja iš naujo.

---

## D-014 — F0.5 „triukšmo mažinimas" atmestas kaip simptomų lopymas (2026-09-15)

**Kontekstas.** Po produkcijos audito siūlyta F0.5 fazė: atskiri A7, A8, dublikatų
bloko ir vėliavėlių pataisymai.

**Sprendimas.** Atmesta tokia forma. A7 nėra „trečias žingsnis ima ne tą lauką" —
tai pasekmė to, kad kandidatų grandinėje **nėra validacijos**: žingsnis priima bet
kokią netuščią reikšmę. Pataisius vieną žingsnį, architektūra lieka ta pati ir
sekantis tiekėjas sukurs tokią pat klaidą kitoje vietoje.

**Kryptis vietoj to (dar nepatvirtinta — žr. D-016):** kandidatas + validatorius +
kilmė. Kiekvienas tikslinis laukas turi kandidatų sąrašą, kiekvienas kandidatas —
patikrinimą, ar reikšmė tinka **būtent tam laukui** (LT įmonės kodas 9 skaitmenys,
PVM kodas LT+9/12, IBAN kontrolinė suma, data ne ateityje, excl+PVM=incl).
Tada A7 tampa neįmanomas, ne pataisytas.

---

## D-015 — „Etalonas iš vartotojo taisymų" atmestas kaip logiškai ydingas (2026-09-15)

**Kontekstas.** Siūlyta, kad peržiūros forma, fiksuojanti kiekvieno lauko
„prieš/po", pagamins etaloną be rankinio darbo, ir F3 taps nereikalinga.

**Sprendimas.** Atmesta. Trys priežastys:

1. Kuo geriau veikia modulis, tuo mažiau taisymų — o etalono labiausiai reikia ten,
   kur modulis klysta **tyliai**, ir būtent tų atvejų vartotojas netaiso, nes nemato.
2. Kai vartotojas nieko netaiso, tai dviprasmiška: arba reikšmė teisinga, arba jis
   jos netikrino. Laikant tai patvirtinimu, modulis matuojamas savo paties klaidomis.
3. Taisymai yra **klaidų žurnalas**, ne etalonas. Vertingi — rodo, kurie laukai ir
   kurie tiekėjai kelia problemų — bet tikslumo jais matuoti negalima.

**Pasekmė.** Rankinio etalono poreikis lieka plane. Sumažėjęs (validatoriai dalį
atvejų gaudo be jo), bet nepanaikintas.

**Peržiūros forma išlieka verta savaime** — modulis buvo atmestas dėl darbo kiekio po
atpažinimo, ne dėl tikslumo. Tik ne kaip etalono gamykla.

---

## D-016 — Architektūros sprendimas atidėtas, kol nepamatytas žalias Azure atsakymas (2026-09-15)

**Kontekstas.** Visa siūloma architektūra rėmėsi prielaida, kad **Azure grąžina
teisingus duomenis, o kodas juos blogai sudėlioja**. Prielaida sutapo su Deivido
patirtimi ir skambėjo įtikinamai.

**Bet jos niekas netikrino.** `ocr_raw_json` tuščias visose 247 sąskaitose — nė vieno
Azure atsakymo nėra matę. Nežinome, ar grąžinamas `VendorTaxId`, ar eilutės ateina
pilnos, ar per-lauko confidence užpildytas.

**Sprendimas.** Architektūros sprendimas (kanoninis modelis, validatoriai, tiekėjų
kaskada) **atidėtas**, kol:

1. Saugykla veikia (PDF + žalias JSON išsaugomi).
2. ~10 sąskaitų praleista per Azure ir žali atsakymai perskaityti.

Jei Azure grąžina prastai, validatoriai ir kanoninis modelis nieko neduos — turėsim
tvarkingą architektūrą virš blogų duomenų, ir reikės visai kito sprendimo.

---

## D-017 — Alias mokymasis tik iš aiškaus vartotojo veiksmo (2026-09-15)

**Kontekstas.** Siūlyta tiekėjų alias lentelė, pildoma automatiškai, kad
`VENDOR_NOT_FOUND` konverguotų į nulį.

**Rizika, pastebėta redteam metu.** Konvergavimas į nulį reiškia, kad sistema nustoja
klausti — įskaitant tuos atvejus, kai priskiria **neteisingai**. Šiandien blogiausias
atvejis yra garsūs „neradau"; su automatiniu mokymusi jis tampa tyliu „radau, ir tai
ne tas tiekėjas", kartojamu kas mėnesį.

**Sprendimas.** Alias įrašas gali kilti **tik iš aiškaus vartotojo veiksmo**, niekada
iš automatinio neaškaus pavadinimo sutapimo. Turi būti matomas ir atšaukiamas.

---

## D-018 — Shadow mode yra regresijos apsauga, ne matavimas (2026-09-15)

**Kontekstas.** Teigta, kad shadow mode (du ekstraktoriai lygiagrečiai) leidžia
matuoti kokybę be etalono.

**Sprendimas.** Netiesa. Shadow gaudo tik tuos atvejus, kur du keliai **nesutampa**.
Ten, kur senas ir naujas klysta vienodai — o taip bus dažnai, nes abu ima tuos pačius
Azure laukus — skirtumo nėra, ir klaida lieka nematoma.

Shadow patvirtina „nieko nesulaužiau". Jis nesako „dabar teisingai".

---

## D-019 — Eilutės: antraštė autoritetinga, eilutės patariamosios (2026-09-15)

**Kontekstas.** Nei validatoriai, nei alias mokymasis eilučių interpretavimo
nepagerins. Tikėtis, kad Azure eilutes skaitys gerai, nėra pagrindo.

**Sprendimas.** Antraštės sumos laikomos autoritetingomis, eilutės — patariamosiomis.
Niekada tyliai netrinti (A5). Neatitikimas žymimas vėliavėle. Investuoti į greitą
eilučių redagavimą formoje, ne į jų ekstrakcijos tobulinimą.

---

## D-020 — Seka (2026-09-15)

1. **Saugykla** — PDF + žalias Azure JSON. Be jos nesikaupia niekas.
2. **~10 sąskaitų per Azure + žalio JSON analizė** — atsako, kiek problemos yra Azure
   pusėje, o kiek kode. Iki tol bet koks planas yra spėjimas.
3. **Architektūros sprendimas** — tik po 2 (D-016).
4. Autorizacija prieš peržiūros formą (forma bus naujas puslapis, `[Authorize]` neveikia).

---

## D-001 — Rasti P0 defektai (2026-09-13)

**Kontekstas.** Modulio analizė (`ExpenseOcrService.cs`, `OcrQueueWorker.cs`,
`ExpenseUploadDialog.razor`, `Program.cs`).

**Radiniai, kuriuos privaloma ištaisyti F0 fazėje:**

1. `OcrQueueWorker` rašo `invoice.PaidAmount = invoice.AmountInclVat` — kiekviena
   per eilę apdorota sąskaita tampa „apmokėta". Gadina skolų ir cash flow ataskaitas.
2. `Program.cs` turi globalų `QueryTrackingBehavior.NoTracking`, todėl
   `queueItem.Status = "PROCESSING"; SaveChangesAsync()` yra no-op. Statusas ir
   `Attempts` nesikeičia -> ta pati eilutė apdorojama kas 30 s be galo -> Azure
   kvota sudeginama -> 429. Tai BUGLOG klasė
   `FindAsync + SaveChangesAsync silently fails under global NoTracking`.
3. `IsAzureHealthyAsync` grąžina `true` visada — konstruktorius nedaro tinklo
   kvietimo. Workerio health gate neveikia.
4. `ExpenseUploadDialog.SaveAsync` sinchronizuoja UI laukus į `_ocrResult` tik
   naujos sąskaitos šakoje. Redaguojant esamą sąskaitą rankiniai pataisymai
   tyliai dingsta.

**Statusas:** laukia nepriklausomo patvirtinimo inventorizacijos ataskaitoje.

---

## D-002 — Azure kviečiamas vieną kartą, testai sukasi offline (2026-09-13)

**Kontekstas.** Svarstyta, ar agento ciklas kiekvienoje iteracijoje siunčia
sąskaitas į Azure per ERP.

**Sprendimas.** Ne. Žali Azure atsakymai fiksuojami vieną kartą į fixtures;
visas testų ir agento ciklas sukasi offline ant išsaugoto JSON.

**Kodėl.** Kvotos deginimas, latencija ir — svarbiausia — nedeterminizmas: Azure
modelio versija gali pasikeisti, ir agentas nežinotų, ar pagerėjimas atsirado dėl
jo pataisymo. Realus Azure kvietimas lieka tik T4 canary teste.

---

## D-003 — Etalonui reikalingas vienkartinis žmogaus darbas (2026-09-13)

**Kontekstas.** Pageidavimas — pilnai automatinis patikrinimas be Deivido laiko.

**Sprendimas.** Testai skaidomi į dvi rūšis. T0 invariantai (aritmetika,
determinizmas, duomenų nepraradimas) veikia be etalono ir automatizuojami 100%.
T1 teisingumo testai reikalauja etaloninių reikšmių, kurias sugeneruoja du
nepriklausomi keliai, o Deividas peržiūri tik nesutapimus plius visus pinigų
laukus (~30–40 min vienkartinio darbo 60 sąskaitų).

**Alternatyva ir kodėl atmesta.** Agentas „taiso, kol testai žali" be etalono —
duotų tvarkingą modulį su neteisingais skaičiais, nes niekas nematuoja
teisingumo.

---

## D-004 — Žali Azure atsakymai gzip diske, ne DB (2026-09-13)

**Sprendimas.** `uploads/invoices/YYYY/MM/<sha>.azure.json.gz`, DB laikomas tik
kelias ir hash.

**Kodėl.** Atsakymas 100–500 KB; LONGTEXT stulpelis išpūstų backup'us ir
sulėtintų kiekvieną užklausą, kuri netyčia paimtų stulpelį. Gzip suspaudžia ~10x.

---

## D-005 — Korpusas 60 sąskaitų, 40 dev + 20 hold-out (2026-09-13)

**Sprendimas.** 60 vietoj 20, parinktų pagal dangos kategorijas. Agentas mato tik
40; 20 hold-out paleidžiami tik ciklo pabaigoje.

**Kodėl.** Be hold-out agentas neišvengiamai užkoduos tiekėjų pavadinimus į `if`
sakinius ir gaus dirbtinai aukštą balą. Hold-out yra vienintelis dalykas,
skiriantis realų pagerėjimą nuo persimokymo.

---

## D-006 — Kontekstas gyvena repozitorijoje, ne pokalbyje (2026-09-13)

**Sprendimas.** Projekto būsena laikoma `Docs/ocr-rebuild/` failuose. Kiekviena
Claude sesija pradedama nuo `STATE.md` perskaitymo. Vienas pokalbis per fazę, ne
vienas visam projektui.

**Kodėl.** Ilgas pokalbis degraduoja, o „konteksto perkėlimas" į naują pokalbį
kas kartą praranda detales nepastebimai.

---

## D-007 — Eilės kelias atgaivinamas, ne šalinamas (2026-09-14)

**Kontekstas.** Abi inventorizacijos patvirtino: `Program.cs` neturi
`AddControllers()` / `MapControllers()`, todėl `ExpenseController` webhook'as
nemaršrutizuojamas; be to jis rašo `InvoiceId = 0`, o worker'is tik atnaujina
esamą sąskaitą. Kelias negyvas iš abiejų galų.

**Sprendimas.** Netrinti. Atgaivinti F7 fazėje kaip tikrą ingest'ą: `document_id`
vietoj base64, sąskaitos kūrimas vietoj atnaujinimo, bendras `OcrPipeline` su
dialogo keliu.

**Kodėl.** Nuolatinis architektūros principas sako, kad sąskaitų modulis
(IMAP -> OCR -> ERP) eina per n8n. Šis webhook'as yra numatytas įėjimo taškas,
tik niekada nebuvo prijungtas. Ištrynus jį, n8n integracijai reikėtų kurti iš naujo.

**Pasekmė.** A1, A2, A3, A18 keliauja iš F0 į F7. F0 lieka tik gyvo kelio klaidos.

**Sąlyga.** Webhook'as įjungiamas su autentikacija tame pačiame commit'e —
šiandien jis negyvas, o įjungtas be apsaugos būtų anoniminis įėjimo taškas į ERP.

---

## D-008 — Produkcijos prieiga: `AGENTS.md` viršesnė už užduotį (2026-09-14)

**Kontekstas.** Inventorizacijos užduotis davė read-only prod prieigos instrukciją,
prieštaraujančią `AGENTS.md` taisyklei, kad produkcija yra tik žmogui. Abu agentai
sustojo ir nurodė prieštarą.

**Sprendimas.** Agentų elgesys buvo teisingas — nuolatinė projekto taisyklė
viršesnė už vienkartinę užduotį. Užduotis buvo klaidinga, ne agentai.

**Laukia Deivido:** arba `AGENTS.md` papildomas išimtimi read-only `SELECT`
užklausoms, arba B8 penkios užklausos vykdomos ranka. Kol to nėra, Q-001 ir Q-003
lieka atviri.

---

## D-009 — Saugykla ir autorizacija aplenkia visa kita (2026-09-14)

**Kontekstas.** PDF rašomi į `wwwroot/uploads/` be volume (dingsta per kiekvieną
deploy) ir serveriuojami per `UseStaticFiles()` be autentikacijos. `Routes.razor`
naudoja `RouteView`, ne `AuthorizeRouteView`; nėra `UseAuthentication()` nei
`UseAuthorization()`.

**Sprendimas.** Nauja F1 fazė prieš dokumentų modelį: autorizacijos infrastruktūra,
failų perkėlimas į `/var/lib/nordicbees/invoices/` su volume, autentikuotas
dokumentų endpoint'as.

**Kodėl.** v1 F1 vartai buvo „10 realių sąskaitų atkuriamos iš disko" — neatkuriamos,
nes ištrinamos. Ir admino vaizdas su žaliu Azure JSON negali remtis `[Authorize]`,
kuris neveikia.

**Rizika.** F1 yra vienintelė fazė, galinti nulaužti veikiančią produkciją:
įjungus autorizaciją, puslapiai, kurie „veikė" tik dėl jos nebuvimo, gali užsidaryti.
Būtina pilna `[Authorize]` inventorizacija prieš įjungiant.

---

## D-010 — Persist yra viena transakcija (2026-09-14)

**Kontekstas.** `CreateFromOcrAsync` daro tris nuoseklius `SaveChangesAsync`
(sąskaita, eilutės, auditas) be transakcijos. Lūžis viduryje palieka sąskaitą be
eilučių. `UpdateFromOcrAsync` tokios pat formos.

**Sprendimas.** 6 pakopa (Persist) — viena transakcija. Įeina į F4 kaip privalomas
reikalavimas, ne kaip pageidavimas.

---

## D-011 — Užduočių paskirstymas tarp harnesų (2026-09-14)

**Kontekstas.** Dvi nepriklausomos inventorizacijos ta pačia užduotimi
(`analysis/COMPARISON.md` §5).

**Sprendimas.** Analitinės ir audito užduotys -> Claude Code. Build užduotys ->
OpenCode harnesas, kol jis bus atskirai išmatuotas toje klasėje.

**Kodėl.** Claude Code rado 14 naujų radinių prieš 8, iš jų trys pakeitė planą, ir
buvo teisus visuose trijuose nesutarimuose. OpenCode padarė vieną klaidingą išvadą
(B4 autorizacija), kuri būtų nuėjusi į F6 dizainą.

**Apribojimas.** Tai nematuoja kodo rašymo užduočių.

---

## D-012 — Read-only užduočių švarumo įrodymas (2026-09-14)

**Kontekstas.** OpenCode paleidimas parašė antrą failą
(`.opencode/planning/task_plan.md`), nors užduotis leido tik ataskaitą, ir
checklist'e teigė, kad ataskaita yra vienintelis sukurtas failas.
`git status --porcelain` to nepagavo, nes `.opencode/` yra git-ignore.

**Sprendimas.** Read-only užduotys nuo šiol reikalauja failų sąrašo su modifikavimo
laikais, įskaitant ignoruojamus katalogus, o ne `git status`.

---

## D-013 — Užduoties riba yra revert'o riba, ne pakeitimų skaičius (2026-09-14)

**Kontekstas.** Svarstyta, ar kiekvieną pataisymą duoti atskira sesija.

**Sprendimas.** Vienas koncernas = vienas **commit'as**, ne viena sesija. Viena
užduotis gali turėti kelis commit'us. Skaidymo kriterijai:

- ką revert'intum kartu, tas yra viena užduotis;
- saugus pakeitimas niekada nededamas į tą pačią užduotį su rizikingu;
- grupuoti galima tik ten, kur kiekvienas pakeitimas turi savą testą — be testų
  grupavimas paverčia diagnostiką spėliojimu;
- konteksto lubos: ~4 failai vienai užduočiai, nepriklausomai nuo logikos.

**Pasekmė planui.** F0 — viena užduotis, keturi commit'ai. F1 — viena ir tik ji
(plius atskira read-only `[Authorize]` inventorizacija prieš ją). F2 — viena.
F4 — dvi (modelis+ekstraktorius / runner+hook'ai). F7 webhook'as: `MapControllers()`
ir autentikacija privalo būti viename commit'e, `InvoiceId` logika ir worker'io
taisymai — atskirai. Sesijų skaičius krenta nuo ~23 iki ~14.
