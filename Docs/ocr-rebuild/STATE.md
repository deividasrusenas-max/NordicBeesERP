# OCR rebuild — būsena

Atnaujinta: 2026-09-28 (naktis) | Fazė: Etapas 0 + 0c **prode (v0.17.91)**; Etapas 1 — **UŽDARYTAS STAGINGE 2026-09-26/27** (prodas lieka v0.17.91, D-037); Etapas 2 — **kodas baigtas `main` (S1–S6), laukia savininko staging darbo** (`STAGING-CHECKS-ETAPAS2.md`); Etapas 3 — **S1–S4 baigtos `main`**, laukia S5 (Etapo 2 švarios pradžios) ir S6; Etapas 4 — **planas parašytas ir dalis kodo baigta `main`** (`PLAN-ETAPAS4.md`, C1–C3); D-047 — **PATAISYTAS** (švari pradžia dabar KITAS žingsnis, ne atidėta) ir A1/A2 dabar turi tikrus bUnit testus; test-DB deadlock flake (Part D) sutvarkytas; D-048 — **masinis įkėlimas + švarios pradžios skriptas + galutinio deploy'o instrukcija baigti `main`, visi keturi nepriklausomai patvirtinti** (žr. „D-048 pre-clean-start tooling" žemiau)

## Dabartinė fazė

Etapas 0 ir Etapas 0c — **BAIGTI, prode v0.17.91** (2026-09-26; `main` `a1ad12a` →
`production`). Tame pačiame deploy'uje yra ir Etapo 1 validatoriai (IBAN, PVM kodo formatas,
EN 16931 sumos) — **prode (v0.17.91) neprijungti**, niekur nekviečiami; `main` jau jungia (žemiau).

**Etapas 1 — UŽDARYTAS STAGINGE 2026-09-26/27** (D-043). Kodas: S1–S7, D-038…D-041 (`e5cfb95`,
0.17.92 `b3e19ca`) ir šalies kodo taisymas D-042 (`073c299`); `main` dabar v0.17.94 (`cd37a80`);
paskutinis pilnas `dotnet test --filter "Category!=E2E"` (šalies kodo taisymo raportas): **972 testai
žali**. Staging patikrų rezultatai — žemiau, „Etapas 1 — staging patikrų rezultatai". Prodo
deploy'o nėra, kol Etapai 1–4 nebaigti (D-037, žr. žemiau). Staging DDL (`unit_price
decimal(18,6)`) savininko pritaikytas.

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

## Etapas 1 — commit'ų intervalai (`main`, ne prode)

| Sesija | Intervalas | Ką daro | Raportas |
|---|---|---|---|
| S1 | `6a4a33d`, `c94d38f`, `21b0234`, `82cb1e9` (+ `41d69a6`) | grynieji validatoriai: eilutės taisyklė nemeta, PVM kodo formatas, `VatRateTable`, `LocaleNumberCandidates` | `etapas1-s1-s2-20260926-1427.md` |
| S2 | `c7bd26b`, `3e83933`, `c89ff74` | redagavimas perkelia nežinomas vėliavėles (OWN_COMPANY), naujos vėliavėlės/etiketės, redagavimas rašo `unit_price` | tas pats |
| S3 | `bfd34da` (D-039), `8c67a62`, `fd28df4` | re-OCR pagal `file_id` (mygtukas, failo vardas, transakcija, paskirstymų patvirtinimas), `unit_price` → `decimal(18,6)` migracija | `etapas1-s3-20260926-1624.md` |
| S4 | `0b1beb2` (D-040) .. `a16ca5e` | EN 16931 vartai: BR-CO-15 tiksliai, BR-CO-10 juostos (`LINE_SUM_ROUNDING`), `MISSING_MONEY_FIELD`, `TOTALS_OUT_OF_RANGE`, eilutės taisyklė (informacija), taisyklių žinutės detalėje | `etapas1-s4-20260926-1714.md` |
| S5 | `f401f6e` .. `ccbc58f` | IBAN ir PVM kodo formato vartai, `VAT_COUNTRY_MISMATCH`, tiekėjo sukūrimo dialogas neperkelia neteisingo IBAN/PVM kodo | `etapas1-s5-s6-20260926-1859.md` |
| S6 | `6c817c6` .. `51b20b6`, `7857ad0` | PVM tarifų vartai (visos eilutės NEPATVIRTINTOS → tik `VAT_RATE_UNCHECKED`), UI nuoseklumas | tas pats |
| Korpusas + S7 | `1a81092`, `28dc8e1` (D-041), `6cb4c9f`, `279a3a6`, `6bb9158`, `e5cfb95` | Azure korpusas (už git ribų), lokalės skaičių aptikimas (`NUMBER_MISREAD` / `NUMBER_AMBIGUOUS`, peržiūra), suderinimas nebetrina eilučių (`LINE_LARGE_QUANTITY`, `LINE_DUPLICATE_DESCRIPTION`) | `etapas1-corpus-s7-20260926-2025.md` |
| S8 | `216dd5d` | `STAGING-CHECKS-ETAPAS1.md` | `etapas1-s8-*.md` |
| Šalies kodas (D-042) | `dc403c0` (docs), `073c299` (fix) | tiekėjo šalis — tik ISO alpha-2 per `CountryCodeResolver`, niekada neapkarpytas pavadinimas | `etapas1-country-fix-20260926-2306.md` |

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

## Etapas 1 — staging patikrų rezultatai (savininkas, 2026-09-26/27)

Numeriai ir tikėtinos vėliavėlės — `STAGING-CHECKS-ETAPAS1.md`. **Etapas 1 uždarytas staginge (D-043).**

**PASSED:**

| Patikra | Rezultatas |
|---|---|
| Deploy'us, schema, bazinė būsena | INV0 376, AUD0 456 |
| U1 DOLABELS 004321 | NUMBER_MISREAD, NUMBER_AMBIGUOUS, LINE_AMOUNT_IMPLAUSIBLE, VAT_RATE_UNCHECKED; vieneto kainos 115 / 118 / 6 / 11990 **nepakeistos**; 4 eilutės |
| U2 Rabenas | AMOUNT_MISMATCH, LINE_DUPLICATE_DESCRIPTION; 8 eilutės išsaugotos; 2385,61 |
| U3 EGO transport ir UTA PL | AMOUNT_MISMATCH, LINE_AMOUNT_IMPLAUSIBLE, VAT_RATE_UNCHECKED |
| U4 LD | MISSING_MONEY_FIELD, ZERO_VAT; aritmetinės vėliavėlės nėra |
| U5 Invoice (1231) | STALE_DATE, VAT_FORMAT_UNCHECKED, ZERO_VAT, WRONG_RECIPIENT → REJECTED; šalis NL |
| U6 Taurobilis („turi praeiti") | švari sąskaita — tik DUPLICATE ir VAT_RATE_UNCHECKED |
| Re-OCR 376 | mygtukas, failo vardas išliko, OCR_RETRIED, nauja sąskaita nesukurta; FUTURE_DATE pagauta (2026-09-30) |
| Redagavimo perkėlimas, 175 | OWN_COMPANY išliko |
| BR-CO-10 juostos, 381 | 0 → nieko; +0,03 → tik LINE_SUM_ROUNDING; +0,06 → AMOUNT_MISMATCH; grąžinus → nieko |
| IBAN / PVM formatas, 374 | INVALID_IBAN + INVALID_VAT_FORMAT su sugadintu kodu; abi išvalytos grąžinus |

**Pastebėta vykdant:**

- Visi korpuso PDF jau buvo prode, todėl **kiekvienas įkėlimas tapo DUPLICATE_PENDING** —
  patikros lygino vėliavėles, ne statusą.
- U3 UTA PL grįžo su **rastu tiekėju**, todėl VAT_FORMAT_UNCHECKED nebuvo.

**SKIPPED:**

- Paskirstymai — automatiniai testai.
- TOTALS_OUT_OF_RANGE — automatiniai testai.
- VAT_RATE_NOT_ALLOWED — nėra CONFIRMED eilučių.
- Tiekėjo sukūrimo užpildymas (14b) — automatiniai testai.
- Etapo 0 likučiai: dialogai nepasiekiami, drag & drop neveikia.

**Rasta vykdant ir SUTAISYTA:** tiekėjo šalis buvo išvedama apkarpant pavadinimą (Lietuva → LI,
Ireland → IR) — D-042, `073c299`, deploy'inta į staging.

**Prieš Etapų 1–4 deploy'ą į prodą liko (atviri):**

- Buhalterė patvirtina PVM tarifus (EK TEDB); EE keitimo data.
- Tiekėjų šalies valymo sąrašas (duomenys, D-042): RABEN LIETUVA LI→LT, Xirgo Global LI→LT,
  OÜ Nordic Hotels ES→EE, partneris 386 (PL PVM saugomas kaip LT, be pavadinimo), 8 partneriai be
  šalies. **KONICK RETAIL HUB — teisingai CZ** (čekų įmonė su LT PVM registracija) — **NEKEISTI**.
- Prodo duomenų valymas su buhaltere (`PROD-DATA-FINDINGS-2026-09-25.md` §6, Q-010).
- Drag & drop; našlaičiai dialogai (Q11); flaky `CreditNoteServiceTests` testas.
- Klientų / įmonės nustatymų šalies keliai (`CustomerCreateDialog.razor`, `CompanySettingsPage.razor`
  dar priskiria žalią šalies kodą — šalies kodo taisymo raporto peržiūrėtojo pastaba).
- Data Protection raktai; `lakstena-dev` RAM.

## Etapas 2 — būsena (2026-09-27; kodas `main`, ne prode, ne staginge)

Sprendimai — D-044 (Q1–Q16), D-045 (duomenys, Q8/Q9, švari pradžia). Kiekvienas commit'as praėjo nepriklausomą
peržiūrą (peržiūrėtojas vykdo testus, laužo taisykles scratch kopijoje); pirmą kartą atmesti commit'ai gavo
„tik testai" taisymo commit'ą ir pakartotinę peržiūrą (visi galutiniai verdiktai APPROVED). Pilnas
`dotnet test --filter "Category!=E2E"`: **1349 žali** (prieš S1 — 972, po S1 — 999).

| Sesija | Commit'ai | Ką daro | Raportas |
|---|---|---|---|
| S1 (vartai 3) | `fe7e72d` (+ D-044 `45494e2`) | `ApproveAsync` be tiekėjo atsisako, PATVIRTINTI paslėptas, atstatymas pagal bendras taisykles, `AssignSupplierAsync` tik iš PENDING_SUPPLIER, re-OCR palieka žmogaus tiekėją, `AssignSupplierDialog` pašalintas | `etapas2-s1-20260927-0230.md` |
| S2 (grynas atitikmuo) | D-045 `51c98e5`; `61bd1ee`, `4d307b6`, `097ef43`, `3d0980e` | `SupplierIdentityNormalizer`, `SupplierMatcher`, įmonės kodo ištraukimas | `etapas2-s2-20260927-0400.md` |
| S3 (prijungimas) | `25cad87`, `57548d8`, `55bb777`, `1bc0259` | vienas atitikmuo per įkėlimą, `VENDOR_AMBIGUOUS` / `VENDOR_SUGGESTED`, `SUPPLIER_MATCHED`, kandidatai detalėje, sweep su normalizatoriais, „Pakeisti tiekėją" | `etapas2-s3-20260927-0246.md` |
| S4 (žinomi IBAN) | `55f27bc`, `e924c52`, `7329de6` | `supplier_bank_accounts`, `SUPPLIER_NEW_IBAN` (peržiūra), „Pridėti IBAN prie tiekėjo", partnerio išsaugojimas įrašo IBAN, backfill skriptas | `etapas2-s4-20260927-0416.md` |
| S5 (aliasai) | `1313c42`, `1148b28`, `0000f1c`, `3c8d87e` | `supplier_aliases` + `supplier_alias_events`, patvirtinimai tik iš žmogaus veiksmų, N = 2, konfliktas → abu FROZEN, taikymas matcher'yje, sąrašas / atšaukimas / atblokavimas | `etapas2-s5-20260927-0530.md` |
| S6 (reitingavimas) | `d93d11d`, `b8e87a2`, `9feddc2` | `SupplierNameRanker` (token-sort Jaro-Winkler), išrikiuoti „Priskirti esamam" / „Pakeisti tiekėją" | `etapas2-s6-20260927-0545.md` |
| S7 | dokumentas + šis įrašas | `STAGING-CHECKS-ETAPAS2.md` | `etapas2-s7-20260927-0545.md` |

**Dev DDL (savininko vienkartinė išimtis, tik `nordic_bees_erp` ir `nordic_bees_erp_test`):** S4 `supplier_bank_accounts`
+ istorijos įrašas, DEV backfill 57 eilutės `MIGRATED`; S5 `supplier_aliases`, `supplier_alias_events` + istorijos įrašas.
Tikslūs sakiniai — S4 / S5 raportuose. Staging ir prodo DDL — **savininko** (`STAGING-CHECKS-ETAPAS2.md` §1).

**Kas laukia savininko:**

1. **Staging DDL** (`STAGING-CHECKS-ETAPAS2.md` §1) ir IBAN backfill — prieš deploy'ą; be lentelių nauji keliai meta klaidą.
   Versijos pakėlimas + push (autonominis paleidimas `bump-version.sh` nevykdė).
2. **Pagrindinių duomenų valymas** su buhaltere (§3): dublikatai (Rotoma 369/381, Rokiškio vandenys 370/377, HONEYMARK PL 36/386,
   Deltamark 396/399, 6 bitininkų poros), D-042 šalių sąrašas (RABEN LIETUVA, Xirgo Global, OÜ Nordic Hotels, 386, 8 be šalies;
   KONICK RETAIL HUB lieka CZ), vienas PVM be prefikso.
3. **Švari pradžia staginge** (§4): atsarginė kopija, ištrynimas, pakartotinis įkėlimas, matavimai.
4. **PVM tarifų patvirtinimas** (buhalterė, EK TEDB; visos eilutės vis dar NEPATVIRTINTOS; EE 22 → 24 % data).
5. Taisyklių patikros (§5) ir go / no-go (§6).

**Kitas:** S7 paleidimas (savininkas), tada Etapo 3 planavimas (ekstrakcija; D-016 žalių JSON korpusas gaunamas iš švarios pradžios).

## Etapas 3 — būsena (2026-09-27; kodas `main`, ne prode, ne staginge)

D-046 priimtas (PLAN-ETAPAS3 OQ-1…OQ-6, D-016 formaliai uždarytas). Sesijos S1–S4 baigtos šioje sesijoje (Claude
Code, autonominis paleidimas su savininko išankstiniu leidimu). Kiekvienas kodo commit'as praėjo nepriklausomą
peržiūrą (peržiūrėtojas vykdo testus, tikrina, kad kodas realiai yra sukompiliuotoje versijoje, bando sulaužyti
kiekvieną naują taisyklę scratch kopijoje); vienas commit'as (S2, `97e28be`) pirmą kartą atmestas (nepilnas laukų
padengimas golden-file momentinėje nuotraukoje), gavo taisymo commit'ą, visi galutiniai verdiktai APPROVED. Pilnas
`dotnet test --filter "Category!=E2E"`: **1399 žali** (prieš šią sesiją — 1357; S1 nekeitė kodo).

| Sesija | Commit'ai | Ką daro | Raportas |
|---|---|---|---|
| Commit 0 (D-046) | `c6b20bf` | Etapo 3 sprendimai (OQ-1…OQ-6), D-016 uždarymas | — |
| S1 (korpuso `tables[]` patikra) | `2723dd2` | 10/11 korpuso dok. turi lentelę atitinkančią žodyną (LT/DE/RO/LV/EE/PL + EN); 5/11 lentelė susiveda su antrašte griežta taisykle, 6-as (EGO) rankiniu būdu patvirtintas per antraštinę eilutę | `etapas3-s1-20260927-1220.md` |
| S2 (golden-file karkasas) | `97e28be` (ATMESTA) → `8880013` (taisymas, PATVIRTINTA) | `Verify.Xunit`; momentinė nuotrauka (antraštės sumos, PVM tarifas praleistas — žinoma riba, eilutės aprašymas/kiekis/kaina/neto/netoIšvestas, tiekėjo identifikatoriai); sintetinis pagrindas repo viduje, korpuso 11 dokumentų — už git ribų | `etapas3-s2-20260927-1316.md` |
| S3 (parinktis (c): deterministinis lentelės taisymas) | `a583bde` + `f44a537` (semgrep taisymas) | `TableLineRepair`; nauja vėliavėlė `LINES_REPAIRED_FROM_TABLE`; UTA PL realiai pataisytas (AMOUNT_MISMATCH dingsta), Rabenas ir EGO teisingai nepaliesti | `etapas3-s3-20260927-1316.md` |
| S4 (ZERO_VAT teisinio pagrindo patikra) | `2950b84` | `ZeroVatFormulationExtractor` (tas pats NEPATVIRTINTA/PATVIRTINTA modelis kaip `VatRateTable`); nauja vėliavėlė `ZERO_VAT_NO_BASIS` (vienintelis šios sesijos Etapo 1/2 statuso logikos pakeitimas, aiškiai leistas įgaliojime); šiandien (viskas NEPATVIRTINTA) elgsena identiška ankstesnei | `etapas3-s4-20260927-1316.md` |

**Kas laukia buhalterės / savininko prieš S5/S6:**

1. Realios LT formuluotės (be Q-009 vienintelio pavyzdžio) — ar „PVM įstatymo N straipsnis" / bare `PVMx` kodas yra
   bendras raštas, ar specifinis vienam tiekėjui.
2. PL/RO/CZ/ES atvirkštinio apmokestinimo frazės — bendros žinios, niekada nepatvirtintos prieš realų dokumentą.
3. S5 žymėjimo grafikas (40 dev + 20 hold-out) — laukia Etapo 2 švarios pradžios (D-045).
4. Q-006 (LT e-sąskaitų datos), Q-007 (Veryfi) — S6 darbas, dar nepradėtas.

**Kitas:** S5 (matavimas ant realaus korpuso) laukia Etapo 2 švarios pradžios rezultatų; S6 (Q-006/Q-007, parinkties
(a)/(b) persvarstymas) — po S5.

## D-047 (open fixes) ir Etapas 4 (matavimas) — būsena (2026-09-27, Claude Code, autonominis paleidimas)

D-047 priimtas (autorizuotas ilgas autonominis paleidimas): Part A (D-047 atviri taisymai),
Part B (Etapo 4 planas), Part C (trys Etapo 4 komponentai). Kiekvienas kodo commit'as praėjo
nepriklausomą peržiūrą (peržiūrėtojas vykdo testus, tikrina kodą, bando sulaužyti kiekvieną naują
elgseną); abu dokumentų commit'ai (D-047 ir `PLAN-ETAPAS4.md`) buvo bent kartą atmesti dėl netikslių
citatų ir taisyti. Pilnas `dotnet test --filter "Category!=E2E"`: **1448 žali** (prieš šią sesiją —
1399).

| Dalis | Commit'ai | Ką daro | Verdiktas |
|---|---|---|---|
| Commit 0 (D-047 docs) | `def21df` → `22b5a99` | Švarios pradžios laikas, išlaidų tiekėjai, užšaldyto dialogo leidimai | **ATMESTA DU KARTUS** — pagal taisyklę, trečias taisymas nedarytas; žr. žemiau |
| A1 | `d419386` | Drag & drop veikia ir po failo pašalinimo (`_dropZoneNeedsSetup`) | PATVIRTINTA |
| A2 | `916688e` | Klaidos fazės paantraštė nerodoma ne-OCR atsisakymams | PATVIRTINTA |
| A3 | `49a878a` | `CreditNoteServiceTests` nestabilaus testo taisymas (fiksuotas `int.MinValue`, ne gyvas `MAX(id)+1`) | PATVIRTINTA |
| A4 | `41a5e8f` | Įėjimo taškai biudžeto/pinigų srautų/tiekėjo istorijos dialogams | PATVIRTINTA (rastas nesusijęs pre-existing radinys: `ExpenseBudgetDialog.Year` neveikia) |
| Part B | `332ac84` → `bf4f481` | `PLAN-ETAPAS4.md` (D-031 kriterijai 3–6) | ATMESTA (2 klaidingos citatos) → PATVIRTINTA po taisymo |
| C1 | `1c7a140` | Žymėjimo CSV eksportas/importas (`Services/Labeling/*`, `/admin/ocr-labels`) | PATVIRTINTA |
| C2 | `36a8781` | Peržiūros eilės senėjimas (`LithuanianWorkingDayCalculator`, `ReviewQueueAgingService`) | PATVIRTINTA |
| C3 | `13e5ad4` | Savaitinė suvestinė (penki skaičiai) | PATVIRTINTA (nebloki radinys: „arithmetic" vartų kategorija naudoja AMOUNT_MISMATCH/TOTALS_OUT_OF_RANGE, o realų antraštės kietą vartą varo AMOUNT_ARITHMETIC_MISMATCH/MISSING_MONEY_FIELD — plano lygio klausimas, ne šio commit'o defektas) |

**D-047 docs (`def21df`/`22b5a99`) liko atmesta be trečio bandymo** (taisyklė: du atmetimai =
stabdyti tą dalį). Pirmas atmetimas: item 1 tyliai pakeitė D-045 2 žingsnio laiką (švarios pradžios
pakartotinis įkėlimas „po Etapo 2" → „pačioje pabaigoje") nepripažindamas, kad tai keičia planą, o
ne tik jį perrašo. Taisymas pripažino įtampą, bet antras atmetimas rado, kad taisymo sprendimas
citavo `PLAN-ETAPAS4.md` „kriterijų 3", kurio tuo metu dar nebuvo. Šis failas dabar egzistuoja (šios
pačios sesijos Part B), tad citata dabar teisinga, bet commit'ai liko be trečios peržiūros — **reikia
savininko dėmesio**.

**Neuždarytas D-045/D-047 klausimas** (taip pat `PLAN-ETAPAS4.md` OQ-4): ar švarios pradžios
pakartotinis įkėlimas vyksta iš karto po Etapo 2 (kaip originaliai sakė D-045 — Etapo 3 S5 to
reikalauja kaip savo šaltinio), ar pačioje pabaigoje (D-047), priimant, kad kriterijai 3/4 lieka
neišmatuoti tol.

**Kitas:** išspręsti D-045/D-047 laiko klausimą; savininkas patvirtina Lietuvos švenčių sąrašą
(`PLAN-ETAPAS4.md` OQ-2) ir suvestinės rodymo kanalą (OQ-3); `expense_audit_samples` DDL, kai
savininkas nori pradėti ketvirtinį auditą (OQ-5). C3 peržiūrėtojo nebloko radinys: savaitinės
suvestinės „arithmetic" vartų skaičius (2 numeris) naudoja `AMOUNT_MISMATCH`/`TOTALS_OUT_OF_RANGE`,
o realų antraštės kietą vartą (D-028/BR-CO-15) varo `AMOUNT_ARITHMETIC_MISMATCH`/
`MISSING_MONEY_FIELD` — paveldėta iš pačio plano (jau peržiūrėto/patvirtinto) kategorizavimo, ne šio
commit'o kodo defektas; verta patikslinti `PLAN-ETAPAS4.md` §5, kai bus grįžtama prie šio komponento.

## D-047 pataisymas, A1/A2 testai, Part D (deadlock) — 2026-09-27 naktis, antra sesija

Owner autorizuotas tolesnis paleidimas: pataisyti D-047 (abu ankstesni bandymai atmesti — žr.
aukščiau), pridėti tikrus testus A1/A2 (kurie anksčiau buvo pažymėti „neįmanoma" dėl bUnit
trūkumo), ir sutvarkyti sesijos metu 3 kartus pastebėtą tikrą MySQL deadlock nestabilumą testų
rinkinyje (Part D). Pilnas `dotnet test --filter "Category!=E2E"`: **1453 žali** (visos 5
pakartotos pilno rinkinio patikros po Part D taisymo — žalios; laikas pakito nuo ~30–32 s iki
~2 min 39–43 s, žr. žemiau).

| Dalis | Commit'ai | Ką daro | Verdiktas |
|---|---|---|---|
| Part 0 (D-047 pataisytas) | `8f6edd2` | Pakeičia `def21df`/`22b5a99` turinį (naujas commit'as, ne istorijos perrašymas): švari pradžia — KITAS žingsnis po šios sesijos, ne atidėta iki Etapų 3–4 pabaigos; pašalina D-045/D-047 žiedą; atsako OQ-2 (LT šventės — sutapo su jau esančiu C2 sąrašu, pridėtas 2028 m. testas) ir OQ-3 (tik prietaisų skydelis) | PATVIRTINTA (pirmą kartą) |
| A1+A2 testai | `5a5506b` | Tikri bUnit/interop testai abiem jau anksčiau patvirtintiems taisymams (`d419386`, `916688e`); `bunit` paketas pridėtas pirmą kartą šiam projektui; abu testai priešpriešiškai patikrinti (laikinai grąžinta sena elgsena scratch pakeitimu → testas raudonas → atstatyta → žalias) | PATVIRTINTA |
| Part D (deadlock) | `c77139b` | Visos 52 realios DB testų klasės pažymėtos `[Collection("RealDatabase")]` — standartinis xUnit būdas serializuoti testus, kurie dalinasi bendru ištekliumi | **ATMESTA** — trūko `ExpenseUploadDialogDragDropTests` (pridėtas `5a5506b` po šio commit'o sąrašo sudarymo) |
| Part D taisymas | `7d179bb` | Pridėta trūkstama žymė; 53/53 realios DB klasės dabar padengtos | PATVIRTINTA |

**Part D kaina, sąžiningai užfiksuota:** pilno rinkinio laikas ~5× ilgesnis (nuo ~30–32 s iki
~2 min 39–43 s), nes visos 53 realios DB klasės dabar vykdomos nuosekliai, ne lygiagrečiai.
Priimta kaip teisingas kompromisas: pilnas serializavimas yra matematiškai tikras taisymas (deadlock
fiziškai neįmanomas, kai 53 klasės niekada nesivykdo lygiagrečiai viena su kita), o siauresnis
(greitesnis) taisymas reikalautų tiksliai žinoti, kurios lentelės susikerta tarp visų 53 klasių —
klaida čia tyliai atkurtų tą patį nestabilumą.



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
  sąmoningas sprendimas (`LOW_CONFIDENCE` numetamas su komentaru, šie du — be jokio). Sutaisyta kode Etape 1 (S2a, `c7bd26b`: redagavimas perkelia viską, ko neperskaičiuoja); **patikrinta
  staginge 2026-09-27 (175: OWN_COMPANY išliko)**. `INVALID_VAT_RATE` vis dar perkeliamas be perskaičiavimo.
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
- **Re-OCR saugomiems failams** — sutvarkyta kode (S3, `8c67a62`); **patikrinta staginge 2026-09-27** (376: mygtukas, failo vardas, OCR_RETRIED); paskirstymų patvirtinimas (11) — tik automatiniai testai.
- **Data Protection raktai neišsaugomi tarp konteinerių** — kiekvienas deploy'us atjungia
  naudotojus (už OCR ribų).
- **3,8 GB RAM `lakstena-dev`** — prodas, staging, DB ir CI kartu (už OCR ribų).
- **PVM tarifų lentelė — visos eilutės NEPATVIRTINTOS** (`Services/Validation/VatRateTable.cs`): kol buhalterė
  nepatikrina pagal EK TEDB, vartai tik informuoja (`VAT_RATE_UNCHECKED` beveik ant kiekvienos sąskaitos).
  EE 22 → 24 % keitimo data nežinoma (eilutė nesuskaidyta); RO — dvi eilutės; „LI" tiekėjai (11) — įtariamos
  pagrindinių duomenų klaidos, lentelėje nėra (D-039).
- **Flaky testas** `CreditNoteServiceTests.UpdateCreditNoteAsync_NonExistentInvoiceLineId_…` — kartais krenta
  (nesusijęs su OCR; nesutvarkytas).
- **Du `IbanValidator`** (`Helpers` ir `Services.Validation`) — konsolidacija atskira užduotis.
- **Vėliavėlių čipai** `INVALID_IBAN` / `INVALID_VAT_FORMAT` su tiekėju vis dar raudoni (`IsCriticalFlag`), nors
  D-039 sako „informacija" — tik atvaizdavimas.
- **Prodo duomenų valymas su buhaltere** — `PROD-DATA-FINDINGS-2026-09-25.md` §6, Q-010.
- **„248 sąskaitos reikalauja dėmesio"** skaitiklis — beprasmis triukšmas, kol nevalyti
  duomenys.

## D-048 pre-clean-start tooling: bulk upload, clean-start script, prod runbook, D1/D2 — 2026-09-27/28 naktis, trečia sesija

Owner autorizuotas tolesnis paleidimas: D-048 (masinis įkėlimas + švarios pradžios scenarijus),
Part A (`clean-start-expenses.sql`), Part B (masinio įkėlimo administratoriaus puslapis), Part C
(galutinio prodo deploy'o instrukcija), Part D (`ExpenseBudgetDialog.Year` negyvas parametras,
C3 „arithmetic gate" kategorizacijos klausimas). NO DDL, niekas nepaleista prieš staging ar
prodą — viskas parašyta ir sausai/integraciniu testu patikrinta DEV (`nordic_bees_erp`,
`nordic_bees_erp_test` ant `100.110.26.80`) arba nepaliesta jokios DB (Part C — dokumentacija).

| Dalis | Commit'ai | Ką daro | Verdiktas |
|---|---|---|---|
| D-048 (docs) | `185fa61` | `DECISIONS.md` D-048 įrašas: masinio įkėlimo ir švarios pradžios sprendimai | PATVIRTINTA |
| Part A (skriptas) | `247f6d4` | `Migrations/Scripts/clean-start-expenses.sql` (DRY RUN + GUARD + DELETE + post-checks sekcijos, DB pavadinimas niekada neįrašytas kietai) + `CleanStartExpensesScriptTests` integracinis testas; naujai rasta schemos nuokrypa (`supplier_approvals.supplier_id` — realus FK į `business_partners`, kurio ankstesnis sąrašas nežinojo) įtraukta į abi (peržiūros ir vartų) užklausas | PATVIRTINTA (pirmą kartą) |
| Part B (masinis įkėlimas) | `00fbccf` | `/expenses/bulk-upload` (`[Authorize(Roles="Admin")]`), `BulkUploadService`/`IBulkUploadService` — naudoja TĄ PATĮ paslaugų sluoksnį kaip įkėlimo dialogas (`CreateFromOcrAsync`), neliečiant nė vieno užšaldyto dialogo nario; `BULK_CREATED` audito eilutė su partijos id, atskirai nuo `CREATED` | **ATMESTA** — recenzentas empiriškai atkūrė: dalinis OCR rezultatas (suma+tiekėjas atpažinti, numeris — ne) pasiekdavo `FileStore.SaveAsync` PRIEŠ `CreateFromOcrAsync` išmesdamas išimtį dėl tuščio numerio, taip „pametant" nesusietą `files` eilutę/blob'ą amžinai |
| Part B taisymas | `944d83e` | Atsisakymas PRIEŠ `FileStore.SaveAsync`, kai sąskaitos numeris neatpažintas; naujas testas įrodo abu (atsisakymą ir kad joks `files` įrašas nesukuriamas) | PATVIRTINTA (pakartotinė peržiūra) |
| Part D (D1+D2) | `b7da7ed` | D1: `ExpenseBudgetDialog.Year` dabar realiai valdo `_year` per `OnInitialized`; D2: „arithmetic" vartų skaitiklis dabar įtraukia BR-CO-15 (`AMOUNT_ARITHMETIC_MISMATCH`/`MISSING_MONEY_FIELD`, D-028), ne tik BR-CO-10 (`AMOUNT_MISMATCH`); `TOTALS_OUT_OF_RANGE` priklausomybė „arithmetic" grupei paliktas atviru klausimu (`PLAN-ETAPAS4.md` OQ-6, trys variantai, joks tyliai nepasirinktas) | PATVIRTINTA |
| Part C (runbook) | `0b1780f` | `Docs/ocr-rebuild/RUNBOOK-FINAL-PROD-DEPLOY.md` — visos schemos pakeitimai nuo v0.17.91 (D-039 `unit_price`, `supplier_bank_accounts`, `supplier_aliases`+`supplier_alias_events`) su DDL, patikrinta prieš DEV `information_schema`; IBAN backfill pažymėtas kaip neegzistuojantis įrankis (tik specifikacija); `expense_audit_samples` pažymėta PENDING (jokios DDL niekur nėra) | PATVIRTINTA |

**Visi keturi šios sesijos darbai (A, B, C, D) dabar sukurti `main` ir nepriklausomai
peržiūrėti/patvirtinti.** Pilnas `dotnet test --filter "Category!=E2E"`: **1462/1462 žali**
(nuo 1454 sesijos pradžioje — 8 nauji testai, 0 regresijų).

**Kitas žingsnis (savininkui, šia tvarka):**

1. Peržiūrėti ir push'inti (`bump-version.sh` → `git push origin main`) — niekas nestumta šios
   sesijos metu.
2. Naršyklės patikros: A1 (drag & drop po failo pašalinimo), A2 (klaidos paantraštė), masinio
   įkėlimo puslapis (`/expenses/bulk-upload`) su keliais realiais PDF, `ExpenseBudgetDialog` su
   skirtingu „Metai" nei einamieji.
3. Staginge: `clean-start-expenses.sql` DRY RUN patikra prieš `nordic_bees_erp_staging` (ta pati
   procedūra kaip Part C runbook §7 žingsnis 2, kuris pats aprašo TIK prodo švarią pradžią —
   staginge naudojama pagal analogiją, ne kaip runbook'o eilutė), tada pati švari pradžia
   (D-045/D-047), tada pakartotinis įkėlimas per `/expenses/bulk-upload`.
4. Etapo 4 kriterijų 3–4 matavimas su tais duomenimis (D-047 punktas 1).
5. Atsakyti PLAN-ETAPAS4.md OQ-6 (ar `TOTALS_OUT_OF_RANGE` priklauso „arithmetic" grupei).
6. Kai pasiruošę galutiniam deploy'ui — `Docs/ocr-rebuild/RUNBOOK-FINAL-PROD-DEPLOY.md`, jos
   pačios pažymėtus ⚠ punktus patikrinti pirmiausia.

## Už OCR ribų — tik užfiksuota

- ULAK sąskaita iš `DeliveryList.razor:277` išrašoma be išskaitų, o dialogo kelias
  (`InvoiceService.CreateInvoiceFromDeliveryAsync`) jas atima (overnight raportas Part C;
  FROZEN §4).
- CI `hardcode-check.yml`: grep žingsniai naudoja `--exclude-path` (ne GNU grep parinktis) —
  žingsnis gali praeiti nieko netikrinęs.

## Kitas žingsnis

**Etapas 2:** kodas baigtas (S1–S6), dokumentas `STAGING-CHECKS-ETAPAS2.md` parašytas. Savininkas: staging DDL →
deploy → pagrindinių duomenų valymas → švari pradžia ir pakartotinis įkėlimas → taisyklių patikros → go / no-go.
**Deploy politika (D-037):** prodas lieka v0.17.91, kol Etapai 1–4 nebaigti ir nepatikrinti staginge; dalinių
deploy'ų į prodą nėra. Prieš galutinį deploy'ą — atvirų punktų sąrašas skyriuje „Etapas 1 — staging patikrų
rezultatai", `STAGING-CHECKS-ETAPAS1.md` „Must be finished…" ir `STAGING-CHECKS-ETAPAS2.md` „What remains before the
Etapai 1–4 production deploy" (prodo DDL: `unit_price`, `supplier_bank_accounts`, `supplier_aliases`,
`supplier_alias_events`; prodo švari pradžia).

**Etapas 3:** S1–S4 baigtos `main` (žr. skyrių aukščiau); S5 (matavimas ant realaus korpuso, 40 dev + 20 hold-out)
laukia Etapo 2 švarios pradžios rezultatų — tai jos šaltinis, ne 11 dokumentų korpusas; S6 (Q-006/Q-007, parinkties
(a)/(b) persvarstymas pagal S5 skaičius) — po S5. Buhalterė turi patvirtinti bent LT formuluotes prieš S4's kodas
ims ką nors realiai keisti (šiandien elgsena nepakitusi).

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
- 2026-09-26 (Claude Code): Etapas 1 S1–S7 (+ Azure korpusas už git ribų, D-038…D-041), 853 testai; S8 — `STAGING-CHECKS-ETAPAS1.md`.
- 2026-09-26/27: Etapas 1 staging patikros (savininkas) — uždarytas staginge (D-043); šalies kodo taisymas (D-042, `073c299`).
- 2026-09-27 (Claude Code): STATE/D-043 — Etapo 1 uždarymas; `PLAN-ETAPAS2.md` (tik planas).
- 2026-09-27 (Claude Code, autonominis paleidimas): Etapo 2 fix-up'ai (A1/A2) ir `PLAN-ETAPAS3.md`.
- 2026-09-27 (Claude Code, autonominis paleidimas): D-046 + Etapo 3 S1–S4 — korpuso `tables[]` patikra, golden-file
  karkasas (`Verify.Xunit`), deterministinis lentelės taisymas (`TableLineRepair`, UTA PL realiai pataisytas),
  ZERO_VAT teisinio pagrindo patikra (`ZeroVatFormulationExtractor`, elgsena nepakitusi kol viskas NEPATVIRTINTA).
  1399 testai žali. `main` `c6b20bf`..`2950b84`.
- 2026-09-27 (Claude Code, autonominis paleidimas): D-047 + Part A (A1–A4: drag & drop po failo
  pašalinimo, klaidos paantraštė, `CreditNoteServiceTests` nestabilaus testo taisymas, orphan
  dialogų įėjimo taškai) + Part B (`PLAN-ETAPAS4.md`, D-031 kriterijai 3–6) + Part C (C1 žymėjimo
  CSV, C2 eilės senėjimas, C3 savaitinė suvestinė). D-047 docs commit'ai atmesti du kartus
  (netikslios citatos/D-045 laiko konfliktas), palikti be trečio bandymo pagal taisyklę — žr.
  aukščiau. 1448 testai žali. `main` `def21df`..`13e5ad4`.

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
| Etapas 1 (EN 16931, lokalės, IBAN, PVM tarifai, re-OCR) | **uždarytas staginge 2026-09-26/27** (D-043; `main` v0.17.94); prode nėra (D-037) |
| Etapas 2 (tiekėjo kaskada) | planas parašytas (`PLAN-ETAPAS2.md`), laukia savininko atsakymų |
| Etapas 3 (ekstrakcija — D-023 kryptis, D-016 UŽDARYTAS) | D-046 priimtas; **S1–S4 baigtos `main`** (`c6b20bf`..`2950b84`), ne staginge, ne prode; S5 (matavimas) laukia Etapo 2 švarios pradžios, S6 (Q-006/Q-007) nepradėtas |
| Etapas 4 (matavimas) | planas parašytas (`PLAN-ETAPAS4.md`, D-031 kriterijai 3–6); **C1–C3 baigti `main`** (žymėjimo CSV, eilės senėjimas, savaitinė suvestinė); realūs skaičiai laukia D-045/D-047 švarios pradžios laiko sprendimo |
| Saugykla, 2 žingsnis (PDF į IFileStore) | nepradėta |
