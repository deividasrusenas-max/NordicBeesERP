# Task: Investigate business-partner type system & expense-supplier/category assignment

## Type: READ-ONLY INVESTIGATION (no code changes)

## Why
Before touching the Suppliers/Customers add/edit dialogs, we need a clear,
accurate picture of:
1. How partner "types" (Tiekėjas/supplier, Pirkėjas/customer, Ūkininkas/
   farmer, and any others) are actually modeled and distinguished in code
   today — is it one shared table with a type flag/enum, separate tables,
   or something else?
2. How "expense supplier" (išlaidų tiekėjas) works and how a supplier
   gets assigned a default expense group/category (`DefaultExpenseCategoryId`
   → `ExpenseCategory`).

The goal of this task is ONLY to gather and report facts. Do not propose
or make any fixes yet — just surface the actual current behavior,
inconsistencies, and any bugs/foot-guns you find, in enough detail that a
human can then decide how the add/edit dialogs should change.

## Steps

### 1. Data model — partner types
- Read `Models/Models_Part1.cs` and `Models/Models_Part2.cs` in full for
  every partner-related class: `BusinessPartner`, `Supplier`, `Customer`,
  and anything else that represents a person/company the business deals
  with. For each, list every field related to "type" or classification
  (enum, bool flags like `IsCustomer`/`IsSupplier`/`IsFarmer`, a
  `PartnerType` string/enum field, `NationalIdNumber` vs `CompanyCode`
  used as a farmer-vs-company signal, etc.) — quote the actual field
  names and types.
- Check whether `Supplier` and `Customer` are separate EF entities/tables
  or the same table filtered by a type column. Check for any
  inheritance (TPH/TPT) in the `DbContext`.
- Grep the codebase for all distinct values ever assigned to any
  "type"-like field (e.g. every literal string like `"Ūkininkas"`,
  `"Įmonė"`, `"Tiekėjas"`, `"Pirkėjas"` etc.) to build a real inventory
  of what type values actually exist in code, not just what the UI tabs
  suggest.

### 2. UI — how type is currently set/shown
- Read `Components/Pages/Suppliers.razor` (all three tab branches) and
  `Components/Pages/Customers.razor` fully, focusing on: how does the
  Ūkininkai/Įmonės/Visi tab split work — is it a filter on a real type
  field, or a heuristic (e.g. "has NationalIdNumber" = farmer)? Quote the
  actual filter predicate for each tab.
- Find and read the Create/Edit dialogs for suppliers and customers
  (search `Components/` for `SupplierDialog`, `SupplierEdit`,
  `CustomerDialog`, `CustomerEdit`, `AssignSupplierDialog`, or similarly
  named files). For each, list every field on the form and whether/how
  the user sets a "type" during creation or editing.
- Check whether a single business partner (e.g. by `BusinessPartnerId`)
  can simultaneously be both a supplier AND a customer in this data
  model, and if so, whether the current UI/dialogs handle that
  correctly or silently overwrite/duplicate records.

### 3. Expense-supplier & expense-category assignment
- Find where "expense supplier" (a supplier used for expense invoices,
  as opposed to a goods/production supplier) is distinguished from a
  regular supplier, if at all — check `Services/ExpenseService.cs` (or
  wherever expense invoice OCR/creation lives) for how it picks/matches
  a supplier record.
- Read the full definition of `ExpenseCategory` in `Models_Part2.cs` and
  every place `DefaultExpenseCategoryId` is read or written (dialogs,
  services, the expense invoice list/creation flow) — confirm whether
  assigning a default category to a supplier is optional or required,
  what happens when it's null, and whether an expense invoice can
  override the supplier's default category per-invoice.
- Check `Docs/BUGLOG.md` for any existing logged issues mentioning
  supplier type, expense category, expense supplier, or partner type
  mismatches.

### 4. Cross-reference with prior work
- Read `.opencode/reports/suppliers-customers-columns-audit-20260904-1200.md`
  and `.opencode/reports/suppliers-customers-columns-final-20260904-0141.md`
  — the earlier column-restructure task found a `PartnerType`-like
  concept during the audit; confirm exactly what it referred to and
  whether it lines up with what you find in step 1, or contradicts it.
- Check `.opencode/reports/payment-term-fix-20260903.md` and any
  `assign-supplier-*` reports for related context on how suppliers get
  matched/assigned during expense workflows.

### 5. Look actively for bugs/inconsistencies
While reading, flag anything that looks like a real problem, for
example (non-exhaustive — report whatever you actually find, don't
force-fit these):
- A farmer/individual (Ūkininkas) being treated as a company somewhere
  (e.g. VAT code validation applied where a national ID should be used).
- A partner that could logically be both supplier and customer, but the
  schema/dialogs assume mutually exclusive roles.
- An expense category default that's silently ignored somewhere in the
  expense invoice flow.
- Any place where a "type" string is compared with the wrong casing,
  wrong literal, or a typo (Lithuanian diacritics mismatches are a known
  recurring bug class in this project per `Docs/BUGLOG.md`).

## Output

Write a full report to
`.opencode/reports/partner-type-expense-category-audit-<YYYYMMDD>-<HHMM>.md`
with these sections:
1. **Data model summary** — exact fields/tables for partner typing, with
   file:line references.
2. **UI behavior summary** — how each tab/dialog currently reads/writes
   type, with file:line references.
3. **Expense-supplier & category assignment flow** — step-by-step of what
   happens today from "supplier has DefaultExpenseCategoryId" to "expense
   invoice uses a category", with file:line references.
4. **Bugs & inconsistencies found** — a numbered list, each with: what's
   wrong, exact file:line, why it's a problem, and severity (low/medium/
   high) — no fix proposals yet, just the finding.
5. **Open questions for the human** — anything genuinely ambiguous that
   needs a product decision before dialogs can be redesigned (e.g. "should
   a partner be allowed to be both supplier and customer?").

Do NOT modify any files in this task. Do NOT propose or write any fix
plan — that comes in a later task once the human has reviewed this
report.

## Final step (required)

Run `./bump-version.sh patch` at the end of this task. Before running,
check `git status --porcelain` — if `.opencode/tasks/latest.md` is the
only uncommitted change, that's expected and fine (harness file, not
code); note it in the report but proceed with the bump as usual for a
read-only task (no code was changed, so this is just a version marker).
