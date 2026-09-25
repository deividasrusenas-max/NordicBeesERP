# TASK: OCR Etapas 1 prep — pure validators (not wired in)

## Task type

**BUILD.** Three commits on a **separate branch in a separate git worktree**. Follow
`AGENTS.md`; where this task and `AGENTS.md` disagree, `AGENTS.md` wins — stop and report.

### Why a separate worktree

Another agent (Claude Code) is committing OCR Etapas 0 on `main` in
`~/Projects/NordicBeesERP` at the same time. This task must not touch that working tree,
its index, or `main`. The owner has created the worktree and branch for you:

- worktree: `~/Projects/NordicBeesERP-e1`
- branch: `ocr/etapas1-validators` (created from `main` at `93581a1`)

`AGENTS.md` forbids creating/switching branches unless the task says so — **this task
explicitly authorises working on `ocr/etapas1-validators` only.** Do not create any other
branch, do not switch to `main`, do not merge, do not rebase.

### Authority rule

A bare "continue", whether typed by a human or injected by auto-resume, is **not**
approval. It does not authorise starting new work, moving past a STOP condition, or
acting on your own recommendation. If this session is resumed automatically with no
explicit human instruction naming what to do, stop and say so.

**Do not push.** Commit, then stop and tell the owner.

### Database rule

No database at all. These are pure functions; tests must not need a DB fixture.

## Preconditions

1. `pwd` is the worktree `~/Projects/NordicBeesERP-e1`. If not, STOP.
2. `git branch --show-current` is `ocr/etapas1-validators`. If not, STOP.
3. `git status --porcelain` is clean (except this task file if the owner copied it in
   untracked). Otherwise STOP.
4. Read first: `AGENTS.md`, `Docs/FROZEN.md`,
   `Docs/ocr-rebuild/analysis/RESEARCH-2026-09-25-reliability.md` §3 (deterministic
   validation), `Docs/ocr-rebuild/DECISIONS.md` D-022, D-028.

## Scope rule — the most important rule in this task

**Only new files.** Everything goes under:

- `Services/Validation/` (namespace `NordicBeesERP.Services.Validation`)
- `Tests/NordicBeesERP.Tests/Validation/`

Do **not** modify any existing file — not services, not DTOs, not `Program.cs`, not DI
registration, not the csproj (unless a new folder genuinely requires it — then STOP and
report instead). Static classes / records only; no DI needed. Wiring these validators
into the OCR pipeline is Etapas 1 proper and happens later, after Etapas 0 is merged.

If a commit seems to need an existing file changed → STOP and report.

---

## Commit 1 — `IbanValidator`

1. `IbanValidator.Validate(string? raw)` → result with: `IsValid`, normalised IBAN
   (upper-case, spaces removed), country code, and a reason code when invalid:
   `Empty`, `BadCharacters`, `UnknownCountryLength`, `WrongLength`, `ChecksumFailed`.
2. Algorithm: ISO 13616 mod-97 — move the first 4 characters to the end, letters →
   numbers (A=10 … Z=35), result mod 97 must equal 1. Use chunked arithmetic or
   `BigInteger`; no string-to-long overflow.
3. Length table (ISO 13616 registry) for at least: LT 20, LV 21, EE 20, DE 22, PL 28,
   RO 24, FI 18, SE 24, NL 18, BE 16, DK 18, AT 20, CZ 24, SK 24, FR 27, IT 27, ES 24,
   GB 22, UA 29. A country not in the table → mod-97 still runs; result carries
   `UnknownCountryLength` as a warning, not a failure. Put the table in one place with a
   comment naming the source.
4. Tests: `DE89370400440532013000` valid; `GB82WEST12345698765432` valid; the same with
   one digit changed → `ChecksumFailed`; lower-case + spaces accepted; wrong length for
   LT → `WrongLength`; empty / null → `Empty`; `LT87 7189 9000 0691 0250` — compute the
   expected result, do not assume it.

## Commit 2 — `VatCodeFormatValidator`

1. `VatCodeFormatValidator.Validate(string? raw, string? countryHint = null)` → result
   with `IsValid`, normalised code (upper-case, spaces/dots/dashes removed), country, and
   reason: `Empty`, `UnknownCountry`, `WrongFormat`.
2. **Format only — no checksums.** The LT check-digit algorithm is unknown (open
   question Q-005); do not invent one. Write that in an XML doc comment.
3. Formats (after the 2-letter prefix; accept the code without prefix when
   `countryHint` is given): LT 9 or 12 digits; LV 11 digits; EE 9 digits; DE 9 digits;
   PL 10 digits; RO 2–10 digits. Greece uses prefix `EL`, not `GR` — include it only if
   you add GR/EL at all.
4. Production evidence to test against: `LT277044060003097307` (18 digits — two codes
   glued together by OCR) → `WrongFormat`. Also: `LT120252515` valid; `LT100013406816`
   valid (12 digits); `120252515` with hint `LT` valid; `LT12025251` → `WrongFormat`;
   `XX123` → `UnknownCountry`.

## Commit 3 — `En16931TotalsValidator` (BR-CO header arithmetic)

1. Input: a plain record (not `OcrResultDto`) with nullable decimals — line net amounts
   (list), allowance total (BT-107), charge total (BT-108), sum of line net (BT-106),
   total without VAT (BT-109), VAT total (BT-110), total with VAT (BT-112), paid amount
   (BT-113), rounding amount (BT-114), amount due (BT-115).
2. Rules, each returning a violation with the rule id, expected value, actual value and a
   Lithuanian message naming the rule id:
   - **BR-CO-10**: BT-106 = Σ line net (BT-131)
   - **BR-CO-13**: BT-109 = BT-106 − BT-107 + BT-108
   - **BR-CO-15**: BT-112 = BT-109 + BT-110
   - **BR-CO-16**: BT-115 = BT-112 − BT-113 + BT-114
3. Comparison semantics exactly as the official Schematron: both sides rounded to 2
   decimals with XPath `round()` semantics (`floor(x × 100 + 0.5) / 100`, i.e. halves
   round toward +∞, also for negatives), then compared for equality. Source:
   `ConnectingEurope/eInvoicing-EN16931`, `ubl/schematron/preprocessed/EN16931-UBL-validation-preprocessed.sch`.
   Put the source in an XML doc comment. Do not use a flat "0,02" tolerance.
4. Missing inputs: a rule whose inputs are missing is **not applicable** (no violation),
   and the result lists it as not applicable — it must never silently pass as "OK".
   Missing allowance/charge/paid/rounding may be treated as 0 **only** where EN 16931
   treats them as optional-with-zero-default; state which in a comment.
5. Separately, and clearly named as **not** EN 16931: a line-level plausibility check
   `qty × unit price ≈ line net` with tolerance `max(0.01, 0.5 % of line net)`. XML doc:
   „Projekto sprendimas, ne EN 16931 reikalavimas (BT-131 deklaruojama siuntėjo)".
6. Tests, using the real ASF0021438 figures (RESEARCH §1): header 934,22 + 196,18 =
   1 130,40 → BR-CO-15 passes; lines where one line net is 803,31 and the rest sum to
   130,91 → BR-CO-10 passes; the same with 972,00 (the gross figure the mapper wrongly
   took) instead of 803,31 → BR-CO-10 fails with expected/actual shown; the Artea
   staging invoice: line 105,41, header totals 0,00 → BR-CO-10 fails; rounding edge case
   x.xx5 on both signs; not-applicable when BT-110 is null.

---

## Rules for all commits

- One concern per commit, three commits, in this order. Do not squash.
- Every commit: `dotnet build` 0 errors; the new tests pass
  (`dotnet test --filter "FullyQualifiedName~Validation"` — no DB needed).
- Guardrail check (`agent-guardrails check --base-ref HEAD~1`) and semgrep per
  `AGENTS.md` on every changed file.
- Reviewer verdict mandatory per `AGENTS.md` (the `reviewer` role).
- User-facing strings (violation messages) in Lithuanian; code and comments in English.
- No new NuGet packages.
- **Do NOT run `bump-version.sh`** — it pushes. **Do not push.** Stop after commit 3.

## Verification required in the report

1. `git log --oneline main..HEAD` — exactly three commits.
2. `git diff --stat main...HEAD` — only new files under the two allowed folders.
3. `git show --stat` for each commit.
4. The `dotnet test --filter "FullyQualifiedName~Validation"` output.
5. Guardrail + semgrep output.
6. Reviewer verdict, verbatim.

## Report

`.opencode/reports/ocr-etapas1-validators-<YYYYMMDD-HHMM>.md` **in the worktree**:

1. What each commit added, with `file:line`.
2. The computed result for `LT87 7189 9000 0691 0250`.
3. Proposed wiring points for Etapas 1 proper (where in `ExpenseOcrService` /
   `ExpenseService` each validator would be called, and which OCR flag it would raise) —
   as a proposal only, nothing implemented.
4. The six verification outputs above.

## STOP conditions

- Not in the worktree, or not on `ocr/etapas1-validators`, or tree dirty at start.
- Any change to an existing file seems necessary.
- A test would need a database.
- You are about to push, merge, rebase, switch branch, or run `bump-version.sh`.
- Any instruction here conflicts with `AGENTS.md`.
