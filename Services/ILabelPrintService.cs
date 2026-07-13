using NordicBeesERP.Models.Printing;

namespace NordicBeesERP.Services;

public interface ILabelPrintService
{
    /// <summary>
    /// Print a receipt label for a container. Returns the print job ID.
    /// Flow: get container → RenderZpl → INSERT print_jobs(PENDING) → INSERT container_label_events.
    /// </summary>
    Task<int> PrintReceiptLabelAsync(int containerId, int stationId, int operatorId);

    /// <summary>
    /// Print a quarantine label for a container. Returns the print job ID.
    /// </summary>
    Task<int> PrintQuarantineLabelAsync(int containerId, int stationId, int operatorId, int? nonConformanceId);

    /// <summary>
    /// Reprint an existing label. Returns the print job ID.
    /// </summary>
    Task<int> ReprintLabelAsync(int containerId, ReprintReasonCode reasonCode, string? reasonText, int operatorId);
}
