# Sandėlio langų suvienodinimas pagal Sąskaitų etaloną – ataskaita

Bazė: `33475e1` (fazės 0 ataskaita). Darbas atliktas ant `main`, nepushinta, versija nebumpinta, nedeployinta, jokių DB komandų / migracijų.

## git log

```
68138a2 style(invoices): show amounts with the shared Money() format (11 180,40 €)
3f9b938 feat(ui): Money() helper and use it on warehouse delivery pages
7d34daf fix(deliveries): forward list returnUrl between detail windows, accept local back targets only
d360569 fix(nav): highlight 'Kainos ir mokėjimai' on delivery pricing detail
a5aa289 feat(deliveries): align pricing detail with invoice view, fix footer, culture and duplicate debt
f3911e6 feat(deliveries): align delivery view header and buttons with invoice view
b066edc fix(deliveries): keep 'Priimtas' label for RECEIVED, tidy blank lines
e9e4bc5 refactor(deliveries): single status vocabulary helper for lists and details
e5a642e feat(deliveries): unify delivery pricing list with invoices list
7c41af8 feat(deliveries): unify deliveries list with invoices list
ed09c93 refactor(ui): extract shared list components from invoices list
33475e1 docs(reports): warehouse UI unification phase 0 investigation
```

**Sąskaitų formato pakeitimas** (fazė 6) yra atskirame commit `68138a2` (Invoices.razor, InvoiceView.razor). Jį galima atšaukti nepaveikiant sandėlio langų.

## Testai

- **Bazė (prieš fazę 1):** Passed 1900 / Failed 0 / Skipped 0.
- **Fazės 1 ir 2:** 1900 / 0 / 0.
- **Fazė 3 ir galutinis paleidimas:** DB testai nebegalėjo prisijungti prie dev DB (`MySqlException: Connect Timeout expired`, Tailscale). Galutinis: Passed 1358 / Failed 554 / Total 1912 (1900 + 12 nauji testai). Visi mano peržiūrėti nepavykę testai – DB prisijungimo timeout'ai (Expense*, Supplier*, Payment*, ReviewQueueAging*, WeeklySummary*, Pdf* ir kt.), jokių kompiliavimo ar logikos klaidų. **Tai ne regresijos įrodymas, bet ir ne jos paneigimas:** DB testai šioje sesijoje faktiškai nebuvo įvykdyti. Būtina perleisti `dotnet test`, kai dev DB pasiekiama.
- Nauji testai (visi praėjo): `DeliveryStatusHelperTests` (5), `FormatAmountHelperMoneyTests` (7).
- `dotnet build`: 0 klaidų po kiekvienos fazės.
- `agent-guardrails check --base-ref 33475e1`: tik įspėjimai (state-mgmt-complexity-multi-file, perf-degradation-large-change, big-bang-change); blokuojančių nėra.

## /code-review išvados

| Fazė | Rasta ir ištaisyta | Likę (nepataisyta) |
|---|---|---|
| 1 | užkoduota datos etiketė; pasikeitęs statuso čipo stilius; `ListPager` nekarpė puslapio | – |
| 2 | pasislėpusios pažymėtos eilutės; „pasirinkti visus" per visus puslapius; puslapis už ribų; filtras perskaičiuojamas kiekvieną kartą; globalus checkbox CSS | – |
| 3 | eilučių spalvinimas neįkainotoms; „Pristatymų nerasta" mirgėjimas kraunant | – |
| 4 | RECEIVED etiketė „Neįkainotas" klaidino → grąžinta „Priimtas"; tušti tarpai | Kainų sąrašas statusą skaičiuoja iš sumų, kiti langai rodo saugomą `deliveries.status` – esami duomenys gali skirtis (pre-existing) |
| 5 | grįžimo URL grandinėjimas tarp langų (returnUrl dabar perduodamas, o ne įdedamas); `HandleBack` priima tik lokalius kelius | Tiekėjo vardas rodomas ir paantraštėje, ir kortelėje (low); NavMenu išimtis specifinė vienam maršrutui (low); „Pristatymas" mygtukas matomas tik Admin/Warehouse (pagal nurodymą) |
| 6 + visas rinkinys | – (peržiūra pastebėjo tik fazės 0 ataskaitos pastabas) | Fazės 0 ataskaitoje minimas Culture defektas jau ištaisytas (žr. žemiau); PID/užduoties nr. ataskaitoje – laikini |

## Padaryta / nepadaryta

**Fazė 1 – bendri komponentai (padaryta).** `ListFilterCard`, `FilterChip`, `SelectionBar`, `ListPager`, `ListRowStyle`; Invoices.razor juos naudoja be elgsenos pasikeitimo.

**Fazė 2 – DeliveryList (padaryta).** „Naujas pristatymas" mygtukas; „Data (nuo / iki)"; čipai Visos · Neįkainoti · Be sąskaitos · Negrąžintos statinės · Šis mėnuo; spalvoti statuso čipai; puslapiavimas po 50; pasirinkimo juosta su „Išrašyti sąskaitas (N)" (ULAK logika nepakeista); `?q=` veikia; dvigubas paspaudimas atidaro eilutę.

**Fazė 3 – DeliveryPricing (padaryta).** Viena čipų eilutė Visos · Neįkainoti · Neapmokėti · Dalinai · Vėluojantys · Šis mėnuo; paieška ir pagal sąskaitos Nr.; `?q=`; tuščios būsenos pranešimas; „Suma" tik skaičius, „su 6 % PVM" antroje eilutėje; eilučių spalvos kaip sąskaitose (`ListRowStyle`); atidarymas dvigubu paspaudimu; puslapiavimas po 50. Paieška atmintyje, `InvoiceService` neliestas, URL parametrų pokytis nekrauna iš DB.

**Fazė 4 – statusų žodynas (padaryta).** `DeliveryStatusHelper` (vienas žodynas + `KeyFromAmounts`) naudojamas abiejuose sąrašuose ir abiejuose detalės languose. DB reikšmės nekeistos. **Nepadaryta:** `SupplierDebtDetail.razor` turi savo kopiją – ne tikslinis langas, paliktas (siūlau atskirai).

**Fazė 5 – detalės langai (padaryta, po commit kiekvienam).**
- DeliveryView: `MaxWidth.ExtraLarge`; antraštė kaip InvoiceView (numeris + statuso čipas, „Tiekėjas: …"); mygtukai kairėje: Atgal · Atsisiųsti aktą (jei pasirašyta) · Kainos ir mokėjimai → (Admin/Manager) · **Gauti parašą** (vienintelis užpildytas, Color.Primary). Tiesioginio redagavimo logika nekeista. „Spausdinti" mygtuko nėra – yra tik „Atsisiųsti aktą".
- DeliveryPricingDetail: `ReturnUrl` + Atgal; Atgal · Pristatymas → (Admin/Warehouse) · Išrašyti sąskaitą · **Registruoti mokėjimą**; pagrindinis – „Išrašyti sąskaitą", kol nėra sąskaitos, vėliau „Registruoti mokėjimą". Ištaisyta: dvigubas „+"; **suvestinės eilutė (footer)** – vietoj `colspan="4"` dabar aiškūs 7 langeliai pagal antraštę (colspan nebuvo pritaikomas MudTd – atitinka naršyklėje matytus x=1261 vs 2039); trečios kortelės pavadinimas „Liko"; skola rodoma vieną kartą (pašalinta viršutinė „Skola" kortelė); **sugadintas `Culture=` atributas** (eilutės ~118 ir ~247) išvalytas. Fiziškai naršyklėje footer nepatikrintas (nebuvo galimybės), patikrinta tik kompiliavimu.
- NavMenu: minimalus pakeitimas – `/warehouse/deliveries/{id}/pricing` paryškina „Kainos ir mokėjimai"; kiti punktai elgiasi kaip anksčiau.

**Fazė 6 – pinigų formatas (padaryta).** `FormatAmountHelper.Money()` („11 180,40 €", U+00A0, 2 sk., „0,00 €"); `Trim` nekeistas. Sandėlio langai – `3f9b938`, sąskaitos – atskirai `68138a2`. Vieneto kaina (`PriceExclVat`) sąskaitoje paliekama su `FormatAmount`, kad nebūtų apvalinama iki 2 sk.

## Žinomi apribojimai / neaišku

- DB testai neįvykdyti (žr. aukščiau) – reikia pakartoti.
- Neįvertinta naršyklėje (nebuvo vizualinio tikrinimo): footer lygiavimas, mygtukų eilutės, NavMenu paryškinimas.
- Pašalinti `?status=` filtrai iš pristatymų sąrašo ir „Apmokėtos" skirtukas iš kainų sąrašo – pagal planą.
- Kainų sąrašas duomenis kraunasi tik `OnInitializedAsync` – spustelėjus tą patį meniu punktą duomenys neatsinaujina.
- `InvoiceService.GetInvoicesAsync(take: 5000)` vis dar kraunami visi dokumentai; visos pristatymų lentelės kraunamos atmintyje (kaip sąskaitose).
- Dvigubas paspaudimas nepatikimas jutikliniuose ekranuose (plano reikalavimas).
- Fazės 0 punktas 5 (redagavimo išsaugojimas): tiesioginis redagavimas DeliveryView išsaugomas automatiškai; statinių `MudNumericField` išsaugo per kiekvieną `ValueChanged`. Logika nekeista.
- `supply_date` migracija netaisyta, kaip nurodyta.

## Pasiūlymas atskirai (neliesta)

Sąskaitos detalėje (`InvoiceView`) naudojamas `Color.Success` ir juodraščiui du užpildyti mygtukai – tai prieštarauja §22 ir vieno pagrindinio mygtuko taisyklei. Sandėlio languose naudojamas `Color.Primary` ir vienas užpildytas mygtukas; sąskaitos detalė paliko kaip buvo.

Atmintinė: nordicbees-erp · darbai +1 ✓0

## Testai po DB įjungimo

Ryšys: `nc -zv -w 5 100.110.26.80 3306` → `Connection to 100.110.26.80 port 3306 [tcp/mysql] succeeded!`. `git status --short` švarus, šaka `main`, HEAD `3ce552d`.

Pilnas `dotnet test` (log `/tmp/test-full.log`), tikra suvestinės eilutė:

```
Passed!  - Failed:     0, Passed:  1912, Skipped:     0, Total:  1912, Duration: 3 m 36 s - NordicBeesERP.Tests.dll (net10.0)
```

- Bazė (prieš 1 fazę): 1900 praėjo / 0 nepavyko. Dabar: 1912 praėjo / 0 nepavyko.
- Skirtumas 12 = nauji testai, visi praeina (atskiras paleidimas su `--filter`: `Passed: 12, Failed: 0`): `DeliveryStatusHelperTests` (5) ir `FormatAmountHelperMoneyTests` (7).
- Nepavykusių testų: 0 (`grep -E "^\s+Failed "` → 0 eilučių), todėl (a)/(b)/(c) klasifikuoti nėra ko; pataisymų commitų šiame žingsnyje nereikėjo, `/code-review` nebuvo reikalingas.
- Ankstesnis 554 nepavykusių rezultatas buvo vien dėl nepasiekiamos DB (`Connect Timeout expired`) – dabar paneigtas: regresijų nėra.
- Migracijos ir DB komandos nevykdytos.

## Pataisymai po vizualinės patikros (localhost:5082)

| # | Problema | Commit |
|---|---|---|
| 1 | Sumų eilutė: `tfoot > tr > tr > td` (įdėtas `<tr>`). Pašalintas vidinis `MudTFootRow` iš `FooterContent` (MudTable jau apgaubia `<tr>`), paliekami `MudTd` tiesiogiai. Patikrinti visi `FooterContent` projekte: įdėtas `MudTFootRow` buvo tik **DeliveryPricingDetail.razor** ir **DeliveryView.razor** (abu ištaisyti tame pačiame commit); ExpenseCashFlow, PaymentHistory, InvoicePaymentList, StatisticsOverview (2), SalesStatistics, DeliverySignature jau naudoja `MudTd` tiesiogiai. | `e8913fe` |
| 2 | Viena efektyvi būsena visuose keturiuose languose: `DeliveryStatusHelper.KeyFor/GetLabel/GetColor(Delivery)` skaičiuoja iš `DebtBase`/`PaidAmount` (kaip kainų sąrašas). Naudojama DeliveryList, DeliveryPricing, DeliveryView, DeliveryPricingDetail. Senieji string perkrovimai pervadinti į `LabelForKey/ColorForKey`. `DeliveryService.GetByIdAsync` dabar užpildo `PaidAmount` iš `supplier_payments` (kaip `GetFilteredAsync`), kitaip DeliveryView visada matytų 0 sumokėta. DB reikšmių nekeista, `RefreshStatusAsync` nekviestas. Pridėti testai (efektyvi būsena ignoruoja pasenusį saugomą statusą, ribiniai atvejai). | `80eb089` |
| 3 | Netto kg pristatymo kainų detalėje (eilutės ir suvestinė) formatuojamas `ToString("N1")` kaip sąrašuose. | `bf37687` |
| 4 | Pristatymų sąrašo pasirinkimo juostoje „Išrašyti sąskaitas (N)" rodomas visada, kai yra pasirinkimas; disabled, kai tinkamų nėra; tooltip nurodo priežastis (neįkainoti / jau turi sąskaitą / tiekėjas ne ūkininkas su kompensaciniu PVM kodu). | `c2251bf` |
| 5 | „Mokėjimai": tuščia būsena „Mokėjimų dar nėra". | `cbad8d9` |

### Čipai ir saugomas statusas (punktas 2)

- **Pristatymų sąrašas:** čipas „Neįkainoti" anksčiau rėmėsi saugomu `Status == "RECEIVED"`; dabar naudoja efektyvią būseną (`KeyFor(d) == "RECEIVED"`, t. y. `DebtBase <= 0`), todėl sutampa su rodoma būsena. Čipai „Be sąskaitos" (`InvoiceNumber`) ir „Negrąžintos statinės" nuo statuso nepriklauso; „Šis mėnuo" – nuo datos.
- **Kainų sąrašas:** čipai Neįkainoti / Neapmokėti / Dalinai / Vėluojantys jau rėmėsi `DebtBase`/`PaidAmount` – sutampa su efektyvia būsena.
- **Nepakeista (už keturių langų ribų), gali nesutapti su rodoma būsena, jei saugomas statusas atsilieka:** `Components/Pages/Home.razor` (neįkainotų pristatymų skaičius), `Services/DashboardSnapshotWorker.cs`, `SupplierDebtDetail.razor` (savo statusų žemėlapis), `DeliveryService.GetFilteredAsync(status, …)` (filtruoja pagal saugomą `Status`; naujas UI jo nebenaudoja su statusu). Siūlau atskirai suvienodinti.
- `KeyFromAmounts` grąžina „Priimtas", kai `DebtBase <= 0`, net jei yra avansinis mokėjimas (taip pat elgiasi kainų sąrašas nuo pradžių).

### /code-review (po kiekvieno pataisymo)

- 1: be pastabų.
- 2: pastabos dėl Home/DashboardSnapshotWorker/SupplierDebtDetail/GetFilteredAsync (aukščiau, už apimties ribų), `PaidAmount` išvedimas dubliuojamas trijose `DeliveryService` vietose (galima išskirti bendrą metodą), `GetByIdAsync` neturi atskiro DB testo.
- 3–5: tik žemo prioriteto pastabos: pasirinkimo juostos `IneligibleReason` skaičiuojamas kiekvieną perpiešimą ir priežasčių skaičiai gali persidengti; svorio formatas (`N1`) kopijuojamas keliose vietose (galima `Weight()` helperis); „Mokėjimų dar nėra" spalva `#666` kaip kitur šiame lange. `CreateInvoicesForSelected` tuščio sąrašo atveju jau rodo įspėjimą.

### Testai

`dotnet test` po 2 punkto: `Passed! - Failed: 0, Passed: 1919, Skipped: 0, Total: 1919, Duration: 3 m 33 s`. Po 3–5 punktų: `Passed! - Failed: 0, Passed: 1919, Skipped: 0, Total: 1919, Duration: 3 m 33 s`. (Bazė 1912/0 + 7 nauji testai.) Aplikacija perstatyta ir paleista iš naujo ant http://localhost:5082.
