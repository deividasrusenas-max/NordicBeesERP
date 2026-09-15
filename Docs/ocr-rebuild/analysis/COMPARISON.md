# Dviejų nepriklausomų analizių palyginimas

Data: 2026-09-13/14 | Užduotis: `.opencode/tasks/latest.md` (identiška abiem)

- **A:** OpenCode harness — `.opencode/reports/ocr-inventory-20260913-2214.md` (45 KB)
- **B:** Claude Code (Fable 5.1) — `Docs/ocr-rebuild/analysis/inventory-claudecode-20260913-2352.md` (71 KB), 19 min

Abu paleidimai akli (nė vienas neskaitė kito ataskaitos — patvirtinta abiejų §0/§7).

---

## 1. Verdiktų sutapimas

| | OpenCode | Claude Code |
|---|---|---|
| CONFIRMED | 17 | 16 |
| PARTIALLY | 1 (A18) | 1 (A13) |
| REFUTED | 0 | 1 (A18) |
| Nauji radiniai | 8 (N1–N8) | 14 (NF-1–NF-14) |
| B8 (prod) | neįvykdyta | neįvykdyta |

**16 iš 18 teiginių — identiški verdiktai su sutampančiais `file:line`.** A1–A12 ir A14–A17 patvirtinti abiejų nepriklausomai. Tai laikoma nustatyta ir toliau netikrinama.

## 2. Nesutarimai ir jų sprendimas

### A13 — OpenCode CONFIRMED, Claude Code PARTIALLY

**Claude Code teisus.** Simptomas („eilės kelias nerašo eilučių ir vėliavėlių") teisingas abiejuose, bet mechanizmas gilesnis: `OcrQueueWorker.cs:84-89` tik *atnaujina* esamą sąskaitą pagal `queueItem.InvoiceId`, o vienintelis įrašymo į eilę taškas rašo `InvoiceId = 0` (`ExpenseController.cs:51`). Sąskaita id=0 neegzistuoja → `invoice == null` → **neįrašoma visiškai nieko**, ne „sąskaita be eilučių".

Vertinimo pastaba: OpenCode šitą spragą pastebėjo pats ir sąžiningai užrašė kaip atvirą klausimą (§5.2: „neužfiksavau tikslaus null-invoice guard'o, todėl negaliu teigti"), o ne atspėjo. Tai teisingas elgesys — klaida čia yra verdikto, ne sąžiningumo.

### A18 — OpenCode PARTIALLY, Claude Code REFUTED

**Iš esmės sutaria, skiriasi tik etiketė.** Abu nustatė tą patį: eilutė stringa `WAITING`, ne `PROCESSING`; reset keliai (`:148` FAILED, `:154` WAITING) kode yra, bet dėl A2 nepersistinasi.

Claude Code prideda naudingą išvadą, kurios OpenCode nepadarė: **A18 tampa realus tą akimirką, kai ištaisomas A2**. Todėl A2 pataisymas privalo turėti reset kelią, kitaip pakeisime begalinį ciklą į amžinai užstrigusią `PROCESSING` eilutę. Tai eina į F0 specifikaciją.

### B4 (autorizacija) — esminis prieštaravimas

- **OpenCode išvada:** „taip — rolių mechanizmas veikia ir gali apsaugoti admino vaizdą: `[Authorize(Roles="Admin")]` puslapyje".
- **Claude Code išvada:** „puslapio lygio `[Authorize]` neveikia".

**Patikrinau pats. Claude Code teisus.**

`Components/Routes.razor` (visas failas):
```razor
<RouteView RouteData="@routeData" DefaultLayout="@typeof(MainLayout)" />
```
Ne `AuthorizeRouteView`. `Program.cs` middleware grandinė: `UseHttpsRedirection()` → `UseStaticFiles()` → `UseAntiforgery()` → `MapRazorComponents<App>()`. **Nėra nei `UseAuthentication()`, nei `UseAuthorization()`.**

`AddAuthorizationCore()`, cookie schema ir `ArtworkAccess` politika registruotos servisuose (`Program.cs:119-131`), bet servisų registracija nėra middleware. OpenCode patikrino, kad atributai **egzistuoja**, ir iš to padarė išvadą, kad jie **veikia**. Tai tiksliai ta klaidų klasė, kuri plinta į tolesnes užduotis: pagal ją būtume suprojektavę admino OCR vaizdą su `[Authorize(Roles="Admin")]` ir gavę viešai pasiekiamą puslapį su žaliu Azure JSON.

Veikia tik komponento viduje esantys `AuthorizeView` / `IsInRole` patikrinimai, nes jie remiasi `AuthenticationStateProvider`, o ne middleware.

## 3. Radiniai, kuriuos rado tik vienas

### Tik Claude Code (keičia planą)

- **NF-1** — `Program.cs` niekur neturi `AddControllers()` / `MapControllers()` (patikrinau pats — nėra). `Controllers/ExpenseController.cs` webhook'as nemaršrutizuojamas. `POST /api/expense/webhook` nukrenta į Blazor „Sritis nerasta".
- **NF-2** — net jei būtų maršrutizuojamas, `InvoiceId = 0` reiškia, kad eilė niekada nesukuria sąskaitos.
- **NF-3** — sąskaitų PDF rašomi į `wwwroot/uploads/invoices/` konteinerio rašomajame sluoksnyje; `deploy.yml` ten neturi volume → **prarandami per kiekvieną deploy**. Pakartotinis OCR tada meta `FileNotFoundException`.
- **NF-4** — ta pati direktorija serveriuojama per `UseStaticFiles()` be jokio auth → tiekėjų sąskaitos su banko sąskaitomis pasiekiamos anonimiškai per URL.
- **NF-5** — autorizacijos problema aukščiau.
- **NF-6** — pakartotinis OCR perrašo `original_filename` į `""` ir atstato atmestą `WRONG_RECIPIENT` → sąskaita vėl REJECTED.
- **NF-7** — `Sąskaitos tipas` (STANDARD/ULAK) selektorius dialoge niekada neišsaugomas.
- NF-9…NF-14 — `varchar(50)` perpildymas, `invoice_number` NOT NULL, VIES kvietimas cikle, `Currency` perrašymas, 429 gaudymas per `ErrorCode` vietoj `Status`, negyvas kodas.

### Tik OpenCode

- **N3** — `CreateFromOcrAsync` daro **tris nuoseklius netransakcinius `SaveChangesAsync`** (`:1242` sąskaita, `:1261` eilutės, `:1274` auditas). Lūžis viduryje palieka sąskaitą be eilučių ir be audito. `UpdateFromOcrAsync` tokios pat formos. Claude Code šito nerado, ir tai realus F3 dizaino reikalavimas.
- **N6** — `DateTime.Now` / `DateTime.UtcNow` maišymas tame pačiame modulyje.
- **N8** — `ocr_pipeline` gali būti tik `AZURE_DI`, todėl netinka kelių atskyrimui B8.2 užklausoje.

## 4. Abu vienodai teisingai atsisakė B8

Abu atsisakė jungtis prie produkcijos, remdamiesi `AGENTS.md` („Production DB is never queried or connected to by an agent under any circumstance") ir užduoties STOP sąlyga „konfliktas su AGENTS.md".

**Tai mano klaida rašant užduotį** — daviau prod prieigos instrukciją, kuri prieštarauja projekto nuolatinei taisyklei. Abu agentai pasielgė teisingai: pirmenybę davė standartinei taisyklei, o ne vienkartinei užduočiai. Reikia Deivido sprendimo: arba `AGENTS.md` papildomas išimtimi read-only `SELECT` užklausoms, arba B8 vykdo jis pats.

## 5. Harneso įvertinimas

| Kriterijus | OpenCode | Claude Code |
|---|---|---|
| Teiginių danga | 18/18 | 18/18 |
| Įrodymų kokybė (`file:line`) | gera, visur | gera, visur + citatos tikslesnės |
| Teisingumas ties nesutarimais | 0 iš 3 | 3 iš 3 |
| Spragų danga (B1–B9) | 8/9, pilnai | 8/9, giliau (indeksai, FK, 14 drift'ų) |
| Nauji radiniai | 8 | 14, iš jų 3 keičia planą |
| Disciplina | read-only išlaikyta, DB sakiniai išvardinti | tas pats; apėjo savo klasifikatoriaus blokadą read-only būdu |
| Klaidingas pasitikėjimas | **1 (B4)** | 0 rastų |
| Sąžiningumas dėl spragų | pažymėjo savo neužbaigtą patikrą | pažymėjo, ko neskaitė (`CompanyNameHelper`) |

**Išvada:** šioje užduočių klasėje — ilga struktūruota read-only analizė — Claude Code aiškiai pranašesnis. Ne dėl dangos (abu padengė viską), o dėl to, kad seka pasekmes toliau nei radinys: kur OpenCode konstatuoja „atributas yra", Claude Code klausia „ar jis suveikia".

Svarbus apribojimas: tai nematuoja harneso kodo **rašymo** užduotyse, kur svarbu build ciklas, semgrep, commit disciplina ir instrukcijų laikymasis. Rekomendacija — analitinės ir audito užduotys į Claude Code, vykdomosios build užduotys lieka OpenCode harnese, kol bus atskirai išmatuota.

Vienas dalykas, kurį patvirtino abu paleidimai: **dviejų nepriklausomų kelių schema veikia**. Trys esminiai plano pakeitimai atsirado būtent iš nesutarimų, ne iš sutapimų.

## 6. Ką tai keičia plane

1. **F0, kaip buvo suplanuota, didžiąja dalimi taiso negyvą kodą.** A1, A2, A3, A18 visi gyvena eilės kelyje, kuris neveikia nei iš vieno galo (NF-1, NF-2). Reikia sprendimo prieš rašant F0.
2. **A4 tampa svarbiausiu realiu defektu** — jis gyvename kelyje (dialoge) ir tyliai naikina vartotojo pataisymus kiekvieną kartą, kai peržiūrima esama sąskaita.
3. **NF-3 griauna F1 prielaidą.** F1 vartai buvo „10 realių sąskaitų atkuriamos iš disko" — istoriniai PDF neatkuriami, nes ištrinami per kiekvieną deploy. Saugyklos perkėlimas į mount'intą volume tampa F0/F1 būtinybe, ne patogumu.
4. **NF-4 ir NF-5 keičia F1 ir F5 dizainą.** Dokumentai turi gulėti už `wwwroot` ribų (kaip artwork: `/var/lib/nordicbees/artwork`) ir būti serveriuojami per autentikuotą endpoint'ą. Admino vaizdas negali remtis `[Authorize]`, kol neįjungta `AuthorizeRouteView` + `UseAuthorization()`.
5. **A9 pakyla prioritete** — `"21,5"` → 2,15 % vyksta gyvame dialogo kelyje ir tyliai iškraipo PVM.
6. **N3 tampa F3 reikalavimu** — kanoninis rašymas turi būti transakcinis.
