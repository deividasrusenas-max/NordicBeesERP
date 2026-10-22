using NordicBeesERP.Helpers;
using Xunit;

namespace NordicBeesERP.Tests;

public class LtNumberTests
{
    [Theory]
    [InlineData("11257,24", "11257.24")]
    [InlineData("11257.24", "11257.24")]
    [InlineData("1 125,50", "1125.50")]
    [InlineData("1 125,50", "1125.50")] // NBSP
    [InlineData("1.125,50", "1125.50")]
    [InlineData("1,125.50", "1125.50")]
    [InlineData("308,80", "308.80")]
    [InlineData("19,5", "19.5")]
    [InlineData("-5,2", "-5.2")]
    [InlineData("1000", "1000")]
    public void TryParseDecimal_Accepts(string text, string expected)
    {
        Assert.True(LtNumber.TryParseDecimal(text, out var v));
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), v);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    [InlineData("1.234.567")]   // ambiguous: never a silent guess
    [InlineData("1,234,567")]
    [InlineData("1,23.456,7")]  // thousands separator after the decimal one
    [InlineData("12,5kg")]
    public void TryParseDecimal_Rejects(string? text)
    {
        Assert.False(LtNumber.TryParseDecimal(text, out _));
    }

    [Fact]
    public void Format_AlwaysComma_RegardlessOfCurrentCulture()
    {
        var old = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            Assert.Equal("308,80", LtNumber.Format(308.8m, 2, grouped: false));
            Assert.Equal("1 125,50", LtNumber.Format(1125.5m));
            Assert.Equal("0,5", LtNumber.Format(0.5, 1));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = old; }
    }

    [Theory]
    [InlineData("11257.24")]
    [InlineData("-5.2")]
    [InlineData("0.01")]
    public void Format_ThenParse_RoundTrips(string text)
    {
        var m = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(LtNumber.TryParseDecimal(LtNumber.Format(m, 2), out var back));
        Assert.Equal(m, back);
    }
}
