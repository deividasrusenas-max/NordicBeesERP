using NordicBeesERP.Models.WarehouseModule;

namespace NordicBeesERP.Services;

/// <summary>
/// BRC8 Clause 3.8 — Non-conformance tracking and quarantine disposition.
/// </summary>
public interface INonConformanceService
{
    /// <summary>
    /// Create a new non-conformance record (INSERT).
    /// Returns the persisted entity with generated Id.
    /// </summary>
    Task<NonConformance> CreateAsync(NonConformance nc, CancellationToken ct = default);

    /// <summary>
    /// Get a single non-conformance by primary key.
    /// </summary>
    Task<NonConformance?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Get all non-conformances referencing a specific container.
    /// </summary>
    Task<IEnumerable<NonConformance>> GetByContainerIdAsync(int containerId, CancellationToken ct = default);

    /// <summary>
    /// Update the disposition of an existing non-conformance (UPDATE).
    /// Sets disposition, disposition_by, and disposition_at in a single SQL statement.
    /// </summary>
    Task UpdateStatusAsync(int id, string status, int? closedBy, CancellationToken ct = default);
}
