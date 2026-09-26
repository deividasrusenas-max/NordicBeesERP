# OCR rebuild — būsena

Atnaujinta: 2026-09-26 | Fazė: Etapas 0 + 0c **prode (v0.17.91)**; kitas — Etapas 1

## Dabartinė fazė

Etapas 0 ir Etapas 0c — **BAIGTI, prode v0.17.91** (2026-09-26; `main` `a1ad12a` →
`production`). Tame pačiame deploy'uje yra ir Etapo 1 validatoriai (IBAN, PVM kodo formatas,
EN 16931 sumos) — **neprijungti**, niekur nekviečiami.

**Deploy politika — D-037:** kitas prodo deploy'us tik tada, kai Etapai 1–4 visiškai baigti ir
patikrinti staginge. `main` kaupia Etapus 1–4, staging — integracijos aplinka, prodas lieka
v0.17.91.

Darbų tvarka — D-022 (vartai prieš ekstrakciją), baigtumo kriterijai — D-031.

**Darbo režimas:** viena juosta, tiesiai `main`, jokių worktree. Užduočių specifikacijas rašo
tik planavimo pokalbis.

## Prodas (2026-09-26)

- Versija **v0.17.91**, `production` = `a1ad12a`.
- Backup'ai `lakstena-dev`: `~/backup/prod-before-deploy-0.17.91.sql.gz`,
  `~/backup/prod-before-files-ddl-2026-09-25.sql.gz` (D-030).
- Ghostscript Dockerfile pin'as pataisytas (`a1ad12a` — priimamos saugumo revizijos
  `ubuntu7*`); CI „Build and Deploy" vėl žalias.

## Commit'ų intervalai (visi prode v0.17.91)

| Dalis | Intervalas | Raportas |
|---|---|---|
| Etapas 0 | `5e44f7c..c93e9af` (8) | `.opencode/reports/ocr-etapas0-20260925-2354.md` |
| Etapas 0c | `4eb3f5d..8d5b95f` (intervale yra ir `50f6a3d` — Etapo 1 `IbanValidator`) | `.opencode/reports/ocr-etapas0c-20260926-0136.md` |
| Etapo 1 validatoriai | merge `4a04361` .. `8535767` | `overnight-20260926-0157.md`, `prepush-20260926-0301.md` |

## Staging patikrų rezultatai (savininkas, 2026-09-26)

Numeriai — `STAGING-CHECKS-ETAPAS0.md`.

| Patikra | Rezultatas |
|---|---|
| 1 — deploy'us | **PASSED** |
| 3 — numatyto termino žymė (įsk. mokėjimo dialogą) | **PASSED** |
| 4 — re-OCR: jokio re-OCR dublikatams; be failo — atsisakoma teisingu pranešimu; 376 be mygtuko | **PASSED** |
| 8 — tas pats PDF atmetamas prieš Azure | **PASSED** |
| 370 redagavimas (iš 15/16) — sumos, 7 eilutės, vėliavėlės išliko; patvirtinimas išliko po ne-vartų lauko redagavimo | **PASSED** (bet žr. OWN_COMPANY žemiau) |
| 9 — drag & drop | **FAILED** — neveikia; C9/D-034 tikslo nepasiekė |
| 2 — cash flow / tiekėjo istorija | **NEPATIKRINTA** — savininkas nerado, kur atidaromi (žr. žemiau: UI jų neatidaro) |
| 5 — biudžetas | **NEPATIKRINTA** (ta pati priežastis) |
| 6 — banko importas | **NEPATIKRINTA** |
| 7 — JPG / skenuoto PDF atmetimas | **NEPATIKRINTA** |
| 10–19 (išskyrus 370 redagavimą) | **NEPATIKRINTA** |

## Atviri klausimai ir tęsiniai (savininko sprendimai / follow-up)

- **Drag & drop neveikia** (patikra 9). Kodas — užšaldytas `FROZEN.md` §3; priežastis dar
  nediagnozuota naršyklėje. Hipotezė iš 0c raporto §5: `setupDropZone` kviečiamas tik pirmo
  render'io metu, o jo pakartojimo ciklas `OnAfterRenderAsync` niekada nesikartoja (funkcija
  tyliai grįžta, kai elemento nėra) — po „✕" (`RemoveFile`) naujai nupieštas drop-zone
  elementas lieka be klausytojų. Taisymui reikės savininko leidimo.
- **Biudžeto, cash flow ir tiekėjo istorijos dialogai UI nepasiekiami** (nustatyta iš kodo
  2026-09-26). `ExpenseBudgetDialog`, `ExpenseCashFlow`, `ExpenseSupplierHistory`
  (`Components/Dialogs/`) — nė vienas failas projekte jų neatidaro (nėra `ShowAsync<…>`,
  nėra `@page`). Meniu „Išlaidos" → „Išlaidų prognozė" (`NavMenu.razor:247`,
  `/expenses/forecast`, `ExpenseForecast.razor`) yra kitas puslapis ir šių dialogų neatidaro.
  Taip pat nesupainioti su „Įplaukų prognozė" (`NavMenu.razor:286`, `/payments/forecast`,
  `CashFlowForecast.razor`) — tai kitas, su `ExpenseCashFlow` nesusijęs puslapis.
  Todėl Etapo 0 karantino (E0-1) ir D-036 biudžeto pakeitimai šiuose dialoguose naudotojui
  nematomi, o patikros 2 ir 5 neįmanomos. Sprendimas: prijungti prie UI (kur?) ar pašalinti.
- **„Savos įmonės sąskaita" dingsta po redagavimo** (370). Nustatyta iš kodo: čipas rodomas iš
  `ocr_flags` (`InvoiceDetailDialog.razor:46-51` → `ExpenseStatusHelper.cs:63`), o redagavimo
  išsaugojimas vėliavėles perkuria iš naujo ir `OWN_COMPANY` į perkeliamų sąrašą neįtraukia
  (`ExpenseService.ComputeManualEditFlags`, `ExpenseService.cs:498-529`). T. y. redagavimas
  **numeta** `OWN_COMPANY` — tas pats nutinka `INVALID_VAT_RATE`. Nėra požymių, kad tai
  sąmoningas sprendimas (`LOW_CONFIDENCE` numetamas su komentaru, šie du — be jokio). Taisyti — Etape 1
  (`PLAN-ETAPAS1.md` §5).
- **Užšaldyto dialogo tekstas** „Patikrinkite ar visi serveriai veikia ir bandykite dar kartą."
  rodomas ir po ne-OCR atmetimų (`ExpenseUploadDialog.razor:83-85`, FROZEN §3) — reikia
  leidimo.
- **`OcrQueueWorker` `Attempts++` niekada neišsaugomas** (FROZEN §5; 0c raportas §2).
  Papildomai: vienintelis eilės įrašų kūrėjas — n8n webhook'as
  (`Controllers/ExpenseController.cs:24-58`, visada `InvoiceId = 0`) — **nemaršrutizuojamas**
  (`Program.cs` neturi `AddControllers`/`MapControllers`; tai jau užrašyta D-007). Kelias
  šiandien negyvas iš abiejų galų. Jei būtų atgaivintas toks, koks yra: eilės įrašo būsena
  neišsaugoma, todėl įrašas (iš kodo skaitymo, nevykdyta) būtų siunčiamas į Azure kas ~30 s be
  galo ir blokuotų vėlesnius; o rašydamas į sąskaitą darbuotojas apeitų visus vartus
  (`PLAN-ETAPAS1.md` §0, §5).
- **decimal-precision radiniai** (`nordicbees-ef-decimal-precision-annotation-missing`).
  Ankstesnėje būsenoje — 12; 0c raporto priede po C8 — 57 šios taisyklės radiniai, nė vieno
  `Models/Expenses/`. Skaičių reikia suderinti.
- **Re-OCR saugomiems failams nepasiekiamas** (STAGING-CHECKS 2 įspėjimas) — Etapas 1
  (`PLAN-ETAPAS1.md` §4).
- **Data Protection raktai neišsaugomi tarp konteinerių** — kiekvienas deploy'us atjungia
  naudotojus (už OCR ribų).
- **3,8 GB RAM `lakstena-dev`** — prodas, staging, DB ir CI kartu (už OCR ribų).
- **Prodo duomenų valymas su buhaltere** — `PROD-DATA-FINDINGS-2026-09-25.md` §6, Q-010.
- **„248 sąskaitos reikalauja dėmesio"** skaitiklis — beprasmis triukšmas, kol nevalyti
  duomenys.

## Už OCR ribų — tik užfiksuota

- ULAK sąskaita iš `DeliveryList.razor:277` išrašoma be išskaitų, o dialogo kelias
  (`InvoiceService.CreateInvoiceFromDeliveryAsync`) jas atima (overnight raportas Part C;
  FROZEN §4).
- CI `hardcode-check.yml`: grep žingsniai naudoja `--exclude-path` (ne GNU grep parinktis) —
  žingsnis gali praeiti nieko netikrinęs.

## Kitas žingsnis

**Etapas 1** — planas `PLAN-ETAPAS1.md` (savininko ir planavimo patarėjo peržiūrai prieš kodą).

## Padaryta

- Sesija 01: modulio analizė, dokumentacijos karkasas.
- Sesija 02: dvi nepriklausomos inventorizacijos + `analysis/COMPARISON.md`.
- Sesija 03: `PLAN.md` v2, F0 (157/157 žali), produkcijos auditas.
- Sesija 04: saugyklos 1 žingsnis — `files`, `IFileStore`, `StorageSentinel`,
  `deploy.yml` mount'ai, žalias JSON į `ocr_raw_json`. 172/172.
- Tyrimas 2026-09-25: `analysis/RESEARCH-2026-09-25-reliability.md`.
- Sesija 05: staging perklonuotas iš prodo (D-029); storage gate uždarytas; prodo
  duomenų analizė (`analysis/PROD-DATA-FINDINGS-2026-09-25.md`); D-021…D-029,
  Q-005…Q-010. Detalės — `sessions/2026-09-25-05.md`.
- 2026-09-25/26 (Claude Code): Etapas 0 (8 commit'ai), Etapas 0c (C1–C9 + fix-up'ai),
  D-031…D-036, Etapo 1 validatoriai; `STAGING-CHECKS-ETAPAS0.md`.
- 2026-09-26: staging patikros (iš dalies, žr. lentelę), prodo deploy'us v0.17.91, D-037.

## Storage gate — įrodymai (2026-09-25, staging)

| Patikra | Rezultatas |
|---|---|
| Įkėlimas → blob'as | `blobs/13/55/1355…9bed`, 174 202 B |
| `files` eilutė | id 1, sha256 sutampa, `entity_id` = 376 |
| `expense_invoices.file_id` | 376 → 1 |
| Žalias Azure JSON | `ocr_raw_json` užpildytas |
| Perkūrimas `stop`+`rm`+`run` | naujas konteineris `2026-09-25T19:17:15Z`, tas pats image |
| Failas po perkūrimo | sha256 sutampa prieš ir po |
| PDF peržiūra po perkūrimo | rodo (s. 376) |
| Senas `/uploads/invoices/...` URL | 404 |
| 247 senos sąskaitos su tuščiu `file_id` | sąrašas ir detalių langas neluža (prodo klonas) |

## Senos šakos (informacija, niekas netrinta; 2026-09-26)

- `git branch --merged main`: `acoustic-sociology`, `almondine-writing`, `boiled-cost`,
  `chocolate-tin`, `erratic-dirigible`, `helpful-temperature`, `horn-caravan`,
  `knowledgeable-blizzard`, `polyester-macrame`, `production`, `quickest-trust`,
  `radial-arch`, `unexpected-guppy` (`production` — deploy'aus šaka, ne sena).
- `git branch --no-merged main`: `ef-migrations-reconcile`, `feature/sverimo-modulis`.
- Ką daryti su senomis šakomis — sprendžia savininkas.

## Blokatoriai

- Q-010 (ar dublikatai pateko į apskaitą) blokuoja prodo duomenų valymą.
- Agento auto-resume: „continue" iš mechanizmo virsta leidimu — užduotyse yra
  Authority rule.
- `llm-overrides-red-gate` (BUGLOG 2026-09-26) — struktūrinis sprendimas neįdiegtas.

## Fazių lentelė

| Fazė | Būsena |
|---|---|
| F0 Gyvo kelio pataisymai | baigta (157/157) |
| Saugykla, 1 žingsnis | baigta, gate uždarytas 2026-09-25 |
| Etapas 0 (tylios klaidos, vartai) | **baigta, prode v0.17.91**; staging patikros dalinės (žr. lentelę) |
| Etapas 0c (redagavimas, įvestis, biudžetas) | **baigta, prode v0.17.91**; drag & drop FAILED, biudžetas nepatikrintas |
| Etapas 1 (EN 16931, lokalės, IBAN, PVM tarifai) | validatoriai prode, neprijungti; planas `PLAN-ETAPAS1.md` |
| Etapas 2 (tiekėjo kaskada) | laukia |
| Etapas 3 (ekstrakcija — D-023 kryptis, D-016 atviras) | laukia; Q-006, Q-007, Q-009 prieš pradedant |
| Etapas 4 (matavimas) | laukia |
| Saugykla, 2 žingsnis (PDF į IFileStore) | nepradėta |
