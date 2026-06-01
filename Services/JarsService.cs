using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Services;

namespace NordicBeesERP.Services;

public class JarsCompanyResult
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string? PvmCode { get; set; }
    public string? LegalForm { get; set; }
    public string Status { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

public interface IJarsService
{
    Task<JarsCompanyResult?> GetCompanyAsync(string companyCode, string country = "lt");
}

public class JarsService : IJarsService
{
    private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;
    private readonly IHttpClientFactory _httpClientFactory;

    public JarsService(IDbContextFactory<NordicBeesERPContext> dbFactory, IHttpClientFactory httpClientFactory)
    {
        _dbFactory = dbFactory;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<JarsCompanyResult?> GetCompanyAsync(string companyCode, string country = "lt")
    {
        try
        {
            using var context = _dbFactory.CreateDbContext();
            var apiKey = await context.AppSettings
                .Where(s => s.SettingKey == "jars_api_key")
                .Select(s => s.SettingValue)
                .FirstOrDefaultAsync();
            if (string.IsNullOrEmpty(apiKey)) return null;

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
            var response = await client.GetAsync($"https://api.jars.lt/api/v1/companies/{companyCode}?country={country}");
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            return System.Text.Json.JsonSerializer.Deserialize<JarsCompanyResult>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { return null; }
    }
}