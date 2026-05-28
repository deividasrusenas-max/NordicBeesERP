using System.Collections.Generic;
using System.Threading.Tasks;
using NordicBeesERP.Models;

public interface ICustomerService
{
    Task<List<Customer>> GetCustomersAsync();
    Task<List<BusinessPartner>> GetAllBusinessPartnersAsync();
    Task<BusinessPartner?> GetBusinessPartnerByIdAsync(int id);
    Task<BusinessPartner> CreateBusinessPartnerAsync(BusinessPartner partner);
    Task<BusinessPartner> UpdateBusinessPartnerAsync(BusinessPartner partner);
    Task<bool> DeleteBusinessPartnerAsync(int id);
    Task<List<BusinessPartner>> GetSuppliersAsync();
    Task<Customer> SaveCustomerAsync(Customer customer);
}
