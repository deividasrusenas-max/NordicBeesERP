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

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Margin(30);
            page.Size(PageSizes.A4);
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
        container.PaddingTop(8).Column(column =>
        {
            column.Spacing(6);

            // a) Partner block — only emit lines that have content.
            if (!string.IsNullOrWhiteSpace(_result.PartnerName) ||
                !string.IsNullOrWhiteSpace(_result.PartnerCode) ||
                !string.IsNullOrWhiteSpace(_result.PartnerAddress))
            {
                column.Item().Column(partner =>
                {
                    partner.Spacing(2);

                    if (!string.IsNullOrWhiteSpace(_result.PartnerName))
                        partner.Item().Text(text =>
                        {
                            text.Span($"{_labels.PartnerLabel}: ").SemiBold();
                            text.Span(_result.PartnerName);
                        });

                    if (!string.IsNullOrWhiteSpace(_result.PartnerCode))
                        partner.Item().Text(text =>
                        {
                            text.Span($"{_labels.CompanyCodeLabel}: ").SemiBold();
                            text.Span(_result.PartnerCode);
                        });

                    if (!string.IsNullOrWhiteSpace(_result.PartnerAddress))
                        partner.Item().Text(text =>
                        {
                            text.Span($"{_labels.AddressLabel}: ").SemiBold();
                            text.Span(_result.PartnerAddress);
                        });
                });
            }

            // b) Opening balance.
            column.Item().Text(text =>
            {
                text.Span($"{_labels.OpeningBalance}: ").SemiBold();
                text.Span(FormatAmount(_result.OpeningBalance));
            });

            // c) Ledger table.
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.2f); // Doc date
                    columns.RelativeColumn(1.2f); // Due date
                    columns.RelativeColumn(2f);   // Doc no
                    columns.RelativeColumn(1.3f); // Debit
                    columns.RelativeColumn(1.3f); // Credit
                    columns.RelativeColumn(1.3f); // Balance
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCellStyle).Text(_labels.DocDate);
                    header.Cell().Element(HeaderCellStyle).Text(_labels.DueDate);
                    header.Cell().Element(HeaderCellStyle).Text(_labels.DocNo);
                    header.Cell().Element(HeaderCellStyle).AlignRight().Text(_labels.Debit);
                    header.Cell().Element(HeaderCellStyle).AlignRight().Text(_labels.Credit);
                    header.Cell().Element(HeaderCellStyle).AlignRight().Text(_labels.Balance);

                    static IContainer HeaderCellStyle(IContainer c) =>
                        c.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(4).BorderBottom(1).BorderColor(Colors.Black);
                });

                foreach (var line in _result.Lines)
                {
                    table.Cell().Element(CellStyle).Text(line.DocumentDate.ToShortDateString());
                    table.Cell().Element(CellStyle).Text(line.DueDate?.ToShortDateString() ?? "");
                    table.Cell().Element(CellStyle).Text(line.DocumentNumber);
                    table.Cell().Element(CellStyle).AlignRight().Text(FormatAmount(line.Debit));
                    table.Cell().Element(CellStyle).AlignRight().Text(FormatAmount(line.Credit));
                    table.Cell().Element(CellStyle).AlignRight().Text(FormatAmount(line.RunningBalance));

                    static IContainer CellStyle(IContainer c) =>
                        c.PaddingVertical(3).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                }

                // Totals row: label in the DocNo column, totals right-aligned.
                table.Cell().Element(TotalsCellStyle).Text("");
                table.Cell().Element(TotalsCellStyle).Text("");
                table.Cell().Element(TotalsCellStyle).Text(_labels.Total).SemiBold();
                table.Cell().Element(TotalsCellStyle).AlignRight().Text(FormatAmount(_result.TotalDebit)).SemiBold();
                table.Cell().Element(TotalsCellStyle).AlignRight().Text(FormatAmount(_result.TotalCredit)).SemiBold();
                table.Cell().Element(TotalsCellStyle).AlignRight().Text(FormatAmount(_result.ClosingBalance)).SemiBold();

                static IContainer TotalsCellStyle(IContainer c) =>
                    c.PaddingVertical(4).BorderTop(1).BorderColor(Colors.Black);
            });

            // d) Amount in words for the closing balance.
            var words = _lang == ReportLanguage.LT
                ? NumberToWordsHelper.ConvertToLithuanianWords(Math.Abs(_result.ClosingBalance))
                : NumberToWordsHelper.ConvertToEnglishWords(Math.Abs(_result.ClosingBalance));

            column.Item().Text(text =>
            {
                text.Span($"{_labels.AmountInWords} ").SemiBold();
                if (_result.ClosingBalance < 0)
                    text.Span(_labels.Minus + " ");
                text.Span(words);
            });

            // e) Signature block.
            column.Item().PaddingTop(24).Row(row =>
            {
                row.RelativeItem().Column(sigs =>
                {
                    sigs.Spacing(16);
                    sigs.Item().Text(_labels.Director);
                    sigs.Item().Text(_labels.ChiefAccountant);
                });

                row.RelativeItem().AlignRight().AlignBottom().Text(_labels.CompanySeal);
            });
        });
    }

    private static string FormatAmount(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture) + " EUR";
}
