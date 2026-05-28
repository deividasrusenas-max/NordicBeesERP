using NordicBeesERP.Models.WarehouseModule;

namespace NordicBeesERP.Services;

public interface IDeliveryService
{
    Task<List<Delivery>> GetAllAsync();
    Task<List<Delivery>> GetFilteredAsync(string? status, int? supplierId, DateTime? fromDate, DateTime? toDate);
    Task<Delivery?> GetByIdAsync(int id);
    Task<List<DeliveryLine>> GetLinesByDeliveryIdAsync(int deliveryId);
    Task<List<Container>> GetContainersByDeliveryAsync(int deliveryId);
    Task<int> CreateDeliveryWithContainersAsync(Delivery delivery, List<DeliveryLine> lines, List<Container> containers);
    Task UpdatePricesAsync(int deliveryId, List<DeliveryLine> lines, int barrelsOwed);
    Task RecalculateTotalsAsync(int deliveryId);
    Task<string> GenerateDeliveryNumberAsync(string materialCode);
    Task UpdateDeliveryStatusAsync(int deliveryId);
}
