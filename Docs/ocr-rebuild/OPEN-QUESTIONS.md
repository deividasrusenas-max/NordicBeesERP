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
