using NordicBeesERP.Models.WarehouseModule;

namespace NordicBeesERP.Services;

public interface IStockMovementService
{
    Task<List<StockMovement>> GetByContainerAsync(int containerId);
    Task<List<StockMovement>> GetFilteredAsync(int? warehouseId, string? movementType, DateTime? fromDate, DateTime? toDate);
    Task CreateMovementAsync(StockMovement movement);
}