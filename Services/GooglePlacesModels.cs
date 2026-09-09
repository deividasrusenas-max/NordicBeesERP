namespace NordicBeesERP.Services;

public record PlaceSuggestion(string PlaceId, string Description);

public record PlaceAddressDetails(
    string StreetAddress,   // route + street number, e.g. "Smėlio g. 17-522"
    string City,
    string PostalCode,
    string Country,         // full country name
    string CountryCode);    // ISO 2-letter, e.g. "LT", "PL"
