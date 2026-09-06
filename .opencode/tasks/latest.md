# Task: Phase 3 Round 1 — role-flag UI controls in existing dialogs + write paths

## Type: BUILD (code changes) — dialogs, DTOs, write-path SQL, xUnit tests. NO dialog unification yet (Round 3, deferred).

## Context
`.opencode/reports/partner-type-phase3-ui-spec-20260906-0750.md` (read in
full first — it has exact line numbers, verbatim current code, and the
full field-collision analysis) is the source of truth for this task.
`Docs/PARTNER_TYPE_ARCHITECTURE_PLAN.md` sections 4 and 9 give the
overall design. Phases 0/1/2 are done and verified.

Goal: add role-flag controls (`IsCustomer`/`IsSupplier`/`IsExpenseSupplier`/
`IsIndividual`) to the THREE partner-creating/editing dialogs
(`SupplierEditDialog`, `SupplierCreateDialog`, `CustomerCreateDialog`),
make the write paths persist them, fix the `ResolveSupplierDialog` inline
write path, and fix the `TopHeader.razor` mislabeling bug. Do NOT unify
the dialogs into one component in this task.

## Step 1 — Add flag properties to the DTOs
- `Models_Part2.cs` `Supplier` class: add `IsCustomer`, `IsSupplier`,
  `IsExpenseSupplier`, `IsIndividual` (bool).
- `InvoiceModels.cs` `Customer` class: same 4 bool props.
These are plain DTO properties (not EF-mapped directly — `BusinessPartner`
already has them from Phase 1; these DTOs are separate read/write shapes
per the existing pattern in this codebase).

## Step 2 — `SupplierEditDialog.razor`
- Replace the `PartnerType` `MudSelect` (verbatim in the spec report §1.1)
  with: 3 `MudSwitch` controls bound to `supplier.IsCustomer`,
  `supplier.IsSupplier`, `supplier.IsExpenseSupplier` (labels: "Klientas",
  "Tiekėjas", "Išlaidų tiekėjas"), plus 1 more control for
  `supplier.IsIndividual` — use `MudSwitch` labeled "Fizinis asmuo /
  ūkininkas" (off = įmonė) for consistency with the other three switches
  rather than a radio group.
- When `IsIndividual` is true, show a new `MudTextField` for
  `supplier.NationalIdNumber` (add this prop to `Supplier` DTO too if not
  already present — the spec notes it's currently DTO-only/unused, so it
  may need adding) and hide/de-emphasize `CompanyCode`+`VatCode`; when
  false, show `CompanyCode`+`VatCode` as today. Follow existing MudBlazor
  conditional-field patterns already used elsewhere in this dialog (e.g.
  the JARS/VIES lookup blocks) for consistency.
- Defaults for a brand-new supplier (currently `PartnerType.Supplier` at
  dialog open): set `IsSupplier = true`, others false.
- Keep `supplier.PartnerType` being set too (derived, for the legacy
  column — see Step 5) — do NOT remove the enum property from the DTO,
  just stop showing it as a direct user control.
- Follow `Docs/DESIGN_SYSTEM.md` tokens for switch styling — match
  existing `IsActive` MudSwitch styling already in this file for
  consistency (Color.Success when true).

## Step 3 — `SupplierCreateDialog.razor` (OCR quick-create)
- Same 4 switches, replacing the emoji 4-option `MudSelect` (spec §1.2).
  **Remove the emoji entirely** — plain Lithuanian labels only, per
  `Docs/DESIGN_SYSTEM.md`.
- OCR-prefill mode (when `PrefilledName` etc. params are set): default
  `IsExpenseSupplier = true`, `IsSupplier = false`, `IsCustomer = false`,
  `IsIndividual = false` (mirrors current `PartnerType.ExpenseSupplier`
  default at line 221 per the spec).
- Non-OCR mode: default `IsSupplier = true`, others false (mirrors
  current `PartnerType.Supplier` default at line 232).
- Same `IsIndividual` → NationalIdNumber field toggle as Step 2.

## Step 4 — `CustomerCreateDialog.razor` (create AND edit)
- Same 4 switches, replacing the STRING-typed `PartnerType` `MudSelect`
  (spec §1.3). Since `Customer.PartnerType` is a string today, keep
  writing a derived string value to it (see Step 5) but stop showing it
  as the user-facing control.
- New customer default: `IsCustomer = true`, others false.
- Preserve the existing duplicate-check logic (company code / VAT /
  name, lines 222-245 per spec) UNCHANGED — do not extend or remove it
  in this task.
- Preserve PL-NIP auto-prefix and JARS address-copy behavior UNCHANGED.
- `Customers.razor`'s `OpenEditDialog` (mapping existing partner → DTO
  for the edit dialog) must be updated to populate the 4 new flags from
  the loaded `BusinessPartner`/mapped `Customer` object so editing an
  existing customer shows their real current flags, not defaults.

## Step 5 — Write paths: persist the new flags

### `SupplierService.SaveSupplierAsync`
- UPDATE branch: append `is_customer = {N}, is_supplier = {N+1},
  is_expense_supplier = {N+2}, is_individual = {N+3}` to the raw SQL
  (append at the end before `updated_at`/`WHERE`, per spec §5.3 —
  renumber all subsequent positional params carefully, this project's
  FROZEN.md requires exact positional `{0},{1}...` matching).
- INSERT branch: set the 4 DTO flag values on the `BusinessPartner`
  object before `Add`.
- Continue writing `partner_type` too (derived — see Step 6), for
  rollback safety during the transition (plan §3.2/§6 — do not remove
  the legacy column write yet).

### `CustomerService.SaveCustomerAsync`
- Same treatment: UPDATE raw SQL gains the 4 columns (renumber params
  carefully — spec warns about the existing `{0}..{18}` numbering);
  INSERT branch sets the 4 flag props.
- Continue deriving and writing the legacy `partner_type` string too.

### `ResolveSupplierDialog.razor` inline create (spec §1.5, lines 141-152)
- This bypasses services entirely — add `IsExpenseSupplier = true` to the
  inline `new BusinessPartner { ... }` object. Set `IsSupplier = false`
  (expense-only, per spec §5.3 recommendation) unless you find contrary
  guidance elsewhere — if uncertain, note it in your report rather than
  guessing further.
- Also update this dialog's OWN candidate filter (spec §1.5) from
  `PartnerType == ExpenseSupplier || PartnerType == Both || PartnerType == Supplier`
  to `IsExpenseSupplier || IsSupplier` (flags-first, matching the Phase 2
  pattern already used in the service layer — no fallback needed here
  since this is a UI query you're actively updating, not a service you're
  transitioning).

### `AssignSupplierDialog.razor` candidate filter (spec §1.4, lines
152-158 and 271-276)
- Update from `PartnerType == ExpenseSupplier || PartnerType == Both` to
  `IsExpenseSupplier == true`. This is the fix for the real production
  gap in plan §7.3 (31 real expense-invoice suppliers weren't visible
  here) — the widened candidate list is intentional, not a regression.
- Confirm the quick-create flow (via `SupplierCreateDialog` in OCR mode,
  Step 3 above) sets `IsExpenseSupplier = true` so a newly created
  supplier is immediately visible in this same dialog's reloaded list
  (spec §4.1 — this is the specific trap to avoid).

## Step 6 — Derive the legacy `partner_type` value from flags (both services)
Add a small helper (or inline logic) that computes the legacy enum/string
value from the 4 flags for continued writes, e.g.:
- Both `IsCustomer` and `IsSupplier` true → `Both`
- Only `IsExpenseSupplier` true (not `IsSupplier`) → `ExpenseSupplier`
- Only `IsSupplier` true → `Supplier`
- Only `IsCustomer` true → `Customer`
- Pick a sensible default for edge combinations (e.g. `IsCustomer +
  IsExpenseSupplier` with no `IsSupplier` — document your choice in the
  report). This derivation logic should live in ONE place per service
  (or a shared helper) — do not duplicate the mapping inline in multiple
  spots.

## Step 7 — `TopHeader.razor` fix (spec §3.5, lines 217-218)
Replace:
```csharp
Subtitle = bp.PartnerType == PartnerType.Supplier ? "Tiekėjas" : "Klientas",
Href = bp.PartnerType == PartnerType.Supplier ? "/suppliers" : "/customers"
```
with flag-based logic: prefer `IsSupplier`/`IsExpenseSupplier` →
"Tiekėjas" + `/suppliers`; `IsCustomer` → "Klientas" + `/customers`; if
both `IsCustomer` and `IsSupplier` are true, decide a sensible single
label+link (e.g. "Klientas ir tiekėjas" is too long for a subtitle —
pick "Tiekėjas" as primary since that's arguably the rarer/more specific
role, or keep it simple and go by whichever flag combination existing
code already privileges — use judgement, note your choice in the report).

## Step 8 — `Suppliers.razor` create-button defaults
Per spec §4.3: `OpenCreateDialog(PartnerType.Both)` /
`OpenCreateDialog(PartnerType.Supplier)` calls currently pass an ignored
parameter (dead code — `OpenCreateDialog` hardcodes `PartnerType.Supplier`
regardless). Fix this dead code AND wire real intent: the "Naujas
tiekėjas" button → `IsSupplier=true`; if there's a separate "Naujas
ūkininkas" affordance, → `IsSupplier=true, IsIndividual=true`. Confirm
exactly what buttons exist and what they should default to by reading
the current `Suppliers.razor` around lines 20-24 and `OpenCreateDialog`
itself before changing.

## Step 9 — xUnit tests (REQUIRED — DB-write methods changed)
Per this project's rule, every DB-write method that is created or
modified must have a corresponding test. Update `SupplierServiceTests.cs`
and `CustomerServiceTests.cs` (existing `DbTestFixture` pattern) to cover
`SaveSupplierAsync`/`SaveCustomerAsync` UPDATE and INSERT paths
persisting the 4 new flags correctly, including at least one case for
the legacy `partner_type` derivation (Step 6) producing the expected
value for a `Both`-flag combination.

## Do NOT
- Do not unify the three dialogs into one component (Round 3, deferred).
- Do not remove the `PartnerType` enum/property from any model or DTO.
- Do not touch `Suppliers.razor`'s `FilteredSuppliers` tab logic (Round 2,
  separate task) — Customers.razor's filter already reads flags via the
  service layer (Phase 2), no change needed there either.
- Do not fix Vaidas Arbutavičius's `is_individual` data value — that's a
  manual data correction for the human, not a code change.
- Do not add unique constraints, indexes, or any DDL.

## Verification (required before finishing)
- `dotnet build` — actual output pasted in the report, 0 errors.
- `dotnet test` — actual output for the updated test files, all passing.
- For each of the 3 dialogs, confirm via manual code read: no emoji
  remain, `Docs/DESIGN_SYSTEM.md` Variant.Text/MudSwitch conventions
  followed, IsIndividual toggle correctly swaps CompanyCode/VatCode vs
  NationalIdNumber visibility.
- List every file changed with a before/after snippet for the write-path
  SQL changes specifically (param renumbering is error-prone — show the
  full final SQL string for both UPDATE statements so a human can verify
  the positional params line up correctly).
- Confirm `ResolveSupplierDialog`'s and `AssignSupplierDialog`'s updated
  candidate filters compile and match the intended flag logic exactly
  (quote before/after).

## Report
Write to
`.opencode/reports/partner-role-flags-phase3-round1-<YYYYMMDD>-<HHMM>.md`.

## Final step (required)
Run `./bump-version.sh patch`. Note: per the investigation report §7,
two unrelated files (`.opencode/tasks/latest.md`,
`Docs/PARTNER_TYPE_ARCHITECTURE_PLAN.md`) may show as modified from prior
sessions — if the dirty-tree guard blocks the bump because of THOSE
files specifically (not files this task touched), report it clearly to
the human rather than committing unrelated changes to work around it.
