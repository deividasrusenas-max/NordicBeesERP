using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models.Expenses;

namespace NordicBeesERP.Services
{
    public class ExpenseExportService : IExpenseExportService
    {
        private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;

        public ExpenseExportService(IDbContextFactory<NordicBeesERPContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        /// <summary>
        /// Export expense invoices to specified format
        /// </summary>
        public async Task<byte[]> ExportInvoicesAsync(List<ExpenseInvoice> invoices, string format)
        {
            if (string.IsNullOrEmpty(format)) format = "xlsx";
            
            format = format.ToLowerInvariant();
            
            if (format == "csv")
                return await ExportInvoicesToCsvAsync(invoices);
            else
                return await ExportInvoicesToExcelAsync(invoices);
        }

        /// <summary>
        /// Export expense invoices filtered by criteria to specified format
        /// </summary>
        public async Task<byte[]> ExportInvoicesByFilterAsync(string? status, DateTime? fromDate, DateTime? toDate, int? supplierId, int? categoryId, string format)
        {
            using var context = _dbFactory.CreateDbContext();
            var query = context.ExpenseInvoices.AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(i => i.Status == status);

            if (supplierId.HasValue)
                query = query.Where(i => i.SupplierId == supplierId.Value);

            if (fromDate.HasValue)
                query = query.Where(i => i.InvoiceDate >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(i => i.InvoiceDate <= toDate.Value);

            if (categoryId.HasValue)
            {
                query = query.Where(i => i.ExpenseInvoiceLines.Any(l => l.ExpenseLineAllocations.Any(a => a.CategoryId == categoryId.Value)));
            }

            var invoices = await query
                .Include(i => i.Supplier)
                .Include(i => i.ExpenseInvoiceLines)
                    .ThenInclude(l => l.ExpenseLineAllocations)
                        .ThenInclude(a => a.Category)
                .Include(i => i.ExpensePayments)
                .OrderByDescending(i => i.InvoiceDate)
                .ToListAsync();

            return await ExportInvoicesAsync(invoices, format);
        }

        /// <summary>
        /// Export expense payments to specified format
        /// </summary>
        public async Task<byte[]> ExportPaymentsAsync(List<ExpensePayment> payments, string format)
        {
            if (string.IsNullOrEmpty(format)) format = "xlsx";
            
            format = format.ToLowerInvariant();
            
            if (format == "csv")
                return await ExportPaymentsToCsvAsync(payments);
            else
                return await ExportPaymentsToExcelAsync(payments);
        }

        /// <summary>
        /// Export expense line allocations to specified format
        /// </summary>
        public async Task<byte[]> ExportAllocationsAsync(List<ExpenseLineAllocation> allocations, string format)
        {
            if (string.IsNullOrEmpty(format)) format = "xlsx";
            
            format = format.ToLowerInvariant();
            
            if (format == "csv")
                return await ExportAllocationsToCsvAsync(allocations);
            else
                return await ExportAllocationsToExcelAsync(allocations);
        }

        // =====================================================
        // INVOICE EXPORT METHODS
        // =====================================================

        private async Task<byte[]> ExportInvoicesToExcelAsync(List<ExpenseInvoice> invoices)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.AddWorksheet("Invoices");

            // Headers
            worksheet.Cell(1, 1).Value = "ID";
            worksheet.Cell(1, 2).Value = "Invoice Number";
            worksheet.Cell(1, 3).Value = "Invoice Date";
            worksheet.Cell(1, 4).Value = "Due Date";
            worksheet.Cell(1, 5).Value = "Supplier";
            worksheet.Cell(1, 6).Value = "Amount Excl VAT";
            worksheet.Cell(1, 7).Value = "VAT Rate";
            worksheet.Cell(1, 8).Value = "VAT Amount";
            worksheet.Cell(1, 9).Value = "Amount Incl VAT";
            worksheet.Cell(1, 10).Value = "Paid Amount";
            worksheet.Cell(1, 11).Value = "Status";
            worksheet.Cell(1, 12).Value = "OCR Status";
            worksheet.Cell(1, 13).Value = "Notes";

            // Style header
            var headerRange = worksheet.Range(1, 1, 1, 13);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

            int row = 2;
            foreach (var invoice in invoices)
            {
                worksheet.Cell(row, 1).Value = invoice.Id;
                worksheet.Cell(row, 2).Value = invoice.InvoiceNumber;
                worksheet.Cell(row, 3).Value = invoice.InvoiceDate;
                worksheet.Cell(row, 4).Value = invoice.DueDate;
                worksheet.Cell(row, 5).Value = invoice.Supplier?.Name ?? "Unknown";
                worksheet.Cell(row, 6).Value = invoice.AmountExclVat;
                worksheet.Cell(row, 7).Value = invoice.VatRate;
                worksheet.Cell(row, 8).Value = invoice.VatAmount;
                worksheet.Cell(row, 9).Value = invoice.AmountInclVat;
                worksheet.Cell(row, 10).Value = invoice.PaidAmount;
                worksheet.Cell(row, 11).Value = invoice.Status;
                worksheet.Cell(row, 12).Value = invoice.OcrStatus;
                worksheet.Cell(row, 13).Value = invoice.Notes;

                row++;
            }

            // Auto-fit columns
            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private async Task<byte[]> ExportInvoicesToCsvAsync(List<ExpenseInvoice> invoices)
        {
            using var writer = new System.IO.StringWriter();
            
            // Headers
            writer.WriteLine("ID,Invoice Number,Invoice Date,Due Date,Supplier,Amount Excl VAT,VAT Rate,VAT Amount,Amount Incl VAT,Paid Amount,Status,OCR Status,Notes");

            foreach (var invoice in invoices)
            {
                var supplierName = invoice.Supplier?.Name ?? "Unknown";
                var notes = invoice.Notes?.Replace("\"", "\"\"") ?? "";
                
                writer.WriteLine($"{invoice.Id},\"{invoice.InvoiceNumber}\",{invoice.InvoiceDate:yyyy-MM-dd},{invoice.DueDate:yyyy-MM-dd},\"{supplierName}\",{invoice.AmountExclVat:F2},{invoice.VatRate:F2},{invoice.VatAmount:F2},{invoice.AmountInclVat:F2},{invoice.PaidAmount:F2},{invoice.Status},{invoice.OcrStatus},\"{notes}\"");
            }

            return System.Text.Encoding.UTF8.GetBytes(writer.ToString());
        }

        // =====================================================
        // PAYMENT EXPORT METHODS
        // =====================================================

        private async Task<byte[]> ExportPaymentsToExcelAsync(List<ExpensePayment> payments)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.AddWorksheet("Payments");

            // Headers
            worksheet.Cell(1, 1).Value = "ID";
            worksheet.Cell(1, 2).Value = "Invoice Number";
            worksheet.Cell(1, 3).Value = "Payment Date";
            worksheet.Cell(1, 4).Value = "Amount";
            worksheet.Cell(1, 5).Value = "Payment Method";
            worksheet.Cell(1, 6).Value = "Reference";
            worksheet.Cell(1, 7).Value = "Notes";

            // Style header
            var headerRange = worksheet.Range(1, 1, 1, 7);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

            int row = 2;
            foreach (var payment in payments)
            {
                worksheet.Cell(row, 1).Value = payment.Id;
                worksheet.Cell(row, 2).Value = payment.Invoice?.InvoiceNumber ?? "Unknown";
                worksheet.Cell(row, 3).Value = payment.PaymentDate;
                worksheet.Cell(row, 4).Value = payment.Amount;
                worksheet.Cell(row, 5).Value = payment.PaymentMethod;
                worksheet.Cell(row, 6).Value = payment.Reference ?? "";
                worksheet.Cell(row, 7).Value = payment.Notes ?? "";

                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private async Task<byte[]> ExportPaymentsToCsvAsync(List<ExpensePayment> payments)
        {
            using var writer = new System.IO.StringWriter();
            
            writer.WriteLine("ID,Invoice Number,Payment Date,Amount,Payment Method,Reference,Notes");

            foreach (var payment in payments)
            {
                var reference = payment.Reference?.Replace("\"", "\"\"") ?? "";
                var notes = payment.Notes?.Replace("\"", "\"\"") ?? "";
                
                writer.WriteLine($"{payment.Id},\"{payment.Invoice?.InvoiceNumber ?? "Unknown"}\",{payment.PaymentDate:yyyy-MM-dd},{payment.Amount:F2},{payment.PaymentMethod},\"{reference}\",\"{notes}\"");
            }

            return System.Text.Encoding.UTF8.GetBytes(writer.ToString());
        }

        // =====================================================
        // ALLOCATION EXPORT METHODS
        // =====================================================

        private async Task<byte[]> ExportAllocationsToExcelAsync(List<ExpenseLineAllocation> allocations)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.AddWorksheet("Allocations");

            // Headers
            worksheet.Cell(1, 1).Value = "ID";
            worksheet.Cell(1, 2).Value = "Invoice Number";
            worksheet.Cell(1, 3).Value = "Line Description";
            worksheet.Cell(1, 4).Value = "Category";
            worksheet.Cell(1, 5).Value = "Cost Center";
            worksheet.Cell(1, 6).Value = "Allocated Amount";
            worksheet.Cell(1, 7).Value = "Allocated Percent";

            // Style header
            var headerRange = worksheet.Range(1, 1, 1, 7);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

            int row = 2;
            foreach (var allocation in allocations)
            {
                worksheet.Cell(row, 1).Value = allocation.Id;
                worksheet.Cell(row, 2).Value = allocation.InvoiceLine?.Invoice?.InvoiceNumber ?? "Unknown";
                worksheet.Cell(row, 3).Value = allocation.InvoiceLine?.Description ?? "";
                worksheet.Cell(row, 4).Value = allocation.Category?.Name ?? "Unknown";
                worksheet.Cell(row, 5).Value = allocation.CostCenter?.Name ?? "Unknown";
                worksheet.Cell(row, 6).Value = allocation.AllocatedAmount;
                worksheet.Cell(row, 7).Value = allocation.AllocatedPercent;

                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private async Task<byte[]> ExportAllocationsToCsvAsync(List<ExpenseLineAllocation> allocations)
        {
            using var writer = new System.IO.StringWriter();
            
            writer.WriteLine("ID,Invoice Number,Line Description,Category,Cost Center,Allocated Amount,Allocated Percent");

            foreach (var allocation in allocations)
            {
                writer.WriteLine($"{allocation.Id},\"{allocation.InvoiceLine?.Invoice?.InvoiceNumber ?? "Unknown"}\",\"{allocation.InvoiceLine?.Description?.Replace("\"", "\"\"") ?? ""}\",\"{allocation.Category?.Name ?? ""}\",\"{allocation.CostCenter?.Name ?? ""}\",{allocation.AllocatedAmount:F2},{allocation.AllocatedPercent:F2}");
            }

            return System.Text.Encoding.UTF8.GetBytes(writer.ToString());
        }
    }
}
