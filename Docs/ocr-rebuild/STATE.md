# OCR rebuild — būsena

Atnaujinta: 2026-09-16 | Sesija: 04 (baigta) | Fazė: saugyklos 1 žingsnis

## Dabartinė fazė

Saugyklos standarto 1 žingsnis įgyvendintas ir išleistas (`v0.17.89`, `v0.17.90`),
bet **vartai neuždaryti** — staging patikrinimas neatliktas.

## Padaryta

- Sesija 01: modulio analizė, dokumentacijos karkasas.
- Sesija 02: dvi nepriklausomos inventorizacijos + `analysis/COMPARISON.md`.
- Sesija 03: `PLAN.md` v2, F0 (157/157 žali), produkcijos auditas.
- Sesija 04: saugyklos 1 žingsnis — `files` lentelė, `IFileStore`,
  `StorageSentinel`, `deploy.yml` mount'ai, žalias Azure JSON į `ocr_raw_json`,
  base64 peržiūra, semgrep. 172/172. Detalės — `sessions/2026-09-16-04.md`.

**Sentinel patvirtintas realiomis sąlygomis:** staging atsisakė startuoti be
žymeklio failo ir pasakė, ko trūksta.

## Kitas žingsnis — trys atskiros sesijos, šia tvarka

1. **Staging bazė** — šešios trūkstamos EF migracijos + niekada nepritaikytas
   `Migrations/Scripts/20260826_artwork_multifile.sql`. Be jų staging testuoja ne
   tai, kas bus produkcijoje. Žr. `Docs/infra/SERVER-STATE.md` I-10.
2. **Staging patikrinimas** pagal runbook'ą — įkelti sąskaitą, patvirtinti blob'ą
   ir `files` eilutę, **perkurti** konteinerį (`stop`+`rm`+`run`, ne `restart`),
   patvirtinti, kad failas išliko; senas `/uploads/...` URL negrąžina failo;
   base64 peržiūra veikia.
3. **Prod deploy** — tik po 1 ir 2. Sentinel
   `/var/lib/nordicbees/prod/.nordicbees-storage` jau paruoštas. Papildoma
   patikra: 247 sąskaitos su tuščiu `file_id` neluža sąraše ir detalių lange.

## Blokatoriai

- Prod deploy blokuojamas, kol nėra 1 ir 2.
- Agento auto-resume: „continue" iš mechanizmo virsta leidimu. Push turi tapti
  žmogaus veiksmu, ne agento.

## Fazių lentelė

| Fazė | Būsena |
|---|---|
| F0 Gyvo kelio pataisymai | baigta (157/157) |
| Saugykla, 1 žingsnis | įgyvendinta, vartai neuždaryti |
| Saugykla, 2 žingsnis (PDF į IFileStore) | **nepradėta** — agentas buvo pradėjęs be leidimo |
| Peržiūros forma | laukia |
| Ekstraktoriaus darbai (korpusas, v2, agento ciklas) | laukia |
