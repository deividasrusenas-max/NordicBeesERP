using NordicBeesERP.Data;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;

namespace NordicBeesERP.Services
{
    public interface IExpenseService
    {
        // Invoices
        Task<List<ExpenseInvoice>> GetInvoicesAsync(string? status = null, int? supplierId = null, DateTime? fromDate = null, DateTime? toDate = null, int? categoryId = null);
        Task<List<ExpenseInvoice>> SearchExpenseInvoicesAsync(string searchTerm, int limit = 20);
        Task<ExpenseInvoice?> GetInvoiceWithDetailsAsync(int id);
        Task<ExpenseInvoice?> GetInvoiceAsync(int id);
        Task<InvoiceAddResult> CreateInvoiceAsync(ExpenseInvoice invoice);
        Task<ExpenseInvoice> UpdateInvoiceAsync(ExpenseInvoice invoice, List<string>? overriddenFlags = null);
        // Budget actuals for a year (D-036): net, allocation → line → invoice category, quarantine excluded
        Task<NordicBeesERP.Services.Dtos.BudgetActualsResult> GetBudgetActualsAsync(int year);
        // Edit-form save: header + lines + flags + status + audit in one transaction (D-035)
        Task<ExpenseInvoice> SaveInvoiceEditAsync(ExpenseInvoice invoice, List<ExpenseInvoiceLine> lines, string performedBy);
        Task<bool> DeleteInvoiceAsync(int id);
        
        // Invoice Lines
        Task<List<ExpenseInvoiceLine>> GetInvoiceLinesAsync(int invoiceId);
        Task<ExpenseInvoiceLine> AddInvoiceLineAsync(ExpenseInvoiceLine line);
        Task<ExpenseInvoiceLine> UpdateInvoiceLineAsync(ExpenseInvoiceLine line);
        Task<bool> DeleteInvoiceLineAsync(int id);
        
        // Allocations
        Task<List<ExpenseLineAllocation>> GetAllocationsAsync(int invoiceLineId);
        Task<ExpenseLineAllocation> AddAllocationAsync(ExpenseLineAllocation allocation);
        Task<ExpenseLineAllocation> UpdateAllocationAsync(ExpenseLineAllocation allocation);
        Task<bool> DeleteAllocationAsync(int id);
        
        // Payments
        Task<List<ExpensePayment>> GetPaymentsAsync(int invoiceId);
        Task<ExpensePayment> AddPaymentAsync(ExpensePayment payment);
        Task<ExpensePayment> UpdatePaymentAsync(ExpensePayment payment);
        Task<bool> DeletePaymentAsync(int id);
        
        // Budgets
        Task<List<ExpenseBudget>> GetBudgetsAsync(int? categoryId = null, int? year = null);
        Task<ExpenseBudget> AddBudgetAsync(ExpenseBudget budget);
        Task<ExpenseBudget> UpdateBudgetAsync(ExpenseBudget budget);
        Task<bool> DeleteBudgetAsync(int id);
        
        // Categories
        Task<List<NordicBeesERP.Models.Expenses.ExpenseCategory>> GetCategoriesAsync(bool? isActive = null);
        Task<NordicBeesERP.Models.Expenses.ExpenseCategory> AddCategoryAsync(NordicBeesERP.Models.Expenses.ExpenseCategory category);
        Task<NordicBeesERP.Models.Expenses.ExpenseCategory> UpdateCategoryAsync(NordicBeesERP.Models.Expenses.ExpenseCategory category);
        Task<bool> ToggleCategoryActiveAsync(int id, bool isActive);
        Task<bool> DeleteCategoryAsync(int id);
        
        // Cost Centers
        Task<List<NordicBeesERP.Models.Expenses.ExpenseCostCenter>> GetCostCentersAsync(bool? isActive = null);
        Task<NordicBeesERP.Models.Expenses.ExpenseCostCenter> AddCostCenterAsync(NordicBeesERP.Models.Expenses.ExpenseCostCenter center);
        Task<NordicBeesERP.Models.Expenses.ExpenseCostCenter> UpdateCostCenterAsync(NordicBeesERP.Models.Expenses.ExpenseCostCenter center);
        Task<bool> DeleteCostCenterAsync(int id);
        
        // Calculations
        Task RecalculateInvoiceStatusAsync(int invoiceId);
        Task<decimal> CalculateInvoiceTotalAsync(int invoiceId);
        Task RecalculateInvoiceTotalsAsync(int invoiceId);
        
        // Analytics
        Task<List<ExpenseInvoice>> GetCashFlowAsync(DateTime from, DateTime to);
        Task<List<ExpenseInvoice>> GetSupplierHistoryAsync(int supplierId, int year);
        
        // OCR
        Task<ExpenseInvoice> CreateFromOcrAsync(OcrResultDto ocrResult, string source = "MANUAL");
        /// <summary>
        /// Re-OCR: replaces the invoice data and lines in one transaction. When the lines have
        /// allocations they are deleted with the lines, which is refused unless
        /// <paramref name="allocationRemovalConfirmed"/> (D-038 Q8); removed allocations are audited.
        /// </summary>
        Task<ExpenseInvoice> UpdateFromOcrAsync(int invoiceId, OcrResultDto ocrResult, bool allocationRemovalConfirmed = false);
        
        // Validation
        Task<int?> CheckDuplicateAsync(int? supplierId, string? supplierVatCode, string invoiceNumber, decimal amountInclVat, int excludeInvoiceId = 0);
        
        // Supplier Assignment
        Task AssignSupplierAsync(int invoiceId, int supplierId, string performedBy);
        
        // Auto-assign supplier to all PENDING_SUPPLIER invoices matching by vatCode OR supplierName
        Task<int> AutoAssignSupplierAsync(string? vatCode, string? supplierName, int supplierId);
        
        // Approval Workflow
        Task ApproveAsync(int invoiceId, string performedBy);
        Task RejectAsync(int invoiceId, string reason, string performedBy);

        // Duplicate resolution: "this is a different invoice" — removes DUPLICATE, status by the shared rules, audited
        Task ResolveDuplicateAsDifferentAsync(int invoiceId, string performedBy);

        // Wrong-recipient dismissal: removes WRONG_RECIPIENT, clears the „Sąskaita ne …" rejection, status by the shared rules, audited
        Task DismissWrongRecipientAsync(int invoiceId, string performedBy);
        Task RestoreInvoiceAsync(int invoiceId);
    }
}