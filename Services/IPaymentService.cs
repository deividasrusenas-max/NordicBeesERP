// =====================================================
// NORDIC BEES ERP - PAYMENT SERVICE INTERFACE
// Framework: .NET 10
// =====================================================

using NordicBeesERP.Models;
using NordicBeesERP.Services.Dtos;

namespace NordicBeesERP.Services
{
    public interface IPaymentService
    {
        Task<int> RegisterPaymentAsync(
            List<int> invoiceIds,
            decimal amount,
            DateTime paymentDate,
            string method,
            string? reference,
            string? notes,
            int userId);
        Task RecalculateInvoiceStatusAsync(int invoiceId);
        Task RecalculateInvoiceStatusAsync(List<int> invoiceIds);
        Task<List<InvoiceWithPaymentInfo>> GetUnpaidInvoicesAsync(
            int? customerId = null,
            string? status = null,
            DateTime? fromDate = null,
            DateTime? toDate = null);
        Task<List<CashFlowWeek>> GetCashFlowForecastAsync(int weeks = 8);
        Task<AgingReport> GetAgingReportAsync();
        Task<PaymentHistoryResult> GetPaymentHistoryAsync(
            int? customerId = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? paymentMethod = null,
            string? source = null,
            string? searchTerm = null,
            string? sortBy = null,
            string? sortDirection = null,
            int take = 50,
            int skip = 0);
        Task<PaymentWithDetails?> GetPaymentDetailAsync(int paymentId);
        Task<bool> DeletePaymentAsync(int paymentId, int userId);
        Task<bool> UpdatePaymentAsync(int paymentId, decimal amount, DateTime date, string method, string? reference, string? notes, int userId);
        Task<List<BankImportRow>> GetUnmatchedBankImportRowsAsync(int bankImportId);
        Task<BankImportRow> MatchBankImportRowAsync(int bankImportRowId, int invoiceId, int userId);
        Task<int> CreatePaymentFromBankImportAsync(int bankImportRowId, int userId);
        Task<List<BankImport>> GetBankImportsAsync(string? status = null, int take = 50, int skip = 0);
        Task<int> CreateBankImportAsync(string fileName, string fileHash, int totalRows, int userId);
        Task UpdateBankImportAsync(int importId, int totalRows);
        Task<BankImport?> GetBankImportWithRowsAsync(int bankImportId);
        Task<InvoiceWithPaymentInfoResult> GetSalesInvoicesAsync(
            int take = 50,
            int skip = 0,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? searchTerm = null,
            InvoiceStatus? status = null);
        Task<List<PaymentHistoryItem>> GetPaymentsByInvoiceAsync(int invoiceId);
        
        // Payments Dashboard KPI
        Task<PaymentsDashboardKpi> GetPaymentsDashboardKpiAsync();
    }
}
