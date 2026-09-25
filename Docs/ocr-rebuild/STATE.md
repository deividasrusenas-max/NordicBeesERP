# OCR rebuild — būsena

Atnaujinta: 2026-09-26 | Fazė: Etapas 0 + 0c + Etapo 1 validatorių paruošimas — main'e, **nepush'inta**

## Dabartinė fazė

Etapas 0, Etapas 0c ir Etapo 1 paruošiamieji validatoriai (IBAN, PVM kodo formatas, EN 16931
sumos) yra commit'inti į `main`, **ne push'inti** (`origin/main` = `cff2d46`). Prodo DB schema
paruošta deploy'ui (D-030). Validatoriai dar niekur neprijungti (Etapas 1).

Nauja darbų tvarka — D-022: vartai prieš ekstrakciją. Etapai ir įverčiai —
`HANDOFF-2026-09-25.md` §5, patikslinta D-027, D-028; baigtumo kriterijai — D-031.

**Darbo režimas:** viena juosta, tiesiai `main`, jokių worktree (savininko sprendimas
2026-09-26).

## Commit'ų intervalai (main, nepush'inta)

| Dalis | Intervalas | Kas |
|---|---|---|
| Etapas 0 | `5e44f7c..c93e9af` (8) | tiekėjas be spėjimų, karantinas, aritmetikos ir datų vartai, dublikatų sprendimas/aptikimas, SHA-256 prieš OCR — raportas `.opencode/reports/ocr-etapas0-20260925-2354.md` |
| Etapas 0c | `4eb3f5d..8d5b95f` | re-OCR, rankinis redagavimas, redagavimo formos išsaugojimas (D-035), banko importas (D-033), tik skaitmeniniai PDF (D-032), biudžetas (D-036), semgrep taisyklės, drag & drop (D-034) — raportas `.opencode/reports/ocr-etapas0c-20260926-0136.md` |
| Etapo 1 validatoriai | merge `4a04361` .. HEAD | `IbanValidator`, `VatCodeFormatValidator`, `En16931TotalsValidator` (BR-CO-10/13/15/16 pažodžiui pagal oficialų Schematron, niekada nemeta išimties) + projekto eilutės taisyklė — raportai `overnight-20260926-0157.md`, `prepush-20260926-*.md` |

## Padaryta

- Sesija 01: modulio analizė, dokumentacijos karkasas.
- Sesija 02: dvi nepriklausomos inventorizacijos + `analysis/COMPARISON.md`.
- Sesija 03: `PLAN.md` v2, F0 (157/157 žali), produkcijos auditas.
- Sesija 04: saugyklos 1 žingsnis — `files`, `IFileStore`, `StorageSentinel`,
  `deploy.yml` mount'ai, žalias JSON į `ocr_raw_json`. 172/172.
- Tyrimas 2026-09-25: `analysis/RESEARCH-2026-09-25-reliability.md`.
- Sesija 05: staging perklonuotas iš prodo (D-029); storage gate uždarytas; prodo
  duomenų analizė (`analysis/PROD-DATA-FINDINGS-2026-09-25.md`); D-021…D-029,
  Q-005…Q-010; Etapo 0a užduotis. Detalės — `sessions/2026-09-25-05.md`.
- 2026-09-25/26 (Claude Code): Etapas 0 (8 commit'ai), Etapas 0c (C1–C9 + fix-up'ai),
  D-031…D-036, Etapo 1 validatoriai; staging patikrų dokumentas
  `STAGING-CHECKS-ETAPAS0.md`.

## Storage gate — įrodymai (2026-09-25, staging)

| Patikra | Rezultatas |
|---|---|
| Įkėlimas → blob'as | `blobs/13/55/1355…9bed`, 174 202 B |
| `files` eilutė | id 1, sha256 sutampa, `entity_id` = 376 |
| `expense_invoices.file_id` | 376 → 1 |
| Žalias Azure JSON | `ocr_raw_json` užpildytas |
| Perkūrimas `stop`+`rm`+`run` | naujas konteineris `2026-09-25T19:17:15Z`, tas pats image |
| Failas po perkūrimo | sha256 sutampa prieš ir po |
| PDF peržiūra po perkūrimo | rodo (s. 376) |
| Senas `/uploads/invoices/...` URL | 404 |
| 247 senos sąskaitos su tuščiu `file_id` | sąrašas ir detalių langas neluža (prodo klonas) |
| Drag & drop dialoge | **nepatikrinta** (žr. Blokatoriai) |

## Kitas žingsnis (eilės tvarka)

1. **Raportų peržiūra** (savininkas): Etapas 0, 0c, validatorių ir prepush raportai
   `.opencode/reports/`.
2. **Bump + push** (`bump-version.sh`, tik savininkas). Po bump'o grąžinti savininko stash'ą
   `wip analyze-timing`: `git stash pop`.
3. **Staging patikra** pagal `STAGING-CHECKS-ETAPAS0.md` (prodo klonas, D-029).
4. **Prodas** — push į `production` šaką ir patikra po deploy'aus. Prodo DB paruošta
   2026-09-25 (D-030): `files` + `expense_invoices.file_id`, backup
   `lakstena-dev:~/backup/prod-before-files-ddl-2026-09-25.sql.gz`, sentinel
   `/var/lib/nordicbees/prod/.nordicbees-storage` paruoštas.
5. **Etapo 0 uždarymas** (D-031 tvarka).
6. **Etapas 1** — validatorių prijungimas (IBAN, PVM kodas, EN 16931 sumos) prie OCR ir
   rankinio redagavimo kelio.

Prodo duomenų valymas — atskiras darbas su buhaltere, po Q-010
(`PROD-DATA-FINDINGS-2026-09-25.md` §6).

## Laukia savininko sprendimų

- `OcrQueueWorker` `Attempts++` defektas — failas užšaldytas (`FROZEN.md` §5); Etapo 0c
  raportas §2/§5.
- Dvi užšaldyto įkėlimo dialogo UI problemos (`FROZEN.md` §3): klaidos fazės eilutė
  „Patikrinkite ar visi serveriai veikia…" po failo atmetimo; `setupDropZone` kviečiamas tik
  pirmo render'io metu (drop → ✕ → drop nieko nedaro).
- 12 decimal-precision radinių (semgrep `nordicbees-ef-decimal-precision-annotation-missing`).
- Senos šakos (žr. žemiau) — ką daryti, sprendžia savininkas.

## Senos šakos (informacija, niekas netrinta; 2026-09-26)

- `git branch --merged main`: `acoustic-sociology`, `almondine-writing`, `boiled-cost`,
  `chocolate-tin`, `erratic-dirigible`, `helpful-temperature`, `horn-caravan`,
  `knowledgeable-blizzard`, `polyester-macrame`, `production`, `quickest-trust`,
  `radial-arch`, `unexpected-guppy` (`production` čia rodoma kaip „merged", nes ji atsilieka
  nuo `main` — ji deploy'aus šaka, ne sena).
- `git branch --no-merged main`: `ef-migrations-reconcile`, `feature/sverimo-modulis`.

## Blokatoriai

- Prod deploy laukia: raportų peržiūros, push'o ir staging patikros.
- Q-010 (ar dublikatai pateko į apskaitą) blokuoja prodo duomenų valymą.
- Agento auto-resume: „continue" iš mechanizmo virsta leidimu — užduotyse yra
  Authority rule.
- `llm-overrides-red-gate` (BUGLOG 2026-09-26) — struktūrinis sprendimas neįdiegtas.

Išspręsta Etape 0c (reikia staging patikros): drag & drop dialoge (D-034, naujas
`OnFileDropped`), banko importas nebesiūlo karantino sąskaitų (D-033).

## Fazių lentelė

| Fazė | Būsena |
|---|---|
| F0 Gyvo kelio pataisymai | baigta (157/157) |
| Saugykla, 1 žingsnis | **baigta, gate uždarytas 2026-09-25** |
| Etapas 0 (tylios klaidos, vartai) | commit'inta main'e, nepush'inta; laukia staging |
| Etapas 0c (redagavimas, įvestis, biudžetas) | commit'inta main'e, nepush'inta; laukia staging |
| Etapas 1 (EN 16931, lokalės, IBAN, PVM tarifai) | paruošti validatoriai (neprijungti); prijungimas laukia |
| Etapas 2 (tiekėjo kaskada) | laukia |
| Etapas 3 (ekstrakcija — D-023 kryptis, D-016 atviras) | laukia; Q-006, Q-007, Q-009 prieš pradedant |
| Etapas 4 (matavimas) | laukia |
| Saugykla, 2 žingsnis (PDF į IFileStore) | nepradėta |
