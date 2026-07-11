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
        using var transaction = await context.Database.BeginTransactionAsync();
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

            delivery.CreatedAt = DateTime.Now;
            delivery.UpdatedAt = DateTime.Now;
            delivery.CreatedByUserId = operatorId;
            context.Deliveries.Add(delivery);
            await context.SaveChangesAsync();

            foreach (var line in lines)
            {
                line.DeliveryId = delivery.Id;
                line.CreatedAt = DateTime.Now;
                context.DeliveryLines.Add(line);
            }
            await context.SaveChangesAsync();

            // Map containers to their delivery lines and assign codes inside transaction
            int seq = 1;
            foreach (var line in lines)
            {
                var lineContainers = containers.Where(c =>
                    c.ContainerType == line.ContainerType &&
                    c.HoneyTypeId == line.HoneyTypeId &&
                    c.ProductId == line.ProductId).ToList();

                foreach (var container in lineContainers)
                {
                    if (container.DeliveryLineId == 0 || container.DeliveryLineId == null)
                    {
                        container.DeliveryLineId = line.Id;
                        container.SupplierId = delivery.SupplierId;
                        container.WarehouseId = delivery.WarehouseId;
                        container.NetWeight = container.GrossWeight - container.TareWeight;
                        container.ContainerCode = $"{delivery.DeliveryNumber}/{seq:D3}";
                        container.ReceivedByUserId = operatorId;
                        container.CreatedAt = DateTime.Now;
                        container.UpdatedAt = DateTime.Now;
                        context.Containers.Add(container);
                        seq++;
                    }
                }
            }
            await context.SaveChangesAsync();

            // Create stock movements for each container (bug fix: CreatedBy = operatorId)
            foreach (var container in containers)
            {
                context.StockMovements.Add(new StockMovement
                {
                    ContainerId = container.Id,
                    MovementType = "IN",
                    ToWarehouseId = delivery.WarehouseId,
                    Quantity = container.GrossWeight - container.TareWeight,
                    ReferenceType = "Delivery",
                    ReferenceId = delivery.Id,
                    CreatedBy = operatorId,
                    CreatedAt = DateTime.Now
                });
            }
            await context.SaveChangesAsync();

            // Recalculate delivery totals
            delivery.TotalNetWeight = lines.Sum(l => l.TotalNetWeight ?? 0);
            context.Deliveries.Update(delivery);
            await context.SaveChangesAsync();

            await transaction.CommitAsync();
            return delivery.Id;
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
