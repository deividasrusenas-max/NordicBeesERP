using NordicBeesERP.Services.Validation;
using Xunit;

namespace NordicBeesERP.Tests.Validation;

/// <summary>
/// OCR Etapas 2 S5 (D-017, D-044 Q4): the alias tier of the pure matcher. An ACTIVE alias assigns after tiers 1, 2 and 4 and never
/// overrides a document VAT / company code that contradicts the partner's; an alias of an ineligible partner only suggests.
/// </summary>
public class SupplierMatcherAliasTests
{
    private static SupplierCandidate P(int id, string name, string? vat = null, string? code = null, bool active = true, bool supplier = true) =>
        new(id, name, vat, code, "LT", Array.Empty<string>(), active, supplier);

    private static SupplierDocument Doc(string? name, string? vat = null, string? code = null, string? iban = null) =>
        new(name, vat, code, iban, null);

    private static ActiveAlias Alias(string rawName, int partnerId) => new(SupplierIdentityNormalizer.NameNormalized(rawName), partnerId);

    private static SupplierMatch Match(SupplierDocument doc, IReadOnlyList<ActiveAlias>? aliases, params SupplierCandidate[] candidates) =>
        SupplierMatcher.Match(doc, candidates, aliases);

    [Fact]
    public void ActiveAlias_AssignsWhenNothingStrongerMatches()
    {
        var m = Match(Doc("ROTOMA, UAB Vilnius"), new[] { Alias("ROTOMA, UAB Vilnius", 7) }, P(7, "Bičių medus Rotoma"), P(8, "Kitas"));
        Assert.Equal(MatchOutcome.Assigned, m.Outcome);
        Assert.Equal(7, m.PartnerId);
        Assert.Equal(MatchTier.Alias, m.Tier);
        Assert.Equal(MatchReason.None, m.Reason);
    }

    [Fact]
    public void WithoutAliases_TheSameDocumentIsNotFound()
    {
        Assert.Equal(MatchOutcome.NotFound, Match(Doc("ROTOMA, UAB Vilnius"), null, P(7, "Bičių medus Rotoma")).Outcome);
        Assert.Equal(MatchOutcome.NotFound, Match(Doc("ROTOMA, UAB Vilnius"), Array.Empty<ActiveAlias>(), P(7, "Bičių medus Rotoma")).Outcome);
    }

    [Fact]
    public void AliasIsKeyedByTheNormalisedName_LegalFormPunctuationAndCaseDoNotMatter()
    {
        var aliases = new[] { Alias("UAB „Rotoma Vilnius“", 7) };
        Assert.Equal(7, Match(Doc("rotoma vilnius, UAB"), aliases, P(7, "Kitas pavadinimas")).PartnerId);
        Assert.Equal(MatchOutcome.NotFound, Match(Doc("Rotoma Kaunas"), aliases, P(7, "Kitas pavadinimas")).Outcome);
    }

    [Fact]
    public void Alias_NeverOverridesAContradictingDocumentVat()
    {
        var m = Match(Doc("Rotoma Vilnius", vat: "LT999999999"), new[] { Alias("Rotoma Vilnius", 7) },
            P(7, "Kitas pavadinimas", vat: "LT111111111"));
        Assert.Equal(MatchOutcome.Suggested, m.Outcome);
        Assert.Null(m.PartnerId);
        Assert.Equal(MatchTier.Alias, m.Tier);
        Assert.Equal(MatchReason.ConflictingIdentifier, m.Reason);
        Assert.Equal(new[] { 7 }, m.CandidateIds);
    }

    [Fact]
    public void Alias_NeverOverridesAContradictingCompanyCode()
    {
        var m = Match(Doc("Rotoma Vilnius", code: "999999999"), new[] { Alias("Rotoma Vilnius", 7) },
            P(7, "Kitas pavadinimas", code: "111111111"));
        Assert.Equal(MatchOutcome.Suggested, m.Outcome);
        Assert.Equal(MatchReason.ConflictingIdentifier, m.Reason);
    }

    [Fact]
    public void Alias_PartnerWithoutCodes_IsNotAContradiction()
    {
        var aliases = new[] { Alias("Rotoma Vilnius", 7) };
        Assert.Equal(MatchTier.Alias, Match(Doc("Rotoma Vilnius", vat: "LT111111111"), aliases, P(7, "X")).Tier);
        Assert.Equal(MatchTier.Alias, Match(Doc("Rotoma Vilnius", code: "111111111"), aliases, P(7, "X")).Tier);
        // an agreeing code is a tier-1 / tier-2 match, not an alias one
        Assert.Equal(MatchTier.Vat, Match(Doc("Rotoma Vilnius", vat: "LT111111111"), aliases, P(7, "X", vat: "lt 111111111")).Tier);
    }

    [Fact]
    public void StrongerTiersWin_VatAndCompanyCodeAndExactName_BeforeTheAlias()
    {
        var aliases = new[] { Alias("Rotoma Vilnius", 7) };
        // VAT points at partner 8: the alias for 7 is not consulted
        var byVat = Match(Doc("Rotoma Vilnius", vat: "LT222222222"), aliases, P(7, "X"), P(8, "Y", vat: "LT222222222"));
        Assert.Equal(8, byVat.PartnerId);
        Assert.Equal(MatchTier.Vat, byVat.Tier);
        // an exact-name partner beats the alias
        var byName = Match(Doc("Rotoma Vilnius"), aliases, P(7, "X"), P(9, "Rotoma Vilnius"));
        Assert.Equal(9, byName.PartnerId);
        Assert.Equal(MatchTier.Name, byName.Tier);
    }

    [Fact]
    public void AliasOfAnIneligiblePartner_OnlySuggests()
    {
        foreach (var partner in new[] { P(7, "X", active: false), P(7, "X", supplier: false) })
        {
            var m = Match(Doc("Rotoma Vilnius"), new[] { Alias("Rotoma Vilnius", 7) }, partner);
            Assert.Equal(MatchOutcome.Suggested, m.Outcome);
            Assert.Equal(MatchReason.PartnerNotEligible, m.Reason);
            Assert.Null(m.PartnerId);
        }
    }

    [Fact]
    public void TwoActiveAliasesForOneKey_AreAmbiguous_NeverAPick()
    {
        var m = Match(Doc("Rotoma Vilnius"), new[] { Alias("Rotoma Vilnius", 7), Alias("Rotoma Vilnius", 8) }, P(7, "X"), P(8, "Y"));
        Assert.Equal(MatchOutcome.Ambiguous, m.Outcome);
        Assert.Null(m.PartnerId);
        Assert.Equal(MatchTier.Alias, m.Tier);
        Assert.Equal(new[] { 7, 8 }.OrderBy(x => x), m.CandidateIds.OrderBy(x => x));
    }

    [Fact]
    public void AliasOfAPartnerThatNoLongerExists_FallsThrough()
    {
        var m = Match(Doc("Rotoma Vilnius"), new[] { Alias("Rotoma Vilnius", 99) }, P(7, "X"));
        Assert.Equal(MatchOutcome.NotFound, m.Outcome);
    }

    [Fact]
    public void AliasAssignment_StillChecksTheDocumentIban()
    {
        const string known = "LT121000011101001000";
        var candidate = new SupplierCandidate(7, "X", null, null, "LT", new[] { known }, true, true);
        var m = Match(Doc("Rotoma Vilnius", iban: "DE89370400440532013000"), new[] { Alias("Rotoma Vilnius", 7) }, candidate);
        Assert.Equal(MatchTier.Alias, m.Tier);
        Assert.False(m.DocumentIbanKnown);
        Assert.Equal(1, m.PartnerKnownIbanCount);
    }

    [Fact]
    public void EmptyDocumentName_NeverMatchesAnAlias()
    {
        Assert.Equal(MatchOutcome.NotFound, Match(Doc(null), new[] { new ActiveAlias("", 7) }, P(7, "X")).Outcome);
        Assert.Equal(MatchOutcome.NotFound, Match(Doc("UAB"), new[] { new ActiveAlias("", 7) }, P(7, "X")).Outcome);
    }
}
