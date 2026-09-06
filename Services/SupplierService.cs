using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Helpers;
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
            var filtered = all.Where(bp => bp.IsSupplier
                                         || bp.IsExpenseSupplier
                                         || (bp.IsSupplier == false && bp.IsCustomer == false && bp.IsExpenseSupplier == false
                                             && (bp.PartnerType == Models.PartnerType.Supplier
                                                 || bp.PartnerType == Models.PartnerType.Both
                                                 || bp.PartnerType == Models.PartnerType.ExpenseSupplier)))
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
                IsCustomer = bp.IsCustomer,
                IsSupplier = bp.IsSupplier,
                IsExpenseSupplier = bp.IsExpenseSupplier,
                IsIndividual = bp.IsIndividual,
                SupplierFirstName = bp.SupplierFirstName,
                SupplierLastName = bp.SupplierLastName,
                NationalIdNumber = bp.NationalIdNumber,
                PartnerType = bp.PartnerType,
                SupplierType = bp.SupplierType,
                DefaultExpenseCategoryId = bp.DefaultExpenseCategoryId
            })
            .OrderBy(s => s.Name.TrimStart('"', '\'', ' ', '('))
            .ToList();

            var withCategory = result.Where(s => s.DefaultExpenseCategoryId.HasValue).Count();

            return result;
        }

        public async Task<List<Supplier>> GetAllSuppliersAsync()
        {
            using var context = _dbFactory.CreateDbContext();
            var all = await context.BusinessPartners.ToListAsync();
            var filtered = all.Where(bp => bp.IsSupplier
                                         || bp.IsExpenseSupplier
                                         || (bp.IsSupplier == false && bp.IsCustomer == false && bp.IsExpenseSupplier == false
                                             && (bp.PartnerType == Models.PartnerType.Supplier
                                                 || bp.PartnerType == Models.PartnerType.Both
                                                 || bp.PartnerType == Models.PartnerType.ExpenseSupplier)))
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
                IsCustomer = bp.IsCustomer,
                IsSupplier = bp.IsSupplier,
                IsExpenseSupplier = bp.IsExpenseSupplier,
                IsIndividual = bp.IsIndividual,
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
            return await context.BusinessPartners.AsNoTracking().FirstOrDefaultAsync(bp => bp.Id == id);
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
                IsCustomer = partner.IsCustomer,
                IsSupplier = partner.IsSupplier,
                IsExpenseSupplier = partner.IsExpenseSupplier,
                IsIndividual = partner.IsIndividual,
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

            var exists = await context.BusinessPartners
                .AsNoTracking()
                .AnyAsync(bp => bp.Id == partner.Id);
            if (!exists)
            {
                throw new InvalidOperationException($"BusinessPartner with ID {partner.Id} not found");
            }

            await context.Database.ExecuteSqlRawAsync(@"
                UPDATE business_partners SET
                    name = {0}, company_code = {1}, vat_code = {2},
                    address = {3}, city = {4}, postal_code = {5}, country = {6}, country_code = {7},
                    phone = {8}, contact_phone = {9}, email = {10}, invoice_email = {11},
                    bank_account = {12}, payment_term_days = {13},
                    default_language = {14}, default_vat_rate = {15}, notes = {16}, is_active = {17},
                    supplier_first_name = {18}, supplier_last_name = {19}, national_id_number = {20},
                    supplier_type = {21}, default_expense_category_id = {22}, updated_at = {23}
                WHERE id = {24}",
                partner.Name,
                partner.CompanyCode,
                partner.VatCode,
                partner.Address,
                partner.City,
                partner.PostalCode,
                partner.Country,
                partner.CountryCode,
                partner.Phone,
                partner.ContactPhone,
                partner.Email,
                partner.InvoiceEmail,
                partner.BankAccount,
                partner.PaymentTermDays,
                partner.DefaultLanguage ?? "lt",
                partner.DefaultVatRate,
                partner.Notes,
                partner.IsActive,
                partner.SupplierFirstName,
                partner.SupplierLastName,
                partner.NationalIdNumber,
                partner.SupplierType,
                partner.DefaultExpenseCategoryId,
                DateTime.Now,
                partner.Id);

            partner.UpdatedAt = DateTime.Now;
            return partner;
        }

        public async Task<bool> DeleteBusinessPartnerAsync(int id)
        {
            using var context = _dbFactory.CreateDbContext();

            var exists = await context.BusinessPartners
                .AsNoTracking()
                .AnyAsync(bp => bp.Id == id);
            if (!exists)
            {
                return false;
            }

            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM business_partners WHERE id = {0}",
                id);

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
                        default_expense_category_id = {17}, is_customer = {18}, is_supplier = {19},
                        is_expense_supplier = {20}, is_individual = {21}, updated_at = {22}
                    WHERE id = {23}",
                    (supplier.IsCustomer || supplier.IsSupplier || supplier.IsExpenseSupplier
                        ? PartnerRoleFlagsHelper.DeriveFromFlags(supplier.IsCustomer, supplier.IsSupplier, supplier.IsExpenseSupplier)
                        : supplier.PartnerType).ToString().ToLower(),
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
                    supplier.IsCustomer,
                    supplier.IsSupplier,
                    supplier.IsExpenseSupplier,
                    supplier.IsIndividual,
                    DateTime.Now,
                    supplier.Id);
                return supplier;
            }
            else
            {
                partner = new BusinessPartner
                {
                    PartnerType = (supplier.IsCustomer || supplier.IsSupplier || supplier.IsExpenseSupplier)
                        ? PartnerRoleFlagsHelper.DeriveFromFlags(supplier.IsCustomer, supplier.IsSupplier, supplier.IsExpenseSupplier)
                        : supplier.PartnerType
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
            partner.IsCustomer = supplier.IsCustomer;
            partner.IsSupplier = supplier.IsSupplier;
            partner.IsExpenseSupplier = supplier.IsExpenseSupplier;
            partner.IsIndividual = supplier.IsIndividual;
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