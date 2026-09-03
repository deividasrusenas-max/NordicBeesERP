# INVESTIGATION TASK — Invoice-issued indicators + PDF title wording

READ-ONLY. Do not edit/create files, do not run migrations, do not touch the DB. Report to
`.opencode/reports/`.

## Context

Feature just shipped: `/warehouse/deliveries/{Id}/pricing` (`DeliveryPricingDetail.razor`) has an
"Išrašyti sąskaitą" button that creates a 6% VAT invoice from a delivery
(`InvoiceService.CreateInvoiceFromDeliveryAsync`), setting `Delivery.InvoiceId` /
`Delivery.InvoiceNumber` and `Invoice.InvoiceType = "6% PVM SĄSKAITA FAKTŪRA"`.

Two follow-up requests:

**A) Show "invoice issued" state in more places.**
1. `/warehouse/delivery-pricing` (list page, `Components/Pages/Warehouse/DeliveryPricing.razor`)
   — must show that a delivery already has an invoice (currently no such column/indicator; the
   table only has Nr./Data/Tiekėjas/Netto/Statusas/Suma/Sumokėta/Skola columns, per
   `GetStatusBadge` logic).
2. `Components/Pages/Warehouse/DeliveryPricingDetail.razor` — when `_delivery.InvoiceId != null`,
   the "Išrašyti sąskaitą" button should be replaced by (or accompanied by) a clear "Sąskaita
   išrašyta: <InvoiceNumber>" indicator, ideally clickable/linking to the invoice view.
3. Report on any OTHER page in the codebase that lists/displays `Delivery` records and could
   reasonably show invoice-issued status too — check at minimum
   `Components/Pages/Warehouse/DeliveryList.razor` (note: this page's "Sąskaita" column was
   recently REMOVED and replaced with a "Tara" column per an explicit prior user decision — do
   NOT recommend reverting that; instead consider whether a small non-intrusive indicator, e.g.
   folded into the existing "Būsena" status chip, would fit without re-adding a dedicated
   column), `Components/Pages/Warehouse/SupplierDebts.razor`, and
   `Components/Pages/Warehouse/SupplierDebtDetail.razor`. Report what each currently shows for a
   delivery row and whether adding an invoice indicator is straightforward given the existing
   data already loaded on that page (avoid recommending pages that would need a whole new data
   join just for this).

**B) PDF title wording.**
Find where the generated invoice PDF renders its title/heading text (likely
`Services/PdfGeneratorService.cs` and/or something in `Services/Pdf/`). Confirm:
- The exact spot where `Invoice.InvoiceType` (or a derived string) is printed on the PDF as the
  document title.
- Whether the current PDF title literally shows "6%" or "6 proc." wording, sourced from
  `Invoice.InvoiceType = "6% PVM SĄSKAITA FAKTŪRA"`.
- Confirm `GenerateNextInvoiceNumberAsync` (`Services/InvoiceService.cs`) and
  `CreateInvoiceFromDeliveryAsync`'s numbering logic BOTH key off `invoiceType.Contains("6%")` to
  decide the ULAK vs LAK number series — so the underlying `InvoiceType` string cannot simply be
  changed to drop "6%" without breaking numbering, unless the PDF display layer is changed
  separately from the stored/compared string.
- Check whether any other place in the app (InvoiceView.razor, invoice list, InvoiceEdit.razor)
  also displays `InvoiceType` verbatim to the user and would need the same wording fix for
  consistency, or whether the request is PDF-only.

## Output

Write findings to `.opencode/reports/invoice-issued-indicators-pdf-title-investigation-<YYYYMMDD>-<HHMM>.md`
with exact file paths, line numbers/verbatim snippets for: (1) the PDF title rendering code, (2)
the `DeliveryPricing.razor` table structure (header+row template, to plan a new column), (3) the
`DeliveryPricingDetail.razor` button/header area (already known but re-confirm current state
after the delivery-invoice-costs build), (4) findings on DeliveryList.razor / SupplierDebts.razor
/ SupplierDebtDetail.razor per point A.3, (5) a recommended, minimal-diff plan for both A and B
that doesn't touch invoice numbering logic.

Do not modify any files.
