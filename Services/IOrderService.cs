using System.Collections.Generic;
using System.Threading.Tasks;
using NordicBeesERP.Models;

namespace NordicBeesERP.Services;

public interface IOrderService
{
    /// Get all orders, optionally filtered by status.
    Task<List<Order>> GetOrdersAsync(string? statusFilter = null);

    /// Get a single order by ID.
    Task<Order?> GetOrderByIdAsync(int id);

    /// Create a new order with its lines.
    Task<int> CreateOrderAsync(Order order, List<OrderLine> lines);

    /// Pack a single order line with lot/expiry info.
    Task PackLineAsync(int orderLineId, string lotNumber, DateTime? expiryDate, int userId);

    /// Mark an order as shipped (auto-checks if all lines packed → ready for pickup).
    Task MarkShippedAsync(int orderId, int userId);

    /// Create a partial shipment for selected pallets and recompute order status.
    /// Each entry in <paramref name="batchQuantities"/> is a (BatchId, Quantity) pair identifying the packed batch to ship and how many units of it are being shipped.
    Task CreateShipmentAsync(int orderId, DateTime shipmentDate, string? courierName, string? notes, int userId, List<(int BatchId, decimal Quantity)> batchQuantities);

    /// Link an external invoice ID to a shipped order.
    Task LinkInvoiceAsync(int orderId, int invoiceId, int userId);

    /// Get shipped orders that do not yet have an invoice linked.
    Task<List<Order>> GetUninvoicedShippedOrdersAsync();

    /// Get total pallet/quantity count for an order (sum of order_lines.quantity).
    Task<decimal> GetOrderPalletCountAsync(int orderId);

    /// Replace the packing batches for one order line (multi-pallet packing).
    /// Deletes unshipped existing batches for the line, inserts the given ones with packed_at NOW().
    Task SaveLineBatchesAsync(int orderLineId, List<OrderLineBatch> batches, int userId);

    /// Get all packed pallets (batches) for an order with their shipment status.
    Task<List<OrderPalletInfo>> GetOrderPalletsAsync(int orderId);
}
