using NordicBeesERP.Helpers;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Pure unit tests for AddressFormatter.FormatFull — joins address/postal
/// code/city/country with ", ", skipping null/empty/whitespace-only parts
/// and trimming each kept part. No DB involvement, no culture concerns
/// (plain ASCII string assertions).
/// </summary>
public class AddressFormatterTests
{
    [Fact]
    public void FormatFull_AllPartsPresent_JoinsWithCommaSpace()
    {
        Assert.Equal("Gedimino pr. 9, 01103, Vilnius, Lietuva",
            AddressFormatter.FormatFull("Gedimino pr. 9", "01103", "Vilnius", "Lietuva"));
    }

    [Fact]
    public void FormatFull_SomePartsNull_SkipsThem()
    {
        Assert.Equal("Gedimino pr. 9, Vilnius",
            AddressFormatter.FormatFull("Gedimino pr. 9", null, "Vilnius", null));
    }

    [Fact]
    public void FormatFull_EmptyStringParts_Skipped()
    {
        // Both empty and whitespace-only parts are skipped.
        Assert.Equal("Gedimino pr. 9, Vilnius",
            AddressFormatter.FormatFull("Gedimino pr. 9", "", "Vilnius", "  "));
    }

    [Fact]
    public void FormatFull_AllPartsNull_ReturnsEmptyString()
    {
        Assert.Equal("", AddressFormatter.FormatFull(null, null, null, null));
    }

    [Fact]
    public void FormatFull_TrimsIndividualParts()
    {
        Assert.Equal("Gedimino pr. 9, 01103, Vilnius, Lietuva",
            AddressFormatter.FormatFull("  Gedimino pr. 9  ", " 01103 ", " Vilnius", "Lietuva "));
    }

    [Fact]
    public void FormatFull_AddressOnly_ReturnsJustAddress()
    {
        Assert.Equal("Gedimino pr. 9",
            AddressFormatter.FormatFull("Gedimino pr. 9", null, null, null));
    }
}
