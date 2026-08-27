using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NordicBeesERP.Services.Pdf;

/// <summary>
/// Generates the statement-of-unpaid-invoices PDF (LT/EN) from an
/// UnpaidInvoicesResult. Branding (logo, company block, page footer)
/// is delegated to BrandedReportHeader so all report PDFs share one look.
/// </summary>
public class UnpaidInvoicesPdfService
{
    private readonly ICompanySettingsService _companySettingsService;
    private readonly BrandedReportHeader _brandedHeader;
    private readonly ILogger<UnpaidInvoicesPdfService> _logger;

    public UnpaidInvoicesPdfService(
        ICompanySettingsService companySettingsService,
        BrandedReportHeader brandedHeader,
        ILogger<UnpaidInvoicesPdfService> logger)
    {
        _companySettingsService = companySettingsService;
        _brandedHeader = brandedHeader;
        _logger = logger;
    }

    public async Task<byte[]> GeneratePdfAsync(UnpaidInvoicesResult result, ReportLanguage lang)
    {
        var settings = await _companySettingsService.GetSettingsAsync();

        QuestPDF.Settings.License = LicenseType.Community;
        var document = new UnpaidInvoicesDocument(result, UnpaidInvoicesLabels.For(lang), settings, _brandedHeader);
        return document.GeneratePdf();
    }
}

/// <summary>
/// QuestPDF layout for the statement of unpaid invoices. Pure function of
/// its model — no DB or service calls in here.
/// </summary>
internal class UnpaidInvoicesDocument : IDocument
{
    private readonly UnpaidInvoicesResult _result;
    private readonly UnpaidInvoicesLabels.Set _labels;
    private readonly CompanySettings _settings;
    private readonly BrandedReportHeader _brandedHeader;

    public UnpaidInvoicesDocument(
        UnpaidInvoicesResult result,
        UnpaidInvoicesLabels.Set labels,
        CompanySettings settings,
        BrandedReportHeader brandedHeader)
    {
        _result = result;
        _labels = labels;
        _settings = settings;
        _brandedHeader = brandedHeader;
    }

    private static readonly Color TableHeaderBg = Color.FromHex("#eef2f7");
    private static readonly Color ZebraRowBg = Color.FromHex("#f8fafc");
    private static readonly Color TotalsRowBg = Color.FromHex("#eef2f7");
    private static readonly Color CardBg = Color.FromHex("#f8fafc");
    private static readonly Color MutedText = Colors.Grey.Darken1;
    private static readonly Color BrandPrimary = Color.FromHex("#4f7cac");

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Margin(32);
            page.Size(PageSizes.A4);
            page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Black).LineHeight(1.15f));
            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().Element(c => _brandedHeader.ComposeFooter(c));
        });
    }

    private void ComposeHeader(IContainer container)
    {
        var periodLabel = $"{_result.PeriodStart:yyyy-MM-dd} – {_result.PeriodEnd:yyyy-MM-dd}";
        _brandedHeader.ComposeHeader(container, _settings, _labels.Title, periodLabel);
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingTop(14).Column(column =>
        {
            column.Spacing(16);

            // a) Partner block — light card, only emitted if there is content.
            if (!string.IsNullOrWhiteSpace(_result.PartnerName) ||
                !string.IsNullOrWhiteSpace(_result.PartnerCode) ||
                !string.IsNullOrWhiteSpace(_result.PartnerAddress))
            {
                column.Item().Background(CardBg).Padding(10).Column(partner =>
                {
                    partner.Spacing(3);

                    if (!string.IsNullOrWhiteSpace(_result.PartnerName))
                        partner.Item().Text(_result.PartnerName).Bold().FontSize(9.5f);

                    if (!string.IsNullOrWhiteSpace(_result.PartnerCode))
                        partner.Item().Text(_result.PartnerCode).FontColor(MutedText);

                    if (!string.IsNullOrWhiteSpace(_result.PartnerAddress))
                        partner.Item().Text(_result.PartnerAddress).FontColor(MutedText);
                });
            }

            // b) Invoice table: 5 columns, amount columns widened so figures
            // like "820 848.91" never wrap onto a second line.
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.6f);  // Invoice no
                    columns.RelativeColumn(1.1f);  // Date
                    columns.RelativeColumn(1.2f);  // Due date
                    columns.RelativeColumn(1.45f); // Amount
                    columns.RelativeColumn(1.45f); // Balance due
                });

                table.Header(header =>
                {
                    header.Cell().Element(c => HeaderCellStyle(c)).Text(_labels.ColInvoiceNo);
                    header.Cell().Element(c => HeaderCellStyle(c)).Text(_labels.ColDate);
                    header.Cell().Element(c => HeaderCellStyle(c)).Text(_labels.ColDueDate);
                    header.Cell().Element(c => HeaderCellStyle(c, right: true)).Text($"{_labels.ColAmount}, EUR");
                    header.Cell().Element(c => HeaderCellStyle(c, right: true)).Text($"{_labels.ColBalanceDue}, EUR");

                    static IContainer HeaderCellStyle(IContainer c, bool right = false)
                    {
                        var styled = c.Background(TableHeaderBg)
                            .PaddingVertical(6).PaddingHorizontal(6)
                            .BorderBottom(1.25f).BorderColor(BrandPrimary)
                            .DefaultTextStyle(x => x.SemiBold().FontSize(8.5f).FontColor(Colors.Grey.Darken3));
                        return right ? styled.AlignRight() : styled;
                    }
                });

                for (var i = 0; i < _result.Lines.Count; i++)
                {
                    var line = _result.Lines[i];
                    var zebra = i % 2 == 1;

                    table.Cell().Element(c => CellStyle(c, zebra)).Text(line.InvoiceNumber).SemiBold();
                    table.Cell().Element(c => CellStyle(c, zebra)).Text(line.InvoiceDate.ToString("yyyy-MM-dd"));
                    table.Cell().Element(c => CellStyle(c, zebra)).Text(line.DueDate?.ToString("yyyy-MM-dd") ?? "—").FontColor(line.DueDate.HasValue ? Colors.Black : Colors.Grey.Lighten1);
                    table.Cell().Element(c => CellStyle(c, zebra, right: true)).Text(FormatAmount(line.TotalAmount));
                    table.Cell().Element(c => CellStyle(c, zebra, right: true)).Text(FormatAmount(line.RemainingAmount)).SemiBold();

                    static IContainer CellStyle(IContainer c, bool zebra, bool right = false)
                    {
                        var styled = (zebra ? c.Background(ZebraRowBg) : c)
                            .PaddingVertical(5).PaddingHorizontal(6)
                            .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                            .DefaultTextStyle(x => x.FontSize(8.5f));
                        return right ? styled.AlignRight() : styled;
                    }
                }

                // Totals row: label spans the first three (non-amount) columns so
                // it reads as one clear row instead of a floating word.
                table.Cell().ColumnSpan(3).Element(TotalsLabelStyle).Text(_labels.TotalLabel).SemiBold();
                table.Cell().Element(c => TotalsAmountStyle(c)).AlignRight().Text(FormatAmount(_result.TotalAmount)).SemiBold();
                table.Cell().Element(c => TotalsAmountStyle(c)).AlignRight().Text(FormatAmount(_result.TotalRemaining)).SemiBold().FontColor(BrandPrimary);

                static IContainer TotalsLabelStyle(IContainer c) =>
                    c.Background(TotalsRowBg).PaddingVertical(7).PaddingHorizontal(6).BorderTop(1.25f).BorderColor(BrandPrimary);

                static IContainer TotalsAmountStyle(IContainer c) =>
                    c.Background(TotalsRowBg).PaddingVertical(7).PaddingHorizontal(6).BorderTop(1.25f).BorderColor(BrandPrimary).DefaultTextStyle(x => x.FontSize(9.5f));
            });
        });
    }

    private static string FormatAmount(decimal value) =>
        value.ToString("N2", CultureInfo.InvariantCulture) + " €";
}
