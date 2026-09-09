namespace NordicBeesERP.Services;

public interface IGooglePlacesService
{
    Task<bool> IsHealthyAsync();

    Task<List<PlaceSuggestion>> AutocompleteAsync(string input, string sessionToken);

    Task<PlaceAddressDetails?> GetPlaceDetailsAsync(string placeId, string sessionToken);
}
