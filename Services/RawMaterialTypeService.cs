using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models.WarehouseModule;
namespace NordicBeesERP.Services;
public class RawMaterialTypeService : IRawMaterialTypeService
{
    private readonly IDbContextFactory<NordicBeesERPContext> _contextFactory;
    public RawMaterialTypeService(IDbContextFactory<NordicBeesERPContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }
    public async Task<List<RawMaterialType>> GetActiveAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.RawMaterialTypes
            .Where(r => r.IsActive)
            .OrderBy(r => r.SortOrder)
            .Select(r => new RawMaterialType
            {
                Id = r.Id,
                Name = r.Name,
                Code = r.Code ?? (r.IsHoney ? "MD" : "ZM"),
                IsHoney = r.IsHoney,
                IsActive = r.IsActive,
                SortOrder = r.SortOrder,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            })
            .ToListAsync();
    }
}