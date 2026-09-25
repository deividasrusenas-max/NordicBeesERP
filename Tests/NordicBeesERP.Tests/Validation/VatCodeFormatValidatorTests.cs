using NordicBeesERP.Services.Validation;
using Xunit;

namespace NordicBeesERP.Tests.Validation;

/// <summary>
/// Pure unit tests for VatCodeFormatValidator (normalisation, country prefix, digit count).
/// Format only — no checksums. No DB involvement.
/// </summary>
public class VatCodeFormatValidatorTests
{
    [Fact]
    public void TwoLtCodesGluedByOcr_WrongFormat()
    {
        // Production evidence: 18 digits — two codes glued together by OCR.
        var result = VatCodeFormatValidator.Validate("LT277044060003097307");

        Assert.False(result.IsValid);
        Assert.Equal(VatCodeValidationReason.WrongFormat, result.Reason);
    }

    [Fact]
    public void LtNineDigits_Valid()
    {
        var result = VatCodeFormatValidator.Validate("LT120252515");

        Assert.True(result.IsValid);
        Assert.Null(result.Reason);
        Assert.Equal("LT", result.CountryCode);
        Assert.Equal("LT120252515", result.NormalizedCode);
    }

    [Fact]
    public void LtTwelveDigits_Valid()
    {
        var result = VatCodeFormatValidator.Validate("LT100013406816");

        Assert.True(result.IsValid);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void NoPrefixWithLtHint_ValidAndPrefixed()
    {
        var result = VatCodeFormatValidator.Validate("120252515", "LT");

        Assert.True(result.IsValid);
        Assert.Equal("LT", result.CountryCode);
        Assert.Equal("LT120252515", result.NormalizedCode);
    }

    [Fact]
    public void LtEightDigits_WrongFormat()
    {
        var result = VatCodeFormatValidator.Validate("LT12025251");

        Assert.False(result.IsValid);
        Assert.Equal(VatCodeValidationReason.WrongFormat, result.Reason);
    }

    [Fact]
    public void UnsupportedPrefix_UnknownCountry()
    {
        var result = VatCodeFormatValidator.Validate("XX123");

        Assert.False(result.IsValid);
        Assert.Equal(VatCodeValidationReason.UnknownCountry, result.Reason);
    }

    [Fact]
    public void NoPrefixWithoutHint_UnknownCountry()
    {
        var result = VatCodeFormatValidator.Validate("120252515");

        Assert.False(result.IsValid);
        Assert.Equal(VatCodeValidationReason.UnknownCountry, result.Reason);
    }

    [Fact]
    public void LowercaseSpacesDotsDashes_Normalized()
    {
        var result = VatCodeFormatValidator.Validate(" lt 120.252-515 ");

        Assert.True(result.IsValid);
        Assert.Equal("LT120252515", result.NormalizedCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullOrWhitespace_Empty(string? input)
    {
        var result = VatCodeFormatValidator.Validate(input);

        Assert.False(result.IsValid);
        Assert.Equal(VatCodeValidationReason.Empty, result.Reason);
    }

    [Theory]
    [InlineData("LV12345678901")]   // 11
    [InlineData("EE123456789")]     // 9
    [InlineData("DE123456789")]     // 9
    [InlineData("PL1234567890")]    // 10
    [InlineData("RO12")]            // 2
    [InlineData("RO1234567890")]    // 10
    public void SupportedCountryCorrectDigitCount_Valid(string input)
    {
        Assert.True(VatCodeFormatValidator.Validate(input).IsValid);
    }

    [Theory]
    [InlineData("LV1234567890")]    // 10, needs 11
    [InlineData("EE12345678")]      // 8, needs 9
    [InlineData("DE1234567890")]    // 10, needs 9
    [InlineData("PL123456789")]     // 9, needs 10
    [InlineData("RO1")]             // 1, needs 2..10
    [InlineData("RO12345678901")]   // 11, needs 2..10
    [InlineData("LT1234567890")]    // 10, LT needs 9 or 12
    [InlineData("LT12025251A")]     // non-digit in body
    public void SupportedCountryWrongBody_WrongFormat(string input)
    {
        var result = VatCodeFormatValidator.Validate(input);

        Assert.False(result.IsValid);
        Assert.Equal(VatCodeValidationReason.WrongFormat, result.Reason);
    }

    [Theory]
    [InlineData("EL123456789")]
    [InlineData("GR123456789")]
    public void GreeceNotSupported_UnknownCountry(string input)
    {
        Assert.Equal(VatCodeValidationReason.UnknownCountry, VatCodeFormatValidator.Validate(input).Reason);
    }
}
