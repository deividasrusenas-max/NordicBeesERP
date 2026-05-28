using NordicBeesERP.Models.WarehouseModule;

namespace NordicBeesERP.Services;

public interface IContainerService
{
    Task<List<Container>> GetByWarehouseAsync(int warehouseId);
    Task<List<Container>> GetFilteredAsync(int? warehouseId, int? honeyTypeId, int? supplierId, string? status, string? searchCode);
    Task<Container?> GetByIdAsync(int id);
    Task<Container?> GetByCodeAsync(string containerCode);
    Task<int> CreateAsync(Container container);
    Task<List<int>> CreateBatchAsync(List<Container> containers);
    Task UpdateStatusAsync(int id, string newStatus);
    Task WriteOffAsync(List<int> containerIds, string reason, int? createdBy);
    Task<int> GetCountByWarehouseAsync(int? warehouseId, string? status);
    Task<decimal> GetTotalNetWeightAsync(int? warehouseId, string? status);
    Task<string?> GetLastContainerCodeAsync();
    Task<string?> GetLastBucketCodeAsync();
    Task<List<Container>> GetByIdsAsync(List<int> ids);
    Task UpdateHoneyTypeAsync(List<int> containerIds, int honeyTypeId);
}
