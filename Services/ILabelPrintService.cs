using NordicBeesERP.Models.Printing;

namespace NordicBeesERP.Services;

public interface ILabelPrintService
{
    Task PrintReceiptLabelAsync(int containerId, int stationId, int? operatorId);
    Task PrintQuarantineLabelAsync(int containerId, int stationId, int? operatorId, int? nonConformanceId);
    Task ReprintLabelAsync(int containerId, ReprintReasonCode reasonCode, string? reasonText, int? operatorId);
}
