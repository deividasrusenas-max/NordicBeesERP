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
        var query = context.Containers.AsQueryable();

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
            query = query.Where(c => c.ContainerCode.Contains(searchCode));

        return await query.OrderByDescending(c => c.CreatedAt).ToListAsync();
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
        "UPDATE containers SET status = {0}, updated_at = NOW() WHERE id = {1}", newStatus, id);
    }

    public async Task WriteOffAsync(List<int> containerIds, string reason, int? createdBy)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
        var containers = await context.Containers
            .Where(c => containerIds.Contains(c.Id))
            .ToListAsync();

        await context.Database.ExecuteSqlRawAsync(
            "UPDATE containers SET status = 'WRITTEN_OFF', updated_at = NOW() WHERE id IN ({0})",
            string.Join(",", containerIds));

        foreach (var container in containers)
        {
            context.StockMovements.Add(new StockMovement
            {
                ContainerId = container.Id,
                MovementType = "OUT",
                FromWarehouseId = container.WarehouseId,
                Quantity = container.NetWeight,
                ReferenceType = "Manual",
                Notes = reason,
                CreatedBy = createdBy,
                CreatedAt = DateTime.Now
            });
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
            
            foreach (var c in containers)
            {
                c.HoneyTypeId = honeyTypeId;
                c.UpdatedAt = DateTime.Now;
                context.Entry(c).Property(x => x.HoneyTypeId).IsModified = true;
                context.Entry(c).Property(x => x.UpdatedAt).IsModified = true;
            }
            await context.SaveChangesAsync();

            // Perskaido DeliveryLine pagal rūšis
            var deliveryLineIds = containers
                .Where(c => c.DeliveryLineId.HasValue)
                .Select(c => c.DeliveryLineId!.Value)
                .Distinct()
                .ToList();

            foreach (var lineId in deliveryLineIds)
            {
                var line = await context.DeliveryLines.FindAsync(lineId);
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
                    line.HoneyTypeId = groups.First().Key;
                    line.TotalNetWeight = lineContainers.Sum(c => c.NetWeight);
                    line.ContainerCount = lineContainers.Sum(c => c.Quantity);
                    context.Entry(line).Property(x => x.HoneyTypeId).IsModified = true;
                    context.Entry(line).Property(x => x.TotalNetWeight).IsModified = true;
                    context.Entry(line).Property(x => x.ContainerCount).IsModified = true;
                    continue;
                }

                // Kelios grupės — skaidome
                bool first = true;
                foreach (var group in groups)
                {
                    if (first)
                    {
                        // Pirmą grupę atnaujinam esamoje eilutėje
                        line.HoneyTypeId = group.Key;
                        line.ContainerCount = group.Sum(c => c.Quantity);
                        line.TotalNetWeight = group.Sum(c => c.NetWeight);
                        line.UpdatedAt = DateTime.Now;
                        context.Entry(line).Property(x => x.HoneyTypeId).IsModified = true;
                        context.Entry(line).Property(x => x.ContainerCount).IsModified = true;
                        context.Entry(line).Property(x => x.TotalNetWeight).IsModified = true;
                        context.Entry(line).Property(x => x.UpdatedAt).IsModified = true;
                        first = false;
                    }
                    else
                    {
                        // Kitas grupes — naujos eilutės
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

                        // Perkelt konteinerius į naują eilutę
                        foreach (var c in group)
                        {
                            c.DeliveryLineId = newLine.Id;
                            context.Entry(c).Property(x => x.DeliveryLineId).IsModified = true;
                        }
                    }
                }
            }

            await context.SaveChangesAsync();

            // Perskaičiuoti delivery totals
            var deliveryIds = containers
                .Where(c => c.DeliveryLineId.HasValue)
                .Select(c => context.DeliveryLines.Find(c.DeliveryLineId!.Value)?.DeliveryId ?? 0)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            foreach (var deliveryId in deliveryIds)
            {
                var delivery = await context.Deliveries.FindAsync(deliveryId);
                if (delivery != null)
                {
                    var allLines = await context.DeliveryLines.Where(l => l.DeliveryId == deliveryId).ToListAsync();
                    delivery.TotalNetWeight = allLines.Sum(l => l.TotalNetWeight ?? 0);
                    delivery.TotalAmount = allLines.Sum(l => l.LineTotal ?? 0);
                    delivery.UpdatedAt = DateTime.Now;
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

    public async Task SaveWeightCorrectionAsync(int containerId, decimal oldGross, decimal newGross,
        decimal oldTare, decimal newTare, decimal oldNet, decimal newNet,
        string reason, int correctedBy)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            // 1. Insert audit record into container_weight_corrections
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO container_weight_corrections (container_id, old_gross_weight, new_gross_weight, old_tare_weight, new_tare_weight, old_net_weight, new_net_weight, reason, corrected_by, corrected_at) VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, NOW())",
                containerId, oldGross, newGross, oldTare, newTare, oldNet, newNet, reason, correctedBy);

            // 2. Update container weights
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE containers SET gross_weight = {0}, tare_weight = {1}, net_weight = {2}, updated_at = NOW() WHERE id = {3}",
                newGross, newTare, newNet, containerId);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
