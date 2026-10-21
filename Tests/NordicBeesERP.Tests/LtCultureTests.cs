using System.Globalization;
using NordicBeesERP.Helpers;
using Xunit;

namespace NordicBeesERP.Tests;

public class LtCultureTests
{
    [Theory]
    [InlineData("308,80", 308.80)]
    [InlineData("308.80", 308.80)]
    [InlineData("1 234,5", 1234.5)]
    [InlineData("1 234,5", 1234.5)]
    [InlineData("19,5", 19.5)]
    [InlineData("-5,25", -5.25)]
    [InlineData("1000", 1000)]
    public void TryParseDecimal_AcceptsCommaAndDot(string text, double expected)
    {
        Assert.True(LtCulture.TryParseDecimal(text, out var v));
        Assert.Equal((decimal)expected, v);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("1.234,5")]
    [InlineData("1,234.5")]
    public void TryParseDecimal_RejectsEmptyAndAmbiguous(string? text)
    {
        Assert.False(LtCulture.TryParseDecimal(text, out _));
    }

    [Fact]
    public void Instance_UsesCommaAsDecimalSeparator_NotThousands()
    {
        Assert.Equal(",", LtCulture.Instance.NumberFormat.NumberDecimalSeparator);
        Assert.True(decimal.TryParse("308,80", NumberStyles.Number, LtCulture.Instance, out var v));
        Assert.Equal(308.80m, v);
    }
}
