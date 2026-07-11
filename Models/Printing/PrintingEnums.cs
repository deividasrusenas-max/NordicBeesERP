namespace NordicBeesERP.Models.Printing;

/// <summary>Connection type for printer communication</summary>
public enum PrinterConnectionType { HTTP, STUB }

/// <summary>Scale protocol for weighing stations</summary>
public enum ScaleProtocol { TOLEDO, METTLER, CAS, KERN, NONE }

/// <summary>Print job type</summary>
public enum PrintJobType { RECEIPT_LABEL, QUARANTINE_LABEL, REPRINT }

/// <summary>Print job status</summary>
public enum PrintJobStatus { PENDING, PROCESSING, DONE, FAILED, CANCELLED }

/// <summary>Container label event type</summary>
public enum ContainerLabelEventType { PRINTED, REPRINTED, QUARANTINE_PRINTED, CANCELLED, PRINT_FAILED }

/// <summary>Reason code for reprint events</summary>
public enum ReprintReasonCode { DAMAGED, LOST, MISPRINT, OTHER }

/// <summary>Weighing mode for containers</summary>
public enum WeighingMode { MANUAL, SCALE }

/// <summary>Weighing status for deliveries</summary>
public enum WeighingStatus { NOT_STARTED, IN_PROGRESS, COMPLETED }

/// <summary>Inspection result for deliveries</summary>
public enum InspectionResult { OK, NOK, CONDITIONAL }

/// <summary>Label template type</summary>
public enum LabelTemplateType
{
    RECEIPT_BARREL,
    RECEIPT_BUCKET,
    QUARANTINE_BARREL,
    QUARANTINE_BUCKET,
    LOT_BARREL,
    LOT_BUCKET
}

/// <summary>Supplier approval risk level</summary>
public enum SupplierRiskLevel { LOW, MEDIUM, HIGH }

/// <summary>Supplier approval method</summary>
public enum SupplierApprovalMethod { AUDIT, QUESTIONNAIRE, CERTIFICATION, OTHER }

/// <summary>Non-conformance severity</summary>
public enum NonConformanceSeverity { MINOR, MAJOR, CRITICAL }

/// <summary>Non-conformance disposition</summary>
public enum NonConformanceDisposition { PENDING, ACCEPTED, REJECTED, REWORKED, QUARANTINED }

/// <summary>Document file reference type</summary>
public enum DocumentFileRefType { DELIVERY, LOT, ORDER }

/// <summary>Document file document type</summary>
public enum DocumentFileDocType { PACKING_LIST, CMR, QUALITY_CERT, RECEIPT_ACT }
