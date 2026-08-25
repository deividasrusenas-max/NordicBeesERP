using System;

namespace NordicBeesERP.Models;

public class OrderLineBatch
{
    public int Id { get; set; }
    public int OrderLineId { get; set; }
    public string LotNumber { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public decimal Quantity { get; set; }
    public DateTime? PackedAt { get; set; }
    public int? PackedByUserId { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class OrderShipment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public DateTime ShipmentDate { get; set; }
    public string? CourierName { get; set; }
    public string? Notes { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class OrderShipmentPallet
{
    public int Id { get; set; }
    public int ShipmentId { get; set; }
    public int OrderLineBatchId { get; set; }
    public DateTime ShippedAt { get; set; }
}

// Display DTO for the pallet list UI (joins batch + order line + product)
public class OrderPalletInfo
{
    public int BatchId { get; set; }
    public int OrderLineId { get; set; }
    public int LineNumber { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string LotNumber { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal ShippedQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public bool IsShipped { get; set; }
    public DateTime? ShippedAt { get; set; }
}
