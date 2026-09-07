using NordicBeesERP.Helpers;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Pure unit tests for CompensationVatCodeValidator (12-digit compensation
/// VAT code checksum). No DB involvement — static validation only.
/// </summary>
public class CompensationVatCodeValidatorTests
{
    [Fact]
    public void KnownGoodSamples_AllValidate()
    {
        string[] validCodes =
        {
            "100008534429",
            "100008460421",
            "100008433121",
            "100008025626", // exercises second-pass fallback (first-pass remainder is 10)
            "100008127723"
        };

        foreach (var code in validCodes)
            Assert.True(CompensationVatCodeValidator.IsValid(code), $"{code} should be valid");
    }

    [Fact]
    public void CorruptedSamples_FailValidation()
    {
        Assert.False(CompensationVatCodeValidator.IsValid("100008534428")); // last digit changed 9 -> 8
        Assert.False(CompensationVatCodeValidator.IsValid("10000853442X")); // non-digit character
    }

    [Fact]
    public void ThirteenDigitObsoleteFormat_RejectedByIsValid_DetectedByIsObsoleteFormat()
    {
        const string obsolete = "1000085344296"; // 13 digits, pre-2013 format

        Assert.False(CompensationVatCodeValidator.IsValid(obsolete));
        Assert.True(CompensationVatCodeValidator.IsObsoleteFormat(obsolete));
    }

    [Fact]
    public void NullOrWhitespace_Invalid()
    {
        string?[] emptyInputs = { null, "", "   " };

        foreach (var input in emptyInputs)
        {
            Assert.False(CompensationVatCodeValidator.IsValid(input));
            Assert.False(CompensationVatCodeValidator.IsObsoleteFormat(input));
        }
    }

    [Fact]
    public void WrongIndexDigit_Invalid()
    {
        // 12 digits, all numeric, but index digit (position 10) is '0' instead of '2'
        Assert.False(CompensationVatCodeValidator.IsValid("100008534409"));
    }
}
