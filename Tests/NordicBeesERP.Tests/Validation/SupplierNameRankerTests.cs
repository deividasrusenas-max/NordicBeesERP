using NordicBeesERP.Services.Validation;
using Xunit;

namespace NordicBeesERP.Tests.Validation;

/// <summary>
/// OCR Etapas 2 S6 (PLAN-ETAPAS2 §1.6, D-044 Q3): suggestion ranking for the assign / change pickers. Table-driven; ranking only — the
/// ranker has no way to assign anything.
/// </summary>
public class SupplierNameRankerTests
{
    // reference values of the standard Jaro-Winkler definition
    [Theory]
    [InlineData("MARTHA", "MARHTA", 0.9611)]
    [InlineData("DWAYNE", "DUANE", 0.84)]
    [InlineData("DIXON", "DICKSONX", 0.8133)]
    [InlineData("abc", "abc", 1.0)]
    [InlineData("abc", "xyz", 0.0)]
    [InlineData("", "", 1.0)]
    [InlineData("abc", "", 0.0)]
    [InlineData("ab", "ba", 0.0)]                 // Jaro window max(len)/2 - 1 = 0: nothing matches
    [InlineData("abcdefgh", "abcdefgz", 0.95)]    // the prefix boost stops at 4 characters (would be 0.9667 at 6)
    [InlineData("abcdxxxx", "abcdyyyy", 0.6667)]  // Jaro < 0.7: no prefix boost at all
    public void JaroWinkler_MatchesTheReferenceValues(string a, string b, double expected)
    {
        Assert.Equal(expected, SupplierNameRanker.JaroWinkler(a, b), 3);
        Assert.Equal(expected, SupplierNameRanker.JaroWinkler(b, a), 3);
    }

    [Theory]
    [InlineData("UAB Rotoma", "UAB Rotoma")]
    [InlineData("UAB Rotoma", "rotoma")]                       // legal form and case
    [InlineData("Rotoma, UAB", "UAB „ROTOMA“")]                // punctuation, quotes, position of the legal form
    [InlineData("Žalias Slyvų Sodas", "zalias slyvu sodas")]   // diacritics
    [InlineData("Rotoma Vilnius", "Vilnius Rotoma")]           // token order
    [InlineData("Gintarinė Bitė MB", "GINTARINE BITE")]
    public void IdenticalNormalisedNames_ScoreExactlyOne(string a, string b)
    {
        Assert.Equal(1.0, SupplierNameRanker.Score(a, b));
        Assert.Equal(1.0, SupplierNameRanker.Score(b, a));
    }

    [Theory]
    [InlineData("UAB Rotoma", "UAB Rotoma Plius")]
    [InlineData("UAB Rotoma", "UAB Rotomax")]
    [InlineData("Rotoma Vilnius", "Rotoma Kaunas")]
    [InlineData("Medus", "Medus ir bitės")]
    public void SimilarButDifferentNames_NeverScoreAsEqual(string a, string b)
    {
        var score = SupplierNameRanker.Score(a, b);
        Assert.True(score is > 0 and < 1, $"{a} vs {b} = {score}");
        Assert.Equal(score, SupplierNameRanker.Score(b, a), 10);
    }

    [Fact]
    public void RotomaVersusRotomaPlius_TheExactOneRanksFirst_AndTheyDoNotTie()
    {
        var ordered = SupplierNameRanker.Order(new[] { "UAB Rotoma Plius", "UAB Bitininkas", "UAB Rotoma" }, n => n, "ROTOMA, UAB");
        Assert.Equal(new[] { "UAB Rotoma", "UAB Rotoma Plius", "UAB Bitininkas" }, ordered);
        Assert.NotEqual(SupplierNameRanker.Score("ROTOMA, UAB", "UAB Rotoma"), SupplierNameRanker.Score("ROTOMA, UAB", "UAB Rotoma Plius"));
    }

    [Fact]
    public void ATypoRanksAboveAnUnrelatedName()
    {
        Assert.True(SupplierNameRanker.Score("Rotoma", "Rotomaa") > SupplierNameRanker.Score("Rotoma", "Bitininkas"));
        Assert.True(SupplierNameRanker.Score("Rotoma", "Rotoma Plius") > SupplierNameRanker.Score("Rotoma", "Deltamark"));
    }

    [Theory]
    [InlineData(null, "UAB Rotoma")]
    [InlineData("", "UAB Rotoma")]
    [InlineData("   ", "UAB Rotoma")]
    [InlineData("UAB", "UAB Rotoma")]          // nothing left after the legal form is dropped
    [InlineData("UAB Rotoma", null)]
    [InlineData("Ir", "Ir")]                   // a conjunction alone says nothing
    public void AnUnusableNameScoresZero(string? a, string? b) => Assert.Equal(0.0, SupplierNameRanker.Score(a, b));

    [Fact]
    public void DuplicateTokens_AreNotTheSameSetAsTheSingleToken_ButStayClose()
    {
        var score = SupplierNameRanker.Score("Rotoma Rotoma", "Rotoma");
        Assert.True(score is > 0.5 and < 1, $"score = {score}");
    }

    [Fact]
    public void ScoresStayInTheUnitInterval()
    {
        var names = new[] { "UAB Rotoma", "Rotoma Plius", "Žalias Sodas", "x", "AB Deltamark Polska Sp. z o.o.", "Kaimo turizmo sodyba Bitė", "" };
        foreach (var a in names)
            foreach (var b in names)
                Assert.InRange(SupplierNameRanker.Score(a, b), 0.0, 1.0);
    }

    [Fact]
    public void PriorityItemsComeFirst_ThenBySimilarity_ThenByName_Deterministically()
    {
        var items = new[] { "Zeta", "Rotoma", "Alfa", "Rotoma Plius", "Beta" };
        var ordered = SupplierNameRanker.Order(items, n => n, "Rotoma", n => n is "Beta" or "Zeta");

        // the matcher's candidates (Beta, Zeta) on top although Rotoma matches better; each group by similarity, ties by name
        Assert.Equal(new[] { "Beta", "Zeta", "Rotoma", "Rotoma Plius", "Alfa" }, ordered);
        Assert.Equal(ordered, SupplierNameRanker.Order(items.Reverse(), n => n, "Rotoma", n => n is "Beta" or "Zeta"));
    }

    [Fact]
    public void WithoutADocumentName_TheOrderIsPriorityThenName()
    {
        var ordered = SupplierNameRanker.Order(new[] { "Beta", "alfa", "Gama" }, n => n, null, n => n == "Gama");
        Assert.Equal(new[] { "Gama", "alfa", "Beta" }, ordered);
    }

    [Fact]
    public void OrderKeepsEveryItem_AndNullNamesDoNotThrow()
    {
        var ordered = SupplierNameRanker.Order(new string?[] { null, "A", "B" }, n => n, "A");
        Assert.Equal(3, ordered.Count);
        Assert.Equal("A", ordered[0]);
    }
}
