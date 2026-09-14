using System.Globalization;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

public class ExpenseOcrServiceVatRateTests
{
    [Theory]
    [InlineData("21", "21")]
    [InlineData("21,00", "21")]
    [InlineData("21,5", "21.5")]
    [InlineData("21.5", "21.5")]
    [InlineData("21,00%", "21")]
    [InlineData("9", "9")]
    [InlineData("0", "0")]
    [InlineData("100", "100")]
    [InlineData("2100", null)]
    [InlineData("abc", null)]
    [InlineData("100.01", null)]
    [InlineData("-1", null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public void ParseVatRate_NormalizesAndValidates(string raw, string? expected)
    {
        var expectedRate = string.IsNullOrEmpty(expected)
            ? (decimal?)null
            : decimal.Parse(expected, CultureInfo.InvariantCulture);

        Assert.Equal(expectedRate, ExpenseOcrService.ParseVatRate(raw));
    }
}
