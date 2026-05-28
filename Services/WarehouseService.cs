using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models;

namespace NordicBeesERP.Services;

public interface IWarehouseService
{
    Task<Warehouse?> GetAsync(int id);
    Task<IEnumerable<Warehouse>> GetAllAsync();
    Task CreateAsync(Warehouse warehouse);
    Task UpdateAsync(Warehouse warehouse);
    Task DeleteAsync(int id);
    Task<IEnumerable<WarehouseStock>> GetAllStocksAsync();
}

public class WarehouseService : IWarehouseService
{
    private readonly IDbContextFactory<NordicBeesERPContext> _dbContextFactory;

    public WarehouseService(IDbContextFactory<NordicBeesERPContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<Warehouse?> GetAsync(int id)
    {
        using var context = _dbContextFactory.CreateDbContext();
        return await context.Warehouses.FindAsync(id);
    }

    public async Task<IEnumerable<Warehouse>> GetAllAsync()
    {
        using var context = _dbContextFactory.CreateDbContext();
        return await context.Warehouses.ToListAsync();
    }

    public async Task CreateAsync(Warehouse warehouse)
    {
        using var context = _dbContextFactory.CreateDbContext();
        context.Warehouses.Add(warehouse);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Warehouse warehouse)
    {
        using var context = _dbContextFactory.CreateDbContext();
        context.Warehouses.Update(warehouse);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        using var context = _dbContextFactory.CreateDbContext();
        var warehouse = await context.Warehouses.FindAsync(id);
        if (warehouse != null)
        {
            context.Warehouses.Remove(warehouse);
            await context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<WarehouseStock>> GetAllStocksAsync()
    {
        using var context = _dbContextFactory.CreateDbContext();
        return await context.WarehouseStocks
            .Include(ws => ws.Warehouse)
            .Include(ws => ws.Product)
            .ToListAsync();
    }
}
