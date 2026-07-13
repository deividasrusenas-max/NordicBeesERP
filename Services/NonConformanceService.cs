using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models.WarehouseModule;

namespace NordicBeesERP.Services;

/// <summary>
/// BRC8 Clause 3.8 — Non-conformance tracking and quarantine disposition.
/// 
/// IMPORTANT: The C# NonConformance model columns (ref_type, ref_id, detected_at, etc.)
/// do NOT match the actual DB columns (delivery_id, container_id, discovered_at, etc.).
/// All SQL is hand-written to hit the real DB columns. We never rely on EF Core mapping
/// for this entity — it is out of sync with the schema.
/// </summary>
public class NonConformanceService : INonConformanceService
{
    private readonly IDbContextFactory<NordicBeesERPContext> _contextFactory;

    public NonConformanceService(IDbContextFactory<NordicBeesERPContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Create a new non-conformance record (INSERT).
    /// Maps from the C# model properties to actual DB columns.
    /// Returns the persisted entity with generated Id.
    /// </summary>
    public async Task<NonConformance> CreateAsync(NonConformance nc, CancellationToken ct = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(ct);

        // Map C# model → actual DB columns.
        // RefType="DELIVERY"  → delivery_id = RefId, container_id = NULL
        // RefType="CONTAINER" → container_id = RefId, delivery_id = lookup from containers table
        int? deliveryId = null;
        int? containerId = null;

        if (nc.RefType == "CONTAINER")
        {
            containerId = nc.RefId;
            // Look up delivery_id from the container row using ExecuteScalarAsync
            var conn = context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync(ct);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT delivery_id FROM containers WHERE id = @p_id";
            var param = cmd.CreateParameter();
            param.ParameterName = "@p_id";
            param.Value = (object?)containerId ?? DBNull.Value;
            cmd.Parameters.Add(param);
            var result = await cmd.ExecuteScalarAsync(ct);
            deliveryId = result != null ? Convert.ToInt32(result) : (int?)null;
        }
        else
        {
            deliveryId = nc.RefId;
        }

        // severity → nc_type mapping (best effort, different enum domains)
        var ncType = nc.Severity switch
        {
            "MINOR" => "OTHER",
            "MAJOR" => "QUALITY",
            "CRITICAL" => "QUALITY",
            _ => "OTHER"
        };

        // disposition → status mapping (different enum domains)
        var dbStatus = nc.Disposition switch
        {
            "PENDING" => "OPEN",
            "ACCEPTED" => "RESOLVED",
            "REJECTED" => "CLOSED",
            "REWORKED" => "INVESTIGATING",
            "QUARANTINED" => "INVESTIGATING",
            _ => "OPEN"
        };

        // INSERT using actual DB column names
        var insertSql = @"
            INSERT INTO non_conformances 
                (delivery_id, container_id, description, nc_type, discovered_by, discovered_at, status, corrective_action)
            VALUES 
                ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7})";

        await context.Database.ExecuteSqlRawAsync(
            insertSql,
            (object?)deliveryId,
            (object?)containerId,
            nc.Description ?? string.Empty,
            ncType,
            nc.DetectedBy,
            nc.DetectedAt,
            dbStatus,
            (object?)(nc.DispositionNotes ?? (object)DBNull.Value));

        // Get the generated Id via LAST_INSERT_ID()
        using var cmd2 = context.Database.GetDbConnection().CreateCommand();
        cmd2.CommandText = "SELECT LAST_INSERT_ID()";
        var newIdObj = await cmd2.ExecuteScalarAsync(ct);
        var newId = newIdObj != null ? Convert.ToInt32(newIdObj) : 0;

        // BRC8 3.9 — Insert ContainerLabelEvent(NON_CONFORMITY) for traceability audit trail.
        // If RefType="CONTAINER" → event on that single container.
        // If RefType="DELIVERY" → event on every container belonging to that delivery.
        if (containerId.HasValue)
        {
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO container_label_events (container_id, event_type, reason_text, operator_id, created_at) VALUES ({0}, 'NON_CONFORMITY', {1}, {2}, NOW())",
                containerId.Value,
                nc.Description ?? string.Empty,
                (object?)nc.DetectedBy ?? DBNull.Value);
        }
        else if (deliveryId.HasValue)
        {
            // Delivery-level non-conformance: touch every container in the delivery
            await context.Database.ExecuteSqlRawAsync(
                @"INSERT INTO container_label_events (container_id, event_type, reason_text, operator_id, created_at)
                  SELECT id, 'NON_CONFORMITY', {0}, {1}, NOW()
                  FROM containers WHERE delivery_id = {2}",
                nc.Description ?? string.Empty,
                (object?)nc.DetectedBy ?? DBNull.Value,
                deliveryId.Value);
        }

        return new NonConformance
        {
            Id = Convert.ToInt32(newId),
            RefType = nc.RefType,
            RefId = nc.RefId,
            Description = nc.Description ?? string.Empty,
            Severity = nc.Severity,
            DetectedBy = nc.DetectedBy,
            DetectedAt = nc.DetectedAt,
            Disposition = nc.Disposition,
            DispositionNotes = nc.DispositionNotes,
            DispositionBy = nc.DispositionBy,
            DispositionAt = nc.DispositionAt,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Get a single non-conformance by primary key.
    /// Reads directly from actual DB columns and maps to the C# model.
    /// </summary>
    public async Task<NonConformance?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(ct);

        var rows = await context.NonConformances
            .FromSqlRaw(@"
                SELECT 
                    id,
                    delivery_id   AS ref_id,
                    'DELIVERY'    AS ref_type,
                    discovered_at AS detected_at,
                    discovered_by AS detected_by,
                    description,
                    nc_type       AS severity,
                    status        AS disposition,
                    closed_by     AS disposition_by,
                    closed_at     AS disposition_at,
                    corrective_action AS disposition_notes,
                    discovered_at AS created_at
                FROM non_conformances 
                WHERE id = {0}", id)
            .AsNoTracking()
            .ToListAsync(ct);

        return rows.FirstOrDefault();
    }

    /// <summary>
    /// Get all non-conformances referencing a specific container.
    /// </summary>
    public async Task<IEnumerable<NonConformance>> GetByContainerIdAsync(int containerId, CancellationToken ct = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.NonConformances
            .FromSqlRaw(@"
                SELECT 
                    id,
                    container_id  AS ref_id,
                    'CONTAINER'   AS ref_type,
                    discovered_at AS detected_at,
                    discovered_by AS detected_by,
                    description,
                    nc_type       AS severity,
                    status        AS disposition,
                    closed_by     AS disposition_by,
                    closed_at     AS disposition_at,
                    corrective_action AS disposition_notes,
                    discovered_at AS created_at
                FROM non_conformances 
                WHERE container_id = {0}", containerId)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    /// <summary>
    /// Update the status of an existing non-conformance (UPDATE).
    /// When status is RESOLVED or CLOSED, also sets closed_by and closed_at.
    /// Uses ExecuteSqlRawAsync per EF Core Rule 1 (global NoTracking).
    /// </summary>
    public async Task UpdateStatusAsync(int id, string status, int? closedBy, CancellationToken ct = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(ct);

        var sql = status switch
        {
            "RESOLVED" or "CLOSED" => @"
                UPDATE non_conformances 
                SET status = {0}, closed_by = {1}, closed_at = NOW() 
                WHERE id = {2}",
            _ => @"
                UPDATE non_conformances 
                SET status = {0} 
                WHERE id = {1}"
        };

        if (status is "RESOLVED" or "CLOSED")
        {
            await context.Database.ExecuteSqlRawAsync(
                sql, status, (object?)closedBy ?? DBNull.Value, id);
        }
        else
        {
            await context.Database.ExecuteSqlRawAsync(sql, status, id);
        }
    }
}
