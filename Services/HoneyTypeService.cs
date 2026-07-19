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
        return await context.HoneyTypes.AsNoTracking().FirstOrDefaultAsync(h => h.Id == id);
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

        var existing = await context.HoneyTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == honeyType.Id);
        if (existing == null)
            throw new InvalidOperationException($"Honey type with ID {honeyType.Id} not found.");

        // Save via raw SQL (NoTracking — Update + SaveChanges would silently do nothing)
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE honey_types SET code = {0}, name = {1}, name_en = {2}, description = {3}, color = {4}, is_active = {5}, sort_order = {6}, updated_at = {7} WHERE id = {8}",
            honeyType.Code,
            honeyType.Name,
            honeyType.NameEn,
            honeyType.Description,
            honeyType.Color,
            honeyType.IsActive,
            honeyType.SortOrder,
            DateTime.UtcNow,
            honeyType.Id
        );
    }

    public async Task DeleteAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var item = await context.HoneyTypes.AsNoTracking().FirstOrDefaultAsync(h => h.Id == id);
        if (item != null)
        {
            await context.Database.ExecuteSqlRawAsync("DELETE FROM honey_types WHERE id = {0}", id);
        }
    }
}