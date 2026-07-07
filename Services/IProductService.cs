using NordicBeesERP.Models;

namespace NordicBeesERP.Services
{
    public interface IProductService
    {
        Task<List<Product>> GetAllProductsAsync();
        Task<Product?> GetProductByIdAsync(int id);
        Task<Product?> GetProductByCodeAsync(string code);
        Task<List<Product>> SearchProductsAsync(string searchTerm);
        Task<Product> CreateProductAsync(Product product);
        Task<Product> UpdateProductAsync(Product product);
        Task<bool> DeleteProductAsync(int id);
        Task<string> GenerateProductCodeAsync(ProductType productType);
    }
}