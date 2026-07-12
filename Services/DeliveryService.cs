using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models.WarehouseModule;

namespace NordicBeesERP.Services;

public class DeliveryService : IDeliveryService
{
    private readonly IDbContextFactory<NordicBeesERPContext> _contextFactory;

    public DeliveryService(IDbContextFactory<NordicBeesERPContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<Delivery>> GetAllAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var deliveries = await context.Deliveries
            .Select(d => new {
                d.Id,
                d.DeliveryNumber,
                d.DeliveryDate,
                d.SupplierId,
                d.WarehouseId,
                d.RawMaterialTypeId,
                d.Status,
                d.TotalNetWeight,
                d.TotalAmount,
                d.BarrelsOwed,
                d.BarrelsReturned,
                d.NeedReturnBarrels,
                d.Notes,
                d.CreatedAt,
                d.UpdatedAt,
                PaidAmount = context.SupplierPayments
                    .Where(p => p.DeliveryId == d.Id)
                    .Sum(p => p.Amount)
            })
            .OrderByDescending(d => d.DeliveryDate)
            .ToListAsync();

        // Map PaidAmount to Delivery objects
        var result = deliveries.Select(d => new Delivery {
            Id = d.Id,
            DeliveryNumber = d.DeliveryNumber,
            DeliveryDate = d.DeliveryDate,
            SupplierId = d.SupplierId,
            WarehouseId = d.WarehouseId,
            RawMaterialTypeId = d.RawMaterialTypeId,
            Status = d.Status,
            TotalNetWeight = d.TotalNetWeight,
            TotalAmount = d.TotalAmount,
            BarrelsOwed = d.BarrelsOwed,
            BarrelsReturned = d.BarrelsReturned,
            NeedReturnBarrels = d.NeedReturnBarrels,
            Notes = d.Notes,
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt,
            PaidAmount = d.PaidAmount
        }).ToList();

        return result;
    }

    public async Task<List<Delivery>> GetFilteredAsync(string? status, int? supplierId, DateTime? fromDate, DateTime? toDate)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Deliveries.AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(d => d.Status == status);
        if (supplierId.HasValue)
            query = query.Where(d => d.SupplierId == supplierId.Value);
        if (fromDate.HasValue)
            query = query.Where(d => d.DeliveryDate >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(d => d.DeliveryDate <= toDate.Value);

        return await query.OrderByDescending(d => d.DeliveryDate).ToListAsync();
    }

    public async Task<Delivery?> GetByIdAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Deliveries.FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<List<DeliveryLine>> GetLinesByDeliveryIdAsync(int deliveryId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.DeliveryLines.Where(l => l.DeliveryId == deliveryId).ToListAsync();
    }

    public async Task<List<Container>> GetContainersByDeliveryAsync(int deliveryId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var lineIds = await context.DeliveryLines
            .Where(l => l.DeliveryId == deliveryId)
            .Select(l => l.Id)
            .ToListAsync();
        return await context.Containers
            .Where(c => c.DeliveryLineId.HasValue && lineIds.Contains(c.DeliveryLineId.Value))
            .OrderBy(c => c.ContainerCode)
            .ToListAsync();
    }

    public async Task<int> CreateDeliveryWithContainersAsync(Delivery delivery, List<DeliveryLine> lines, List<Container> containers, int? operatorId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            // Generate delivery_number inside transaction with UNIQUE constraint + retry
            if (string.IsNullOrEmpty(delivery.DeliveryNumber))
            {
                var materialCode = delivery.RawMaterialTypeId.HasValue
                    ? (await context.RawMaterialTypes.FindAsync(delivery.RawMaterialTypeId.Value))?.Name?.Substring(0, 1).ToUpper() ?? "XX"
                    : "XX";
                delivery.DeliveryNumber = await GenerateDeliveryNumberInContextAsync(context, materialCode);
            }

            var now = DateTime.Now;

            // 1. Insert delivery header — ExecuteSqlRawAsync (NoTracking-safe)
            await context.Database.ExecuteSqlRawAsync(
                @"INSERT INTO deliveries 
                  (delivery_number, delivery_date, supplier_id, warehouse_id, raw_material_type_id, status,
                   total_net_weight, total_amount, paid_amount, barrels_owed, barrels_returned,
                   need_return_barrels, notes, invoice_id, invoice_number,
                   signed_by_type, receiver_name, origin_country,
                   weighing_status, weighing_station_id,
                   created_by_user_id, received_by_user_id,
                   created_at, updated_at)
                  VALUES ({0}, {1}, {2}, {3}, {4}, {5},
                          {6}, {7}, {8}, {9}, {10},
                          {11}, {12}, {13}, {14},
                          {15}, {16}, {17},
                          {18}, {19},
                          {20}, {21},
                          {22}, {23})",
                delivery.DeliveryNumber,
                delivery.DeliveryDate,
                delivery.SupplierId,
                delivery.WarehouseId,
                (object?)delivery.RawMaterialTypeId ?? DBNull.Value,
                delivery.Status,
                delivery.TotalNetWeight,
                delivery.TotalAmount,
                delivery.PaidAmount,
                delivery.BarrelsOwed,
                delivery.BarrelsReturned,
                delivery.NeedReturnBarrels,
                (object?)delivery.Notes ?? DBNull.Value,
                (object?)delivery.InvoiceId ?? DBNull.Value,
                (object?)delivery.InvoiceNumber ?? DBNull.Value,
                delivery.SignedByType,
                (object?)delivery.ReceiverName ?? DBNull.Value,
                (object?)delivery.OriginCountry ?? DBNull.Value,
                delivery.WeighingStatus,
                (object?)delivery.WeighingStationId ?? DBNull.Value,
                (object?)operatorId ?? DBNull.Value,
                (object?)operatorId ?? DBNull.Value,
                now,
                now);

            var deliveryId = await context.Deliveries
                .FromSqlRaw("SELECT * FROM deliveries WHERE id = LAST_INSERT_ID() LIMIT 1")
                .Select(d => d.Id)
                .FirstOrDefaultAsync();

            // 2. Insert delivery lines — ExecuteSqlRawAsync
            var lineIdMap = new Dictionary<int, int>(); // original index → DB id
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                await context.Database.ExecuteSqlRawAsync(
                    @"INSERT INTO delivery_lines
                      (delivery_id, product_id, honey_type_id, container_type, container_count,
                       total_gross_weight, total_tare_weight, total_net_weight,
                       unit_price, line_total, created_at, updated_at)
                      VALUES ({0}, {1}, {2}, {3}, {4},
                              {5}, {6}, {7},
                              {8}, {9}, {10}, {11})",
                    deliveryId,
                    (object?)line.ProductId ?? DBNull.Value,
                    (object?)line.HoneyTypeId ?? DBNull.Value,
                    line.ContainerType,
                    line.ContainerCount,
                    line.TotalNetWeight ?? 0m,
                    0m, // total_tare_weight — not on model, default 0
                    line.TotalNetWeight ?? 0m,
                    (object?)line.UnitPrice ?? DBNull.Value,
                    (object?)line.LineTotal ?? DBNull.Value,
                    now,
                    now);

                var lineId = await context.DeliveryLines
                    .FromSqlRaw("SELECT * FROM delivery_lines WHERE id = LAST_INSERT_ID() LIMIT 1")
                    .Select(l => l.Id)
                    .FirstOrDefaultAsync();
                lineIdMap[i] = lineId;
            }

            // 3. Insert containers with generated codes — ExecuteSqlRawAsync
            // Container codes: {DELIVERY_NUMBER}/{SEQ:D3} — generated ONLY here, never in UI
            var containerIdMap = new Dictionary<int, int>(); // original index → DB id
            int seq = 1;
            for (int c = 0; c < containers.Count; c++)
            {
                var container = containers[c];
                // Find matching line for this container
                var matchingLineIndex = -1;
                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];
                    if (container.ContainerType == line.ContainerType &&
                        (container.HoneyTypeId == line.HoneyTypeId || (container.HoneyTypeId == null && line.HoneyTypeId == null)) &&
                        (container.ProductId == line.ProductId || (container.ProductId == null && line.ProductId == null)))
                    {
                        matchingLineIndex = i;
                        break;
                    }
                }

                var lineId = matchingLineIndex >= 0 ? lineIdMap[matchingLineIndex] : (int?)null;
                var netWeight = container.GrossWeight - container.TareWeight;
                var containerCode = $"{delivery.DeliveryNumber}/{seq:D3}";

                await context.Database.ExecuteSqlRawAsync(
                    @"INSERT INTO containers
                      (container_code, container_type, supplier_id, delivery_line_id, warehouse_id,
                       product_id, honey_type_id, gross_weight, tare_weight, net_weight,
                       quantity, remaining_quantity, status,
                       weighing_mode, received_by_user_id, label_print_count,
                       created_at, updated_at)
                      VALUES ({0}, {1}, {2}, {3}, {4},
                              {5}, {6}, {7}, {8}, {9},
                              {10}, {11}, {12},
                              {13}, {14}, {15},
                              {16}, {17})",
                    containerCode,
                    container.ContainerType,
                    delivery.SupplierId,
                    (object?)lineId ?? DBNull.Value,
                    delivery.WarehouseId,
                    (object?)container.ProductId ?? DBNull.Value,
                    (object?)container.HoneyTypeId ?? DBNull.Value,
                    container.GrossWeight,
                    container.TareWeight,
                    netWeight,
                    container.Quantity,
                    container.RemainingQuantity,
                    container.Status,
                    container.WeighingMode,
                    (object?)operatorId ?? DBNull.Value,
                    0, // label_print_count
                    now,
                    now);

                var containerId = await context.Containers
                    .FromSqlRaw("SELECT * FROM containers WHERE id = LAST_INSERT_ID() LIMIT 1")
                    .Select(c => c.Id)
                    .FirstOrDefaultAsync();
                containerIdMap[c] = containerId;
                seq++;
            }

            // 4. Insert stock movements — ExecuteSqlRawAsync (CreatedBy = operatorId, BRC8 audit)
            for (int c = 0; c < containers.Count; c++)
            {
                var container = containers[c];
                var containerId = containerIdMap[c];
                var netQty = container.GrossWeight - container.TareWeight;

                await context.Database.ExecuteSqlRawAsync(
                    @"INSERT INTO stock_movements
                      (container_id, movement_type, to_warehouse_id, quantity,
                       reference_type, reference_id, created_by, created_at)
                      VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7})",
                    containerId,
                    "IN",
                    delivery.WarehouseId,
                    netQty,
                    "Delivery",
                    deliveryId,
                    (object?)operatorId ?? DBNull.Value,
                    now);
            }

            // 5. Update delivery totals — ExecuteSqlRawAsync
            var totalNetWeight = lines.Sum(l => l.TotalNetWeight ?? 0);
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE deliveries SET total_net_weight = {0}, updated_at = {1} WHERE id = {2}",
                totalNetWeight, now, deliveryId);

            await transaction.CommitAsync();
            return deliveryId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<string> GenerateDeliveryNumberInContextAsync(NordicBeesERPContext context, string materialCode)
    {
        var code = string.IsNullOrEmpty(materialCode) ? "XX" : materialCode;
        var prefix = $"PR-{code}{DateTime.Now:yyMM}-";

        var existing = await context.Deliveries
            .Where(d => d.DeliveryNumber != null && d.DeliveryNumber.StartsWith(prefix))
            .Select(d => d.DeliveryNumber!)
            .ToListAsync();

        int next = 1;
        while (existing.Contains($"{prefix}{next:D3}"))
            next++;

        return $"{prefix}{next:D3}";
    }

    public async Task UpdatePricesAsync(int deliveryId, List<DeliveryLine> updatedLines, int barrelsOwed)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            foreach (var line in updatedLines)
            {
                var existing = await context.DeliveryLines.FindAsync(line.Id);
                if (existing != null)
                {
                    existing.UnitPrice = line.UnitPrice;
                    existing.LineTotal = (existing.TotalNetWeight ?? 0m) * (line.UnitPrice ?? 0m);
                    existing.UpdatedAt = DateTime.Now;
                    context.Entry(existing).Property(x => x.UnitPrice).IsModified = true;
                    context.Entry(existing).Property(x => x.LineTotal).IsModified = true;
                    context.Entry(existing).Property(x => x.UpdatedAt).IsModified = true;
                }
            }
            
            // Perskaičiuojam TotalAmount iš updatedLines (kurios jau turi naują UnitPrice)
            var delivery = await context.Deliveries.FirstOrDefaultAsync(d => d.Id == deliveryId);
            if (delivery != null)
            {
                delivery.TotalAmount = updatedLines.Sum(l => (l.TotalNetWeight ?? 0m) * (l.UnitPrice ?? 0m));
                delivery.BarrelsOwed = barrelsOwed;
                delivery.Status = "RECEIVED";
                delivery.UpdatedAt = DateTime.Now;
                context.Entry(delivery).Property(d => d.TotalAmount).IsModified = true;
                context.Entry(delivery).Property(d => d.BarrelsOwed).IsModified = true;
                context.Entry(delivery).Property(d => d.Status).IsModified = true;
                context.Entry(delivery).Property(d => d.UpdatedAt).IsModified = true;
            }
            
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task RecalculateTotalsAsync(int deliveryId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var delivery = await context.Deliveries.FirstOrDefaultAsync(d => d.Id == deliveryId);
        if (delivery != null)
        {
            var lines = await context.DeliveryLines.Where(l => l.DeliveryId == deliveryId).ToListAsync();
            delivery.TotalNetWeight = lines.Sum(l => l.TotalNetWeight ?? 0);
            delivery.TotalAmount = lines.Sum(l => l.LineTotal ?? 0);
            delivery.UpdatedAt = DateTime.Now;
            await context.SaveChangesAsync();
        }
    }

    public async Task<string> GenerateDeliveryNumberAsync(string materialCode)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var code = string.IsNullOrEmpty(materialCode) ? "XX" : materialCode;
        var prefix = $"PR-{code}{DateTime.Now:yyMM}-";

        var existing = await context.Deliveries
            .Where(d => d.DeliveryNumber != null && d.DeliveryNumber.StartsWith(prefix))
            .Select(d => d.DeliveryNumber!)
            .ToListAsync();

        int next = 1;
        while (existing.Contains($"{prefix}{next:D3}"))
            next++;

        return $"{prefix}{next:D3}";
    }

    public async Task UpdateDeliveryStatusAsync(int deliveryId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var delivery = await context.Deliveries.FirstOrDefaultAsync(d => d.Id == deliveryId);
        if (delivery == null) return;
        var paid = await context.SupplierPayments
            .Where(p => p.DeliveryId == deliveryId)
            .SumAsync(p => p.Amount);
        if (delivery.TotalAmount > 0 && paid >= delivery.TotalAmount)
            delivery.Status = "PAID";
        else if (delivery.TotalAmount > 0 && paid > 0)
            delivery.Status = "PARTIAL_PAID";
        else if (delivery.TotalAmount > 0)
            delivery.Status = "PRICED";
        else
            delivery.Status = "RECEIVED";
        context.Entry(delivery).Property(d => d.Status).IsModified = true;
        await context.SaveChangesAsync();
    }

    public async Task<bool> SaveSignatureAsync(int deliveryId, string signerName, string signatureSvg)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE deliveries SET supplier_signer_name=@name, supplier_signature_svg=@svg, supplier_signed_at=@signed WHERE id=@id",
            new MySqlConnector.MySqlParameter("@name", signerName),
            new MySqlConnector.MySqlParameter("@svg", signatureSvg),
            new MySqlConnector.MySqlParameter("@signed", DateTime.Now),
            new MySqlConnector.MySqlParameter("@id", deliveryId));
        return true;
    }
}
