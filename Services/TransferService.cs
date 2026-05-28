using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models.WarehouseModule;

namespace NordicBeesERP.Services;

public class TransferService : ITransferService
{
    private readonly IDbContextFactory<NordicBeesERPContext> _contextFactory;

    public TransferService(IDbContextFactory<NordicBeesERPContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<string> TransferContainersAsync(List<int> containerIds, int fromWarehouseId, int toWarehouseId, string? notes)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            // Generuoti perkėlimo numerį
            var year = DateTime.Now.Year;
            var yearShort = DateTime.Now.ToString("yy");
            var month = DateTime.Now.ToString("MM");
            var prefix = $"PK-{yearShort}{month}-";

            var lastNumber = await context.StockMovements
                .Where(m => m.MovementType == "TRANSFER" && m.Notes != null && m.Notes.StartsWith(prefix))
                .OrderByDescending(m => m.Id)
                .Select(m => m.Notes)
                .FirstOrDefaultAsync();

            int nextCounter = 1;
            if (lastNumber != null)
            {
                var lastDash = lastNumber.LastIndexOf('-');
                if (lastDash >= 0 && int.TryParse(lastNumber.Substring(lastDash + 1), out int current))
                    nextCounter = current + 1;
            }
            var transferNumber = $"{prefix}{nextCounter:D3}";

            // Perkelti kiekvieną konteinerį
            var containers = await context.Containers
                .Where(c => containerIds.Contains(c.Id))
                .ToListAsync();

            foreach (var container in containers)
            {
                var oldWarehouseId = container.WarehouseId;
                container.WarehouseId = toWarehouseId;
                container.UpdatedAt = DateTime.Now;

                // Priverstinai pažymėti kaip pakeistą
                context.Entry(container).Property(x => x.WarehouseId).IsModified = true;
                context.Entry(container).Property(x => x.UpdatedAt).IsModified = true;

                context.StockMovements.Add(new StockMovement
                {
                    ContainerId = container.Id,
                    MovementType = "TRANSFER",
                    FromWarehouseId = oldWarehouseId,
                    ToWarehouseId = toWarehouseId,
                    Quantity = container.NetWeight,
                    ReferenceType = "Transfer",
                    Notes = transferNumber,
                    CreatedAt = DateTime.Now
                });
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            return transferNumber;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<StockMovement>> GetTransferHistoryAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.StockMovements
            .Where(m => m.MovementType == "TRANSFER")
            .OrderByDescending(m => m.CreatedAt)
            .Take(100)
            .ToListAsync();
    }
}