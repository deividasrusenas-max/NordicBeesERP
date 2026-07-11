namespace NordicBeesERP.Models.Printing;

/// <summary>
/// Data transfer object for ZPL label rendering.
/// Contains all fields needed to render receipt and quarantine labels.
/// DeliveryDate is used (NOT DateTime.Now) — BRC8 3.3 compliance.
/// </summary>
public class ContainerLabelData
{
    /// <summary>Unique container code (e.g. PR-MD2606-001/001)</summary>
    public string ContainerCode { get; set; } = string.Empty;

    /// <summary>Supplier name</summary>
    public string SupplierName { get; set; } = string.Empty;

    /// <summary>Raw material name (e.g. "Medus liepų")</summary>
    public string RawMaterialName { get; set; } = string.Empty;

    /// <summary>Country of origin (e.g. "Lietuva")</summary>
    public string OriginCountry { get; set; } = string.Empty;

    /// <summary>Net weight in kg</summary>
    public decimal NetWeightKg { get; set; }

    /// <summary>Tare weight in kg</summary>
    public decimal TareWeightKg { get; set; }

    /// <summary>Gross weight in kg</summary>
    public decimal GrossWeightKg { get; set; }

    /// <summary>Delivery date — used on label (NOT DateTime.Now)</summary>
    public DateTime DeliveryDate { get; set; }

    /// <summary>Warehouse name</summary>
    public string WarehouseName { get; set; } = string.Empty;

    /// <summary>Non-conformance ID (for quarantine labels)</summary>
    public int? NonConformanceId { get; set; }

    /// <summary>Container type: BARREL or BUCKET</summary>
    public string ContainerType { get; set; } = "BARREL";

    /// <summary>Delivery number for reference</summary>
    public string DeliveryNumber { get; set; } = string.Empty;
}
