namespace NordicBeesERP.Services;

public record PlaceSuggestion(string PlaceId, string Description);

public record PlaceAddressDetails(
    string StreetAddress,   // route + street number, e.g. "Smėlio g. 17-522"
    string City,
    string PostalCode,
    string Country,         // full country name
    string CountryCode);    // ISO 2-letter, e.g. "LT", "PL"

/// <summary>
/// Result of picking a Google suggestion in AddressSearch. Address is never empty (street address, or the
/// suggestion description when Google returned no street part). The other fields are empty when unknown.
/// </summary>
public record AddressPick(string Address, string City, string PostalCode, string Country, string CountryCode)
{
    /// <summary>Writes only non-empty parts, so a pick never wipes values the user already has.</summary>
    public void ApplyTo(Action<string> setAddress, Action<string> setCity, Action<string> setPostalCode,
        Action<string> setCountry, Action<string> setCountryCode)
    {
        if (!string.IsNullOrWhiteSpace(Address)) setAddress(Address);
        if (!string.IsNullOrWhiteSpace(City)) setCity(City);
        if (!string.IsNullOrWhiteSpace(PostalCode)) setPostalCode(PostalCode);
        if (!string.IsNullOrWhiteSpace(Country)) setCountry(Country);
        if (!string.IsNullOrWhiteSpace(CountryCode)) setCountryCode(CountryCode);
    }
}
