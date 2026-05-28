using NordicBeesERP.Models.WarehouseModule;

namespace NordicBeesERP.Services;

public interface ITransferService
{
    Task<string> TransferContainersAsync(List<int> containerIds, int fromWarehouseId, int toWarehouseId, string? notes);
    Task<List<StockMovement>> GetTransferHistoryAsync();
}