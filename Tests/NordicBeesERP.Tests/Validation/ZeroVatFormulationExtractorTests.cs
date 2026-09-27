using System.Text.RegularExpressions;
using NordicBeesERP.Services.Validation;
using Xunit;

namespace NordicBeesERP.Tests.Validation;

/// <summary>
/// Etapas 3 S4 (PLAN-ETAPAS3.md §5, D-046 OQ-4/OQ-5, D-026): the legal-basis formulation search itself, pure —
/// synthetic text only. The full <see cref="ExpenseOcrService.ProcessAsync"/> wiring (flag translation, ULAK
/// exclusion, path parity) is tested separately in <c>ExpenseOcrServiceZeroVatTests</c>.
/// </summary>
public class ZeroVatFormulationExtractorTests
{
    // ---------- Today's real rows: every row is UNCONFIRMED (must stay true — this is the whole point of D-046 OQ-5) ----------

    [Fact]
    public void AllShippedRows_AreUnconfirmed()
    {
        Assert.False(ZeroVatFormulationExtractor.AllRowsConfirmed);
        Assert.All(ZeroVatFormulationExtractor.Rows, r => Assert.Equal(ZeroVatBasisStatus.Unconfirmed, r.Status));
    }

    [Fact]
    public void ShippedRows_CoverLtPlRoCzEs_InThatPriorityOrder_PerD046Oq5()
    {
        var order = ZeroVatFormulationExtractor.Rows.Select(r => r.CountryCode).Distinct().ToList();
        Assert.Equal(new[] { "LT", "PL", "RO", "CZ", "ES" }, order);
    }

    // ---------- The one real example (Q-009): AB Artea, "Finansinių paslaugų teikimas - PVM įstatymo 28 straipsnis, PVM5." ----------

    [Theory]
    [InlineData("Finansinių paslaugų teikimas - PVM įstatymo 28 straipsnis, PVM5.")]
    [InlineData("kažkas prieš PVM5 kažkas po")] // the bare i.SAF code alone is also a match
    public void Q009RealExample_MatchesTheLtRow_ButStaysUnconfirmedToday(string text)
    {
        var result = ZeroVatFormulationExtractor.Check(text, "LT");

        Assert.Equal(ZeroVatCheckOutcome.BasisFoundButUnconfirmed, result.Outcome);
        Assert.Equal("LT", result.CountryCode);
        Assert.NotNull(result.MatchedPattern);
    }

    [Fact]
    public void NoFormulationText_LtCountry_NoBasisFound()
    {
        var result = ZeroVatFormulationExtractor.Check("Sąskaita už prekes, jokios PVM išimties nuorodos.", "LT");

        Assert.Equal(ZeroVatCheckOutcome.NoBasisFound, result.Outcome);
        Assert.Null(result.MatchedPattern);
    }

    [Fact]
    public void UnknownCountry_NoBasisFound_RegardlessOfText()
    {
        var result = ZeroVatFormulationExtractor.Check("Finansinių paslaugų teikimas - PVM įstatymo 28 straipsnis, PVM5.", "DE");

        Assert.Equal(ZeroVatCheckOutcome.NoBasisFound, result.Outcome); // DE not in the list yet (D-046 OQ-5)
    }

    [Fact]
    public void NullOrEmptyCountry_NoBasisFound()
    {
        Assert.Equal(ZeroVatCheckOutcome.NoBasisFound, ZeroVatFormulationExtractor.Check("PVM5", null).Outcome);
        Assert.Equal(ZeroVatCheckOutcome.NoBasisFound, ZeroVatFormulationExtractor.Check("PVM5", "").Outcome);
    }

    [Fact]
    public void NullOrEmptyText_NoBasisFound()
    {
        Assert.Equal(ZeroVatCheckOutcome.NoBasisFound, ZeroVatFormulationExtractor.Check(null, "LT").Outcome);
        Assert.Equal(ZeroVatCheckOutcome.NoBasisFound, ZeroVatFormulationExtractor.Check("", "LT").Outcome);
    }

    // ---------- Other listed countries' UNCONFIRMED reverse-charge phrases ----------

    [Theory]
    [InlineData("PL", "Faktura z odwrotne obciążenie")]
    [InlineData("RO", "Se aplică taxare inversă")]
    [InlineData("CZ", "Uplatní se přenesení daňové povinnosti")]
    [InlineData("ES", "Aplica la inversión del sujeto pasivo")]
    public void OtherCountries_ReverseChargePhrase_FoundButUnconfirmed(string country, string text)
    {
        var result = ZeroVatFormulationExtractor.Check(text, country);

        Assert.Equal(ZeroVatCheckOutcome.BasisFoundButUnconfirmed, result.Outcome);
    }

    // ---------- Confirmed-row behaviour (synthetic row set — proves the mechanism ahead of any real confirmation) ----------

    private static readonly IReadOnlyList<ZeroVatFormulationRow> ConfirmedLt = new[]
    {
        new ZeroVatFormulationRow("LT", ZeroVatBasisStatus.Confirmed, "test fixture",
            new[] { new Regex(@"PVM\s?\d", RegexOptions.IgnoreCase) })
    };

    [Fact]
    public void ConfirmedCountry_PatternMatches_ClosesTheFlag()
    {
        var result = ZeroVatFormulationExtractor.Check("... PVM5 ...", "LT", ConfirmedLt);

        Assert.Equal(ZeroVatCheckOutcome.BasisConfirmedFound, result.Outcome);
    }

    [Fact]
    public void ConfirmedCountry_NoPatternMatches_IsARealActionableReview()
    {
        var result = ZeroVatFormulationExtractor.Check("Sąskaita be jokios nuorodos", "LT", ConfirmedLt);

        Assert.Equal(ZeroVatCheckOutcome.ConfirmedNoBasisFound, result.Outcome);
    }

    [Fact]
    public void CountryConfirmedOnlyWhenEveryRowForItIs()
    {
        // LT has two real rows (Q-009's shape + D-026's Directive-226 shape) — both UNCONFIRMED today, so even a
        // partial "confirm just one row" state must not start driving real REVIEW status.
        var partiallyConfirmed = new[]
        {
            new ZeroVatFormulationRow("LT", ZeroVatBasisStatus.Confirmed, "confirmed row", new[] { new Regex("XXXX") }),
            new ZeroVatFormulationRow("LT", ZeroVatBasisStatus.Unconfirmed, "still unconfirmed row", new[] { new Regex("YYYY") })
        };

        var result = ZeroVatFormulationExtractor.Check("no match here at all", "LT", partiallyConfirmed);

        Assert.Equal(ZeroVatCheckOutcome.NoBasisFound, result.Outcome); // NOT ConfirmedNoBasisFound
    }
}
