using NordicBeesERP.Helpers;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Pure unit tests for FormatAmountHelper.Trim (lt-LT display culture,
/// trailing-zero trimming). No DB involvement — these are culture-specific
/// string-format assertions. Convention: maxDecimals=2 for money, 3 for
/// qty/weight.
/// </summary>
public class FormatAmountHelperTests
{
    [Fact]
    public void Trim_Decimal_WholeNumber()
    {
        Assert.Equal("5", FormatAmountHelper.Trim(5m));       // default maxDecimals=3
        Assert.Equal("5", FormatAmountHelper.Trim(5m, 2));
    }

    [Fact]
    public void Trim_Decimal_RealDecimalDigitsKept()
    {
        Assert.Equal("5,25", FormatAmountHelper.Trim(5.25m, 3));
        Assert.Equal("5,25", FormatAmountHelper.Trim(5.25m, 2));
    }

    [Fact]
    public void Trim_Decimal_OneRealDigitAfterZerosTrimmed()
    {
        Assert.Equal("5,5", FormatAmountHelper.Trim(5.50m, 3));   // trailing zero trimmed, real 5 kept
        Assert.Equal("5,5", FormatAmountHelper.Trim(5.500m, 3));
    }

    [Fact]
    public void Trim_Decimal_MaxDecimalsRounds()
    {
        // .NET decimal formatting rounds to nearest (ties away from zero):
        // 1.2349 -> "1,235" at 3 decimals; 12.345 -> "12,35" at 2 decimals.
        Assert.Equal("1,235", FormatAmountHelper.Trim(1.2349m, 3));
        Assert.Equal("12,35", FormatAmountHelper.Trim(12.345m, 2));
    }

    [Fact]
    public void Trim_Nullable_EmptyWhenNull()
    {
        Assert.Equal("", FormatAmountHelper.Trim((decimal?)null));
        Assert.Equal("7,5", FormatAmountHelper.Trim((decimal?)7.5m, 3));
    }
}
