# OCR rebuild — būsena

Atnaujinta: 2026-09-25 | Sesija: 05 (vyksta) | Fazė: Etapas 0 (Claude Code vykdo 0a + 0b)

## Dabartinė fazė

**Storage gate staginge UŽDARYTAS** (2026-09-25). Etapas 0 (Part A + Part B, 8 commit'ai)
vykdomas Claude Code pagal `.opencode/tasks/latest.md`, laukia raporto
`.opencode/reports/ocr-etapas0-*.md`. Prodo DB schema paruošta deploy'ui (D-030).

Nauja darbų tvarka — D-022: vartai prieš ekstrakciją. Etapai ir įverčiai —
`HANDOFF-2026-09-25.md` §5, patikslinta D-027, D-028.

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

## Kitas žingsnis

1. **Etapas 0a** — OpenCode: `FindSupplierIdAsync` (tuščias PVM kodas), karantinas
   (`DUPLICATE_PENDING`/`REJECTED` ne įsipareigojimai), antraštės aritmetikos vartas
   įskaitant tiekėjo priskyrimo kelią. Po raporto — patikra staginge ant prodo klono.
2. **Etapas 0b** — ateities datos vartas, `MISSING_DUE_DATE` pateikimas (D-025),
   `DuplicateInvoiceDialog` (FindAsync + hard delete), SHA-256 prieš OCR.
3. **Prod deploy** — tik po Etapo 0 (kitaip prodas gauna naują kodą su žinomu 336 defektu).
   **Prodo DB paruošta 2026-09-25** (D-030): `files` lentelė ir `expense_invoices.file_id`
   pritaikyti, schema = staging. Backup prieš DDL:
   `lakstena-dev:~/backup/prod-before-files-ddl-2026-09-25.sql.gz`. Sentinel
   `/var/lib/nordicbees/prod/.nordicbees-storage` jau paruoštas. Liko: push į
   `production` šaką ir patikra po deploy'aus.
4. **Prodo duomenų valymas** — atskiras darbas su buhaltere, po Q-010.
   Sąrašas — `PROD-DATA-FINDINGS-2026-09-25.md` §6.

## Blokatoriai

- Prod deploy blokuojamas Etapo 0 (Claude Code vykdo).
- Q-010 (ar dublikatai pateko į apskaitą) blokuoja prodo duomenų valymą.
- `FROZEN.md` §3 teigia, kad `ExpenseUploadDialog` turi `[JSInvokable] OnFileDropped`
  — metodo nebėra, `setupDropZone` vis dar kviečiamas. Drag & drop tikėtinai neveikia.
  Užšaldytas blokas — taisyti tik su leidimu.
- Agento auto-resume: „continue" iš mechanizmo virsta leidimu — užduotyse yra
  Authority rule.
- `BankImport.razor:510–512` (užšaldytas §7) siūlo `DUPLICATE_PENDING` / `REJECTED`
  sąskaitas kaip banko mokėjimo atitikmenis — mokėjimas gali būti susietas su dublikatu.
  Taisyti tik su savininko leidimu.

## Fazių lentelė

| Fazė | Būsena |
|---|---|
| F0 Gyvo kelio pataisymai | baigta (157/157) |
| Saugykla, 1 žingsnis | **baigta, gate uždarytas 2026-09-25** |
| Etapas 0a (tylios klaidos) | Claude Code vykdo |
| Etapas 0b (vartai, UI) | Claude Code vykdo (ta pati sesija) |
| Etapas 1 (EN 16931, lokalės, IBAN, PVM tarifai) | laukia |
| Etapas 2 (tiekėjo kaskada) | laukia |
| Etapas 3 (ekstrakcija — D-023 kryptis, D-016 atviras) | laukia; Q-006, Q-007, Q-009 prieš pradedant |
| Etapas 4 (matavimas) | laukia |
| Saugykla, 2 žingsnis (PDF į IFileStore) | nepradėta |
