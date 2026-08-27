using ClosedXML.Excel;
using NordicBeesERP.Models;

namespace NordicBeesERP.Services.Xlsx
{
    /// <summary>
    /// Renders the "Prekių pardavimo suvestinė" (Sales by Customer) report as a live XLSX
    /// spreadsheet. No database access and no disk writes — pure in-memory generation from
    /// an already-computed <see cref="SalesByCustomerReportResult"/>. All visible text comes
    /// from <see cref="SalesByCustomerReportLabels"/> — no hardcoded LT/EN strings here.
    /// </summary>
    public class SalesByCustomerXlsxService
    {
        private readonly ILogger<SalesByCustomerXlsxService> _logger;

        public SalesByCustomerXlsxService(ILogger<SalesByCustomerXlsxService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Generate the sales-by-customer summary as XLSX bytes.
        /// </summary>
        public byte[] GenerateXlsx(SalesByCustomerReportResult result, ReportLanguage lang)
        {
            var labels = SalesByCustomerReportLabels.For(lang);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.AddWorksheet(labels.SheetName);

            const string AmountFormat = "#,##0.00";
            const string DateFormat = "yyyy-MM-dd";

            // Column layout: A=Customer, B=Product, C=InvoiceNo, D=Date, E=Quantity, F=UnitPrice, G=Amount
            const int ColCustomer = 1;
            const int ColProduct = 2;
            const int ColInvoiceNo = 3;
            const int ColDate = 4;
            const int ColQuantity = 5;
            const int ColUnitPrice = 6;
            const int ColAmount = 7;

            // Row 1: title
            worksheet.Cell(1, 1).Value = labels.Title;
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Cell(1, 1).Style.Font.FontSize = 14;
            worksheet.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#4f7cac");

            // Row 2: customer filter line (row 3 left blank)
            worksheet.Cell(2, ColCustomer).Value = $"{labels.Customer}: {result.CustomerFilter}";
            worksheet.Cell(2, ColCustomer).Style.Font.Bold = true;

            // Table header at row 4
            const int HeaderRow = 4;
            worksheet.Cell(HeaderRow, ColCustomer).Value = labels.Customer;
            worksheet.Cell(HeaderRow, ColProduct).Value = labels.Product;
            worksheet.Cell(HeaderRow, ColInvoiceNo).Value = labels.InvoiceNo;
            worksheet.Cell(HeaderRow, ColDate).Value = labels.Date;
            worksheet.Cell(HeaderRow, ColQuantity).Value = labels.Quantity;
            worksheet.Cell(HeaderRow, ColUnitPrice).Value = labels.UnitPrice;
            worksheet.Cell(HeaderRow, ColAmount).Value = labels.Amount;

            var headerRange = worksheet.Range(HeaderRow, ColCustomer, HeaderRow, ColAmount);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#eef2f7");
            headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
            headerRange.Style.Border.BottomBorderColor = XLColor.FromHtml("#4f7cac");
            worksheet.Cell(HeaderRow, ColQuantity).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            worksheet.Cell(HeaderRow, ColUnitPrice).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            worksheet.Cell(HeaderRow, ColAmount).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            worksheet.SheetView.FreezeRows(HeaderRow);

            // Data rows — detail rows are zebra-striped (subtotal/total rows excluded from the count)
            var currentRow = HeaderRow + 1;
            var detailIndex = 0;

            foreach (var customer in result.Customers)
            {
                foreach (var product in customer.Products)
                {
                    foreach (var row in product.Rows)
                    {
                        worksheet.Cell(currentRow, ColCustomer).Value = customer.CustomerName;
                        worksheet.Cell(currentRow, ColProduct).Value =
                            product.ProductCode == "NENUSTATYTA"
                                ? labels.NoProduct
                                : $"{product.ProductCode} — {product.ProductName}";
                        worksheet.Cell(currentRow, ColInvoiceNo).Value = row.DocumentNumber;

                        var dateCell = worksheet.Cell(currentRow, ColDate);
                        dateCell.Value = row.DocumentDate;
                        dateCell.Style.NumberFormat.Format = DateFormat;

                        var quantityCell = worksheet.Cell(currentRow, ColQuantity);
                        quantityCell.Value = row.Quantity;
                        quantityCell.Style.NumberFormat.Format = AmountFormat;
                        quantityCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                        var unitPriceCell = worksheet.Cell(currentRow, ColUnitPrice);
                        unitPriceCell.Value = row.UnitPrice;
                        unitPriceCell.Style.NumberFormat.Format = AmountFormat;
                        unitPriceCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                        var amountCell = worksheet.Cell(currentRow, ColAmount);
                        amountCell.Value = row.LineTotal;
                        amountCell.Style.NumberFormat.Format = AmountFormat;
                        amountCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                        if (row.IsCredit)
                            worksheet.Range(currentRow, ColCustomer, currentRow, ColAmount).Style.Font.FontColor = XLColor.FromHtml("#c0392b");

                        if (detailIndex % 2 == 1)
                            worksheet.Range(currentRow, ColCustomer, currentRow, ColAmount).Style.Fill.BackgroundColor = XLColor.FromHtml("#f8fafc");

                        detailIndex++;
                        currentRow++;
                    }

                    // Product subtotal row
                    worksheet.Cell(currentRow, ColCustomer).Value = labels.ProductSubtotal;
                    worksheet.Cell(currentRow, ColCustomer).Style.Font.Bold = true;
                    var productQtyCell = worksheet.Cell(currentRow, ColQuantity);
                    productQtyCell.Value = product.TotalQuantity;
                    productQtyCell.Style.NumberFormat.Format = AmountFormat;
                    productQtyCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    var productAmountCell = worksheet.Cell(currentRow, ColAmount);
                    productAmountCell.Value = product.TotalAmount;
                    productAmountCell.Style.NumberFormat.Format = AmountFormat;
                    productAmountCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    var productSubtotalRange = worksheet.Range(currentRow, ColCustomer, currentRow, ColAmount);
                    productSubtotalRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#eef2f7");
                    productSubtotalRange.Style.Border.TopBorder = XLBorderStyleValues.Medium;
                    productSubtotalRange.Style.Border.TopBorderColor = XLColor.FromHtml("#4f7cac");
                    currentRow++;
                }

                // Customer total row
                worksheet.Cell(currentRow, ColCustomer).Value = labels.CustomerTotal;
                worksheet.Cell(currentRow, ColCustomer).Style.Font.Bold = true;
                var customerQtyCell = worksheet.Cell(currentRow, ColQuantity);
                customerQtyCell.Value = customer.TotalQuantity;
                customerQtyCell.Style.NumberFormat.Format = AmountFormat;
                customerQtyCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                var customerAmountCell = worksheet.Cell(currentRow, ColAmount);
                customerAmountCell.Value = customer.TotalAmount;
                customerAmountCell.Style.NumberFormat.Format = AmountFormat;
                customerAmountCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                var customerTotalRange = worksheet.Range(currentRow, ColCustomer, currentRow, ColAmount);
                customerTotalRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#eef2f7");
                customerTotalRange.Style.Border.TopBorder = XLBorderStyleValues.Medium;
                customerTotalRange.Style.Border.TopBorderColor = XLColor.FromHtml("#4f7cac");
                currentRow++;
            }

            // Grand total row
            worksheet.Cell(currentRow, ColCustomer).Value = labels.GrandTotal;
            worksheet.Cell(currentRow, ColCustomer).Style.Font.Bold = true;
            var grandQtyCell = worksheet.Cell(currentRow, ColQuantity);
            grandQtyCell.Value = result.GrandTotalQuantity;
            grandQtyCell.Style.NumberFormat.Format = AmountFormat;
            grandQtyCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            var grandAmountCell = worksheet.Cell(currentRow, ColAmount);
            grandAmountCell.Value = result.GrandTotalAmount;
            grandAmountCell.Style.NumberFormat.Format = AmountFormat;
            grandAmountCell.Style.Font.Bold = true;
            grandAmountCell.Style.Font.FontColor = XLColor.FromHtml("#4f7cac");
            grandAmountCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            var grandTotalRange = worksheet.Range(currentRow, ColCustomer, currentRow, ColAmount);
            grandTotalRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#eef2f7");
            grandTotalRange.Style.Border.TopBorder = XLBorderStyleValues.Medium;
            grandTotalRange.Style.Border.TopBorderColor = XLColor.FromHtml("#4f7cac");
            currentRow++;

            // Product totals mini-table (all customers)
            if (result.ProductTotals.Count > 0)
            {
                currentRow++; // blank row

                worksheet.Cell(currentRow, ColCustomer).Value = labels.ProductTotalsSection;
                worksheet.Cell(currentRow, ColCustomer).Style.Font.Bold = true;
                worksheet.Cell(currentRow, ColCustomer).Style.Font.FontSize = 11;
                worksheet.Cell(currentRow, ColCustomer).Style.Font.FontColor = XLColor.FromHtml("#4f7cac");
                currentRow++;

                var ptHeaderRow = currentRow;
                worksheet.Cell(ptHeaderRow, ColProduct).Value = labels.Product;
                worksheet.Cell(ptHeaderRow, ColQuantity).Value = labels.Quantity;
                worksheet.Cell(ptHeaderRow, ColAmount).Value = labels.Amount;
                var ptHeaderRange = worksheet.Range(ptHeaderRow, ColProduct, ptHeaderRow, ColAmount);
                ptHeaderRange.Style.Font.Bold = true;
                ptHeaderRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#eef2f7");
                ptHeaderRange.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
                ptHeaderRange.Style.Border.BottomBorderColor = XLColor.FromHtml("#4f7cac");
                worksheet.Cell(ptHeaderRow, ColQuantity).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                worksheet.Cell(ptHeaderRow, ColAmount).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                currentRow++;

                foreach (var pt in result.ProductTotals)
                {
                    worksheet.Cell(currentRow, ColProduct).Value =
                        pt.ProductCode == "NENUSTATYTA"
                            ? labels.NoProduct
                            : $"{pt.ProductCode} — {pt.ProductName}";
                    var ptQtyCell = worksheet.Cell(currentRow, ColQuantity);
                    ptQtyCell.Value = pt.TotalQuantity;
                    ptQtyCell.Style.NumberFormat.Format = AmountFormat;
                    ptQtyCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    var ptAmountCell = worksheet.Cell(currentRow, ColAmount);
                    ptAmountCell.Value = pt.TotalAmount;
                    ptAmountCell.Style.NumberFormat.Format = AmountFormat;
                    ptAmountCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    currentRow++;
                }
            }

            worksheet.Columns().AdjustToContents();

            _logger.LogDebug("Generated sales-by-customer XLSX for filter {CustomerFilter}: {CustomerCount} customers",
                result.CustomerFilter, result.Customers.Count);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }
}
