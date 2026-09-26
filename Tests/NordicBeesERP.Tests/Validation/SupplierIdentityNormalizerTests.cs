using NordicBeesERP.Services.Validation;
using Xunit;

namespace NordicBeesERP.Tests.Validation;

/// <summary>OCR Etapas 2 S2a (PLAN-ETAPAS2 §1.2, D-044, D-045): the pure identifier normaliser. No I/O.</summary>
public class SupplierIdentityNormalizerTests
{
    // ---------------------------------------------------------------- VAT

    [Theory]
    [InlineData("LT120252515", null, "LT120252515", true)]
    [InlineData("lt 120252515", null, "LT120252515", true)]
    [InlineData("LT-120.252.515", null, "LT120252515", true)]
    [InlineData("LT–120252515", null, "LT120252515", true)]   // en dash the OCR produces for a hyphen
    [InlineData("  LT 120 252 515  ", "DE", "LT120252515", true)]  // an own prefix always beats the hint
    [InlineData("IE8256796U", null, "IE8256796U", true)]
    public void Vat_PrefixedCode_KeepsThePrefix(string raw, string? hint, string expected, bool hasPrefix)
    {
        var vat = SupplierIdentityNormalizer.Vat(raw, hint);
        Assert.Equal(expected, vat.Normalized);
        Assert.Equal(hasPrefix, vat.HasPrefix);
    }

    [Theory]
    [InlineData("120252515", "LT", "LT120252515")]
    [InlineData("120252515", "lt", "LT120252515")]
    [InlineData("123 456 789", "DE", "DE123456789")]
    [InlineData("094259216", "GR", "EL094259216")]    // Greece's VAT prefix is EL
    public void Vat_PrefixLess_UsesAKnownCountryHint(string raw, string hint, string expected)
    {
        var vat = SupplierIdentityNormalizer.Vat(raw, hint);
        Assert.Equal(expected, vat.Normalized);
        Assert.True(vat.HasPrefix);
    }

    [Theory]
    [InlineData("120252515", null)]
    [InlineData("120252515", "")]
    [InlineData("120252515", "  ")]
    [InlineData("120252515", "LTU")]     // not an ISO alpha-2 code
    [InlineData("120252515", "XX")]      // not a country
    [InlineData("120252515", "Lietuva")]
    public void Vat_PrefixLess_WithoutAUsableCountry_StaysPrefixLess_NoLtAssumption(string raw, string? hint)
    {
        var vat = SupplierIdentityNormalizer.Vat(raw, hint);
        Assert.Equal("120252515", vat.Normalized);
        Assert.False(vat.HasPrefix);
        Assert.NotEqual("LT120252515", vat.Normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-.")]
    public void Vat_Empty_IsNull(string? raw)
    {
        Assert.Null(SupplierIdentityNormalizer.Vat(raw, "LT").Normalized);
    }

    // ---------------------------------------------------------------- company code

    [Theory]
    [InlineData("112025254", "112025254")]
    [InlineData("1120 25254", "112025254")]
    [InlineData("11-202.5254", "112025254")]
    [InlineData("hrb 12345", "HRB12345")]                // a 3-letter register prefix is not a VAT prefix
    public void CompanyCode_LettersAndDigitsOnly(string raw, string expected)
    {
        Assert.Equal(expected, SupplierIdentityNormalizer.CompanyCode(raw));
    }

    [Theory]
    [InlineData("LT120252515")]     // a prefixed VAT code is not a registration number
    [InlineData("lt 120 252 515")]
    [InlineData("DE123456789")]
    [InlineData("PL1234567890")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("UAB Rotoma")]                           // a name has no digit
    [InlineData("...")]
    public void CompanyCode_VatLookalikesNamesAndEmpty_AreNull(string? raw)
    {
        Assert.Null(SupplierIdentityNormalizer.CompanyCode(raw));
    }

    [Fact]
    public void CompanyCode_IsNeverDerivedFromTheVatCode_Artea()
    {
        // Artea prints company code 112025254 and VAT LT120252515: neither is the other with a prefix added or removed
        var code = SupplierIdentityNormalizer.CompanyCode("112025254");
        var vat = SupplierIdentityNormalizer.Vat("LT120252515").Normalized;
        Assert.NotEqual("LT" + code, vat);
        Assert.NotEqual(code, vat!["LT".Length..]);
    }

    // ---------------------------------------------------------------- IBAN

    [Theory]
    [InlineData("LT12 1000 0111 0100 1000", "LT121000011101001000")]
    [InlineData("lt121000011101001000", "LT121000011101001000")]
    [InlineData("DE89 3704 0044 0532 0130 00", "DE89370400440532013000")]
    [InlineData("GB82WEST12345698765432", "GB82WEST12345698765432")]
    public void Iban_Valid_IsNormalised(string raw, string expected)
    {
        Assert.Equal(expected, SupplierIdentityNormalizer.Iban(raw));
    }

    [Theory]
    [InlineData("LT121000011101001001")]   // checksum broken
    [InlineData("LT12100001110100100")]    // too short
    [InlineData("DE89 3704 0044 0532 0130 0X")]
    [InlineData("not an iban")]
    [InlineData(null)]
    [InlineData("")]
    public void Iban_Invalid_IsNull_NeverAKey(string? raw)
    {
        Assert.Null(SupplierIdentityNormalizer.Iban(raw));
    }

    // ---------------------------------------------------------------- name, exact

    [Theory]
    [InlineData("UAB Rotoma", "uab rotoma")]
    [InlineData("  UAB   ROTOMA  ", "uab rotoma")]
    [InlineData("UAB Žūklinė", "uab zukline")]
    [InlineData("Müller GmbH", "muller gmbh")]
    [InlineData("Straße", "strasse")]
    [InlineData("UAB „Rotoma“", "uab „rotoma“")]      // punctuation is kept in the exact key
    public void NameExact_FoldsCaseAndDiacritics_OnlyThat(string raw, string expected)
    {
        Assert.Equal(expected, SupplierIdentityNormalizer.NameExact(raw));
    }

    [Fact]
    public void NameExact_DoesNotMergeLegalForms()
    {
        Assert.NotEqual(SupplierIdentityNormalizer.NameExact("UAB Rotoma"), SupplierIdentityNormalizer.NameExact("AB Rotoma"));
        Assert.NotEqual(SupplierIdentityNormalizer.NameExact("UAB Rotoma"), SupplierIdentityNormalizer.NameExact("Rotoma"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NameExact_Empty_IsEmptyString(string? raw)
    {
        Assert.Equal(string.Empty, SupplierIdentityNormalizer.NameExact(raw));
    }

    // ---------------------------------------------------------------- name, normalised

    [Theory]
    [InlineData("UAB Rotoma", "rotoma")]
    [InlineData("UAB „Rotoma“", "rotoma")]
    [InlineData("UAB \"Rotoma\"", "rotoma")]
    [InlineData("Rotoma, UAB", "rotoma")]
    [InlineData("ROTOMA", "rotoma")]
    [InlineData("  Rotoma  UAB.", "rotoma")]
    [InlineData("MB „Žūklinė“", "zukline")]
    [InlineData("IĮ Jonas Jonaitis", "jonas jonaitis")]
    [InlineData("VšĮ Gerumo namai", "gerumo namai")]
    [InlineData("ŽŪB Ąžuolas", "azuolas")]
    [InlineData("SIA Rimi Latvia", "rimi latvia")]
    [InlineData("Rimi Eesti Food AS", "rimi eesti food")]
    [InlineData("OÜ Nordic Hotels", "nordic hotels")]
    [InlineData("Müller GmbH", "muller")]
    [InlineData("Deutsche Post AG", "deutsche post")]
    [InlineData("Artea Sp. z o.o.", "artea")]
    [InlineData("Artea sp. z o. o.", "artea")]
    [InlineData("Firma S.R.L.", "firma")]
    [InlineData("Firma S.A.", "firma")]
    [InlineData("ТОВ Ромашка", "ромашка")]
    public void NameNormalized_StripsLegalFormsQuotesAndPunctuation(string raw, string expected)
    {
        Assert.Equal(expected, SupplierIdentityNormalizer.NameNormalized(raw));
    }

    [Theory]
    [InlineData("Smith & Sons", "smith and sons")]
    [InlineData("Smith and Sons", "smith and sons")]
    [InlineData("Smith und Söhne", "smith and sohne")]
    [InlineData("Pjūvis ir Ko", "pjuvis and ko")]
    [InlineData("Pjuvis și Ko", "pjuvis and ko")]
    public void NameNormalized_UnifiesTheConjunction(string raw, string expected)
    {
        Assert.Equal(expected, SupplierIdentityNormalizer.NameNormalized(raw));
    }

    [Fact]
    public void NameNormalized_LegalFormsAreStrippedOnlyFromTheEnds()
    {
        // "as" in the middle of a name is a word, not a legal form
        Assert.Equal("nordic as hotels", SupplierIdentityNormalizer.NameNormalized("Nordic AS Hotels"));
        Assert.Equal("rotoma group", SupplierIdentityNormalizer.NameNormalized("AB Rotoma Group"));
    }

    [Fact]
    public void NameNormalized_NamesThatDifferOnlyInLegalForm_BecomeEqual()
    {
        // ... which is exactly why this key only ever suggests (D-044 Q1)
        Assert.Equal(SupplierIdentityNormalizer.NameNormalized("UAB Rotoma"), SupplierIdentityNormalizer.NameNormalized("AB Rotoma"));
    }

    [Theory]
    [InlineData("UAB")]
    [InlineData("UAB AB")]
    [InlineData("Sp. z o.o.")]
    [InlineData("„“")]
    [InlineData("")]
    [InlineData(null)]
    public void NameNormalized_NothingLeft_IsEmpty_SoItCanNeverMatch(string? raw)
    {
        Assert.Equal(string.Empty, SupplierIdentityNormalizer.NameNormalized(raw));
    }
}
