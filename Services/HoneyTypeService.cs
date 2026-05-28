using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models;

namespace NordicBeesERP.Services;

public class HoneyTypeService : IHoneyTypeService
{
    private readonly IDbContextFactory<NordicBeesERPContext> _contextFactory;

    public HoneyTypeService(IDbContextFactory<NordicBeesERPContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<HoneyType>> GetAllAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.HoneyTypes.OrderBy(h => h.SortOrder).ThenBy(h => h.Name).ToListAsync();
    }

    public async Task<List<HoneyType>> GetActiveAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.HoneyTypes.Where(h => h.IsActive).OrderBy(h => h.SortOrder).ThenBy(h => h.Name).ToListAsync();
    }

    public async Task<HoneyType?> GetByIdAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.HoneyTypes.FindAsync(id);
    }

    public async Task<int> CreateAsync(HoneyType honeyType)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        honeyType.CreatedAt = DateTime.Now;
        honeyType.UpdatedAt = DateTime.Now;
        context.HoneyTypes.Add(honeyType);
        await context.SaveChangesAsync();
        return honeyType.Id;
    }

    public async Task UpdateAsync(HoneyType honeyType)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        honeyType.UpdatedAt = DateTime.Now;
        context.HoneyTypes.Update(honeyType);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var item = await context.HoneyTypes.FindAsync(id);
        if (item != null)
        {
            context.HoneyTypes.Remove(item);
            await context.SaveChangesAsync();
        }
    }
}