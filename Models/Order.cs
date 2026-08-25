using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models;

public class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public int CustomerId { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string Status { get; set; } = "draft";
    public string? Notes { get; set; }

    // Shipping / pickup
    public DateTime? ShippedAt { get; set; }
    public int? ShippedByUserId { get; set; }

    // Invoice linkage
    public int? InvoiceId { get; set; }
    public DateTime? InvoicedAt { get; set; }
    public int? InvoicedByUserId { get; set; }

    // Audit
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Transient — populated by the service, never persisted
    [NotMapped]
    public List<OrderLine> Lines { get; set; } = new();

    // Transient — computed by GetOrdersAsync: true when shipped AND invoice_id IS NULL
    [NotMapped]
    public bool IsUninvoiced { get; set; }

    // Transient — populated by GetOrdersAsync: shipped unit total across all lines/batches (partial-shipment progress display)
    [NotMapped]
    public decimal ShippedQuantity { get; set; }

    // Transient — populated by GetOrdersAsync: total ordered units across all lines (partial-shipment progress display)
    [NotMapped]
    public decimal TotalQuantity { get; set; }
}
