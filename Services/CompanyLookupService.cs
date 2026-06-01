using NordicBeesERP.Services;

namespace NordicBeesERP.Services;

public enum LookupSource { None, Jars, Vies }

public class CompanyLookupResult
{
    public bool Found { get; set; }
    public LookupSource Source { get; set; }
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? VatCode { get; set; }
    public string? CompanyCode { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? CountryCode { get; set; }
    public string? StatusLabel { get; set; }
}

public interface ICompanyLookupService
{
    Task<CompanyLookupResult> LookupByCompanyCodeAsync(string companyCode);
    Task<CompanyLookupResult> LookupByVatCodeAsync(string vatCode);
}

public class CompanyLookupService : ICompanyLookupService
{
    private readonly IJarsService _jarsService;
    private readonly IViesService _viesService;

    public CompanyLookupService(IJarsService jarsService, IViesService viesService)
    {
        _jarsService = jarsService;
        _viesService = viesService;
    }

    public async Task<CompanyLookupResult> LookupByCompanyCodeAsync(string companyCode)
    {
        // Try JARS for LT, LV, EE
        foreach (var country in new[] { "lt", "lv", "ee" })
        {
            var jars = await _jarsService.GetCompanyAsync(companyCode, country);
            if (jars != null)
            {
                return new CompanyLookupResult
                {
                    Found = true,
                    Source = LookupSource.Jars,
                    Name = jars.Name,
                    Address = jars.Address,
                    VatCode = jars.PvmCode,
                    CompanyCode = jars.Code,
                    Email = jars.Email,
                    Phone = jars.Phone,
                    CountryCode = country.ToUpper(),
                    StatusLabel = $"{jars.LegalForm} · {jars.Status}"
                };
            }
        }
        return new CompanyLookupResult { Found = false };
    }

    public async Task<CompanyLookupResult> LookupByVatCodeAsync(string vatCode)
    {
        var vies = await _viesService.LookupAsync(vatCode);
        if (vies?.IsValid == true)
        {
            return new CompanyLookupResult
            {
                Found = true,
                Source = LookupSource.Vies,
                Name = vies.Name,
                Address = string.IsNullOrWhiteSpace(vies.Address) || vies.Address == "---" ? null : vies.Address,
                VatCode = vatCode,
                CountryCode = vies.CountryCode,
                StatusLabel = "VIES patvirtinta"
            };
        }
        return new CompanyLookupResult { Found = false, Source = LookupSource.Vies };
    }
}