# Sprendimų žurnalas

Tik pildomas. Sprendimai nešalinami — jei sprendimas atšaukiamas, prirašomas
naujas su nuoroda į senąjį.

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
