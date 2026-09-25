# Atviri klausimai

Klausimai, laukiantys Deivido sprendimo. Uždarius — perkeliama į `DECISIONS.md`.

---

## Q-001 — Realus išlaidų sąskaitų kiekis per mėnesį

**Kodėl svarbu.** Nulemia, ar F2–F4 fazės proporcingos. Jei per mėnesį apdorojama
mažiau nei ~30 sąskaitų, pigiau būtų gera peržiūros forma su patikimu fiksavimu
(F0 + F1 + F5), o ne pilnas ekstraktoriaus perrašymas su agento ciklu.

**Kaip atsakyti.** Užklausa produkcijos DB per `mysql-prod` MCP (inventorizacijos
užduotis B8.3), skaičius per paskutinius 12 mėn. pagal mėnesį.

**Statusas:** atviras.

---

## Q-002 — Kur laikomas korpusas

**Kodėl svarbu.** `testdata/ocr-corpus/` turės realių tiekėjų sąskaitas su PVM
kodais, sumomis ir banko sąskaitomis. Į git jų dėti negalima.

**Variantai.**
- (a) Vietinis katalogas repo viduje + `.gitignore`, į git tik `corpus.lock.json`
- (b) NAS katalogas, prijungtas per konfige nurodytą kelią
- (c) Atskiras privatus repo

**Rekomendacija.** (a) — paprasčiausia, testai paleidžiami tik lokaliai.

**Statusas:** atviras.

---

## Q-003 — Kiek `expense_invoices` įrašų jau sugadinta

**Kodėl svarbu.** `PaidAmount = AmountInclVat` klaida veikia produkcijoje.
Reikia žinoti mastą prieš taisant kodą, ir ar reikia atskiro valymo skripto.

**Kaip atsakyti.** Produkcijos užklausa per `mysql-prod` MCP (inventorizacijos
užduotis B8.1 ir B8.2): sąskaitos, kur `paid_amount = amount_incl_vat`, bet
neturi atitinkamų `expense_payments` įrašų.

**Statusas:** atviras.

---

## Q-004 — Ar reikalinga admino rolė, ar užtenka esamos

**Kodėl svarbu.** Trečias vaizdas su žaliu Azure JSON turi būti prieinamas tik
administratoriui. Reikia žinoti, ar `ErpUser` jau turi rolių mechanizmą.

**Kaip atsakyti.** Inventorizacijos ataskaita (sesija 01).

**Statusas:** atviras.

---

## Q-005 — LT įmonės kodo ir PVM kodo kontrolinės sumos algoritmas

**Kodėl svarbu.** Kontrolinė suma leistų atmesti OCR klaidas tiekėjo kode be VIES
kvietimo. Tyrimas svorių vektoriaus nerado (`ambrazasp/lt-codes` patvirtina tik, kad
kontrolinis skaitmuo egzistuoja).

**Kaip atsakyti.** Pirminis VMI / Registrų centro šaltinis arba atvirojo kodo
validatoriaus išeities kodas.

**Statusas:** atviras. Žemas prioritetas — VIES jau duoda gyvą patikrą ES numeriams.

---

## Q-006 — LT B2B e-sąskaitų privalomumo datos

**Kodėl svarbu.** Tyrime minėtos datos (2025-07 XML pagal pareikalavimą, 2027 visiems
PVM mokėtojams) NEPATVIRTINTOS. Jei tiesa, dalis tiekėjų pateiks struktūrizuotus
duomenis ir OCR jiems nebereikės — tai keistų investicijos į Etapą 3 prasmę.

**Kaip atsakyti.** VMI pirminis šaltinis.

**Statusas:** atviras. Atsakyti prieš Etapą 3.

---

## Q-007 — Ar Veryfi nemokamas planas tinka mūsų sąskaitoms

**Kodėl svarbu.** ≤100 dok./mėn. padengia visą tūrį. Nepatvirtinta, ar tvarko
europietiškus skaičių formatus ir LT/DE/RO/LV/EE/UA sąskaitas.

**Kaip atsakyti.** Vienos dienos testas ant 10–15 realių sąskaitų. Prieš tai — jų
dokumentacija dėl kalbų.

**Statusas:** atviras. Atsakyti prieš Etapą 3.

---

## Q-008 — Azure kainos iš pirminio šaltinio

**Kodėl svarbu.** Tyrimo 8 skyriaus kainos paimtos iš trečiųjų šalių sekyklių; Azure
kainoraščiai tyrimo metu buvo nepasiekiami.

**Kaip atsakyti.** Azure Pricing Calculator, West Europe / Sweden Central.

**Statusas:** atviras. Sprendimui įtakos nedaro (skirtumas ~12 USD/metus), bet prieš
biudžetą patikrinti.

---

## Q-009 — Teisinio pagrindo formuluotės 0 % PVM sąskaitose

**Kodėl svarbu.** D-026 reikalauja formuluočių sąrašo pagal sandorio tipą (tiekimas ES
viduje, atvirkštinis apmokestinimas, išimtys) ir kalbą (LT/DE/LV/EE/PL/RO/UA).
Tyrimas LT atitikmens („Atvirkštinis apmokestinimas") praktinio vartojimo nepatvirtino.

**Kaip atsakyti.** Surinkti formuluotes iš naujai įkeliamų `ZERO_VAT` sąskaitų (senųjų
247 prodo PDF nebėra — `Docs/infra/SERVER-STATE.md` §1.3) arba iš popierinių / el. pašto
originalų, ir patikrinti su buhaltere.

**Statusas:** atviras. Atsakyti prieš Etapą 3.

---

## Q-010 — Ar 28 679,74 € realiai pateko į apskaitą

**Kodėl svarbu.** ERP viduje `DUPLICATE_PENDING` skaičiuojamas cash flow ir eksporte
(D-027). Bet ar buhalterė tas sąskaitas įtraukė į apskaitos programą — nežinoma.
Nuo to priklauso, ar tai ERP rodinių klaida, ar tikra apskaitos klaida su PVM
atskaitomybe (i.SAF).

**Kaip atsakyti.** Buhalterė patikrina 27 dublikatų sąrašą
(`analysis/PROD-DATA-FINDINGS-2026-09-25.md`) savo apskaitos programoje.

**Statusas:** atviras. Reikalingas prieš prodo duomenų valymą.
