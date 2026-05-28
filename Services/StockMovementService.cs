using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models.WarehouseModule;

namespace NordicBeesERP.Services;

public class StockMovementService : IStockMovementService
{
    private readonly IDbContextFactory<NordicBeesERPContext> _contextFactory;

    public StockMovementService(IDbContextFactory<NordicBeesERPContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<StockMovement>> GetByContainerAsync(int containerId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.StockMovements
            .Where(m => m.ContainerId == containerId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<StockMovement>> GetFilteredAsync(int? warehouseId, string? movementType, DateTime? fromDate, DateTime? toDate)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.StockMovements.AsQueryable();

        if (warehouseId.HasValue)
            query = query.Where(m => m.FromWarehouseId == warehouseId.Value || m.ToWarehouseId == warehouseId.Value);
        if (!string.IsNullOrEmpty(movementType))
            query = query.Where(m => m.MovementType == movementType);
        if (fromDate.HasValue)
            query = query.Where(m => m.CreatedAt >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(m => m.CreatedAt <= toDate.Value);

        return await query.OrderByDescending(m => m.CreatedAt).ToListAsync();
    }

    public async Task CreateMovementAsync(StockMovement movement)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        movement.CreatedAt = DateTime.Now;
        context.StockMovements.Add(movement);
        await context.SaveChangesAsync();
    }
}