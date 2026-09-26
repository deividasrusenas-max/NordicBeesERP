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

    // --- S1b: the four open notes from the validator review (PLAN-ETAPAS1 §1.1) ---

    [Theory]
    [InlineData("LT120\u2013252\u2013515")]  // en dash
    [InlineData("LT120\u2014252515")]         // em dash
    [InlineData("LT120\u2212252515")]         // minus sign
    [InlineData("LT120\u2010252515")]         // Unicode hyphen
    public void DashCharacters_Stripped(string input)
    {
        var result = VatCodeFormatValidator.Validate(input);

        Assert.True(result.IsValid);
        Assert.Equal("LT120252515", result.NormalizedCode);
    }

    [Theory]
    [InlineData(".-.-")]
    [InlineData(" . - ")]
    [InlineData("\u2013")]
    public void SeparatorOnlyInput_Empty(string input)
    {
        var result = VatCodeFormatValidator.Validate(input);

        Assert.False(result.IsValid);
        Assert.Equal(VatCodeValidationReason.Empty, result.Reason);
        Assert.Null(result.NormalizedCode);
    }

    [Theory]
    [InlineData("LTU")]
    [InlineData("L")]
    [InlineData("1T")]
    [InlineData("L-")]
    [InlineData("")]
    public void HintNotTwoLetters_TreatedAsNoHint(string hint)
    {
        var result = VatCodeFormatValidator.Validate("120252515", hint);

        Assert.False(result.IsValid);
        Assert.Equal(VatCodeValidationReason.UnknownCountry, result.Reason);
        Assert.Equal("120252515", result.NormalizedCode); // nothing prepended
        Assert.Null(result.CountryCode);
    }

    [Fact]
    public void HintWithSpacesAndLowerCase_Accepted()
    {
        var result = VatCodeFormatValidator.Validate("120252515", " lt ");

        Assert.True(result.IsValid);
        Assert.Equal("LT120252515", result.NormalizedCode);
    }

    [Fact]
    public void PrefixDisagreesWithHint_CountryMismatch()
    {
        var result = VatCodeFormatValidator.Validate("LT120252515", "DE");

        Assert.False(result.IsValid);
        Assert.Equal(VatCodeValidationReason.CountryMismatch, result.Reason);
        Assert.Equal("LT", result.CountryCode);
        Assert.Equal("LT120252515", result.NormalizedCode);
    }

    [Fact]
    public void PrefixAgreesWithHint_Valid()
    {
        Assert.True(VatCodeFormatValidator.Validate("LT120252515", "lt").IsValid);
    }

    [Fact]
    public void MalformedCodeWithDisagreeingHint_WrongFormatWins()
    {
        Assert.Equal(VatCodeValidationReason.WrongFormat, VatCodeFormatValidator.Validate("LT12025251", "DE").Reason);
    }

    [Fact]
    public void UnsupportedPrefixWithHint_UnknownCountryWins()
    {
        Assert.Equal(VatCodeValidationReason.UnknownCountry, VatCodeFormatValidator.Validate("FR12345678901", "LT").Reason);
    }

    [Fact]
    public void InvalidHintWithPrefixedCode_PrefixDecides()
    {
        Assert.True(VatCodeFormatValidator.Validate("LT120252515", "LTU").IsValid);
    }

    [Fact]
    public void RandomInputs_NeverThrow()
    {
        var random = new Random(20260926);
        const string alphabet = "LTDEPLROEVGR0123456789 .-\u2013\u2212abcXYZ\u00c4";
        string?[] hints = { null, "", "LT", "DE", "lt", "LTU", "1", " ", "EL" };
        for (int i = 0; i < 20_000; i++)
        {
            var chars = Enumerable.Range(0, random.Next(0, 18)).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray();
            var result = VatCodeFormatValidator.Validate(new string(chars), hints[random.Next(hints.Length)]);
            Assert.Equal(result.IsValid, result.Reason is null);
        }
    }
}
