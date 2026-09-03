# BUILD TASK — Invoice-issued indicators + PDF title wording

Read `Docs/FROZEN.md` and `AGENTS.md` first. This build is based on
`.opencode/reports/invoice-issued-indicators-pdf-title-investigation-20260903-1656.md` — read it
fully first; it has exact line numbers and verbatim current code for every file below.

No DB/migration changes in this task — every page already loads full `Delivery` entities with
`InvoiceId`/`InvoiceNumber` populated, so this is UI-only.

## A.1 — `Components/Pages/Warehouse/DeliveryPricing.razor` (list, `/warehouse/delivery-pricing`)

Add a dedicated "Sąskaita" column (this list's whole purpose is the invoicing workflow, so a
column is appropriate here — unlike `DeliveryList.razor`, do not fold into the status chip).

- Header: add `<MudTh>Sąskaita</MudTh>` after the `Statusas` `<MudTh>`.
- Row: add a matching `<MudTd>` after the status `MudTd`:
  ```razor
  <MudTd>
      @if (d.InvoiceId != null)
      {
          <MudButton Variant="Variant.Text" Size="Size.Small" Color="Color.Primary"
              StartIcon="@Icons.Material.Filled.Receipt"
              Href="@($"/invoices/{d.InvoiceId}")">@d.InvoiceNumber</MudButton>
      }
      else
      {
          <MudText Typo="Typo.caption" Style="color:#aaa">—</MudText>
      }
  </MudTd>
  ```

## A.2 — `Components/Pages/Warehouse/DeliveryPricingDetail.razor` (detail, `/warehouse/deliveries/{Id}/pricing`)

In the header `MudStack` (currently: title, "Atgal" button, conditional "Išrašyti sąskaitą"
button gated on `_canIssueInvoice`), add an `else if` branch so that when an invoice already
exists, a clickable indicator replaces the issue button:

```razor
@if (_canIssueInvoice)
{
    <MudButton Variant="Variant.Filled" Color="Color.Primary"
        StartIcon="@Icons.Material.Filled.Receipt"
        OnClick="IssueInvoiceAsync"
        Class="ml-2">Išrašyti sąskaitą</MudButton>
}
else if (_delivery.InvoiceId != null)
{
    <MudButton Variant="Variant.Text" Color="Color.Primary"
        StartIcon="@Icons.Material.Filled.Receipt"
        Href="@($"/invoices/{_delivery.InvoiceId}")"
        Class="ml-2">Sąskaita išrašyta: @_delivery.InvoiceNumber</MudButton>
}
```

While in this file: the header `MudStack` uses `Justify="Justify.SpaceBetween"` with what can now
be 3 children (title / Atgal / invoice button-or-indicator) — verify visually it doesn't look
oddly spread out; if it does, wrap the "Atgal" + invoice-state button in a nested
`MudStack Row="true" Spacing="2"` so SpaceBetween only splits title vs. the button group.

## A.3 — `Components/Pages/Warehouse/DeliveryList.razor` and `Components/Pages/Warehouse/SupplierDebtDetail.razor`

Both already show a "Būsena" status chip driven by `StatusDisplayHelper`. In both, when the
delivery has a non-empty `InvoiceNumber`, add a small receipt icon to that same chip (do NOT add
a new column — a dedicated "Sąskaita" column was intentionally removed from `DeliveryList.razor`
in a prior change). Example pattern for the chip:

```razor
<MudChip T="string" Size="Size.Small" Color="@StatusDisplayHelper.GetColor(context.Status, _statusDisplay)"
    Icon="@(!string.IsNullOrEmpty(context.InvoiceNumber) ? Icons.Material.Filled.Receipt : null)">
    @StatusDisplayHelper.GetLabel(context.Status, _statusDisplay)
</MudChip>
```
(Adjust the context variable name per file — `context` in `DeliveryList.razor`, `delivery` in
`SupplierDebtDetail.razor`, per the investigation report's line references.) Passing `Icon=null`
when there's no invoice must not render an empty icon slot — verify visually.

## A.4 — `Components/Pages/Warehouse/SupplierDebts.razor`

Skip. This table is supplier-aggregated, not per-delivery — no invoice-issued indicator here per
the investigation report's recommendation. Do not touch this file.

## B — PDF title: `Services/PdfGeneratorService.cs` (lines ~273–281 per investigation report)

Change ONLY the display string, not any comparison logic:

```csharp
// BEFORE
string documentTitle = labels.DocumentTitle;
if (isReverseCharge6)
{
    documentTitle = "6% PVM SĄSKAITA FAKTŪRA";
}

// AFTER
string documentTitle = labels.DocumentTitle;
if (isReverseCharge6)
{
    documentTitle = "PVM SĄSKAITA FAKTŪRA";
}
```

**Do NOT touch:**
- `Invoice.InvoiceType` stored string (stays `"6% PVM SĄSKAITA FAKTŪRA"` — used by numbering).
- `isReverseCharge6` computation at `PdfGeneratorService.cs:139` (`Contains("6%")`).
- `GenerateNextInvoiceNumberAsync`'s `Contains("6%")` check in `Services/InvoiceService.cs:433`.
- The VAT% table cell (`displayVatRate`, ~line 336) — that's the actual 6% VAT rate, correct as-is.
- `InvoiceView.razor`'s `GetInvoiceTypeLabel` and `InvoiceDetailDialog.razor`'s verbatim
  `InvoiceType` display — out of scope for this task (PDF-only per the request); leave them
  showing "6%" as today. Note this inconsistency in your report but do not fix it.

## Constraints

- `dotnet build` must pass, 0 errors, before finishing.
- This diff touches Lithuanian UI strings ("Sąskaita", "Sąskaita išrašyta: ...") — reviewer must
  quote every changed Lithuanian string verbatim in its verdict per AGENTS.md.
- No DB/migration/service-layer changes — this is UI + one PDF string only.

## Report

Write to `.opencode/reports/invoice-issued-indicators-pdf-title-build-<YYYYMMDD>-<HHMM>.md`
listing every file changed and confirming the clean `dotnet build`.

## Required last step

Run `./bump-version.sh patch`.
