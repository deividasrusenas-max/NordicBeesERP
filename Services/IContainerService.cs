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
    Task<string> GenerateNextContainerCodeAsync();
    /// <summary>app_settings 'AutoBarrelNumbering' (default false = operator enters a start ID).</summary>
    Task<bool> IsAutoNumberingEnabledAsync();
    /// <summary>Codes from <paramref name="codes"/> that already exist in containers.</summary>
    Task<List<string>> FindExistingCodesAsync(IEnumerable<string> codes);
    Task<List<Container>> GetByIdsAsync(List<int> ids);
    /// <summary>Sets the honey type of IN_STOCK / RECEIVED containers; splits / merges delivery lines, recomputes totals, clears the cached receipt.</summary>
    Task UpdateHoneyTypeAsync(List<int> containerIds, int honeyTypeId);
    /// <summary>Same rules for every container of one delivery line (a legacy line without containers is updated directly).</summary>
    Task UpdateDeliveryLineHoneyTypeAsync(int deliveryLineId, int honeyTypeId);
}
