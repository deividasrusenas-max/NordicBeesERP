// =====================================================
// NORDIC BEES ERP - COMPANY SETTINGS SERVICE
// Framework: .NET 10
// ORM: Entity Framework Core
// =====================================================

using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models;

namespace NordicBeesERP.Services
{
    // =====================================================
    // ĮMONĖS NUSTATYMAI - SERVICE IMPLEMENTATION
    // =====================================================

    public class CompanySettingsService : ICompanySettingsService
    {
        private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;

        public CompanySettingsService(IDbContextFactory<NordicBeesERPContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public async Task<CompanySettings> GetSettingsAsync()
        {
            using var context = _dbFactory.CreateDbContext();
            
            // Gauti pirmą įrašą (turi būti tik vienas)
            var settings = await context.CompanySettings.FirstOrDefaultAsync();
            
            if (settings == null)
            {
                throw new InvalidOperationException(
                    "Company settings not found in database. Please configure company settings via the admin panel.");
            }
            
            return settings;
        }

        public async Task UpdateSettingsAsync(CompanySettings settings)
        {
            try
            {
                await using var context = await _dbFactory.CreateDbContextAsync();
                
                var existing = await context.CompanySettings.FirstOrDefaultAsync();
                
                if (existing == null)
                {
                    settings.UpdatedAt = DateTime.UtcNow;
                    context.CompanySettings.Add(settings);
                }
                else
                {
                    existing.CompanyName = settings.CompanyName;
                    existing.CompanyCode = settings.CompanyCode;
                    existing.VatCode = settings.VatCode;
                    existing.Address = settings.Address;
                    existing.BankName = settings.BankName;
                    existing.BankIban = settings.BankIban;
                    existing.BankSwift = settings.BankSwift;
                    existing.BankAccount = settings.BankAccount;
                    existing.Email = settings.Email;
                    existing.Phone = settings.Phone;
                    existing.UpdatedAt = DateTime.UtcNow;
                    
                    context.Entry(existing).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                }
                
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException dbEx)
            {
                throw new Exception("Klaida atnaujinant įmonės duomenis", dbEx);
            }
            catch (Exception ex)
            {
                throw new Exception("Klaida atnaujinant įmonės duomenis", ex);
            }
        }
    }
}
