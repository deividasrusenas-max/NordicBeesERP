using System;

namespace NordicBeesERP.Models;

public class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int LineNumber { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? Price { get; set; }
    public string? Notes { get; set; }

    // Packing / traceability
    public string? LotNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? PackedAt { get; set; }
    public int? PackedByUserId { get; set; }
}
