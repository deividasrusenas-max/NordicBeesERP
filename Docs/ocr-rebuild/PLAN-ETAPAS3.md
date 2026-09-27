# Etapas 3 — plan (extraction; for review before any code)

Written 2026-09-27 by Claude Code for the owner and the planning advisor. **Nothing here is implemented.**
Base: `analysis/RESEARCH-2026-09-25-reliability.md` §2 (extraction architecture), §5 (confidence), §7 (measurement), §8
(cost), §10 (recommended plan, items 12–14); D-016, D-019, D-022, D-023, D-026, D-032; `OPEN-QUESTIONS.md` Q-006,
Q-007, Q-008, Q-009; the Etapas 2 reports (`etapas2-s3-…` through `-s7-…`); `analysis/PROD-DATA-FINDINGS-2026-09-25.md`;
code read at `main` `38b64bd` plus the two Part A fix-up commits of this session (identity kept on match, transactional
alias confirm). Every `file:line` was read in this session unless marked otherwise. Claims from reading code that were
**not executed** are marked *(read, not run)*. Where something could not be checked, §11 says so — the format is
`PLAN-ETAPAS1.md` / `PLAN-ETAPAS2.md`'s.

Deploy context (D-037): nothing from this plan goes to production until Etapai 1–4 are done and verified on staging.
Etapas 3 is closed on staging, same as 1 and 2.

## Contents

0. How extraction works today; concrete failure classes with the real cases
1. Options, compared on accuracy evidence, cost, privacy, operational risk, effort
2. D-016 closure: the corpus + the clean-start re-upload, and how to use them
3. The dual asymmetric call (Hunter/Mapper) — worth it here or not
4. Where Etapas 3 plugs into the Etapas 1/2 gates
5. ZERO_VAT per D-026: the formulation check
6. Q-006 and Q-007 — how to answer each before building
7. Measurement: the D-031 criterion 3 test and the golden-file regression Etapas 4 builds on
8. Sessions, tests, staging checks, estimate, open questions

---

## 0. How extraction works today

### 0.1 The call

`ExpenseOcrService.AnalyzeInvoiceAsync` (`Services/ExpenseOcrService.cs:65-94`) calls Azure Document Intelligence
`prebuilt-invoice` (`ModelId` constant, `:23`) with `locale: "lt-LT"`, `pages: "1-2"` (`:89-90`). It reads Azure
credentials from `app_settings` (`GetAzureCredentialsAsync`, `:33-41`) — no per-call model choice, no page-image
option, no second call. `ProcessAsync` (`:96-…`) is the only caller in the live pipeline (`Program.cs` DI, and
`OcrQueueWorker.cs:77`, FROZEN §5, dead today per PLAN-ETAPAS2 §0.1).

### 0.2 Fields → DTO

`ProcessAsync` parses `analyzeResult.documents[0].fields` (`:105-140`) through two small helpers, `TryGetField` /
`TryGetFieldProperty` (`:143-158`), and reads, in order: `VendorName` (`:161-168`), `VendorTaxId` (`:171-174`, through
`CleanVatCode` — strips spaces/dashes/dots only, `:842-846`), `VendorAddress` (`:177-195`, with a hand-rolled
"106L Marvelės g." → "Marvelės g. 106L" reorder, `:183-188`), `VendorAddressRecipient` as a name override when
`VendorName` looks like a logo without a legal form (`:198-209`), `PaymentDetails[0]` for the IBAN/account
(`:216-229`), `VendorPhone` / `VendorEmail` (`:232-241`, not persisted, PLAN-ETAPAS2 §0.3), `InvoiceId` with a
`"Serija … Nr. "` prefix strip (`:244-264`), `InvoiceDate` / `DueDate` with a `PaymentTerm`-day fallback
(`:267-307`), `CustomerName` / `CustomerTaxId` (`:309-326`, the `WRONG_RECIPIENT` inputs). Header totals
(`SubTotal`/`TotalTax`/`InvoiceTotal`) go through `OcrNumberReads.ReadHeaderTotals` (`:328-329`,
`Services/OcrNumberReads.cs:20-41`), which also captures the printed `content` next to each for D-041's locale check.

The supplier's **registration code** is extracted separately, after the buyer's own settings are loaded, via the
`ExtractSupplierCompanyCode` wrapper (`ExpenseOcrService.cs:786-805`, called at `:518-520`), which reads
`VendorBusinessNumber` and `analyzeResult.content` out of the raw JSON and calls
`SupplierCompanyCodeExtractor.Extract` (`Services/Validation/SupplierCompanyCodeExtractor.cs`, the actual call at
`ExpenseOcrService.cs:803`) — a labelled-text regex search over that raw text plus `VendorBusinessNumber` (Etapas 2
S2c, D-045); this is the one place the code already reads *raw text* rather than a typed Azure field, and it is the
direct model for the ZERO_VAT text search in §5.

### 0.3 Lines

`Items` (`:332-342`) is enumerated for the VAT rate (`:344-378`, first non-zero `TaxRate`, or derived from
`TotalTax`/`SubTotal` when absent, `:380-382`) and then again for each line (`:385-…`): `Description` with two
fallbacks (`ProductCode`, `ProductDescription`, `:396-412`), `Quantity`/`UnitPrice`/`Amount` (with `Net` as a
fallback for `Amount`) via `OcrNumberReads.ReadLineNumbers` (`:415`, `OcrNumberReads.cs:47-73`, again capturing the
printed text), `Unit` (`:418-420`), the line's own `TaxRate`/`TaxAmount` with a header fallback (`:427-458`), and
`AmountInclVat` computed from the net and rate, or the net itself derived as `UnitPrice × Quantity` when `Amount`
gave nothing (`NetDerived = true`, `:465-472`). `ReconcileLines` (`:636-666`, D-038 Q7/D-041 S7) then drops
zero-amount lines only if the sum still exceeds the header by more than 0,05 €, and — since S7 — never deletes a
real line; it marks `LINE_LARGE_QUANTITY` / `LINE_DUPLICATE_DESCRIPTION` (information) where the old code used to
delete.

**`analyzeResult.tables[]` is never read anywhere in this codebase** *(grep across `Services/*.cs`, confirmed empty,
2026-09-27)*. Every line comes from `documents[0].fields.Items`, the same "prebuilt field-mapper" layer RESEARCH §1
identifies as the actual point of failure on ASF0021438.

### 0.4 What is stored

`ocr_raw_json` (Azure's full response) is stored unconditionally on every create and re-OCR
(`ExpenseService.cs:2472` `OcrRawJson = ocrResult.RawJson`; the re-OCR `UPDATE`'s `ocr_raw_json = {20}` SQL text at
`:2656`, the bound `ocrResult.RawJson` argument at `:2686`). This is the asset D-016 conditioned the architecture
decision on, and it is what a golden-file regression suite (§7) runs against — no new storage work is needed for
Etapas 3. *(Line numbers as of this session's `main`, `57c1ca4`; `ExpenseService.cs` was touched by two fix-up
commits earlier in this session, so earlier drafts of this citation were already stale by the time this plan was
written — re-check before relying on them in a later session.)*

### 0.5 Concrete failure classes, with the real cases

| Class | Case | What happened | Where it is documented |
|---|---|---|---|
| **Wrong column mapped to the line net** | ASF0021438 | `Quantity`/`Amount` for one line came from the "Suma su PVM" (gross) column while `analyzeResult.tables[2]` had the same row correct with a labelled `columnHeader` | RESEARCH §1 (opening paragraph), PLAN-ETAPAS1 §3.1 "optional row" |
| **Same class, two more real documents** | EGO transport, UTA PL | Header net/VAT/gross 430,00/90,30/520,30 (EGO) and 268,30/61,70/330,00 (UTA PL); the lines summed to the **gross**, not the net, in both — caught only because BR-CO-10 (D-040) now flags the mismatch, not because the extraction itself noticed | `STAGING-CHECKS-ETAPAS1.md` §5, D-023 (the direction this plan is written against) |
| **Same class, a third case** | "Union Tank" *(named in this session's authorising message; I could not independently corroborate the supplier name from the read documents — the mechanism itself is the same one documented for EGO/UTA PL above)* | — | — |
| **Locale/thousands misread** | the "Eurovertis" / **"DOLABELS 004321"** invoice | Three unit prices printed „0,115", „0,118", „0,006" read by Azure as **115, 118, 6** (a ×1000 error — the decimal comma dropped); a fourth, „11,990", read as **11990** where the line arithmetic (`1 × 11,99 = 11,99`) contradicts Azure's own choice | `STAGING-CHECKS-ETAPAS1.md` §3 ("Eurovertis"), `STATE.md:84` (same numbers, named "DOLABELS 004321"), D-041 (the corpus finding: 3/3 real `NUMBER_MISREAD` cases in the 11-document corpus are exactly this shape) |
| **Header totals that do not mean what the code assumes** | AB Artea, interest statement (invoice 376, `PL99810705.pdf`) | Header gross = 0 (an interest/financial-services notice, 0 % VAT, D-026's own first real ZERO_VAT example) while the lines carry the real amounts — "gross 0" here is not a data error, it is a *different total concept* (nothing payable) that the code's `MISSING_MONEY_FIELD` gate correctly flags as "cannot verify" rather than "wrong" | `STAGING-CHECKS-ETAPAS1.md` §10, D-045 (Artea's company-code/VAT-code independence), `OPEN-QUESTIONS.md` Q-009 |
| **Lines that legitimately exceed the header** | Rabenas | 10 Azure line items summing to 2 385,61 against a header net of 2 060,33; repeated descriptions and two zero-amount lines | `STAGING-CHECKS-ETAPAS1.md` §4, D-041 item 4 (the S7 decision not to delete these) |
| **Missing money field entirely** | LD | Azure returned only the gross total (158,49); no net, no VAT amount to reconcile against | `STAGING-CHECKS-ETAPAS1.md` §6 |

Six of these seven rows are *already* caught — loudly, not silently — by Etapas 0/1's deterministic gates
(D-028, D-040, D-041) **without** touching extraction at all. That is the point RESEARCH §1 makes and PLAN-ETAPAS1/2
already acted on: the gates are load-bearing; Etapas 3 changes what feeds them, and must not remove or weaken any of
them (§4).

---

## 1. Options, compared

None of these has been benchmarked on this project's actual documents — no LT/Baltic invoice benchmark exists for
any of them (RESEARCH §2, §11). The comparison below is therefore evidence-for-the-mechanism, not
evidence-for-the-number; §7's dev/hold-out measurement is what turns any of these rows into a real number for this
business.

| Option | Accuracy evidence | Cost/year (35 inv./mo., RESEARCH §8) | Privacy | Operational risk | Effort |
|---|---|---|---|---|---|
| **(a) `prebuilt-layout` + own `tables[]` serialisation + ONE Azure OpenAI Structured Outputs call** *(RESEARCH §8's "(b)/(b')" row — not the one RESEARCH itself labels "Rekomenduojama architektūra", which is the dual-call variant at §3/row (a+) below, ~19,70/~10,60 USD)* | Directly fixes the ASF0021438 class (the correct data was already in `tables[]`); no LT/Baltic-specific benchmark | ~14,10 USD (gpt-4.1) / ~9,50 USD (gpt-4.1-mini) | Azure-hosted; same jurisdiction posture as today (Azure DI is already used) | Depends on Azure OpenAI quota/availability in the same region; a documented markdown-table colspan bug (RESEARCH §2) is exactly why "own serialisation" is required, not markdown | Medium-high: new prompt, new schema, new parsing, must keep `ocr_raw_json` (now Azure DI's *and* Azure OpenAI's raw response) for §7 |
| **(b) Same, with a LOCAL LLM on the owner's rig** (`llama-server`, JSON-schema/grammar-constrained output; `http://100.110.26.80:9292/v1` via `llama-swap`, `Docs/HARNESS_GUIDE.md:93,110`) | No benchmark for grammar-constrained local models on invoices found in this session or in RESEARCH | ~0 marginal (electricity only) — the rig is already paid for and running for the harness | **Strongest** — the document content never leaves the owner's network; relevant given personal data appears on some invoices (beekeeper names/addresses, D-041) | **This is the real risk, not accuracy.** The GPU host is a *separate machine reachable over Tailscale*, not a managed cloud service: it has gone offline at least twice in the 2026-09-25/26 window (`.opencode/reports/ocr-etapas0-20260925-2354.md:13` — "the dev DB host `local-llm` (100.110.26.80) went offline"; a second outage referenced in this session's authorising message). An OCR pipeline that depends on it for every invoice inherits that availability, on a machine whose primary job is the coding harness, not production uptime | Medium: still needs the `tables[]` serialisation and a schema; grammar-constrained decoding on a 4× RTX 3090 box has never been load-tested against JSON-Schema-shaped invoice output at this project's line-count sizes |
| **(c) Keep `prebuilt-invoice`, repair lines from `tables[]` deterministically (no LLM)** | Directly addresses ASF0021438's actual defect (a labelled `columnHeader` table exists in the same response) with no new external call at all | **0 additional** — `tables[]` is already inside the response the code already pays for | No new call, no new data leaving Azure at all beyond what already happens | Lowest of the four: no new dependency, no new failure mode, nothing to keep running | Low-medium: parse `tables[]`, match rows to the `Items` already extracted by column header text (LT/DE/RO/LV/EE header vocabulary), and prefer the table's line net when it disagrees with `Items` and the table's row reconciles against the header (BR-CO-10) and `Items`'s does not |
| **(d) Veryfi** (Q-007) | Unconfirmed for LT/DE/RO/LV/EE/UA (RESEARCH §8, §11 — "no commercial vendor's language support was verified") | 0 (free tier, ≤100 ops/mo. — this project's volume fits entirely) | Documents leave the network to a third party — a privacy regression versus (b)/(c), and versus (a) if the data residency terms differ from Azure's | Free-tier limits/ToS not read in this session; a vendor dependency for the whole pipeline | Zero to test (already-built API), non-zero to integrate if adopted |

**Recommendation for Etapas 3 itself: (c) first, as a bounded, low-risk session, then re-open the choice between (a)
and (b) using the §7 measurement.** Reasoning:

1. (c) directly fixes the one *documented, real* extraction defect this project has ever found (ASF0021438), with the
   least new surface area and the shortest path to a golden-file regression test (§7) — the RESEARCH report itself
   frames the LLM options as *comparisons* to make once a baseline exists, not as the first move; RESEARCH §8's own
   "Rekomenduojama architektūra" label sits on the **dual-call** variant (~19,70/~10,60 USD), which describes a
   target state this plan defers to §3, not something option (a) above claims to be.
2. Adding an LLM call — local or Azure — before a deterministic `tables[]` repair is tried is the same mistake D-022
   already named once for the whole rebuild ("vartai prieš ekstrakciją"): it invests in machinery before the cheap,
   provable fix.
3. If (c) turns out insufficient on the dev/hold-out set (§7 — a document where the table itself is malformed, or no
   table exists), the choice between (a) and (b) then has real numbers behind it (does (c) alone close the gap? by
   how much for how many documents?) instead of being made on RESEARCH's general literature review alone.
4. The local-rig option (b) is the most attractive on cost and privacy, but its *availability* risk is unresolved
   and unmeasured; deciding it now would be exactly the kind of unverified assumption D-016 was created to prevent.

**What NOT to do now, and why:** page-image / multimodal input (RESEARCH §2's benchmark is real but not for
Baltic invoices and not for Azure DI markdown vs `tables[]` specifically) and the Hunter/Mapper dual call (§3) are
both justified additions *once a single-call pipeline's error rate is measured* (§7), not before.

---

## 2. D-016 closure

D-016 deferred any extraction architecture decision until (1) storage works and (2) ~10 invoices had gone through
Azure with the raw responses read. **Both conditions are already met**, and D-016 should be formally closed in the
first Etapas 3 session:

1. Storage: `ocr_raw_json` on every invoice since Etapas 0 (§0.4).
2. ~10 raw responses read: the corpus (`~/NordicBeesERP-corpus/`, outside git — personal data, D-041) holds **11**
   Azure `prebuilt-invoice` responses (SHA-256-named `.json` files, a `manifest.tsv` mapping each hash to its
   original local path, and `tools/CorpusTool/` — the one-off collector program, itself outside the repo). D-041
   already used it to measure `NUMBER_MISREAD`/`NUMBER_AMBIGUOUS` rates for S7 and, later, the S2c company-code
   extractor's field-presence numbers (`.opencode/reports/etapas2-s2-20260927-0400.md`). Confirmed present on this
   machine 2026-09-27 (`ls ~/NordicBeesERP-corpus/`, 11 `.json` files + `manifest.tsv` + `own-company.txt`).

**How to use the corpus for Etapas 3, concretely:**

- **`tables[]` presence and shape.** A one-off script (Etapas 3 S1, no product code) over the 11 stored JSON files:
  for each, does `analyzeResult.tables` exist, how many tables, does any table's `columnHeader` cells overlap the
  vocabulary the line-repair needs (Kiekis/Quantity, Kaina/Price, Suma/Amount, in LT/DE/RO/LV/EE). This is the
  precondition check for option (c) — if most invoices have no usable table, (c) does not close the gap and the
  plan revisits (a)/(b) sooner.
- **The dev/hold-out set (§7) comes from the clean-start re-upload**, not from the 11-document corpus. D-045's clean
  start (`STAGING-CHECKS-ETAPAS2.md` §4) re-uploads ~250 real expense invoices to staging through the *current*
  pipeline, and every one gets a fresh `ocr_raw_json`. That is ~250 raw responses with known, human-visible outcomes
  (the owner is the one clicking through every upload) — far more than RESEARCH §7's "40 dev + 20 hold-out"
  minimum, and it happens regardless of Etapas 3 (D-045 step 2 is Etapas 2's own closure work). **Etapas 3 should not
  duplicate this** — it should wait for the Etapas 2 clean-start run to finish, then draw its dev/hold-out set from
  its results.
- **Who labels, in what format, stored where.** The owner, because labelling means "is this number/name/date what
  the PDF actually says" — the one comparison a script cannot make. Format: a per-invoice YAML/JSON sidecar keyed by
  the invoice's file SHA-256 (matching the corpus's own naming) with the fields RESEARCH §7 cares about (header
  totals, VAT rate, each line's quantity/unit price/net, supplier VAT/name) and the human-verified correct value —
  **not** a full re-transcription of the invoice. Stored **outside git**, next to the existing corpus
  (`~/NordicBeesERP-corpus/labels/`, or a sibling directory) — it is personal data (supplier names, sometimes
  individual beekeepers, D-041) by the same reasoning that keeps the corpus itself out of the repository. The
  regression suite (§7) reads it from an environment variable pointing at that path, exactly as
  `ExpenseSupplierMatchingTests`-style tests already gate corpus-dependent cases on a file's presence (D-041's own
  pattern, `.opencode/reports/etapas2-s2-20260927-0400.md`: "the corpus test needs
  `~/NordicBeesERP-corpus/own-company.txt`… skipped otherwise").

---

## 3. The dual asymmetric call (Hunter/Mapper)

**Not for Etapas 3.** RESEARCH §2/§5 is explicit about the evidence tier: ExtractConf is a single-author workshop
paper (RobustifAI @ IJCAI-ECAI 2026), its numbers are "reported, not independently verified", and — most
importantly for the recommendation above — **no production system implementing this pattern was found anywhere**
(RESEARCH §11: "Nerastas joks produkcinis dual-call… įgyvendinimas"). Its actual finding that transfers here is not
"run two calls" but **"OCR-native confidence features alone reach AUC 0,896 against 0,928 for the full 40-feature
model"** (RESEARCH §5) — i.e., the *existing*, free Azure DI per-field confidence is most of the signal, and a
second call buys three points of AUC for a workshop-paper-strength claim. Given this project's actual measured
failure modes (§0.5) are column-mapping and locale-decimal errors — both of which the deterministic checks in §4
already catch structurally, not statistically — a disagreement-based confidence signal solves a problem this
project does not currently have evidence of having. Revisit only if the §7 measurement shows a residual class of
silent errors that neither the deterministic checks nor a `tables[]` repair catches, and only with the ~10 USD/year
marginal cost noted in the recommendation as the actual bar to clear (RESEARCH §8: the cost difference between
architectures is single-digit dollars per year — it is not the constraint).

---

## 4. Where Etapas 3 plugs into the Etapas 1/2 gates

**Nothing in Etapas 1 or Etapas 2 is touched by Etapas 3 except the values that feed it.** Concretely, every gate
below stays exactly as it is; Etapas 3 only changes what `ExpenseOcrService.ProcessAsync` (or its (c)/(a)/(b)
replacement) puts into `OcrResultDto` before these run:

| Gate | Lives in | Etapas 3's relationship to it |
|---|---|---|
| EN 16931 arithmetic (BR-CO-10/13/15/16) | `Services/Validation/En16931TotalsValidator.cs:100-230`, wired via `ExpenseService.RecomputeValidationFlags` | Unchanged. If a `tables[]` repair changes a line's net, BR-CO-10 re-runs against the *new* net — this is the regression oracle §7 uses: a correct repair should *reduce* `AMOUNT_MISMATCH` counts on documents like EGO/UTA PL, never increase them elsewhere |
| Line-amount plausibility | `Services/Validation/LineAmountPlausibilityRule.cs:41-…` | Unchanged; same relationship |
| Locale-number detection (`NUMBER_MISREAD`/`NUMBER_AMBIGUOUS`) | `Services/Validation/LocaleNumberCandidates.cs`, `Services/OcrNumberReads.cs` | This is the mechanism a `tables[]` repair should make **less necessary**, not replace: if the repaired line net already matches the printed text, `LocaleNumberCandidates.Check` reports `Match`, not `Misread`/`Ambiguous`. The detection stays as a safety net regardless of which extraction option is chosen — it is orthogonal to *where* the number came from |
| Supplier cascade (Etapas 2) | `Services/Validation/SupplierMatcher.cs`, `Services/SupplierMatching.cs`, `Services/SupplierAliases.cs` | Unchanged; it consumes `SupplierName`/`SupplierVatCode`/`SupplierCompanyCode`/`SupplierBankAccount` from the same `OcrResultDto` shape. A better extraction only changes how *reliably* those fields are populated — the matcher's tiers, contradiction rule and alias learning are untouched |
| IBAN / VAT-code format gates | `Services/Validation/IbanValidator.cs`, `Services/Validation/VatCodeFormatValidator.cs` | Unchanged |
| Gate 3 (no invoice leaves `PENDING_SUPPLIER` without a supplier) | `ExpenseService.ApproveAsync` et al. (Etapas 2 S1) | Unchanged |
| ZERO_VAT | Currently just `VatRate == 0 && AmountInclVat > 0` (`ExpenseOcrService.cs:575`), a plain review flag (`ExpenseService.cs:1984`, in `HasReviewFlag`'s list, which still lacks a formulation check) | **This is the one flag Etapas 3 is actually scoped to change the *logic* of**, not just the inputs — see §5 |

The practical rule for every Etapas 3 session: a change is in scope if it changes what goes *into* `OcrResultDto`
(extraction) or adds a check that only extraction can run (the raw-text ZERO_VAT search, §5); it is out of scope the
moment it would touch a file under `Services/Validation/*Validator.cs` / `*Rule.cs`'s existing logic, `SupplierMatcher.cs`,
or any Etapas 1/2 status-decision code. `ExpenseOcrService.cs` itself is **not** frozen (Etapas 2 S2c already edited
it extensively, D-045); `Docs/FROZEN.md` §5 (`OcrQueueWorker.cs` — do not touch, and keep the
`ExtractInvoiceDataAsync` alias `IExpenseOcrService.cs:10` it calls) and §6 (`ViesService.cs` — do not touch) are the
only frozen files this stage can reach at all, and neither needs to change for any option in §1.

---

## 5. ZERO_VAT per D-026

**Current state:** `ZERO_VAT` is set whenever `VatRate == 0 && AmountInclVat > 0`
(`ExpenseOcrService.cs:575`) and stays a plain review flag with no formulation check
(`ExpenseService.cs:1984`, in `HasReviewFlag`'s list). D-026 (2026-09-25) requires a text search for the legal-basis
formulation, keyed by **transaction type**, not one universal string:

- intra-Community supply (Directive 226(11)) — a reference to the exemption (e.g. "steuerfreie innergemeinschaftliche
  Lieferung", "Art. 138");
- reverse charge (226(11a)) — "Reverse charge" or the national equivalent;
- other exemptions — a reference to the specific exemption basis.

The first *real* example, found on staging 2026-09-25 (`OPEN-QUESTIONS.md` Q-009): AB Artea, an interest statement,
0 % VAT: **„Finansinių paslaugų teikimas - PVM įstatymo 28 straipsnis, PVM5."** — a PVMĮ article reference plus an
i.SAF VAT-classifier code (`PVM5`). Q-009 itself notes the code (`PVMx`) may be a more reliable signal than free text.

**Implementation, once wired (this reuses the exact pattern `SupplierCompanyCodeExtractor` already established for
reading raw text instead of a typed field, §0.2):**

1. A new pure class, `Services/Validation/ZeroVatFormulationExtractor.cs` (name to be confirmed in session), taking
   the document's raw text (`analyzeResult.content`, the same string `SupplierCompanyCodeExtractor.Extract`
   already receives) and the transaction-type hint (supplier's country vs the buyer's), reusing the rate gate's own
   country resolution rather than inventing a second one: `ResolveRateCountry` (`ExpenseService.cs:2084-2085`)
   already prefers the assigned partner's stored `BusinessPartners.CountryCode` (via `GetPartnerCountryAsync`) once
   a supplier exists, and falls back to the document's own country otherwise — the same fallback value that
   `CountryCodeResolver.FromAddress` populates into `pending_supplier_country_code` at OCR time
   (`ExpenseService.cs:2454`, `:2672`) for an invoice with no supplier yet. Returning: formulation found / not
   found, and which pattern matched (for the audit trail).
2. Language coverage for the search list is an **open question** (Q-009 status: "atviras. Atsakyti prieš Etapą 3"),
   not yet answered for DE/LV/EE/PL/RO/UA — §6 below covers how to close it with real formulations rather than
   guesses, the same discipline D-045 applied to company-code label vocabulary.
3. Flag semantics change, but the *flag name* does not: `ZERO_VAT` found-a-formulation closes (becomes information,
   matching D-026's "vėliavėlė užsidaro"); not-found stays a review flag, now for a *specific, actionable* reason
   ("PVM 0%, teisinio pagrindo formuluotė nerasta") instead of "PVM 0%" alone. This changes `HasReviewFlag`'s
   classification (`ExpenseService.cs:1984` currently treats every `ZeroVat` occurrence as review) to depend on a new,
   second flag or an explicit sub-state — the exact mechanism (new flag `ZERO_VAT_NO_BASIS` vs. reusing `ZeroVat`
   with an attached reason string) is a session-time design choice against the existing `OcrFlag` pattern
   (`Services/Dtos/OcrResultDto.cs:139-…`), not decided here.
4. **Frozen exemption preserved:** ULAK invoices (FROZEN §4) never carry this check — VAT there is always 6 %, never
   0 %, so the gate structurally never fires for them; no special-casing needed in the new code, only a test proving
   it (§8).
5. CJEU C-247/21's "cannot be corrected retroactively" holding (RESEARCH §3) argues for the check running at
   **extraction time** (create/re-OCR), not only at approval — consistent with D-022's "gates before extraction
   quality" and with where every other Etapas 1/2 check already runs.

**What the accountant must provide before this can ship (mirrors Q-009's own "Kaip atsakyti"):** the correct LT
formulation(s) actually seen in practice (not "Atvirkštinis apmokestinimas" as an unverified guess, RESEARCH §3), and
confirmation of the `PVMx` i.SAF-classifier-code approach as a valid, sufficient signal on its own.

---

## 6. Q-006 and Q-007 — answering them before building

**Q-006 (LT B2B e-invoicing mandatory dates).** RESEARCH could not confirm the 2025-07 (XML on demand) / 2027 (all
VAT payers) dates from any primary source (RESEARCH §11). If confirmed true, some suppliers will send structured
data directly and OCR becomes unnecessary *for those suppliers* — which changes how much extraction investment is
worth making now. **How to answer:** the VMI primary source directly (`vmi.lt`, e-invoicing/i.SAF-t pages), not a
secondary aggregator — RESEARCH's own attempted source (`ecosio.com`) explicitly said it did not have this
information. **Effect on the plan if confirmed:** does not change the recommendation in §1 (option (c) has near-zero
sunk cost either way), but would argue for **not** building (a)/(b) at all if the mandatory date is close and the
supplier base is mostly domestic — worth re-checking before committing to Etapas 3 session 2+ (the LLM-repair
sessions, if (c) proves insufficient).

**Q-007 (Veryfi free-tier fit).** ≤100 ops/month covers this project's volume (35 invoices/month) with headroom.
**How to answer, in order:** (1) read Veryfi's documentation specifically for LT/DE/RO/LV/EE/UA language support —
RESEARCH found no vendor's language coverage verified for any of the six commercial options (§8, §11); if the
documentation does not confirm it, stop here and record "unconfirmed" rather than guessing. (2) Only if (1) confirms
plausible coverage: **with the owner's explicit approval** (this is sending real invoice documents, some containing
personal data per D-041, to a third-party service — an external-service authorisation, not a code change), run a
one-day test on 10–15 real documents already in `~/NordicBeesERP-corpus/` or newly selected by the owner, spanning
the languages actually in the supplier base (§0.5's failure classes are LT/PL/DE-shaped; the corpus's own manifest
lists the source PDFs). Compare Veryfi's line-level output against the same documents' known-correct values (from
the §2 labelling process, once it exists) or, absent labels yet, against manual reading of the PDF. **Effect on the
plan if Veryfi handles European number formats and the six languages well:** it becomes a candidate to *replace*
Azure DI entirely for the free tier's volume, which would remove the `tables[]`/repair question altogether — worth
deciding before investing in option (a)/(b), cheap because the test itself costs nothing (free tier) beyond the
owner's approval to send documents out and the time to compare results.

*(Q-008, Azure pricing from a primary source, is explicitly marked in `OPEN-QUESTIONS.md` as not decision-relevant —
the RESEARCH §8 architectures differ by ~12 USD/year regardless — and is not repeated here as a precondition for
Etapas 3; it can be checked whenever the owner needs a real budget line, independent of this plan.)*

---

## 7. Measurement

### 7.1 The D-031 criterion 3 test (40 dev + 20 hold-out, 0 silent errors on hold-out)

**Source of the 60 documents:** the Etapas 2 clean-start re-upload (§2) — not the 11-document corpus, which is too
small and already spent on D-041's locale-detection measurement. Split: 40 dev (used while building/tuning whichever
option §1 lands on), 20 hold-out (touched only once, at the end, to report the number D-031 asks for). RESEARCH §7's
own honesty applies unchanged here: "is 60 enough" is not a cited industry figure, it is this project's own
first-principles choice, and it measures *this project's* document mix, not a general claim.

**Running it, concretely:**

1. For each of the 60 chosen invoices (their `ocr_raw_json` already exists from the clean-start upload — no new
   Azure calls needed for repeated runs against the *same* stored response), the owner (or the accountant, split by
   who has time) labels the fields listed in §2 against the actual PDF.
2. A test project method (xUnit `[Theory]` over the label file, `Tests/NordicBeesERP.Tests/…`, new file — pattern
   matches `OcrFixtures.cs`'s existing corpus-gated tests) re-runs the *current* extraction code (whichever of §1's
   options is live) against each stored `ocr_raw_json` and compares every labelled field.
3. **Dev set:** used iteratively while the chosen option is built — this is where disagreements get root-caused and
   either the extraction or the label gets corrected (never silently accept a match without checking which was
   wrong first, D-016's own discipline).
4. **Hold-out set:** run exactly once, after the dev set shows 0 silent errors, and the money-field result (not
   confidence, not "close enough" — RESEARCH §7's "silent error" is a money value a human did not need to correct
   but that was actually wrong) is what gets reported per D-031 criterion 3.

### 7.2 Golden-file regression (what Etapas 4 builds on)

**Not installed yet** — confirmed 2026-09-27: no `Verify`/`ApprovalTests` package reference in
`NordicBeesERP.csproj` or the test project; `Tests/NordicBeesERP.Tests/OcrFixtures.cs` is hand-built fixture
*generation* (JSON string builders for unit tests), not a snapshot/approval framework. Etapas 3's own session should
add this — it is the mechanism that stops a *later* extraction change from silently regressing a document this
stage already fixed, and per RESEARCH §7 the snapshot must be the **extracted and normalised field set**, not the
raw Azure JSON (a snapshot of the raw response would only prove "Azure didn't change", not "my mapping is still
correct").

1. Add `Verify.Xunit` (RESEARCH §7's recommendation; first-class .NET support, xUnit-native) to the test project.
2. One golden file per corpus/clean-start document that has been human-labelled (§2): input = the stored
   `ocr_raw_json`, output = the normalised `OcrResultDto` (or a projection of it — header totals, VAT rate, each
   line's net/quantity/description, supplier identifiers) run through whichever extraction option is live.
3. **The explicit anti-pattern to avoid** (RESEARCH §7's own warning): never "accept" a received snapshot that
   disagrees with a document's human label just to make the test pass. A red golden-file test on a labelled document
   is a real regression; a red test on an *unlabelled* one is new information to label, not a hurdle to clear.
4. The rule for new documents (RESEARCH §7): every time a genuinely new supplier or a noticeably different template
   appears in production, one real invoice from it is added to the hold-out set — the fixed 60 does not protect
   against drift by itself.

---

## 8. Sessions, tests, staging checks, estimate, open questions

### 8.1 Sessions

| # | Session | Content | Files | Frozen conflicts | Depends on | Est. |
|---|---|---|---|---|---|---|
| **S1** | **D-016 closure + corpus tooling** (no product code) | Close D-016 formally (STATE.md entry); a one-off script over the 11-document corpus checking `tables[]` presence/shape against the LT/DE/RO/LV/EE header vocabulary (§2); write up the numbers (no personal data beyond field names/presence counts, D-041's own discipline) | new one-off script (not shipped), `STATE.md`, a new `.opencode/reports/…` | none | the Etapas 2 clean-start having run (§2) is NOT required for S1 itself (S1 only touches the existing 11-doc corpus) | 3–5 h |
| **S2** | **Golden-file harness** (§7.2) | Add `Verify.Xunit`; the snapshot shape (normalised `OcrResultDto` projection); wire it against whatever documents are already labelled from S1's corpus check (a handful, not the full 60 yet) | `Tests/NordicBeesERP.Tests/*.csproj`, new test files, new `.received`/`.verified` fixture files | none | S1 | 6–10 h |
| **S3** | **Option (c): deterministic `tables[]` repair** | Parse `analyzeResult.tables[]`; match rows to `Items` by column-header vocabulary; when a table row reconciles against the header (via the existing `En16931TotalsValidator`) and the corresponding `Items` line does not, prefer the table's values for that line; new pure class + wiring into `ExpenseOcrService.cs`'s line loop (§0.3); golden-file cases for ASF0021438-shape documents | `Services/Validation/…` (new), `Services/ExpenseOcrService.cs` (line-extraction block only, §0.3) | none (`ExpenseOcrService.cs` not frozen) | S2 | 10–14 h |
| **S4** | **ZERO_VAT formulation check** (§5) | `ZeroVatFormulationExtractor` (raw-text search, transaction-type-aware); wiring into the flag/status logic; the accountant's confirmed formulation list is a **precondition for shipping**, not for building the mechanism — the class can be built and tested against synthetic text before the real formulations are confirmed, then the search list is swapped in | `Services/Validation/…` (new), `Services/ExpenseOcrService.cs`, `Services/ExpenseService.cs` (`HasReviewFlag` classification), `Helpers/ExpenseStatusHelper.cs` (new flag/label if a new constant is chosen) | none | Q-009 formulation list from the accountant (can start before it lands, cannot ship without it) | 6–9 h |
| **S5** | **Dev/hold-out measurement** (§7.1) | Build the 40+20 label file from the clean-start's real invoices; run it against S3's (and, if shipped, S4's) code; root-cause every dev-set disagreement; report the hold-out number per D-031 criterion 3 | new label file (outside git), a new xUnit theory test, a `.opencode/reports/…` write-up | none | the Etapas 2 clean start having completed on staging (D-045 step 2) | 10–16 h (mostly the owner's/accountant's labelling time, not agent time) |
| **S6** | **Q-006/Q-007 answers** (§6), and the option re-decision | VMI primary-source check for Q-006; Veryfi documentation check + (if the owner approves sending documents out) the one-day test for Q-007; write up whether (a)/(b) is still worth pursuing given S5's numbers | `Docs/ocr-rebuild/OPEN-QUESTIONS.md` (close Q-006/Q-007), a new `.opencode/reports/…` | none | S5 | 4–8 h + the owner's approval step for any external test |

Order: S1 → S2 → S3 → S5 can start once the Etapas 2 clean start is done (independent of S4); S4 in parallel with S3
(new files, no shared code path until the final wiring step); S6 last, since it needs S5's numbers to be useful.
**Total ≈ 39–62 h** of agent work plus the owner's/accountant's labelling and confirmation time — in the same range
RESEARCH §10 estimated for "Etapas 3" (~35 h) plus this plan's added S1/S2/S6, which RESEARCH's estimate did not
itemise separately.

### 8.2 Tests (pattern: table-driven pure-function tests for the new classes, real-dev-DB integration tests only where a
schema or service call is involved — this stage needs none of the latter)

- **S1:** a script's output is a report, not shippable code — no unit tests; the report itself is the "test" (does
  the corpus have usable tables, yes/no, per document).
- **S2:** golden-file infrastructure tests itself only in the sense that a deliberately-wrong extraction must produce
  a red diff — one smoke test proving the harness catches a known-bad snapshot.
- **S3:** table-row-to-`Items`-line matching (header vocabulary in each of LT/DE/RO/LV/EE/PL); the "prefer the table"
  rule fires only when the table's row reconciles and the `Items` line does not (never override a correct `Items`
  line); ASF0021438's own numbers as a fixture (`OcrFixtures.cs`-style, no corpus dependency for the *unit* tests);
  the corpus-dependent golden-file cases (skipped without the corpus, same pattern as the existing S2c test).
- **S4:** each transaction type's formulation list, table-driven, across the confirmed languages; the ULAK exemption
  (FROZEN §4) never triggers the check; a document with the `PVMx` classifier code alone (no free text) still
  closes the flag if Q-009's confirmation says the code is sufficient; a 0 %-VAT document with neither closes it as
  review, with a message naming the missing formulation (not a bare "ZERO_VAT").
- **S5:** not a code test in the usual sense — the "test" is the hold-out run itself; the artifact is the report,
  matching RESEARCH §7's own framing (the number is the deliverable, not a passing xUnit assertion).
- **S6:** no code.

### 8.3 Staging checks (S3/S4's shape, written properly once the sessions land — sketched here so the estimate above
accounts for it)

Per PLAN-ETAPAS1/2's own pattern: one document that must change (a table-repair case, a ZERO_VAT formulation case)
and one that must not (a clean document, an already-correct line), on create and re-OCR. Because Etapas 2's clean
start (D-045) will have already re-uploaded the real corpus by the time S3/S4 reach staging, the "every corpus PDF
is already in production → duplicate" caveat from Etapas 1/2's own staging checks applies again: verify via re-OCR,
not fresh upload, for anything already staged.

### 8.4 What could not be verified in this plan

- **No LT/Baltic invoice extraction benchmark exists for any option** (RESEARCH §2, §11) — every accuracy claim in
  §1 is either mechanism-level (ASF0021438's `tables[]` genuinely had the right data) or literature from a different
  document population.
- **The local rig's actual availability rate** — two outages are known (one from a prior session's report, one
  named in this session's authorising message); no measured uptime percentage exists to turn "risky" into a number.
- **"Union Tank" as a third gross-column-lines case** (§0.5) — named directly in this session's task, not
  independently found in the documents read.
- **Whether a `tables[]` repair (option c) alone would have closed the ASF0021438/EGO/UTA PL/DOLABELS cases** —
  plausible from reading the responses' structure (a labelled `columnHeader` exists for at least ASF0021438), not
  proven by running code against them in this session.
- **VMI's actual e-invoicing mandate dates (Q-006)** and **Veryfi's language coverage (Q-007)** — both explicitly
  left open by this plan for the owner/accountant to close per §6, not answered here.
- **The exact mechanism for closing `ZERO_VAT`** (a new flag vs. an attached reason on the existing one) — a
  session-time design choice, not decided in this planning pass.
- **Azure OpenAI/local-LLM token-cost and latency numbers specific to this project's real invoices** — RESEARCH §8's
  figures are engineering estimates (3 500 in / 800 out tokens per 2-page invoice), not measured against this
  project's actual documents.

### 8.5 Open questions for the owner

- **OQ-1 — Which option to build first.** This plan recommends (c) (deterministic `tables[]` repair) as the sole
  Etapas 3 session, deferring the (a)/(b) choice to after §7's measurement. Confirm, or direct a different order.
- **OQ-2 — The local rig for OCR.** Given the two known outages, is running production OCR extraction through
  `100.110.26.80`'s `llama-server` acceptable, or does that machine stay scoped to the coding harness only? This
  gates whether option (b) is ever worth prototyping at all, independent of §7's numbers.
- **OQ-3 — Veryfi test approval (Q-007).** If Veryfi's documentation confirms plausible European-language coverage,
  is sending 10–15 real (some personal-data-bearing) invoice documents to a third party for a one-day test
  approved? Without this the option stays permanently unevaluated (RESEARCH itself could not verify it).
- **OQ-4 — ZERO_VAT closing mechanism.** New flag (`ZERO_VAT_NO_BASIS` or similar) vs. an attached reason on the
  existing `ZeroVat` flag — a UI/audit-trail preference, not a technical constraint either way.
- **OQ-5 — Accountant's confirmed formulation list (Q-009).** Which languages actually need coverage now (the
  supplier base per D-039 item 3 is LT/LI(sic)/RO/CZ/PL/ES-heavy — DE/LV/EE/UA may have zero real documents to test
  against) — confirm the priority order rather than building all six up front.
- **OQ-6 — Labelling capacity for S5.** 60 invoices' worth of field-by-field verification is real accountant/owner
  time (RESEARCH §7's own honest framing: "the labelling load, not the code, is Etapas 3's actual cost"). Confirm
  who does it and on what timeline before S5 is scheduled.
