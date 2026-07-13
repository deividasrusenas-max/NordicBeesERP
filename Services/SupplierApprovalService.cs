using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models.WarehouseModule;

namespace NordicBeesERP.Services;

/// <summary>
/// BRC8 Clause 3.5 — Supplier approval checks.
/// Read-only queries against supplier_approvals table.
/// </summary>
public class SupplierApprovalService : ISupplierApprovalService
{
    private readonly IDbContextFactory<NordicBeesERPContext> _contextFactory;

    public SupplierApprovalService(IDbContextFactory<NordicBeesERPContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Creates a new supplier approval record.
    /// Automatically marks any existing current approval for the same supplier as no longer current.
    /// Uses an EF Core tracked insert (Add + SaveChangesAsync) so nullable columns and the
    /// auto-increment id are handled natively — avoids the "store type mapping for DBNull"
    /// error that raw SQL parameters with DBNull.Value would trigger.
    /// </summary>
    public async Task<int> CreateApprovalAsync(SupplierApproval approval, CancellationToken ct = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(ct);
        using var transaction = await context.Database.BeginTransactionAsync(ct);
        try
        {
            // Step 1: mark any existing current approval as no longer current
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE supplier_approvals SET is_current = 0 WHERE supplier_id = {0} AND is_current = 1",
                approval.SupplierId);

            // Step 2: insert the new approval via EF Core (handles nulls and the
            // auto-generated id natively — no DBNull.Value raw-SQL parameters)
            context.SupplierApprovals.Add(approval);
            await context.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);
            return approval.Id;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>
    /// Returns true if the supplier has any approval record (current or historical).
    /// </summary>
    public async Task<bool> IsSupplierApprovedAsync(int supplierId, CancellationToken ct = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var exists = await context.SupplierApprovals
            .AnyAsync(a => a.SupplierId == supplierId, ct);
        return exists;
    }

    /// <summary>
    /// Returns the most recent approval record for the supplier, or null if none exists.
    /// Uses the is_current flag and falls back to the most recent by created_at.
    /// </summary>
    public async Task<SupplierApproval?> GetCurrentApprovalAsync(int supplierId, CancellationToken ct = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        // First try to find a record marked as current
        var current = await context.SupplierApprovals
            .AsNoTracking()
            .Where(a => a.SupplierId == supplierId && a.IsCurrent)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (current != null)
            return current;

        // Fallback: most recent approval by created_at
        return await context.SupplierApprovals
            .AsNoTracking()
            .Where(a => a.SupplierId == supplierId)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Returns true if the supplier has an approval that is not yet expired.
    /// An approval with a null expiration date is considered indefinitely active.
    /// Uses current UTC date for the comparison.
    /// </summary>
    public async Task<bool> HasActiveApprovalAsync(int supplierId, CancellationToken ct = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow.Date;

        var hasActive = await context.SupplierApprovals
            .AsNoTracking()
            .AnyAsync(a => a.SupplierId == supplierId
                && (a.ExpiresAt == null || a.ExpiresAt.Value.Date >= now), ct);

        return hasActive;
    }
}
