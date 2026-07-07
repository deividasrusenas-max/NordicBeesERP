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

            var exists = await context.Products
                .AsNoTracking()
                .AnyAsync(p => p.Id == product.Id);
            if (!exists)
                throw new InvalidOperationException($"Product with ID {product.Id} not found");

            var updatedAt = DateTime.Now;
            await context.Database.ExecuteSqlRawAsync(@"
                UPDATE products SET
                    name = {0},
                    ean_code = {1},
                    product_type = {2},
                    category_id = {3},
                    unit = {4},
                    cost_price = {5},
                    sale_price = {6},
                    purchase_price = {7},
                    warehouse_managed = {8},
                    track_lots = {9},
                    min_stock_level = {10},
                    description = {11},
                    notes = {12},
                    is_active = {13},
                    updated_at = {14}
                WHERE id = {15}",
                product.Name,
                product.EanCode,
                product.ProductType.ToString(),
                product.CategoryId,
                product.Unit,
                product.CostPrice,
                product.SalePrice,
                product.PurchasePrice,
                product.WarehouseManaged,
                product.TrackLots,
                product.MinStockLevel,
                product.Description,
                product.Notes,
                product.IsActive,
                updatedAt,
                product.Id);

            product.UpdatedAt = updatedAt;
            return product;
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

        public async Task<string> GenerateProductCodeAsync(ProductType productType)
        {
            string prefix = productType switch
            {
                ProductType.RawMaterial => "RAW",
                ProductType.Packaging => "PKG",
                ProductType.SemiFinished => "SEM",
                ProductType.Service => "SRV",
                _ => "PRD"
            };

            using var context = _dbFactory.CreateDbContext();
            var existing = await context.Products
                .AsNoTracking()
                .Where(p => p.Code.StartsWith(prefix + "-"))
                .Select(p => p.Code)
                .ToListAsync();

            int maxNum = 0;
            foreach (var code in existing)
            {
                var parts = code.Split('-');
                if (parts.Length == 2 && int.TryParse(parts[1], out int num))
                    maxNum = Math.Max(maxNum, num);
            }

            return $"{prefix}-{(maxNum + 1):D3}";
        }
    }
}
