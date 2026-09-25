using NordicBeesERP.Models.Expenses;

namespace NordicBeesERP.Services
{
    public interface IExpenseExportService
    {
        /// <summary>
        /// Export expense invoices to specified format
        /// </summary>
        /// <param name="invoices">List of invoices to export</param>
        /// <param name="format">Export format: "csv" or "xlsx"</param>
        /// <returns>Byte array of the exported file</returns>
        Task<byte[]> ExportInvoicesAsync(List<ExpenseInvoice> invoices, string format);

        /// <summary>
        /// Export expense payments to specified format
        /// </summary>
        /// <param name="payments">List of payments to export</param>
        /// <param name="format">Export format: "csv" or "xlsx"</param>
        /// <returns>Byte array of the exported file</returns>
        Task<byte[]> ExportPaymentsAsync(List<ExpensePayment> payments, string format);

        /// <summary>
        /// Export expense line allocations to specified format
        /// </summary>
        /// <param name="allocations">List of allocations to export</param>
        /// <param name="format">Export format: "csv" or "xlsx"</param>
        /// <returns>Byte array of the exported file</returns>
        Task<byte[]> ExportAllocationsAsync(List<ExpenseLineAllocation> allocations, string format);
    }
}
