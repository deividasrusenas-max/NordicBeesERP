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
            .SqlQueryRaw<int>("SELECT COALESCE(MAX(CAST(SUBSTRING(container_code, 3) AS UNSIGNED)), 0) AS Value FROM containers WHERE container_code LIKE 'TP%'")
            .FirstOrDefaultAsync();

        return "TP" + (maxSeq + 1).ToString("D6");
    }

    public async Task<bool> IsAutoNumberingEnabledAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var raw = await context.AppSettings
            .Where(s => s.SettingKey == "AutoBarrelNumbering")
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync();
        return bool.TryParse(raw, out var enabled) ? enabled : raw == "1";
    }

    public async Task<List<string>> FindExistingCodesAsync(IEnumerable<string> codes)
    {
        var list = codes.Distinct().ToList();
        if (list.Count == 0) return new List<string>();
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Containers
            .Where(c => list.Contains(c.ContainerCode))
            .Select(c => c.ContainerCode)
            .ToListAsync();
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
            var containers = await context.Containers.AsNoTracking()
                .Where(c => containerIds.Contains(c.Id))
                .ToListAsync();

            // Only stock that is still there can be written off: a second write-off would add a second OUT
            // movement (balance goes negative). Nothing is written when any container is not IN_STOCK.
            var notInStock = containers.Where(c => c.Status != "IN_STOCK").Select(c => c.ContainerCode).OrderBy(c => c).ToList();
            if (notInStock.Count > 0)
                throw new InvalidOperationException($"Nurašyti galima tik sandėlyje esančius konteinerius. Netinka: {string.Join(", ", notInStock)}");

            foreach (var container in containers)
            {
                // the reason is appended - earlier notes on the container are kept
                await context.Database.ExecuteSqlRawAsync(
                    "UPDATE containers SET status = {0}, notes = CONCAT_WS(' | ', NULLIF(notes, ''), {1}), updated_at = NOW() WHERE id = {2} AND status = 'IN_STOCK'",
                    "WRITTEN_OFF", reason, container.Id);

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
            .Where(c => c.ContainerType == "BUCKET_GROUP" || c.ContainerType == "BUCKET")
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

            // Re-typing stock that already left (written off / sold / transferred) would silently rewrite a closed line.
            var gone = containers.Where(c => c.Status is "WRITTEN_OFF" or "SOLD" or "TRANSFERRED").Select(c => c.ContainerCode).OrderBy(c => c).ToList();
            if (gone.Count > 0)
                throw new InvalidOperationException($"Medaus rūšies keisti negalima nebesančiam sandėlyje konteineriui: {string.Join(", ", gone)}");

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

            // line weight, line_total (net x unit_price for every line, incl. the split-off ones), delivery totals, status
            foreach (var deliveryId in uniqueDeliveryIds)
                await DeliveryTotals.RecalculateAsync(context, deliveryId);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
