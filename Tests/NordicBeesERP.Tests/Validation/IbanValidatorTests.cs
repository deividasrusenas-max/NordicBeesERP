using NordicBeesERP.Services.Validation;
using Xunit;

namespace NordicBeesERP.Tests.Validation;

/// <summary>
/// Pure unit tests for IbanValidator (normalisation, per-country length, ISO 13616 mod-97).
/// No DB involvement — static validation only.
/// </summary>
public class IbanValidatorTests
{
    [Fact]
    public void KnownValidGermanIban_Valid()
    {
        var result = IbanValidator.Validate("DE89370400440532013000");

        Assert.True(result.IsValid);
        Assert.Null(result.Reason);
        Assert.Equal("DE", result.CountryCode);
        Assert.Equal("DE89370400440532013000", result.NormalizedIban);
    }

    [Fact]
    public void KnownValidBritishIban_Valid()
    {
        var result = IbanValidator.Validate("GB82WEST12345698765432");

        Assert.True(result.IsValid);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void ChangedLastDigit_ChecksumFailed()
    {
        var result = IbanValidator.Validate("DE89370400440532013001");

        Assert.False(result.IsValid);
        Assert.Equal(IbanValidationReason.ChecksumFailed, result.Reason);
    }

    [Fact]
    public void LowercaseWithSpaces_NormalizedAndValid()
    {
        var result = IbanValidator.Validate("de89 3704 0044 0532 0130 00");

        Assert.True(result.IsValid);
        Assert.Equal("DE89370400440532013000", result.NormalizedIban);
    }

    [Fact]
    public void LithuanianIbanTooShort_WrongLength()
    {
        var result = IbanValidator.Validate("LT87718990000691025"); // 19 chars, LT needs 20

        Assert.False(result.IsValid);
        Assert.Equal(IbanValidationReason.WrongLength, result.Reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullOrWhitespace_Empty(string? input)
    {
        var result = IbanValidator.Validate(input);

        Assert.False(result.IsValid);
        Assert.Equal(IbanValidationReason.Empty, result.Reason);
        Assert.Null(result.NormalizedIban);
    }

    [Fact]
    public void LithuanianIbanWithSpaces_Valid()
    {
        var result = IbanValidator.Validate("LT87 7189 9000 0691 0250");

        Assert.True(result.IsValid);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void HyphenCaughtBeforeLengthCheck_BadCharacters()
    {
        // Stripped length is 21 (LT needs 20), but the hyphen must be caught first.
        var result = IbanValidator.Validate("LT87 7189 9000 0691 025-");

        Assert.False(result.IsValid);
        Assert.Equal(IbanValidationReason.BadCharacters, result.Reason);
    }

    [Fact]
    public void UnknownCountryBadChecksum_ChecksumFailed()
    {
        var result = IbanValidator.Validate("XX877189900006910250"); // unknown country XX, mod-97 = 24

        Assert.False(result.IsValid);
        Assert.Equal(IbanValidationReason.ChecksumFailed, result.Reason);
    }

    [Fact]
    public void UnknownCountryGoodChecksum_ValidWithWarning()
    {
        var result = IbanValidator.Validate("NO3586009999999"); // unknown country NO, 15 chars, mod-97 = 1

        Assert.True(result.IsValid);
        Assert.Null(result.Reason);
        Assert.Contains(IbanValidationReason.UnknownCountryLength, result.Warnings);
    }
}
