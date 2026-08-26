using ClosedXML.Excel;
using NordicBeesERP.Models;

namespace NordicBeesERP.Services.Xlsx
{
    /// <summary>
    /// Renders a debt reconciliation statement as a live XLSX spreadsheet.
    /// No database access and no disk writes — pure in-memory generation from
    /// an already-computed <see cref="DebtReconciliationResult"/>. The Balance
    /// column is formula-driven (running balance), so the sheet stays a live
    /// spreadsheet rather than a frozen snapshot. All visible text comes from
    /// <see cref="DebtReconciliationLabels"/> — no hardcoded LT/EN strings here.
    /// </summary>
    public class DebtReconciliationXlsxService
    {
        private readonly ILogger<DebtReconciliationXlsxService> _logger;

        public DebtReconciliationXlsxService(ILogger<DebtReconciliationXlsxService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Generate the reconciliation statement as XLSX bytes.
        /// </summary>
        public byte[] GenerateXlsx(DebtReconciliationResult result, ReportLanguage lang)
        {
            var labels = DebtReconciliationLabels.For(lang);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.AddWorksheet(labels.SheetName);

            const string AmountFormat = "#,##0.00";
            const string DateFormat = "yyyy-MM-dd";

            // Column layout: A=DocNo, B=DocDate, C=DueDate, D=Debit, E=Credit, F=Balance
            const int ColDocNo = 1;
            const int ColDocDate = 2;
            const int ColDueDate = 3;
            const int ColDebit = 4;
            const int ColCredit = 5;
            const int ColBalance = 6;

            // Row 1: title
            worksheet.Cell(1, 1).Value = labels.Title;
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Cell(1, 1).Style.Font.FontSize = 14;
            worksheet.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#4f7cac");

            // Partner block
            worksheet.Cell(3, 1).Value = labels.PartnerLabel;
            worksheet.Cell(3, 1).Style.Font.Bold = true;
            worksheet.Cell(3, 2).Value = result.PartnerName;
            worksheet.Cell(4, 1).Value = labels.CompanyCodeLabel;
            worksheet.Cell(4, 1).Style.Font.Bold = true;
            worksheet.Cell(4, 2).Value = result.PartnerCode;
            worksheet.Cell(5, 1).Value = labels.AddressLabel;
            worksheet.Cell(5, 1).Style.Font.Bold = true;
            worksheet.Cell(5, 2).Value = result.PartnerAddress;

            // Period line — whole year when the period ends on Dec 31, otherwise the specific end month
            int? endMonth = (result.PeriodEnd.Month == 12 && result.PeriodEnd.Day == 31) ? null : result.PeriodEnd.Month;
            worksheet.Cell(6, 1).Value = DebtReconciliationLabels.FormatPeriod(lang, result.PeriodStart.Year, endMonth);
            worksheet.Cell(6, 1).Style.Font.Bold = true;
            worksheet.Range(3, 1, 6, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#f8fafc");

            // Opening balance row (numeric cell — the formula anchor for the running balance)
            var openingRow = 8;
            worksheet.Cell(openingRow, ColDocNo).Value = labels.OpeningBalance;
            worksheet.Cell(openingRow, ColDebit).Value = result.OpeningBalance;
            worksheet.Cell(openingRow, ColDebit).Style.NumberFormat.Format = AmountFormat;
            var openingCellAddress = worksheet.Cell(openingRow, ColDebit).Address.ToString();

            // Table header
            var headerRow = 10;
            worksheet.Cell(headerRow, ColDocNo).Value = labels.DocNo;
            worksheet.Cell(headerRow, ColDocDate).Value = labels.DocDate;
            worksheet.Cell(headerRow, ColDueDate).Value = labels.DueDate;
            worksheet.Cell(headerRow, ColDebit).Value = labels.Debit;
            worksheet.Cell(headerRow, ColCredit).Value = labels.Credit;
            worksheet.Cell(headerRow, ColBalance).Value = labels.Balance;

            var headerRange = worksheet.Range(headerRow, ColDocNo, headerRow, ColBalance);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            worksheet.SheetView.FreezeRows(headerRow);

            // Data rows — Balance is a real formula: opening + debit - credit (first row),
            // previous balance + debit - credit (subsequent rows).
            var firstDataRow = headerRow + 1;
            var lastBalanceCellAddress = openingCellAddress;
            var currentRow = firstDataRow;

            foreach (var line in result.Lines)
            {
                worksheet.Cell(currentRow, ColDocNo).Value = line.DocumentNumber;
                worksheet.Cell(currentRow, ColDocDate).Value = line.DocumentDate;
                worksheet.Cell(currentRow, ColDocDate).Style.NumberFormat.Format = DateFormat;

                if (line.DueDate.HasValue)
                {
                    worksheet.Cell(currentRow, ColDueDate).Value = line.DueDate.Value;
                    worksheet.Cell(currentRow, ColDueDate).Style.NumberFormat.Format = DateFormat;
                }

                worksheet.Cell(currentRow, ColDebit).Value = line.Debit;
                worksheet.Cell(currentRow, ColDebit).Style.NumberFormat.Format = AmountFormat;
                worksheet.Cell(currentRow, ColCredit).Value = line.Credit;
                worksheet.Cell(currentRow, ColCredit).Style.NumberFormat.Format = AmountFormat;

                var debitCellAddress = worksheet.Cell(currentRow, ColDebit).Address.ToString();
                var creditCellAddress = worksheet.Cell(currentRow, ColCredit).Address.ToString();
                var balanceCell = worksheet.Cell(currentRow, ColBalance);
                balanceCell.FormulaA1 = $"{lastBalanceCellAddress}+{debitCellAddress}-{creditCellAddress}";
                balanceCell.Style.NumberFormat.Format = AmountFormat;

                lastBalanceCellAddress = balanceCell.Address.ToString();
                currentRow++;
            }

            var lastDataRow = currentRow - 1;

            // Total row: SUM formulas over the debit/credit ranges, closing balance as a formula.
            var totalRow = currentRow;
            worksheet.Cell(totalRow, ColDocNo).Value = labels.Total;
            if (result.Lines.Count > 0)
            {
                // Derive the column letters from actual cells so the SUM ranges stay
                // correct even if the column constants above ever change.
                var debitColLetter = worksheet.Cell(firstDataRow, ColDebit).Address.ColumnLetter;
                var creditColLetter = worksheet.Cell(firstDataRow, ColCredit).Address.ColumnLetter;
                worksheet.Cell(totalRow, ColDebit).FormulaA1 = $"SUM({debitColLetter}{firstDataRow}:{debitColLetter}{lastDataRow})";
                worksheet.Cell(totalRow, ColCredit).FormulaA1 = $"SUM({creditColLetter}{firstDataRow}:{creditColLetter}{lastDataRow})";
            }
            else
            {
                // No lines: closing balance is just the opening balance.
                worksheet.Cell(totalRow, ColDebit).Value = 0m;
                worksheet.Cell(totalRow, ColCredit).Value = 0m;
            }
            worksheet.Cell(totalRow, ColDebit).Style.NumberFormat.Format = AmountFormat;
            worksheet.Cell(totalRow, ColCredit).Style.NumberFormat.Format = AmountFormat;

            var closingRow = totalRow + 1;
            worksheet.Cell(closingRow, ColDocNo).Value = labels.ClosingBalance;
            var closingCell = worksheet.Cell(closingRow, ColDebit);
            if (result.Lines.Count > 0)
                closingCell.FormulaA1 = $"={lastBalanceCellAddress}";
            else
                closingCell.Value = result.OpeningBalance;
            closingCell.Style.NumberFormat.Format = AmountFormat;

            worksheet.Columns().AdjustToContents();

            _logger.LogDebug("Generated debt reconciliation XLSX for partner {PartnerCode}: {LineCount} lines",
                result.PartnerCode, result.Lines.Count);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }
}
