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
                // Grąžinti numatytąjį objektą jei duomenų nėra
                return new CompanySettings
                {
                    CompanyName = "Nordic Bees UAB",
                    CompanyCode = "123456789",
                    VatCode = "LT123456789",
                    Address = "Klaipėdos str. 15",
                    BankAccount = "LT12 3456 7890 1234 5678",
                    BankSwift = "COBA LT XX",
                    BankName = "SEB Bankas",
                    UpdatedAt = DateTime.UtcNow
                };
            }
            
            return settings;
        }

        public async Task UpdateSettingsAsync(CompanySettings settings)
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            
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
                
                // Priverstinai pažymėti VISUS laukus kaip pakeistus
                context.Entry(existing).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
            }
            
            var saved = await context.SaveChangesAsync();
            System.Console.WriteLine($"DEBUG UPDATE SETTINGS: saved={saved} rows, Name={existing?.CompanyName}, Email={existing?.Email}");
        }
    }
}
