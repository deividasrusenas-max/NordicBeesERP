using NordicBeesERP.Models.WarehouseModule;

namespace NordicBeesERP.Services;

/// <summary>
/// PDF generation for supplier approval documents (BRC8 Clause 3.5).
/// </summary>
public interface ISupplierApprovalPdfService
{
    Task<byte[]> GenerateApprovalPdfAsync(int approvalId);
    Task<string> GenerateAndSaveApprovalPdfAsync(int approvalId);
}
