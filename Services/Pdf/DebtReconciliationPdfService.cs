using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NordicBeesERP.Helpers;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NordicBeesERP.Services.Pdf;

/// <summary>
/// Generates the debt reconciliation statement PDF (LT/EN) from a
/// DebtReconciliationResult. Branding (logo, company block, page footer)
/// is delegated to BrandedReportHeader so all report PDFs share one look.
/// </summary>
public class DebtReconciliationPdfService
{
    private readonly ICompanySettingsService _companySettingsService;
    private readonly BrandedReportHeader _brandedHeader;
    private readonly ILogger<DebtReconciliationPdfService> _logger;

    public DebtReconciliationPdfService(
        ICompanySettingsService companySettingsService,
        BrandedReportHeader brandedHeader,
        ILogger<DebtReconciliationPdfService> logger)
    {
        _companySettingsService = companySettingsService;
        _brandedHeader = brandedHeader;
        _logger = logger;
    }

    public async Task<byte[]> GeneratePdfAsync(DebtReconciliationResult result, ReportLanguage lang)
    {
        var settings = await _companySettingsService.GetSettingsAsync();

        QuestPDF.Settings.License = LicenseType.Community;
        var document = new DebtReconciliationDocument(result, DebtReconciliationLabels.For(lang), settings, _brandedHeader, lang);
        return document.GeneratePdf();
    }
}

/// <summary>
/// QuestPDF layout for the debt reconciliation statement. Pure function of
/// its model — no DB or service calls in here.
/// </summary>
internal class DebtReconciliationDocument : IDocument
{
    private readonly DebtReconciliationResult _result;
    private readonly DebtReconciliationLabels.Set _labels;
    private readonly CompanySettings _settings;
    private readonly BrandedReportHeader _brandedHeader;
    private readonly ReportLanguage _lang;

    public DebtReconciliationDocument(
        DebtReconciliationResult result,
        DebtReconciliationLabels.Set labels,
        CompanySettings settings,
        BrandedReportHeader brandedHeader,
        ReportLanguage lang)
    {
        _result = result;
        _labels = labels;
        _settings = settings;
        _brandedHeader = brandedHeader;
        _lang = lang;
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
        var year = _result.PeriodStart.Year;
        var endMonth = (_result.PeriodEnd.Month == 12 && _result.PeriodEnd.Day == 31) ? (int?)null : _result.PeriodEnd.Month;
        var periodLabel = DebtReconciliationLabels.FormatPeriod(_lang, year, endMonth);

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
                        partner.Item().Text(text =>
                        {
                            text.Span($"{_labels.PartnerLabel}: ").SemiBold().FontSize(9.5f);
                            text.Span(_result.PartnerName).FontSize(9.5f);
                        });

                    if (!string.IsNullOrWhiteSpace(_result.PartnerCode))
                        partner.Item().Text(text =>
                        {
                            text.Span($"{_labels.CompanyCodeLabel}: ").SemiBold().FontColor(MutedText);
                            text.Span(_result.PartnerCode).FontColor(MutedText);
                        });

                    if (!string.IsNullOrWhiteSpace(_result.PartnerAddress))
                        partner.Item().Text(text =>
                        {
                            text.Span($"{_labels.AddressLabel}: ").SemiBold().FontColor(MutedText);
                            text.Span(_result.PartnerAddress).FontColor(MutedText);
                        });
                });
            }

            // b) Opening balance — highlighted stat row, separate from the card above.
            column.Item().Row(row =>
            {
                row.RelativeItem().Text(_labels.OpeningBalance).FontColor(MutedText);
                row.RelativeItem().AlignRight().Text($"{FormatAmount(_result.OpeningBalance)} EUR").FontSize(11).Bold().FontColor(BrandPrimary);
            });

            // c) Ledger table. Date columns narrowed, amount columns widened so
            // figures like "820 848.91" never wrap onto a second line.
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.0f);  // Doc date
                    columns.RelativeColumn(1.0f);  // Due date
                    columns.RelativeColumn(1.5f);  // Doc no
                    columns.RelativeColumn(1.55f); // Debit
                    columns.RelativeColumn(1.55f); // Credit
                    columns.RelativeColumn(1.65f); // Balance
                });

                table.Header(header =>
                {
                    header.Cell().Element(c => HeaderCellStyle(c)).Text(_labels.DocDate);
                    header.Cell().Element(c => HeaderCellStyle(c)).Text(_labels.DueDate);
                    header.Cell().Element(c => HeaderCellStyle(c)).Text(_labels.DocNo);
                    header.Cell().Element(c => HeaderCellStyle(c, right: true)).Text($"{_labels.Debit}, EUR");
                    header.Cell().Element(c => HeaderCellStyle(c, right: true)).Text($"{_labels.Credit}, EUR");
                    header.Cell().Element(c => HeaderCellStyle(c, right: true)).Text($"{_labels.Balance}, EUR");

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

                    table.Cell().Element(c => CellStyle(c, zebra)).Text(line.DocumentDate.ToString("yyyy-MM-dd"));
                    table.Cell().Element(c => CellStyle(c, zebra)).Text(line.DueDate?.ToString("yyyy-MM-dd") ?? "—").FontColor(line.DueDate.HasValue ? Colors.Black : Colors.Grey.Lighten1);
                    table.Cell().Element(c => CellStyle(c, zebra)).Text(line.DocumentNumber).SemiBold();
                    table.Cell().Element(c => CellStyle(c, zebra, right: true)).Text(line.Debit > 0 ? FormatAmount(line.Debit) : "—").FontColor(line.Debit > 0 ? Colors.Black : Colors.Grey.Lighten1);
                    table.Cell().Element(c => CellStyle(c, zebra, right: true)).Text(line.Credit > 0 ? FormatAmount(line.Credit) : "—").FontColor(line.Credit > 0 ? Colors.Black : Colors.Grey.Lighten1);
                    table.Cell().Element(c => CellStyle(c, zebra, right: true)).Text(FormatAmount(line.RunningBalance)).SemiBold();

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
                table.Cell().ColumnSpan(3).Element(TotalsLabelStyle).Text(_labels.Total).SemiBold();
                table.Cell().Element(c => TotalsAmountStyle(c)).AlignRight().Text(FormatAmount(_result.TotalDebit)).SemiBold();
                table.Cell().Element(c => TotalsAmountStyle(c)).AlignRight().Text(FormatAmount(_result.TotalCredit)).SemiBold();
                table.Cell().Element(c => TotalsAmountStyle(c)).AlignRight().Text(FormatAmount(_result.ClosingBalance)).SemiBold().FontColor(BrandPrimary);

                static IContainer TotalsLabelStyle(IContainer c) =>
                    c.Background(TotalsRowBg).PaddingVertical(7).PaddingHorizontal(6).BorderTop(1.25f).BorderColor(BrandPrimary);

                static IContainer TotalsAmountStyle(IContainer c) =>
                    c.Background(TotalsRowBg).PaddingVertical(7).PaddingHorizontal(6).BorderTop(1.25f).BorderColor(BrandPrimary).DefaultTextStyle(x => x.FontSize(9.5f));
            });

            // d) Amount in words for the closing balance — muted, italic caption style.
            var words = _lang == ReportLanguage.LT
                ? NumberToWordsHelper.ConvertToLithuanianWords(Math.Abs(_result.ClosingBalance))
                : NumberToWordsHelper.ConvertToEnglishWords(Math.Abs(_result.ClosingBalance));

            column.Item().Text(text =>
            {
                text.Span($"{_labels.AmountInWords} ").SemiBold().FontSize(8.5f);
                if (_result.ClosingBalance < 0)
                    text.Span(_labels.Minus + " ").Italic().FontSize(8.5f);
                text.Span(words).Italic().FontSize(8.5f).FontColor(MutedText);
            });

            // e) Signature block — an actual ruled line per signer, label beneath it.
            column.Item().PaddingTop(20).Row(row =>
            {
                row.RelativeItem().Column(sigs =>
                {
                    sigs.Spacing(22);

                    sigs.Item().Column(line =>
                    {
                        line.Item().Height(20);
                        line.Item().BorderBottom(0.75f).BorderColor(Colors.Grey.Lighten1);
                        line.Item().PaddingTop(3).Text(_labels.Director).FontSize(8).FontColor(MutedText);
                    });

                    sigs.Item().Column(line =>
                    {
                        line.Item().Height(20);
                        line.Item().BorderBottom(0.75f).BorderColor(Colors.Grey.Lighten1);
                        line.Item().PaddingTop(3).Text(_labels.ChiefAccountant).FontSize(8).FontColor(MutedText);
                    });
                });

                row.ConstantItem(24);

                row.RelativeItem().AlignRight().AlignBottom().Text(_labels.CompanySeal).FontSize(8).FontColor(Colors.Grey.Lighten1);
            });
        });
    }

    private static string FormatAmount(decimal value) =>
        value.ToString("#,##0.00", CultureInfo.InvariantCulture);
}
