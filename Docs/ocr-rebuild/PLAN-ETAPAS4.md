# Etapas 4 — plan (measurement and control; for review before any code)

## Contents

- [0. Scope and what already exists](#0-scope-and-what-already-exists)
- [1. Criterion 3 — the 40 dev + 20 hold-out labelled set](#1-criterion-3--the-40-dev--20-hold-out-labelled-set)
- [2. Criterion 4 — golden-file regression on the full labelled set](#2-criterion-4--golden-file-regression-on-the-full-labelled-set)
- [3. Criterion 5 — the production audit](#3-criterion-5--the-production-audit)
- [4. Criterion 6 — review queue aging](#4-criterion-6--review-queue-aging)
- [5. The weekly summary — five numbers](#5-the-weekly-summary--five-numbers)
- [6. What "module done" looks like end-to-end](#6-what-module-done-looks-like-end-to-end)
- [7. Sessions, tests, estimate, open questions](#7-sessions-tests-estimate-open-questions)

---

## 0. Scope and what already exists

D-031 (2026-09-26) names six criteria for "module done". Criteria 1–2 (hard gates working; supplier
found without guessing) are Etapas 0–2's job and are covered by their own staging-check documents.
This plan covers **criteria 3–6**, which are explicitly the measurement/control layer, not extraction
mechanism — no new extraction code is in scope here (that is Etapas 3, `PLAN-ETAPAS3.md`).

**This is not starting from zero.** `PLAN-ETAPAS3.md` §7 already sketches criterion 3 (§7.1, as its own
S5 session) and criterion 4 (§7.2, golden-file harness) as the *bootstrap* — a first pass built while
Etapas 3's extraction code (S3 `TableLineRepair`, S4 `ZeroVatFormulationExtractor`) was still being
built and needed something to check itself against. This plan does not duplicate that; it names exactly
what §7.1/§7.2 already cover, and specifies the parts D-031 needs that they do not: the full 60-document
labelled set as a *standing*, repeatable measurement (not a one-off S5 run), the production audit
(criterion 5), the review-queue aging control (criterion 6), and the weekly summary that makes all of
this visible without anyone having to go looking for it (RESEARCH §7).

**The D-045/D-047 timing dependency (open, not resolved by this document).** `PLAN-ETAPAS3.md`
§7.1/§8.1 (S5) states plainly that the 60-document label set is drawn from "the Etapas 2 clean-start
re-upload" and that Etapas 3's own S5 session "depends on: the Etapas 2 clean start having completed on
staging (D-045 step 2)". D-047 (this session, `DECISIONS.md`) retimed that re-upload to "the very end,
after Etapai 3–4 code is complete" and flagged, without resolving, the resulting circularity: Etapas 3
cannot close without S5, S5 needs the corpus, the corpus needs the re-upload, and D-047 now defers the
re-upload past both Etapas 3 and 4's code. **This plan is written assuming the owner will either (a)
move the re-upload earlier (back to right after Etapas 2, as D-045 originally specified — the option
this plan's §1 default assumes), or (b) explicitly accept that criterion 3/4's real numbers cannot be
produced until the very end, in which case Etapas 3 and this Etapas 4 code all ship "unmeasured" and the
owner runs §1's labelling round as the last step before any production decision.** Either way, nothing
in §1–§5 below requires code changes once the corpus exists — the CSV export (Part C, C1) and the
aging/summary services (C2, C3) work the same regardless of when the re-upload happens. Only the
*timing* of when a real number can be reported is affected. This is flagged here, not decided.

---

## 1. Criterion 3 — the 40 dev + 20 hold-out labelled set

**Source of the documents.** Unchanged from `PLAN-ETAPAS3.md` §7.1: the Etapas 2 clean-start re-upload
(D-045), not the 11-document Azure corpus (too small, already spent on D-041's locale-detection
measurement). 40 dev, 20 hold-out, split once and never reshuffled (reshuffling after seeing results is
exactly the "approval-testing" trap RESEARCH §7 warns about — a wrong extraction silently becoming the
new "correct" answer).

**The CSV labelling round (D-046 OQ-6).** D-046 already decided the mechanism: "the owner and
accountant label after the clean start; the agent generates a CSV with the extracted values for every
invoice; people mark only what is wrong, next to the PDF." This plan specifies the CSV's shape (built in
Part C, C1):

| Column | Source | Notes |
|---|---|---|
| `invoice_id` | `expense_invoices.id` | join key back to the PDF via `files`/`IFileStore` |
| `file_name` | `expense_invoices.original_filename` (direct column, `ExpenseInvoice.cs:145-147` — no join needed) | so the labeller can open the right PDF without a DB query |
| `field` | a fixed enum: header fields (`invoice_number`, `invoice_date`, `due_date`, `amount_excl_vat`, `vat_rate`, `vat_amount`, `amount_incl_vat`, `supplier_name`, `supplier_vat_code`) + one row per line (`line_1_description`, `line_1_amount_excl_vat`, `line_1_vat_rate`, `line_1_amount_incl_vat`, `line_1_quantity`, `line_1_unit_price`, …) | matches `ExpenseInvoice`/`ExpenseInvoiceLine` (`Models/Expenses/ExpenseInvoice.cs:183-223`) column-for-column so the compare step (below) needs no translation layer |
| `extracted_value` | the current stored value for that field | what the pipeline actually saved |
| `printed_content` | the corresponding Azure DI field's raw `content` string, read from `expense_invoices.ocr_raw_json`, if that field exists in the response | not always present — Azure's field structure does not guarantee a `content` string for every derived value (e.g. a computed VAT amount); the column is blank rather than invented when absent, and this is called out in the CSV's own header row |
| `is_wrong` | blank, filled in by the labeller | the only column a human touches |
| `correct_value` | blank, filled in by the labeller **only when `is_wrong` is set** | the label |

**Where labels live.** Outside git, per every prior labelling decision in this project (D-041's corpus,
the accountant's VAT-rate-table confirmation) — this CSV contains real supplier names, amounts, and
invoice numbers, i.e. personal/commercial data under the same rule that already keeps the 11-document
Azure corpus out of the repository. Proposed path: `~/nordicbees-labels/etapas4-<date>.csv` on the
machine the owner/accountant actually work from, mirroring `~/backup/`'s existing convention for
sensitive artifacts kept beside the repo, never in it. **Not decided here — the exact path is the
owner's call** (open question OQ-1 below); C1's export command takes the path as an argument rather than
hardcoding one.

**How the hold-out is kept blind.** The 20 hold-out invoices are chosen by the same seeded-random draw
that picks the 60 in the first place (C1's `--seed` parameter, Part C), and the CSV generator does not
mark which 20 of the 60 rows are hold-out — the labeller fills in all 60 identically, not knowing which
are "the ones that count." The dev/hold-out split is applied only on the **import/compare** side (C1's
second half), after labelling is complete, using the same seed. This matches `PLAN-ETAPAS3.md` §7.1's
own discipline ("never silently accept a match without checking which was wrong first") extended to the
hold-out's actual purpose: if the labeller could see which rows were hold-out, there would be a
temptation to double-check those more carefully than the dev ones, which would bias the very number
D-031 asks for.

**What "0 silent errors in money fields" is computed as.** A silent error is a labelled row where
`field` is one of the money fields (`amount_excl_vat`, `vat_rate`, `vat_amount`, `amount_incl_vat`, and
the per-line equivalents) **and** `is_wrong = true` **and** the invoice's current status was one that
does not require a human to look at that field before the money moves (i.e. it was auto-accepted or
approved without the flag that would have caught it — cross-referenced against `expense_invoices.status`
and `ocr_flags` at the time the invoice was labelled, not at the time it was originally processed, since
the label is a point-in-time judgement against the PDF). A wrong `description` or a wrong
`supplier_vat_code` that was already flagged (`VENDOR_SUGGESTED`, `VAT_FORMAT_UNCHECKED`, …) and stopped
before approval is **not** a silent error under this definition — the gate did its job; the label is
still useful (it tells you the gate fired correctly), it is just not counted against the money-field
number D-031 criterion 3 reports. This distinction — "wrong" vs. "wrong *and unflagged*" — is the same
one RESEARCH §7 draws for the production audit (§3 below) and is kept identical on purpose so the two
numbers are comparable.

---

## 2. Criterion 4 — golden-file regression on the full labelled set

**Extends, does not replace, `PLAN-ETAPAS3.md` §7.2.** §7.2 already specifies the mechanism (`Verify.Xunit`,
snapshot = normalised `OcrResultDto` projection, never the raw Azure JSON) and the rule for new documents
(a genuinely new supplier or template adds one hold-out invoice). This plan's addition: once the 60-document
CSV round (§1) is done, every one of the 60 becomes a golden-file case, not just the "a handful" §7.2
mentions as the S2 bootstrap set. Each case's expected snapshot is generated from the **labelled**
values (not from whatever the pipeline currently outputs) for any field the labeller marked wrong — i.e.
the golden file records the *correct* answer, and a currently-red case is a known, tracked gap between
the code and the label, not silently "accepted" the way an un-labelled Verify snapshot normally would be
on first run. This is the same anti-pattern warning §7.2 already states, applied now that real labels
exist to check against instead of just "whatever the code produced."

**Snapshot location outside git.** The 60 golden-file inputs (`ocr_raw_json` per invoice) and their
expected outputs contain the same personal/commercial data as the CSV in §1 and must live beside it, not
in `Tests/NordicBeesERP.Tests/`. `Verify.Xunit`'s default behaviour writes `.verified.txt` files next to
the test source, which would put real invoice data in git — this needs an explicit `VerifySettings`
`UseDirectory(...)` pointed outside the repo (a path under the same `~/nordicbees-labels/` tree as §1's
CSV) for this specific test class. This is a real, unavoidable code change from `Verify.Xunit`'s default,
and is called out here so it is not missed when S2 (already landed per `PLAN-ETAPAS3.md`) is extended
with the full 60.

**How a new supplier or template adds a case.** Unchanged from §7.2's rule, made concrete: when
production sees an invoice from a supplier not already in the 60, or an EN 16931 layout that visibly
differs from the existing set (a new gross-column-lines case like Union Tank/EGO/UTA PL, or a new
language), one real invoice from it is added to the **hold-out** set (never dev — adding to dev after
the fact reopens the "tuning against what you're measuring" problem), labelled the same way, and the
hold-out count grows past 20 over time. The 40/20 split is a starting point, not a ceiling.

---

## 3. Criterion 5 — the production audit

RESEARCH §7's own math (rule of three) is the spec: n=60 at 0 errors → "< 5% at 95% confidence"; n=150 →
"< 2%"; n=300 → "< 1%". At ~420 documents/year, n=300 is nearly the whole year's volume, so the honest
protocol RESEARCH §7 proposes — and this plan adopts unchanged — is a **quarterly 15–20 invoice random
sample from auto-accepted (unedited) invoices**, manually checked field-by-field against the PDF, with
the confidence bound reported over a **rolling 12–24 month window**, not per quarter.

**The sampling query.** "Auto-accepted" means: reached a terminal confirmed status (`PENDING`, `PARTIAL`,
`PAID`, `OVERDUE`) without a human edit changing any money field along the way. The audit trail for this
is `expense_invoice_audit` (`Models/Expenses/ExpenseModels.cs:29-65`; `action`, `old_status`,
`new_status`, `performed_at`) — an invoice qualifies if it has no audit row with an action indicating a
manual field edit (the existing `ComputeManualEditFlags`/edit-save path, `ExpenseService.cs`, already
writes such rows for the manual-edit case per D-035) between creation and its current confirmed status.
Concretely, for a given quarter:

```sql
SELECT ei.id, ei.invoice_number, ei.invoice_date, ei.amount_incl_vat
FROM expense_invoices ei
WHERE ei.status IN ('PENDING','PARTIAL','PAID','OVERDUE')
  AND ei.created_at >= '<quarter start>' AND ei.created_at < '<quarter end>'
  AND NOT EXISTS (
    SELECT 1 FROM expense_invoice_audit a
    WHERE a.invoice_id = ei.id AND a.action = 'EDITED'
  )
ORDER BY RAND()
LIMIT 20;
```

(Confirmed against the live code during Part C, C3: `ExpenseService.cs:424` writes `Action = "EDITED"`
for a manual field edit — this was flagged unverified when this plan was first written; it is now
confirmed, not a guess.)

**The audit record.** Each quarterly run produces one row per invoice in a table this plan proposes as
`expense_audit_samples` (owner DDL, not created by this session per AGENTS.md's no-DDL-from-an-agent
rule): `id`, `invoice_id`, `quarter` (e.g. `2027-Q1`), `field`, `extracted_value`, `correct_value`,
`is_silent_error` (bool), `checked_by`, `checked_at`. This is deliberately the same shape as §1's CSV
columns, so the same import/compare code (C1) can be reused for both the one-time 60-document round and
the recurring quarterly audit — one mechanism, two call sites, not two separate tools.

---

## 4. Criterion 6 — review queue aging

**What "unresolved" means, per status.** Only the three statuses that mean "a human has not yet decided
what this invoice is" count: `PENDING_SUPPLIER`, `NEEDS_REVIEW`, `DUPLICATE_PENDING` — the same three
`ExpenseStatusHelper.NeedsAttention` already treats as attention-worthy (`Helpers/ExpenseStatusHelper.cs:144`).
`REJECTED` is excluded — it is a human decision already made (quarantined, D-027), not something still
"unresolved." Confirmed statuses (`PENDING`, `PARTIAL`, `PAID`, `OVERDUE`) are excluded for the same
reason: overdue-payment aging is a different, already-existing metric (the dashboard's aging buckets —
display at `Components/Pages/ExpensesDashboard.razor:81-131`, computed via `CalculateBucket`,
`ExpensesDashboard.razor:356-360` calling the method at `:383`), not this one.

**Since when.** Not `invoice_date` (that is business-document age, not queue age) and not blindly
`created_at` either, because an invoice can cycle back into an unresolved status after a re-OCR or a
"change supplier" action (D-045's S3 addition). The correct clock-start is: the most recent
`expense_invoice_audit` row where `new_status` equals the invoice's *current* status, i.e. when it last
*entered* that status; if no such audit row exists (the common case — an invoice's very first OCR run
lands it directly in `PENDING_SUPPLIER`/`NEEDS_REVIEW` with no audit row yet, since the audit table
records transitions, not creation), fall back to `expense_invoices.created_at`.

**Working-day calculation — proposed, not confirmed.** No existing working-day/holiday helper exists in
the codebase (checked: no file matches `WorkingDay`/`BusinessDay`/`Holiday` anywhere under version
control). This plan proposes a small `LithuanianWorkingDayCalculator` (Part C, C2) that:
1. Excludes Saturday/Sunday unconditionally.
2. Excludes Lithuania's fixed-date statutory holidays (Jan 1, Feb 16, Mar 11, May 1, Jun 24, Jul 6, Aug
   15, Nov 1, Nov 2, Dec 24, Dec 25, Dec 26) plus the two Easter-linked dates (Easter Sunday and Easter
   Monday), computed from the standard Gregorian Easter algorithm rather than a lookup table, so no
   yearly maintenance is needed.
3. **This list is written from general knowledge of Lithuanian public holidays, not verified against an
   official calendar in this session — same "NEPATVIRTINTA until confirmed" pattern this project already
   uses for the VAT rate table (D-039) and formulation lists (D-046 OQ-5).** The accountant should
   confirm the list before it governs anything visible to the owner (open question OQ-2 below). The
   mechanism is reversible either way: it is one array of dates plus one function, not wired into
   anything that would be expensive to change.

**Where it is shown.** The expenses dashboard (`/expenses`) gains a count ("N sąskaitos neišspręstos
ilgiau nei 5 d.d.") plus an expandable list, next to the existing "reikalauja dėmesio" panel
(`ExpensesDashboard.razor:139-180`) rather than replacing it — that panel already shows the three
statuses by *count*, this adds the *age* dimension the D-031 criterion actually asks about ("no invoice
unresolved more than N working days", not "no invoice unresolved"). N defaults to 5, configurable (D-031
already says "the owner can change it") — proposed via `app_settings`
(`Models/Expenses/ExpenseModels.cs:240`, the existing generic key-value settings table already used
elsewhere in this module), not a new table.

---

## 5. The weekly summary — five numbers

RESEARCH §7's own five, with each one's concrete source query against this schema:

| # | Number | Definition | Source |
|---|---|---|---|
| 1 | Processed / % auto-accepted with zero human edits | Invoices created in the window ÷ those reaching a confirmed status with no `EDITED` audit row (same predicate as §3's sampling query) | `expense_invoices` + `expense_invoice_audit` |
| 2 | Hard-gate trigger count, by gate | Count of invoices where `ocr_flags` contains each of the four hard-gate flags this week (arithmetic/`AMOUNT_MISMATCH`+`TOTALS_OUT_OF_RANGE`; duplicate/`DUPLICATE_PENDING` status; supplier/`PENDING_SUPPLIER` status; date/`STALE_DATE`+`FUTURE_DATE`) | `expense_invoices.ocr_flags` (JSON column) |
| 3 | Count unresolved longer than N working days | Exactly C2's service method (§4) | `expense_invoice_audit` + the working-day calculator |
| 4 | Field correction rate | Count of `EDITED` audit rows this week ÷ total invoices processed this week (an approximation at the invoice level, not the field level — the audit row does not currently record *which* field changed, only that an edit happened; a precise per-field rate would need `ActionDetails` (`ExpenseInvoiceAudit.ActionDetails`, free text today) to carry a structured field list, which is a follow-up, not built in this plan) | `expense_invoice_audit` |
| 5 | Rolling 12-month silent-error confidence bound | The rule-of-three bound from the last 4 quarterly audits' combined sample (§3) | `expense_audit_samples` (proposed table, owner DDL) |

**Where it is shown.** A dashboard card (Part C, C3) on `/expenses`, next to the existing KPI row
(`ExpensesDashboard.razor:22-67`) — reusing the page and the `IExpenseService`/`DbContextFactory`
injection already present there, not a new page. **Not wired to Telegram or e-mail in this plan.** The
app already has a generic Telegram group mechanism (`Docs/TELEGRAM_NOTIFICATIONS.md`;
`Telegram:Groups:<key>` config, `TelegramNotificationService.SendToGroupAsync`), so a weekly push is
mechanically simple to add later, but doing so means the owner receiving a message every week whether
they asked for it or not — that decision is explicitly left to the owner (open question OQ-3), not
assumed. The service method behind the dashboard card is written so that wiring a scheduled Telegram
send later is a one-line caller, not a redesign.

---

## 6. What "module done" looks like end-to-end

1. **Clean start** (D-045, timing per D-047 — see §0's open dependency): master-data cleanup → backup →
   delete → re-upload, on staging first, then production.
2. **Labelling** (§1): the owner/accountant label the 60-document CSV against the PDFs, off-repo.
3. **Measurement**: the dev-set disagreements get root-caused (extraction fix or label fix, never both
   silently); the hold-out set is run once; the golden-file suite (§2) is extended to all 60; the
   hold-out number is the one reported against D-031 criterion 3.
4. **Production deploy** (D-037): once Etapas 1–4 code is on staging and the criteria above are met on
   the labelled set, the final production deploy happens, followed by production's own clean start
   (D-045 step 3).
5. **Audit** (§3): the first quarterly 15–20 sample runs no earlier than one full quarter after
   production go-live (there must be auto-accepted production invoices to sample from).
6. **Review queue control** (§4) and the **weekly summary** (§5) run continuously from production
   go-live onward — these are the ongoing control layer, not one-time gates.

**Owner/accountant steps in this order:** confirm the re-upload timing (§0); label the CSV (§1); confirm
the working-day holiday list (§4); decide the weekly-summary display channel (§5); run each quarterly
audit sample (§3) going forward.

---

## 7. Sessions, tests, estimate, open questions

### 7.1 Sessions

| # | Session | Content | Files | Depends on | Est. |
|---|---|---|---|---|---|
| **C1** | Labelling CSV export/import | Export command (invoice ids or seeded random sample → CSV per §1's shape); import/compare command (filled CSV → silent-error count per §1's definition); reused as-is for §3's quarterly audit | new `Services/…` (pure, no DB writes beyond reading), a CLI entry point or admin-only trigger (no public UI) | none (works against whatever invoices already exist; more useful once the clean-start corpus exists) | 6–10 h |
| **C2** | Review-queue aging | `LithuanianWorkingDayCalculator` (pure); a service method computing unresolved-longer-than-N per §4; dashboard count + list | new `Services/…`, `Helpers/…`, `Components/Pages/ExpensesDashboard.razor` (additive) | none | 5–8 h |
| **C3** | Weekly summary | Service computing the five numbers (§5); dashboard card | new `Services/…`, `Components/Pages/ExpensesDashboard.razor` (additive) | C2 (shares the working-day calculator for number 3) | 5–8 h |
| **S1 (future, not this session)** | Golden-file extension to the full 60 | Once §1's CSV round is labelled: generate all 60 golden-file cases, wire `UseDirectory` outside git | `Tests/NordicBeesERP.Tests/…` | the clean-start re-upload having happened (§0) | 8–12 h |
| **S2 (future, not this session)** | First quarterly audit | Run the §3 sampling query for real; owner/accountant check 15–20 invoices; write the audit record | `expense_audit_samples` (owner DDL first) | one full quarter of production volume after go-live | 4–6 h + owner/accountant checking time |

C1–C3 are this session's Part C scope. S1/S2 are named here for completeness (they are literally what
"module done" in §6 requires) but are blocked on the owner's clean-start timing decision and on
production go-live respectively — not buildable as code today.

### 7.2 Tests

- **C1:** pure computation, tested with synthetic data — no real invoice data in the test project (per
  the task's own constraint and this project's established personal-data-outside-git rule). Cases: a
  field marked wrong on a flagged invoice is not counted as silent; a field marked wrong on an
  auto-accepted invoice is; the seeded random sample is deterministic (same seed → same ids); the
  dev/hold-out split is stable across repeated runs of the same seed.
- **C2:** the working-day calculator, table-driven — a holiday that falls on a weekend, a run spanning a
  holiday, the Easter calculation for a handful of known years (cross-checked by hand against publicly
  known Lithuanian Easter dates, since no external calendar package is added). The aging query itself:
  an invoice with no audit row (falls back to `created_at`), an invoice that cycled status twice (uses
  the latest transition into its current status, not the first).
- **C3:** the five-number computation against fixed, hand-built `DbTestFixture` data (real test DB,
  matching this project's existing integration-test convention, `Tests/NordicBeesERP.Tests/DbTestFixture.cs`)
  — one invoice per category needed to exercise each number at least once.

### 7.3 What could not be verified in this planning pass

- **The Lithuanian public holiday list** (§4) — written from general knowledge, not checked against an
  official source in this session.
- **Whether `expense_invoice_audit` rows exist for *every* status transition or only some** (e.g. does
  the very first OCR run that lands an invoice in `PENDING_SUPPLIER` write a row with `old_status = NULL`,
  or no row at all?) — §4's "since when" fallback to `created_at` assumes the latter; not traced through
  every write call site in this pass.
- **Whether `app_settings` (proposed home for the configurable N in §4) already has a per-module
  namespacing convention** that this plan should follow, or whether a flat key is fine — not checked
  against every existing `app_settings` key in this pass.
- **The owner's preference for where labelled CSVs and golden-file snapshots physically live** (§1, §2)
  — `~/nordicbees-labels/` is this plan's proposal, matching `~/backup/`'s existing convention, not
  confirmed.

### 7.4 Open questions for the owner

- **OQ-1 — Where do labelled CSVs and golden-file snapshots live on disk?** This plan proposes
  `~/nordicbees-labels/`, mirroring the existing `~/backup/` convention for sensitive data kept outside
  git. Confirm or redirect.
- **OQ-2 — Confirm (or correct) the Lithuanian public holiday list in §4** before it governs the
  review-queue aging count that anyone will actually look at.
- **OQ-3 — Weekly summary display channel.** Dashboard card only (this plan's default), or also pushed
  to a Telegram group (mechanism already exists, `Docs/TELEGRAM_NOTIFICATIONS.md`) or e-mail? If
  Telegram, which group key, and does the owner want it weekly regardless of whether anything changed?
- **OQ-4 — The D-045/D-047 re-upload timing** (§0) — resolve whether the clean-start re-upload happens
  right after Etapas 2 (as D-045 originally specified, needed for `PLAN-ETAPAS3.md` S5/this plan's §1 to
  produce real numbers before Etapas 4 code ships) or at the very end (D-047, this session), accepting
  that criteria 3/4 stay unmeasured until then.
- **OQ-5 — `expense_audit_samples`' DDL** (§3) is proposed, not created (no DDL from an agent, per
  AGENTS.md). When the owner is ready to start quarterly audits, this table needs creating manually; the
  exact `CREATE TABLE` statement can be written on request once OQ-1's storage location is confirmed
  (the table itself has no personal data — it stores ids and computed booleans — so it can live in the
  regular schema, unlike the CSVs/snapshots).
