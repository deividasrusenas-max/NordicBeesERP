# Etapas 2 — plan (supplier recognition; for review before any code)

Written 2026-09-27 by Claude Code for the owner and the planning advisor. **Nothing here is
implemented.** Base: `analysis/RESEARCH-2026-09-25-reliability.md` §4 (supplier recognition), §6 gate 3
and §10 (Etapas 2, items 9–11), D-017 (a loud "not found" over a silent guess; aliases only from an
explicit human action), D-022, D-027, D-031 (criterion 2: "the supplier is found without guessing;
ambiguous → not found"), D-037, D-039 item 2, D-042/D-043, and the code at `main` `1cfa577`
(v0.17.94 + the docs commits). Every `file:line` was read in this session. Claims that come from reading
the code and were **not executed** are marked *(read, not run)*. Where something could not be checked,
it says so (§7.4).

Deploy context (D-037): nothing from this plan goes to production until Etapai 1–4 are done and verified
on staging. Etapas 2 is closed on **staging**.

## Contents

0. How supplier assignment works today
1. The cascade, tier by tier
2. Alias table (learning from corrections)
3. The supplier hard gate (RESEARCH §6 gate 3)
4. Registries
5. Data: staging queries for the owner
6. Interaction with Etapas 0 / 1
7. Sessions, tests, staging checks, estimate, what could not be verified, open questions

---

## 0. How supplier assignment works today

### 0.1 Where a supplier gets set (five live paths + one apparently dead)

| # | Path | Code | What it does |
|---|---|---|---|
| A | OCR upload — **two** matcher calls | `ExpenseOcrService.ProcessAsync` → `ResolveSupplierAsync` (`:574`, `:726-797`) → `FindSupplierIdAsync` (`:794`); **and again** in the upload dialog (`ExpenseUploadDialog.razor:724-726`) | The dialog's call wins: `_supplierId` is what `SyncOcrResultFromUi` writes back (`:1030`) and what `CreateFromOcrAsync` stores (`ExpenseService.cs:2121`). The dialog passes the raw `result.SupplierVatCode`, so it does **not** get `ProcessAsync`'s "malformed VAT is never used to match" guard (`:732-735`, `:794`). *(read, not run)* |
| B | Re-OCR | `UpdateFromOcrAsync` (`ExpenseService.cs:2204`), same dialog | Writes `supplier_id = ocrResult.SupplierId` (`:2291`, `:2321`) — a **fresh** match, possibly `null`. A supplier a human assigned earlier is replaced unless the cascade finds it again; nothing carries it over. *(read, not run)* |
| C | Manual assign | `InvoiceDetailDialog.razor:852-876` → `AssignSupplierAsync` (`ExpenseService.cs:1377-1415`) | Shown only for `PENDING_SUPPLIER && !_supplierFound` (`:119`). Removes `VENDOR_NOT_FOUND`, recomputes rate flags with the partner's country (`:1392`, `:1428-1436`), status by `StatusAfterSupplierAssigned` (`:1394`, `:1687`). Audit row `SUPPLIER_ASSIGNED` with details `Tiekėjo ID: {id}` only (`:1407-1413`) — no tier, no matched string. |
| D | Create supplier from the invoice | `InvoiceDetailDialog.razor:818-850` → `SupplierCreateDialog` → `AutoAssignSupplierAsync` (`:839`; `ExpenseService.cs:1438-1530`) | Bulk-assigns **every** `PENDING_SUPPLIER` invoice whose pending VAT (trim + upper-case equality, no other normalisation) **or** pending name (exact after trim) equals the just-used values (`:1451-1463`). |
| E | Edit form | — | Cannot change the supplier: `invoice.SupplierId = stored.SupplierId` (`ExpenseService.cs:462`; `UpdateInvoiceAsync` comment `:301`). |

| F | `Components/Dialogs/AssignSupplierDialog.razor` (apparently **dead**) | Calls `AssignSupplierAsync` (`:258`, `:285`). After "create supplier" it runs its own ad-hoc matcher (`:209-254`): exact VAT, exact name, then `_suppliers.OrderByDescending(s => s.Id).First()` (`:232`, `:237`, `:249`, `:254`) — the **newest partner, silently**: the exact D-017 failure shape. Opened only from `ExpenseInvoices.razor:598-601` (`OpenAssignSupplierDialog`); a grep of `Components` finds no caller of that method (2026-09-27). *(read, not run)* S1 deletes it or routes it through the assign guard and the new matcher (Q16). |

`ExpenseOcrService.ProcessAsync` is also what `OcrQueueWorker` calls (`OcrQueueWorker.cs:77`; FROZEN §5 separately
requires the `ExtractInvoiceDataAsync` alias to stay) — dead today, but a revived worker would get whatever matcher we build.

### 0.2 What `FindSupplierIdAsync` does (`ExpenseOcrService.cs:819-877`, after Etapas 0 commit 1 `5e44f7c`)

1. Normalises the document VAT: drop **spaces only**, trim, upper-case (`:823`). (`CleanVatCode` at `:882-886`
   already dropped `-` and `.` on the OCR path, but the dialog and any other caller do not go through it.)
2. If non-empty: `WHERE vat_code IS NOT NULL AND vat_code <> '' AND (vat_code = v OR vat_code = 'LT'+v OR vat_code = v-without-leading-LT)`
   (`:837-847`). Consequences *(read, not run)*:
   - **The stored side is not normalised.** A partner stored as `LT 123456789`, `lt123456789` (fine — collation is
     case-insensitive) or `LT-123456789` (not fine) does not match.
   - **`LT` is assumed.** A digits-only document code matches a partner stored as `LT`+digits, and the reverse.
     A 9-digit code from a DE or EE invoice can therefore match an LT partner with the same 9 digits. This is a
     guess dressed as normalisation.
   - No role filter, no `is_active` filter: customers and deactivated partners match (`:840-847`; the manual
     picker uses `IsActive` only, `InvoiceDetailDialog.razor:664-667`).
3. `Take(2)`; two hits → `(null, null)` with only a `LogDebug` (`:849-853`). The caller cannot tell "ambiguous"
   from "not found": both become `VENDOR_NOT_FOUND` (`ExpenseOcrService.cs:602-604`), the human sees no candidates.
4. **If VAT gave no hit — including when the document has a VAT code that no partner has — it falls through to
   an exact-name match** (`:858-874`), also `Take(2)`, two hits → `(null, null)`. So a partner with the same name
   but a **different** VAT is assigned silently, and the document's own VAT/IBAN/code are then dropped (§0.4).
   This is exactly the "found, and it is the wrong supplier" shape D-017 warns about (bounded here to
   "same exact name", but still unflagged).
5. Name equality is the MySQL collation: `business_partners.name` is `utf8mb4_unicode_ci`
   (`Migrations/20260602150000_InitialCreate.cs:136-137`; confirmed with a read-only `information_schema` query
   on the **dev** DB 2026-09-27 for name, vat_code, company_code, bank_account, country_code — **staging not
   checked**, §7.4). That collation is case- **and accent-insensitive** and ignores trailing spaces, so today's
   "exact" name match already folds `Ą/A`, `UAB rotoma/UAB Rotoma`. It does **not** strip legal forms or
   punctuation.
6. Before matching, `ResolveSupplierAsync` may replace the name: VIES name overrides the OCR name (`:764-769`),
   own-company keeps it (`:757-763`), then `CompanyNameHelper.Normalize` expands long legal forms at the start to
   abbreviations (`:785-790`; `Helpers/CompanyNameHelper.cs:9-56`) — abbreviates only; no diacritic folding, no
   legal-form stripping.
7. Not used at all for matching: company code, IBAN, phone, email.

### 0.3 What the OCR step puts in the identifier fields

- **`SupplierCompanyCode` is not reliably a registration number** (`ExpenseOcrService.cs:211-270`): (1) the whole
  cleaned `VendorTaxId` — i.e. the **VAT code including its country prefix** (`:216-230`); else (2)
  `VendorBusinessNumber` (`:234-243`); else (3) the **full `VendorAddressRecipient` string — a name** (`:246-255`);
  else (4) an LT-only 9-digit regex over the recipient (`:258-270`). Tier 2 (company code) cannot be built on this
  value as it is.
- **`SupplierBankAccount`**: the `IBAN` (else `AccountNumber`) of the **first** `PaymentDetails` entry only
  (`:273-286`); no normalisation. Validation happens later (`IbanValidator`, `INVALID_IBAN`, Etapas 1).
- `SupplierVatCode`: `VendorTaxId` through `CleanVatCode` (`:171-174`, `:882-886`).
- `SupplierPhone` / `SupplierEmail` exist on the DTO (`Services/Dtos/OcrResultDto.cs:18-19`); not persisted on the
  invoice and not used for matching.

### 0.4 What is stored on the invoice, and what is lost

`CreateFromOcrAsync` (`:2127-2134`) and `UpdateFromOcrAsync` (`:2322-2329`) store the `pending_supplier_*` fields
(name, VAT, address, city, postal code, country, company code, bank account) **only when no supplier was matched**;
with a match they are `NULL`. So for a matched invoice the document's VAT, company code and **IBAN are stored
nowhere** (the comment at `ExpenseService.cs:542-543` says so). `AssignSupplierAsync` does **not** clear them
(`:1397-1404`), so a manually assigned invoice keeps its pending name/VAT/IBAN — useful seed data for §2 (§5 query 9).

### 0.5 The partner model (`Models/Models_Part1.cs:112-250`)

`name` (`:134-137`), `company_code` varchar(50) null (`:139-141`), `vat_code` varchar(50) null (`:143-145`),
`country_code` varchar(10) default `"LT"` (`:172-174`), `bank_account` varchar(50) null — **one** IBAN column
(`:195-197`), `phone`/`email`/`invoice_email`, role flags `is_customer`/`is_supplier`/`is_expense_supplier`
(`:122-129`), `is_active` (`:234-235`), `national_id_number` (`:219-221`, personal data — not proposed as a key).
Indexes: `idx_name`, `idx_vat_code`, `idx_country`, `idx_partner_type`; **no unique index on anything but `id`**
(`InitialCreate.cs:165-169`; dev `information_schema.statistics` 2026-09-27). There is **no table** of alternative
names, known IBANs or aliases (dev DB table search for `%alias%`, `%iban%`, `%bank%`, `%supplier%` found only
`bank_import*`, `supplier_approvals` — a certificate table, not referenced from any `.cs`/`.razor` — and
`supplier_payments`).
`SaveSupplierAsync` writes `""` (not `NULL`) for a null company code, VAT and bank account on update
(`SupplierService.cs:320-321`, `:329`), and stores whatever it is given on insert (`:367-380`), so both `NULL` and
`''` occur in the data. The create dialog's save button (`SubmitForm`, `SupplierCreateDialog.razor:229`, `:340`) does
**not** run the "supplier with this name exists" check; `HandleValidSubmit` (`:445-453`) only warns and saves
nothing. So duplicate partners can be created freely *(read, not run)*. No merge tool exists (grep for
`Merge…Supplier/Partner` in Services, Components, Helpers: no hit).

### 0.6 Facts about the rest of the pipeline the cascade must fit

- Country of a supplier: `CountryCodeResolver.Resolve(vat, address)` runs **before** matching (`:782`);
  `ResolveRateCountry` already prefers the partner's stored country once a supplier is set
  (`ExpenseService.cs:1767-1768`) — D-043 in code form, for the rate gate.
- `VENDOR_NOT_FOUND`: set when `SupplierId == null` (`ExpenseOcrService.cs:602-604`), carried by the edit path while
  there is no supplier (`ExpenseService.cs:558`), removed on assign (`:1391`, `:1477`), hidden as a chip in the detail
  dialog while status is `PENDING_SUPPLIER` (`InvoiceDetailDialog.razor:48`), critical in lists
  (`ExpenseStatusHelper.cs:145`). Status precedence: `DecideOcrStatus` (`ExpenseService.cs:1678-1684`): WRONG_RECIPIENT →
  `REJECTED`; supplier null → `PENDING_SUPPLIER`; review flag → `NEEDS_REVIEW`; else `PENDING`.
- Duplicate detection is **supplier-independent**: `CheckDuplicateAsync(supplierId, supplierVatCode, number, amount, …)`
  ignores both supplier parameters (`ExpenseService.cs:1347-1371`; number normalised + amount ±0.01 + non-positive
  amount excluded).
- Partner tests today: `Tests/NordicBeesERP.Tests/ExpenseOcrServiceFindSupplierTests.cs` (7 tests, real dev DB;
  `.opencode/reports/ocr-etapas0-20260925-2354.md` §"Commit 1").

---

## 1. The cascade, tier by tier

### 1.1 Shape (proposal)

One **pure** matcher: input = the document's identifiers (VAT, company code, IBAN, name) + a snapshot of candidate
partners (id, name, vat, company code, country, known IBANs, role flags, active); output =

| Outcome | Meaning | Status effect |
|---|---|---|
| `Assigned(partner, tier)` | one partner, strong evidence, nothing contradicts | as today: `PENDING` / `NEEDS_REVIEW` by flags |
| `Assigned` + `SUPPLIER_NEW_IBAN` | as above, but the document IBAN is not among that partner's known accounts | `NEEDS_REVIEW` (new review flag) |
| `Suggested(candidates, reason)` | weaker evidence, or evidence contradicts (e.g. same name, **different VAT**) | `PENDING_SUPPLIER`, candidates shown to the human |
| `Ambiguous(candidates)` | ≥ 2 partners tie at the deciding tier | `PENDING_SUPPLIER`, candidates shown |
| `NotFound` | nothing | `PENDING_SUPPLIER` |

The rule that ties it to D-017: **automatic assignment only when the strongest evidence points at exactly one
partner and no other identifier that the document *and* the partner both carry contradicts it.** Everything else is
loud — `PENDING_SUPPLIER` with the reason and candidates — never a silent pick. Both matcher call sites
(§0.1 A) collapse into one: the dialog stops calling `FindSupplierIdAsync` and uses `result.SupplierId`
(otherwise the gate the OCR step applied is undone by the dialog).

New informational flags (Etapas 1 pattern: constants in `OcrFlag`, label, colour, classification in `HasReviewFlag`
`ExpenseService.cs:1667-1675`, `IsCriticalFlag` `ExpenseStatusHelper.cs:145`, label `:61`, colour `:98`):
`VENDOR_AMBIGUOUS` (info), `VENDOR_SUGGESTED` (info), and one **review** flag `SUPPLIER_NEW_IBAN`. All are
document facts the edit form cannot recompute, so the default carry-over rule of `ComputeManualEditFlags`
(`ExpenseService.cs:568-572`) keeps them — no change to `ManualEditOwnedFlags` (`:511-526`).

### 1.2 Tier table

| # | Key | Data today | Schema needed | Ambiguity | Auto-assign |
|---|---|---|---|---|---|
| 1 | **VAT ID**, normalised (strip whitespace, `.`, dash characters — reuse the normalisation in `VatCodeFormatValidator.Validate`, `VatCodeFormatValidator.cs:75-105`; upper-case) and compared **with country prefix** | `vat_code` on the partner (`:143-145`); document VAT from `VendorTaxId` (`:171-174`) | none. Stored side normalised **in C#** over the candidate snapshot (a few hundred rows, RESEARCH §4: "blocking not needed"), or in SQL with the REPLACE chain as `WhereNormalizedNumberEquals` does for invoice numbers (`ExpenseService.cs:1343-1345`) | ≥ 2 partners with the same normalised VAT → `Ambiguous` (duplicate master data; §5 query 7 sizes it) | **Yes**, if no contradiction (see 1.3) |
| 2 | **Company code** (registration number), digits/letters only | `company_code` on the partner (`:139-141`). The OCR value is **not** a registration number today (§0.3) | none for storage. **Extraction** must change: accept only a value that looks like a code (no country prefix, no name); optionally read `Įmonės kodas` / `Įm. k.` / `Reg. Nr.` style labels from the raw text (`ocr_raw_json` is stored). Whether Azure's `VendorBusinessNumber` is ever present on real invoices: **not verified** | same as tier 1 | **Yes** |
| 3 | **IBAN**, `IbanValidator.Validate` first (`Services/Validation/IbanValidator.cs:65`), normalised | one column `business_partners.bank_account` (`:195-197`), often empty/`''`; the document IBAN is dropped on match (§0.4) | **yes**: a known-accounts table (§1.4), and store the document IBAN also when a supplier is matched | an IBAN present on ≥ 2 partners → `Ambiguous` | **No as a sole key in Etapas 2** — see 1.5. Used (a) to **check** a tier 1/2/4 match (`SUPPLIER_NEW_IBAN`) and (b) to **suggest** |
| 4 | **Name, exact** (today's behaviour: collation-folded, §0.2 item 5) | `name` | none | ≥ 2 → `Ambiguous` | **Yes only** when the document carries **no VAT and no company code**, or all of those it carries **agree** with the partner's; **never** when the document VAT/code differs from the partner's non-empty VAT/code (that becomes `Suggested`, reason "kodai nesutampa") |
| 4b | **Name, normalised** (diacritics folded via `DiacriticHelper.Fold`, `Helpers/DiacriticHelper.cs:12`; quotes/punctuation dropped; legal-form tokens UAB/AB/MB/IĮ/VšĮ/ŽŪB, SIA, OÜ/AS, GmbH/AG/UG, SRL/SA, Sp. z o.o. stripped; `&`/`ir`/`und`/`și` unified — RESEARCH §4 tier 4) | — | none | many merges are plausible (`UAB Rotoma` ≈ `AB Rotoma` are different legal entities) → `Ambiguous` / `Suggested` | **No** — `Suggested` only (RESEARCH: "only with confirmation from tier 6 or 7"; phone/email are not extracted onto the invoice, §0.3) |
| 5 | **Fuzzy name** (RESEARCH: token-sort/set + Jaro-Winkler; ≥ 95 auto, 85–95 review, < 85 none) | — | none (pure code) | — | **Not in Etapas 2** — see 1.6 |
| 6–7 | email domain, phone | partner has them (`:178-190`); the document's are extracted but not persisted | — | — | **No** (RESEARCH: confirmatory only). Not in Etapas 2 |
| 8 | **Alias** (§2) | — | table | frozen on conflict | Yes, after promotion, with the tier-1/2 contradiction check |
| 9 | Registry enrichment | — | — | — | Not in Etapas 2 (§4) |

**Deviation from RESEARCH §4 tier 1** (which says "strip the country prefix"): the plan compares **with** the prefix,
using the partner's `country_code` only to give a prefix-less stored code its prefix. Reason: D-043 (country is
authoritative once stored) and the cross-country 9-digit collision in §0.2 item 2. Put to the owner in Q8.

### 1.3 Contradiction rule (the behaviour change to accept)

"Contradiction" = the document and the candidate partner **both** have a non-empty value for the same strong
identifier (VAT or company code) and the normalised values differ. Then the candidate is not assigned; it is
offered as `Suggested`. Today's code assigns such a candidate silently through the name fallback (§0.2 item 4).
Also: if two different strong identifiers point at **different** partners (VAT → A, company code → B) →
`Ambiguous`. Counting how often this would have fired is not possible from history (the document values are
`NULL` for matched invoices, §0.4) — it is measured from the first staging run onwards (S3 records the tier and
reason in the audit row, §7.1).

### 1.4 Known IBANs (tier 3 data) — schema proposal

`business_partners.bank_account` is a single column edited in `SupplierEditDialog`/`SupplierCreateDialog`. RESEARCH §4
("why IBAN is strong but dangerous") requires one-to-many. Proposal — **new table**, D-039 pattern (the agent writes
the model + EF migration; the owner runs the DDL on dev and staging; production at the final Etapai 1–4 deploy):

```sql
CREATE TABLE IF NOT EXISTS supplier_bank_accounts (
  id            INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  partner_id    INT NOT NULL,
  iban          VARCHAR(34) NOT NULL,              -- normalised: upper-case, no spaces; IbanValidator-valid
  source        VARCHAR(20) NOT NULL,              -- MIGRATED | MANUAL | INVOICE_CONFIRMED
  source_invoice_id INT NULL,
  is_active     TINYINT(1) NOT NULL DEFAULT 1,
  created_at    DATETIME NOT NULL,
  created_by    VARCHAR(100) NULL,
  UNIQUE KEY uq_partner_iban (partner_id, iban),
  KEY idx_iban (iban)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
```

- **No foreign key** to `business_partners` (AGENTS: no new FKs on existing tables without approval; a FK *from* a new
  table is a different case, but the rule is cheap to respect — owner may approve one, Q11).
- Backfill (owner-run): `bank_account` values that pass `IbanValidator` → rows with `source='MIGRATED'`.
  Values that do not pass stay only in the legacy column and are listed for the accountant (§5 query 5).
- `bank_account` stays as the display/primary field (`SupplierService.cs:59`, `:114`, `:176`); saving a partner with a valid
  IBAN upserts it into the table (S4). `iban` is not unique across partners on purpose: the same IBAN on two partners
  is data the matcher must see as ambiguity, not a constraint violation.
- Migration file: a **new** `Migrations/YYYYMMDDHHMMSS_*.cs` (AGENTS — `InitialCreate.cs` frozen; FROZEN §8:
  `dotnet ef migrations add`, inspect `Up()/Down()`; the S3 report of Etapas 1 shows the scaffold also emits unrelated
  snapshot churn that must be trimmed by hand).

### 1.5 IBAN — argue: suggest, do not auto-assign (deviation from RESEARCH's table)

RESEARCH tier 3 says "auto-accept, but a new IBAN for an existing supplier → hold for review". Two reasons to be
stricter in Etapas 2:

1. **There is no known-IBAN set yet.** Most partners will have an empty `bank_account`; an IBAN-only match
   would fire rarely and its first real effect would be the flag below on nearly every invoice. §5 queries 5 and 8
   size this.
2. **Factoring** (RESEARCH §4): an invoice can legitimately show a third party's IBAN; a fraudster can show his own.
   A match on IBAN alone identifies whoever owns the account — which is not necessarily the supplier.

So: IBAN **checks** a match found by tiers 1/2/4 and **suggests** candidates when nothing stronger exists; it does
not assign on its own. Switching IBAN-only to auto-assign later is a one-line policy change once §5 data exist (Q2).

`SUPPLIER_NEW_IBAN` rules (avoids alert fatigue, RESEARCH §6): raised only when the matched partner **has at least one
known account** and the (valid) document IBAN is not among them. A partner with no known account gets no flag; the
detail dialog offers "add this IBAN to the supplier" instead (Q5). Clearing: the action "Pridėti IBAN prie tiekėjo"
inserts the account (`source='INVOICE_CONFIRMED'`, `source_invoice_id`), removes the flag, writes an audit row;
PATVIRTINTI without adding leaves the flag as a record (the `NUMBER_*` pattern — `ApproveAsync` does not touch
flags, `ExpenseService.cs:1549-1556`) — the factoring case. An **invalid** document IBAN is never used as a key and
never added (`INVALID_IBAN` from Etapas 1 handles it, D-039 item 2).
Persisting the value: S3 stops nulling `pending_supplier_bank_account` when a supplier is matched
(`ExpenseService.cs:2134`, `:2329`) so the review UI knows what to add. The document-flag logic keys on
`stored.SupplierId == null` (`:544-546`), so it is unaffected. *(read, not run)*

### 1.6 Fuzzy name — argue: **not auto in Etapas 2; suggestions only, and optional**

- D-017 + D-031 criterion 2: "ambiguous → not found". A ≥ 95 auto-accept is a guess by definition; RESEARCH itself says
  the threshold must be "calibrated on your list", and no calibration data exists. The DNB source it cites is a
  Dutch central-bank matching job, not evidence for LT/RO/LV supplier names.
- The scale (a few hundred partners) makes fuzzy cheap to run, not safe to trust: `UAB Rotoma` vs `UAB Rotoma Plius`
  is the shape of error that becomes a silent wrong assignment repeated monthly (D-017's warning).
- What fuzzy **is** good for here: ranking `Suggested` candidates in the "Priskirti esamam" picker, which today is a
  substring box over a full list (`InvoiceDetailDialog.razor:809-814`). That helps the human and cannot mis-assign.
- Implementation without a new dependency: the project has none of FuzzySharp / F23.StringSimilarity
  (`NordicBeesERP.csproj:13-28`); a pure token-set + Jaro-Winkler on top of §1.2's normaliser is ≈ 60 lines and fully
  unit-testable (RESEARCH's own suggestion of packages is optional).
- Auto-assign on ≥ 95 is revisited **after** Etapas 4's weekly numbers exist (Q3).

### 1.7 What happens on ambiguity, in one line

No pick. `PENDING_SUPPLIER`, `VENDOR_AMBIGUOUS`, candidates (with the tier that produced them and each partner's
VAT/code/country) in the detail dialog next to "Sukurti tiekėją" / "Priskirti esamam" (`InvoiceDetailDialog.razor:119-154`).
Ambiguity caused by **duplicate master data** is fixed in the data (no merge tool exists, §0.5 — Q9).

### 1.8 Behaviour changes the owner must accept (§1)

1. Same name, different VAT → no longer assigned silently (§1.3).
2. `LT` is no longer assumed for a digits-only document VAT when a country is known from the address; when neither is
   known the existing LT assumption stays **unless** the owner says otherwise (Q8).
3. Inactive partners and customer-only partners are no longer matched automatically (Q7).
4. More `PENDING_SUPPLIER` at first (ambiguity and contradictions become visible), fewer wrong assignments.
5. A partner with the wrong `country_code` and a prefix-less stored VAT will miss (loud) — the LI cleanup
   (`STATE.md`, D-042) is a precondition for that subset.

---

## 2. Alias table (RESEARCH §4 "learning from corrections")

### 2.1 Semantics

- **What an alias is:** "this exact **normalised OCR supplier name** → partner X" — the name only. VAT, company code
  and IBAN are not aliased: a human assigning partner X to an invoice whose VAT differs from X's is a master-data
  question ("add VAT V to X?"), not something to learn silently.
- **Where a confirmation comes from (D-017: only explicit human actions):** (1) `ConfirmExistingSupplierAsync` →
  `AssignSupplierAsync` (`InvoiceDetailDialog.razor:852-876`); (2) `SupplierCreateDialog` for **that one** invoice.
  **Not** a confirmation: the other invoices `AutoAssignSupplierAsync` sweeps in (`:1451-1463`), and any assignment the
  matcher made.
- **Promotion:** `CANDIDATE` → `ACTIVE` after N confirmations from **distinct invoices** (distinct `invoice_id`;
  a re-OCR of the same invoice does not count twice), all for the same partner. RESEARCH: "2–3, a reasonable starting
  parameter, not best practice — no quantitative source found". Proposal N = 2 (Q4).
- **Conflict:** a confirmation of the same key for a **different** partner → both rows `FROZEN`, reason recorded, the
  alias is not applied until a human unfreezes or revokes it. No silent overwrite (RESEARCH §4).
- **Application:** an `ACTIVE` alias yields `Assigned(tier=ALIAS)` **subject to the §1.3 contradiction rule** (an alias
  never overrides a document VAT/code that contradicts the partner). Applying is derived from ≥ N explicit human
  actions and is visible and revocable — my reading of D-017 (Q4 asks the owner to confirm it).
- **No retroactive sweep** in Etapas 2: promotion does not reassign existing `PENDING_SUPPLIER` invoices (that would be
  an unreviewed batch). The existing `AutoAssign` after "create supplier" remains the only bulk path.
- **Visible and revocable:** a list of a partner's aliases in `SupplierEditDialog` with state, confirmations, revoke
  and unfreeze buttons (UI details to be checked against MudBlazor 8.15.0 through the `mudblazor` MCP in the session —
  AGENTS; not done here).

### 2.2 Schema proposal (owner runs the DDL, D-039 pattern; no FKs)

```sql
CREATE TABLE IF NOT EXISTS supplier_aliases (
  id                INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  partner_id        INT NOT NULL,
  alias_key         VARCHAR(255) NOT NULL,        -- normalised OCR supplier name (SupplierNameNormalizer)
  raw_example       VARCHAR(255) NOT NULL,        -- the string as read, for humans
  state             VARCHAR(12) NOT NULL,         -- CANDIDATE | ACTIVE | FROZEN | REVOKED
  confirmations     INT NOT NULL DEFAULT 0,
  frozen_reason     VARCHAR(255) NULL,
  created_at        DATETIME NOT NULL,
  updated_at        DATETIME NOT NULL,
  UNIQUE KEY uq_alias_partner (alias_key, partner_id),
  KEY idx_alias_key (alias_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS supplier_alias_events (
  id          INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  alias_id    INT NOT NULL,
  invoice_id  INT NULL,
  event       VARCHAR(20) NOT NULL,               -- CONFIRMED | PROMOTED | CONFLICT_FROZEN | REVOKED | UNFROZEN | APPLIED
  actor       VARCHAR(100) NULL,
  details     TEXT NULL,
  created_at  DATETIME NOT NULL,
  KEY idx_alias_events_alias (alias_id),
  KEY idx_alias_events_invoice (invoice_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
```

The audit trail of an *application* on an invoice also goes into the existing `expense_invoice_audit`
(`action` varchar(50), `action_details` text; dev `information_schema`, 2026-09-27) as `SUPPLIER_MATCHED`
(§7.1 S3), so the invoice history shows the tier/alias that decided.

### 2.3 Seed data

Manually assigned invoices keep their pending name (§0.4). §5 query 9 counts them. They are **candidates for a
human-approved import** (each distinct name → partner pair shown to the accountant), not an automatic seed:
`AutoAssignSupplierAsync`-assigned invoices in that set were not individually confirmed, and the two cannot be told
apart from the audit row (`SUPPLIER_ASSIGNED`, id only, `:1407-1413`).

---

## 3. The supplier hard gate (RESEARCH §6 gate 3)

Gate 3: "an invoice cannot leave `PENDING_SUPPLIER` without a supplier."

### 3.1 What already holds *(read, not run)*

- Creation, re-OCR, dismiss-wrong-recipient, resolve-duplicate all decide status through `DecideOcrStatus` — supplier
  null → `PENDING_SUPPLIER` (`ExpenseService.cs:1678-1684`; call sites `:2099`, `:2281`, `:610`, `:1584`).
- The edit form keeps `PENDING_SUPPLIER` when there is no supplier (`StatusAfterManualEdit`, `:582-591`) and cannot set
  one (`:462`).
- Payment recalculation leaves `PENDING_SUPPLIER` alone (`ExpenseStatusHelper.Recalculate`, `Helpers/ExpenseStatusHelper.cs:155-156`);
  the payment button exists only for `PENDING`/`PARTIAL`/`OVERDUE` (`InvoiceDetailDialog.razor:464`); bank import excludes
  `PENDING_SUPPLIER` (`BankImport.razor:511` — FROZEN §7, not touched).

### 3.2 What does **not** hold *(read, not run)*

1. **`ApproveAsync` (`ExpenseService.cs:1532-1566`) sets `status = 'PENDING'` unconditionally** — the only guard is the
   `DUPLICATE_PENDING` refusal (`:1541-1543`). And the PATVIRTINTI button is rendered **for `PENDING_SUPPLIER`**
   (`InvoiceDetailDialog.razor:456-461`). So one click moves a supplier-less invoice to `PENDING`, records
   `approved_by`, and it becomes payable (`PENDING` → payment button, cash flow). This is the hole.
2. **`RestoreInvoiceAsync` (`:1611-1617`) sets `NEEDS_REVIEW` without looking at the supplier**; the button is on every
   `REJECTED` row (`ExpenseInvoices.razor:240-246`). A rejected supplier-less invoice → `NEEDS_REVIEW` → PATVIRTINTI
   → `PENDING`.
3. **`AssignSupplierAsync` has no status guard** (`:1377-1415`): called on a `DUPLICATE_PENDING` or `REJECTED`
   invoice it would set `PENDING`/`NEEDS_REVIEW` and release the quarantine. Latent — the UI offers assign only for
   `PENDING_SUPPLIER` (`InvoiceDetailDialog.razor:119`) and `AutoAssign` filters on it (`:1453`) — but nothing in the
   service enforces it.
4. Re-OCR replaces the supplier with a fresh match, possibly `null`, for an invoice a human had assigned (§0.1 B).
   Not a status leak, but it moves a `PENDING` invoice back to `PENDING_SUPPLIER` — and, through the reverse, a wrong
   fresh match replaces a right human choice.

Whether the hole has been used on the production clone is a staging query (§5 query 8): any invoice with
`supplier_id IS NULL` whose status is not `PENDING_SUPPLIER`/`REJECTED`/`DUPLICATE_PENDING`.

### 3.3 Proposal (S1 — small, no schema, independent of the matcher)

- `ApproveAsync`: refuse when `supplier_id IS NULL` (`InvalidOperationException("Pirmiausia priskirkite tiekėją")` —
  the dialog already turns `InvalidOperationException` into a warning snackbar, `InvoiceDetailDialog.razor:778-782`;
  same pattern as `:1541-1543`).
- Dialog: PATVIRTINTI not rendered for `PENDING_SUPPLIER` without a supplier (ATMESTI stays — rejecting is a legitimate exit).
- `RestoreInvoiceAsync`: status by `DecideOcrStatus(flags, supplierId)` instead of a hard-coded `NEEDS_REVIEW`
  (supplier null → `PENDING_SUPPLIER`); note this also changes what happens to `WRONG_RECIPIENT` rows — `DecideOcrStatus`
  would re-`REJECT` them (`:1681`), which is arguably right and must be a stated decision in the session (Q13).
- `AssignSupplierAsync`: allowed only from `PENDING_SUPPLIER`; other statuses refused.
- Re-OCR: keep an existing `supplier_id` when the fresh match is `null` or a **different** partner (the latter with a
  `VENDOR_SUGGESTED`-style note) — Q6.
- Tests on the service (real dev DB, like the existing ones): each of the above, plus "approve with supplier still works".
- `PENDING_SUPPLIER` **is** counted as payable in cash flow (`GetCashFlowAsync`, `ExpenseService.cs:1167-1176`,
  `WhereCountsAsPayable` excludes only `DUPLICATE_PENDING`/`REJECTED`, `ExpenseStatusHelper.cs:14-15`). A supplier-less
  invoice is still a real liability, so this is left as is — noted, not proposed.

---

## 4. Registries

RESEARCH §4 table, restated with what the repo already does:

- **Already in the product:** VIES at OCR time (`ExpenseOcrService.cs:741`; `ViesService.cs` FROZEN §6) and, in the
  supplier edit dialog, JARS for LT/LV/EE by company code and VIES by VAT code
  (`Services/CompanyLookupService.cs:157-219`, used at `SupplierEditDialog.razor:371`, `:381`). So a human creating a
  supplier already has registry-backed prefill; the registry step (RESEARCH tier 9, "enrich → a **new correct** supplier
  record, one human confirmation") largely exists.
- **Free and callable per RESEARCH:** RO ANAF `PlatitorTvaRest` v9 (confirmed free), EE RIK open-data (free, contract,
  ≤ 5 working days), GLEIF (free, likely poor coverage). LT — the majority — has **no verified live per-query API**
  (RESEARCH); LV, DE, UA not free or unconfirmed. I did not re-verify these sources.
- **Recommendation: none in Etapas 2.**
  1. Registries help **creating** a supplier, not **recognising** one; a registry hit that does not match a partner
     still ends in a human creating a partner — the machinery for that exists (above).
  2. Coverage does not justify it yet: staging invoices with a supplier are LT 83, LI 11, RO 3, CZ 2, PL 2, ES 1
     (D-039 item 3); RO — the only country with a confirmed free API in that list — is 3 invoices, EE is 0.
  3. Each adds an external dependency, rate limits, a contract (EE) and an unverified terms-of-use position for
     automated use; a wrong or stale registry name would feed the very name matcher this stage is trying to make
     stricter.
  4. Revisit after Etapas 2 runs and §5 query 6 shows which countries the unmatched suppliers come from.

---

## 5. Data: staging-only SELECT queries for the owner

**Not run by me.** Staging only (`nordic_bees_erp_staging`, D-029); read-only; the same form as
`STAGING-CHECKS-ETAPAS1.md` §2. No double quotes, backticks or `$` inside the SQL (they sit inside a double-quoted
shell string). `REGEXP_REPLACE` is used for normalisation — MariaDB 11.8 (prod's server, D-030) and MySQL 8 both have
it; I did not run these against staging (I only read `information_schema` on dev, §7.4). Each query's purpose is in
the line above it. "Pending" = the `pending_supplier_*` columns of `PENDING_SUPPLIER` invoices.

```bash
# 1. status distribution — how many invoices wait for a supplier, and the baseline
sudo mariadb nordic_bees_erp_staging -e "SELECT status, COUNT(*) AS n, ROUND(SUM(amount_incl_vat),2) AS total FROM expense_invoices GROUP BY status ORDER BY n DESC;"

# 2. PENDING_SUPPLIER: how many carry each identifier, and how many carry none (name-only cases)
sudo mariadb nordic_bees_erp_staging -e "SELECT COUNT(*) AS pending_supplier, SUM(NULLIF(TRIM(pending_supplier_vat),'') IS NOT NULL) AS with_vat, SUM(NULLIF(TRIM(pending_supplier_company_code),'') IS NOT NULL) AS with_company_code, SUM(NULLIF(TRIM(pending_supplier_bank_account),'') IS NOT NULL) AS with_iban, SUM(NULLIF(TRIM(pending_supplier_name),'') IS NOT NULL) AS with_name, SUM(NULLIF(TRIM(pending_supplier_vat),'') IS NULL AND NULLIF(TRIM(pending_supplier_company_code),'') IS NULL AND NULLIF(TRIM(pending_supplier_bank_account),'') IS NULL) AS no_identifier FROM expense_invoices WHERE status = 'PENDING_SUPPLIER';"

# 3. PENDING_SUPPLIER with a VAT code: does that code already exist on a partner? (as stored / normalised / ignoring the prefix)
#    normalised_or_prefix_only > as_stored means the cascade would match invoices today's matcher misses (or, for the prefix column, the LT-assumption cases)
sudo mariadb nordic_bees_erp_staging -e "SELECT COUNT(*) AS pending_with_vat, SUM(EXISTS(SELECT 1 FROM business_partners bp WHERE bp.vat_code <> '' AND bp.vat_code = e.pending_supplier_vat)) AS as_stored, SUM(EXISTS(SELECT 1 FROM business_partners bp WHERE bp.vat_code <> '' AND REGEXP_REPLACE(UPPER(bp.vat_code),'[^0-9A-Z]','') = REGEXP_REPLACE(UPPER(e.pending_supplier_vat),'[^0-9A-Z]',''))) AS normalised, SUM(EXISTS(SELECT 1 FROM business_partners bp WHERE bp.vat_code <> '' AND REGEXP_REPLACE(REGEXP_REPLACE(UPPER(bp.vat_code),'[^0-9A-Z]',''),'^[A-Z]{2}','') = REGEXP_REPLACE(REGEXP_REPLACE(UPPER(e.pending_supplier_vat),'[^0-9A-Z]',''),'^[A-Z]{2}',''))) AS prefix_ignored FROM expense_invoices e WHERE e.status = 'PENDING_SUPPLIER' AND e.pending_supplier_vat IS NOT NULL AND e.pending_supplier_vat <> '';"

# 4a. pending company codes: what do they actually look like? (VAT with prefix, a name, or a real registration number — §0.3)
sudo mariadb nordic_bees_erp_staging -e "SELECT pending_supplier_company_code, pending_supplier_vat, COUNT(*) AS n FROM expense_invoices WHERE status = 'PENDING_SUPPLIER' AND pending_supplier_company_code IS NOT NULL AND pending_supplier_company_code <> '' GROUP BY pending_supplier_company_code, pending_supplier_vat ORDER BY n DESC LIMIT 40;"
# 4b. ... and how many exist as a partner company_code
sudo mariadb nordic_bees_erp_staging -e "SELECT COUNT(*) AS pending_with_code, SUM(EXISTS(SELECT 1 FROM business_partners bp WHERE bp.company_code <> '' AND REGEXP_REPLACE(UPPER(bp.company_code),'[^0-9A-Z]','') = REGEXP_REPLACE(UPPER(e.pending_supplier_company_code),'[^0-9A-Z]',''))) AS matches_partner_company_code FROM expense_invoices e WHERE e.status = 'PENDING_SUPPLIER' AND e.pending_supplier_company_code IS NOT NULL AND e.pending_supplier_company_code <> '';"

# 5a. IBANs: how many supplier partners have a stored IBAN at all, and how many of those would pass a mod-97 check (the second number needs the app; here just non-empty and plausible length)
sudo mariadb nordic_bees_erp_staging -e "SELECT COUNT(*) AS supplier_partners, SUM(bank_account IS NOT NULL AND bank_account <> '') AS with_bank_account, SUM(bank_account IS NOT NULL AND bank_account <> '' AND CHAR_LENGTH(REGEXP_REPLACE(bank_account,'[^0-9A-Za-z]','')) BETWEEN 15 AND 34) AS plausible_length FROM business_partners WHERE is_supplier = 1 OR is_expense_supplier = 1;"
# 5b. pending IBANs that already exist on a partner
sudo mariadb nordic_bees_erp_staging -e "SELECT COUNT(*) AS pending_with_iban, SUM(EXISTS(SELECT 1 FROM business_partners bp WHERE bp.bank_account <> '' AND REGEXP_REPLACE(UPPER(bp.bank_account),'[^0-9A-Z]','') = REGEXP_REPLACE(UPPER(e.pending_supplier_bank_account),'[^0-9A-Z]',''))) AS iban_on_a_partner FROM expense_invoices e WHERE e.status = 'PENDING_SUPPLIER' AND e.pending_supplier_bank_account IS NOT NULL AND e.pending_supplier_bank_account <> '';"

# 6. how many distinct suppliers wait, and which countries they come from (registries §4; how many partners to create)
sudo mariadb nordic_bees_erp_staging -e "SELECT pending_supplier_country_code, COUNT(*) AS invoices, COUNT(DISTINCT LOWER(TRIM(pending_supplier_name))) AS distinct_names, COUNT(DISTINCT REGEXP_REPLACE(UPPER(pending_supplier_vat),'[^0-9A-Z]','')) AS distinct_vat FROM expense_invoices WHERE status = 'PENDING_SUPPLIER' GROUP BY pending_supplier_country_code ORDER BY invoices DESC;"
# 6b. pending names that equal a partner name exactly (collation-folded): these should be 0 unless the partner was created later or the name is ambiguous (two partners)
sudo mariadb nordic_bees_erp_staging -e "SELECT e.id, e.pending_supplier_name, (SELECT COUNT(*) FROM business_partners bp WHERE bp.name = e.pending_supplier_name) AS partners_with_that_name FROM expense_invoices e WHERE e.status = 'PENDING_SUPPLIER' AND EXISTS(SELECT 1 FROM business_partners bp WHERE bp.name = e.pending_supplier_name) ORDER BY e.id;"

# 7a. business_partners duplicates by normalised VAT (with prefix)
sudo mariadb nordic_bees_erp_staging -e "SELECT REGEXP_REPLACE(UPPER(vat_code),'[^0-9A-Z]','') AS v, COUNT(*) AS c, GROUP_CONCAT(id ORDER BY id) AS ids, GROUP_CONCAT(name ORDER BY id SEPARATOR ' / ') AS names FROM business_partners WHERE vat_code IS NOT NULL AND vat_code <> '' GROUP BY v HAVING c > 1;"
# 7b. ... by VAT ignoring the prefix (LT123 vs 123 stored on two partners)
sudo mariadb nordic_bees_erp_staging -e "SELECT REGEXP_REPLACE(REGEXP_REPLACE(UPPER(vat_code),'[^0-9A-Z]',''),'^[A-Z]{2}','') AS v, COUNT(*) AS c, GROUP_CONCAT(id ORDER BY id) AS ids, GROUP_CONCAT(vat_code ORDER BY id SEPARATOR ' / ') AS stored FROM business_partners WHERE vat_code IS NOT NULL AND vat_code <> '' GROUP BY v HAVING c > 1;"
# 7c. ... by name (GROUP BY uses the column collation: case and accent folded)
sudo mariadb nordic_bees_erp_staging -e "SELECT name, COUNT(*) AS c, GROUP_CONCAT(id ORDER BY id) AS ids, GROUP_CONCAT(IFNULL(vat_code,'-') ORDER BY id SEPARATOR ' / ') AS vats FROM business_partners GROUP BY name HAVING c > 1;"
# 7d. ... by company code and by bank account
sudo mariadb nordic_bees_erp_staging -e "SELECT company_code, COUNT(*) AS c, GROUP_CONCAT(id ORDER BY id) AS ids FROM business_partners WHERE company_code IS NOT NULL AND company_code <> '' GROUP BY company_code HAVING c > 1;"
sudo mariadb nordic_bees_erp_staging -e "SELECT bank_account, COUNT(*) AS c, GROUP_CONCAT(id ORDER BY id) AS ids FROM business_partners WHERE bank_account IS NOT NULL AND bank_account <> '' GROUP BY bank_account HAVING c > 1;"
# 7e. shape of the stored VAT codes: NULL vs empty, no letter prefix, punctuation or spaces inside (drives the stored-side normalisation and the LT assumption)
sudo mariadb nordic_bees_erp_staging -e "SELECT COUNT(*) AS partners, SUM(vat_code IS NULL) AS vat_null, SUM(vat_code = '') AS vat_empty, SUM(vat_code <> '' AND vat_code NOT REGEXP '^[A-Za-z][A-Za-z]') AS no_letter_prefix, SUM(vat_code <> '' AND vat_code REGEXP '[^0-9A-Za-z]') AS has_separator, SUM(vat_code <> '' AND vat_code <> UPPER(vat_code)) AS lower_case FROM business_partners;"

# 8. gate 3: has the hole been used? supplier-less invoices by status (expected: only PENDING_SUPPLIER, REJECTED, DUPLICATE_PENDING)
sudo mariadb nordic_bees_erp_staging -e "SELECT status, COUNT(*) AS n, SUM(approved_by IS NOT NULL) AS approved FROM expense_invoices WHERE supplier_id IS NULL GROUP BY status;"

# 9. seed data for aliases: invoices with a supplier that still carry a pending name (assigned by a human or by the create-supplier sweep)
sudo mariadb nordic_bees_erp_staging -e "SELECT COUNT(*) AS invoices, COUNT(DISTINCT LOWER(TRIM(pending_supplier_name))) AS distinct_names FROM expense_invoices WHERE supplier_id IS NOT NULL AND pending_supplier_name IS NOT NULL AND pending_supplier_name <> '';"
sudo mariadb nordic_bees_erp_staging -e "SELECT action, COUNT(*) AS n FROM expense_invoice_audit WHERE action IN ('SUPPLIER_ASSIGNED','CREATED') GROUP BY action;"
```

What each answer decides: 1–2 the size of the problem and how many cases are name-only (Q1); 3 how many invoices the
cascade would rescue vs today's matcher, and whether the LT assumption matters (Q8); 4 whether tier 2 has any usable
input without the extraction change (S2); 5 whether IBAN-only or `SUPPLIER_NEW_IBAN` is worth anything at rollout
(Q2, Q5); 6 registries (§4) and 6b whether "created later" or ambiguity explains the leftovers; 7 whether duplicate
partners must be cleaned **before** the cascade goes live (Q9); 8 gate 3's real-world exposure; 9 alias seed (§2.3).

---

## 6. Interaction with Etapas 0 / 1

| Topic | Interaction | Proposal |
|---|---|---|
| **Duplicate detection / quarantine (D-027)** | `CheckDuplicateAsync` ignores the supplier (`ExpenseService.cs:1347-1371`), so supplier fragmentation does not break it. RESEARCH §3 would key on (supplier, number). | **Unchanged in Etapas 2.** It caught all 27 real duplicates in production; the cascade does not need it. A supplier-scoped second pass is possible once suppliers are reliable (Q12). Quarantine (`DUPLICATE_PENDING`, kept on re-OCR `:2245`) must not be released by any new path — hence the `AssignSupplierAsync` status guard (§3.3). |
| **`INVALID_IBAN` / `INVALID_VAT_FORMAT` (Etapas 1)** | A malformed VAT is already not used to match (`ExpenseOcrService.cs:732-735`, `:794`) — but the dialog's second call bypasses it (§0.1 A). An invalid IBAN must never be a key or be added to known accounts. | One matcher call (dialog uses `result.SupplierId`); matcher takes the *validated* codes only. |
| **D-039 item 2** ("once a supplier exists, the document's `INVALID_IBAN`/`INVALID_VAT_FORMAT` are information, everywhere") | `HasReviewFlag(flags, hasSupplier)` (`ExpenseService.cs:1667-1675`). | `SUPPLIER_NEW_IBAN` is a **different** fact (valid IBAN, unknown for this supplier) and stays review even with a supplier — it must not be folded into the item-2 exemption. `VENDOR_AMBIGUOUS` / `VENDOR_SUGGESTED` are info and irrelevant once a supplier exists. |
| **Country rule (D-042 / D-043)** | Partner country authoritative once assigned (`ResolveRateCountry` `:1767`); the VAT prefix is the OCR-time heuristic (`ExpenseOcrService.cs:782`). | The cascade never writes or "corrects" a partner's country; it uses the partner's `country_code` only to give a **prefix-less stored VAT** its prefix (§1.2 tier 1), which makes the LI/ES cleanup a precondition for that subset. KONICK RETAIL HUB (CZ with an LT VAT, prefix present) is unaffected. Country is never a match key. |
| **Approval retention** | An approved invoice keeps its approval on edit unless a gate field changed (`ApplyManualEditStatusAsync` `:451-466`, `ChangedGateFields` `:491`). Supplier is not an edit-form field (`:462`). | Two paths can still change a supplier under an approval: `AssignSupplierAsync` (no status guard, §3.2 item 3) and re-OCR (§0.1 B). Both closed by §3.3; each gets a test "approved invoice keeps its supplier". |
| **`AutoAssignSupplierAsync`** | Bulk, OR-logic, weaker normalisation than the matcher (§0.1 D). | Reuse the matcher's normalisers so "create supplier" and "upload" agree; the sweep stays `PENDING_SUPPLIER`-only and is not a confirmation (§2.1). |
| **Rate gate on assignment** | `RecomputeRateFlagsForSupplierAsync` (`:1428-1436`) already re-runs when a supplier is assigned. | Reused unchanged by every new assign path. |
| **`OcrQueueWorker`** (FROZEN §5) | Calls `ProcessAsync` (`:77`); dead today. | Not touched. If revived, it inherits the matcher. |

---

## 7. Sessions, tests, staging checks, estimate, open questions

### 7.1 Sessions (each commit: task spec from the planning chat → agent → fresh reviewer → full `dotnet test --filter "Category!=E2E"` → guardrail + semgrep; D-031: parallel only for new-file, no-DB work)

| # | Session | Content | Files | Frozen conflicts | Depends on | Est. |
|---|---|---|---|---|---|---|
| **S1** | **Gate 3** (no schema) | `ApproveAsync` refuses without a supplier; PATVIRTINTI hidden for supplier-less `PENDING_SUPPLIER`; `RestoreInvoiceAsync` by `DecideOcrStatus`; `AssignSupplierAsync` status guard; re-OCR keeps a human-assigned supplier (Q6); `AssignSupplierDialog.razor` deleted or guarded (Q16) | `Services/ExpenseService.cs`, `Services/IExpenseService.cs` (only if a signature changes), `Components/Dialogs/InvoiceDetailDialog.razor`, `Components/Dialogs/AssignSupplierDialog.razor` + `ExpenseInvoices.razor:598-601`, tests | none (`InvoiceDetailDialog` is not frozen; `BankImport.razor` untouched) | Q6, Q13 | 3–5 h |
| **S2** | **Pure matcher** (new files, no DB) | `SupplierIdentityNormalizer` (VAT, company code, IBAN, name — reuses `VatCodeFormatValidator` and `DiacriticHelper`), `SupplierMatcher` (tiers 1, 2, 4, 4b, contradiction rule, outcomes), candidate DTOs; also the **company-code extraction clean-up** in `ExpenseOcrService` (§0.3) | `Services/Validation/…` (new), `Services/ExpenseOcrService.cs:211-270` | none (the extraction block is not frozen) | Q1, Q7, Q8 | 8–12 h |
| **S3** | **Wire the matcher** | `FindSupplierIdAsync` → matcher (keep the interface member, `IExpenseOcrService.cs:12`); dialog uses `result.SupplierId` (one call); `VENDOR_AMBIGUOUS`, `VENDOR_SUGGESTED` flags + labels; candidates and tier in the detail dialog; audit row `SUPPLIER_MATCHED` with tier/reason; `AutoAssignSupplierAsync` uses the same normalisers; updates the 7 existing `FindSupplier` tests | `Services/ExpenseOcrService.cs`, `Services/Dtos/OcrResultDto.cs` (flag consts), `Helpers/ExpenseStatusHelper.cs`, `Services/ExpenseService.cs`, `Components/Dialogs/ExpenseUploadDialog.razor` (only the analysis method around `:724`), `InvoiceDetailDialog.razor`, tests | `ExpenseUploadDialog.razor` §3: only `OnAfterRenderAsync`, `OnFileDropped`, `DisposeAsync`, `DroppedFile` are frozen — the analysis method is outside; `ViesService` (§6) and `OcrQueueWorker` (§5) not touched | S2; §5 data (Q9) | 8–10 h |
| **S4** | **Known IBANs** (schema) | `supplier_bank_accounts` model + migration (owner runs the DDL + backfill); IBAN check tier; `SUPPLIER_NEW_IBAN` (review) + "add IBAN" action; stop nulling `pending_supplier_bank_account` on match; partner save upserts the IBAN | `Models/…` (new), `Data/NordicBeesErpContext.cs`, `Migrations/` (new file), `SupplierService.cs`, `ExpenseService.cs`, dialogs | none | S3, Q2, Q5, Q11, owner DDL | 8–10 h |
| **S5** | **Aliases** (schema) | `supplier_aliases` + `supplier_alias_events`; confirmation capture at assign/create; promotion, conflict freeze; application in the matcher; list/revoke/unfreeze in `SupplierEditDialog` | new files, `SupplierEditDialog.razor`, `ExpenseService.cs`, migration | none | S3, Q4, owner DDL | 10–14 h |
| **S6** | **Suggestions ranking** (optional; Q3) | pure token-set + Jaro-Winkler ranking for the assign picker; no auto-assign | new file, `InvoiceDetailDialog.razor` | none | S3 | 4–6 h |
| **S7** | **Staging checks** `STAGING-CHECKS-ETAPAS2.md`, then the owner's run | — | doc | — | all | 2–3 h + owner |

**Total ≈ 43–60 h** of agent work plus reviews and the owner's staging time. That is above RESEARCH's ~20 h
(items 9–11) because (a) RESEARCH costed the cascade at 12–16 h without the three write paths, the dialog's second call,
the extraction clean-up and the candidate UI, (b) two schema additions with a human-applied DDL each, (c) gate 3 is
larger than "2 h" once `Restore`/`Assign`/re-OCR are included. If the owner wants a smaller Etapas 2: S1 + S2 + S3 alone
(≈ 19–27 h) already close gate 3 and the "silent wrong supplier" shapes; S4–S6 can follow.

Suggested order: S1 first (independent, closes a live hole), S2 in parallel (new files only), then S3, S4, S5.

### 7.2 Tests

- **S1** (real dev DB, like the existing service tests): approve refused with no supplier / allowed with one;
  restore of a supplier-less rejected invoice → `PENDING_SUPPLIER`; `AssignSupplierAsync` refused on
  `DUPLICATE_PENDING`/`REJECTED`; re-OCR keeps the human-assigned supplier; approved invoice keeps its supplier.
- **S2** (pure, table-driven): VAT normalisation (spaces, dots, en/em dash, lower case, missing prefix with and without
  country hint); tier order; contradiction rule (same name/different VAT → `Suggested`; VAT→A and code→B →
  `Ambiguous`); two partners same VAT → `Ambiguous`; empty identifiers never match (the partner-336 case: 16 invoices wrongly linked, `PROD-DATA-FINDINGS-2026-09-25.md` §3); name
  normalisation (`UAB „Rotoma"`, `Rotoma, UAB`, `ROTOMA`, `Žūklinė`), legal-form stripping never yields `Assigned`;
  IBAN check-only behaviour; company-code extraction stops returning a VAT with prefix or a name.
- **S3** (integration): the 7 existing `FindSupplier` cases still hold; document VAT not on any partner but name
  equal → not assigned; ambiguity → `PENDING_SUPPLIER` + `VENDOR_AMBIGUOUS` + candidates; dialog path produces the same
  result as `ProcessAsync` (regression for the second call, including: `ProcessAsync` finds no supplier, so
`VENDOR_NOT_FOUND` is in `result.Flags`, while the dialog's own call would have matched — no stale flag next to a
supplier); audit row content.
- **S4**: known-IBAN match, new IBAN with ≥ 1 known → `NEEDS_REVIEW`, none known → no flag, invalid IBAN never added,
  "add IBAN" clears the flag, edit path carries the flag, PATVIRTINTI keeps it.
- **S5**: N confirmations promote, re-OCR of the same invoice does not count twice, conflict freezes both, frozen never
  applies, revoke, alias never overrides a contradicting VAT.

### 7.3 Staging checks (written in S7 against the final code; the shape)

Per rule: one invoice that must be stopped, one that must pass, on create, re-OCR and edit — as in
`STAGING-CHECKS-ETAPAS1.md`. Cases: VAT variants (spaces / dashes / lower case / prefix-less) match one partner;
same VAT on two partners → `PENDING_SUPPLIER` + both candidates, nothing assigned; same name + different VAT → not
assigned; known supplier + new IBAN → `NEEDS_REVIEW` + `SUPPLIER_NEW_IBAN`, "add IBAN" clears it, PATVIRTINTI keeps
it; alias: two confirmations promote, a conflicting third freezes; gate 3: PATVIRTINTI refused / hidden without a
supplier, restore of a supplier-less rejected invoice; re-OCR keeps a human-assigned supplier; quarantine unaffected;
a noise check (count of `VENDOR_NOT_FOUND`, `VENDOR_AMBIGUOUS`, `VENDOR_SUGGESTED`, `SUPPLIER_NEW_IBAN` after the run,
against the §5 baseline). **Caveat learned in Etapas 1:** every corpus PDF is already in production, so uploads become
`DUPLICATE_PENDING` — checks must compare flags/`supplier_id`, and the plan for S7 should say so up front.

### 7.4 What I could not verify

- Collation and indexes on **staging**: read on **dev** only (2026-09-27, `information_schema`); production's `MariaDB 11.8.2`
  (D-030) vs the dev DB's `MySQL 8.0.46` (AGENTS) may differ. §5 uses functions both have but nothing was executed on staging.
- Whether Azure ever returns `VendorBusinessNumber`, and how often invoices print a registration number apart from the
  VAT code — needs the raw responses (the corpus outside git, D-041) or the §5 query 4.
- That an LT VAT code is `LT` + the company code (true for 9-digit legal entities by general knowledge; not verified in
  the repo — `VatCodeFormatValidator` only allows 9 or 12 digits, `:57`).
- The current data: how many partners are duplicates, how many stored VATs lack a prefix, how many have IBANs (§5).
- UI behaviour (all "read, not run"): the second matcher call, the missing name check on the create button, the PATVIRTINTI
  hole, re-OCR replacing the supplier. Each has a proposed test that would turn it into a fact.
- MudBlazor 8.15.0 parameters for the new UI pieces — to be checked through the `mudblazor` MCP in the session (AGENTS), not here.
- Performance: candidate snapshots of a few hundred partners per upload were assumed cheap (RESEARCH §4); not measured.
- I did not re-verify RESEARCH's registry facts (§4) or its fuzzy-matching sources.

### 7.5 Open questions for the owner

- **Q1 — Name-only, unique, exact match.** Keep auto-assign when the document carries no VAT/code (proposed; today's
  behaviour), or demote to a suggestion? Same name with a **different** VAT is never assigned (§1.3) — confirm.
- **Q2 — IBAN as a key.** Suggest-only (proposed) or auto-assign when exactly one partner holds it? Decide after §5 query 5.
- **Q3 — Fuzzy.** Suggestions only in the picker (S6), or defer entirely? Auto-assign ≥ 95 not before Etapas 4 numbers (proposed).
- **Q4 — Aliases.** N = 2 or 3; name-only aliases; no retroactive sweep; and do you agree that *applying* an alias that
  came from ≥ N explicit confirmations, visibly and revocably, is within D-017?
- **Q5 — `SUPPLIER_NEW_IBAN` at rollout.** Flag only when the supplier already has ≥ 1 known account (proposed), so the
  first weeks are not noise? Should a supplier's **first** IBAN ever seen be offered for adding (info) or ignored?
- **Q6 — Re-OCR and a human-assigned supplier.** Keep it (proposed) or let the fresh match overwrite as today? If kept and
  the fresh match differs — flag only, or also show both?
- **Q7 — Candidate scope.** Match only `is_active` partners with a supplier role (`is_supplier` / `is_expense_supplier`)
  (proposed), or any partner as today? Customer-only or inactive partners → `Suggested` only.
- **Q8 — LT assumption.** Keep "digits-only document VAT ⇒ LT" when nothing else says otherwise, or require a country?
  (§5 queries 3 and 7e size it.)
- **Q9 — Duplicate partners.** Ambiguity from duplicate master data becomes visible. Clean the duplicates (§5 query 7)
  **before** S3 goes live? There is no merge tool; build one (separate task) or clean by hand?
- **Q10 — Scope.** All of S1–S7, or S1–S3 first (≈ 19–27 h) and S4–S6 later?
- **Q11 — New tables without foreign keys.** Acceptable (proposed), or approve FKs to `business_partners` /
  `expense_invoices` explicitly? DDL for both tables applied by you on dev/staging and at the final deploy (D-037/D-039).
- **Q12 — Duplicate detection.** Leave supplier-independent (proposed) or add a supplier-scoped second pass later?
- **Q13 — Restore.** Restoring a rejected invoice through `DecideOcrStatus` would re-reject a `WRONG_RECIPIENT` one
  (§3.3). Intended, or keep `NEEDS_REVIEW` for that case?
- **Q14 — Personal ID (`national_id_number`) as a key for individual suppliers** (beekeepers, no VAT): proposed **no**
  (personal data extraction/storage); confirm.
- **Q15 — Registries.** Confirm "none in Etapas 2" (§4).
- **Q16 — `AssignSupplierDialog.razor`.** Apparently dead (§0.1 F) with a "newest partner" fallback. Delete it
  (proposed, in S1) or keep and route through the guard and matcher?
- **Note on scope (Q3).** RESEARCH §10 item 9 counts "FuzzySharp Token Sort ≥95" inside Etapas 2; deferring auto-fuzzy is a
  deliberate scope deviation, argued in §1.6.
