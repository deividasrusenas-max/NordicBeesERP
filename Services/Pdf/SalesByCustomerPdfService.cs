using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
/// Generates the "Sales by Customer" (Prekių pardavimo suvestinė) report PDF
/// (LT/EN) from a SalesByCustomerReportResult. Branding (logo, company block,
/// page footer) is delegated to BrandedReportHeader so all report PDFs share
/// one look.
/// </summary>
public class SalesByCustomerPdfService
{
    private readonly ICompanySettingsService _companySettingsService;
    private readonly BrandedReportHeader _brandedHeader;
    private readonly ILogger<SalesByCustomerPdfService> _logger;

    public SalesByCustomerPdfService(
        ICompanySettingsService companySettingsService,
        BrandedReportHeader brandedHeader,
        ILogger<SalesByCustomerPdfService> logger)
    {
        _companySettingsService = companySettingsService;
        _brandedHeader = brandedHeader;
        _logger = logger;
    }

    public async Task<byte[]> GeneratePdfAsync(SalesByCustomerReportResult result, ReportLanguage lang)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var settings = await _companySettingsService.GetSettingsAsync();
        var document = new SalesByCustomerDocument(result, SalesByCustomerReportLabels.For(lang), settings, _brandedHeader, lang);
        return document.GeneratePdf();
    }
}

/// <summary>
/// QuestPDF layout for the sales-by-customer report. Pure function of its
/// model — no DB or service calls in here.
/// </summary>
internal class SalesByCustomerDocument : IDocument
{
    private readonly SalesByCustomerReportResult _result;
    private readonly SalesByCustomerReportLabels.Set _labels;
    private readonly CompanySettings _settings;
    private readonly BrandedReportHeader _brandedHeader;
    private readonly ReportLanguage _lang;

    public SalesByCustomerDocument(
        SalesByCustomerReportResult result,
        SalesByCustomerReportLabels.Set labels,
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
    private static readonly Color BrandPrimary = Color.FromHex("#4f7cac");
    private static readonly Color MutedText = Colors.Grey.Darken1;

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
        string periodLabel;
        if (_result.FromDate == null && _result.ToDate == null)
            periodLabel = _labels.AllCustomers;
        else if (_result.FromDate == null)
            periodLabel = _result.ToDate.Value.ToString("yyyy-MM-dd");
        else if (_result.ToDate == null)
            periodLabel = _result.FromDate.Value.ToString("yyyy-MM-dd");
        else
            periodLabel = $"{_result.FromDate:yyyy-MM-dd} – {_result.ToDate:yyyy-MM-dd}";

        _brandedHeader.ComposeHeader(container, _settings, _labels.Title, periodLabel);
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingTop(14).Column(column =>
        {
            column.Spacing(14);

            // a) Customer filter caption.
            column.Item().Text($"{_labels.Customer}: {_result.CustomerFilter}").FontSize(9).FontColor(MutedText);

            // b) Main table: customer / product / invoice lines with per-product
            // and per-customer subtotals, then the grand total row.
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.4f);  // Customer
                    columns.RelativeColumn(1.6f);  // Product
                    columns.RelativeColumn(1.3f);  // Invoice no
                    columns.RelativeColumn(1.0f);  // Date
                    columns.RelativeColumn(0.9f);  // Quantity
                    columns.RelativeColumn(1.0f);  // Unit price
                    columns.RelativeColumn(1.1f);  // Amount
                });

                table.Header(header =>
                {
                    header.Cell().Element(c => HeaderCellStyle(c)).Text(_labels.Customer);
                    header.Cell().Element(c => HeaderCellStyle(c)).Text(_labels.Product);
                    header.Cell().Element(c => HeaderCellStyle(c)).Text(_labels.InvoiceNo);
                    header.Cell().Element(c => HeaderCellStyle(c)).Text(_labels.Date);
                    header.Cell().Element(c => HeaderCellStyle(c, right: true)).Text(_labels.Quantity);
                    header.Cell().Element(c => HeaderCellStyle(c, right: true)).Text(_labels.UnitPrice);
                    header.Cell().Element(c => HeaderCellStyle(c, right: true)).Text(_labels.Amount);

                    static IContainer HeaderCellStyle(IContainer c, bool right = false)
                    {
                        var styled = c.Background(TableHeaderBg)
                            .PaddingVertical(6).PaddingHorizontal(6)
                            .BorderBottom(1.25f).BorderColor(BrandPrimary)
                            .DefaultTextStyle(x => x.SemiBold().FontSize(8.5f).FontColor(Colors.Grey.Darken3));
                        return right ? styled.AlignRight() : styled;
                    }
                });

                var rowZebra = 0;
                foreach (var customer in _result.Customers)
                {
                    // Customer group header — spans all 7 columns.
                    table.Cell().ColumnSpan(7).Element(GroupHeaderStyle).Text($"{customer.CustomerName}{(string.IsNullOrEmpty(customer.CustomerCode) ? "" : " (" + customer.CustomerCode + ")")}").SemiBold().FontSize(9.5f).FontColor(BrandPrimary);

                    foreach (var product in customer.Products)
                    {
                        // Product group header — spans all 7 columns.
                        table.Cell().ColumnSpan(7).Element(ProductHeaderStyle).Text(product.ProductCode == "NENUSTATYTA" ? _labels.NoProduct : $"{product.ProductCode} — {product.ProductName}").FontSize(8.5f).SemiBold();

                        foreach (var row in product.Rows)
                        {
                            var zebra = rowZebra % 2 == 1;
                            rowZebra++;

                            if (row.IsCredit)
                            {
                                // Credit / KLAK rows are stored negative — render the
                                // whole row red + italic so they stand out.
                                table.Cell().Element(c => CellStyle(c, zebra)).Text(customer.CustomerName).FontColor(Colors.Red.Darken1).Italic();
                                table.Cell().Element(c => CellStyle(c, zebra)).Text(product.ProductCode == "NENUSTATYTA" ? _labels.NoProduct : product.ProductName).FontColor(Colors.Red.Darken1).Italic();
                                table.Cell().Element(c => CellStyle(c, zebra)).Text(row.DocumentNumber).SemiBold().FontColor(Colors.Red.Darken1).Italic();
                                table.Cell().Element(c => CellStyle(c, zebra)).Text(row.DocumentDate.ToString("yyyy-MM-dd")).FontColor(Colors.Red.Darken1).Italic();
                                table.Cell().Element(c => CellStyle(c, zebra, right: true)).Text(FormatAmount(row.Quantity)).FontColor(Colors.Red.Darken1).Italic();
                                table.Cell().Element(c => CellStyle(c, zebra, right: true)).Text(FormatAmount(row.UnitPrice)).FontColor(Colors.Red.Darken1).Italic();
                                table.Cell().Element(c => CellStyle(c, zebra, right: true)).Text(FormatAmount(row.LineTotal)).FontColor(Colors.Red.Darken1).Italic();
                            }
                            else
                            {
                                table.Cell().Element(c => CellStyle(c, zebra)).Text(customer.CustomerName);
                                table.Cell().Element(c => CellStyle(c, zebra)).Text(product.ProductCode == "NENUSTATYTA" ? _labels.NoProduct : product.ProductName);
                                table.Cell().Element(c => CellStyle(c, zebra)).Text(row.DocumentNumber).SemiBold();
                                table.Cell().Element(c => CellStyle(c, zebra)).Text(row.DocumentDate.ToString("yyyy-MM-dd"));
                                table.Cell().Element(c => CellStyle(c, zebra, right: true)).Text(FormatAmount(row.Quantity));
                                table.Cell().Element(c => CellStyle(c, zebra, right: true)).Text(FormatAmount(row.UnitPrice));
                                table.Cell().Element(c => CellStyle(c, zebra, right: true)).Text(FormatAmount(row.LineTotal));
                            }

                            static IContainer CellStyle(IContainer c, bool zebra, bool right = false)
                            {
                                var styled = (zebra ? c.Background(ZebraRowBg) : c)
                                    .PaddingVertical(5).PaddingHorizontal(6)
                                    .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                    .DefaultTextStyle(x => x.FontSize(8.5f));
                                return right ? styled.AlignRight() : styled;
                            }
                        }

                        // Product subtotal row: label spans the 5 non-amount columns.
                        table.Cell().ColumnSpan(5).Element(TotalsLabelStyle).Text($"{_labels.ProductSubtotal}").SemiBold();
                        table.Cell().Element(c => TotalsAmountStyle(c)).AlignRight().Text(FormatAmount(product.TotalQuantity));
                        table.Cell().Element(c => TotalsAmountStyle(c)).AlignRight().Text(FormatAmount(product.TotalAmount));
                    }

                    // Customer total row.
                    table.Cell().ColumnSpan(5).Element(TotalsLabelStyle).Text($"{_labels.CustomerTotal}").SemiBold();
                    table.Cell().Element(c => TotalsAmountStyle(c)).AlignRight().Text(FormatAmount(customer.TotalQuantity));
                    table.Cell().Element(c => TotalsAmountStyle(c)).AlignRight().Text(FormatAmount(customer.TotalAmount));

                    static IContainer GroupHeaderStyle(IContainer c) =>
                        c.Background(TableHeaderBg).PaddingVertical(6).PaddingHorizontal(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2);

                    static IContainer ProductHeaderStyle(IContainer c) =>
                        c.Background(ZebraRowBg).PaddingVertical(4).PaddingHorizontal(6).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2);
                }

                // Grand total row — amount emphasized in brand color.
                table.Cell().ColumnSpan(5).Element(TotalsLabelStyle).Text($"{_labels.GrandTotal}").SemiBold();
                table.Cell().Element(c => TotalsAmountStyle(c)).AlignRight().Text(FormatAmount(_result.GrandTotalQuantity)).SemiBold();
                table.Cell().Element(c => TotalsAmountStyle(c)).AlignRight().Text(FormatAmount(_result.GrandTotalAmount)).SemiBold().FontColor(BrandPrimary);

                static IContainer TotalsLabelStyle(IContainer c) =>
                    c.Background(TotalsRowBg).PaddingVertical(7).PaddingHorizontal(6).BorderTop(1.25f).BorderColor(BrandPrimary);

                static IContainer TotalsAmountStyle(IContainer c) =>
                    c.Background(TotalsRowBg).PaddingVertical(7).PaddingHorizontal(6).BorderTop(1.25f).BorderColor(BrandPrimary).DefaultTextStyle(x => x.FontSize(9.5f));
            });

            // c) Cross-customer product totals section — a small 3-column table.
            if (_result.ProductTotals.Count > 0)
            {
                column.Item().Text(_labels.ProductTotalsSection).FontSize(10).Bold().FontColor(BrandPrimary);

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3.0f);  // Product (code + name)
                        columns.RelativeColumn(1.5f);  // Quantity
                        columns.RelativeColumn(1.5f);  // Amount
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(c => HeaderCellStyle(c)).Text(_labels.Product);
                        header.Cell().Element(c => HeaderCellStyle(c, right: true)).Text(_labels.Quantity);
                        header.Cell().Element(c => HeaderCellStyle(c, right: true)).Text(_labels.Amount);

                        static IContainer HeaderCellStyle(IContainer c, bool right = false)
                        {
                            var styled = c.Background(TableHeaderBg)
                                .PaddingVertical(6).PaddingHorizontal(6)
                                .BorderBottom(1.25f).BorderColor(BrandPrimary)
                                .DefaultTextStyle(x => x.SemiBold().FontSize(8.5f).FontColor(Colors.Grey.Darken3));
                            return right ? styled.AlignRight() : styled;
                        }
                    });

                    for (var i = 0; i < _result.ProductTotals.Count; i++)
                    {
                        var pt = _result.ProductTotals[i];
                        var zebra = i % 2 == 1;

                        table.Cell().Element(c => CellStyle(c, zebra)).Text(pt.ProductCode == "NENUSTATYTA" ? _labels.NoProduct : $"{pt.ProductCode} — {pt.ProductName}");
                        table.Cell().Element(c => CellStyle(c, zebra, right: true)).Text(FormatAmount(pt.TotalQuantity));
                        table.Cell().Element(c => CellStyle(c, zebra, right: true)).Text(FormatAmount(pt.TotalAmount));

                        static IContainer CellStyle(IContainer c, bool zebra, bool right = false)
                        {
                            var styled = (zebra ? c.Background(ZebraRowBg) : c)
                                .PaddingVertical(5).PaddingHorizontal(6)
                                .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                .DefaultTextStyle(x => x.FontSize(8.5f));
                            return right ? styled.AlignRight() : styled;
                        }
                    }
                });
            }

            // d) Amount in words for the grand total — muted, italic caption style.
            var words = _lang == ReportLanguage.LT
                ? NumberToWordsHelper.ConvertToLithuanianWords(Math.Abs(_result.GrandTotalAmount))
                : NumberToWordsHelper.ConvertToEnglishWords(Math.Abs(_result.GrandTotalAmount));

            column.Item().Text(text =>
            {
                text.Span($"{_labels.AmountInWords} ").SemiBold().FontSize(8.5f);
                if (_result.GrandTotalAmount < 0)
                    text.Span("− ").Italic().FontSize(8.5f);
                text.Span(words).Italic().FontSize(8.5f).FontColor(MutedText);
            });
        });
    }

    private static string FormatAmount(decimal value) =>
        value.ToString("#,##0.00", CultureInfo.InvariantCulture);
}
