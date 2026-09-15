# OCR rebuild — būsena

Atnaujinta: 2026-09-14 | Sesija: 03 | Fazė: F0 (vykdoma)

## Dabartinė fazė

F0 — gyvo kelio defektų taisymas. Užduotis paruošta `.opencode/tasks/latest.md`,
keturi commit'ai vienoje užduotyje (D-013).

## Padaryta

- Sesija 01: modulio analizė, dokumentacijos karkasas, inventorizacijos užduotis.
- Sesija 02: dvi nepriklausomos inventorizacijos (OpenCode + Claude Code),
  palyginimas `analysis/COMPARISON.md`, 16/18 teiginių patvirtinti nepriklausomai,
  trys nesutarimai išspręsti skaitant kodą.
- Sesija 03: `PLAN.md` v2 — fazių tvarka perdaryta pagal patikrintus faktus;
  `DECISIONS.md` D-007…D-013.

## Vykdoma

F0 build užduotis OpenCode harnese. Keturi commit'ai:
A4 (dialogas naikina pataisymus) · A9 (PVM tarifas su kableliu) ·
A11 (aritmetinė patikra, išgalvotos sumos pašalinimas) · N2 (`invoice_number` NULL).

## Kitas žingsnis

Perskaityti `.opencode/reports/f0-live-path-fixes-*.md`, patikrinti keturis
verifikacijos išvesties punktus, tada F1 paruošimas — atskira read-only
`[Authorize]` inventorizacija prieš autorizacijos įjungimą.

## Blokatoriai

- B8 (prod auditas) neįvykdytas — D-008 laukia sprendimo. Q-001 ir Q-003 atviri.
- B8.3 (sąskaitų kiekis per mėn.) privalo būti atsakytas **prieš F3**, nes nuo jo
  priklauso, ar F3–F5 apskritai proporcingi.

## Fazių lentelė

| Fazė | Būsena |
|---|---|
| F0 Gyvo kelio pataisymai | vykdoma |
| F1 Prieiga ir saugykla | laukia (rizikingiausia fazė) |
| F2 Dokumentų modelis | laukia |
| F3 Korpusas + etalonas | laukia (kietas vartas, priklauso nuo B8.3) |
| F4 Ekstraktorius v2 + runner | laukia |
| F5 Agento ciklas | laukia |
| F6 Admino vaizdas | laukia |
| F7 Ingest atgaivinimas (n8n) | laukia |
| F8 Shadow + perjungimas | laukia |
