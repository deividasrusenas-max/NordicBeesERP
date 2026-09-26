using NordicBeesERP.Services.Validation;
using Xunit;

namespace NordicBeesERP.Tests.Validation;

/// <summary>
/// Pure unit tests for CountryCodeResolver (D-042): ISO alpha-2 only, never a truncated name.
/// Staging evidence: LT VAT codes stored with "LI", IE8256796U with "IR". No DB involvement.
/// </summary>
public class CountryCodeResolverTests
{
    [Theory]
    [InlineData("Lietuva", "LT")]          // the staging bug: was "LI"
    [InlineData("Lithuania", "LT")]
    [InlineData("Litauen", "LT")]
    [InlineData("Ireland", "IE")]          // the staging bug: was "IR"
    [InlineData("Airija", "IE")]
    [InlineData("Éire", "IE")]
    [InlineData("Nederland", "NL")]
    [InlineData("Netherlands", "NL")]
    [InlineData("Nyderlandai", "NL")]
    [InlineData("Olandija", "NL")]
    [InlineData("Latvija", "LV")]
    [InlineData("Eesti", "EE")]
    [InlineData("Deutschland", "DE")]
    [InlineData("Polska", "PL")]
    [InlineData("România", "RO")]
    [InlineData("Česká republika", "CZ")]
    [InlineData("España", "ES")]
    [InlineData("United Kingdom", "GB")]
    [InlineData("Ukraina", "UA")]
    [InlineData("Suomi", "FI")]
    [InlineData("Sverige", "SE")]
    [InlineData("Danmark", "DK")]
    [InlineData("France", "FR")]
    [InlineData("Italia", "IT")]
    [InlineData("Österreich", "AT")]
    [InlineData("België", "BE")]
    public void FromName_MapsNativeEnglishAndLithuanianNames(string name, string expected)
    {
        Assert.Equal(expected, CountryCodeResolver.FromName(name));
        Assert.Equal(expected, CountryCodeResolver.FromAddress(name));
    }

    [Theory]
    [InlineData("LIETUVA", "LT")]
    [InlineData("  lietuva  ", "LT")]
    [InlineData("IRELAND", "IE")]
    [InlineData("olandija", "NL")]
    [InlineData("OSTERREICH", "AT")]       // diacritic-insensitive
    [InlineData("Cesko", "CZ")]
    [InlineData("Pays-Bas", "NL")]
    public void FromName_IsCaseAndDiacriticInsensitive(string name, string expected)
    {
        Assert.Equal(expected, CountryCodeResolver.FromName(name));
    }

    [Theory]
    [InlineData("Atlantis")]
    [InlineData("Narnia")]
    [InlineData("Li")]                     // never the first letters of a name
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void FromName_Unknown_IsNull(string? name)
    {
        Assert.Null(CountryCodeResolver.FromName(name));
    }

    [Theory]
    [InlineData("LT", "LT")]
    [InlineData("lt", "LT")]
    [InlineData(" IE ", "IE")]
    [InlineData("GB", "GB")]
    public void FromAddress_ValidIsoCode_IsKept(string value, string expected)
    {
        Assert.Equal(expected, CountryCodeResolver.FromAddress(value));
    }

    [Theory]
    [InlineData("XX")]                     // two letters, not an ISO code: never stored
    [InlineData("EL")]                     // a VAT prefix, not an ISO country code
    [InlineData("Latinamerika")]           // unknown name: not "LA"
    [InlineData("Ireland Republic Of X")]  // unknown name: not "IR"
    [InlineData("")]
    [InlineData(null)]
    public void FromAddress_NotIsoAndNotAKnownName_IsNull(string? value)
    {
        Assert.Null(CountryCodeResolver.FromAddress(value));
    }

    [Fact]
    public void FromAddress_UkIsMappedToGb()
    {
        Assert.Equal("GB", CountryCodeResolver.FromAddress("UK"));
    }

    [Theory]
    [InlineData("LT237375410", "LT")]      // staging codes
    [InlineData("LT333702811", "LT")]
    [InlineData("LT100001772414", "LT")]
    [InlineData("IE8256796U", "IE")]
    [InlineData("lt 237 375 410", "LT")]   // spaces / case
    [InlineData("LV40003032949", "LV")]
    [InlineData("DE123456789", "DE")]
    [InlineData("GB123456789", "GB")]
    [InlineData("EL123456789", "GR")]      // Greek VAT prefix is EL, the country is GR
    public void FromVatPrefix_WellFormed_GivesCountry(string vat, string expected)
    {
        Assert.Equal(expected, CountryCodeResolver.FromVatPrefix(vat));
    }

    [Theory]
    [InlineData("123456789")]              // no prefix
    [InlineData("XX123456789")]            // prefix outside ISO
    [InlineData("XI123456789")]            // Northern Ireland VAT prefix is not an ISO country code
    [InlineData("LT")]                     // nothing after the prefix
    [InlineData("L1234567")]               // one letter
    [InlineData("LT12$456")]               // not alphanumeric
    [InlineData("")]
    [InlineData("  - ")]
    [InlineData(null)]
    public void FromVatPrefix_NotWellFormed_IsNull(string? vat)
    {
        Assert.Null(CountryCodeResolver.FromVatPrefix(vat));
    }

    [Theory]
    [InlineData("LT237375410", "Lietuva", "LT")]
    [InlineData("LT237375410", "Lithuania", "LT")]
    [InlineData("IE8256796U", "Ireland", "IE")]
    [InlineData("LT237375410", "Latvija", "LT")]     // VAT prefix wins over a conflicting address
    [InlineData("IE8256796U", "LT", "IE")]
    [InlineData("EL123456789", "Lietuva", "GR")]
    [InlineData("123456789", "Lietuva", "LT")]       // no prefix: the address decides
    [InlineData("123456789", "LV", "LV")]
    [InlineData("XX123456789", "Ireland", "IE")]     // malformed prefix does not win
    [InlineData("", "Nederland", "NL")]
    [InlineData(null, null, null)]
    [InlineData("123456789", "Atlantis", null)]      // unknown → null, never a truncated name
    [InlineData("", "", null)]
    public void Resolve_VatPrefixThenAddressThenNull(string? vat, string? address, string? expected)
    {
        Assert.Equal(expected, CountryCodeResolver.Resolve(vat, address));
    }

    [Theory]
    [InlineData("LI", true)]
    [InlineData("li", true)]
    [InlineData("IR", true)]
    [InlineData("GB", true)]
    [InlineData("UK", false)]
    [InlineData("EL", false)]
    [InlineData("XX", false)]
    [InlineData("L", false)]
    [InlineData("LIE", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidIso(string? code, bool expected)
    {
        Assert.Equal(expected, CountryCodeResolver.IsValidIso(code));
    }

    [Fact]
    public void EveryNameInTheMap_ResolvesToAValidIsoCode()
    {
        foreach (var name in new[] { "Lietuva", "Ireland", "Nederland", "Graikija", "Liechtenstein", "USA", "UK" })
            Assert.True(CountryCodeResolver.IsValidIso(CountryCodeResolver.FromName(name)), name);
    }
}
