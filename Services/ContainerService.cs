using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models.WarehouseModule;

namespace NordicBeesERP.Services;

public class ContainerService : IContainerService
{
    private readonly IDbContextFactory<NordicBeesERPContext> _contextFactory;

    public ContainerService(IDbContextFactory<NordicBeesERPContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<Container>> GetByWarehouseAsync(int warehouseId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Containers
            .Where(c => c.WarehouseId == warehouseId && c.Status != "WRITTEN_OFF" && c.Status != "SOLD")
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Container>> GetFilteredAsync(int? warehouseId, int? honeyTypeId, int? supplierId, string? status, string? searchCode)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Containers.AsNoTracking().AsQueryable();

        if (warehouseId.HasValue)
            query = query.Where(c => c.WarehouseId == warehouseId.Value);
        if (honeyTypeId.HasValue)
            query = query.Where(c => c.HoneyTypeId == honeyTypeId.Value);
        if (supplierId.HasValue)
            query = query.Where(c => c.SupplierId == supplierId.Value);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(c => c.Status == status);
        else
            query = query.Where(c => c.Status != "WRITTEN_OFF" && c.Status != "SOLD");

        if (!string.IsNullOrEmpty(searchCode))
        {
            var pattern = $"%{searchCode}%";
            query = query.Where(c =>
                EF.Functions.Like(c.ContainerCode, pattern)
                || context.BusinessPartners.Any(s => s.Id == c.SupplierId && EF.Functions.Like(s.Name, pattern))
                || (c.HoneyTypeId.HasValue && context.HoneyTypes.Any(h => h.Id == c.HoneyTypeId!.Value && EF.Functions.Like(h.Name, pattern)))
                || context.Warehouses.Any(w => w.Id == c.WarehouseId && EF.Functions.Like(w.Name, pattern)));
        }

        return await query.OrderByDescending(c => c.CreatedAt).ToListAsync();
    }

    public async Task<string> GenerateNextContainerCodeAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        // Race-safe when the caller wraps this + the subsequent insert in a
        // transaction on the same connection (e.g. CreateBatchAsync pattern):
        // the MAX is read inside that transaction, so concurrent inserts cannot
        // slip between the read and the write.
        var maxSeq = await context.Database
            .SqlQueryRaw<int>("SELECT COALESCE(MAX(CAST(SUBSTRING(container_code, 3) AS UNSIGNED)), 0) FROM containers WHERE container_code LIKE 'TP%'")
            .FirstOrDefaultAsync();

        return "TP" + (maxSeq + 1).ToString("D6");
    }

    public async Task<Container?> GetByIdAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Containers.FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Container?> GetByCodeAsync(string containerCode)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Containers.FirstOrDefaultAsync(c => c.ContainerCode == containerCode);
    }

    public async Task<int> CreateAsync(Container container)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        container.CreatedAt = DateTime.Now;
        container.UpdatedAt = DateTime.Now;
        container.NetWeight = container.GrossWeight - container.TareWeight;
        context.Containers.Add(container);
        await context.SaveChangesAsync();
        return container.Id;
    }

    public async Task<List<int>> CreateBatchAsync(List<Container> containers)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var ids = new List<int>();
            foreach (var container in containers)
            {
                container.CreatedAt = DateTime.Now;
                container.UpdatedAt = DateTime.Now;
                container.NetWeight = container.GrossWeight - container.TareWeight;
                context.Containers.Add(container);
                await context.SaveChangesAsync();
                ids.Add(container.Id);
            }
            await transaction.CommitAsync();
            return ids;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task UpdateStatusAsync(int id, string newStatus)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE containers SET status = {0}, updated_at = NOW() WHERE id = {1}",
            newStatus, id);
    }

    public async Task WriteOffAsync(List<int> containerIds, string reason, int? createdBy)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            foreach (var containerId in containerIds)
            {
                var container = await context.Containers.FirstOrDefaultAsync(c => c.Id == containerId);
                if (container != null)
                {
                    await context.Database.ExecuteSqlRawAsync(
                        "UPDATE containers SET status = {0}, updated_at = NOW() WHERE id = {1}",
                        "WRITTEN_OFF", container.Id);

                    context.StockMovements.Add(new StockMovement
                    {
                        ContainerId = containerId,
                        MovementType = "OUT",
                        FromWarehouseId = container.WarehouseId,
                        Quantity = container.NetWeight,
                        ReferenceType = "Manual",
                        Notes = reason,
                        CreatedBy = createdBy,
                        CreatedAt = DateTime.Now
                    });
                }
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

    public async Task<int> GetCountByWarehouseAsync(int? warehouseId, string? status)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Containers.AsQueryable();
        if (warehouseId.HasValue)
            query = query.Where(c => c.WarehouseId == warehouseId.Value);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(c => c.Status == status);
        else
            query = query.Where(c => c.Status != "WRITTEN_OFF" && c.Status != "SOLD");
        return await query.CountAsync();
    }

    public async Task<decimal> GetTotalNetWeightAsync(int? warehouseId, string? status)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Containers.AsQueryable();
        if (warehouseId.HasValue)
            query = query.Where(c => c.WarehouseId == warehouseId.Value);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(c => c.Status == status);
        else
            query = query.Where(c => c.Status != "WRITTEN_OFF" && c.Status != "SOLD");
        return await query.SumAsync(c => c.NetWeight);
    }

    public async Task<string?> GetLastContainerCodeAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var last = await context.Containers
            .Where(c => c.ContainerType == "BARREL")
            .OrderByDescending(c => c.Id)
            .Select(c => c.ContainerCode)
            .FirstOrDefaultAsync();
        return last;
    }

    public async Task<string?> GetLastBucketCodeAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var last = await context.Containers
            .Where(c => c.ContainerType == "BUCKET_GROUP")
            .OrderByDescending(c => c.Id)
            .Select(c => c.ContainerCode)
            .FirstOrDefaultAsync();
        return last;
    }

    public async Task<List<Container>> GetByIdsAsync(List<int> ids)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Containers.Where(c => ids.Contains(c.Id)).ToListAsync();
    }

    public async Task UpdateHoneyTypeAsync(List<int> containerIds, int honeyTypeId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var containers = await context.Containers.Where(c => containerIds.Contains(c.Id)).ToListAsync();

            // 1. Update container honey_type_id via raw SQL
            foreach (var c in containers)
            {
                await context.Database.ExecuteSqlRawAsync(
                    "UPDATE containers SET honey_type_id = {0}, updated_at = NOW() WHERE id = {1}",
                    honeyTypeId, c.Id);
            }

            // Perskaido DeliveryLine pagal rūšis
            var deliveryLineIds = containers
                .Where(c => c.DeliveryLineId.HasValue)
                .Select(c => c.DeliveryLineId!.Value)
                .Distinct()
                .ToList();

            foreach (var lineId in deliveryLineIds)
            {
                var line = await context.DeliveryLines.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lineId);
                if (line == null) continue;

                // Gauti visus šios eilutės konteinerius
                var lineContainers = await context.Containers
                    .Where(c => c.DeliveryLineId == lineId)
                    .ToListAsync();

                // Grupuoti pagal HoneyTypeId
                var groups = lineContainers.GroupBy(c => c.HoneyTypeId).ToList();

                // Jei tik viena grupė — nereikia skaidyti
                if (groups.Count <= 1)
                {
                    await context.Database.ExecuteSqlRawAsync(
                        "UPDATE delivery_lines SET honey_type_id = {0}, total_net_weight = {1}, container_count = {2}, updated_at = NOW() WHERE id = {3}",
                        groups.First().Key,
                        lineContainers.Sum(c => c.NetWeight),
                        lineContainers.Sum(c => c.Quantity),
                        lineId);
                    continue;
                }

                // Kelios grupės — skaidome
                bool first = true;
                foreach (var group in groups)
                {
                    if (first)
                    {
                        // Pirmą grupę atnaujinam esamoje eilutėje
                        await context.Database.ExecuteSqlRawAsync(
                            "UPDATE delivery_lines SET honey_type_id = {0}, container_count = {1}, total_net_weight = {2}, updated_at = NOW() WHERE id = {3}",
                            group.Key,
                            group.Sum(c => c.Quantity),
                            group.Sum(c => c.NetWeight),
                            lineId);
                        first = false;
                    }
                    else
                    {
                        // Kitas grupes — naujos eilutės (genuine INSERT — SaveChangesAsync is correct here)
                        var newLine = new DeliveryLine
                        {
                            DeliveryId = line.DeliveryId,
                            ProductId = line.ProductId,
                            HoneyTypeId = group.Key,
                            ContainerType = line.ContainerType,
                            ContainerCount = group.Sum(c => c.Quantity),
                            TotalNetWeight = group.Sum(c => c.NetWeight),
                            UnitPrice = line.UnitPrice,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        };
                        context.DeliveryLines.Add(newLine);
                        await context.SaveChangesAsync();

                        // Perkelt konteinerius į naują eilutę via raw SQL
                        foreach (var c in group)
                        {
                            await context.Database.ExecuteSqlRawAsync(
                                "UPDATE containers SET delivery_line_id = {0}, updated_at = NOW() WHERE id = {1}",
                                newLine.Id, c.Id);
                        }
                    }
                }
            }

            // Perskaičiuoti delivery totals
            var deliveryIds = containers
                .Where(c => c.DeliveryLineId.HasValue)
                .Select(c => c.DeliveryLineId!.Value)
                .Distinct()
                .ToList();

            var deliveryLineMap = await context.DeliveryLines
                .Where(dl => deliveryIds.Contains(dl.Id))
                .Select(dl => new { dl.Id, dl.DeliveryId })
                .ToListAsync();

            var uniqueDeliveryIds = deliveryLineMap
                .Select(dl => dl.DeliveryId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            foreach (var deliveryId in uniqueDeliveryIds)
            {
                var allLines = await context.DeliveryLines
                    .Where(l => l.DeliveryId == deliveryId)
                    .ToListAsync();

                await context.Database.ExecuteSqlRawAsync(
                    "UPDATE deliveries SET total_net_weight = {0}, total_amount = {1}, updated_at = NOW() WHERE id = {2}",
                    allLines.Sum(l => l.TotalNetWeight ?? 0),
                    allLines.Sum(l => l.LineTotal ?? 0),
                    deliveryId);
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
