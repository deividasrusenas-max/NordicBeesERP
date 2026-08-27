using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models;
using System.Data;
using System.Data.Common;

namespace NordicBeesERP.Services;

public class OrderService : IOrderService
{
    private readonly IDbContextFactory<NordicBeesERPContext> _contextFactory;
    private readonly TelegramNotificationService? _telegram;

    public OrderService(IDbContextFactory<NordicBeesERPContext> contextFactory, TelegramNotificationService? telegram = null)
    {
        _contextFactory = contextFactory;
        _telegram = telegram;
    }

    // =====================================================
    // READS
    // =====================================================

    public async Task<List<Order>> GetOrdersAsync(string? statusFilter = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var sql = @"SELECT id AS Id, order_number AS OrderNumber, order_date AS OrderDate, 
                     customer_id AS CustomerId, delivery_date AS DeliveryDate, 
                     status AS Status, notes AS Notes, 
                     shipped_at AS ShippedAt, shipped_by_user_id AS ShippedByUserId, 
                     invoice_id AS InvoiceId, invoiced_at AS InvoicedAt, invoiced_by_user_id AS InvoicedByUserId, 
                     created_at AS CreatedAt, updated_at AS UpdatedAt, 
                     (SELECT COALESCE(SUM(osp.quantity_shipped), 0) FROM order_shipment_pallets osp 
                       INNER JOIN order_line_batches b ON b.id = osp.order_line_batch_id 
                       INNER JOIN order_lines ol ON ol.id = b.order_line_id 
                       WHERE ol.order_id = orders.id) AS ShippedQuantity, 
                     (SELECT COALESCE(SUM(ol2.quantity), 0) FROM order_lines ol2 WHERE ol2.order_id = orders.id) AS TotalQuantity 
                     FROM orders";

        if (!string.IsNullOrEmpty(statusFilter))
            sql += " WHERE status = @status";

        sql += " ORDER BY order_date DESC, id DESC";

        var conn = context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        if (!string.IsNullOrEmpty(statusFilter))
        {
            var p = cmd.CreateParameter();
            p.ParameterName = "@status";
            p.Value = statusFilter;
            cmd.Parameters.Add(p);
        }

        var orders = new List<Order>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var o = new Order
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                OrderNumber = reader.GetString(reader.GetOrdinal("OrderNumber")),
                OrderDate = reader.GetDateTime(reader.GetOrdinal("OrderDate")),
                CustomerId = reader.GetInt32(reader.GetOrdinal("CustomerId")),
                DeliveryDate = ReadNullableDateTime(reader, "DeliveryDate"),
                Status = reader.GetString(reader.GetOrdinal("Status")),
                Notes = ReadNullableString(reader, "Notes"),
                ShippedAt = ReadNullableDateTime(reader, "ShippedAt"),
                ShippedByUserId = ReadNullableInt(reader, "ShippedByUserId"),
                InvoiceId = ReadNullableInt(reader, "InvoiceId"),
                InvoicedAt = ReadNullableDateTime(reader, "InvoicedAt"),
                InvoicedByUserId = ReadNullableInt(reader, "InvoicedByUserId"),
                CreatedAt = ReadNullableDateTime(reader, "CreatedAt"),
                UpdatedAt = ReadNullableDateTime(reader, "UpdatedAt")
            };
            o.IsUninvoiced = o.Status == "shipped" && !o.InvoiceId.HasValue;
            o.ShippedQuantity = reader.GetDecimal(reader.GetOrdinal("ShippedQuantity"));
            o.TotalQuantity = reader.GetDecimal(reader.GetOrdinal("TotalQuantity"));
            orders.Add(o);
        }

        return orders;
    }

    public async Task<Order?> GetOrderByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var conn = context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync();

        // --- Order ---
        var orderSql = @"SELECT id AS Id, order_number AS OrderNumber, order_date AS OrderDate, 
                       customer_id AS CustomerId, delivery_date AS DeliveryDate, 
                       status AS Status, notes AS Notes, 
                       shipped_at AS ShippedAt, shipped_by_user_id AS ShippedByUserId, 
                       invoice_id AS InvoiceId, invoiced_at AS InvoicedAt, invoiced_by_user_id AS InvoicedByUserId, 
                       created_at AS CreatedAt, updated_at AS UpdatedAt 
                       FROM orders WHERE id = @id";

        var cmd = conn.CreateCommand();
        cmd.CommandText = orderSql;
        var idParam = cmd.CreateParameter();
        idParam.ParameterName = "@id";
        idParam.Value = id;
        cmd.Parameters.Add(idParam);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var order = new Order
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            OrderNumber = reader.GetString(reader.GetOrdinal("OrderNumber")),
            OrderDate = reader.GetDateTime(reader.GetOrdinal("OrderDate")),
            CustomerId = reader.GetInt32(reader.GetOrdinal("CustomerId")),
            DeliveryDate = ReadNullableDateTime(reader, "DeliveryDate"),
            Status = reader.GetString(reader.GetOrdinal("Status")),
            Notes = ReadNullableString(reader, "Notes"),
            ShippedAt = ReadNullableDateTime(reader, "ShippedAt"),
            ShippedByUserId = ReadNullableInt(reader, "ShippedByUserId"),
            InvoiceId = ReadNullableInt(reader, "InvoiceId"),
            InvoicedAt = ReadNullableDateTime(reader, "InvoicedAt"),
            InvoicedByUserId = ReadNullableInt(reader, "InvoicedByUserId"),
            CreatedAt = ReadNullableDateTime(reader, "CreatedAt"),
            UpdatedAt = ReadNullableDateTime(reader, "UpdatedAt")
        };
        order.IsUninvoiced = order.Status == "shipped" && !order.InvoiceId.HasValue;
        await reader.DisposeAsync();

        // --- Lines ---
        var linesSql = @"SELECT id AS Id, order_id AS OrderId, line_number AS LineNumber, 
                       product_id AS ProductId, quantity AS Quantity, price AS Price, notes AS Notes, 
                       lot_number AS LotNumber, expiry_date AS ExpiryDate, 
                       packed_at AS PackedAt, packed_by_user_id AS PackedByUserId 
                       FROM order_lines WHERE order_id = @orderId ORDER BY line_number";

        cmd.CommandText = linesSql;
        cmd.Parameters.Clear();
        var orderIdParam = cmd.CreateParameter();
        orderIdParam.ParameterName = "@orderId";
        orderIdParam.Value = id;
        cmd.Parameters.Add(orderIdParam);

        var lines = new List<OrderLine>();
        await using var lineReader = await cmd.ExecuteReaderAsync();
        while (await lineReader.ReadAsync())
        {
            lines.Add(new OrderLine
            {
                Id = lineReader.GetInt32(lineReader.GetOrdinal("Id")),
                OrderId = lineReader.GetInt32(lineReader.GetOrdinal("OrderId")),
                LineNumber = lineReader.GetInt32(lineReader.GetOrdinal("LineNumber")),
                ProductId = lineReader.GetInt32(lineReader.GetOrdinal("ProductId")),
                Quantity = lineReader.GetDecimal(lineReader.GetOrdinal("Quantity")),
                Price = ReadNullableDecimal(lineReader, "Price"),
                Notes = ReadNullableString(lineReader, "Notes"),
                LotNumber = ReadNullableString(lineReader, "LotNumber"),
                ExpiryDate = ReadNullableDateTime(lineReader, "ExpiryDate"),
                PackedAt = ReadNullableDateTime(lineReader, "PackedAt"),
                PackedByUserId = ReadNullableInt(lineReader, "PackedByUserId")
            });
        }

        order.Lines = lines;
        return order;
    }

    // =====================================================
    // WRITES
    // =====================================================

    public async Task<int> CreateOrderAsync(Order order, List<OrderLine> lines)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Generate order number if not provided (read-only, can stay outside transaction)
        if (string.IsNullOrWhiteSpace(order.OrderNumber))
            order.OrderNumber = await GenerateNextOrderNumberAsync();

        var status = string.IsNullOrEmpty(order.Status) ? "draft" : order.Status;

        // Wrap INSERT order + LAST_INSERT_ID() + INSERT order_lines in a single transaction.
        // This guarantees the same underlying connection is used for all three operations,
        // so LAST_INSERT_ID() returns the correct auto-increment ID from the order INSERT.
        // Without this, EF Core may return the connection to the pool after the INSERT,
        // and LAST_INSERT_ID() on a new connection returns 0 → FK violation on order_lines.
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO orders (order_number, order_date, customer_id, delivery_date, status, notes, created_at, updated_at) " +
                "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, NOW(), NOW())",
                order.OrderNumber,
                order.OrderDate,
                order.CustomerId,
                order.DeliveryDate,
                status,
                order.Notes
            );

            var newOrderId = await context.Database.SqlQueryRaw<int>("SELECT LAST_INSERT_ID() as Value").FirstAsync();

            // Insert lines
            foreach (var line in lines)
            {
                await context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO order_lines (order_id, line_number, product_id, quantity, price, notes, lot_number, expiry_date, packed_at, packed_by_user_id) " +
                    "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9})",
                    newOrderId,
                    line.LineNumber,
                    line.ProductId,
                    line.Quantity,
                    line.Price,
                    line.Notes,
                    line.LotNumber,
                    line.ExpiryDate,
                    line.PackedAt,
                    line.PackedByUserId
                );
            }

            await transaction.CommitAsync();

            if (_telegram is not null)
                _ = _telegram.SendToGroupAsync("uzsakymai",
                    $"🆕 Naujas užsakymas\n\n🔹 Užsakymo nr.: {order.OrderNumber}\n🔹 Klientas (ID): {order.CustomerId}");

            return newOrderId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task PackLineAsync(int orderLineId, string lotNumber, DateTime? expiryDate, int userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Get the order_id for this line
        var orderId = await context.Database.SqlQueryRaw<int>(
            "SELECT order_id AS Value FROM order_lines WHERE id = {0}", orderLineId).FirstAsync();

        // Update the line with packing info
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE order_lines SET lot_number = {0}, expiry_date = {1}, packed_at = NOW(), packed_by_user_id = {2} WHERE id = {3}",
            lotNumber, expiryDate, userId, orderLineId);

        // Check if all lines are now packed → auto-transition to ready_for_pickup
        await MarkReadyForPickupCheckAsync(context, orderId);
    }

    public async Task SaveLineBatchesAsync(int orderLineId, List<OrderLineBatch> batches, int userId)
    {
        if (batches == null || batches.Count == 0)
            throw new ArgumentException("Batches list cannot be empty", nameof(batches));

        await using var context = await _contextFactory.CreateDbContextAsync();

        // Multi-statement write: delete unshipped old batches + insert new ones + stamp the line,
        // all on one connection so LAST_INSERT_ID()/the readiness check see consistent state.
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            // Delete only UNshipped existing batches for this line (batches referenced by
            // order_shipment_pallets are already part of a shipment and must be kept).
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM order_line_batches WHERE order_line_id = {0} AND id NOT IN (SELECT order_line_batch_id FROM order_shipment_pallets)",
                orderLineId);

            foreach (var batch in batches)
            {
                await context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO order_line_batches (order_line_id, lot_number, expiry_date, quantity, packed_at, packed_by_user_id) " +
                    "VALUES ({0}, {1}, {2}, {3}, NOW(), {4})",
                    orderLineId,
                    batch.LotNumber,
                    batch.ExpiryDate,
                    batch.Quantity,
                    userId);
            }

            // Stamp the parent line as packed (deliberately NOT touching lot_number/expiry_date —
            // those are per-batch now, see order_line_batches).
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE order_lines SET packed_at = NOW(), packed_by_user_id = {0} WHERE id = {1}",
                userId, orderLineId);

            var orderId = await context.Database.SqlQueryRaw<int>(
                "SELECT order_id AS Value FROM order_lines WHERE id = {0}", orderLineId).FirstAsync();

            // Check if all lines are now packed → auto-transition to ready_for_pickup
            await MarkReadyForPickupCheckAsync(context, orderId);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task MarkShippedAsync(int orderId, int userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        await context.Database.ExecuteSqlRawAsync(
            "UPDATE orders SET status = 'shipped', shipped_at = NOW(), shipped_by_user_id = {0} WHERE id = {1} AND status = 'ready_for_pickup'",
            userId, orderId);

        var orderNumber = await context.Database.SqlQueryRaw<string>(
            "SELECT order_number AS Value FROM orders WHERE id = {0}", orderId).FirstOrDefaultAsync();
        var hasInvoice = await context.Database.SqlQueryRaw<int?>(
            "SELECT invoice_id AS Value FROM orders WHERE id = {0}", orderId).FirstOrDefaultAsync() != null;
        if (!hasInvoice && _telegram is not null)
            _ = _telegram.SendToGroupAsync("uzsakymai",
                $"⚠️ Užsakymas uždarytas be sąskaitos faktūros\n\n🔹 Užsakymo nr.: {orderNumber}");
    }

    public async Task CreateShipmentAsync(int orderId, DateTime shipmentDate, string? courierName, string? notes, int userId, List<(int BatchId, decimal Quantity)> batchQuantities)
    {
        if (batchQuantities == null || batchQuantities.Count == 0)
            throw new ArgumentException("Batch quantities list cannot be empty", nameof(batchQuantities));

        foreach (var (_, quantity) in batchQuantities)
        {
            if (quantity <= 0)
                throw new ArgumentException("Shipment quantity must be greater than zero");
        }

        await using var context = await _contextFactory.CreateDbContextAsync();

        // Multi-statement write: INSERT shipment + per-batch shipped-quantity rows + status recompute,
        // all on one connection/transaction so LAST_INSERT_ID() and the counts are consistent.
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO order_shipments (order_id, shipment_date, courier_name, notes, created_by_user_id) " +
                "VALUES ({0}, {1}, {2}, {3}, {4})",
                orderId, shipmentDate, courierName, notes, userId);

            var newShipmentId = await context.Database.SqlQueryRaw<int>("SELECT LAST_INSERT_ID() AS Value").FirstAsync();

            foreach (var (batchId, quantity) in batchQuantities)
            {
                // Server-side re-validation against fresh DB state inside the transaction:
                // remaining = batch.quantity - already shipped for this batch.
                var remaining = await context.Database.SqlQueryRaw<decimal>(
                    "SELECT b.quantity - COALESCE((SELECT SUM(osp.quantity_shipped) FROM order_shipment_pallets osp WHERE osp.order_line_batch_id = b.id), 0) AS Value " +
                    "FROM order_line_batches b INNER JOIN order_lines ol ON ol.id = b.order_line_id " +
                    "WHERE b.id = {0} AND ol.order_id = {1}",
                    batchId, orderId).FirstOrDefaultAsync();

                if (remaining == null)
                    throw new InvalidOperationException($"Partija #{batchId} nerasta šiame užsakyme arba ji nebeegzistuoja.");

                if (remaining <= 0)
                    throw new InvalidOperationException($"Partijos #{batchId} kiekis jau yra visiškai išsiųstas – likęs kiekis: 0.");

                if (quantity > remaining)
                    throw new InvalidOperationException($"Nurodytas kiekis ({quantity}) viršija partijos #{batchId} likusį kiekį ({remaining}).");

                // Deliberately NO one-row-per-batch guard: multiple rows per batch across
                // different shipments must be allowed (partial shipments accumulate).
                await context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO order_shipment_pallets (shipment_id, order_line_batch_id, quantity_shipped, shipped_at) " +
                    "VALUES ({0}, {1}, {2}, NOW())",
                    newShipmentId, batchId, quantity);
            }

            var totalBatches = await context.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM order_line_batches b INNER JOIN order_lines ol ON ol.id = b.order_line_id WHERE ol.order_id = {0}",
                orderId).FirstAsync();

            var notFullyShippedBatches = await context.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM order_line_batches b " +
                "INNER JOIN order_lines ol ON ol.id = b.order_line_id " +
                "WHERE COALESCE((SELECT SUM(osp.quantity_shipped) FROM order_shipment_pallets osp WHERE osp.order_line_batch_id = b.id), 0) < b.quantity " +
                "AND ol.order_id = {0}",
                orderId).FirstAsync();

            if (totalBatches > 0 && notFullyShippedBatches == 0)
            {
                await context.Database.ExecuteSqlRawAsync(
                    "UPDATE orders SET status = 'shipped', shipped_at = NOW(), shipped_by_user_id = {0}, updated_at = NOW() WHERE id = {1} AND status IN ('ready_for_pickup', 'partially_shipped')",
                    userId, orderId);

                var orderNumber = await context.Database.SqlQueryRaw<string>(
                    "SELECT order_number AS Value FROM orders WHERE id = {0}", orderId).FirstOrDefaultAsync();
                var hasInvoice = await context.Database.SqlQueryRaw<int?>(
                    "SELECT invoice_id AS Value FROM orders WHERE id = {0}", orderId).FirstOrDefaultAsync() != null;
                if (!hasInvoice && _telegram is not null)
                    _ = _telegram.SendToGroupAsync("uzsakymai",
                        $"⚠️ Užsakymas uždarytas be sąskaitos faktūros\n\n🔹 Užsakymo nr.: {orderNumber}");
            }
            else if (totalBatches > 0)
            {
                await context.Database.ExecuteSqlRawAsync(
                    "UPDATE orders SET status = 'partially_shipped', updated_at = NOW() WHERE id = {0} AND status IN ('ready_for_pickup', 'partially_shipped', 'confirmed', 'packing')",
                    orderId);
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task LinkInvoiceAsync(int orderId, int invoiceId, int userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Invoices can only be linked once the order is fully shipped.
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE orders SET invoice_id = {0}, invoiced_at = NOW(), invoiced_by_user_id = {1} WHERE id = {2} AND status = 'shipped'",
            invoiceId, userId, orderId);
    }

    public async Task<List<Order>> GetUninvoicedShippedOrdersAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var sql = @"SELECT id AS Id, order_number AS OrderNumber, order_date AS OrderDate, 
                     customer_id AS CustomerId, delivery_date AS DeliveryDate, 
                     status AS Status, notes AS Notes, 
                     shipped_at AS ShippedAt, shipped_by_user_id AS ShippedByUserId, 
                     invoice_id AS InvoiceId, invoiced_at AS InvoicedAt, invoiced_by_user_id AS InvoicedByUserId, 
                     created_at AS CreatedAt, updated_at AS UpdatedAt 
                     FROM orders WHERE status = 'shipped' AND invoice_id IS NULL 
                     ORDER BY order_date DESC, id DESC";

        var conn = context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        var orders = new List<Order>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var o = new Order
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                OrderNumber = reader.GetString(reader.GetOrdinal("OrderNumber")),
                OrderDate = reader.GetDateTime(reader.GetOrdinal("OrderDate")),
                CustomerId = reader.GetInt32(reader.GetOrdinal("CustomerId")),
                DeliveryDate = ReadNullableDateTime(reader, "DeliveryDate"),
                Status = reader.GetString(reader.GetOrdinal("Status")),
                Notes = ReadNullableString(reader, "Notes"),
                ShippedAt = ReadNullableDateTime(reader, "ShippedAt"),
                ShippedByUserId = ReadNullableInt(reader, "ShippedByUserId"),
                InvoiceId = ReadNullableInt(reader, "InvoiceId"),
                InvoicedAt = ReadNullableDateTime(reader, "InvoicedAt"),
                InvoicedByUserId = ReadNullableInt(reader, "InvoicedByUserId"),
                CreatedAt = ReadNullableDateTime(reader, "CreatedAt"),
                UpdatedAt = ReadNullableDateTime(reader, "UpdatedAt")
            };
            o.IsUninvoiced = true;
            orders.Add(o);
        }

        return orders;
    }

    public async Task<decimal> GetOrderPalletCountAsync(int orderId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var count = await context.Database.SqlQueryRaw<decimal>(
            "SELECT COALESCE(SUM(quantity), 0) AS Value FROM order_lines WHERE order_id = {0}",
            orderId).FirstAsync();

        return count;
    }

    public async Task<List<OrderPalletInfo>> GetOrderPalletsAsync(int orderId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var sql = @"SELECT b.id AS BatchId,
                    b.order_line_id AS OrderLineId,
                    ol.line_number AS LineNumber,
                    ol.product_id AS ProductId,
                    COALESCE(p.name, '') AS ProductName,
                    b.lot_number AS LotNumber,
                    b.expiry_date AS ExpiryDate,
                    b.quantity AS Quantity,
                    COALESCE(osp_agg.shipped_sum, 0) AS ShippedQuantity,
                    GREATEST(b.quantity - COALESCE(osp_agg.shipped_sum, 0), 0) AS RemainingQuantity,
                    CASE WHEN GREATEST(b.quantity - COALESCE(osp_agg.shipped_sum, 0), 0) <= 0 THEN 1 ELSE 0 END AS IsShipped,
                    osp_agg.last_shipped_at AS ShippedAt
                    FROM order_line_batches b
                    INNER JOIN order_lines ol ON ol.id = b.order_line_id
                    LEFT JOIN products p ON p.id = ol.product_id
                    LEFT JOIN (
                        SELECT order_line_batch_id,
                               SUM(quantity_shipped) AS shipped_sum,
                               MAX(shipped_at) AS last_shipped_at
                        FROM order_shipment_pallets
                        GROUP BY order_line_batch_id
                    ) osp_agg ON osp_agg.order_line_batch_id = b.id
                    WHERE ol.order_id = @orderId
                    ORDER BY ol.line_number, b.id";

        var conn = context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var orderIdParam = cmd.CreateParameter();
        orderIdParam.ParameterName = "@orderId";
        orderIdParam.Value = orderId;
        cmd.Parameters.Add(orderIdParam);

        var pallets = new List<OrderPalletInfo>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            pallets.Add(new OrderPalletInfo
            {
                BatchId = reader.GetInt32(reader.GetOrdinal("BatchId")),
                OrderLineId = reader.GetInt32(reader.GetOrdinal("OrderLineId")),
                LineNumber = reader.GetInt32(reader.GetOrdinal("LineNumber")),
                ProductId = reader.GetInt32(reader.GetOrdinal("ProductId")),
                ProductName = reader.GetString(reader.GetOrdinal("ProductName")),
                LotNumber = reader.GetString(reader.GetOrdinal("LotNumber")),
                ExpiryDate = reader.GetDateTime(reader.GetOrdinal("ExpiryDate")),
                Quantity = reader.GetDecimal(reader.GetOrdinal("Quantity")),
                ShippedQuantity = reader.GetDecimal(reader.GetOrdinal("ShippedQuantity")),
                RemainingQuantity = reader.GetDecimal(reader.GetOrdinal("RemainingQuantity")),
                IsShipped = reader.GetInt32(reader.GetOrdinal("IsShipped")) == 1,
                ShippedAt = ReadNullableDateTime(reader, "ShippedAt")
            });
        }

        return pallets;
    }

    // =====================================================
    // PRIVATE HELPERS
    // =====================================================

    private async Task MarkReadyForPickupCheckAsync(NordicBeesERPContext context, int orderId)
    {
        // Count unpacked lines
        var unpackedCount = await context.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM order_lines WHERE order_id = {0} AND packed_at IS NULL",
            orderId).FirstAsync();

        if (unpackedCount == 0)
        {
            // All lines packed — auto-transition to ready_for_pickup (only from confirmed or packing)
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE orders SET status = 'ready_for_pickup', updated_at = NOW() WHERE id = {0} AND status IN ('draft', 'packing', 'confirmed')",
                orderId);

            var orderNumber = await context.Database.SqlQueryRaw<string>(
                "SELECT order_number AS Value FROM orders WHERE id = {0}", orderId).FirstOrDefaultAsync();
            if (_telegram is not null)
                _ = _telegram.SendToGroupAsync("uzsakymai",
                    $"✅ Užsakymas supakuotas\n\n🔹 Užsakymo nr.: {orderNumber}");
        }
    }

    private async Task<string> GenerateNextOrderNumberAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var year = DateTime.Now.Year;
        var yearSuffix = (year % 100).ToString("D2");
        var prefix = "UZS" + yearSuffix;

        var conn = context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT MAX(order_number) AS LastNum FROM orders WHERE order_number LIKE @prefix";
        cmd.Parameters.Clear();
        var p = cmd.CreateParameter();
        p.ParameterName = "@prefix";
        p.Value = prefix + "%";
        cmd.Parameters.Add(p);

        var result = await cmd.ExecuteScalarAsync();
        var lastNumber = result == null || result == DBNull.Value ? null : result.ToString();

        int nextNumber = 1;
        if (!string.IsNullOrEmpty(lastNumber))
        {
            var numPart = lastNumber.Substring(prefix.Length);
            if (int.TryParse(numPart, out int lastNum))
                nextNumber = lastNum + 1;
        }

        return $"{prefix}{nextNumber:D3}";
    }

    // --- Reader helpers for nullable types ---

    private static DateTime? ReadNullableDateTime(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    private static int? ReadNullableInt(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static decimal? ReadNullableDecimal(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    }

    private static string? ReadNullableString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
