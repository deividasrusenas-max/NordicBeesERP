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
        return await context.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id);
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

        // Verify warehouse exists (NoTracking read)
        var existing = await context.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == warehouse.Id);
        if (existing == null)
            throw new InvalidOperationException($"Warehouse with id {warehouse.Id} not found");

        // Update via raw SQL — NoTracking makes Update + SaveChangesAsync silently persist 0 rows
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE warehouses SET code = {0}, name = {1}, warehouse_type_id = {2}, address = {3}, city = {4}, country = {5}, description = {6}, is_active = {7}, email = {8}, updated_at = NOW() WHERE id = {9}",
            warehouse.Code,
            warehouse.Name,
            warehouse.WarehouseTypeId,
            warehouse.Address,
            warehouse.City,
            warehouse.Country,
            warehouse.Description,
            warehouse.IsActive,
            warehouse.Email,
            warehouse.Id);
    }

    public async Task DeleteAsync(int id)
    {
        using var context = _dbContextFactory.CreateDbContext();
        var warehouseExists = await context.Warehouses.AsNoTracking().AnyAsync(w => w.Id == id);
        if (warehouseExists)
        {
            await context.Database.ExecuteSqlRawAsync("DELETE FROM warehouses WHERE id = {0}", id);
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
