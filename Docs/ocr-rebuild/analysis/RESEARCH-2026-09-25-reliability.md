# Patikimumą lemia vartai, ne atpažinimas

Sistema neveikia patikimai ne todėl, kad Azure blogai skaito sąskaitas, o todėl, kad niekas neprivalo sureaguoti į tai, ką ji randa. Produkcijos duomenys tai parodo tiesiogiai: 247 sąskaitos buvo pažymėtos vėliavėlėmis, buhalterė jas matė, nepataisė nė vienos ir paliko duomenis produkcijoje — rezultatas 25 dublikatų grupės, 27 pertekliniai įrašai ir **28 679,74 € dvigubai apskaityta**. Tuo pačiu metu atpažinimo klaida, kurią turi konkretų pavyzdį — `prebuilt-invoice` perskaitė `Quantity=3` ten, kur dokumente parašyta „3 888,000", o `Amount` paėmė iš „Suma su PVM" stulpelio (972,00) vietoj „Suma be PVM" (803,31) — kainavo nulį eurų, nes `analyzeResult.tables[2]` toje pačioje atsakymo struktūroje turėjo teisingus duomenis su tvarkingais `columnHeader` langeliais. Tai reiškia tris atskiras problemas, kurios dažnai suplakamos į vieną: netinkama ekstrakcijos strategija, nesančios deterministinės taisyklės ir peržiūros procesas, kurio niekas nenaudoja. Pagal padarytą žalą jos rikiuojasi atvirkščiai, nei intuityviai atrodo — peržiūros procesas kainavo 28 679,74 €, deterministinių taisyklių nebuvimas leido tai nepastebėti, o atpažinimo klaida kainavo tik jūsų laiką. Todėl darbų eilė turi prasidėti nuo kietų vartų (hard gates) ir EN 16931 aritmetikos, o ne nuo pipeline'o perrašymo — nors pipeline'ą irgi reikia perrašyti, ir šioje ataskaitoje parašyta kaip.

---

## 1. Diagnozė: kur iš tikrųjų problema

Turimi įrodymai leidžia atskirti tris nepriklausomas gedimo priežastis, ir tai svarbu, nes kiekviena taisoma skirtingai ir kiekviena kainuoja skirtingai.

**(a) Netinkama ekstrakcijos strategija.** `prebuilt-invoice` antraštę perskaitė gerai — SubTotal 934,22 (conf 0,95), TotalTax 196,18, InvoiceTotal 1130,4, VendorTaxId LT333473113 (conf 0,836) — bet eilutes sugadino katastrofiškai. Esminė detalė: tame pačiame `AnalyzeResult` objekte `tables[2]` turėjo tą pačią lentelę idealiai, su teisingai atpažintais `columnHeader` langeliais (`Eil. Nr., Pavadinimas, Kodas, Mato vnt., Kiekis, Kaina be PVM, Kaina su PVM, Nuol. %, Suma be PVM, Suma su PVM`) ir teisingais europietiško formato skaičiais (`3 888,000`, `803,31`, `972,00`). **Žalias OCR buvo geras. Blogas buvo prebuilt field-mapper.** Tai nėra atpažinimo kokybės problema — tai laukų priskyrimo (mapping) problema, ir ji taisoma pakeičiant sluoksnį, kuris daro priskyrimą, o ne geresniu OCR.

**(b) Deterministinio tikrinimo nebuvimas.** Jei būtų veikusi paprasta `Σ(eilutės be PVM) = SubTotal` patikra, ta pati sąskaita būtų sustojusi automatiškai: eilučių suma su 972,00 niekada nesusives į 934,22. Ši patikra nėra jūsų išgalvota — tai EN 16931 BR-CO-10 taisyklė su oficialiai apibrėžta 0,01 valiutos vieneto tolerancija ([ConnectingEurope/eInvoicing-EN16931](https://github.com/ConnectingEurope/eInvoicing-EN16931)). Jos nebuvimas reiškia, kad kiekviena ekstrakcijos klaida keliauja į buhalterinę knygą nepastebėta, nebent žmogus ją asmeniškai pagauna.

**(c) Peržiūros procesas, kurio niekas nenaudoja.** Čia yra pagrindinė žala. Vėliavėlės buvo — VENDOR_NOT_FOUND 141, MISSING_DUE_DATE ~74, AMOUNT_MISMATCH ~70, ZERO_VAT ~47, DUPLICATE ~26, OWN_COMPANY ~14 — ir buhalterė jas ignoravo iki vieno. Tai nėra jos asmeninis aplaidumas; tai tiksliai atitinka literatūroje dokumentuotą alert fatigue mechaniką (žr. 6 skyrių). Ir būtent šis gedimas pagimdė 28 679,74 €.

| Problema | Tiesioginė žala turimais duomenimis | Taisymo kaina | Prioritetas |
|---|---|---|---|
| (c) Peržiūra, kurios niekas nenaudoja | **28 679,74 €** dvigubai apskaityta, 27 pertekliniai įrašai, 2 ateities datos produkcijoje | Maža (kietų vartų logika + UI) | **1** |
| (b) Nėra deterministinio tikrinimo | 0 € tiesiogiai, bet tai vienintelė priežastis, kodėl (a) ir (c) liko nepastebėti | Maža (10–15 val. C# kodo) | **2** |
| (a) Bloga ekstrakcijos strategija | 0 € — klaida buvo pagauta rankiniu tyrimu, ne produkcijoje | Vidutinė (pipeline perrašymas) | **3** |

Skaičius, kurį verta įsidėmėti: **vienas dublikatų incidentas kainavo maždaug 6 kartus daugiau, nei visa teorinė metinė darbo sąnaudų nauda iš automatizavimo** (~4 830 USD/metus, žr. 9 skyrių). Todėl bet koks planas, kuris pradeda nuo ekstrakcijos tobulinimo ir kietus vartus palieka „vėliau", optimizuoja neteisingą dydį.

---

## 2. Ką daryti su atpažinimu

### Kodėl `prebuilt-invoice` genda būtent šios klasės dokumentuose

Microsoft pats turi sprendimų medį, ir jis priklauso nuo šablonų kintamumo. „Standardizuotoms, vieno formato sąskaitoms" Microsoft rekomenduoja `prebuilt-invoice` („high accuracy with common, structured document templates... best when consistency, low latency, and proven accuracy are the priority"), o „didelio kintamumo pusiau struktūrizuotoms sąskaitoms iš daugelio tiekėjų su skirtingais formatais" — Content Understanding custom analyzer arba LLM mapping sluoksnį ([Choosing the right AI tool](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/choosing-right-ai-tool)). Sąskaitos iš LT, DE, RO, LV, EE ir UA nuo ~100–200 tiekėjų yra antroji kategorija be jokių išlygų.

Azure Architecture Center kanoninė nuoroda sako tą patį kitais žodžiais: rekomenduojamas kelias yra **OCR/layout → Azure OpenAI schemos priskyrimas → struktūrizuotas JSON su confidence**, o Document Intelligence prebuilt modeliai įvardijami kaip *alternatyva* komandoms, kurios apdoroja standartinius dokumentų tipus ir kurių schema sutampa su standartiniais modeliais ([Extract and map information from unstructured content](https://learn.microsoft.com/en-us/azure/architecture/ai-ml/architecture/automate-document-processing-azure-ai-document-intelligence)). Tą pačią architektūrą nepriklausomai įgyvendina ir Microsoft artimi .NET pavyzdžiai — [azure-ai-document-pipeline-sample](https://github.com/jamesmcroft/azure-ai-document-pipeline-sample) siunčia dokumentą tiesiai į GPT-4o su vision ir Structured Outputs, o [Azure-Samples/azure-ai-document-processing-samples](https://github.com/Azure-Samples/azure-ai-document-processing-samples) turi tris lygiagrečias strategijas, įskaitant multimodalinę (layout tekstas + puslapio vaizdas).

### Ką paduoti LLM: `tables[]`, ne markdown

Čia yra konkreti, dokumentuota spąstų vieta. `outputContentFormat=markdown` lenteles renderina kaip **HTML `<table>` markup'ą, ne markdown pipe-lenteles**, būtent tam, kad išsaugotų `rowspan`/`colspan` ([Document Intelligence supported Markdown elements](https://learn.microsoft.com/en-us/azure/ai-services/document-intelligence/concept/markdown-elements?view=doc-intel-4.0.0)). Tačiau Azure SDK issue trackeryje yra užregistruota ir **uždaryta kaip „not planned"** problema apie sugadintas lenteles markdown išvestyje su stulpelių nesulygiavimu ([Issue #29071, azure-sdk-for-js](https://github.com/Azure/azure-sdk-for-js/issues/29071)), ir antra, vis dar atvira, apie neatitikimą tarp to, ką rodo Studio, ir to, ką grąžina markdown API ([Issue #36834, azure-sdk-for-python](https://github.com/Azure/azure-sdk-for-python/issues/36834)). Oficialus markdown dokumentacijos puslapis apskritai nedokumentuoja stulpelių antraščių semantikos ar daugiapuslapių lentelių tęsinio.

Praktinė išvada: **serializuokite `analyzeResult.tables[]` patys**, su eksplicitiškai pažymėtomis `columnHeader` reikšmėmis, ir paduokite LLM šią serializaciją kartu su markdown tekstu (prozai, etiketėms, apatinėms pastaboms). Tai apeina dokumentuotą colspan gedimą ir — svarbiausia — tai būtent tas sluoksnis, kuris jūsų realiame gedimo atvejyje suveikė teisingai. Svarbu pažymėti: **nepavyko rasti nė vieno oficialaus Microsoft A/B testo, kuris lygintų „paduok LLM `tables[]` JSON" su „paduok markdown"** — tai pagrįsta išvada iš dokumentuotų markdown apribojimų ir jūsų paties įrodymų, ne išmatuotas faktas.

### Vaizdas ar tik tekstas: yra konkretūs skaičiai

Stipriausias turimas 2025–2026 m. įrodymas yra dedikuotas benchmark'as, palyginęs 8 modelius per 3 šeimas trijose sąskaitų/kvitų aibėse po 500–1000 pavyzdžių, kur natyvus vaizdo įvedimas buvo lyginamas su Docling teksto konversijos pipeline'u ([arXiv:2509.04469](https://arxiv.org/abs/2509.04469)):

| Dokumentų klasė | Natyvus vaizdas | Teksto pipeline (Docling) | Skirtumas |
|---|---|---|---|
| Švarios sąskaitos | **96,50 %** | 85,14 % | +11,4 p. p. |
| Skenuotos sąskaitos | **92,71 %** | 63,94 % | +28,8 p. p. |
| Skenuoti kvitai | **87,46 %** | 47,00 % | +40,5 p. p. |

Straipsnio išvada: „native image processing generally outperforms structured [text-conversion] approaches", o konversijos žingsnis „created a performance bottleneck". Du perspėjimai, kuriuos reikia pasakyti atvirai: benchmark'as naudojo Docling, **ne** Azure DI layout markdown, todėl tikslus skirtumo dydis jūsų konkrečiai konfigūracijai neįrodytas; ir straipsnio kalbų mišinys nėra nurodytas — **jokio benchmark'o baltiškoms ar lietuviškoms sąskaitoms nerasta**. Tačiau kryptis jūsų atvejui ypač aktuali: jūsų gedimas buvo *pozicinė* klaida (pasirinktas ne tas stulpelis tarp dviejų vizualiai gretimų), o ne simbolių atpažinimo klaida — tokia klaida vizualiniame kontekste yra akivaizdi, o serializuotame tekste dingsta.

### Dual-call: ką konkrečiai jis duoda

ExtractConf aprašo tiksliai tokią architektūrą: du struktūriškai **asimetriški** LLM kvietimai — „Hunter" (field-guided: „surask lauką X", linkęs haliucinuoti reikšmes nesantiems laukams) ir „Mapper" (document-guided: skenuoja visą dokumentą, iškelia turiniu pagrįstus kandidatus, bet praleidžia vizualiai neryškius laukus). Jų **nesutarimas** naudojamas kaip nepriklausomas patikimumo signalas, nes gedimo režimai skirtingi ir paprastu resampling'u neišlenda ([arXiv:2606.24420](https://arxiv.org/abs/2606.24420)). Rezultatai būtent sąskaitų aibėje (DocILE): **ROC AUC 0,928**, **99,1 % tikslumas esant 80 % coverage**, ir 70 % selective-prediction rizikos sumažėjimas prieš logprob baseline'ą.

Jūsų atveju „Hunter vs Mapper" persidengia su diagnoze beveik pažodžiui: `prebuilt-invoice` field-mapper (field-guided) paėmė ne tą stulpelį, o `tables[]` (holistinis, dokumentu grįstas vaizdas su išlikusia antraštės eilute) turėjo teisingus duomenis. Praktiškai tai reiškia du promptus — vieną „ištrauk šiuos įvardintus laukus", kitą „štai visa lentelė, perrašyk kiekvieną eilutę pažodžiui ir tik tada susieki su schema" — ir žmogaus peržiūrą ten, kur eilučių sumos, kiekiai ar `Amount` skiriasi daugiau nei tolerancija. **Svarbu:** nerasta nė vieno produkcinio inžinerinio aprašymo, kad kokia nors įmonė šį dual-call šabloną naudotų gyvoje sistemoje — įrodymas yra akademinis (ir tai workshop, ne main track straipsnis, žr. 5 skyrių), ne pramoninis.

### Structured Outputs: ką jis garantuoja ir ko ne

`strict: true` su `json_schema` garantuoja, kad atsakymas atitiks jūsų JSON Schema. Tai **tik sintaksinė garantija**. Griežtame režime negalima naudoti `minimum`/`maximum` skaičiams, `minLength`/`pattern`/`format` eilutėms, `minItems`/`maxItems` masyvams; visi laukai privalo būti `required` (neprivalomumas modeliuojamas kaip nullable unija); `additionalProperties: false` privalomas; riba — 100 savybių ir 5 gylio lygiai ([How to use structured outputs with Azure OpenAI](https://learn.microsoft.com/en-us/azure/foundry/openai/how-to/structured-outputs)). Konkrečiai: **schema negali priversti, kad `Quantity` būtų teigiamas** — tai jūsų C# kodo darbas.

Ir tai tiksliai nurodo, kodėl Structured Outputs neišsprendžia jūsų problemos: `prebuilt-invoice` išvestis *jau* buvo teisingos formos JSON su egzistuojančiais `Quantity`, `UnitPrice`, `Amount` laukais. Blogos buvo reikšmės. Formos garantija čia nepadeda niekuo — padeda geresnis įvestis (2 skyrius) ir deterministinė patikra (3 skyrius).

**Rekomenduojama architektūra:** `prebuilt-layout` (ne `prebuilt-invoice`) → savo serializuotas `tables[]` + markdown + puslapio vaizdas → du asimetriški Azure OpenAI Structured Outputs kvietimai → nesutarimo vėliava → deterministinės taisyklės → kieti vartai arba auto-priėmimas.

---

## 3. Deterministinis tikrinimas — pigiausias ir tikriausias laimėjimas

Tai yra skyrius, kurį reikia įgyvendinti pirmiausia po kietų vartų, nes jis yra vienintelė dalis visoje sistemoje, kur teisingumas yra **įrodomas**, o ne tikėtinas.

### EN 16931 aritmetinės taisyklės su tiksliais ID ir tikra tolerancija

Oficialūs EN 16931 validacijos artefaktai (Schematron/XSLT UBL 2.1 ir CII D16B sintaksėms) yra CEN/TC 434 palaikomi, EUPL 1.2 licencijos, v1.3.16 (2026 m. balandis) ([ConnectingEurope/eInvoicing-EN16931](https://github.com/ConnectingEurope/eInvoicing-EN16931)). Svarbi detalė, kuri dažnai užrašoma neteisingai: **tolerancija yra 0,01 valiutos vieneto kiekvienam palyginimui, skaičiuojama kaip `round(value × 100) / 100`** — ne plokščias „0,02 EUR" konstantas, kaip kartais manoma. Schematrono testas rašo `round(sum(...) * 10 * 10) div 100` ([EN16931-UBL-validation-preprocessed.sch](https://github.com/ConnectingEurope/eInvoicing-EN16931/blob/master/ubl/schematron/preprocessed/EN16931-UBL-validation-preprocessed.sch)).

| Taisyklė | Tapatybė | Ką pagauna jūsų duomenyse |
|---|---|---|
| **BR-CO-10** | Σ(eilutės neto, BT-131) = BT-106 | **Tiesiogiai „Suma su PVM" vietoj „Suma be PVM" klaidą** — 972,00 niekada nesusives į 934,22 |
| **BR-CO-13** | BT-109 = Σ(eilutės neto) − nuolaidos + priemokos | Praleistos, dubliuotos ar neteisingai priskirtos eilutės |
| **BR-CO-15** | BT-112 = BT-109 + BT-110 (neto + PVM = bruto) | Skaitmenų klaidas antraštėje, PVM tarifo klaidas |
| **BR-CO-16** | BT-115 = BT-112 − BT-113 + BT-114 | Apmokėjimo/apvalinimo nesutapimus |
| **BR-CO-18** | Privalo egzistuoti bent viena PVM išklotinės grupė (BG-23) | Netikrą/nestruktūrizuotą sąskaitą |

Nepriklausomas antrinis šaltinis patvirtina pačias tapatybes atskirai nuo Schematrono ([Invoice Navigator, EN16931 rules guide](https://www.invoicenavigator.eu/blog/en16931-validation-rules-complete-guide)). **BR-S-08 (standartinio tarifo PVM kategorijos taisyklė) tikslaus teksto iš pirminio šaltinio šiame tyrime gauti nepavyko** — minima tik antriniuose šaltiniuose be formulės.

Kritinė riba: EN 16931 **nereikalauja** per-eilutės `qty × price = line net` perskaičiavimo — BT-131 tiesiog *deklaruojama* siuntėjo. Todėl eilutės lygio tolerancija yra **jūsų dizaino sprendimas, ne teisinis reikalavimas**, ir tai reikia užrašyti kodo komentare. Pagrįstas pradinis variantas: `max(0.01, 0.5 % nuo eilutės neto)`.

Pilno Schematron/XSLT pipeline'o statyti neverta: reikėtų OCR laukus paversti sintetiniu UBL/CII XML, o ~955 taisyklių didžioji dalis yra struktūrinės/kodų sąrašų taisyklės, nereikšmingos jau ištrauktiems duomenims. **Aritmetinės BR-CO taisyklės yra maža, lengvai perkeliama poaibis (~5–10 taisyklių)** — parašykite jas kaip C# metodus tiesiai iš Schematrono teksto, ir klaidos pranešime cituokite taisyklės ID („BR-CO-15 pažeista"). Referencinėms implementacijoms verta paskaityti Go paketus [speedata/einvoice/rules](https://pkg.go.dev/github.com/speedata/einvoice/rules) ir [jxsl13/einvoice/rules](https://pkg.go.dev/github.com/jxsl13/einvoice/rules) — **palaikomo .NET NuGet paketo su pilnu EN 16931 rinkiniu šiame tyrime rasti nepavyko**.

### Lokalės skaičių parsinimas — būtent tai, kas būtų pagavę „3 888,000" → 3

Abi jūsų klaidos yra vadovėlinis dešimtainio kablelio ir tūkstančių skirtuko dviprasmiškumas. `"3 888,000"` europietiškai reiškia 3888,000, bet jei parseris naudoja `en-US` konvencijas arba tiesiog nutrina tarpą ir kablelį palaiko dešimtainiu tašku, gaunamas „3,888" → 3. `"9,000"` yra tikslus atvirkštinis atvejis: amerikietiškai tai devyni tūkstančiai, europietiškai — devyni.

Blogiausia naujiena: **.NET jūsų neapsaugos**. `Decimal.Parse`/`TryParse` su `NumberStyles.Any` + `InvariantCulture` turi dokumentuotą, užregistruotą defektą — jis priima neteisingą tūkstančių skirtukų išdėstymą (pvz., priima `"1,1.1"`), nes tikrina simbolių aibę, bet netikrina grupių dydžio teisingumo ([dotnet/runtime issue #4762](https://github.com/dotnet/runtime/issues/4762)).

Vienintelis tvirtas sprendimas nėra „pasirinkti geresnę `CultureInfo`" — ta pati eilutė `"9,000"` yra genuinly dviprasmiška be konteksto. Sprendimas yra **konteksto valdomas disambiguavimas**, ir jis remiasi 3 skyriaus aritmetika: išbandykite abi interpretacijas ir pasirinkite tą, kuri **susiveda** su atspausdinta eilutės suma per toleranciją. Jei `3888 × 0,2066 ≈ 803,31` susiveda, o `3 × 0,2066 = 0,62` nesusiveda, atsakymas nedviprasmiškas. Tai paverčia dviprasmišką parsinimą vienareikšmiu, naudojant paties dokumento vidinį perteklių, ir yra stipresnis signalas nei bandymas atspėti lokalę iš OCR teksto. Papildomai: atmeskite arba pažymėkite bet kurį parsinimą, kur eilutėje yra **ir** kablelis, **ir** taškas, o jų pozicijos neatitinka griežtų 3 skaitmenų grupavimo taisyklių nė vienai kandidatinei lokalei — .NET parseris to už jus nepadarys.

Datos turi tą pačią ligą. Lietuva perėjo prie ISO 8601 (`YYYY-MM-DD`), anksčiau naudojo `YYYY.MM.DD`; Vokietijoje ISO 8601 tapo nacionaliniu standartu 1995 m., bet tradicinis `DD.MM.YYYY` 2001 m. buvo formaliai vėl leistas kaip alternatyva — **t. y. vokiška sąskaita teisėtai gali būti bet kuriuo formatu**, ir ERP negali fiksuoti vieno ([Date and time notation in Europe](https://en.wikipedia.org/wiki/Date_and_time_notation_in_Europe)). Dvi jūsų ateities datos (2026-10-06, 2026-11-02) abi turi dienos komponentę ≤12, t. y. abi patenka į dviprasmišką DD/MM vs MM/DD zoną — **tai hipotezė, pagrįsta duomenų raštu, o ne nepriklausomai patikrintas faktas**, bet ji tiksliai paaiškintų abu atvejus. Rumunijos, Latvijos, Estijos ir Ukrainos datų bei skaičių formatų konvencijų **šiame tyrime nepavyko patikrinti prieš cituojamą autoritetingą šaltinį** — prieš implementaciją verta pasitikrinti tiesiai CLDR duomenyse (`unicode-org/cldr`, failai `common/main/ro.xml`, `lv.xml`, `et.xml`, `uk.xml`), nes būtent juos .NET naudoja per ICU.

### Kontrolinės sumos: kas realiai įgyvendinama

**IBAN yra vienintelė pilnai specifikuota, nedviprasmiška kontrolinė suma šiame sąraše** ir turi būti padaryta iš karto. ISO 13616 mod-97: perkelkite pirmus 4 simbolius į galą, raides paverskite skaičiais (A=10…Z=35), patikrinkite ≡ 1 (mod 97) ([IBAN Structure and Mod 97 Validation](https://medium.com/@matlabb/iban-structure-and-mod-97-validation-algorithm-719e3d4db5f2)). Ilgiai: LT=20, DE=22, RO=24, LV=21, EE=20 ([ValidateFin IBAN Format by Country](https://validatefin.com/en/blog/iban-format-by-country)). Ukrainos IBAN (29 simboliai) statusas **šiame tyrime nebuvo patikrintas pirminiu šaltiniu** — teigta iš bendrų žinių apie NBU perėjimą.

PVM numerių kontrolinės sumos yra kur kas prastesnė padėtis, ir čia reikia būti tiesmukam:

| Kodas | Formatas patikrintas | Kontrolinės sumos algoritmas |
|---|---|---|
| LT įmonės kodas / PVM kodas | 9 arba 12 skaitmenų | **Nerastas** — egzistavimas patvirtintas ([ambrazasp/lt-codes](https://github.com/ambrazasp/lt-codes) grąžina `INVALID_CONTROL_NUMBER`), svorių vektorius — ne |
| DE USt-IdNr | 9 skaitmenys | **Egzistavimas nepatvirtintas** nė viename gautame šaltinyje |
| LV / EE / RO | 11 / 9 / 2–10 skaitmenų | **Nerasti** |
| UA EDRPOU / ІПН | 8 / 9–12 skaitmenų | **Nerasti**; taxid.pro aiškiai nurodo „no validation methodology is provided" |

Praktinė išvada: **PVM kontrolinių sumų reverse-engineering'as yra žemo prioriteto, didelės tyrimo kainos darbas**, nes VIES jau duoda autoritetingą gyvą patikrą ES numeriams. Bet VIES reikia naudoti teisingai. Jo dokumentuoti fault kodai apima `SERVICE_UNAVAILABLE (300)`, `MS_UNAVAILABLE (301)`, `TIMEOUT (302)`, `GLOBAL_MAX_CONCURRENT_REQ (500)`, `MS_MAX_CONCURRENT_REQ (600)` ([VIESAC](https://viesac.eu/articles/what-to-do-when-vies-unavailable)), o vokiški numeriai **specifiškai ir dažnai** grąžina `MS_MAX_CONCURRENT_REQ` — tai įvardintas, pasikartojantis gedimas ([viesapi.eu](https://viesapi.eu/vies-problems-with-verifying-companies-from-germany-de/)). Taisyklė: **„nepasiekiamas" ≠ „negalioja"**, niekada neblokuokite darbo eigos laukdami VIES, dėkite į eilę ir kartokite fone. .NET klientas egzistuoja: [zapadi/vies-dotnet](https://github.com/zapadi/vies-dotnet) (Apache 2.0, NuGet, v3.1.0).

### ZERO_VAT ir MISSING_DUE_DATE: dvi vėliavėlės, kurios klaidingai suprastos

**ZERO_VAT (~47) beveik neabejotinai yra triažo, o ne aptikimo spraga.** 0 % PVM yra teisėtas esant vidinės Bendrijos tiekimui, atvirkštiniam apmokestinimui arba įstatyminei išimčiai — visi trys dažni medaus verslui, perkančiam pakuotes, logistiką ar žaliavas iš DE/RO/LV/EE. Bet ESTT byloje **C-247/21 (Luxury Trust Automobil)** nusprendė, kad sąskaitoje privalo būti būtent žodžiai „Reverse charge" — pakaitalas kaip „Exempt intra-Community triangular transaction" **nepakanka**, ir praleidimo **negalima ištaisyti atgaline data** ([VATupdate C-247/21](https://www.vatupdate.com/2025/06/12/briefing-document-podcast-ecj-c-247-21-luxury-trust-automobil-gmbh-mandatory-invoice-requirements-for-triangulation-are-final-and-uncorrectable/)). Taisymas: kai PVM = 0 %, deterministiškai ieškokite privalomos formuluotės ištrauktame tekste; radus — vėliavėlė automatiškai užsidaro; neradus — tai **tikra atitikties rizika**, ne triukšmas. *Tikslios lietuviškos formuluotės („Atvirkštinis apmokestinimas") praktinio vartojimo šiame tyrime patvirtinti nepavyko* — patikrinkite realiose LT sąskaitose arba VMI gairėse prieš naudodami kaip string-match taikinį.

**MISSING_DUE_DATE (~74) greičiausiai yra klaidingai klasifikuota.** Direktyvos 2006/112/EB 226 straipsnis išvardija privalomą sąskaitos turinį: išrašymo data (226(1)), eilės numeris (226(2)), tiekėjo PVM ID (226(3)), tiekimo data jei skiriasi (226(7)), apmokestinamoji suma pagal tarifą (226(8)), PVM tarifas (226(9)), PVM suma (226(10)), atvirkštinio apmokestinimo nuoroda (226(11a)) ([VATupdate, Art. 226](https://www.vatupdate.com/2022/05/12/eu-vat-directive-2006-112-ec-explained-art-226-content-of-an-invoice/)). **Mokėjimo termino sąraše nėra.** Tai komercinė konvencija, ne ES teisės reikalaujamas elementas. Šios vėliavėlės perklasifikavimas iš „klaida" į „informacija" kainuoja mažiau nei valandą ir vienu ypu pašalina ~74 iš 372 vėliavėlių — tai didžiausias triukšmo sumažinimas už mažiausią darbą visoje sistemoje.

PVM tarifų baltasis sąrašas pagal šalį ir datą yra dar viena pigi patikra. Rumunija **nuo 2025-08-01** pakeitė standartinį tarifą iš 19 % į **21 %** ir sujungė 5 %/9 % į vieną **11 %** (GD 602/2025) ([VATupdate](https://www.vatupdate.com/2025/07/08/romania-to-implement-new-vat-rates-standard-21-reduced-11-by-august-2025/), [Fiscal Solutions](https://www.fiscal-requirements.com/news/4272)). 2026 m. tarifai: LT 21 % (5/9), DE 19 % (7), RO 21 % (11), LV 21 % (5/12), EE 24 % (9/13) ([Tax Foundation, 2026 VAT Rates in Europe](https://taxfoundation.org/data/all/eu/value-added-tax-vat-rates-europe/)). Bet kuris tarifas už šio rinkinio ribų yra arba OCR klaida, arba tikras neatitikimas — abiem atvejais vertas sustabdymo.

### Dublikatai: teisingas raktas

Standartinė AP praktika lygina tiekėją, sąskaitos numerį, sumą ir datą, bet **eksplicitiškai įspėja, kad to neužtenka**: įvardintas gedimas yra „ta pati sąskaita užregistruota po dviem tiekėjų įrašais", nes dauguma ERP tikrina tik tikslų sąskaitos numerį per tiekėją ([Stampli](https://www.stampli.com/resources/duplicate-invoice-detection/)). Tai tiesiogiai jungiasi su jūsų VENDOR_NOT_FOUND 141: fragmentuota tiekėjų kartoteka **sulaužo** numeriu grįstą dedupą.

Teisingas dizainas yra trijų sluoksnių. Pirma, **failo turinio hash (SHA-256) prieš bet kokį OCR** — nulinės kainos, nulio klaidingų teigiamų, pagauna tą patį PDF įkeltą dukart. Antra, **loginis raktas `(normalizuotas tiekėjo PVM kodas arba įmonės kodas, normalizuotas sąskaitos numeris)`** — ne vardas, nes būtent laisvo teksto vardas fragmentuoja tiekėją. Trečia, **fuzzy sutapimas pagal `(tiekėjas, suma, data ±N dienų)`**. Pastebėtina: 25 rastos dublikatų grupės ir ~26 DUPLICATE vėliavėlės skaičiai beveik sutampa, kas leidžia daryti išvadą, kad **esama dublikatų logika nėra bloga tiems atvejams, kuriuos pagauna** — 28 679,74 € greičiausiai kilo iš dublikatų, kurie apskritai nebuvo pažymėti, dėl tiekėjo tapatybės fragmentacijos. *Tai išvada iš skaičių artumo, ne nepriklausomai patvirtintas faktas.*

### i.SAF: nepanaudotas kryžminės patikros šaltinis

Verslas beveik neabejotinai jau teikia **gautų PVM sąskaitų faktūrų registrą VMI per i.SAF** — kalendorinio mėnesio periodiškumu, iki kito mėnesio 20 d., apimant **ir išrašytas, ir gautas** sąskaitas ([invoicedataextraction.com](https://invoicedataextraction.com/blog/lithuania-i-saf-requirements)). i.MAS tapo privalomas nuo 2016-10-01; i.SAF-T (platesnis audito failo standartas, kitas dalykas) — nuo 2020-01-01 tik įmonėms virš **45 mln. €** apyvartos, t. y. jūsų netaikomas ([ecosio.com](https://ecosio.com/en/compliance/lithuania/e-invoicing/)). Tai reiškia, kad **jau šiandien** egzistuoja nepriklausomas, VMI teikiamas tų pačių laukų perpasakojimas, ir bet koks neatitikimas tarp OCR ištraukto ir i.SAF pateikto yra deterministinis klaidos signalas. Tai brangiausia šio skyriaus pozicija (15–25 val.) ir reikalauja išsiaiškinti, kaip verslas šiuo metu i.SAF duomenis ruošia.

---

## 4. Tiekėjo atpažinimas

VENDOR_NOT_FOUND yra **141 — didžiausia kategorija su dideliu atotrūkiu**, ir jos priežastis beveik tikrai yra vidinis kartotekos sutapatinimas, ne išorinis validavimas. VIES to neišspręs: jis pasako, ar PVM numeris galioja, bet nepasako, kuris jūsų kartotekos įrašas yra tas pats tiekėjas.

### Rivile Cloud šešių raktų kaskada — vienintelis patikrintas Baltijos precedentas

Iš visų tirtų sistemų (Rivile, Finvalda, Centas, Agnum, Directo, Merit Aktiva, Standard Books) **tik Rivile Cloud turi viešai dokumentuotą, patikrinamą daugiaraktę kaskadą**: tiekėjas identifikuojamas naudojant „pavadinimą, PVM kodą, įmonės kodą, IBAN sąskaitos numerį, telefoną, el. paštą" kartu, o neradus atitikmens sistema **nukrenta į numatytąją kategoriją, o ne blokuoja dokumentą** ([Rivile ERP guide – OCR](https://guide.rivile.cloud/integrative_solutions/ocr)). Tai išorinis patvirtinimas, kad IBAN, įmonės kodo, telefono ir el. pašto pridėjimas kaip raktų yra pramonėje normalus dizainas, o ne spekuliacija. Netiesiogiai tą patį rodo ir kita LT platforma: teisingas priskyrimas „priklauso nuo to, ar tinkamai suvestas įmonės kodas, PVM kodas" ([help.monet.lt](https://help.monet.lt/docs/darbozpradzia/pardavimai-9737/irasu-vedimas-11388/ocr/)). **Visų kitų įvardintų sistemų sutapatinimo raktų patikrinti nepavyko** — tai įrodymų spraga, ne įrodymas, kad jų nėra.

### Siūloma kaskada

| Pakopa | Raktas | Auto-priėmimas? | Būtina normalizacija |
|---|---|---|---|
| 1 | PVM ID tikslus | Taip | Nuimti šalies prefiksą (`LT`/`Lt`/`lt`), tarpus, brūkšnelius |
| 2 | Įmonės registracijos kodas tikslus | Taip | Nuimti tarpus; LT — 9 skaitmenys |
| 3 | IBAN tikslus **prieš to tiekėjo žinomų sąskaitų aibę** | Taip, bet **naujas IBAN esamam tiekėjui → sulaikyti peržiūrai** | mod-97 kontrolinė suma, nuimti tarpus |
| 4 | Normalizuotas juridinis pavadinimas tikslus | Tik su patvirtinimu iš 6 ar 7 pakopos | Diakritikų šalinimas, teisinės formos žetonų šalinimas (UAB/AB/MB/IĮ, SIA, OÜ/AS, GmbH/AG/UG, SRL/SA, ТОВ/ФОП), `&`/`ir`/`und`/`și` suvienodinimas |
| 5 | Fuzzy pavadinimas (Token Sort/Token Set + Jaro-Winkler) | Tik ≥95/100, kalibruota jūsų sąraše; 85–95 → peržiūra; <85 → ne sutapimas | Ta pati kaip 4, prieš skaičiavimą |
| 6 | El. pašto / svetainės domenas iš sąskaitos | Ne — tik patvirtinantis | Mažosios raidės, subdomenų triukšmo šalinimas |
| 7 | Telefono numeris | Ne — tik lygiųjų laužytuvas | Formatavimo šalinimas, šalies kodo normalizacija |
| 8 | Išmoktas alias (buhalterės patvirtintas ≥2–3 kartus) | Taip, po promocijos | Tikslus sutapimas su patvirtinta OCR eilute/IBAN |
| 9 | Registro praturtinimas → **naujo teisingo** tiekėjo įrašas | Ne — vienkartinis žmogaus patvirtinimas | Per-registro laukų priskyrimas |

Slenkstis ≥95/100 nėra išgalvotas: taip daro DNB (Nyderlandų centrinis bankas) savo įmonių pavadinimų sutapatinimo darbe, kartu su teisinės formos priesagų nuėmimu per `cleanco` prieš bet kokį panašumo skaičiavimą, ir rekomenduoja rankinę peržiūrą prie pat slenksčio FP/FN dažniams įvertinti ([DNB Data Science Hub](https://medium.com/dnb-data-science-hub/company-name-matching-6a6330710334)). .NET tai įgyvendinama be jokio custom variklio: [FuzzySharp](https://www.nuget.org/packages/FuzzySharp/) turi `TokenSortRatio`/`TokenSetRatio`, [F23.StringSimilarity](https://www.nuget.org/packages/F23.StringSimilarity) — Jaro-Winkler ir Levenshtein šeimą. Blocking (kandidatų generavimas) **jums nereikalingas**: prie 100–200 tiekėjų n² yra daugiausia 40 000 palyginimų, t. y. milisekundės ([Coleridge Initiative, Record Linkage](https://textbook.coleridgeinitiative.org/chap-link.html)).

### Kodėl IBAN yra stiprus, bet pavojingas raktas

IBAN mechaniškai pranašesnis už pavadinimą: fiksuoto formato, su kontroline suma, be teisinės formos priesagų, be transliteracijos, be skyrybos dviprasmybių. Bet **jis nėra saugus kaip savarankiškas raktas**, ir tam yra trys skirtingos priežastys. Teisėtas tiekėjas gali turėti kelias sąskaitas (skirtingos valiutos, banko keitimas) — todėl IBAN modeliuojamas kaip **vienas-su-daugeliu**, o naujas IBAN esamam tiekėjui eina į peržiūrą, ne į tylų priėmimą. Faktoringas reiškia, kad sąskaita teisėtai nurodo **trečiosios šalies** IBAN — naivi sistema, reikalaujanti sutapimo su žinomu tiekėjo IBAN, klaidingai atmestų teisėtą faktoringą, o naivi sistema, mokanti iš bet kurio IBAN, leistų sukčiui tyliai peradresuoti visus būsimus mokėjimus ([EZ Invoice Factoring](https://www.ezinvoicefactoring.com/top-factoring-fraud-prevention-red-flags-in-invoice-verification/)). Ir „bank details changed" yra įvardintas, dokumentuotas sukčiavimo šablonas, kur pagrindinis apsaugos mechanizmas yra patvirtinimas **nepriklausomu kanalu** — skambučiu į anksčiau žinomą numerį, ne į tą, kuris nurodytas naujoje sąskaitoje ([FraudRoom](https://fraudroom.com/blog/fake-invoice-bank-details-scam), [vetthisvendor.com](https://vetthisvendor.com/guide/supplier-changed-bank-details)). **Baltijos regionui specifinės statistikos apie šio sukčiavimo dažnį nerasta** — šaltiniai bendri/globalūs.

### Registrai: kurie realiai nemokami ir užklausiami

| Šalis | Šaltinis | Statusas |
|---|---|---|
| **RO** | ANAF `PlatitorTvaRest` v9, `https://webservicesp.anaf.ro/api/PlatitorTvaRest/v9/tva` | **Patvirtintai nemokamas ir dokumentuotas.** Grąžina pavadinimą, adresą, registracijos nr., PVM statusą, telefoną ir **deklaruotą IBAN**. Limitai: ≤100 CUI per užklausą, ~1 užkl./s ([doc_WS_V9.txt](https://static.anaf.ro/static/10/Anaf/Informatii_R/Servicii_web/doc_WS_V9.txt)) |
| **EE** | e-äriregister / RIK open-data API | **Patvirtintai nemokamas** („the use of the services is free of charge"), reikia pasirašyti nemokamą sutartį (peržiūra ≤5 d. d.); autocomplete veikia be sutarties. Iki 50 000 užkl./d. ([RIK API](https://avaandmed.ariregister.rik.ee/en/open-data-api/introduction-api-services)) |
| **Visos ES** | GLEIF / LEI | **Patvirtintai nemokamas, be registracijos** ([GLEIF](https://www.gleif.org/en/lei-data/access-and-use-lei-data)). Bet **tikėtinai labai žemas padengimas** jūsų tiekėjams — LEI daugiausia turi reguliuojamų finansinių sandorių dalyviai. *Tai išvada, ne cituota statistika.* |
| **LT** | Registrų centras / data.gov.lt | Atviri **duomenų rinkiniai** (JAR, JADIS) yra ([data.gov.lt](https://data.gov.lt/dataset/lietuvos-respublikos-juridiniu-asmenu-registre-iregistruoti-juridiniai-asmenys/)), bet **gyvo per-užklausą REST API, analogiško ANAF ar RIK, patvirtinti nepavyko**. Praktiškai tai reikštų periodinį rinkinio veidrodį lokaliai |
| **LT** | rekvizitai.lt | **Nepatikrinta.** Oficialios API dokumentacijos nerasta; rasta tik neoficiali trečiosios šalies API ir diskusija apie scraping'o teisėtumą. **Nelaikykite jo nemokamu ar teisėtu API** |
| **LV** | ur.gov.lv per VDAA API Manager | API egzistuoja, **kaina nenurodyta** gautame puslapyje ([ur.gov.lv](https://www.ur.gov.lv/en/registry-api-web-services-service-list)) |
| **DE** | Handelsregister | **Nemokamo oficialaus API nepatvirtinta.** Duomenys perskelbti trečiųjų šalių (OffeneRegister.de, OpenCorporates); šaltinis iš 2019 m., dabartinis pilnumas nepatikrintas ([netzpolitik.org](https://netzpolitik.org/2019/das-handelsregister-endlich-offene-daten/)) |
| **UA** | Opendatabot | **Nelaikykite nemokamu** — kaina neatskleista, reikia paraiškos ([Opendatabot](https://opendatabot.ua/en/open/api)) |
| **ES** | VIES | Nemokamas, bet grąžina vardą/adresą **nenuosekliai pagal šalį** (DE — struktūrizuotus laukus, IE — vieną sulietą eilutę, kai kurios — nieko), ~10 užkl./s riba, planinis prastovos langas pirmadieniais 03:00–05:00 UTC ([vatverify.dev](https://www.vatverify.dev/guides/vies-api-guide)) |

### Mokymasis iš pataisymų — tik su apsaugomis

**Nerasta nė vieno dokumentuoto produkcinio „tiekėjo alias auto-mokymosi" mechanizmo** jokioje ERP ar record-linkage sistemoje. Tai, kas žemiau, yra pagrįstas dizainas, ne cituota praktika. Alias turi būti apribotas **tiksliu** raktu („ši normalizuota OCR eilutė → tiekėjas Y"), ne fuzzy kaimynyste — tai apriboja klaidingo pataisymo sprogimo spindulį. Auto-taikymas turi būti **už patvirtinimų skaitiklio** (pirmas pataisymas sukuria kandidatą, ~2–3 nepriklausomi patvirtinimai jį promuoja) — dvasia analogiška Oracle EPM „Auto Accept threshold" šablonui, kuris yra realiai išleistas produkte ([Oracle EPM](https://docs.oracle.com/en/cloud/saas/readiness/epm/2024/epm-dec24/24dec-epm-wn-f36287.htm)). Ir **prieštaraujantis** naujas pataisymas (ta pati eilutė priskirta kitam tiekėjui) turi **sustabdyti** alias peržiūrai, ne tyliai perrašyti. Kiek patvirtinimų yra teisingas skaičius — **jokios kiekybinės medžiagos nerasta**; 2–3 yra pagrįstas pradinis parametras, ne geroji praktika.

---

## 5. Pasitikėjimo įvertinimas: kas realu prie 420 dokumentų per metus

Čia reikia atskirti naudingą žodyną nuo nenaudingos matematikos.

### Selective prediction kaip kalba, o ne kaip formulė

Selective classification formalizuoja sistemą kaip porą (prognozuotojas *f*, selektorius *g*): **coverage** φ = E[g(x)] yra dalis įvesčių, į kurias sistema atsako, o **selective risk** R = E[ℓ(f(x),y)·g(x)]/φ yra klaidų dažnis tik priimtoje aibėje ([arXiv:2505.15008](https://arxiv.org/html/2505.15008)). Trijų pakopų maršrutizatorius (auto-post / peržiūra / atmesti) yra tiesiog du slenksčiai γ_low < γ_high tame pačiame patikimumo skalėje — du taškai ant risk-coverage kreivės.

Šio žodyno vertė jums yra **komunikacinė, ne skaičiavimo**. Sakinys „esant 80 % coverage, auto-priimtų sąskaitų tikslumas yra 99 %" buhalterei yra suprantamas ir veiksmingas; „vidutinis confidence 0,89" — ne. Bet pati kreivė prie jūsų n bus labai triukšminga, todėl praktinis receptas yra: paimkite Azure DI per-lauko confidence, ranka sužymėkite ~60 sąskaitų laukų teisingai/neteisingai, **pažiūrėkite, žemiau kokios reikšmės susispiečia klaidos**, ir nustatykite slenkstį konservatyviai virš jos. Tai risk-coverage mąstymas be statistiškai įvertintos kreivės.

### Conformal prediction: kiekybinis verdiktas ir įrašo pataisymas

IJDAR straipsnis yra tikras ir patikrinamas: **Rombach & Mehdiyev, „Beyond Accuracy: Understanding Model Confidence in Key Information Extraction with Conformal Prediction", IJDAR 29(2), 551–563** ([Springer](https://link.springer.com/article/10.1007/s10032-026-00572-y)). Bet **„~165 kalibravimo pavyzdžių" figūros patikrinti nepavyko ir ji atrodo klaidinga**. Tikrieji straipsnio skaičiai: kalibravimas ant **100 kvitų, duodančių 4 074 token lygio kalibravimo taškus**, esant **α = 0,02**; testas ant 472 kvitų; pasiekta **98,3 % marginal coverage**, **70 % singleton prognozių**, vidutinis prognozės aibės dydis **1,38**. Kritinė detalė vertimui į jūsų kontekstą: **kalibravimo vienetas yra laukas/token, ne dokumentas.**

Iš to seka kiekybinis atsakymas. Split-CP baigtinės imties garantija yra 1−α ≤ P ≤ 1−α + 1/(n+1), o mažiausia prasminga α yra maždaug 1/(n+1) ([Angelopoulos & Bates, arXiv:2107.07511](https://arxiv.org/pdf/2107.07511)):

| Kalibravimo n | Min. α ≈ 1/(n+1) | Realiai naudingas α | Ką tai reiškia |
|---|---|---|---|
| 60 (dokumentai, ~2 mėn.) | 0,016 | **0,10–0,20** | Prie krašto kvantilis atsisėda ant blogiausio balo — aibės tampa maksimaliai konservatyvios, praktiškai beverčios |
| 165 | 0,006 | 0,05–0,10 | 90–95 % coverage |
| 420 (dokumentai, visi metai) | 0,0024 | 0,02–0,05 | Pradeda priminti tai, ką pasiekė straipsnis — bet **straipsnis turėjo 4 074 laukų, ne 420 dokumentų** |

Ir lemiamas skaičius: **pačios coverage stabilumui** (ne tik jos ilgalaikiam vidurkiui) tas pats šaltinis duoda lentelę esant α=0,1, δ=0,1 — **n(ε=0,1)=22, n(ε=0,05)=102, n(ε=0,01)=2 491**, o paties vadovėlio nykščio taisyklė yra „n=1000 užtenka daugumai tikslų". Tai reiškia: kad coverage būtų pririšta ±1 procentinio punkto tikslumu, reikia ~2 500 kalibravimo taškų. **Per dokumentus jūs to nepasieksite niekada; per laukus (35 sąsk./mėn. × ~15 laukų ≈ 525/mėn.) pasieksite per ~5 mėnesius rankinio žymėjimo** — bet tik jei rankinio tikrinimo tempas neatsilieka, o jis atsiliks.

Yra ir gilesnė problema, kurią patys IJDAR autoriai įvardija: **exchangeability lūžta būtent ten, kur gyvena jūsų rizika.** Jie tiesiogiai vardija layout drift/šablonų evoliuciją, pasiskirstymo poslinkį tarp tiekėjų, OCR kokybės svyravimą. Jūs turite šešių šalių šablonus ir nuolat naujus tiekėjus. Be to, laukai iš to paties dokumento nėra nepriklausomi: sisteminis gedimas (pvz., DE sąskaitos visada painioja neto/bruto dėl dešimtainio kablelio) koreliuos per visus to dokumento laukus, todėl **efektyvi imtis yra mažesnė nei žalias skaičius** — to IJDAR straipsnis neanalizuoja, ir jokio KIE-specifinio šaltinio, kuris tai kiekybiškai įvertintų, nerasta.

**Verdiktas: conformal prediction jums nėra pradžios taškas.** Jis tampa gynybiškas kaip vidutinės trukmės tikslas, jei ir tik jei (a) kalibruojate per laukus, ne per dokumentus, (b) laikote α laisvą (0,05–0,15), ir (c) perkalibruojate ketvirtiniu slenkančiu langu — tiksliai tai, ką patys autoriai rekomenduoja. Teiginys „165 pavyzdžiai yra įrodytas minimumas" straipsnyje neranda pagrindo.

### ExtractConf: pakartotinai panaudojamas radinys

ExtractConf yra **vieno autoriaus workshop straipsnis** (Nitesh Kumar, RobustifAI @ IJCAI-ECAI 2026, Bremenas) — **ne main track publikacija**, todėl konkrečius skaičius reikia laikyti pranešta-bet-nepatikrinta ([arXiv:2606.24420](https://arxiv.org/abs/2606.24420)). Tai pasakius, jis yra tiksliausiai į jūsų klausimą atsakantis darbas, koks egzistuoja.

| Konfigūracija | AUC | AURC |
|---|---|---|
| B1: logprob vidurkis (baseline) | 0,705 | 0,145 |
| B3: self-consistency (5×) | 0,744 | 0,138 |
| M1: **tik logprob** features | 0,880 | — |
| M4: **tik OCR** features | **0,896** | — |
| M6: pilnas ExtractConf (40 features, CatBoost) | **0,928** | 0,043 |
| M8: + Lasso rekalibracija | 0,928 | **0,042** |

**Tai yra esminis radinys jums: OCR kilmės požymiai VIENI pasiekia AUC 0,896 prieš 0,928 pilno apmokyto modelio.** Trijų punktų skirtumas. Tai reiškia, kad **didžiąją dalį vertės gaunate be jokio modelio apmokymo** — be CatBoost, be 40 požymių inžinerijos, be labeled dataset'o, be perkvalifikavimo disciplinos, kurios solo developeris prie 420 dokumentų per metus neišlaikys. Autorių pačių paaiškinimas, kodėl taip: „OCR confidence directly measures the cause of failure; logprobs measure a consequence orthogonal to it."

Antras naudingas radinys: požymių svarbos viršūnėje yra **HV>90 ir HV>75** — dalis Hunter ištrauktų reikšmių, sutampančių su OCR kandidatais prie ≥90 %/≥75 % panašumo, t. y. **cross-call sutarimas, sulietas su OCR pagrindimu**. Grynas OCR reikšmės confidence yra trečias. **Logprob požymių gautame top-6 nėra apskritai.** Tai tiesiogiai argumentuoja prieš investavimą į Azure OpenAI token logprobs kaip pagrindinį pasitikėjimo signalą ir už OCR confidence + dviejų kvietimų sutarimą — o tai jums patogu, nes Azure DI confidence jau ateina nemokamai.

### Ar Azure DI confidence apskritai kalibruotas?

Microsoft apibrėžia confidence kaip kalibravimo teiginį: 0,95 reiškia „prognozė teisinga apie 19 kartų iš 20" ([Accuracy and confidence scores](https://github.com/MicrosoftDocs/azure-ai-docs/blob/main/articles/ai-services/document-intelligence/concept/accuracy-confidence.md)). Gairės yra bendros („naudokite confidence sprendimui auto-accept vs. human review") **be jokio konkretaus skaitinio slenksčio**. **Jokio nepriklausomo, kiekybinio Azure DI confidence kalibravimo audito viešoje literatūroje nerasta** — tai atrodo tikra spraga, ne paieškos nesėkmė.

Jūsų pačių duomenys tai kelia į klausimą. Pasiskirstymas per 247 sąskaitas: **20 ties 95–100, 150 ties 85–94, 69 ties 70–84, 8 žemiau 70.** Tai reiškia, kad 88 % dokumentų sėdi 70–94 juostoje, kur jokio natūralaus lūžio nėra — slenkstis toje zonoje yra savavališkas. Pramonės komentaras apie confidence balus bendrai yra skeptiškas: „the scores are not representative of probabilities so an 80 score does not mean '80 percent correct'" ([Parascript](https://www.parascript.com/blog/your-ocr-confidence-scores/)), o artimas analogas (LLM logprobs ExtractConf'e) pasirodė prastai kalibruotas neapdorotas (ECE 0,245) ir po izotoninės ar Lasso rekalibracijos ECE krito 71–86 %.

Praktinė rekomendacija: **netikėkite 0,95 = 95 % pažodžiui**, o empiriškai patikrinkite ant savo ~60 rankiniu būdu patikrintų sąskaitų — suskirstykite confidence į 4–5 stambias juostas ir palyginkite su tikru teisingumo dažniu juostoje. Jei kalibruosite, **naudokite Platt scaling arba temperature scaling, ne izotoninę regresiją**: Niculescu-Mizil & Caruana rezultatas yra tiesmukas — „When the calibration set is small (less than about 200–1000 cases), Platt Scaling outperforms Isotonic Regression with all nine learning methods", ir izotoninė persimoko būtent todėl, kad ji mažiau apribota ([Predicting Good Probabilities With Supervised Learning](https://www.cs.cornell.edu/~alexn/papers/calibration.icml05.crc.rev3.pdf)). Prie n≈60 paprastas binning'as apskritai gali būti tinkamiausias — skaidriausias ir nereikalaujantis jokios bibliotekos.

---

## 6. Peržiūros procesas — kaip padaryti, kad buhalterė naudotų

Trumpas atsakymas: **niekaip.** Jei vėliavėlė yra atmetama, ji bus atmetama. Vienintelis dizainas, kurio negalima „išvarginti", yra toks, kurio negalima apeiti.

### Įrodymai, kodėl vėliavėlių mažinimas neveikia

2024 m. sisteminė apžvalga ir meta-analizė, sujungusi **11 tyrimų ir 570 776 receptus**, nustatė bendrą DDI įspėjimų ignoravimo dažnį **90 % (95 % PI 85,6–95,0 %, p<0,0001)**; atskirų įstaigų reikšmės svyravo nuo 65,3 % (Pietų Korėja, kur valstybinis apmokėjimo ribojimas pridėjo trinties) iki **97,9 %** ([Felisberto et al. 2024](https://journals.sagepub.com/doi/10.1177/14604582241263242)).

Lemiamas skaičius yra šis: **tame pačiame darbe aprašytas atvejis, kur intervencija sumažino įspėjimų generavimą nuo 8 % iki 2 % užsakymų, o ignoravimo dažnis pajudėjo tik nuo 97,9 % iki 96 %.** Keturgubas tūrio sumažinimas davė 1,9 procentinio punkto. Kai nepasitikėjimas kanalu jau susiformavęs, tūrio mažinimas jo neatkuria.

2026 m. apžvalga, apibendrinusi 22 sistemines apžvalgas, prideda antrą svarbų dalyką: **tik vienas įtrauktas tyrimas iš visų 22 apžvalgų turėjo operacinį alert fatigue apibrėžimą**, ir „no validated benchmarks currently exist for determining acceptable alert volume or override rates" ([PMC13385993](https://pmc.ncbi.nlm.nih.gov/articles/PMC13385993/)). AHRQ formuluoja mechanizmą tiksliai: perteklinis ir/arba nereikšmingas įspėjimų srautas veda prie jų ignoravimo „regardless of their relevance" — žmogus nustoja vertinti įspėjimus po vieną ir pradeda šabloniškai atmesti visą kanalą ([AHRQ PSNet](https://psnet.ahrq.gov/primer/alert-fatigue)).

Jūsų buhalterės elgesys — įkelti paketą, pamatyti daug vėliavėlių, neišspręsti nė vienos, palikti duomenis gyvus — yra vadovėlinis šio šablono atvejis. **Tai nėra jos problema, kurią galima išspręsti mokymu.**

### Dviejų pakopų dizainas: kietas vs. tylus

Rossum savo dokumentacijoje eksplicitiškai skiria dvi klases. Kai validacija aptinka neatitikimą — konkrečiai įvardija „an incorrect PO, quantity or VAT code" — sistema **„stops this data getting into your ERP"**, t. y. kietas blokas prie išsaugojimo/eksporto, ne vėliavėlė ([Rossum, Invoice Exception Handling](https://rossum.ai/blog/invoice-exception-handling-features/)). Kitai problemų klasei (laiškams be apdorojamo dokumento) jie naudoja **minkštą, pačią užsidarančią** žymą: „flagged with a red alert badge which disappears automatically once the user opens the problematic email". Rossum taip pat sako atvirai, kad net prie jų automatizavimo lygio „There will always be a need for a human in the loop". *Įspėjimas: buvo gauta tik Rossum vieša medžiaga — Tipalti, Stampli, Basware, Coupa ir Medius atskirai netikrinti, todėl teiginys, kad „brandūs AP tiekėjai" apskritai laikosi šio šablono, remiasi vienu tiekėju.*

### Konkretūs kieti vartai

Keturi, ir tiek. Kiekvienas iš jų atitinka realų incidentą jūsų duomenyse.

| # | Vartai | Sąlyga | Kokį incidentą būtų sustabdę |
|---|---|---|---|
| 1 | **Aritmetikos** | Eilutės + PVM nesusiveda su antrašte per 0,01 toleranciją (BR-CO-10/13/15) | „Suma su PVM" vietoj „Suma be PVM"; ~70 AMOUNT_MISMATCH |
| 2 | **Dublikato** | Tas pats tiekėjas + sąskaitos nr. jau yra, **arba** tas pats tiekėjas + suma + data per langą | **25 grupės, 27 pertekliniai įrašai, 28 679,74 €** |
| 3 | **Tiekėjo** | Nepavyko priskirti tiekėjo — įrašo negalima išsaugoti su „tiekėjas: nėra" | 141 VENDOR_NOT_FOUND |
| 4 | **Datos sveikatos** | Sąskaitos data ateityje arba neįtikėtinai sena įkėlimo atžvilgiu | 2 ateities datos sąskaitos |

Viskas kita — vidutinis vienos eilutės confidence, neįprasta suma, nematytas produkto aprašymas — **privalo būti nematoma arba pačiai užsidaranti**. Ne dialogas, ne patvirtinimas, ne vėliavėlė sąraše. Daugiausia gintaro spalvos lauko paryškinimas, kurį galima ignoruoti nesustojant. Logika paprasta: jei minkšti įspėjimai vis tiek ignoruojami 80–98 % atvejų nepriklausomai nuo pagrįstumo, tai bet kas, ko negalima leisti į produkciją, turi būti **neišsaugomas**, o ne pažymimas.

Priešingas argumentas, kurį verta pasakyti garsiai: kieti vartai gali užblokuoti teisėtą sąskaitą ir tada buhalterė nedirbs. Todėl kiekvieni vartai privalo turėti **eksplicitų, užfiksuojamą apėjimą su priežastimi** („dublikatas, bet tai tikrai kreditinė korekcija — patvirtinu"), kuris patenka į audito žurnalą. Skirtumas nuo vėliavėlės yra tas, kad apėjimas reikalauja sąmoningo veiksmo ir palieka pėdsaką, o vėliavėlės ignoravimas nereikalauja nieko ir nepalieka nieko.

### Realistiškas automatizavimo lygis

Nepriklausomi Ardent Partners benchmark'ai rodo: **vidutinė organizacija — 9,84 USD sąskaitai, 8,2 dienos apdorojimo laikas, 18,4 % išimčių (exception) dažnis**; „Best-in-Class" (geriausi 20 % pagal kainą ir ciklo trukmę) turi 79 % mažesnę kainą, 47 % mažesnį išimčių dažnį ir **1,8× didesnį STP tūrį** ([Payables Place](https://payablesplace.ardentpartners.com/2026/01/state-of-epayables-part-nine-ap-benchmarks-and-best-in-class-performance/)). Atkreipkite dėmesį: 1,8× yra **santykinis daugiklis, ne absoliutus STP procentas** — absoliutaus Best-in-Class STP skaičiaus laisvai prieinamoje medžiagoje nerasta, todėl jo cituoti negalima. Rossum „up to 90 % touchless" yra tiekėjo lubų teiginys, ne išmatuotas vidurkis.

Sąžiningas tikslas jums: **ne „90 % touchless", o „ne blogiau už pramonės vidutinį ~18 % išimčių dažnį, su išimtimis, kurios yra saugios (kietai užblokuotos), o ne tyliai neteisingos".**

---

## 7. Kaip išmatuoti, kad veikia

### Golden-file regresija ant išsaugoto žalio JSON

Jūsų architektūra — žalias Azure JSON saugomas visam laikui, ekstrakcijos logika paleidžiama iš naujo offline prieš saugotą JSON — yra tiksliai tai, kam skirtas golden-file / approval / snapshot testavimas, ir .NET turi pirmos klasės įrankius. [Verify](https://github.com/VerifyTests/Verify) serializuoja testo rezultatą į failą pagal testo vardą, pirmą kartą sukuria „received" failą, kurį reikia priimti į „verified", o vėliau diff'ina ir krenta su vizualiu skirtumu; palaiko xUnit, .NET Framework 4.6.2 iki .NET 10. Alternatyva — [ApprovalTests.Net](https://github.com/approvals/ApprovalTests.Net).

Esminė detalė: **snapshot turi būti ištrauktas ir normalizuotas laukų rinkinys, ne žalias OCR** — tada testo kritimas reiškia „mano ekstrakcijos/normalizacijos logika pakeitė elgseną ant realios sąskaitos", o tai ir yra norimas signalas.

40 dev + 20 hold-out realių sąskaitų yra pagrįstas **startas**, bet tai užfiksuoja elgseną tik jau matytiems tiekėjams ir formatams. Naujo tiekėjo ar naujo šablono fiksuotas korpusas neapsaugos — tai struktūrinė riba, ne dydžio problema. Todėl reikalingas eksplicitus mechanizmas: **kiekvieną kartą, kai produkcijoje pasirodo tikrai naujas tiekėjas ar pastebimai kitoks šablonas, į hold-out aibę pridedama viena reali sąskaita iš jo.** Ir kasmet dev aibę reikia peržiūrėti iš naujo — klasikinė approval-testing rizika yra ta, kad neteisinga ekstrakcija vieną kartą „patvirtinama" ir tyliai užsifiksuoja kaip naujas teisingas atsakymas. *„Ar 60 dokumentų užtenka" nėra cituojamas pramonės skaičius — tai išvada iš pirmų principų.*

### Trijų taisyklė ir ką imtis sąžiningai įrodo

Svarbiausias skaičius yra ne confidence, o **tylių klaidų dažnis** — klaidos, kurios praeina visus patikrinimus ir pasiekia knygas nepastebėtos. Prie 420 dokumentų per metus jo tiksliai išmatuoti negalima, bet binominė trijų taisyklė duoda sąžiningą protokolą: jei imtyje dydžio *n* rasta **nulis** klaidų, intervalas **[0, 3/n]** yra 95 % pasikliovimo intervalas tikrajam dažniui; aproksimacija patikima nuo maždaug n>30 ([Rule of three, Wikipedia](https://en.wikipedia.org/wiki/Rule_of_three_(statistics))).

| Audituota imtis, 0 klaidų | Ką galima sąžiningai pasakyti |
|---|---|
| n = 60 | „Tikrasis tylių klaidų dažnis < **5 %** su 95 % pasikliovimu" |
| n = 150 | „< **2 %**" |
| n = 300 | „< **1 %**" |

Ir dabar nemaloni dalis: **n=300 yra beveik visas jūsų metinis tūris (420).** Tai reiškia, kad **per vienerius metus jūs negalite pigiai įrodyti mažesnio nei 1 % tylių klaidų dažnio.** Galite arba priimti platesnį (bet vis tiek naudingą) rėžį „<5 % su 95 % pasikliovimu" iš mažesnės imties, arba kaupti auditą per kelerius metus. Praktinis protokolas: kas ketvirtį atsitiktinė 15–20 sąskaitų imtis iš auto-priimtų (nekoreguotų) dokumentų, rankinis kiekvieno lauko tikrinimas prieš PDF, ir pasikliovimo rėžis skelbiamas **už slenkantį 12–24 mėn. langą**, ne už ketvirtį. Kiekviena rasta klaida iš karto duoda du veiksmus: įrašą į regresijos korpusą ir root-cause peržiūrą, kodėl kieti vartai jos nepagavo. *Šis protokolas yra šios ataskaitos vedinys, sudėtas iš trijų taisyklės ir bendros confidence balų kritikos — nerasta nė vieno šaltinio, kuris jį aprašytų kaip visumą dokumentų ekstrakcijai.*

### Savaitinė suvestinė — penki skaičiai

Realaus laiko įspėjimai šio dydžio sistemai yra neteisingas režimas: esant mažam signalo tankiui, jie ignoruojami lygiai taip pat patikimai, kaip ir esant dideliam tūriui, nes didžiąją dalį savaičių nieko nebūna. Viena savaitinė suvestinė yra tinkama kadencija.

| # | Skaičius | Kodėl |
|---|---|---|
| 1 | Apdorota sąskaitų / **% auto-priimtų be nė vieno žmogaus redagavimo** | Tai jūsų coverage — paprastas produkcijos skaitiklis, ne imtis |
| 2 | **Kietų vartų suveikimų skaičius, išskaidytas pagal vartus** (aritmetika / dublikatas / tiekėjas / data) | Ne nulis čia yra svarbiausia eilutė visoje suvestinėje |
| 3 | **Sąskaitų, neišspręstų ilgiau nei N dienų, skaičius** | Būtent ši eilutė būtų pagavusi 247 vėliavėlių incidentą per savaitę, o ne po metų |
| 4 | **Laukų korekcijos dažnis** (kiek laukų buhalterė taisė iš ištrauktų) | Nemokamas ekstrakcijos kokybės pakaitalas, skaičiuojamas iš audito žurnalo |
| 5 | **Slenkantis 12 mėn. tylių klaidų pasikliovimo rėžis** | Atnaujinamas ketvirtiniu, rodomas kas savaitę — degradacija matoma anksti |

### Kodėl „vidutinis confidence" yra bevertė metrika

Trys priežastys, kiekviena savarankiškai pakankama. Pirma, balai nėra tikimybės — „an 80 score does not mean '80 percent correct'", ir tie patys balai skirtinguose dokumentuose reiškia skirtingą realų tikslumą ([Parascript](https://www.parascript.com/blog/your-ocr-confidence-scores/)). Antra, vidurkis suploja būtent tą uodegą, kuri jums rūpi: jūsų 8 dokumentai žemiau 70 dingsta 150 dokumentų 85–94 juostoje, o vidurkis atrodo puikiai. Trečia, ir svarbiausia — **jūsų brangiausias gedimas neturėjo nieko bendro su confidence**: VendorTaxId turėjo 0,836, SubTotal 0,95, ir vis tiek eilutės buvo katastrofiškai neteisingos, o 28 679,74 € atsirado iš dublikatų, kuriems confidence apskritai netaikomas. Metrika, kuri būtų rodžiusi žalią lemputę per visą incidentą, nėra metrika.

---

## 8. Kaina

Visi skaičiavimai: 35 sąskaitos/mėn., ~2 psl., 840 psl./metus, 420 dokumentų/metus.

| Architektūra | Metinė kaina | Pastaba |
|---|---|---|
| (a) `prebuilt-invoice` vienas | **~8,40 USD** | Pigiausia ir, pagal jūsų pačių įrodymus, nepatikimiausia eilutėms |
| (b) `prebuilt-layout` + 1 LLM kvietimas (gpt-4.1) | **~14,10 USD** | DI 8,40 + LLM ~5,70 |
| (b') tas pats su gpt-4.1-mini | **~9,50 USD** | |
| (c) `prebuilt-layout` + **dual LLM** (gpt-4.1) | **~19,70 USD** | Rekomenduojama architektūra |
| (c') tas pats su gpt-4.1-mini | **~10,60 USD** | |
| (d) Azure AI Content Understanding | **~7–10 USD** | **Silpniausiai pagrįstas skaičius visame skyriuje** |

Vieneto kainos, kuriomis remiasi lentelė: DI `prebuilt-invoice` ir `prebuilt-layout` po **10,00 USD/1000 psl.**, `prebuilt-read` 1,50 USD/1000 psl. ([DocuOCR](https://docuocr.com/blog/azure-document-intelligence-pricing), [StarNova AI](https://starnovai.com/azure-ai-document-intelligence-pricing)); Azure OpenAI Global tier — gpt-4.1 2,00/8,00 USD už 1M tokenų, gpt-4.1-mini 0,40/1,60 USD ([PricePerToken](https://pricepertoken.com/endpoints/azure)). Token prielaida: ~3 500 įvesties / 800 išvesties tokenų vienai 2 psl. sąskaitai — **tai inžinerinis įvertis, ne išmatuotas skaičius**, kurį reikia patikrinti prieš savo realias sąskaitas.

**Svarbiausia šio skyriaus žinia: skirtumas tarp pigiausios ir brangiausios Azure architektūros yra ~12 USD per metus.** Kaina negali būti sprendimo kriterijus. Nėra jokios finansinės priežasties taupyti atsisakant dual-call arba nesiunčiant puslapio vaizdo — ribinė „daryti patikimiau" kaina yra centai per metus.

Palyginimui, komercinės alternatyvos prie 420 dok./metus:

| Tiekėjas | Kaina | Verdiktas |
|---|---|---|
| **Rossum** | **18 000 USD/metus minimalus įsipareigojimas**, 1 m. minimalus terminas; Starter yra „the floor of the range, not the middle" ([Floowed](https://www.floowed.com/insights/rossum-pricing)) | **~43 USD už dokumentą.** Atmetama vien dėl kainos |
| **Veryfi** | **Nemokamas planas 0–100 operacijų/mėn.** įskaitant sąskaitas su eilutėmis ([Veryfi FAQ](https://faq.veryfi.com/en/articles/3743986-what-are-the-plans-prices-for-ocr-api)) | Jūs telpate visiškai nemokamai. **Verta 1 dienos testo** |
| **Mindee** | 44 USD/mėn. (529 USD/metus) su 6 000 kreditų/mėn. | Naudotumėte <2 % pajėgumo — perteklinis, bet pigus |
| **Google Document AI Invoice Parser** | 10,00 USD/1000 psl. → ~8,40 USD/metus ([pricing](https://cloud.google.com/document-ai/pricing)) | Tos pačios eilės kaip Azure |
| **AWS Textract AnalyzeExpense** | 0,01 USD/psl. → ~8,40 USD/metus ([AWS](https://aws.amazon.com/textract/pricing/)) | Tos pačios eilės; **ES regiono kainos nepatvirtintos** |
| **Klippa** | **Kainos nerasta** | Tikra spraga, ne išvada |

Kritinis įspėjimas, kurį būtina pasakyti: **nė vieno iš šešių komercinių tiekėjų LT/DE/RO/LV/EE/UA kalbų palaikymas šiame tyrime nebuvo patikrintas.** Prieš skiriant laiką Veryfi ar Mindee testui, jų dokumentacija turi būti patikrinta specifiškai dėl baltiškų kalbų ir ukrainiečių kalbos — tai jums kietas reikalavimas, o nė vienas gautas kainodaros puslapis to neliečia.

---

## 9. Sąžininga išvada apie verslo prasmę

Čia reikia nesigražinti.

**Darbo sąnaudų taupymas neatperka šios sistemos.** Rankinio apdorojimo etalonai: ~15 min. sąskaitai (duomenų suvedimas, patikra, tvirtinimo maršrutas, segtuvas), t. y. **~5 sąskaitos per valandą**; automatizuotas — po 5 min. ([ResolvePay, cituojant IOFM ir Levvel Research](https://resolvepay.com/blog/13-statistics-that-quantify-cost-per-invoice-in-manual-vs-automated-flows)). Kaina sąskaitai: **rankinis 15–16 USD, automatizuotas 3–5 USD** — iki 80 % sumažėjimas.

Skaičiuokime jūsų tūriu. 35 × 15 min. = **~8,75 valandos per mėnesį** rankinio darbo. Tai nėra krizė — tai vienos darbo dienos užduotis vienam žmogui. Taupymas: (15,50 − 4,00) × 35 ≈ **402 USD/mėn. ≈ 4 830 USD/metus** teorinės darbo sąnaudų naudos. Solo developeriui, kuris šią sistemą stato, derina ir dabar dar taiso 247 vėliavėlių incidentą, **pirmieji metai beveik tikrai yra už lūžio taško** — kiekviena valanda, praleista šią sistemą taisant, valgo tą 4 830 USD.

Ir dabar palyginimas, kuris viską nusako: **vienas dublikatų incidentas kainavo 28 679,74 € — maždaug 6–7 kartus daugiau nei visa metinė teorinė darbo sąnaudų nauda.**

Iš to seka konkreti išvada apie tai, kas šioje sistemoje iš tikrųjų vertinga. **Vertingiausia funkcija nėra greitesnis atpažinimas. Vertingiausia funkcija yra kietas dublikatų vartas.** Ekonominis pagrindas šiai sistemai yra:

- **Dublikatų ir sukčiavimo prevencija** — vienintelis dydis, kuris duomenyse jau pasirodė penkiaženkliu skaičiumi;
- **Audito pėdsakas** — nekeičiamas žurnalas „laukas X pakeistas iš A į B, vartotojas U, laikas T", kuris turi dvigubą naudą: apskaitinis gynybiškumas per 10 metų saugojimo langą ir nemokamas žymėtų klaidų rinkinys matavimui;
- **Dokumentų išlikimas ir paieška** — turinio adresuojama PDF saugykla plius nuolatinė žalio JSON saugykla, kuri struktūriškai tenkina „saugok originalą" reikalavimą ir daro pakartotinį apdorojimą saugiu ir mechanišku.

Lietuvoje apskaitos dokumentai — eksplicitiškai įvardijant „PVM sąskaitos faktūros, sąskaitos, banko išrašai, kasos dokumentai, važtaraščiai" — saugotini **10 metų**, o elektroniniu parašu pasirašyti dokumentai privalo būti saugomi originaliu elektroniniu formatu, užtikrinant e. parašo galiojimą ir failo vientisumą visą terminą; spausdintos kopijos neturi teisinės galios kaip pakaitalai ([instrukcija.eu](https://instrukcija.eu/kiek-laiko-saugoti-imones-dokumentus-2026/)). **Šis 10 metų skaičius gautas tik iš antrinio atitikties šaltinio** — VMI komentaro puslapis grąžino 403, o e-Seimas įrašas buvo tik metaduomenys. Prieš remiantis tuo kaip teisine pozicija, patikrinkite su buhaltere arba teisininku. Taip pat liko neatsakyta, ar **popierinio originalo skenas savaime pakankamas** (t. y. ar popierių galima sunaikinti) — šaltiniai aiškiai kalba tik apie elektroniniu parašu pasirašytus dokumentus.

Tiesmukai: šią sistemą verta turėti ne todėl, kad ji taupo buhalterės valandas, o todėl, kad **verslas su vienu ne technišku peržiūrėtoju neturi jokio kito saugiklio nuo būtent tokios tylios, besikaupiančios klaidos, kuri produkcijoje jau įvyko kartą.**

---

## 10. Rekomenduojamas planas

Surikiuota pagal (vertė / pastangos). Skaičiai — grubūs darbo valandų įverčiai.

**Etapas 0 — sustabdyti kraujavimą (šią savaitę, ~10 val.)**

1. **Perklasifikuoti MISSING_DUE_DATE iš klaidos į informaciją** (<1 val.). Mokėjimo terminas nėra 226 str. privalomas laukas. Vienu ypu dingsta ~74 iš 372 vėliavėlių. Didžiausias triukšmo sumažinimas už mažiausią darbą visoje sistemoje.
2. **Failo turinio SHA-256 dedupas prieš OCR** (1–2 val.). Nulis klaidingų teigiamų, pagauna trivialų atvejį.
3. **Kieti vartai #2 (dublikatas) ir #4 (ateities data)** (3–4 val.). Šie du tiesiogiai atitinka 28 679,74 € ir dvi ateities datas. Su privalomu, žurnalizuojamu apėjimu.
4. **Išvalyti esamus duomenis**: 25 dublikatų grupės, 27 pertekliniai įrašai, 2 ateities datos (3–4 val., daugiausia rankinis darbas su buhaltere).

**Etapas 1 — deterministinė bazė (2–3 savaitės, ~25 val.)**

5. **EN 16931 BR-CO-10/13/15/16 kaip C# taisyklių modulis su taisyklių ID klaidų pranešimuose** (8–12 val.). Tiesiogiai taikosi į ~70 AMOUNT_MISMATCH ir į „Suma su PVM" klaidos klasę. Tai kietas vartas #1.
6. **Lokalės skaičių disambiguavimas per aritmetinę kryžminę patikrą** (6–10 val.). Būtent tai pagautų „3 888,000" → 3. Bandykite abi interpretacijas, priimkite tą, kuri susiveda.
7. **IBAN mod-97** (2–3 val.). Pilnai specifikuota, nemokama nauda.
8. **PVM tarifų baltasis sąrašas pagal šalį ir datą** (3–4 val.). RO 21 %/11 % nuo 2025-08-01 yra konkretus, iš karto veikiantis atvejis.

**Etapas 2 — tiekėjo kaskada (2–3 savaitės, ~20 val.)**

9. **Kaskados pakopos 1–5 su normalizacija** (12–16 val.) — PVM ID, įmonės kodas, IBAN prieš žinomą aibę, normalizuotas pavadinimas, FuzzySharp Token Sort ≥95. Tai didžiausia kategorija (141) ir tiesioginis dublikatų dedupo prielaida.
10. **Kietas vartas #3 (tiekėjo)** (2 val.) — įrašo negalima išsaugoti be tiekėjo.
11. **Išmoktų alias lentelė su ≥2–3 patvirtinimų promocija ir audito pėdsaku** (4–6 val.).

**Etapas 3 — ekstrakcijos perrašymas (3–4 savaitės, ~35 val.)**

12. **Perjungti į `prebuilt-layout` + savo `tables[]` serializacija + puslapio vaizdas + Structured Outputs** (16–20 val.).
13. **Antras asimetriškas kvietimas (Mapper) ir nesutarimo vėliava** (8–10 val.). Ribinė kaina ~10 USD/metus.
14. **ZERO_VAT antros pakopos patikra** — privalomos atvirkštinio apmokestinimo formuluotės paieška ištrauktame tekste (5–7 val.). Taikosi į ~47 vėliavėles, kurios „niekada neužsidaro".

**Etapas 4 — matavimas (nuolatinis, ~20 val. pradinių)**

15. **Golden-file regresijos rinkinys per Verify + xUnit ant saugoto žalio JSON** (10–14 val.), 40 dev + 20 hold-out, su taisykle pridėti po vieną sąskaitą kiekvienam naujam tiekėjui/šablonui.
16. **Savaitinė suvestinė su penkiais skaičiais** (4–6 val.).
17. **Ketvirtinis 15–20 sąskaitų atsitiktinis auditas** — nuolatinė ~2 val./ketvirtį procedūra.

**Ko NEDARYTI dabar:** conformal prediction implementacijos (5 skyrius — per maža imtis, exchangeability lūžta būtent ten, kur jūsų rizika); CatBoost ar bet kokio apmokyto confidence modelio (OCR-only požymiai duoda 0,896 prieš 0,928 — trys punktai neverti ML pipeline'o solo developeriui); izotoninės regresijos (persimoks žemiau 200–1000 taškų); pilno Schematron/XSLT pipeline'o su sintetiniu UBL (~955 taisyklės, iš kurių jums reikia 6); blocking'o tiekėjų paieškai (40 000 palyginimų yra milisekundės).

**Vienas pigus lygiagretus eksperimentas:** kadangi Veryfi nemokamas planas (≤100 dok./mėn.) jūsų tūrį padengia visiškai, **vienos dienos testas ant 10–15 realių LT/DE/RO/LV/EE/UA sąskaitų kainuoja nulį** ir gali arba duoti sveiko proto bazinę liniją jūsų pipeline'ui, arba — jei jis gerai tvarko europietiškus skaičių formatus — reikšmingai sumažinti inžinerijos apimtį. Prieš tai patikrinkite jų dokumentaciją dėl kalbų palaikymo, nes šiame tyrime tai nebuvo patvirtinta.

---

## 11. Ko nepavyko patikrinti

Šis sąrašas yra pilnas. Kiekvienas punktas čia yra spraga, į kurią reikia atsižvelgti prieš remiantis atitinkamu ataskaitos teiginiu.

**Kontrolinių sumų algoritmai**
- **Lietuvos įmonės kodo / PVM kodo kontrolinės sumos svorių vektorius nerastas** po keturių tikslinių paieškų. Patvirtintas tik faktas, kad kontrolinis skaitmuo egzistuoja ([ambrazasp/lt-codes](https://github.com/ambrazasp/lt-codes) grąžina `INVALID_CONTROL_NUMBER`). Prieš implementuojant — patikrinti prieš Registrų centro specifikaciją arba atvirojo kodo implementacijos šaltinį.
- **Vokietijos USt-IdNr kontrolinės sumos egzistavimas nepatvirtintas** nė viename gautame šaltinyje.
- **Ukrainos EDRPOU ir ІПН kontrolinių sumų algoritmai nerasti**; taxid.pro eksplicitiškai nurodo, kad metodologija neteikiama. Ukraina taip pat už VIES ribų — gyvo patikrinimo alternatyvos nėra.
- Latvijos, Estijos ir Rumunijos PVM numerių kontrolinės sumos taip pat nerastos.
- **Ukrainos IBAN statusas ir formatas (29 simboliai) teigtas iš bendrų žinių**, nepatikrintas pirminiu šaltiniu.

**Teisė ir terminai**
- **LT B2B e-sąskaitų „2025 m. liepa: XML pagal pareikalavimą" ir „2027: privaloma visiems PVM mokėtojams" datos NEPATIKRINTOS.** Vienintelis sėkmingai gautas LT e-invoicing šaltinis ([ecosio.com](https://ecosio.com/en/compliance/lithuania/e-invoicing/)) eksplicitiškai nurodė, kad tokios informacijos neturi. Nenaudokite šių datų kaip patvirtintų.
- **10 metų saugojimo terminas gautas tik iš antrinio atitikties šaltinio** (instrukcija.eu). VMI komentaro puslapis grąžino 403; e-Seimas įrašas — tik metaduomenys. Tikslus galiojantis straipsnio numeris nepatvirtintas.
- **Neatsakyta, ar popierinio originalo skenas savaime pakankamas** (ar popierių galima sunaikinti) — šaltiniai kalba tik apie elektroniniu parašu pasirašytus dokumentus.
- **BR-S-08 tikslaus teksto/formulės iš pirminio šaltinio gauti nepavyko.**
- **Tikslios lietuviškos atvirkštinio apmokestinimo formuluotės praktinis vartojimas nepatikrintas** („Atvirkštinis apmokestinimas" — prielaida). Latvijos, Estijos ir Ukrainos atitikmenys taip pat nepatikrinti.
- **Rumunijos, Latvijos, Estijos, Ukrainos datų ir skaičių formatų konvencijos nepatikrintos** prieš cituojamą autoritetingą šaltinį — CLDR fetch'ai nepavyko.

**Kainodara**
- **Azure pirminiai kainodaros puslapiai neprieinami** šioje sesijoje (provenance apribojimai ir/arba kliente renderinamos kainų lentelės). **Visos DI, Azure OpenAI ir Content Understanding vieneto kainos ataskaitoje ateina iš trečiųjų šalių sekiklių**, ir du šaltiniai nesutarė dėl custom modelio kainos (30 vs 50 USD/1000 psl.). Prieš biudžetą — patikrinti tiesiai `azure.microsoft.com/pricing/details/...` arba Azure Pricing Calculator, filtruojant West Europe/Sweden Central.
- **Content Understanding kaina už puslapį yra silpniausiai pagrįstas skaičius visoje ataskaitoje** — vienas trečiosios šalies palyginamasis straipsnis.
- **Klippa kainodara nerasta apskritai.**
- **AWS Textract ir Google Document AI ES regiono kainos nepatvirtintos** (rastos JAV regionų kainos).
- **GPT-5 šeimos (gpt-5-mini) token kainos nepatvirtintos**, nors gpt-5-mini palaiko Structured Outputs.
- Token prielaida (3 500 įvesties / 800 išvesties 2 psl. sąskaitai) yra inžinerinis įvertis, ne matavimas.

**Tiekėjai ir registrai**
- **Nė vieno komercinio tiekėjo LT/DE/RO/LV/EE/UA kalbų palaikymas nepatikrintas** — nei Rossum, nei Klippa, nei Mindee, nei Veryfi, nei Google, nei AWS.
- **Registrų centro gyvas per-užklausą REST API nepatvirtintas** — rasti tik bulk duomenų rinkiniai. Tai tiesiogiai lemia, ar LT tiekėjus (dauguma iš ~200) galima praturtinti realiu laiku, ar tik per periodiškai atnaujinamą lokalų veidrodį.
- **rekvizitai.lt sąlygos ir API nepatikrintos** — oficialios dokumentacijos nerasta, tik neoficiali trečiosios šalies API ir diskusija apie scraping'o teisėtumą.
- **Latvijos ur.gov.lv API kaina nenurodyta**; **Ukrainos Opendatabot kaina neatskleista** — nė vieno negalima laikyti nemokamu.
- **Finvalda, Centas, Agnum, Directo, Merit Aktiva, Standard Books tiekėjo sutapatinimo raktai nepatikrinti** — tik Rivile Cloud kaskada dokumentuota viešai. Nepatvirtinta ir tai, ar Rivile GAMA naudoja tą pačią kaskadą kaip Rivile Cloud.
- **GLEIF/LEI žemas padengimas mažoms LT įmonėms yra išvada, ne cituota statistika.**

**Modeliai, matavimas, UI**
- **Jokio nepriklausomo Azure DI confidence kalibravimo audito nerasta** — nei reliability diagram, nei ECE viešame duomenų rinkinyje. Atrodo tikra literatūros spraga.
- Nerasta duomenų, kaip Azure DI confidence elgiasi ne angliškose / Vidurio Europos sąskaitose.
- **Peržiūros UI sekundžių/dokumentui skaičius nerastas** nė viename šaltinyje (Rossum, Klippa, Docsumo, HCI literatūra). Ataskaitoje nėra jokio pralaidumo skaičiaus peržiūros ekranui, ir neturi būti.
- **Absoliutus „Best-in-Class STP %" skaičius negautas** — tik santykinis 1,8× daugiklis. Pilna Ardent Partners ataskaita už lead-capture formos.
- **Nerasta mažoms įmonėms (<50 sąsk./mėn.) specifinių STP, išimčių dažnio ar rankinio apdorojimo skaičių** — visi etalonai iš vidutinių/didelių AP skyrių.
- **ResolvePay 15–16 USD / 3–5 USD skaičiai yra antrinis agregatorius**, cituojantis IOFM ir Levvel vardu, be tiesioginės nuorodos į pirminį tyrimą (IOFM puslapis už mokamos sienos).
- **Nerastas joks dokumentuotas produkcinis „tiekėjo alias auto-mokymosi" mechanizmas** jokioje ERP sistemoje; „2–3 patvirtinimai" yra pagrįstas parametras, ne geroji praktika.
- **Nerastas joks produkcinis dual-call (Hunter/Mapper) įgyvendinimas** gyvoje sistemoje — įrodymas tik akademinis, ir tai workshop straipsnis.
- **ExtractConf yra vieno autoriaus workshop straipsnis** (RobustifAI @ IJCAI-ECAI 2026), ne main track — skaičiai nepriklausomai nepakartoti. Taip pat nepatvirtinta, ar jo modeliai veikė ant Azure OpenAI.
- **Nerasta kiekybinio įvertinimo, kiek dokumento lygio koreliacija tarp laukų sumažina efektyvią conformal kalibravimo imtį** — tai išvada iš bendros teorijos.
- **Nerasta jokio benchmark'o, kuris lygintų Azure DI layout markdown su `tables[]` JSON** kaip LLM įvestį, nei baltiškoms kalboms skirto ekstrakcijos benchmark'o.
- **„Ar 60 dokumentų užtenka regresijos korpusui" nėra cituojamas skaičius** — pirmų principų samprotavimas.
- **Nerastas joks skaitinis alert-volume slenkstis**, virš kurio fatigue patikimai įsijungia — sisteminė apžvalga eksplicitiškai sako, kad tokių validuotų etalonų nėra.
- **Dviejų pakopų (kieti/minkšti) dizaino įrodymas remiasi tik Rossum** — Tipalti, Stampli, Basware, Coupa, Medius netikrinti.
- **Temperature scaling mažos imties elgsena nepatvirtinta atskirai** — išvesta iš to, kad tai apribotas Platt scaling atvejis.

---

## 12. Šaltiniai

**Ekstrakcijos architektūra (2 skyrius)**
- [Choosing the right Azure AI tool for document processing](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/choosing-right-ai-tool)
- [Extract and map information from unstructured content — Azure Architecture Center](https://learn.microsoft.com/en-us/azure/architecture/ai-ml/architecture/automate-document-processing-azure-ai-document-intelligence)
- [Automate PDF forms processing — Azure Architecture Center](https://learn.microsoft.com/en-us/azure/architecture/ai-ml/architecture/automate-pdf-forms-processing)
- [jamesmcroft/azure-ai-document-pipeline-sample (.NET)](https://github.com/jamesmcroft/azure-ai-document-pipeline-sample)
- [Azure-Samples/azure-ai-document-processing-samples](https://github.com/Azure-Samples/azure-ai-document-processing-samples)
- [Document Intelligence supported Markdown elements](https://learn.microsoft.com/en-us/azure/ai-services/document-intelligence/concept/markdown-elements?view=doc-intel-4.0.0)
- [Azure SDK for JS issue #29071 — Malformed tables in markdown outputs](https://github.com/Azure/azure-sdk-for-js/issues/29071)
- [Azure SDK for Python issue #36834 — markdown vs Studio table output](https://github.com/Azure/azure-sdk-for-python/issues/36834)
- [How to use structured outputs with Azure OpenAI](https://learn.microsoft.com/en-us/azure/foundry/openai/how-to/structured-outputs)
- [Multi-Modal Vision vs. Text-Based Parsing: Benchmarking LLM Strategies for Invoice Processing (arXiv:2509.04469)](https://arxiv.org/abs/2509.04469)

**Deterministinis tikrinimas (3 skyrius)**
- [ConnectingEurope/eInvoicing-EN16931 — oficialūs validacijos artefaktai](https://github.com/ConnectingEurope/eInvoicing-EN16931)
- [EN16931-UBL-validation-preprocessed.sch — BR-CO taisyklių pirminis šaltinis](https://github.com/ConnectingEurope/eInvoicing-EN16931/blob/master/ubl/schematron/preprocessed/EN16931-UBL-validation-preprocessed.sch)
- [Invoice Navigator — EN16931 validation rules guide](https://www.invoicenavigator.eu/blog/en16931-validation-rules-complete-guide)
- [speedata/einvoice/rules (Go referencinė implementacija)](https://pkg.go.dev/github.com/speedata/einvoice/rules)
- [jxsl13/einvoice/rules (Go)](https://pkg.go.dev/github.com/jxsl13/einvoice/rules)
- [dotnet/runtime issue #4762 — Decimal.Parse priima neteisingą grupavimą](https://github.com/dotnet/runtime/issues/4762)
- [Date and time notation in Europe (Wikipedia)](https://en.wikipedia.org/wiki/Date_and_time_notation_in_Europe)
- [IBAN Structure and Mod 97 Validation Algorithm](https://medium.com/@matlabb/iban-structure-and-mod-97-validation-algorithm-719e3d4db5f2)
- [ValidateFin — IBAN Format by Country](https://validatefin.com/en/blog/iban-format-by-country)
- [VAT identification number (Wikipedia)](https://en.wikipedia.org/wiki/VAT_identification_number)
- [ambrazasp/lt-codes](https://github.com/ambrazasp/lt-codes)
- [taxid.pro — Ukraine Tax ID guide](https://taxid.pro/docs/countries/ukraine)
- [VIES checkVatService WSDL (ec.europa.eu)](https://ec.europa.eu/taxation_customs/vies/checkVatService.wsdl)
- [VIESAC — What to do when VIES is unavailable](https://viesac.eu/articles/what-to-do-when-vies-unavailable)
- [viesapi.eu — VIES problems verifying DE companies](https://viesapi.eu/vies-problems-with-verifying-companies-from-germany-de/)
- [zapadi/vies-dotnet](https://github.com/zapadi/vies-dotnet)
- [VATupdate — EU VAT Directive Art. 226, content of an invoice](https://www.vatupdate.com/2022/05/12/eu-vat-directive-2006-112-ec-explained-art-226-content-of-an-invoice/)
- [VATupdate — CJEU C-247/21 Luxury Trust Automobil](https://www.vatupdate.com/2025/06/12/briefing-document-podcast-ecj-c-247-21-luxury-trust-automobil-gmbh-mandatory-invoice-requirements-for-triangulation-are-final-and-uncorrectable/)
- [VATupdate — Romania 21% / 11% VAT from August 2025](https://www.vatupdate.com/2025/07/08/romania-to-implement-new-vat-rates-standard-21-reduced-11-by-august-2025/)
- [Fiscal Solutions — GD 602/2025, Romania 11% rate](https://www.fiscal-requirements.com/news/4272)
- [Tax Foundation — 2026 VAT Rates in Europe](https://taxfoundation.org/data/all/eu/value-added-tax-vat-rates-europe/)
- [Stampli — How duplicate invoice detection works](https://www.stampli.com/resources/duplicate-invoice-detection/)
- [invoicedataextraction.com — Lithuania i.SAF requirements](https://invoicedataextraction.com/blog/lithuania-i-saf-requirements)
- [ecosio.com — E-invoicing compliance in Lithuania](https://ecosio.com/en/compliance/lithuania/e-invoicing/)

**Tiekėjo atpažinimas (4 skyrius)**
- [Rivile ERP guide — Dokumentų skaitmeninimas (OCR)](https://guide.rivile.cloud/integrative_solutions/ocr)
- [help.monet.lt — Sąskaitų skaitmenizavimas OCR](https://help.monet.lt/docs/darbozpradzia/pardavimai-9737/irasu-vedimas-11388/ocr/)
- [DNB Data Science Hub — Company Name Matching](https://medium.com/dnb-data-science-hub/company-name-matching-6a6330710334)
- [FuzzySharp (NuGet)](https://www.nuget.org/packages/FuzzySharp/) · [Raffinert/FuzzySharp (GitHub)](https://github.com/Raffinert/FuzzySharp)
- [F23.StringSimilarity (NuGet)](https://www.nuget.org/packages/F23.StringSimilarity)
- [Coleridge Initiative — Record Linkage (blocking)](https://textbook.coleridgeinitiative.org/chap-link.html)
- [FraudRoom — Fake Invoice "Bank Details Changed" Scam](https://fraudroom.com/blog/fake-invoice-bank-details-scam)
- [vetthisvendor.com — Supplier changed bank details](https://vetthisvendor.com/guide/supplier-changed-bank-details)
- [EZ Invoice Factoring — Factoring fraud red flags](https://www.ezinvoicefactoring.com/top-factoring-fraud-prevention-red-flags-in-invoice-verification/)
- [ANAF — doc_WS_V9.txt (PlatitorTvaRest v9 oficiali dokumentacija)](https://static.anaf.ro/static/10/Anaf/Informatii_R/Servicii_web/doc_WS_V9.txt)
- [Estijos e-Business Register — Introduction to API Services](https://avaandmed.ariregister.rik.ee/en/open-data-api/introduction-api-services)
- [GLEIF — LEI Data: Access & Use](https://www.gleif.org/en/lei-data/access-and-use-lei-data)
- [data.gov.lt — JAR įregistruoti juridiniai asmenys](https://data.gov.lt/dataset/lietuvos-respublikos-juridiniu-asmenu-registre-iregistruoti-juridiniai-asmenys/)
- [ur.gov.lv — Registry API Web Services list](https://www.ur.gov.lv/en/registry-api-web-services-service-list)
- [netzpolitik.org — Das Handelsregister: Endlich offene Daten](https://netzpolitik.org/2019/das-handelsregister-endlich-offene-daten/)
- [Opendatabot — API of state registers of Ukraine](https://opendatabot.ua/en/open/api)
- [vatverify.dev — The VIES API guide](https://www.vatverify.dev/guides/vies-api-guide)
- [Oracle EPM — Auto Accept and Exclude Thresholds for Matching Rules](https://docs.oracle.com/en/cloud/saas/readiness/epm/2024/epm-dec24/24dec-epm-wn-f36287.htm)

**Pasitikėjimo įvertinimas (5 skyrius)**
- [Know When to Abstain: Optimal Selective Classification (arXiv:2505.15008)](https://arxiv.org/html/2505.15008)
- [SelectiveNet (Geifman & El-Yaniv, ICML 2019)](http://proceedings.mlr.press/v97/geifman19a/geifman19a.pdf)
- [Overcoming Common Flaws in the Evaluation of Selective Classification Systems (NeurIPS 2024)](https://proceedings.neurips.cc/paper_files/paper/2024/file/047c84ec50bd8ea29349b996fc64af4b-Paper-Conference.pdf)
- [Rombach & Mehdiyev — Beyond Accuracy: KIE with Conformal Prediction, IJDAR 29(2), 551–563](https://link.springer.com/article/10.1007/s10032-026-00572-y)
- [Angelopoulos & Bates — A Gentle Introduction to Conformal Prediction (arXiv:2107.07511)](https://arxiv.org/pdf/2107.07511)
- [Kumar — Beyond Logprobs: ExtractConf (arXiv:2606.24420)](https://arxiv.org/abs/2606.24420) · [HTML](https://arxiv.org/html/2606.24420)
- [When LLMs Agree, Are They Right? (arXiv:2607.08065)](https://arxiv.org/html/2607.08065)
- [Niculescu-Mizil & Caruana — Predicting Good Probabilities With Supervised Learning (ICML 2005)](https://www.cs.cornell.edu/~alexn/papers/calibration.icml05.crc.rev3.pdf)
- [Microsoft — Accuracy and confidence scores (Document Intelligence)](https://github.com/MicrosoftDocs/azure-ai-docs/blob/main/articles/ai-services/document-intelligence/concept/accuracy-confidence.md)
- [Receiptor AI — AI Accuracy in Receipt Extraction (2026)](https://receiptor.ai/blog/ai-accuracy-in-receipt-extraction-is-it-ready-for-professional-use-2026)

**Peržiūra, matavimas, operacijos (6–7, 9 skyriai)**
- [Alert fatigue measurement in clinical decision support: a systematic review (PMC13385993)](https://pmc.ncbi.nlm.nih.gov/articles/PMC13385993/)
- [Felisberto et al. — Override rate of DDI alerts: systematic review and meta-analysis (2024)](https://journals.sagepub.com/doi/10.1177/14604582241263242)
- [AHRQ PSNet — Alert Fatigue](https://psnet.ahrq.gov/primer/alert-fatigue)
- [Rossum — Introducing Enhanced Invoice Exception Handling](https://rossum.ai/blog/invoice-exception-handling-features/)
- [Ardent Partners / Payables Place — State of ePayables: AP Benchmarks and Best-in-Class Performance](https://payablesplace.ardentpartners.com/2026/01/state-of-epayables-part-nine-ap-benchmarks-and-best-in-class-performance/)
- [Parascript — How Confident Are You in Your OCR Confidence Scores](https://www.parascript.com/blog/your-ocr-confidence-scores/)
- [Rule of three (statistics) — Wikipedia](https://en.wikipedia.org/wiki/Rule_of_three_(statistics))
- [VerifyTests/Verify (.NET snapshot testing)](https://github.com/VerifyTests/Verify)
- [approvals/ApprovalTests.Net](https://github.com/approvals/ApprovalTests.Net)
- [Docsumo — Review Screen documentation](https://support.docsumo.com/docs/review-screen)
- [ResolvePay — 13 Invoice Processing Stats: Manual vs. Automation](https://resolvepay.com/blog/13-statistics-that-quantify-cost-per-invoice-in-manual-vs-automated-flows)
- [instrukcija.eu — Kiek metų saugoti įmonės dokumentus](https://instrukcija.eu/kiek-laiko-saugoti-imones-dokumentus-2026/)

**Kaina (8 skyrius)**
- [DocuOCR — Azure AI Document Intelligence Pricing 2026](https://docuocr.com/blog/azure-document-intelligence-pricing)
- [StarNova AI — Azure AI Document Intelligence Pricing](https://starnovai.com/azure-ai-document-intelligence-pricing)
- [DocuOCR — Azure Content Understanding vs Document Intelligence](https://docuocr.com/azure-content-understanding-vs-document-intelligence)
- [Microsoft — Pricing for Azure Content Understanding](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/pricing-explainer)
- [PricePerToken — Azure endpoints](https://pricepertoken.com/endpoints/azure)
- [Floowed — Rossum Pricing in 2026](https://www.floowed.com/insights/rossum-pricing)
- [Veryfi — Plans & Prices FAQ](https://faq.veryfi.com/en/articles/3743986-what-are-the-plans-prices-for-ocr-api)
- [Mindee Pricing](https://www.mindee.com/pricing)
- [Google Document AI Pricing](https://cloud.google.com/document-ai/pricing)
- [AWS Textract Pricing](https://aws.amazon.com/textract/pricing/)
