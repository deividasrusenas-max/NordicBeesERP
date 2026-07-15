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

    /// Link an external invoice ID to a shipped order.
    Task LinkInvoiceAsync(int orderId, int invoiceId, int userId);

    /// Get shipped orders that do not yet have an invoice linked.
    Task<List<Order>> GetUninvoicedShippedOrdersAsync();

    /// Get total pallet/quantity count for an order (sum of order_lines.quantity).
    Task<decimal> GetOrderPalletCountAsync(int orderId);
}
