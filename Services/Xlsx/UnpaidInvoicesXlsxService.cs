using ClosedXML.Excel;
using NordicBeesERP.Models;

namespace NordicBeesERP.Services.Xlsx
{
    /// <summary>
    /// Renders the statement of unpaid invoices as a live XLSX spreadsheet.
    /// No database access and no disk writes — pure in-memory generation from
    /// an already-computed <see cref="UnpaidInvoicesResult"/>. The totals row is
    /// formula-driven (SUM over the data rows), so the sheet stays a live
    /// spreadsheet rather than a frozen snapshot. All visible text comes from
    /// <see cref="UnpaidInvoicesLabels"/> — no hardcoded LT/EN strings here.
    /// </summary>
    public class UnpaidInvoicesXlsxService
    {
        private readonly ILogger<UnpaidInvoicesXlsxService> _logger;

        public UnpaidInvoicesXlsxService(ILogger<UnpaidInvoicesXlsxService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Generate the statement of unpaid invoices as XLSX bytes.
        /// </summary>
        public byte[] GenerateXlsx(UnpaidInvoicesResult result, ReportLanguage lang)
        {
            var labels = UnpaidInvoicesLabels.For(lang);

            using var workbook = new XLWorkbook();
            // The report title is too long for a worksheet name (Excel caps at 31 chars),
            // so use a fixed short sheet name; the full title goes in row 1 instead.
            const string SheetName = "Neapmoketos";
            var worksheet = workbook.AddWorksheet(SheetName);

            const string AmountFormat = "#,##0.00";
            const string DateFormat = "yyyy-MM-dd";

            // Column layout: A=InvoiceNo, B=Date, C=DueDate, D=Amount, E=BalanceDue
            const int ColInvoiceNo = 1;
            const int ColDate = 2;
            const int ColDueDate = 3;
            const int ColAmount = 4;
            const int ColBalanceDue = 5;

            // Row 1: title
            worksheet.Cell(1, 1).Value = labels.Title;
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Cell(1, 1).Style.Font.FontSize = 14;
            worksheet.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#4f7cac");

            // Partner block + period line (start–end)
            worksheet.Cell(3, 1).Value = result.PartnerName;
            worksheet.Cell(3, 1).Style.Font.Bold = true;
            worksheet.Cell(4, 1).Value = result.PartnerCode;
            worksheet.Cell(5, 1).Value = result.PartnerAddress;
            worksheet.Cell(6, 1).Value = $"{result.PeriodStart:yyyy-MM-dd} – {result.PeriodEnd:yyyy-MM-dd}";
            worksheet.Cell(6, 1).Style.Font.Bold = true;
            worksheet.Range(3, 1, 6, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#f8fafc");

            // Table header
            var headerRow = 8;
            worksheet.Cell(headerRow, ColInvoiceNo).Value = labels.ColInvoiceNo;
            worksheet.Cell(headerRow, ColDate).Value = labels.ColDate;
            worksheet.Cell(headerRow, ColDueDate).Value = labels.ColDueDate;
            worksheet.Cell(headerRow, ColAmount).Value = labels.ColAmount;
            worksheet.Cell(headerRow, ColBalanceDue).Value = labels.ColBalanceDue;

            var headerRange = worksheet.Range(headerRow, ColInvoiceNo, headerRow, ColBalanceDue);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#eef2f7");
            headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
            headerRange.Style.Border.BottomBorderColor = XLColor.FromHtml("#4f7cac");
            worksheet.Cell(headerRow, ColAmount).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Cell(headerRow, ColBalanceDue).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.SheetView.FreezeRows(headerRow);

            // Data rows
            var firstDataRow = headerRow + 1;
            var currentRow = firstDataRow;

            foreach (var line in result.Lines)
            {
                worksheet.Cell(currentRow, ColInvoiceNo).Value = line.InvoiceNumber;

                var dateCell = worksheet.Cell(currentRow, ColDate);
                dateCell.Value = line.InvoiceDate;
                dateCell.Style.NumberFormat.Format = DateFormat;

                if (line.DueDate.HasValue)
                {
                    var dueCell = worksheet.Cell(currentRow, ColDueDate);
                    dueCell.Value = line.DueDate.Value;
                    dueCell.Style.NumberFormat.Format = DateFormat;
                }

                var amountCell = worksheet.Cell(currentRow, ColAmount);
                amountCell.Value = line.TotalAmount;
                amountCell.Style.NumberFormat.Format = AmountFormat;
                amountCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                var balanceCell = worksheet.Cell(currentRow, ColBalanceDue);
                balanceCell.Value = line.RemainingAmount;
                balanceCell.Style.NumberFormat.Format = AmountFormat;
                balanceCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                currentRow++;
            }

            var lastDataRow = currentRow - 1;

            for (int r = firstDataRow; r <= lastDataRow; r++)
            {
                if ((r - firstDataRow) % 2 == 1)
                    worksheet.Range(r, ColInvoiceNo, r, ColBalanceDue).Style.Fill.BackgroundColor = XLColor.FromHtml("#f8fafc");
            }

            // Total row: real SUM formulas over the data-row ranges so the sheet recalculates.
            var totalRow = currentRow;
            worksheet.Cell(totalRow, ColInvoiceNo).Value = labels.TotalLabel;
            worksheet.Cell(totalRow, ColInvoiceNo).Style.Font.Bold = true;
            if (result.Lines.Count > 0)
            {
                // Derive the column letters from actual cells so the SUM ranges stay
                // correct even if the column constants above ever change.
                var amountColLetter = worksheet.Cell(firstDataRow, ColAmount).Address.ColumnLetter;
                var balanceColLetter = worksheet.Cell(firstDataRow, ColBalanceDue).Address.ColumnLetter;
                worksheet.Cell(totalRow, ColAmount).FormulaA1 = $"SUM({amountColLetter}{firstDataRow}:{amountColLetter}{lastDataRow})";
                worksheet.Cell(totalRow, ColBalanceDue).FormulaA1 = $"SUM({balanceColLetter}{firstDataRow}:{balanceColLetter}{lastDataRow})";
            }
            else
            {
                worksheet.Cell(totalRow, ColAmount).Value = 0m;
                worksheet.Cell(totalRow, ColBalanceDue).Value = 0m;
            }
            worksheet.Cell(totalRow, ColAmount).Style.NumberFormat.Format = AmountFormat;
            worksheet.Cell(totalRow, ColAmount).Style.Font.Bold = true;
            worksheet.Cell(totalRow, ColAmount).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            worksheet.Cell(totalRow, ColBalanceDue).Style.NumberFormat.Format = AmountFormat;
            worksheet.Cell(totalRow, ColBalanceDue).Style.Font.Bold = true;
            worksheet.Cell(totalRow, ColBalanceDue).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            var totalRange = worksheet.Range(totalRow, ColInvoiceNo, totalRow, ColBalanceDue);
            totalRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#eef2f7");
            totalRange.Style.Border.TopBorder = XLBorderStyleValues.Medium;
            totalRange.Style.Border.TopBorderColor = XLColor.FromHtml("#4f7cac");

            worksheet.Columns().AdjustToContents();

            _logger.LogDebug("Generated unpaid invoices XLSX for partner {PartnerCode}: {LineCount} lines",
                result.PartnerCode, result.Lines.Count);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }
}
