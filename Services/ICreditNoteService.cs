// =====================================================
// NORDIC BEES ERP - CREDIT NOTE SERVICE INTERFACE
// Framework: .NET 10
// Status Schema: Draft / Printed / Disputed
// =====================================================

using NordicBeesERP.Models;
using NordicBeesERP.Services.Dtos;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NordicBeesERP.Services
{
    public interface ICreditNoteService
    {
        // =====================================================
        // LINE OPERATIONS
        // =====================================================

        Task<List<InvoiceLineDto>> GetInvoiceLinesAsync(int invoiceId);

        // =====================================================
        // INVOICE SELECTION FOR APPLYING
        // =====================================================

        Task<List<InvoiceSelectDto>> GetCustomerInvoicesForApplyingAsync(int customerId, int excludeInvoiceId);

        // =====================================================
        // INVOICE SEARCH FOR AUTOCOMPLETE (Credit Note Dialog)
        // =====================================================

        Task<List<InvoiceModel>> SearchInvoicesAsync(string searchTerm, int limit = 20, CancellationToken cancellationToken = default);

        // =====================================================
        // INVOICE SEARCH FOR AUTOCOMPLETE (Credit Note Page)
        // =====================================================

        Task<List<InvoiceSelectDto>> SearchInvoicesForCreditNoteAsync(string searchTerm, int customerId, int limit = 20, CancellationToken cancellationToken = default);

        // =====================================================
        // CREDIT NOTE CREATION
        // =====================================================

        Task<CreditNote> CreateCreditNoteAsync(CreateCreditNoteRequest request, int userId);

        // =====================================================
        // GET CREDIT NOTE NUMBER FOR DATE
        // =====================================================

        Task<string> GetNextCreditNoteNumberAsync(DateTime creditDate);

        // =====================================================
        // CREDIT NOTE LISTING (with pagination and filtering)
        // =====================================================

        Task<(List<CreditNoteListDto> Items, int TotalCount)> GetCreditNotesAsync(
            int currentPage, 
            int itemsPerPage, 
            string? filterCustomerName, 
            CreditNoteStatus? filterStatus, 
            DateTime? filterFromDate, 
            DateTime? filterToDate);

        // =====================================================
        // CREDIT NOTE RETRIEVAL
        // =====================================================

        Task<CreditNoteDetailDto> GetCreditNoteAsync(int id);

        // =====================================================
        // SET PRINTED STATUS - Draft → Printed
        // =====================================================

    Task SetPrintedAsync(int creditNoteId);

    // =====================================================
    // GENERATE PDF (used by API controller)
    // =====================================================

    Task<byte[]> GeneratePdfAsync(int id);

    // =====================================================
    // SET DISPUTED STATUS
    // =====================================================

        Task SetDisputedAsync(int creditNoteId, int userId);

        // =====================================================
        // CREDIT NOTE UPDATE (EDIT) - Draft only
        // =====================================================

        Task UpdateCreditNoteAsync(UpdateCreditNoteRequest request, int? userId);

        // =====================================================
        // CREDIT NOTE DELETE
        // =====================================================

        Task DeleteCreditNoteAsync(int id);
    }
}