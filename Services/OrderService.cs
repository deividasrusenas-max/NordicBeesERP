using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models;
using System.Data;
using System.Data.Common;

namespace NordicBeesERP.Services;

public class OrderService : IOrderService
{
    private readonly IDbContextFactory<NordicBeesERPContext> _contextFactory;

    public OrderService(IDbContextFactory<NordicBeesERPContext> contextFactory)
    {
        _contextFactory = contextFactory;
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
                     created_at AS CreatedAt, updated_at AS UpdatedAt 
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

        // Generate order number if not provided
        if (string.IsNullOrWhiteSpace(order.OrderNumber))
            order.OrderNumber = await GenerateNextOrderNumberAsync();

        var status = string.IsNullOrEmpty(order.Status) ? "draft" : order.Status;

        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO orders (order_number, order_date, customer_id, delivery_date, status, notes, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, NOW(), NOW())",
            order.OrderNumber,
            order.OrderDate,
            order.CustomerId,
            (object?)order.DeliveryDate ?? DBNull.Value,
            status,
            (object?)order.Notes ?? DBNull.Value
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
                (object?)line.Price ?? DBNull.Value,
                (object?)line.Notes ?? DBNull.Value,
                (object?)line.LotNumber ?? DBNull.Value,
                (object?)line.ExpiryDate ?? DBNull.Value,
                (object?)line.PackedAt ?? DBNull.Value,
                (object?)line.PackedByUserId ?? DBNull.Value
            );
        }

        return newOrderId;
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

    public async Task MarkShippedAsync(int orderId, int userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        await context.Database.ExecuteSqlRawAsync(
            "UPDATE orders SET status = 'shipped', shipped_at = NOW(), shipped_by_user_id = {0} WHERE id = {1} AND status = 'ready_for_pickup'",
            userId, orderId);
    }

    public async Task LinkInvoiceAsync(int orderId, int invoiceId, int userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        await context.Database.ExecuteSqlRawAsync(
            "UPDATE orders SET invoice_id = {0}, invoiced_at = NOW(), invoiced_by_user_id = {1} WHERE id = {2}",
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

        var lastNumber = (string?)(await cmd.ExecuteScalarAsync());

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
