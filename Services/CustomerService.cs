using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NordicBeesERP.Data;
using NordicBeesERP.Models;

namespace NordicBeesERP.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;
        private readonly ILogger<CustomerService> _logger;

        public CustomerService(IDbContextFactory<NordicBeesERPContext> dbFactory, ILogger<CustomerService> logger)
        {
            _dbFactory = dbFactory;
            _logger = logger;
        }

        public async Task<List<Customer>> GetCustomersAsync()
        {
            using var context = _dbFactory.CreateDbContext();
            return await context.BusinessPartners
                .Where(bp => bp.PartnerType == PartnerType.Customer || bp.PartnerType == PartnerType.Both)
                .GroupJoin(
                    context.Invoices,
                    bp => bp.Id,
                    i => i.CustomerId,
                    (bp, invoices) => new { Partner = bp, InvoiceCount = invoices.Count() })
                .OrderByDescending(x => x.InvoiceCount)
                .ThenBy(x => x.Partner.Name)
                .Select(x => new Customer
                {
                    Id = x.Partner.Id,
                    Name = x.Partner.Name,
                    City = x.Partner.City,
                    VatCode = x.Partner.VatCode,
                    PaymentTermDays = x.Partner.PaymentTermDays,
                    DefaultLanguage = x.Partner.DefaultLanguage,
                    DefaultVatRate = x.Partner.DefaultVatRate
                })
                .ToListAsync();
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

            existing.PartnerType = partner.PartnerType;
            existing.Name = partner.Name;
            existing.CompanyCode = partner.CompanyCode;
            existing.VatCode = partner.VatCode;
            existing.Address = partner.Address;
            existing.City = partner.City;
            existing.PostalCode = partner.PostalCode;
            existing.Country = partner.Country;
            existing.CountryCode = partner.CountryCode;
            existing.Phone = partner.Phone;
            existing.ContactPhone = partner.ContactPhone;
            existing.Email = partner.Email;
            existing.InvoiceEmail = partner.InvoiceEmail;
            existing.BankAccount = partner.BankAccount;
            existing.PaymentTermDays = partner.PaymentTermDays ;
            existing.DefaultLanguage = partner.DefaultLanguage;
            existing.DefaultVatRate = partner.DefaultVatRate;
            existing.Notes = partner.Notes;
                existing.IsActive = partner.IsActive;
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

        private PartnerType ParsePartnerType(string partnerTypeString) => partnerTypeString.ToLower() switch
        {
            "customer" or "klientas" => PartnerType.Customer,
            "supplier" or "tiekėjas" => PartnerType.Supplier,
            "both" or "abu" => PartnerType.Both,
            "expensesupplier" or "expense_supplier" or "išlaidų tiekėjas" => PartnerType.ExpenseSupplier,
            _ => PartnerType.Customer
        };

        public async Task<List<BusinessPartner>> GetSuppliersAsync()
        {
            using var context = _dbFactory.CreateDbContext();
            return await context.BusinessPartners
                .Where(bp => bp.PartnerType == PartnerType.Supplier || bp.PartnerType == PartnerType.Both)
                .OrderBy(bp => bp.Name)
                .ToListAsync();
        }

        public async Task<Customer> SaveCustomerAsync(Customer customer)
        {
            using var context = _dbFactory.CreateDbContext();
            
            customer.UpdatedAt = DateTime.Now;
            
            // Konvertuoti PartnerType string į enum
            PartnerType partnerType = ParsePartnerType(customer.PartnerType);
            
            if (customer.Id == 0)
            {
                // Create new customer
                customer.CreatedAt = DateTime.Now;
                
                var newPartner = new BusinessPartner
                {
                    PartnerType = partnerType,
                    Name = customer.Name,
                    CompanyCode = customer.CompanyCode,
                    VatCode = customer.VatCode,
                    Address = customer.Address,
                    City = customer.City,
                    PostalCode = customer.PostalCode,
                    Country = customer.Country ?? "Lithuania",
                    CountryCode = customer.CountryCode ?? "LT",
                    Phone = customer.Phone,
                    Email = customer.Email,
                    BankAccount = customer.BankAccount,
                    PaymentTermDays = customer.PaymentTermDays,
                    DefaultLanguage = customer.DefaultLanguage ?? "LT",
                    DefaultVatRate = customer.DefaultVatRate,
                    Notes = customer.Notes,
                    IsActive = customer.IsActive,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                
                context.BusinessPartners.Add(newPartner);
                await context.SaveChangesAsync();
                
                // Grąžinti su nauju ID
                customer.Id = newPartner.Id;
                return customer;
            }
            else
            {
                // Update existing customer using raw SQL to bypass EF tracking issues
                var rows = await context.Database.ExecuteSqlRawAsync(@"
                    UPDATE business_partners SET
                        partner_type = {0},
                        name = {1},
                        company_code = {2},
                        vat_code = {3},
                        address = {4},
                        city = {5},
                        postal_code = {6},
                        country = {7},
                        country_code = {8},
                        phone = {9},
                        email = {10},
                        bank_account = {11},
                        payment_term_days = {12},
                        default_language = {13},
                        default_vat_rate = {14},
                        notes = {15},
                        is_active = {16},
                        updated_at = {17}
                    WHERE id = {18}",
                    partnerType.ToString().ToLower(),
                    customer.Name,
                    customer.CompanyCode ?? "",
                    customer.VatCode ?? "",
                    customer.Address ?? "",
                    customer.City ?? "",
                    customer.PostalCode ?? "",
                    customer.Country ?? "Lithuania",
                    customer.CountryCode ?? "LT",
                    customer.Phone ?? "",
                    customer.Email ?? "",
                    customer.BankAccount ?? "",
                    customer.PaymentTermDays,
                    customer.DefaultLanguage ?? "lt",
                    customer.DefaultVatRate,
                    customer.Notes ?? "",
                    customer.IsActive,
                    DateTime.Now,
                    customer.Id);

                _logger.LogDebug("ExecuteSqlRaw rows affected: {Rows}", rows);

                if (rows == 0) throw new InvalidOperationException($"Klientas su ID {customer.Id} nerastas arba nepakeistas");

                customer.UpdatedAt = DateTime.Now;
                
                // Grąžinti su atnaujintais duomenimis
                return customer;
            }
        }
    }
}