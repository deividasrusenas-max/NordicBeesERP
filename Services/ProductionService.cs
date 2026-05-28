using System.Collections.Generic;
using System.Threading.Tasks;
using NordicBeesERP.Models;
using NordicBeesERP.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq; // Required for FirstOrDefaultAsync
using System;

namespace NordicBeesERP.Services
{
    public class ProductionService : IProductionService
    {
        private readonly IDbContextFactory<NordicBeesERPContext> _dbContextFactory;
        public ProductionService(IDbContextFactory<NordicBeesERPContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<List<WarehouseStockViewModel>> GetWarehouseStockAsync()
        {
            using var context = _dbContextFactory.CreateDbContext();
            var warehouseStock = await context.WarehouseStocks
                .Select(ws => new WarehouseStockViewModel
                {
                    Id = ws.Id,
                    ProductName = ws.Product.Name,
                    WarehouseName = ws.Warehouse.Name,
                    LotNumber = ws.LotNumber,
                    Quantity = ws.Quantity
                })
                .ToListAsync();

            return warehouseStock;
        }

        public async Task<ServiceResult> CreateBatchAsync(NewBatchViewModel newBatch)
        {
            using var context = _dbContextFactory.CreateDbContext();
            var product = await context.Products.FirstOrDefaultAsync(p => p.Name == newBatch.ProductName);
            var warehouse = await context.Warehouses.FirstOrDefaultAsync(w => w.Id == newBatch.WarehouseId);

            if (product == null || warehouse == null)
            {
                return new ServiceResult { Success = false, ErrorMessage = "Product or Warehouse not found." };
            }

            var newBatchStock = new WarehouseStock
            {
                Product = product,
                Warehouse = warehouse,
                LotNumber = GenerateLotNumber(),
                Quantity = newBatch.Quantity ?? 0
            };

            context.WarehouseStocks.Add(newBatchStock);
            await context.SaveChangesAsync();

            return new ServiceResult { Success = true };
        }

        public async Task<List<Warehouse>> GetWarehousesAsync()
        {
            using var context = _dbContextFactory.CreateDbContext();
            var warehouses = await context.Warehouses.ToListAsync();
            return warehouses;
        }

        private string GenerateLotNumber()
        {
            // Placeholder for generating a lot number
            return $"LOT-{DateTime.Now:yyyyMMddHHmmss}";
        }
    }
}