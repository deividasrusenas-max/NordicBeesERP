using System.Net.Http.Json;
using System.Text.Json;
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

        public async Task<List<PlaceSuggestion>> AutocompleteAsync(string input, string sessionToken)
        {
            if (string.IsNullOrWhiteSpace(input) || input.Trim().Length < 3) return new List<PlaceSuggestion>();

            var apiKey = await GetApiKeyAsync();
            if (string.IsNullOrEmpty(apiKey)) return new List<PlaceSuggestion>();

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://places.googleapis.com/v1/places:autocomplete");
                request.Headers.TryAddWithoutValidation("X-Goog-Api-Key", apiKey);
                request.Headers.TryAddWithoutValidation("X-Goog-FieldMask", "suggestions.placePrediction.placeId,suggestions.placePrediction.text");
                request.Content = JsonContent.Create(new { input, sessionToken });
                using var resp = await _httpClient.SendAsync(request);
                if (!resp.IsSuccessStatusCode) return new List<PlaceSuggestion>();

                var body = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                var suggestions = new List<PlaceSuggestion>();
                if (doc.RootElement.TryGetProperty("suggestions", out var suggestionsEl))
                {
                    foreach (var suggestion in suggestionsEl.EnumerateArray())
                    {
                        if (!suggestion.TryGetProperty("placePrediction", out var prediction)) continue;
                        var placeId = prediction.TryGetProperty("placeId", out var placeIdEl) ? placeIdEl.GetString() ?? "" : "";
                        var text = prediction.TryGetProperty("text", out var textEl) && textEl.TryGetProperty("text", out var innerText)
                            ? innerText.GetString() ?? ""
                            : "";
                        suggestions.Add(new PlaceSuggestion(placeId, text));
                    }
                }
                return suggestions;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Google Places autocomplete failed");
                return new List<PlaceSuggestion>();
            }
        }

        public async Task<PlaceAddressDetails?> GetPlaceDetailsAsync(string placeId, string sessionToken)
        {
            if (string.IsNullOrWhiteSpace(placeId)) return null;

            var apiKey = await GetApiKeyAsync();
            if (string.IsNullOrEmpty(apiKey)) return null;

            try
            {
                var url = $"https://places.googleapis.com/v1/places/{Uri.EscapeDataString(placeId)}?sessionToken={Uri.EscapeDataString(sessionToken ?? "")}";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("X-Goog-Api-Key", apiKey);
                request.Headers.TryAddWithoutValidation("X-Goog-FieldMask", "addressComponents");
                using var resp = await _httpClient.SendAsync(request);
                if (!resp.IsSuccessStatusCode) return null;

                var body = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);

                string street = "", city = "", postalCode = "", country = "", countryCode = "";
                if (doc.RootElement.TryGetProperty("addressComponents", out var components))
                {
                    foreach (var component in components.EnumerateArray())
                    {
                        var longText = component.TryGetProperty("longText", out var lt) ? lt.GetString() ?? "" : "";
                        var shortText = component.TryGetProperty("shortText", out var st) ? st.GetString() ?? "" : "";
                        if (!component.TryGetProperty("types", out var typesEl)) continue;

                        foreach (var type in typesEl.EnumerateArray())
                        {
                            var t = type.GetString();
                            switch (t)
                            {
                                case "route" when street.Length == 0:
                                    street = longText;
                                    break;
                                case "street_number":
                                    if (!string.IsNullOrEmpty(street)) street += " ";
                                    street += longText;
                                    break;
                                case "locality" when city.Length == 0:
                                    city = longText;
                                    break;
                                case "postal_town" when city.Length == 0:
                                    city = longText;
                                    break;
                                case "administrative_area_level_2" when city.Length == 0:
                                    city = longText;
                                    break;
                                case "postal_code" when postalCode.Length == 0:
                                    postalCode = longText;
                                    break;
                                case "country":
                                    country = longText;
                                    countryCode = shortText;
                                    break;
                            }
                        }
                    }
                }

                return new PlaceAddressDetails(street, city, postalCode, country, countryCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Google Places details lookup failed");
                return null;
            }
        }
    }
}
