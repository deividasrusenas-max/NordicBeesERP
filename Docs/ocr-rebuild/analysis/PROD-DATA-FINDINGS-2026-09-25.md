# Prodo duomenų radiniai — 2026-09-25

**Šaltinis.** `nordic_bees_erp_staging`, perklonuota iš prodo `nordic_bees_erp`
2026-09-25 ~21:52 (D-029). Visos užklausos — tik `SELECT`, vykdė Deividas ant
`lakstena-dev`. Prodas tiesiogiai neužklaustas.

**Paskirtis.** (1) Etapo 0 dizaino pagrindas; (2) tikslus valymo sąrašas buhalterei.
Valymas — NE šios sesijos darbas (prodo duomenys, reikia buhalterės, Q-010).

---

## 1. Statusų pasiskirstymas

| Statusas | Kiekis | Suma su PVM, € |
|---|---|---|
| `DUPLICATE_PENDING` | 28 | 28 679,74 |
| `NEEDS_REVIEW` | 60 | 519 780,58 |
| `PENDING` | 40 | 44 641,17 |
| `PENDING_SUPPLIER` | 119 | 50 626,98 |
| **Iš viso** | **247** | |

`NEEDS_REVIEW` sumą iškreipia vienas įrašas (`213`, žr. §4).

---

## 2. Dublikatai

**Išvada.** Aptikimas veikė: kiekvienas tikras dublikatas gavo `DUPLICATE_PENDING`.
Žala atsiranda todėl, kad šis statusas skaičiuojamas cash flow, eksporte ir sąrašuose
(D-027).

**27 tikri dublikatai** (tas pats normalizuotas numeris ir suma, įkelta minučių
skirtumu). Originalas nustatytas kaip mažiausias `id` grupėje — **prieš valymą
patikrinti `original_filename` kiekvienai porai** (patikrinta tik 197/206).

| Dublikatas | Originalas | Numeris | Suma, € | Pastaba |
|---|---|---|---|---|
| 149 | 148 | PRD 0015764 | 113,74 | |
| 158 | 130 | 24- 14 | 19,50 | |
| 201 | 200 | EDV-1084052 | 16,02 | |
| 209 | 200 | EDV-1084052 | 16,02 | trečia kopija |
| 206 | 197 | 26 0232 | 50,00 | abu priskirti 336 — žr. §3 |
| 207 | 198 | 26190571 | 1 414,03 | |
| 208 | 199 | EDV-1084221 | 12,30 | |
| 210 | 203 | 9156078 | 266,20 | |
| 211 | 204 | APJ010940 | 35,46 | |
| 212 | 205 | 534289/0227 | 17,00 | |
| 226 | 225 | 27886 | 111,32 | |
| 231 | 229 | 000834 | 1 403,60 | |
| 232 | 230 | 23004729 | 25,57 | |
| 246 | 245 | INV 2026-API-138 | 23 524,80 | 82 % visos sumos |
| 249 | 248 | BIK1895517 | 28,65 | |
| 252 | 250 | 00527 | 200,00 | |
| 253 | 251 | CE-RO09189 | 23,00 | |
| 255 | 254 | 0019081 | 60,00 | |
| 274 | 273 | 0060802 | 12,00 | |
| 278 | 273 | 0060802 | 12,00 | trečia kopija |
| 311 | 307 | LYT-F070-021243 | 138,20 | |
| 327 | 326 | URN 0202 | 108,90 | |
| 333 | 332 | VLT0575914 | 261,64 | |
| 340 | 339 | BIK1900002 | 386,43 | |
| 354 | 353 | 2408047 | 378,73 | |
| 357 | 356 | LYT-F150P-079102 | 39,68 | |
| 361 | 360 | PRATC-1913107374 | 4,95 | |

**1 klaidingas dublikatas:** `277` (`20260713_165208_Rotada 1.pdf`, „1", 0,00 €)
sulygintas su `173` (`20260713_151513_Franko.pdf`, „1", 0,00 €). Skirtingi tiekėjai.
Priežastis: trumpas numeris + suma 0,00 (D-027 „žinoma spraga").

---

## 3. Tylus neteisingas tiekėjo priskyrimas

**Priežastis.** `ExpenseOcrService.FindSupplierIdAsync`: kai OCR negrąžina PVM kodo,
sąlyga `bp.VatCode == vatCode` virsta `bp.VatCode == ""` ir grąžina pirmą partnerį su
tuščiu PVM kodu. Prode tokių partnerių 17; visus atvejus gavo vienas — `336`
(Antanas Auglys). Nė viena iš 16 sąskaitų nėra jo. `VENDOR_NOT_FOUND` nepasirodo.

| id | Numeris | Data | Suma, € | Failas (tikrasis tiekėjas) |
|---|---|---|---|---|
| 139 | 1 | 2026-01-12 | 3 291,30 | Gimziunas |
| 140 | 1 | 2026-01-22 | 2 150,00 | klepeckiene KK2026 01 |
| 145 | ECM8245019487 | 2026-01-31 | 169,00 | Nuotolinė edukacija |
| 146 | PRATC-1912293712 | 2026-01-31 | 0,00 | Panevėžio atliekos |
| 172 | 2026479 | 2026-02-06 | 580,00 | Eesti Mesinike Liit |
| 173 | 1 | 2026-07-13 | 0,00 | Franko |
| 186 | 1 | 2026-02-18 | 301,00 | siaudykisG GS2026 01 |
| 190 | 1218355 | 2026-02-15 | 330,00 | UTA PL |
| 197 | 26 0232 | 2026-03-25 | 50,00 | Dezekspresas |
| 202 | 1 | 2026-03-31 | 223,50 | Gintaras Dagys S2026 03 |
| 206 | 26 0232 | 2026-03-25 | 50,00 | Dezekspresas (dublikatas) |
| 237 | 1239109 | 2026-03-15 | 397,85 | UTA PL |
| 256 | 26 0322 | 2026-04-27 | 50,00 | Dezexpres |
| 296 | 26 0409 | 2026-05-25 | 50,00 | Dezexpres |
| 319 | PRATC-1913016225 | 2026-05-31 | 14,85 | Panevėžio atliekos |
| 331 | 1297197 | 2026-05-31 | 0,00 | UTA PL 1 |

**Šalutinis radinys.** Bitininkų sąskaitų serijos (`KK2026 01`, `GS2026 01`,
`S2026 03`) Azure nukirstos iki „1". Numerio ištraukimas prarado seriją.

---

## 4. Sulūžusi antraštės aritmetika

`AddAmountConsistencyFlags` šiuos atvejus pažymėtų, bet vėliavėlė neįeina į statusą
(D-028).

| id | Numeris | Tiekėjas | Neto | PVM | Bruto | Vėliavėlės |
|---|---|---|---|---|---|---|
| 213 | 26030004 | 10 | 0,00 | 0,00 | **465 374,45** | ZERO_VAT, AMOUNT_MISMATCH |
| 167 | 90 | 40 | 5,00 | 0,00 | 5 127,85 | ZERO_VAT, LINES_NOT_FOUND |
| 168 | 91 | 40 | 17 973,40 | 0,00 | 3 528,00 | ZERO_VAT, AMOUNT_MISMATCH |

Tiekėjas `371` (numeriai `RBS…`) turi pasikartojantį `AMOUNT_MISMATCH` (≥5 sąskaitos)
— eilučių lygio problema, antraštė susiveda.

---

## 5. Ateities datos

| id | Numeris | Data | Sukurta |
|---|---|---|---|
| 168 | 91 | 2026-11-02 | 2026-07-13 |
| 341 | PV/12911/2026 | 2026-10-06 | 2026-07-14 |
| 167 | 90 | 2026-09-02 | 2026-07-13 |

Visų trijų diena ≤12. 90 ir 91 — gretimi numeriai, sukurti tą pačią minutę.
**Hipotezė** (nepatikrinta prieš PDF — jų nebėra): DD/MM sukeitimas (2026-02-09,
2026-02-11, 2026-06-10).

`167` ir `168` sutampa su §4 — tie patys du dokumentai sugadinti ir datoje, ir sumose.

---

## 6. Valymo sąrašas (buhalterei, atskiras darbas)

1. 27 dublikatai (§2) → `REJECTED` su priežastimi, po Q-010 patikros.
2. `277` → grąžinti iš `DUPLICATE_PENDING` į peržiūrą (klaidingas dublikatas).
3. 16 sąskaitų (§3) → atsieti nuo `336`, grąžinti į `PENDING_SUPPLIER`, priskirti teisingai.
4. `213`, `167`, `168` → sumos ir datos iš originalų.
5. `341` → data iš originalo.

Senųjų PDF nebėra (`Docs/infra/SERVER-STATE.md` §1.3) — taisymui reikalingi popieriniai
arba el. pašto originalai.
