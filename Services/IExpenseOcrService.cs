using NordicBeesERP.Services.Dtos;

namespace NordicBeesERP.Services;

public interface IExpenseOcrService {
    // Primary method used by ExpenseUploadDialog
    Task<OcrResultDto> ProcessAsync(string base64, string fileName);

    // Alias for OcrQueueWorker and ExpenseUploadDialog compatibility
    Task<OcrResultDto> ExtractInvoiceDataAsync(string base64, string fileName);

    Task<(int? supplierId, int? defaultCategoryId)> FindSupplierIdAsync(string supplierName, string vatCode);
    Task<bool> IsAzureHealthyAsync();
}