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
    private readonly ILogger<JarsService> _logger;

    public JarsService(IDbContextFactory<NordicBeesERPContext> dbFactory, IHttpClientFactory httpClientFactory, ILogger<JarsService> logger)
    {
        _dbFactory = dbFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
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

            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("JARS API key not found in app_settings");
                return null;
            }

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);

            var url = $"https://api.jars.lt/api/v1/companies/{companyCode}?country={country}";
            _logger.LogInformation("JARS lookup: {Url}", url);
            
            var response = await client.GetAsync(url);
            _logger.LogInformation("JARS response: {StatusCode}", response.StatusCode);
            
            if (!response.IsSuccessStatusCode) return null;
            
            var json = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("JARS result: {Json}", json[..Math.Min(200, json.Length)]);
            
            return System.Text.Json.JsonSerializer.Deserialize<JarsCompanyResult>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JARS lookup failed for {Code}/{Country}", companyCode, country);
            return null;
        }
    }
}