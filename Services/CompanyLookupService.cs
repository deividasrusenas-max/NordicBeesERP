using NordicBeesERP.Services;
using NordicBeesERP.Helpers;

namespace NordicBeesERP.Services;

public enum LookupSource { None, Jars, Vies }

public class CompanyLookupResult
{
    public bool Found { get; set; }
    public LookupSource Source { get; set; }
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? VatCode { get; set; }
    public string? CompanyCode { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? BankAccount { get; set; }
    public string? CountryCode { get; set; }
    public string? StatusLabel { get; set; }
    public string? RawAddress { get; set; }
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

        private string CleanCompanyName(string? name)
        {
            if (string.IsNullOrEmpty(name)) return "";

            var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // LT
                { "Uždaroji akcinė bendrovė", "UAB" },
                { "Uždaroji akcinė bendrove", "UAB" },
                { "Akcinė bendrovė", "AB" },
                { "Akcine bendrove", "AB" },
                { "Mažoji bendrija", "MB" },
                { "Mazoji bendrija", "MB" },
                { "Individuali įmonė", "IĮ" },
                { "Individuali imone", "IĮ" },
                { "Viešoji įstaiga", "VšĮ" },
                { "Viesoji istaiga", "VšĮ" },
                // LV
                { "Sabiedrība ar ierobežotu atbildību", "SIA" },
                { "Sabiedriba ar ierobezotu atbildibu", "SIA" },
                { "Akciju sabiedrība", "AS" },
                { "Akciju sabiedriba", "AS" },
                { "Individuālais komersants", "IK" },
                // EE
                { "Osaühing", "OÜ" },
                { "Aktsiaselts", "AS" },
                { "Tulundusühistu", "TÜ" },
                // PL
                { "Spółka z ograniczoną odpowiedzialnością", "Sp. z o.o." },
                { "Spolka z ograniczona odpowiedzialnoscia", "Sp. z o.o." },
                { "Spółka akcyjna", "S.A." },
                { "Spolka akcyjna", "S.A." },
                { "Spółka jawna", "Sp.j." },
                { "Spółka komandytowa", "Sp.k." },
                // DE
                { "Gesellschaft mit beschränkter Haftung", "GmbH" },
                { "Gesellschaft mit beschrankter Haftung", "GmbH" },
                { "Aktiengesellschaft", "AG" },
                { "Unternehmergesellschaft", "UG" },
                { "Kommanditgesellschaft", "KG" },
                { "Offene Handelsgesellschaft", "OHG" },
                // CZ
                { "Společnost s ručením omezeným", "s.r.o." },
                { "Spolecnost s rucenim omezenym", "s.r.o." },
                { "Akciová společnost", "a.s." },
                { "Akciova spolecnost", "a.s." },
                // BE
                { "Société à responsabilité limitée", "SRL" },
                { "Naamloze vennootschap", "NV" },
                { "Besloten vennootschap", "BV" },
                { "Société anonyme", "SA" },
                { "Coöperatieve vennootschap", "CV" },
                // NL
                { "Besloten Vennootschap", "BV" },
                { "Naamloze Vennootschap", "NV" },
                { "Vennootschap onder firma", "VOF" },
                { "Commanditaire vennootschap", "CV" },
                { "Eenmanszaak", "Eenmanszaak" },
            };

            var result = name;
            foreach (var kvp in replacements)
            {
                if (result.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    result = kvp.Value + result.Substring(kvp.Key.Length);
                    break;
                }
            }
            return result.Trim();
        }

        private (string? city, string? postalCode, string? streetAddress) ParseJarsAddress(string? address)
    {
        if (string.IsNullOrEmpty(address)) return (null, null, null);
        
        var parts = address.Split(',').Select(p => p.Trim()).ToArray();
        
        string? city = null;
        string? postalCode = null;
        string? streetAddress = null;
        
        foreach (var part in parts)
        {
            // Postal code: LT-12345 or 12345
            if (System.Text.RegularExpressions.Regex.IsMatch(part, @"^LT-\d{5}$") ||
                System.Text.RegularExpressions.Regex.IsMatch(part, @"^\d{5}$"))
            {
                postalCode = part.Replace("LT-", "");
            }
            // Street: contains abbreviation like g., pr., al., pl., a.
            else if (System.Text.RegularExpressions.Regex.IsMatch(part, @"\b(g\.|pr\.|al\.|pl\.|a\.|sk\.|kl\.|per\.|kelias)\b"))
            {
                streetAddress = part;
            }
            // City: short word without street abbreviations
            else if (part.Length > 1 && part.Length < 40 && !part.Contains('.'))
            {
                city = part;
            }
        }
        
        // Fallback: if no street found, use full address
        if (streetAddress == null) streetAddress = address;
        
        return (city, postalCode, streetAddress);
    }

    public async Task<CompanyLookupResult> LookupByCompanyCodeAsync(string input)
    {
        input = input?.Trim() ?? "";
        if (string.IsNullOrEmpty(input)) return new CompanyLookupResult { Found = false };
        
        // Detect if input looks like a VAT code (starts with 2 letters)
        var isVatCode = input.Length > 2 && char.IsLetter(input[0]) && char.IsLetter(input[1]);
        
        if (isVatCode)
        {
            // Route to VIES
            return await LookupByVatCodeAsync(input);
        }

        // Try JARS for LT, LV, EE
        foreach (var country in new[] { "lt", "lv", "ee" })
        {
            var jars = await _jarsService.GetCompanyAsync(input, country);
            if (jars != null)
            {
                var (city, postalCode, streetAddress) = ParseJarsAddress(jars.Address);

                  return new CompanyLookupResult
                  {
                      Found = true,
                      Source = LookupSource.Jars,
                      Name = CleanCompanyName(jars.Name ?? ""),
                    Address = streetAddress ?? jars.Address,
                    City = city,
                    PostalCode = postalCode,
                    VatCode = jars.PvmCode,
                    CompanyCode = jars.Code,
                    Email = jars.Email,
                    Phone = jars.Phone,
                    CountryCode = country.ToUpper(),
                    StatusLabel = $"{jars.LegalForm} · {jars.Status}",
                    RawAddress = jars.Address
                };
            }
        }
        return new CompanyLookupResult { Found = false };
    }

    public async Task<CompanyLookupResult> LookupByVatCodeAsync(string vatCode)
    {
        vatCode = vatCode?.Trim() ?? "";
        if (string.IsNullOrEmpty(vatCode)) return new CompanyLookupResult { Found = false };
        
        var vies = await _viesService.LookupAsync(vatCode);
        if (vies?.IsValid == true)
        {
             return new CompanyLookupResult
             {
                 Found = true,
                 Source = LookupSource.Vies,
                 Name = CleanCompanyName(vies.Name),
                Address = string.IsNullOrWhiteSpace(vies.Address) || vies.Address == "---" ? null : vies.Address,
                VatCode = vatCode,
                CountryCode = vies.CountryCode,
                StatusLabel = "VIES patvirtinta"
            };
        }
        return new CompanyLookupResult { Found = false, Source = LookupSource.Vies };
    }
}