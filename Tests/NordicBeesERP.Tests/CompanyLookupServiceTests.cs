using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Pure unit tests for CompanyLookupService.ParseJarsAddress (LT address
/// string parsing: street / city / postal code). No DB involvement — these
/// are plain string-parsing assertions against the static method.
/// </summary>
public class CompanyLookupServiceTests
{
    [Fact]
    public void ParseJarsAddress_StreetAndCityInOnePart_SplitsThem()
    {
        var (city, postalCode, streetAddress) = CompanyLookupService.ParseJarsAddress("Vilniaus g. 5 Kaunas");

        Assert.Equal("Kaunas", city);
        Assert.Null(postalCode);
        Assert.Equal("Vilniaus g. 5", streetAddress);
    }

    [Fact]
    public void ParseJarsAddress_StreetOnlyNoCity_WholePartIsStreet()
    {
        var (city, postalCode, streetAddress) = CompanyLookupService.ParseJarsAddress("Vilniaus g. 5");

        Assert.Null(city);
        Assert.Null(postalCode);
        Assert.Equal("Vilniaus g. 5", streetAddress);
    }

    [Fact]
    public void ParseJarsAddress_StreetAbbrevOnly_WholePartIsStreet()
    {
        var (city, postalCode, streetAddress) = CompanyLookupService.ParseJarsAddress("Vilniaus g.");

        Assert.Null(city);
        Assert.Null(postalCode);
        Assert.Equal("Vilniaus g.", streetAddress);
    }

    [Fact]
    public void ParseJarsAddress_CommaSeparatedFullAddress_ParsesAllThree()
    {
        var (city, postalCode, streetAddress) = CompanyLookupService.ParseJarsAddress("Gedimino pr. 9, 01103, Vilnius");

        Assert.Equal("Vilnius", city);
        Assert.Equal("01103", postalCode);
        Assert.Equal("Gedimino pr. 9", streetAddress);
    }

    [Fact]
    public void ParseJarsAddress_LtPostalCode_StripsPrefix()
    {
        var (city, postalCode, streetAddress) = CompanyLookupService.ParseJarsAddress("Vilniaus g. 5, LT-01103, Vilnius");

        Assert.Equal("Vilnius", city);
        Assert.Equal("01103", postalCode); // "LT-" prefix stripped
        Assert.Equal("Vilniaus g. 5", streetAddress);
    }

    [Fact]
    public void ParseJarsAddress_NoStreetAbbreviation_FallsBackToWholeString()
    {
        // No street abbreviation and no comma: the single part also matches the
        // "city" branch (length 1..39, no dot), so city is set to the whole
        // string AND streetAddress falls back to the full input.
        var (city, postalCode, streetAddress) = CompanyLookupService.ParseJarsAddress("Neries krantinė 12");

        Assert.Equal("Neries krantinė 12", city);
        Assert.Null(postalCode);
        Assert.Equal("Neries krantinė 12", streetAddress);
    }

    [Fact]
    public void ParseJarsAddress_Null_ReturnsNulls()
    {
        var (city, postalCode, streetAddress) = CompanyLookupService.ParseJarsAddress(null);

        Assert.Null(city);
        Assert.Null(postalCode);
        Assert.Null(streetAddress);
    }

    [Fact]
    public void ParseJarsAddress_Empty_ReturnsNulls()
    {
        var (city, postalCode, streetAddress) = CompanyLookupService.ParseJarsAddress("");

        Assert.Null(city);
        Assert.Null(postalCode);
        Assert.Null(streetAddress);
    }
}
