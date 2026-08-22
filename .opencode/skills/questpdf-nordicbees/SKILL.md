---
name: questpdf-nordicbees
description: Guide for generating or editing PDF documents (invoices, quality certificates / kokybės pažymėjimas, CMR documents, labels) in NordicBeesERP using QuestPDF. Use this whenever creating, modifying, or debugging a PDF-generating service or document layout.
---

# QuestPDF — NordicBeesERP Usage Guide

This project uses **QuestPDF** (fluent C# API, no HTML/wkhtmltopdf) for all generated PDFs — invoices, quality certificates, and (planned) CMR documents.

Official docs: https://www.questpdf.com/

## Three-layer architecture (follow this, don't collapse layers)

1. **Document Model** — plain C# classes holding only the data to render (e.g. `InvoiceModel`, `Address`, line items). No formatting logic here.
2. **Data source / assembly** — a service method that queries the DB and builds the Document Model. This is where `CompanySettings`, invoice records, etc. get pulled in — **never hardcode company name/VAT here, see the git-workflow-nordicbees skill.**
3. **Document definition** — a class implementing `IDocument` (or a `Compose` method) that takes the Document Model and lays it out with QuestPDF's fluent API. Keep this layer free of DB/service calls — it should be a pure function of the model.

## Core fluent API shape

```csharp
public class InvoiceDocument : IDocument
{
    private readonly InvoiceModel _model;
    public InvoiceDocument(InvoiceModel model) => _model = model;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Margin(30);
            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().AlignCenter().Text(x =>
            {
                x.CurrentPageNumber();
                x.Span(" / ");
                x.TotalPages();
            });
        });
    }

    private void ComposeHeader(IContainer container) { /* ... */ }
    private void ComposeContent(IContainer container) { /* ... */ }
}
```

Generate bytes/stream:
```csharp
var pdfBytes = new InvoiceDocument(model).GeneratePdf();
// or: document.GeneratePdf(filePath);
```

## Project-specific rules

- **File paths**: use `IWebHostEnvironment.WebRootPath` (injected), never `Directory.GetCurrentDirectory()`, for any logo/asset/output path. This has caused real bugs in this project (see FROZEN.md).
- **Localization**: use `PdfLocalization.cs` (already exists in `Models/`) for currency (EUR) and country name strings — don't inline "EUR" or country names as raw literals scattered across document classes; centralize through that helper.
- **VAT/amounts**: pull `SubtotalExclVat`/`TotalVat`/`Total` directly from the invoice record being rendered — never recompute a VAT amount from a hardcoded rate inside the PDF layer (see git-workflow-nordicbees skill — this is a CI-blocking violation).
- **Dates**: for any BRC8-relevant document (delivery receipts, labels, certificates), use the actual business date field (e.g. `delivery.DeliveryDate`) — never `DateTime.Now` — matching the project's traceability requirements.
- **Tables**: QuestPDF's `Table` component (with `.Columns(cols => ...)`, `.Header(header => ...)`, `.Cell()`) is the standard way to render line items — prefer it over manual positioning for any tabular content (invoice lines, weight correction history, etc.).
- **Fonts/branding**: check `Docs/DESIGN_SYSTEM.md` / existing document classes (search `Services/` for existing `IDocument` implementations) for the established color scheme and font before introducing new styling — stay consistent with what's already shipped.

## Testing/preview

QuestPDF supports a hot-reload preview (`.ShowInPreviewer()` during local dev) — mention this to the user if they want to visually iterate on layout, but don't leave preview calls in committed code.
