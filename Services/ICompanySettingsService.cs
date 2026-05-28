// =====================================================
// NORDIC BEES ERP - COMPANY SETTINGS SERVICE
// Framework: .NET 10
// =====================================================

using NordicBeesERP.Models;

namespace NordicBeesERP.Services
{
    // =====================================================
    // ĮMONĖS NUSTATYMAI - SERVICE INTERFACE
    // =====================================================

    public interface ICompanySettingsService
    {
        Task<NordicBeesERP.Models.CompanySettings> GetSettingsAsync();
        Task UpdateSettingsAsync(CompanySettings settings);
    }
}