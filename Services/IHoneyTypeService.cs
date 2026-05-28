using NordicBeesERP.Models;

namespace NordicBeesERP.Services;

public interface IHoneyTypeService
{
    Task<List<HoneyType>> GetAllAsync();
    Task<List<HoneyType>> GetActiveAsync();
    Task<HoneyType?> GetByIdAsync(int id);
    Task<int> CreateAsync(HoneyType honeyType);
    Task UpdateAsync(HoneyType honeyType);
    Task DeleteAsync(int id);
}