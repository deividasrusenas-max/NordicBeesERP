using System.Threading.Tasks;
using System.Collections.Generic;
using NordicBeesERP.Models;

namespace NordicBeesERP.Services
{
    public interface IProductionService
    {
        Task<List<WarehouseStockViewModel>> GetWarehouseStockAsync();
        Task<ServiceResult> CreateBatchAsync(NewBatchViewModel newBatch);
        Task<List<Warehouse>> GetWarehousesAsync();
    }
}
