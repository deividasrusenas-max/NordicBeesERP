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
    /// <summary>
    /// Edits one container's weights and cascades everything derived from them in ONE transaction: container net, the IN
    /// stock movement, the delivery line weight + line_total, the delivery totals and status, and drops the cached receipt PDF.
    /// Throws InvalidOperationException (BucketTareException for tare rules) with a user-facing message when the edit is not allowed.
    /// </summary>
    Task UpdateContainerWeightAsync(int containerId, decimal grossWeight, decimal tareWeight, bool tareManual);
    /// <summary>Updates only the barrel-return fields (targeted UPDATE - never rewrites the whole delivery row).</summary>
    Task UpdateBarrelsAsync(int deliveryId, bool needReturnBarrels, int barrelsOwed, int barrelsReturned);
    Task<string> GenerateDeliveryNumberAsync(string materialCode);
    Task UpdateDeliveryStatusAsync(int deliveryId);
    Task<bool> SaveSignatureAsync(int deliveryId, string signerName, string signatureSvg);
    Task<List<Delivery>> SearchDeliveriesAsync(string searchTerm, int limit = 20);
}
