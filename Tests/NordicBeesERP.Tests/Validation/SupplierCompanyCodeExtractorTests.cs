using System.Text.Json;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
using NordicBeesERP.Services.Validation;
using Xunit;

namespace NordicBeesERP.Tests.Validation;

/// <summary>
/// OCR Etapas 2 S2c (PLAN-ETAPAS2 §0.3, D-045): the supplier's company code is a registration number or empty — never a VAT
/// code (prefix included), never a name. Pure tests on synthetic text; the corpus test at the bottom is skipped when the
/// corpus (outside git, personal data) is absent.
/// </summary>
public class SupplierCompanyCodeExtractorTests
{
    private const string Own = "302905315";
    private const string OwnVat = "LT100013406816";
    private static readonly string[] Excluded = { Own, OwnVat, "LT100013406816" };

    private static CompanyCodeExtraction Extract(string? content, string? businessNumber = null, params string[] extraExcluded) =>
        SupplierCompanyCodeExtractor.Extract(businessNumber, content, Excluded.Concat(extraExcluded));

    // ---------------------------------------------------------------- labelled text

    [Theory]
    [InlineData("Įmonės kodas 123456789")]
    [InlineData("Įmonės kodas: 123456789")]
    [InlineData("Įmonės kodas:123456789")]
    [InlineData("Įm.kodas: 123456789")]
    [InlineData("Įm. kodas 123456789")]
    [InlineData("Įm. kodas:123456789")]
    [InlineData("Į. k./Reg. no. 123456789")]
    [InlineData("Reg.Nr: 123456789")]
    [InlineData("Reg. Nr. 123456789")]
    [InlineData("Reg. nr. 123456789")]
    [InlineData("Company code: 123456789")]
    [InlineData("Company registration number 123456789")]
    [InlineData("Registration no. 123456789")]
    [InlineData("REGON 123456789")]
    [InlineData("KODAS: 123456789")]      // a bare "Kodas" with exactly 9 digits (LT company code)
    [InlineData("Kodas 123456789")]
    public void LabelledText_YieldsTheRegistrationNumber(string text)
    {
        var e = Extract("UAB Foo\nAdresas Vilnius\n" + text + "\nPVM kodas LT111111111");
        Assert.Equal("123456789", e.Code);
        Assert.Equal(CompanyCodeSource.LabelledText, e.Source);
    }

    [Theory]
    [InlineData("KRS 0000123456", "0000123456")]
    [InlineData("registrikood 12345678", "12345678")]
    [InlineData("Reģ. nr. 40003123456", "40003123456")]
    [InlineData("Handelsregisternummer 1234567", "1234567")]
    public void LabelledText_OtherCountriesLabels(string text, string expected)
    {
        Assert.Equal(expected, Extract(text).Code);
    }

    [Fact]
    public void Artea_CompanyCodeAndVatAreDifferentNumbers_TheVatIsNeverReturned()
    {
        // company code 112025254, VAT LT120252515
        var e = Extract("Artea UAB Įmonės kodas 112025254 PVM kodas LT120252515");
        Assert.Equal("112025254", e.Code);
        Assert.NotEqual("LT120252515", e.Code);
        Assert.NotEqual("120252515", e.Code);
    }

    [Fact]
    public void ThePvmCodeAloneIsNotACompanyCode()
    {
        Assert.Equal(CompanyCodeSource.None, Extract("PVM kodas LT120252515").Source);
        Assert.Equal(CompanyCodeSource.None, Extract("PVM mokėtojo kodas 120252515").Source);
        Assert.Equal(CompanyCodeSource.None, Extract("PVM kodas: 120252515").Source);
    }

    [Theory]
    [InlineData("AB SEB bankas, kodas 70440")]            // bank code, 5 digits
    [InlineData("Banko kodas 123456789")]
    [InlineData("Kliento kodas 123456789")]
    [InlineData("Asmens kodas 123456789")]
    [InlineData("Kodas 12345678")]                         // a bare "Kodas" needs exactly 9 digits
    [InlineData("Kodas 1234567890")]
    [InlineData("Įmonės kodas 123456")]                    // 6 digits: below 7
    [InlineData("Įmonės kodas 123456789012345")]           // 15 digits: above 14
    [InlineData("NIP 1234567890")]                         // Poland's tax id — the VAT tier's job (D-045)
    [InlineData("NIP / PVM Nr. 1234567890")]
    [InlineData("IBAN LT121000011101001000")]
    [InlineData("Sąskaitos nr. 123456789")]
    public void NotACompanyCode(string text)
    {
        var e = Extract(text);
        Assert.Equal(string.Empty, e.Code);
        Assert.Equal(CompanyCodeSource.None, e.Source);
    }

    // review of S2c: the bare "Kodas" guard is an allowlist (a denylist of other kinds of code leaked)
    [Theory]
    [InlineData("VAT kodas 123456789")]
    [InlineData("PVM-kodas 123456789")]
    [InlineData("PVM. kodas 123456789")]
    [InlineData("PVM/kodas 123456789")]
    [InlineData("PVM identifikacinis kodas 123456789")]
    [InlineData("Prekės kodas 123456789")]
    [InlineData("Užsakymo kodas 123456789")]
    [InlineData("Pirkėjo kodas 555555555")]
    [InlineData("Pirkim kodas 123456789")]         // "im kodas" inside a word is not the "Įm. kodas" label
    public void OtherKindsOfCode_AreNotACompanyCode(string text)
    {
        Assert.Equal(CompanyCodeSource.None, Extract(text).Source);
    }

    [Theory]
    [InlineData("Pardavėjas\nKodas 123456789")]
    [InlineData("Kodas 123456789")]
    [InlineData("UAB Foo, kodas 123456789")]
    [InlineData("UAB Foo; Kodas: 123456789")]
    [InlineData("Pardavėjas:  Kodas 123456789")]
    public void BareKodas_AtALineStartOrAfterPunctuation_IsAccepted(string text)
    {
        Assert.Equal("123456789", Extract(text).Code);
    }

    [Theory]
    [InlineData("Reg. Nr. 20260927")]
    [InlineData("Reg. Nr. 2026-09-27")]
    [InlineData("Įmonės kodas 20261231")]
    public void ADate_IsNotARegistrationNumber(string text)
    {
        Assert.Equal(CompanyCodeSource.None, Extract(text).Source);
    }

    [Theory]
    [InlineData("2026-09-27")]
    [InlineData("20260927")]
    public void BusinessNumberField_ADate_IsIgnored(string field)
    {
        Assert.Equal(CompanyCodeSource.None, Extract("nothing", field).Source);
    }

    [Fact]
    public void LongWhitespaceRuns_DoNotHangOrThrow()
    {
        var text = "Įmonės kodas" + new string(' ', 200_000) + "x";
        var e = Extract(text);
        Assert.Equal(CompanyCodeSource.None, e.Source);
        Assert.Equal(CompanyCodeSource.None, Extract("Kodas" + new string('\n', 100_000) + "x").Source);
    }

    // ---------------------------------------------------------------- the buyer's codes are excluded (an invoice prints both)

    [Fact]
    public void OwnCompanyCode_IsExcluded_TheVendorsCodeRemains()
    {
        var e = Extract($"Pardavėjas Įmonės kodas 123456789\nPirkėjas MB Lakštenai Įmonės kodas {Own}");
        Assert.Equal("123456789", e.Code);
        Assert.Equal(CompanyCodeSource.LabelledText, e.Source);
    }

    [Fact]
    public void OwnCompanyCode_Only_GivesNothing()
    {
        Assert.Equal(CompanyCodeSource.None, Extract($"Pirkėjas Įmonės kodas {Own}").Source);
    }

    [Fact]
    public void BuyersVatDigits_AreExcluded()
    {
        var e = Extract("Įmonės kodas 123456789 Įmonės kodas 987654321", null, "LT987654321");
        Assert.Equal("123456789", e.Code);
    }

    [Fact]
    public void TwoDifferentVendorCandidates_AreAmbiguous_NothingIsReturned()
    {
        var e = Extract("Įmonės kodas 123456789 ... Reg. Nr. 987654321");
        Assert.Equal(string.Empty, e.Code);
        Assert.Equal(CompanyCodeSource.Ambiguous, e.Source);
    }

    [Fact]
    public void TheSameCandidateTwice_IsOneCandidate()
    {
        var e = Extract("Įmonės kodas 123456789 ... Įm. kodas: 123456789");
        Assert.Equal("123456789", e.Code);
    }

    // ---------------------------------------------------------------- VendorBusinessNumber

    [Theory]
    [InlineData("123456789", "123456789")]
    [InlineData("123 456 789", "123456789")]
    [InlineData("1234-56789", "123456789")]
    public void BusinessNumberField_APlainNumber_IsUsed(string field, string expected)
    {
        var e = Extract("no labels here", field);
        Assert.Equal(expected, e.Code);
        Assert.Equal(CompanyCodeSource.BusinessNumberField, e.Source);
    }

    [Theory]
    [InlineData("LT123456789")]        // a VAT code
    [InlineData("J40/1234/2005")]      // RO trade-register number: not a plain number
    [InlineData("UAB Foo")]
    [InlineData("12345")]
    [InlineData("")]
    [InlineData(Own)]                  // the buyer's own code
    public void BusinessNumberField_NotAPlainRegistrationNumber_IsIgnored(string field)
    {
        var e = Extract("no labels here", field);
        Assert.Equal(string.Empty, e.Code);
        Assert.Equal(CompanyCodeSource.None, e.Source);
    }

    [Fact]
    public void BusinessNumberField_BeatsTheText()
    {
        var e = Extract("Įmonės kodas 987654321", "123456789");
        Assert.Equal("123456789", e.Code);
        Assert.Equal(CompanyCodeSource.BusinessNumberField, e.Source);
    }

    // ---------------------------------------------------------------- empty input

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyContent_GivesNothing(string? content)
    {
        var e = SupplierCompanyCodeExtractor.Extract(null, content, Excluded);
        Assert.Equal(string.Empty, e.Code);
        Assert.Equal(CompanyCodeSource.None, e.Source);
    }

    [Fact]
    public void NoOwnCodeConfigured_TheBuyersCodeMakesTheResultAmbiguous_NotWrong()
    {
        // an empty own company code excludes nothing: two labelled codes stay two → nothing is returned (loud, never a wrong code)
        var e = SupplierCompanyCodeExtractor.Extract(null, $"Įmonės kodas 123456789 Įmonės kodas {Own}", new string?[] { "", null });
        Assert.Equal(string.Empty, e.Code);
        Assert.Equal(CompanyCodeSource.Ambiguous, e.Source);
    }

    // ---------------------------------------------------------------- the wrapper in ExpenseOcrService (review of S2c, mutation vii)

    private static string AzureShaped(string content, string? businessNumber = null) =>
        JsonSerializer.Serialize(new
        {
            analyzeResult = new
            {
                content,
                documents = new[]
                {
                    new
                    {
                        fields = businessNumber == null
                            ? new Dictionary<string, object> { ["VendorName"] = new { valueString = "x" } }
                            : new Dictionary<string, object> { ["VendorBusinessNumber"] = new { valueString = businessNumber } }
                    }
                }
            }
        });

    [Fact]
    public void Wrapper_ExcludesTheBuyersVatDigits_PassedAsCustomerVatCode()
    {
        using var json = JsonDocument.Parse(AzureShaped("Įmonės kodas 123456789 Įmonės kodas 987654321"));
        var settings = new CompanySettings { CompanyCode = Own, VatCode = OwnVat };

        Assert.Equal("123456789", ExpenseOcrService.ExtractSupplierCompanyCode(json.RootElement, settings, "LT987654321").Code);
        // without the buyer's VAT the two labelled numbers are two candidates → nothing is guessed
        Assert.Equal(CompanyCodeSource.Ambiguous, ExpenseOcrService.ExtractSupplierCompanyCode(json.RootElement, settings, "").Source);
    }

    [Fact]
    public void Wrapper_ExcludesOurOwnCompanyCodeAndVat_FromSettings()
    {
        using var json = JsonDocument.Parse(AzureShaped($"Įmonės kodas 123456789 Įmonės kodas {Own} Reg. Nr. 100013406816"));
        var e = ExpenseOcrService.ExtractSupplierCompanyCode(json.RootElement, new CompanySettings { CompanyCode = Own, VatCode = OwnVat }, null);
        Assert.Equal("123456789", e.Code);

        var noSettings = ExpenseOcrService.ExtractSupplierCompanyCode(json.RootElement, new CompanySettings(), null);
        Assert.Equal(CompanyCodeSource.Ambiguous, noSettings.Source);
    }

    [Fact]
    public void Wrapper_ReadsVendorBusinessNumber_FromTheDocumentFields()
    {
        using var json = JsonDocument.Parse(AzureShaped("Įmonės kodas 987654321", businessNumber: "123456789"));
        var e = ExpenseOcrService.ExtractSupplierCompanyCode(json.RootElement, new CompanySettings(), null);
        Assert.Equal("123456789", e.Code);
        Assert.Equal(CompanyCodeSource.BusinessNumberField, e.Source);
    }

    [Fact]
    public void Wrapper_ResponseWithoutDocumentsOrContent_GivesNothing_NoThrow()
    {
        using var json = JsonDocument.Parse("{\"analyzeResult\":{}}");
        Assert.Equal(CompanyCodeSource.None, ExpenseOcrService.ExtractSupplierCompanyCode(json.RootElement, new CompanySettings(), null).Source);
    }
}

/// <summary>Skipped when the corpus folder (outside git, personal data — D-038 Q9, D-041) or its own-company file is absent.</summary>
public sealed class CorpusFactAttribute : FactAttribute
{
    public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "NordicBeesERP-corpus");

    public CorpusFactAttribute()
    {
        if (!Directory.Exists(Dir) || !File.Exists(Path.Combine(Dir, "own-company.txt")) || Directory.GetFiles(Dir, "*.json").Length == 0)
            Skip = "Azure corpus (~/NordicBeesERP-corpus with own-company.txt) not present";
    }
}

/// <summary>
/// The extraction on the real Azure responses of the Etapas 2 corpus (11 documents). Numbers only — no personal data in the
/// assertions or the messages. <c>own-company.txt</c> in the corpus folder holds our own company code and VAT code.
/// </summary>
public class SupplierCompanyCodeCorpusTests
{
    [CorpusFact]
    public void Corpus_EveryExtractedCodeIsARegistrationNumber_NeverAVatCodeOrOurOwnCode()
    {
        var own = File.ReadAllLines(Path.Combine(CorpusFactAttribute.Dir, "own-company.txt"));
        var settings = new CompanySettings { CompanyCode = own[0].Trim(), VatCode = own[1].Trim() };

        var bySource = new Dictionary<CompanyCodeSource, int>();
        var documents = 0;
        foreach (var file in Directory.GetFiles(CorpusFactAttribute.Dir, "*.json"))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(file));
            var root = json.RootElement;
            var resultRoot = root.TryGetProperty("analyzeResult", out var ar) ? ar : root;
            var fields = resultRoot.GetProperty("documents")[0].GetProperty("fields");

            string Field(string name) => fields.TryGetProperty(name, out var f)
                ? (f.TryGetProperty("valueString", out var vs) ? vs.GetString() : f.TryGetProperty("content", out var c) ? c.GetString() : "") ?? ""
                : "";
            string Digits(string s) => new string(s.Where(char.IsAsciiDigit).ToArray());

            var extraction = ExpenseOcrService.ExtractSupplierCompanyCode(root, settings, Field("CustomerTaxId"));
            documents++;
            bySource[extraction.Source] = bySource.GetValueOrDefault(extraction.Source) + 1;

            if (extraction.Code.Length == 0) continue;
            Assert.InRange(extraction.Code.Length, 7, 14);
            Assert.All(extraction.Code, ch => Assert.True(char.IsAsciiDigit(ch)));
            Assert.NotEqual(settings.CompanyCode, extraction.Code);                 // not the buyer's code
            Assert.NotEqual(Digits(settings.VatCode), extraction.Code);
            var vendorVat = Field("VendorTaxId").Replace(" ", "").ToUpperInvariant();
            Assert.NotEqual(vendorVat, extraction.Code);                             // never the vendor's VAT code with prefix ...
            Assert.NotEqual(Digits(vendorVat), extraction.Code);                     // ... nor its digits (D-045: not derived from it)
        }

        Assert.Equal(11, documents);
        // the measured yield on this corpus: a code from the labelled text in 8 of 11, none from VendorBusinessNumber (absent in all 11),
        // none in 3 (no registration label with a number in the text) and no ambiguous document
        Assert.Equal(8, bySource.GetValueOrDefault(CompanyCodeSource.LabelledText));
        Assert.Equal(0, bySource.GetValueOrDefault(CompanyCodeSource.BusinessNumberField));
        Assert.Equal(3, bySource.GetValueOrDefault(CompanyCodeSource.None));
        Assert.Equal(0, bySource.GetValueOrDefault(CompanyCodeSource.Ambiguous));
    }
}
