using System.IO;
using Microsoft.AspNetCore.Hosting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using NordicBeesERP.Models;

namespace NordicBeesERP.Services.Pdf;

/// <summary>
/// Reusable branded header/footer for report PDFs (e.g. the debt
/// reconciliation statement). Brand colors are centralized here so every
/// report shares the same look. Language-agnostic: callers pass
/// already-localized reportTitle and periodLabel strings.
/// </summary>
public class BrandedReportHeader
{
    private static readonly Color Primary = Color.FromHex("#4f7cac");
    private static readonly Color Secondary = Color.FromHex("#7fb685");

    private readonly IWebHostEnvironment _webHostEnvironment;

    public BrandedReportHeader(IWebHostEnvironment webHostEnvironment)
    {
        _webHostEnvironment = webHostEnvironment;
    }

    public void ComposeHeader(IContainer container, CompanySettings company, string reportTitle, string periodLabel)
    {
        container.Column(column =>
        {
            column.Spacing(6);

            // Branding row: company details on the left, logo on the right.
            column.Item().BorderBottom(1).BorderColor(Primary).PaddingBottom(8).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(company.CompanyName ?? "").FontSize(12).Bold().FontColor(Primary);

                    if (!string.IsNullOrEmpty(company.CompanyCode))
                        col.Item().Text(t => { t.Span("Įmonės kodas: ").FontSize(8); t.Span(company.CompanyCode).FontSize(8); });

                    if (!string.IsNullOrEmpty(company.Address))
                        col.Item().Text(t => { t.Span("Adresas: ").FontSize(8); t.Span(company.Address).FontSize(8); });

                    if (!string.IsNullOrEmpty(company.VatCode))
                        col.Item().Text(t => { t.Span("PVM kodas: ").FontSize(8); t.Span(company.VatCode).FontSize(8); });
                });

                var logoPath = Path.Combine(_webHostEnvironment.WebRootPath, "logo.png");
                if (File.Exists(logoPath))
                    row.ConstantItem(110).AlignCenter().AlignMiddle().Image(logoPath).FitWidth();
            });

            // Report title (left) + period (right).
            column.Item().Row(row =>
            {
                row.RelativeItem().Text(reportTitle).FontSize(14).Bold().FontColor(Primary);
                row.RelativeItem().AlignRight().Text(periodLabel).FontSize(10).FontColor(Secondary);
            });
        });
    }

    public void ComposeFooter(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().AlignLeft().Text("Sugeneruota NordicBeesERP").FontSize(8).FontColor(Colors.Grey.Medium);
            row.RelativeItem().AlignRight().Text(t =>
            {
                t.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                t.Span(" / ").FontSize(8).FontColor(Colors.Grey.Medium);
                t.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
            });
        });
    }
}
