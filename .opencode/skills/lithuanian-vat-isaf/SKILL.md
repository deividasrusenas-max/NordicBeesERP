---
name: lithuanian-vat-isaf
description: Lithuanian VAT (PVM) rate categories and i.SAF XML export conventions for NordicBeesERP. Use this whenever working on invoice VAT calculation, VAT rate selection, or the i.SAF XML export module (VMI tax authority reporting).
---

# Lithuanian VAT (PVM) & i.SAF Export — NordicBeesERP

**Scope reminder**: this project intentionally does NOT build a full accounting module — i.SAF XML export is the only accounting-adjacent deliverable. Formal accounting stays in dedicated external software. Don't scope-creep into general ledger / bookkeeping features.

**Status note**: i.SAF export implementation is waiting on a sample XML file from the company's external accountant before being built — treat any structure below as a starting reference to align with that sample once available, not as a final, verified spec. Flag this explicitly if asked to implement i.SAF export before a real sample has been reviewed.

## VAT rate categories used in this project

- **PVM1** — 21% standard domestic rate.
- **PVM6** — 6% reduced rate.
- **PVM16** — 0% intra-EU supply (reverse charge / triangulation within EU).
- **PVM21** — 0% export outside EU.

i.SAF requires invoice lines to be **grouped/reported separately per VAT rate category** — a single invoice with mixed-rate lines needs to be split into separate i.SAF line entries per rate, not summed into one blended line.

## General principles

- **Never hardcode a VAT rate as a literal** anywhere in code — always read it from `CompanySettings.DefaultVatRate` for defaults, or from the actual stored invoice line's rate/amount fields for reporting/export. This is a CI-blocking rule in this project (see git-workflow-nordicbees skill).
- **VAT amount fields**: use the invoice's stored `SubtotalExclVat`/`TotalVat` values directly — don't recompute from a rate multiplied against subtotal, since rounding/manual adjustments on the original invoice must be preserved exactly in the export.
- **Currency**: EUR only for this project's invoicing — no multi-currency handling needed.

## Before implementing i.SAF export

1. Confirm a real sample XML has been provided by the accountant and reviewed — do not guess at VMI's exact XML schema/namespace/element names from general knowledge, since i.SAF format details (element ordering, required vs optional fields, date formats) are precise and audited by VMI.
2. Map each of this project's existing invoice VAT categories (PVM1/PVM6/PVM16/PVM21) to the corresponding i.SAF `<PVMklas>` (or equivalent) code from the real sample.
3. Validate against a few real historical invoices spanning more than one VAT category before considering the export "done" — a single-rate test invoice won't catch line-grouping bugs.
