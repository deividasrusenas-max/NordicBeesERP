using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Helpers;
using NordicBeesERP.Models.WarehouseModule;

namespace NordicBeesERP.Services;

public class DeliveryService : IDeliveryService
{
    private readonly IDbContextFactory<NordicBeesERPContext> _contextFactory;
    private readonly IBucketTareService _bucketTare;

    public DeliveryService(IDbContextFactory<NordicBeesERPContext> contextFactory, IBucketTareService? bucketTare = null)
    {
        _contextFactory = contextFactory;
        _bucketTare = bucketTare ?? new BucketTareService(contextFactory);
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
                d.InvoiceId,
                d.InvoiceNumber,
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
            InvoiceId = d.InvoiceId,
            InvoiceNumber = d.InvoiceNumber,
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt,
            PaidAmount = d.PaidAmount
        }).ToList();

        await DeliveryDebt.ApplyAsync(context, result);
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

        var deliveries = await query.OrderByDescending(d => d.DeliveryDate).ToListAsync();

        // deliveries.paid_amount is never written by the app: the paid amount is always derived from supplier_payments
        // (same source as GetAllAsync), otherwise fully paid suppliers show as full debt.
        var ids = deliveries.Select(d => d.Id).ToList();
        if (ids.Count > 0)
        {
            var paidByDelivery = await context.SupplierPayments
                .Where(p => ids.Contains(p.DeliveryId))
                .GroupBy(p => p.DeliveryId)
                .Select(g => new { DeliveryId = g.Key, Paid = g.Sum(p => p.Amount) })
                .ToDictionaryAsync(x => x.DeliveryId, x => x.Paid);
            foreach (var d in deliveries)
                d.PaidAmount = paidByDelivery.TryGetValue(d.Id, out var paid) ? paid : 0m;
        }
        await DeliveryDebt.ApplyAsync(context, deliveries);

        return deliveries;
    }

    public async Task<Delivery?> GetByIdAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var delivery = await context.Deliveries.FirstOrDefaultAsync(d => d.Id == id);
        if (delivery != null) await DeliveryDebt.ApplyAsync(context, new[] { delivery });
        return delivery;
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

    private static bool IsBucket(string? containerType) => containerType is "BUCKET" or "BUCKET_GROUP";

    /// <summary>
    /// Server-side truth for bucket weights: honey buckets get their tare from the configured intervals unless the
    /// operator overrode it (TareManual); netto is always recomputed from brutto and tare. Never trusts client values.
    /// </summary>
    private async Task ApplyBucketWeightsAsync(IDbContextFactory<NordicBeesERPContext> factory, Delivery delivery, List<Container> containers)
    {
        foreach (var c in containers)
            c.NetWeight = c.GrossWeight - c.TareWeight;

        var buckets = containers.Where(c => IsBucket(c.ContainerType)).ToList();
        if (buckets.Count == 0) return;

        var isHoney = true;
        if (delivery.RawMaterialTypeId.HasValue)
        {
            using var ctx = await factory.CreateDbContextAsync();
            isHoney = await ctx.RawMaterialTypes
                .Where(r => r.Id == delivery.RawMaterialTypeId.Value)
                .Select(r => r.IsHoney)
                .FirstOrDefaultAsync();
        }
        // Non-honey packages (bee bread, propolis, ...) keep the operator-entered tare.
        if (!isHoney) return;

        foreach (var c in buckets)
        {
            if (c.GrossWeight <= 0)
                throw new BucketTareException($"Kibiras {c.ContainerCode}: brutto svoris turi būti didesnis už 0.");

            if (c.TareManual)
            {
                if (c.TareWeight <= 0 || c.TareWeight >= c.GrossWeight)
                    throw new BucketTareException($"Kibiras {c.ContainerCode}: rankinė tara turi būti didesnė už 0 ir mažesnė už brutto.");
            }
            else
            {
                var result = await _bucketTare.GetTareAsync(c.GrossWeight);
                if (result.IsOutOfRange)
                    throw new BucketTareException($"Kibiras {c.ContainerCode}: brutto {c.GrossWeight:0.##} kg už intervalų ribų, įveskite tarą ranka.");
                c.TareWeight = result.Tare!.Value;
            }
            c.NetWeight = BucketTareCalculator.Netto(c.GrossWeight, c.TareWeight);
        }
    }

    // NetWeight is always set by ApplyBucketWeightsAsync (never taken from the client).
    private static decimal NetOf(Container c) => c.NetWeight;

    /// <summary>Rejects the whole batch (before anything is written) when a code repeats or already exists.</summary>
    private async Task EnsureCodesAreFreeAsync(List<Container> containers)
    {
        var codes = containers.Select(c => c.ContainerCode).ToList();
        var duplicates = codes.GroupBy(c => c).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

        using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await context.Containers
            .Where(c => codes.Contains(c.ContainerCode))
            .Select(c => c.ContainerCode)
            .ToListAsync();

        var conflicts = duplicates.Union(existing).OrderBy(c => c, StringComparer.Ordinal).ToList();
        if (conflicts.Count > 0) throw new ContainerCodeConflictException(conflicts);
    }

    public async Task<int> CreateDeliveryWithContainersAsync(Delivery delivery, List<DeliveryLine> lines, List<Container> containers)
    {
        await EnsureCodesAreFreeAsync(containers);
        await ApplyBucketWeightsAsync(_contextFactory, delivery, containers);
        foreach (var line in lines)
        {
            var lineContainers = containers.Where(c =>
                c.ContainerType == line.ContainerType &&
                c.HoneyTypeId == line.HoneyTypeId &&
                c.ProductId == line.ProductId).ToList();
            if (lineContainers.Count > 0)
                line.TotalNetWeight = lineContainers.Sum(NetOf);
        }

        using var context = await _contextFactory.CreateDbContextAsync();
        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            delivery.CreatedAt = DateTime.Now;
            delivery.UpdatedAt = DateTime.Now;
            context.Deliveries.Add(delivery);
            await context.SaveChangesAsync();

            foreach (var line in lines)
            {
                line.DeliveryId = delivery.Id;
                line.CreatedAt = DateTime.Now;
                context.DeliveryLines.Add(line);
            }
            await context.SaveChangesAsync();

            // Map containers to their delivery lines
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
                        container.NetWeight = NetOf(container);
                        container.CreatedAt = DateTime.Now;
                        container.UpdatedAt = DateTime.Now;
                        context.Containers.Add(container);
                    }
                }
            }
            await context.SaveChangesAsync();

            // Create stock movements for each container
            foreach (var container in containers)
            {
                context.StockMovements.Add(new StockMovement
                {
                    ContainerId = container.Id,
                    MovementType = "IN",
                    ToWarehouseId = delivery.WarehouseId,
                    Quantity = NetOf(container),
                    ReferenceType = "Delivery",
                    ReferenceId = delivery.Id,
                    CreatedBy = null,
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

    public async Task UpdatePricesAsync(int deliveryId, List<DeliveryLine> updatedLines, int barrelsOwed)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var now = DateTime.Now;

            var ownLineIds = await context.DeliveryLines.AsNoTracking()
                .Where(l => l.DeliveryId == deliveryId)
                .Select(l => l.Id)
                .ToListAsync();

            foreach (var line in updatedLines)
            {
                if (!ownLineIds.Contains(line.Id))
                    throw new InvalidOperationException("Eilutė nepriklauso šiam pristatymui.");

                var unitPrice = line.UnitPrice ?? 0m;
                if (unitPrice < 0)
                    throw new InvalidOperationException("Kaina negali būti neigiama.");

                // Only the unit price comes from the page. The weight is read from the database row in the same
                // statement, so a page loaded before a weight edit can never write a stale line_total.
                await context.Database.ExecuteSqlRawAsync(
                    "UPDATE delivery_lines SET unit_price = {0}, line_total = COALESCE(total_net_weight, 0) * {0}, updated_at = {1} WHERE id = {2} AND delivery_id = {3}",
                    unitPrice, now, line.Id, deliveryId);
            }

            await context.Database.ExecuteSqlRawAsync(
                "UPDATE deliveries SET barrels_owed = {0}, updated_at = {1} WHERE id = {2}",
                barrelsOwed, now, deliveryId);

            // total_amount from the lines and the status from the payments - never a literal status
            // (a priced / partly paid / paid delivery must not fall back to RECEIVED).
            await DeliveryTotals.RecalculateHeaderAsync(context, deliveryId);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>Largest believable barrel net weight (kg). Anything above is a typo (e.g. 30880 for 308,80). Awaiting owner confirmation.</summary>
    public const decimal MaxBarrelNetKg = 400m;

    public async Task UpdateContainerWeightAsync(int containerId, decimal grossWeight, decimal tareWeight, bool tareManual)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var container = await context.Containers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == containerId)
            ?? throw new InvalidOperationException("Konteineris nerastas.");
        if (container.Status != "IN_STOCK")
            throw new InvalidOperationException($"Konteinerio {container.ContainerCode} svorio keisti negalima – jo statusas {container.Status}, o ne IN_STOCK.");
        if (!container.DeliveryLineId.HasValue)
            throw new InvalidOperationException($"Konteineris {container.ContainerCode} nepriskirtas pristatymo eilutei.");

        var line = await context.DeliveryLines.AsNoTracking().FirstOrDefaultAsync(l => l.Id == container.DeliveryLineId.Value)
            ?? throw new InvalidOperationException("Pristatymo eilutė nerasta.");
        var delivery = await context.Deliveries.AsNoTracking().FirstOrDefaultAsync(d => d.Id == line.DeliveryId)
            ?? throw new InvalidOperationException("Pristatymas nerastas.");

        // A supplier-signed act must keep matching the data it was signed on (owner decision pending: block vs re-sign).
        if (delivery.SupplierSignedAt.HasValue)
            throw new InvalidOperationException($"Pristatymo aktas {delivery.DeliveryNumber} pasirašytas tiekėjo – svorių keisti negalima.");

        if (grossWeight <= 0)
            throw new InvalidOperationException($"Konteineris {container.ContainerCode}: brutto svoris turi būti didesnis už 0.");
        if (tareWeight < 0)
            throw new InvalidOperationException($"Konteineris {container.ContainerCode}: tara negali būti neigiama.");
        if (tareWeight >= grossWeight)
            throw new InvalidOperationException($"Konteineris {container.ContainerCode}: tara turi būti mažesnė už brutto.");

        // Same server-side rules as on creation (bucket tare intervals / manual tare); barrels: net = gross - tare.
        var candidate = new Container
        {
            Id = container.Id,
            ContainerCode = container.ContainerCode,
            ContainerType = container.ContainerType,
            GrossWeight = grossWeight,
            TareWeight = tareWeight,
            TareManual = tareManual
        };
        await ApplyBucketWeightsAsync(_contextFactory, delivery, new List<Container> { candidate });

        if (candidate.NetWeight <= 0)
            throw new InvalidOperationException($"Konteineris {container.ContainerCode}: neto svoris turi būti didesnis už 0.");
        if (container.ContainerType == "BARREL" && candidate.NetWeight > MaxBarrelNetKg)
            throw new InvalidOperationException($"Konteineris {container.ContainerCode}: neto {candidate.NetWeight:0.##} kg viršija statinės ribą ({MaxBarrelNetKg:0} kg) – patikrinkite kablelį.");

        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            // status = 'IN_STOCK' in the WHERE closes the race with a concurrent write-off (no row count check:
            // with UseAffectedRows an identical re-save would report 0 rows and fail wrongly).
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE containers SET gross_weight = {0}, tare_weight = {1}, tare_manual = {2}, net_weight = {3}, updated_at = NOW() WHERE id = {4} AND status = 'IN_STOCK'",
                candidate.GrossWeight, candidate.TareWeight, candidate.TareManual, candidate.NetWeight, containerId);

            await context.Database.ExecuteSqlRawAsync(
                "UPDATE stock_movements SET quantity = {0} WHERE container_id = {1} AND movement_type = 'IN' AND reference_type = 'Delivery'",
                candidate.NetWeight, containerId);

            // The cached receipt PDF shows the old weights; with the path cleared it is regenerated on the next download.
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE deliveries SET receipt_pdf_path = NULL WHERE id = {0}", delivery.Id);

            await DeliveryTotals.RecalculateAsync(context, delivery.Id);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task UpdateBarrelsAsync(int deliveryId, bool needReturnBarrels, int barrelsOwed, int barrelsReturned)
    {
        if (barrelsOwed < 0 || barrelsReturned < 0)
            throw new InvalidOperationException("Statinių skaičius negali būti neigiamas.");

        using var context = await _contextFactory.CreateDbContextAsync();
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE deliveries SET need_return_barrels = {0}, barrels_owed = {1}, barrels_returned = {2}, updated_at = NOW() WHERE id = {3}",
            needReturnBarrels, barrelsOwed, barrelsReturned, deliveryId);
    }

    public async Task RecalculateTotalsAsync(int deliveryId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            await DeliveryTotals.RecalculateAsync(context, deliveryId);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
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
        await DeliveryTotals.RefreshStatusAsync(context, deliveryId);
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

    public async Task<List<Delivery>> SearchDeliveriesAsync(string searchTerm, int limit = 20)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        // deliveries is tiny (~3 rows) and its collation is accent-sensitive, so server-side
        // LIKE cannot do diacritic-insensitive matching — materialize and fold in memory
        // (same pattern as CustomerService.SearchBusinessPartnersAsync).
        var deliveries = await context.Deliveries.AsNoTracking()
            .OrderByDescending(d => d.DeliveryDate)
            .ToListAsync();

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return deliveries.Take(limit).ToList();
        }

        var foldedTerm = DiacriticHelper.Fold(searchTerm.Trim());

        var supplierNames = await context.BusinessPartners.AsNoTracking()
            .ToDictionaryAsync(p => p.Id, p => p.Name ?? string.Empty);

        return deliveries
            .Where(d =>
                DiacriticHelper.Fold(d.DeliveryNumber ?? string.Empty).Contains(foldedTerm) ||
                (supplierNames.TryGetValue(d.SupplierId, out var supplierName) &&
                 DiacriticHelper.Fold(supplierName).Contains(foldedTerm)))
            .Take(limit)
            .ToList();
    }
}
