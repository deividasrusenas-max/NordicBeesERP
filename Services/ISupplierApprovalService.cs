using NordicBeesERP.Models.WarehouseModule;

namespace NordicBeesERP.Services;

/// <summary>
/// BRC8 Clause 3.5 — Supplier approval management.
/// Queries and inserts against supplier_approvals table.
/// </summary>
public interface ISupplierApprovalService
{
    /// <summary>
    /// Creates a new supplier approval record.
    /// Automatically marks any existing current approval for the same supplier as no longer current.
    /// </summary>
    /// <returns>The generated approval id.</returns>
    Task<int> CreateApprovalAsync(SupplierApproval approval, CancellationToken ct = default);

    /// <summary>
    /// Returns true if the supplier has any approval record (current or historical).
    /// </summary>
    Task<bool> IsSupplierApprovedAsync(int supplierId, CancellationToken ct = default);

    /// <summary>
    /// Returns the most recent approval record for the supplier, or null if none exists.
    /// </summary>
    Task<SupplierApproval?> GetCurrentApprovalAsync(int supplierId, CancellationToken ct = default);

    /// <summary>
    /// Returns true if the supplier has an approval that is not yet expired.
    /// An approval with a null expiration date is considered indefinitely active.
    /// </summary>
    Task<bool> HasActiveApprovalAsync(int supplierId, CancellationToken ct = default);
}
