using NordicBeesERP.Models.WarehouseModule;

namespace NordicBeesERP.Services;

public interface ISupplierPaymentService
{
    Task<List<SupplierPayment>> GetByDeliveryAsync(int deliveryId);
    Task<List<SupplierPayment>> GetBySupplierAsync(int supplierId);
    Task<decimal> GetTotalPaidForDeliveryAsync(int deliveryId);
    Task<int> CreatePaymentAsync(SupplierPayment payment);
    Task DeletePaymentAsync(int id);
    Task UpdateAsync(SupplierPayment payment);
}
