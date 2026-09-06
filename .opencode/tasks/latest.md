# Task: VIES VAT verification persisted at partner level (customers + suppliers)

## Type: BUILD (schema + service + UI). DDL is human-applied per FROZEN.md — draft migration only, do not run `dotnet ef database update`.

## Context
`Services/ViesService.cs` (`IViesService.LookupAsync(vatCode)`) already
exists and works — it's currently only used transiently inside
`ExpenseOcrService.ProcessAsync` for OCR'd invoices, with results
snapshotted per-invoice on `expense_invoices` (`supplier_vat_verified`,
`supplier_vat_verified_name` — do NOT touch these, unrelated). This task
adds a SEPARATE, NEW capability: persisted VIES verification at the
`business_partners` level, for both customers and suppliers.

Read `Components/Dialogs/SupplierEditDialog.razor` in full first (already
read this session — has `OnVatCodeChanged()` hook, `supplier.VatCode`,
`supplier.IsIndividual`). Also read `Components/Dialogs/CustomerCreateDialog.razor`
in full to confirm its exact current VAT-code field/handler names before
editing (may differ — verify, don't assume it mirrors Supplier's).

## Step 1 — New columns on `business_partners` (draft migration, do not apply)
```csharp
public bool? VatVerified { get; set; }        // null = never checked, true = valid, false = confirmed invalid
public DateTime? VatVerifiedAt { get; set; }
public string? VatVerifiedName { get; set; }  // official registered name from VIES, for comparison
```
Add to `Models/Models_Part1.cs` `BusinessPartner` class. Run
`dotnet ef migrations add AddPartnerVatVerification` to generate the
migration file only — do NOT apply it. Confirm the generated `Up()`/`Down()`
match this project's `ADD COLUMN`/`DropColumn` conventions (check a recent
migration file for the pattern, same as the Phase 1 role-flags migration
did).

## Step 2 — Add matching properties to the DTOs
`Supplier` (`Models_Part2.cs`) and `Customer` (`InvoiceModels.cs`): add
the same 3 properties. Map them in `SupplierService.GetSuppliersAsync`/
`GetAllSuppliersAsync` and `CustomerService.GetCustomersAsync` (read
paths — mirror however `IsIndividual` etc. were mapped in Phase 3).

## Step 3 — Verification logic (new helper, shared by both dialogs)
Create a small shared method (e.g. static helper in a new
`Helpers/VatVerificationHelper.cs`, or a method on `IViesService` itself
if that fits the existing pattern better — use judgement) that, given a
VAT code and the partner's current stored `VatVerified`/`VatVerifiedAt`/
last-known VAT code, decides whether a fresh VIES check is needed:
- Skip entirely if `IsIndividual == true` or VAT code is empty.
- Check if: VAT code differs from what's persisted (changed since last
  save/load), OR `VatVerified == null` (never checked).
- Otherwise, do NOT re-check (avoid hammering VIES on every save/open).

On a successful VIES response: set `VatVerified = viesResult.IsValid`,
`VatVerifiedAt = DateTime.UtcNow`, `VatVerifiedName = viesResult.Name`.
On `ServiceAvailable == false` (VIES down/timeout): do NOT change
`VatVerified`/`VatVerifiedAt` at all — leave whatever was there before,
just show a transient "VIES nepasiekiamas, patikrinta vėliau" message in
the UI for that render, don't persist an error state.

## Step 4 — Wire into `SupplierEditDialog.razor`
- On dialog open for an EXISTING supplier (`OnInitializedAsync`/
  `OnParametersSet`, whichever fits the current lifecycle without
  duplicating the `OnAfterRenderAsync` company-lookup pattern already
  there): if the verification-needed check from Step 3 says yes, call
  VIES and update the in-memory `supplier` object's 3 new fields
  (display only — don't silently write to DB just from opening the
  dialog; persist on Save per Step 5).
- Extend the existing `OnVatCodeChanged()` handler: after the existing
  JARS/company lookup logic, ALSO run the Step 3 check/call if the new
  VAT code differs from the original.
- Add a small status indicator next to the VAT code field, following the
  existing `_lookupResult` badge pattern already in this file (green
  check + "PVM patikrintas VIES" when `VatVerified == true`; red icon +
  "PVM kodas negalioja VIES" when `VatVerified == false`; grey/neutral
  "Netikrinta" when `VatVerified == null`; nothing extra needed for the
  transient "VIES nepasiekiamas" case beyond a snackbar).

## Step 5 — Wire into `CustomerCreateDialog.razor`
Same treatment, using whatever the actual current VAT-code field/handler
names are (confirm from your Step-1 read — do not assume they match
Supplier's naming).

## Step 6 — Persist on save
`SupplierService.SaveSupplierAsync` and `CustomerService.SaveCustomerAsync`:
add `vat_verified`, `vat_verified_at`, `vat_verified_name` to both the
UPDATE raw SQL (renumber positional params carefully, same caution as
Phase 3 Round 1) and the INSERT branch. Persist whatever values are
currently on the in-memory DTO at save time (the dialog already
populated them via Step 4/5's lazy-check).

## Step 7 — Do NOT touch
- `ExpenseOcrService.cs`'s existing per-invoice VIES usage or the
  `expense_invoices.supplier_vat_verified*` columns — completely separate,
  unrelated to this task.
- `AssignSupplierDialog.razor` / `ResolveSupplierDialog.razor` — out of
  scope for this task.
- Any DDL execution — migration file only, human applies per FROZEN.md.

## Step 8 — Draft the backfill (optional, do NOT apply)
Since this is a brand-new concept (no legacy column to derive from),
there's no meaningful backfill — existing rows will simply have
`vat_verified = NULL` (never checked) until each is opened/saved once.
Note this explicitly in the report; no backfill SQL needed for this task.

## Verification (required before finishing)
- `dotnet build` — 0 errors.
- `dotnet test` — both with and without `TEST_DB_CONNECTION` (per the
  pattern established in Phase 3 — if this exposes the SAME `nordic_bees_erp_test`
  schema-drift issue for these 3 new columns, STOP and report exactly
  which column is missing, same as before; do not fix test DB DDL
  yourself).
- Confirm the raw-SQL UPDATE param renumbering is correct — show the full
  final SQL strings for both services in the report.
- Manually trace through: (a) new supplier with VAT code → save → VIES
  checked once, persisted; (b) existing supplier, open dialog, VAT code
  unchanged, already verified → no VIES call made (confirm this via a
  log statement or code trace, not just claiming it); (c) existing
  supplier, VAT code edited to a different value → re-check triggered;
  (d) `IsIndividual == true` → VIES never called regardless of VatCode
  content (should be empty anyway, but defensive).

## Report
Write to
`.opencode/reports/partner-vat-verification-<YYYYMMDD>-<HHMM>.md`.

## Final step (required)
Run `./bump-version.sh patch`.
