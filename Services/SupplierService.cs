using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models;

namespace NordicBeesERP.Services
{
    public interface ISupplierService
    {
        Task<List<Supplier>> GetSuppliersAsync();
        Task<List<Supplier>> GetAllSuppliersAsync();
        Task<List<BusinessPartner>> GetAllBusinessPartnersAsync();
        Task<BusinessPartner?> GetBusinessPartnerByIdAsync(int id);
        Task<Supplier?> GetSupplierByIdAsync(int id);
        Task<BusinessPartner> CreateBusinessPartnerAsync(BusinessPartner partner);
        Task<BusinessPartner> UpdateBusinessPartnerAsync(BusinessPartner partner);
        Task<bool> DeleteBusinessPartnerAsync(int id);
        Task<Supplier> SaveSupplierAsync(Supplier supplier);
    }

    public class SupplierService : ISupplierService
    {
        private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;

        public SupplierService(IDbContextFactory<NordicBeesERPContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public async Task<List<Supplier>> GetSuppliersAsync()
        {
            using var context = _dbFactory.CreateDbContext();
            var all = await context.BusinessPartners.ToListAsync();
            var filtered = all.Where(bp => bp.PartnerType == Models.PartnerType.Supplier 
                                         || bp.PartnerType == Models.PartnerType.Both
                                         || bp.PartnerType == Models.PartnerType.ExpenseSupplier)
                              .ToList();
            var result = filtered.Select(bp => new Supplier
            {
                Id = bp.Id,
                Name = bp.Name,
                City = bp.City ?? string.Empty,
                VatCode = bp.VatCode,
                PaymentTermDays = bp.PaymentTermDays,
                DefaultLanguage = bp.DefaultLanguage,
                DefaultVatRate = bp.DefaultVatRate,
                CompanyCode = bp.CompanyCode,
                Address = bp.Address,
                PostalCode = bp.PostalCode,
                Country = bp.Country,
                CountryCode = bp.CountryCode,
                Phone = bp.Phone,
                ContactPhone = bp.ContactPhone,
                Email = bp.Email,
                InvoiceEmail = bp.InvoiceEmail,
                BankAccount = bp.BankAccount,
                Notes = bp.Notes,
                IsActive = bp.IsActive,
                SupplierFirstName = bp.SupplierFirstName,
                SupplierLastName = bp.SupplierLastName,
                NationalIdNumber = bp.NationalIdNumber,
                PartnerType = bp.PartnerType,
                SupplierType = bp.SupplierType,
                DefaultExpenseCategoryId = bp.DefaultExpenseCategoryId
            })
            .OrderBy(s => s.Name)
            .ToList();

            var withCategory = result.Where(s => s.DefaultExpenseCategoryId.HasValue).Count();

            return result;
        }

        public async Task<List<Supplier>> GetAllSuppliersAsync()
        {
            using var context = _dbFactory.CreateDbContext();
            var all = await context.BusinessPartners.ToListAsync();
            var filtered = all.Where(bp => bp.PartnerType == Models.PartnerType.Supplier 
                                         || bp.PartnerType == Models.PartnerType.Both
                                         || bp.PartnerType == Models.PartnerType.ExpenseSupplier)
                              .ToList();
            return filtered.Select(bp => new Supplier
            {
                Id = bp.Id,
                Name = bp.Name,
                City = bp.City ?? string.Empty,
                VatCode = bp.VatCode,
                PaymentTermDays = bp.PaymentTermDays,
                DefaultLanguage = bp.DefaultLanguage,
                DefaultVatRate = bp.DefaultVatRate,
                CompanyCode = bp.CompanyCode,
                Address = bp.Address,
                PostalCode = bp.PostalCode,
                Country = bp.Country,
                CountryCode = bp.CountryCode,
                Phone = bp.Phone,
                ContactPhone = bp.ContactPhone,
                Email = bp.Email,
                InvoiceEmail = bp.InvoiceEmail,
                BankAccount = bp.BankAccount,
                Notes = bp.Notes,
                IsActive = bp.IsActive,
                SupplierFirstName = bp.SupplierFirstName,
                SupplierLastName = bp.SupplierLastName,
                NationalIdNumber = bp.NationalIdNumber,
                PartnerType = bp.PartnerType,
                SupplierType = bp.SupplierType,
                DefaultExpenseCategoryId = bp.DefaultExpenseCategoryId
            })
            .OrderBy(s => s.Name)
            .ToList();
        }

        public async Task<List<BusinessPartner>> GetAllBusinessPartnersAsync()
        {
            using var context = _dbFactory.CreateDbContext();
            return await context.BusinessPartners
                .OrderBy(bp => bp.Name)
                .ToListAsync();
        }

        public async Task<BusinessPartner?> GetBusinessPartnerByIdAsync(int id)
        {
            using var context = _dbFactory.CreateDbContext();
            return await context.BusinessPartners.FindAsync(id);
        }

        public async Task<Supplier?> GetSupplierByIdAsync(int id)
        {
            using var context = _dbFactory.CreateDbContext();
            var partner = await context.BusinessPartners
                .FirstOrDefaultAsync(bp => bp.Id == id);
            if (partner == null)
                return null;

            return new Supplier
            {
                Id = partner.Id,
                Name = partner.Name,
                City = partner.City ?? string.Empty,
                VatCode = partner.VatCode,
                PaymentTermDays = partner.PaymentTermDays,
                DefaultLanguage = partner.DefaultLanguage,
                DefaultVatRate = partner.DefaultVatRate,
                CompanyCode = partner.CompanyCode,
                Address = partner.Address,
                PostalCode = partner.PostalCode,
                Country = partner.Country,
                CountryCode = partner.CountryCode,
                Phone = partner.Phone,
                ContactPhone = partner.ContactPhone,
                Email = partner.Email,
                InvoiceEmail = partner.InvoiceEmail,
                BankAccount = partner.BankAccount,
                Notes = partner.Notes,
                IsActive = partner.IsActive,
                SupplierFirstName = partner.SupplierFirstName,
                SupplierLastName = partner.SupplierLastName,
                NationalIdNumber = partner.NationalIdNumber,
                PartnerType = partner.PartnerType,
                SupplierType = partner.SupplierType,
                DefaultExpenseCategoryId = partner.DefaultExpenseCategoryId
            };
        }

        public async Task<BusinessPartner> CreateBusinessPartnerAsync(BusinessPartner partner)
        {
            using var context = _dbFactory.CreateDbContext();
            
            partner.CreatedAt = DateTime.Now;
            partner.UpdatedAt = DateTime.Now;
            
            context.BusinessPartners.Add(partner);
            await context.SaveChangesAsync();
            
            return partner;
        }

        public async Task<BusinessPartner> UpdateBusinessPartnerAsync(BusinessPartner partner)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var existing = await context.BusinessPartners.FindAsync(partner.Id);
            if (existing == null)
            {
                throw new InvalidOperationException($"BusinessPartner with ID {partner.Id} not found");
            }
            
            existing.Name = partner.Name;
            existing.City = partner.City;
            existing.VatCode = partner.VatCode;
            existing.PaymentTermDays = partner.PaymentTermDays;
            existing.DefaultLanguage = partner.DefaultLanguage;
            existing.DefaultVatRate = partner.DefaultVatRate;
            existing.CompanyCode = partner.CompanyCode;
            existing.Address = partner.Address;
            existing.PostalCode = partner.PostalCode;
            existing.Country = partner.Country;
            existing.CountryCode = partner.CountryCode;
            existing.Phone = partner.Phone;
            existing.ContactPhone = partner.ContactPhone;
            existing.Email = partner.Email;
            existing.InvoiceEmail = partner.InvoiceEmail;
            existing.BankAccount = partner.BankAccount;
            existing.Notes = partner.Notes;
            existing.IsActive = partner.IsActive;
            existing.SupplierFirstName = partner.SupplierFirstName;
            existing.SupplierLastName = partner.SupplierLastName;
            existing.NationalIdNumber = partner.NationalIdNumber;
            existing.SupplierType = partner.SupplierType;
            existing.DefaultExpenseCategoryId = partner.DefaultExpenseCategoryId;
            existing.UpdatedAt = DateTime.Now;
            
            await context.SaveChangesAsync();
            
            return existing;
        }

        public async Task<bool> DeleteBusinessPartnerAsync(int id)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var partner = await context.BusinessPartners.FindAsync(id);
            if (partner == null)
            {
                return false;
            }
            
            context.BusinessPartners.Remove(partner);
            await context.SaveChangesAsync();
            
            return true;
        }

        public async Task<Supplier> SaveSupplierAsync(Supplier supplier)
        {
            using var context = _dbFactory.CreateDbContext();
            
            BusinessPartner partner;
            if (supplier.Id > 0)
            {
                await context.Database.ExecuteSqlRawAsync(@"
                    UPDATE business_partners SET
                        partner_type = {0}, name = {1}, company_code = {2}, vat_code = {3},
                        address = {4}, city = {5}, postal_code = {6}, country = {7}, country_code = {8},
                        phone = {9}, email = {10}, bank_account = {11}, payment_term_days = {12},
                        default_language = {13}, default_vat_rate = {14}, notes = {15}, is_active = {16},
                        default_expense_category_id = {17}, updated_at = {18}
                    WHERE id = {19}",
                    supplier.PartnerType.ToString().ToLower(),
                    supplier.Name ?? "",
                    supplier.CompanyCode ?? "",
                    supplier.VatCode ?? "",
                    supplier.Address ?? "",
                    supplier.City ?? "",
                    supplier.PostalCode ?? "",
                    supplier.Country ?? "",
                    supplier.CountryCode ?? "",
                    supplier.Phone ?? "",
                    supplier.Email ?? "",
                    supplier.BankAccount ?? "",
                    supplier.PaymentTermDays,
                    supplier.DefaultLanguage ?? "lt",
                    supplier.DefaultVatRate,
                    supplier.Notes ?? "",
                    supplier.IsActive,
                    supplier.DefaultExpenseCategoryId,
                    DateTime.Now,
                    supplier.Id);
                return supplier;
            }
            else
            {
                partner = new BusinessPartner
                {
                    PartnerType = supplier.PartnerType
                };
                context.BusinessPartners.Add(partner);
            }
            
            partner.Name = supplier.Name;
            partner.City = supplier.City ?? string.Empty;
            partner.VatCode = supplier.VatCode;
            partner.PaymentTermDays = supplier.PaymentTermDays;
            partner.DefaultLanguage = supplier.DefaultLanguage ?? "LT";
            partner.DefaultVatRate = supplier.DefaultVatRate;
            partner.CompanyCode = supplier.CompanyCode;
            partner.Address = supplier.Address;
            partner.PostalCode = supplier.PostalCode;
            partner.Country = supplier.Country;
            partner.CountryCode = supplier.CountryCode;
            partner.Phone = supplier.Phone;
            partner.ContactPhone = supplier.ContactPhone;
            partner.Email = supplier.Email;
            partner.InvoiceEmail = supplier.InvoiceEmail;
            partner.BankAccount = supplier.BankAccount;
            partner.Notes = supplier.Notes;
            partner.IsActive = supplier.IsActive;
            partner.SupplierFirstName = supplier.SupplierFirstName;
            partner.SupplierLastName = supplier.SupplierLastName;
            partner.NationalIdNumber = supplier.NationalIdNumber;
            partner.SupplierType = supplier.SupplierType;
            partner.DefaultExpenseCategoryId = supplier.DefaultExpenseCategoryId;
            partner.UpdatedAt = DateTime.Now;
            
            if (supplier.Id <= 0)
            {
                partner.CreatedAt = DateTime.Now;
                await context.SaveChangesAsync();
                supplier.Id = partner.Id;
            }
            
            return supplier;
        }
    }
}