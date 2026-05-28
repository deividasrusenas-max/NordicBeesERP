using NordicBeesERP.Models.WarehouseModule;
namespace NordicBeesERP.Services;
public interface IRawMaterialTypeService
{
    Task<List<RawMaterialType>> GetActiveAsync();
}