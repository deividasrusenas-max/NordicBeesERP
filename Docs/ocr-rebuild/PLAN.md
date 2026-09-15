# OCR modulio restruktūrizacija — planas v2

Versija: 2 (2026-09-14) | Pakeičia v1 (2026-09-13)
Pagrindas: dvi nepriklausomos inventorizacijos + `analysis/COMPARISON.md`

Šis failas keičiasi retai. Dabartinė būsena — `STATE.md`.
Sprendimai — `DECISIONS.md`. Atviri klausimai — `OPEN-QUESTIONS.md`.

---

## 1. Kas pasikeitė nuo v1

v1 buvo rašytas ant vienos analizės. Dvi nepriklausomos inventorizacijos
patvirtino 16 iš 18 teiginių, o trys naujai patvirtinti faktai pakeitė fazių
tvarką iš esmės:

- **Eilės kelias negyvas iš abiejų galų.** `Program.cs` neturi
  `AddControllers()` / `MapControllers()`, todėl `ExpenseController` webhook'as
  nemaršrutizuojamas; be to jis rašo `InvoiceId = 0`, o worker'is tik atnaujina
  esamą sąskaitą pagal tą id. Keturi v1 P0 defektai (A1, A2, A3, A18) yra tikri,
  bet negyvame kode.
- **Dokumentai prarandami ir yra vieši.** PDF rašomi į `wwwroot/uploads/`
  konteinerio rašomajame sluoksnyje be volume — dingsta per kiekvieną deploy.
  Ta pati direktorija serveriuojama per `UseStaticFiles()` be autentikacijos.
- **Puslapio lygio autorizacija neveikia.** `Routes.razor` naudoja `RouteView`,
  ne `AuthorizeRouteView`; `Program.cs` neturi `UseAuthentication()` nei
  `UseAuthorization()`. `[Authorize]` atributai yra dekoracija.

Pasekmė: saugykla ir prieiga kyla iš „patogumo" į būtinybę ir aplenkia visa kita,
o eilės kelio taisymas nusileidžia į vėlyvą fazę, kur jis atgaivinamas kaip
n8n ingest'as, o ne lopomas vietoje.

## 2. Nekintantys reikalavimai

Iš ankstesnių sesijų, nesikeičia:

1. Įkeltas PDF išsaugomas ir lieka atkuriamas.
2. Žalias Azure atsakymas išsaugomas ir lieka atkuriamas.
3. Vartotojas mato tik PDF ir formą.
4. Adminas papildomai mato trečią vaizdą su žaliu Azure atsakymu.
5. Testavimas automatinis: 60 realių sąskaitų, agentas sukasi cikle be Deivido.
6. Nuoseklus tikrinimas — kiekviena fazė turi mechaniškai tikrinamus vartus.
7. Azure kviečiamas vieną kartą; testų ciklas sukasi offline (D-002).

## 3. Tikslinė architektūra

```
1 Ingest      failas -> PDF normalizacija -> sha256 -> expense_documents
2 OCR         Azure kvietimas -> žalias atsakymas -> expense_ocr_runs
3 Extract     žalias JSON -> CanonicalInvoice + Trace     <- GRYNA FUNKCIJA
4 Enrich      VIES, tiekėjo paieška, kategorija           <- IO, mockinama
5 Validate    aritmetika, invariantai, vėliavėlės
6 Persist     ERP įrašai arba peržiūros eilė              <- VIENA TRANSAKCIJA
```

3 pakopa neliečia `DbContext`, `HttpClient`, `DateTime.Now`. Įtvirtinama semgrep
taisykle `.semgrep.yml`, ne susitarimu.

6 pakopa yra **viena transakcija**. Dabartinis `CreateFromOcrAsync` daro tris
nuoseklius `SaveChangesAsync` (sąskaita, eilutės, auditas) be transakcijos —
lūžis viduryje palieka sąskaitą be eilučių. Tai tapo privalomu reikalavimu.

### Lentelės

```
expense_documents        id, sha256 UNIQUE, original_filename, mime_type,
                         byte_size, page_count, stored_path, uploaded_by,
                         uploaded_at

expense_ocr_runs         id, document_id, provider, api_version, model_id,
                         request_params JSON, raw_response_path,
                         raw_response_sha256, http_status, duration_ms,
                         pages_billed, created_at

expense_ocr_extractions  id, ocr_run_id, extractor_version, canonical_json,
                         trace_json, created_at

supplier_ocr_rules       id, match_key, match_type, field_overrides JSON,
                         active, created_at
```

`expense_invoices` gauna `document_id`. `expense_ocr_queue.file_content` (base64)
pašalinamas — eilė nurodo `document_id`.

### Failų saugykla

Ne `wwwroot`. Sekam artwork precedentu: `/var/lib/nordicbees/invoices/`,
mount'intas volume `deploy.yml`, serveriuojamas per autentikuotą endpoint'ą, ne
`UseStaticFiles()`.

```
/var/lib/nordicbees/invoices/YYYY/MM/<sha256>.pdf
/var/lib/nordicbees/invoices/YYYY/MM/<sha256>.azure.json.gz
```

Žali JSON gzip (100–500 KB -> ~10x mažiau). DB laiko tik kelią ir hash.

### Provenance

```csharp
public sealed record FieldValue<T>(
    T? Value,
    string? SourceJsonPath,
    decimal? ProviderConfidence,
    string RuleId,
    string? RawContent);
```

Duoda admino vaizdą, testų diagnostiką ir agentui tikslų taikinį vienu kirčiu.

## 4. Fazės

Kiekviena fazė turi vartus. Kitos nepradedam, kol vartai neuždaryti.

---

### F0 — Gyvo kelio kraujavimo sustabdymas

Tik tai, kas veikia produkcijoje ir kenkia dabar. Maža, saugu, be architektūros.

1. **A4** — `ExpenseUploadDialog.SaveAsync` sinchronizuoja UI laukus į `_ocrResult`
   tik naujos sąskaitos šakoje; redaguojant esamą, pataisymai tyliai dingsta.
2. **A9** — PVM tarifo parsinimas: `"21,5"` -> 2,15 %. Normalizuoti kablelį prieš
   parsinimą, pašalinti `/100` guard'ą, kuris maskuoja klaidą.
3. **A11 dalis** — įdėti aritmetinę patikrą `excl + PVM = incl` ir pašalinti
   fallback'ą, kuris išgalvoja `AmountInclVat`.
4. **N2** — `invoice_number` NULL į NOT NULL stulpelį: validacijos vartai prieš
   įrašymą.

**Vartai:** xUnit testai šiems keturiems; rankinė patikra, kad esamos sąskaitos
redagavimas išsaugo pakeitimus; `dotnet build` švarus; `bump-version.sh patch`.

**Sąmoningai NEDAROMA F0 fazėje:** A1, A2, A3, A18 — jie negyvame kode (žr. F7).

---

### F1 — Prieiga ir saugykla

Rizikingiausia fazė visame plane, nes liečia visos aplikacijos autentikaciją.
Daroma atskirai, su rollback planu.

1. **Autorizacijos infrastruktūra**: `UseAuthentication()`, `UseAuthorization()`,
   `AddCascadingAuthenticationState()`, `Routes.razor` -> `AuthorizeRouteView`.
2. **Prieš įjungiant** — inventorizuoti kiekvieną `[Authorize]` atributą repo ir
   nustatyti, kas nutiks kiekvienam puslapiui, kai jie pradės veikti. Šiuo metu
   dalis puslapių „veikia" tik todėl, kad apsauga neveikia.
3. **Failų saugykla** iš `wwwroot/uploads/` į `/var/lib/nordicbees/invoices/`,
   volume `deploy.yml`, migracijos skriptas esamiems failams (jei konteineryje
   dar kas nors yra).
4. **Autentikuotas dokumentų endpoint'as**, `UseStaticFiles()` nebeserveriuoja
   sąskaitų.

**Vartai:** kiekvienas puslapis patikrintas su kiekviena role rankiniu būdu;
anoniminis `GET` į seną PDF URL grąžina 401/404; įkeltas PDF išlieka po
`docker compose down && up`.

**Rollback:** vienas commit'as, revert'inamas per minutę; deploy'inti ne piko metu.

---

### F2 — Dokumentų modelis ir žaliavos fiksavimas

Elgsena nesikeičia, tik pradeda kauptis medžiaga.

1. Trys lentelės + `document_id` į `expense_invoices`.
2. `IDocumentStore`: sha256, PDF įrašymas, dublikatų gaudymas pagal hash.
3. `IOcrProvider`: Azure kvietimas -> `expense_ocr_runs` su žaliu JSON gzip,
   `api_version`, `model_id`, trukmė, apmokestinti puslapiai.
4. Prijungimas prie esamo dialogo kelio. Senas ekstraktorius veikia toliau.
5. `pages: "1-2"` -> visas dokumentas; `locale` iš nustatymų, ne užkoduotas.

**Vartai:** 10 realių sąskaitų turi `ocr_run` su atkuriamu žaliu JSON; tas pats
failas antrą kartą atpažįstamas kaip dublikatas; deploy neprarado nė vieno failo.

---

### F3 — Korpusas ir etalonas

**Kietas vartas. Be jo F5 nepradedamas.**

60 sąskaitų pagal dangos kategorijas (po >=3):
LT skaitmeninis · LT skenuotas · ES užsienio · ne-ES · 3+ puslapių · viena eilutė ·
15+ eilučių · mišrūs PVM tarifai · 0% atvirkštinis · kreditinė · užsienio valiuta ·
ne mūsų įmonė pirkėjas · dublikatas · prastas skenas · eilučių suma teisėtai !=
antraštės.

**Padalinimas 40 dev + 20 hold-out.** Agentas mato tik dev.

Etalono surinkimas:
1. Vienkartinis Azure fiksavimas (60 puslapių).
2. Ekstraktorius v1 duoda kandidatus.
3. Nepriklausomas antras kelias (lokalus modelis ant Azure `content` markdown)
   duoda kandidatus, nematydamas v1.
4. Sutapimas + aritmetika sueina -> `auto-agreed`; kitur -> `needs-review`.
5. XLSX peržiūra: Deividas tikrina tik geltonus langelius plius **visus** pinigų
   laukus. ~30–40 min, trimis prisėdimais.
6. Importas į `testdata/expected/<sha>.json`.

Korpusas `testdata/ocr-corpus/` (gitignore). Į git tik `corpus.lock.json`.

**Vartai:** 60 `expected/*.json` egzistuoja ir praeina schemos validaciją.

---

### F4 — Grynas ekstraktorius v2 ir testų runner

1. `CanonicalInvoice`, `FieldValue<T>`, `IInvoiceExtractor`.
2. `AzureInvoiceExtractorV2` — perkėlimas į gryną funkciją, už feature flag'o,
   v1 lieka default.
3. Semgrep taisyklė ekstraktoriaus grynumui.
4. **Transakcinis persist** (N3): sąskaita + eilutės + auditas vienoje transakcijoje.
5. Testų runner -> `scorecard.json`.
6. T0 invariantai, T2 praturtinimas, T3 persistencija, anti-hardcode grep,
   git pre-commit hook'ai.

**Vartai:** v2 praeina visą korpusą be exception; bazinis scorecard sugeneruotas
ir commit'intas; hook'ai blokuoja `testdata/expected/` redagavimą.

---

### F5 — Agento ciklas

```
run-ocr-tests.sh -> scorecard.json -> agentas -> prasčiausia laukų klasė
-> vienas mikro-pataisymas -> perleidžia -> delta -> commit -> kartoja
```

Apsaugos:
- Uždrausta liesti `testdata/expected/`, `corpus.lock.json`, runner'į —
  git pre-commit hook'u, ne prompt'e.
- Anti-hardcode grep: ekstraktoriuje jokių korpuso tiekėjų pavadinimų ar PVM kodų.
  Išimtys eina į `supplier_ocr_rules`.
- Kiekviena iteracija commit'ina scorecard'ą. Pagerėjimas be commit'into delta
  neegzistuoja.
- Limitas 25 iteracijos; stop po 3 be delta.
- Hold-out tik pabaigoje.

#### Sėkmės slenksčiai

| Laukų klasė | Reikalavimas |
|---|---|
| Sumos (excl, PVM, incl), valiuta | 100%, nulis klaidų |
| Sąskaitos nr., data | >= 98% |
| Tiekėjo identifikavimas | >= 95% |
| Mokėjimo terminas | >= 90% |
| Eilučių rinkinys tiksliai | >= 85% |
| T0 invariantai | 100% |
| Hold-out nuokrypis nuo dev | <= 5 p. p. |

**Vartai:** slenksčių lentelė + hold-out.

---

### F6 — Admino trečias vaizdas

Trys sekcijos: laukų lentelė (`Laukas | Reikšmė | JSON kelias | confidence |
RuleId`), žalias JSON su paieška, paleidimo metaduomenys.

Du mygtukai: **Perskaičiuoti** (3–5 pakopos ant žaliavos, be Azure) ir **Kviesti
Azure iš naujo** (naujas `ocr_run`).

Pakartotinio apdorojimo taisyklės (NF-6): `original_filename` neperrašomas;
`WRONG_RECIPIENT` atmetimas neatstatomas automatiškai.

**Vartai:** ne-admino rolė negauna prieigos (patikrinta po F1); perskaičiavimas
nekviečia Azure (patvirtinta `ocr_runs` skaičiumi).

---

### F7 — Ingest kelio atgaivinimas (n8n)

Dabar, kai yra dokumentų saugykla ir veikianti autorizacija, eilės kelias
atgaivinamas kaip tikras ingest'as, o ne lopomas.

1. `AddControllers()` + `MapControllers()`.
2. **Webhook'as ateina su autentikacija tame pačiame commit'e.** Šiandien jis
   negyvas; įjungtas be apsaugos jis būtų anoniminis įėjimo taškas į ERP.
3. `InvoiceId = 0` problema: eilė nurodo `document_id`, worker'is **kuria**
   sąskaitą, ne tik atnaujina.
4. A1 (`PaidAmount`), A2 (`NoTracking` statusas per `ExecuteSqlRawAsync`),
   A3 (`IsAzureHealthyAsync`), A18 (reset kelias) — visi taisomi čia.
   **A2 pataisymas privalo turėti reset kelią**: kitaip begalinis ciklas virsta
   amžinai užstrigusia `PROCESSING` eilute.
5. Eilė ir dialogas eina per tą patį `OcrPipeline` — jokios elgsenos divergencijos
   (A13, A14).

**Vartai:** T3 testai žali; n8n testinis kvietimas sukuria sąskaitą; neautentikuotas
kvietimas atmetamas; eilutė po klaidos grįžta į `WAITING` su `Attempts++`.

---

### F8 — Shadow mode ir perjungimas

Dvi savaites v1 ir v2 sukasi lygiagrečiai ant kiekvienos realios sąskaitos,
skirtumai logginami, UI rodo v1. Nulis skirtumų pinigų laukuose -> v2 default.
Senas kodas trinamas kaip atskira užduotis su data.

---

## 5. Premortem

Praėjo trys mėnesiai, modulis vis dar netvarkingas. Kodėl?

- **Etalonas nebaigtas.** Labiausiai tikėtina. -> F3 kietas vartas, XLSX kelias,
  3 prisėdimai.
- **F1 įjungė autorizaciją ir kažką sulaužė produkcijoje.** Realiausia techninė
  rizika visame plane. -> pilna `[Authorize]` inventorizacija prieš įjungiant,
  rankinė patikra su kiekviena role, vienas revert'inamas commit'as, deploy ne
  piko metu.
- **F7 įjungė webhook'ą be apsaugos.** -> autentikacija tame pačiame commit'e,
  vartai reikalauja neautentikuoto kvietimo atmetimo.
- **Agentas persimokė ant 40 sąskaitų.** -> hold-out, anti-hardcode grep,
  `supplier_ocr_rules`.
- **Azure pakeitė modelio versiją.** -> `api_version` konfige, `model_id` rašomas,
  T4 canary, etalonai pririšti prie žalio JSON sha.
- **Refaktoringas įstrigo per pusę, produkcijoje du keliai.** -> feature flag su
  trynimo data F8 fazėje.
- **Paaiškėjo, kad sąskaitų kiekis mažas ir visa tai neatsiperka.** -> B8.3
  užklausa turi būti atsakyta prieš F3, ne po.

## 6. Redteam

- **F1 yra vienintelė fazė, kuri gali nulaužti veikiančią produkciją.** Visa kita
  arba prideda naują kodą už flag'o, arba taiso negyvą. Jei plane kas nors turi
  gauti dvigubą dėmesį, tai F1.
- **„Gryna funkcija" nėra visiškai gryna** — valiuta ir lokalė priklauso nuo
  įmonės nustatymų. -> perduodami kaip aiškus parametras.
- **Testai žali, produkcija kitokia**, nes produkcija eina per dialogą ir eilę.
  -> T3 ir F8 shadow nėra pasirinktini.
- **60 sąskaitų per mažai eilutėms.** Antraštei pakanka. Eilutės — atskira vaga
  su savo slenksčiu, neblokuoja perjungimo.
- **Dokumentų saugykla auga be ribų.** -> retencijos politika ir dydžio
  monitoringas F2 fazėje, ne „vėliau".
- **Proporcingumas.** F0–F2 verti bet kuriuo atveju: taiso aktyvų duomenų
  praradimą, viešą prieigą prie sąskaitų ir veikiančio kelio klaidas. F3–F5
  priklauso nuo B8.3 rezultato.

## 7. Vaidmenys ir ritualas

| Kas | Ką daro |
|---|---|
| Claude | Planas, specifikacijos, task prompt'ai, mechaninė verifikacija, `STATE.md` |
| Claude Code | Analitinės ir audito užduotys (pagal `COMPARISON.md` §5) |
| OpenCode | Build užduotys |
| Deividas | Verslo sprendimai, F3 etalono peržiūra, F1 rankinė patikra |

Sesijos pradžia: perskaityti `STATE.md`, paskutinį `sessions/` failą,
`git log --oneline -15`.

Sesijos pabaiga: perrašyti `STATE.md`, prirašyti sesijos žurnalą, commit'inti.

Sesija baigiasi žalia būsena arba grąžinamu commit'u.
