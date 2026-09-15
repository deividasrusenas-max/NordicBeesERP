# OCR rebuild — būsena

Atnaujinta: 2026-09-15 | Sesija: 03 (baigta) | Fazė: F0 uždarytas

## Dabartinė fazė

F0 baigtas ir vartai uždaryti. Kita — D-014/D-015 sprendimai, tada F1 paruošimas.

## Padaryta

- Sesija 01: modulio analizė, dokumentacijos karkasas, inventorizacijos užduotis.
- Sesija 02: dvi nepriklausomos inventorizacijos (OpenCode + Claude Code),
  `analysis/COMPARISON.md`, 16/18 teiginių patvirtinti nepriklausomai.
- Sesija 03: `PLAN.md` v2, `DECISIONS.md` D-007…D-013, F0 įvykdytas,
  produkcijos auditas. Detalės — `sessions/2026-09-15-03.md`.

### F0 vartai — uždaryti

Commit'ai `2b7f9a3` (A4), `e59ca7f` (A9), `6e864fd` (A11), `63ccdc0` (N2)
plius version bump. **Pilnas rinkinys 157/157 žali** — jokių regresijų.

## Kitas žingsnis

1. D-014 (F0.5 triukšmo mažinimas) ir D-015 (peržiūros forma kaip savarankiška
   fazė) — laukia Deivido patvirtinimo. Keičia fazių tvarką.
2. Read-only `[Authorize]` inventorizacija — prieš F1 autorizacijos įjungimą.
3. Saugyklos užduotis: `deploy.yml` volume + kelias kode, vienu commit'u.
   Atrakina korpuso rinkimą.

## Blokatoriai

- Korpuso rinkimo nepradėti, kol nėra saugyklos — kitaip failai vėl dings.
- D-008 (`AGENTS.md` prod prieiga) neišspręstas, bet nebeblokuoja: auditas
  atliktas rankiniu būdu.

## Žinoma, bet netvarkoma čia

- `bump-version.sh` testų gate praleidžiamas, kai `TEST_DB_CONNECTION` nenustatytas,
  nors `DbTestFixture` turi veikiančią numatytąją reikšmę. Bump'as atrodo
  patikrintas, nors testai nepaleisti. → `Docs/infra/SERVER-STATE.md`
- Infrastruktūros radiniai — `Docs/infra/SERVER-STATE.md`, atskira sesija.

## Fazių lentelė

| Fazė | Būsena |
|---|---|
| F0 Gyvo kelio pataisymai | **baigta** (157/157) |
| F0.5 Triukšmo mažinimas | siūloma (D-014) |
| F1 Prieiga ir saugykla | laukia (rizikingiausia fazė) |
| F1.5 Peržiūros forma | siūloma (D-015) |
| F2 Dokumentų modelis | laukia |
| F3 Korpusas + etalonas | laukia (priklauso nuo saugyklos) |
| F4 Ekstraktorius v2 + runner | laukia |
| F5 Agento ciklas | laukia |
| F6 Admino vaizdas | laukia |
| F7 Ingest atgaivinimas (n8n) | laukia |
| F8 Shadow + perjungimas | laukia |
