using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;

namespace NordicBeesERP.Services
{
    public class GooglePlacesService : IGooglePlacesService
    {
        private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;
        private readonly HttpClient _httpClient;
        private readonly ILogger<GooglePlacesService> _logger;

        public GooglePlacesService(IDbContextFactory<NordicBeesERPContext> dbFactory, HttpClient httpClient, ILogger<GooglePlacesService> logger)
        {
            _dbFactory = dbFactory;
            _httpClient = httpClient;
            _logger = logger;
        }

        private async Task<string> GetApiKeyAsync()
        {
            await using var context = _dbFactory.CreateDbContext();
            return await context.AppSettings
                .Where(s => s.SettingKey == "google_places_api_key")
                .Select(s => s.SettingValue)
                .FirstOrDefaultAsync() ?? "";
        }

        public async Task<bool> IsHealthyAsync()
        {
            var apiKey = await GetApiKeyAsync();
            if (string.IsNullOrEmpty(apiKey)) return false;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://places.googleapis.com/v1/places:autocomplete");
                request.Headers.TryAddWithoutValidation("X-Goog-Api-Key", apiKey);
                request.Headers.TryAddWithoutValidation("X-Goog-FieldMask", "suggestions.placePrediction.placeId");
                request.Content = JsonContent.Create(new { input = "Vilnius", includedRegionCodes = new[] { "lt" } });
                using var resp = await _httpClient.SendAsync(request);
                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Google Places health-check failed");
                return false;
            }
        }
    }
}
