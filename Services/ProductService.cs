using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models;

namespace NordicBeesERP.Services
{
    public class ProductService : IProductService
    {
        private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;

        public ProductService(IDbContextFactory<NordicBeesERPContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public async Task<List<Product>> GetAllProductsAsync()
        {
            using var context = _dbFactory.CreateDbContext();
            return await context.Products
                .OrderBy(p => p.Code)
                .ToListAsync();
        }

        public async Task<Product?> GetProductByIdAsync(int id)
        {
            using var context = _dbFactory.CreateDbContext();
            return await context.Products.FindAsync(id);
        }

        public async Task<Product?> GetProductByCodeAsync(string code)
        {
            using var context = _dbFactory.CreateDbContext();
            return await context.Products
                .FirstOrDefaultAsync(p => p.Code == code);
        }

        public async Task<List<Product>> SearchProductsAsync(string searchTerm)
        {
            using var context = _dbFactory.CreateDbContext();
            var term = searchTerm.ToLower();
            return await context.Products
                .Where(p => p.Code.ToLower().Contains(term) || 
                           p.Name.ToLower().Contains(term) ||
                           (p.EanCode != null && p.EanCode.ToLower().Contains(term)))
                .OrderBy(p => p.Code)
                .ToListAsync();
        }

        public async Task<Product> CreateProductAsync(Product product)
        {
            using var context = _dbFactory.CreateDbContext();
            
            product.CreatedAt = DateTime.Now;
            product.UpdatedAt = DateTime.Now;
            
            context.Products.Add(product);
            await context.SaveChangesAsync();
            
            return product;
        }

        public async Task<Product> UpdateProductAsync(Product product)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var existing = await context.Products.FindAsync(product.Id);
            if (existing == null)
            {
                throw new InvalidOperationException($"Product with ID {product.Id} not found");
            }

            existing.Code = product.Code;
            existing.Name = product.Name;
            existing.EanCode = product.EanCode;
            existing.ProductType = product.ProductType;
            existing.Category = product.Category;
            existing.CategoryId = product.CategoryId;
            existing.Unit = product.Unit;
            existing.CostPrice = product.CostPrice;
            existing.SalePrice = product.SalePrice;
            existing.PurchasePrice = product.PurchasePrice;
            existing.WarehouseManaged = product.WarehouseManaged;
            existing.TrackLots = product.TrackLots;
            existing.MinStockLevel = product.MinStockLevel;
            existing.Description = product.Description;
            existing.Notes = product.Notes;
            existing.IsActive = product.IsActive;
            existing.UpdatedAt = DateTime.Now;

            await context.SaveChangesAsync();
            
            return existing;
        }

        public async Task<bool> DeleteProductAsync(int id)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var product = await context.Products.FindAsync(id);
            if (product == null)
            {
                return false;
            }

            context.Products.Remove(product);
            await context.SaveChangesAsync();
            
            return true;
        }
    }
}
